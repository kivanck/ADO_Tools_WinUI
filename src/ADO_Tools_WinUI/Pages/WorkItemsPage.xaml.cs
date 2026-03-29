using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using ADO_Tools_WinUI.Models;
using ADO_Tools_WinUI.Services;

namespace ADO_Tools_WinUI.Pages
{
    public sealed partial class WorkItemsPage : Page
    {
        private enum ListMode { Query, SearchQuery, SearchBacklog, Compare }

        private TfsRestClient? _tfsRest;
        private List<QueryDto> _queryTree = new();
        private List<QueryDto> _queryListFlat = new();
        private List<WorkItemDto> _workItemList = new();
        private readonly ObservableCollection<WorkItemRow> _rows = new();
        private SemanticSearchService? _semanticSearch;
        private Bm25SearchService? _bm25BacklogSearch;
        private Bm25SearchService? _bm25QuerySearch;
        private QuerySearchCache? _querySearchCache;
        private ListMode _listMode = ListMode.Query;
        private string _lastQueryName = "";
        private string _lastSearchQuery = "";
        private string _lastCompareSource = "";
        private List<string> _queryColumns = [];
        private bool _initialized;

        // Friendly display names for ADO field reference names
        private static readonly Dictionary<string, string> FieldDisplayNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["System.Id"] = "ID",
            ["System.Title"] = "Title",
            ["System.State"] = "State",
            ["System.CreatedBy"] = "Created By",
            ["System.CreatedDate"] = "Date",
            ["System.WorkItemType"] = "Type",
            ["System.IterationPath"] = "Iteration",
            ["System.AreaPath"] = "Area Path",
            ["System.AssignedTo"] = "Assigned To",
            ["System.ChangedDate"] = "Changed",
            ["System.ChangedBy"] = "Changed By",
            ["System.Reason"] = "Reason",
            ["System.Tags"] = "Tags",
            ["Microsoft.VSTS.Common.Priority"] = "Priority",
            ["Microsoft.VSTS.Common.Severity"] = "Severity",
            ["Microsoft.VSTS.Common.Activity"] = "Activity",
            ["Microsoft.VSTS.Scheduling.Effort"] = "Effort",
            ["Microsoft.VSTS.Scheduling.StoryPoints"] = "Story Points",
            ["Microsoft.VSTS.Scheduling.RemainingWork"] = "Remaining",
            ["Microsoft.VSTS.Common.ValueArea"] = "Value Area",
        };

        private static readonly List<string> DefaultColumns =
            ["System.Id", "System.Title", "System.State",
             "System.CreatedBy", "System.CreatedDate",
             "System.WorkItemType", "System.IterationPath"];

        public WorkItemsPage()
        {
            InitializeComponent();
            Loaded += WorkItemsPage_Loaded;
        }

        private async void WorkItemsPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized) return;
            _initialized = true;

            var s = AppSettings.Default;
            txtProjectName.Text = string.IsNullOrWhiteSpace(s.WorkItemProject) ? s.Project : s.WorkItemProject;
            dataGridWorkItems.ItemsSource = _rows;

            await TryLoadExistingCacheAsync();
            await TryAutoConnectAsync();
        }

        private async Task TryLoadExistingCacheAsync()
        {
            var settings = AppSettings.Default;
            string project = string.IsNullOrWhiteSpace(settings.WorkItemProject) ? settings.Project : settings.WorkItemProject;
            if (string.IsNullOrWhiteSpace(settings.Organization) || string.IsNullOrWhiteSpace(project))
                return;

            string modelDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Models");
            if (!File.Exists(Path.Combine(modelDir, "model.onnx")))
                return;

            lblCacheStatus.Text = "Loading search index…";

            try
            {
                string cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ADO_Tools_WinUI", "EmbeddingCache");
                string areaPath = settings.SearchAreaPath?.Trim() ?? "";
                string org = settings.Organization;

                // Heavy work: ONNX model load, JSON deserialization, BM25 index build
                var result = await Task.Run(() =>
                {
                    var search = new SemanticSearchService(modelDir, cacheDir);
                    if (search.TryLoadCache(org, project, areaPath))
                    {
                        var bm25 = new Bm25SearchService();
                        bm25.BuildIndex(search.GetCacheEntries(false));
                        return (Search: search, Bm25: bm25, Loaded: true);
                    }
                    else
                    {
                        search.Dispose();
                        return (Search: (SemanticSearchService?)null, Bm25: (Bm25SearchService?)null, Loaded: false);
                    }
                });

                if (result.Loaded)
                {
                    _semanticSearch?.Dispose();
                    _semanticSearch = result.Search;
                    _bm25BacklogSearch = result.Bm25;

                    txtSemanticSearch.IsEnabled = true;
                    btnFindSimilar.IsEnabled = true;
                    lblCacheStatus.Text = FormatCacheLabel($"{_semanticSearch!.CachedItemCount} items in cache");
                }
                else
                {
                    lblCacheStatus.Text = "No cache found. Go to Settings to build the search index.";
                }
            }
            catch
            {
                lblCacheStatus.Text = "Failed to load search index.";
            }
        }

        private async Task TryAutoConnectAsync()
        {
            var settings = AppSettings.Default;
            string project = string.IsNullOrWhiteSpace(settings.WorkItemProject) ? settings.Project : settings.WorkItemProject;

            if (string.IsNullOrWhiteSpace(settings.Organization)
                || string.IsNullOrWhiteSpace(settings.PersonalAccessToken)
                || string.IsNullOrWhiteSpace(project))
                return;

            lblConnectionStatus.Text = "Connecting\u2026";

            try
            {
                _tfsRest = new TfsRestClient(settings.Organization, project, settings.PersonalAccessToken);
                _queryTree = await _tfsRest.GetQueriesAsync();
                _queryListFlat = TfsRestClient.FlattenQueries(_queryTree);

                PopulateQueryTree();

                btnReadItems.IsEnabled = true;
                btnDownloadSelected.IsEnabled = true;
                btnDownloadSingle.IsEnabled = true;
                btnFindSimilar.IsEnabled = _semanticSearch != null && _semanticSearch.IsReady;
                btnUpdateIndex.IsEnabled = true;
                lblConnectionStatus.Text = "Connected";
            }
            catch (HttpRequestException ex)
            {
                _tfsRest = null;
                lblConnectionStatus.Text = $"Could not reach the server. Check your internet connection or organization URL. ({ex.Message})";
            }
            catch (Exception ex)
            {
                _tfsRest = null;
                lblConnectionStatus.Text = $"Auto-connect failed: {ex.Message}";
            }
        }

        // ?? View Model Row

        public class WorkItemRow
        {
            public int Id { get; set; }
            public string Title { get; set; } = "";
            public string State { get; set; } = "";
            public string CreatedBy { get; set; } = "";
            public string CreatedDateShort { get; set; } = "";
            public string TypeName { get; set; } = "";
            public string IterationShort { get; set; } = "";
            public DateTime CreatedDate { get; set; }
            public bool Downloaded { get; set; }
            public Brush? RowBackground { get; set; }
            public string HtmlUrl { get; set; } = "";

            /// <summary>
            /// All raw field values from the work item, keyed by reference name.
            /// Used for dynamic column display.
            /// </summary>
            public Dictionary<string, string> FieldValues { get; set; } = [];
        }

        // ?? Helpers ?????????????????????????????????????????????????????

        private void UpdateContextBadge()
        {
            switch (_listMode)
            {
                case ListMode.Query:
                    badgeIcon.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 212));
                    badgeGlyph.Glyph = "\uE8A7";
                    lblContextBadge.Text = string.IsNullOrEmpty(_lastQueryName)
                        ? "Query Results"
                        : $"Query: {_lastQueryName}";
                    break;
                case ListMode.SearchQuery:
                    badgeIcon.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 151, 110));
                    badgeGlyph.Glyph = "\uE721";
                    lblContextBadge.Text = string.IsNullOrEmpty(_lastSearchQuery)
                        ? "Query Search Results"
                        : $"Search in \u201c{_lastQueryName}\u201d: \u201c{_lastSearchQuery}\u201d";
                    break;
                case ListMode.SearchBacklog:
                    badgeIcon.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 116, 77, 169));
                    badgeGlyph.Glyph = "\uE721";
                    lblContextBadge.Text = string.IsNullOrEmpty(_lastSearchQuery)
                        ? "Backlog Search Results"
                        : $"Backlog Search: \u201c{_lastSearchQuery}\u201d";
                    break;
                case ListMode.Compare:
                    badgeIcon.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 202, 80, 16));
                    badgeGlyph.Glyph = "\uE721";
                    lblContextBadge.Text = string.IsNullOrEmpty(_lastCompareSource)
                        ? "Compare Results"
                        : $"Similar to: {_lastCompareSource}";
                    break;
            }
        }

        private QueryDto? _restoredQuery;

        private void PopulateQueryTree()
        {
            treeQueries.RootNodes.Clear();
            _restoredQuery = null;
            foreach (var root in _queryTree)
            {
                var node = BuildTreeNode(root);
                // Expand Favorites by default, collapse All Queries
                if (root.Name.Contains("Favorites"))
                    node.IsExpanded = true;
                treeQueries.RootNodes.Add(node);
            }

            // Restore previously selected query
            string savedPath = AppSettings.Default.SelectedQueryPath;
            if (!string.IsNullOrEmpty(savedPath))
            {
                var match = _queryListFlat.FirstOrDefault(q => q.Path == savedPath);
                if (match != null)
                {
                    SelectNodeByQuery(treeQueries.RootNodes, match);
                    CollapseQueryTree(match);
                    _restoredQuery = match;
                }
            }
        }

        private static bool SelectNodeByQuery(IList<TreeViewNode> nodes, QueryDto target)
        {
            foreach (var node in nodes)
            {
                if (node.Content is QueryDto q && q.Path == target.Path)
                    return true;

                if (SelectNodeByQuery(node.Children, target))
                {
                    node.IsExpanded = true;
                    return true;
                }
            }
            return false;
        }

        private static TreeViewNode BuildTreeNode(QueryDto dto)
        {
            var node = new TreeViewNode
            {
                Content = dto,
                IsExpanded = false
            };
            if (dto.IsFolder)
            {
                foreach (var child in dto.Children)
                    node.Children.Add(BuildTreeNode(child));
            }
            return node;
        }

        private void TreeQueries_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            // Get the selected query
            var selected = treeQueries.SelectedItem;
            var q = selected as QueryDto
                ?? (selected as TreeViewNode)?.Content as QueryDto;
            if (q == null || q.IsFolder) return;

            // Collapse tree and show selected query name
            CollapseQueryTree(q);
        }

        private void LblSelectedQuery_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            // Expand tree again for query selection
            lblSelectedQuery.Visibility = Visibility.Collapsed;
            treeQueries.Visibility = Visibility.Visible;
        }

        private void CollapseQueryTree(QueryDto q)
        {
            lblSelectedQuery.Text = $"\u25B6 {q.Name}";
            lblSelectedQuery.Visibility = Visibility.Visible;
            treeQueries.Visibility = Visibility.Collapsed;

            // Persist the selection
            var s = AppSettings.Default;
            s.SelectedQueryPath = q.Path;
            s.Save();
        }

        private void PersistSettings()
        {
            var s = AppSettings.Default;
            s.WorkItemProject = txtProjectName.Text.Trim();
            s.Save();
        }

        private async void ShowMessage(string message, string title = "")
        {
            await new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
        }

        private string ReadTopFolder()
        {
            string folder = AppSettings.Default.RootFolder.Trim();
            if (!Directory.Exists(folder))
                folder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (!folder.EndsWith(Path.DirectorySeparatorChar))
                folder += Path.DirectorySeparatorChar;
            return folder;
        }

        private static string CreateFolder(string topFolder, string subFolder)
        {
            string path = Path.Combine(topFolder, subFolder);
            Directory.CreateDirectory(path);
            return path;
        }

        private static string RemoveIllegalCharacters(string input)
        {
            string pattern = $"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars()))}]";
            input = Regex.Replace(input, pattern, "").Trim();
            return input.Length > 200 ? input[..200] : input;
        }

        private static string CalculateAttachmentSize(WorkItemDto workItem)
        {
            double size = workItem.Attachments.Sum(a => (double)a.Length);
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            while (size >= 1024 && order < units.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {units[order]}";
        }

        private string? _lastDownloadFolder;

        private void ShowDownloadFolderLink(string? folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                btnOpenDownloadFolder.Visibility = Visibility.Collapsed;
                return;
            }

            _lastDownloadFolder = folderPath;
            string dirName = Path.GetFileName(Path.GetDirectoryName(folderPath.TrimEnd(Path.DirectorySeparatorChar)) ?? "")
                             + Path.DirectorySeparatorChar
                             + Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar));
            lblDownloadPath.Text = $"Open {dirName}";
            btnOpenDownloadFolder.Visibility = Visibility.Visible;
        }

        private void BtnOpenDownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_lastDownloadFolder) && Directory.Exists(_lastDownloadFolder))
                Process.Start(new ProcessStartInfo("explorer.exe", _lastDownloadFolder) { UseShellExecute = true });
        }

        private void HighlightRows()
        {
            int days = (int)numHighlightDays.Value;
            var now = DateTime.Now;
            var highlightBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 0, 120, 215));

            foreach (var row in _rows)
            {
                bool recent = row.CreatedDate.AddDays(days) > now;
                row.RowBackground = recent ? highlightBrush : null;
            }

            // Force DataGrid to re-read the collection
            dataGridWorkItems.ItemsSource = null;
            dataGridWorkItems.ItemsSource = _rows;
        }

        private List<WorkItemRow> BuildRows(List<WorkItemDto> items)
        {
            return items.Select(wi => BuildRow(wi)).ToList();
        }

        private static WorkItemRow BuildRow(WorkItemDto wi)
        {
            var fieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["System.Id"] = wi.Id.ToString(),
                ["System.Title"] = wi.Title ?? "",
                ["System.State"] = wi.State ?? "",
                ["System.CreatedBy"] = wi.CreatedBy ?? "",
                ["System.CreatedDate"] = wi.CreatedDate.ToShortDateString(),
                ["System.WorkItemType"] = wi.TypeName ?? "",
                ["System.IterationPath"] = wi.IterationPath ?? ""
            };

            foreach (var kvp in wi.Fields)
            {
                if (!fieldValues.ContainsKey(kvp.Key) && kvp.Value != null)
                {
                    string val = kvp.Value.ToString() ?? "";
                    if (val.StartsWith('{'))
                    {
                        try
                        {
                            var jobj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(val);
                            if (jobj != null && jobj.TryGetValue("displayName", out var dn))
                                val = dn?.ToString() ?? val;
                        }
                        catch { /* use raw string */ }
                    }
                    fieldValues[kvp.Key] = val;
                }
            }

            return new WorkItemRow
            {
                Id = wi.Id,
                Title = wi.Title ?? "",
                State = wi.State ?? "",
                CreatedBy = wi.CreatedBy ?? "",
                CreatedDate = wi.CreatedDate,
                CreatedDateShort = wi.CreatedDate.ToShortDateString(),
                TypeName = wi.TypeName ?? "",
                IterationShort = (wi.IterationPath ?? "").Replace("Civil Design\\Civil Designer Products\\", ""),
                HtmlUrl = wi.HtmlUrl ?? "",
                FieldValues = fieldValues
            };
        }

        private static WorkItemRow BuildRowFromCacheEntry(EmbeddingCacheEntry entry, string? scorePrefix = null)
        {
            string title = scorePrefix != null ? $"{scorePrefix} {entry.Title}" : entry.Title;

            var fieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["System.Id"] = entry.WorkItemId.ToString(),
                ["System.Title"] = title,
                ["System.State"] = entry.State,
                ["System.CreatedBy"] = entry.CreatedBy,
                ["System.CreatedDate"] = entry.CreatedDate.ToShortDateString(),
                ["System.WorkItemType"] = entry.TypeName,
                ["System.IterationPath"] = entry.IterationPath
            };

            // Add all cached fields for dynamic column display
            foreach (var kvp in entry.Fields)
            {
                if (!fieldValues.ContainsKey(kvp.Key))
                    fieldValues[kvp.Key] = kvp.Value;
            }

            return new WorkItemRow
            {
                Id = entry.WorkItemId,
                Title = title,
                State = entry.State,
                CreatedBy = entry.CreatedBy,
                CreatedDate = entry.CreatedDate,
                CreatedDateShort = entry.CreatedDate.ToShortDateString(),
                TypeName = entry.TypeName,
                IterationShort = (entry.IterationPath ?? "").Replace("Civil Design\\Civil Designer Products\\", ""),
                HtmlUrl = entry.HtmlUrl ?? "",
                FieldValues = fieldValues
            };
        }

        /// <summary>
        /// Rebuilds the DataGrid columns to match the given column list.
        /// Falls back to default columns when columns is empty (backlog search mode).
        /// </summary>
        private void ApplyDynamicColumns(List<string> columns)
        {
            if (columns.Count == 0)
            {
                var configured = AppSettings.Default.SearchResultColumns;
                columns = configured.Count > 0 ? configured : DefaultColumns;
            }

            dataGridWorkItems.Columns.Clear();

            foreach (var fieldRef in columns)
            {
                string header = FieldDisplayNames.TryGetValue(fieldRef, out var display)
                    ? display
                    : fieldRef.Split('.').Last();

                // Map well-known fields to typed properties, others to FieldValues indexer
                string bindingPath = fieldRef switch
                {
                    "System.Id" => "Id",
                    "System.Title" => "Title",
                    "System.State" => "State",
                    "System.CreatedBy" => "CreatedBy",
                    "System.CreatedDate" => "CreatedDateShort",
                    "System.WorkItemType" => "TypeName",
                    "System.IterationPath" => "IterationShort",
                    _ => $"FieldValues[{fieldRef}]"
                };

                var col = new DataGridTextColumn
                {
                    Header = header,
                    Binding = new Binding { Path = new PropertyPath(bindingPath) },
                    CanUserResize = true,
                    CanUserSort = true,
                };

                // Set sensible default widths
                if (fieldRef == "System.Id")
                    col.Width = new DataGridLength(80);
                else if (fieldRef == "System.Title")
                    col.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                else if (fieldRef == "System.State" || fieldRef == "System.CreatedDate" || fieldRef == "System.ChangedDate")
                    col.Width = new DataGridLength(90);
                else if (fieldRef == "System.CreatedBy" || fieldRef == "System.AssignedTo" || fieldRef == "System.ChangedBy")
                    col.Width = new DataGridLength(130);
                else if (fieldRef == "System.WorkItemType")
                    col.Width = new DataGridLength(100);
                else if (fieldRef == "System.IterationPath" || fieldRef == "System.AreaPath")
                    col.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                else
                    col.Width = new DataGridLength(120);

                dataGridWorkItems.Columns.Add(col);
            }
        }

        // Event Handlers
        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            string project = txtProjectName.Text.Trim();
            if (string.IsNullOrEmpty(project))
            {
                ShowMessage("Please provide a Project Name.", "Missing Information");
                return;
            }

            var settings = AppSettings.Default;
            if (string.IsNullOrWhiteSpace(settings.Organization) || string.IsNullOrWhiteSpace(settings.PersonalAccessToken))
            {
                ShowMessage("Organization and PAT are not set. Check Settings.", "Missing Configuration");
                return;
            }

            btnConnect.IsEnabled = false;
            ShowProgress();

            try
            {
                _tfsRest = new TfsRestClient(settings.Organization, project, settings.PersonalAccessToken);
                _queryTree = await _tfsRest.GetQueriesAsync();
                _queryListFlat = TfsRestClient.FlattenQueries(_queryTree);

                PopulateQueryTree();

                btnReadItems.IsEnabled = true;
                btnDownloadSelected.IsEnabled = true;
                btnDownloadSingle.IsEnabled = true;
                btnFindSimilar.IsEnabled = _semanticSearch != null && _semanticSearch.IsReady;
                btnUpdateIndex.IsEnabled = true;
                lblConnectionStatus.Text = "Connected";
            }
            catch (HttpRequestException ex)
            {
                _tfsRest = null;
                ShowMessage($"Could not reach the server. Check your internet connection or verify the organization URL.\n\nDetails: {ex.Message}", "Connection Error");
                lblConnectionStatus.Text = "No connection";
            }
            catch (Exception ex)
            {
                _tfsRest = null;
                ShowMessage("Failed to connect or load queries: " + ex.Message, "Error");
                lblConnectionStatus.Text = "Connection failed";
            }

            HideProgress();
            btnConnect.IsEnabled = true;
            PersistSettings();
        }

        private async void BtnReadItems_Click(object sender, RoutedEventArgs e)
        {
            if (_tfsRest == null || _queryListFlat.Count == 0) return;

            // SelectedItem may be a TreeViewNode (when using RootNodes) or the Content directly.
            // Fall back to the restored query when the tree is collapsed with no active selection.
            var selected = treeQueries.SelectedItem;
            var q = selected as QueryDto
                ?? (selected as TreeViewNode)?.Content as QueryDto
                ?? _restoredQuery;
            if (q == null || q.IsFolder) return;

            string wiql = q.Wiql ?? "";

            lblItemCount.Text = "Reading…";
            btnReadItems.IsEnabled = false;
            ShowProgress();
            _rows.Clear();

            try
            {
                // Step 1: Execute query to get IDs and column definitions (fast, no data fetch)
                var queryResult = await _tfsRest.ExecuteQueryAsync(wiql);
                _queryColumns = queryResult.Columns;
                q.Columns = queryResult.Columns;
                var allIds = queryResult.WorkItemIds;

                if (allIds.Count == 0)
                {
                    _workItemList = new List<WorkItemDto>();
                }
                else if (!string.IsNullOrEmpty(q.Id))
                {
                    // Step 2: Load cache and get lightweight ChangedDates from API
                    string cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ADO_Tools_WinUI", "QueryCache");
                    _querySearchCache = new QuerySearchCache(q.Id, cacheDir);
                    _querySearchCache.TryLoad();

                    // Switch to determinate progress for the checking phase
                    progressBar.IsIndeterminate = false;
                    progressBar.Minimum = 0;
                    progressBar.Maximum = allIds.Count;
                    progressBar.Value = 0;

                    lblItemCount.Text = $"Checking 0/{allIds.Count} items…";
                    var freshChangedDates = await _tfsRest.FetchWorkItemChangedDatesAsync(allIds, (fetched, total) =>
                    {
                        DispatcherQueue.TryEnqueue(() =>
                                    {
                                        progressBar.Value = fetched;
                                        lblItemCount.Text = $"Checking {fetched}/{total} items…";
                                    });
                    });

                    // Step 3: Determine which items need a full re-fetch
                    var idsToFetch = _querySearchCache.GetIdsNeedingFetch(allIds, freshChangedDates);

                    // Step 4: Fetch only new/changed items
                    if (idsToFetch.Count > 0)
                    {
                        progressBar.Maximum = idsToFetch.Count;
                        progressBar.Value = 0;
                        string parallelInfo = "";

                        lblItemCount.Text = $"Fetching 0/{idsToFetch.Count} items…";
                        var freshItems = await _tfsRest.FetchWorkItemsByIdsAsync(idsToFetch,
                            (fetched, total) =>
                            {
                                DispatcherQueue.TryEnqueue(() =>
                                            {
                                                progressBar.Value = fetched;
                                                lblItemCount.Text = $"Fetching {fetched}/{total} items… {parallelInfo}";
                                            });
                            },
                            (status) =>
                            {
                                parallelInfo = status;
                            });
                        _querySearchCache.MergeFullItems(freshItems);
                        await _querySearchCache.SaveAsync();
                    }

                    // Step 5: Reconstruct the full work item list from cache (in query order)
                    _workItemList = _querySearchCache.GetCachedWorkItems(allIds);
                }
                else
                {
                    // No query ID — fall back to full fetch (no caching possible)
                    var fullResult = await _tfsRest.QueryWorkItemsAsync(wiql);
                    _workItemList = fullResult.WorkItems;
                }
            }
            catch (Exception ex)
            {
                ShowMessage("Failed to run query: " + ex.Message, "Error");
                _workItemList = new List<WorkItemDto>();
                _queryColumns = [];
            }

            _listMode = ListMode.Query;
            _lastQueryName = q.Name;

            // Apply dynamic columns from the query definition
            ApplyDynamicColumns(_queryColumns);

            if (_workItemList.Count == 0)
            {
                lblItemCount.Text = "0 items";
                txtQuerySearch.IsEnabled = false;
            }
            else
            {
                foreach (var row in BuildRows(_workItemList))
                    _rows.Add(row);

                lblItemCount.Text = $"{_workItemList.Count} items";
                HighlightRows();

                // Build BM25 search index from the already-loaded cache
                if (_querySearchCache != null)
                {
                    _bm25QuerySearch = new Bm25SearchService();
                    _bm25QuerySearch.BuildIndex(_querySearchCache.GetAsBm25Entries());
                    txtQuerySearch.IsEnabled = true;
                    lblItemCount.Text = $"{_workItemList.Count} items (searchable)";
                }
            }

            UpdateContextBadge();
            HideProgress();
            btnReadItems.IsEnabled = true;
        }

        private async void BtnDownloadSelected_Click(object sender, RoutedEventArgs e)
        {
            if (_tfsRest == null) return;
            var selected = dataGridWorkItems.SelectedItems.OfType<WorkItemRow>().ToList();
            if (selected.Count == 0)
            {
                ShowMessage("Select one or more work items first.");
                return;
            }

            progressBar.IsIndeterminate = true;
            progressBar.Visibility = Visibility.Visible;
            btnDownloadSelected.IsEnabled = false;

            int failedAttachments = 0;
            string? firstDownloadPath = null;

            try
            {
                btnOpenDownloadFolder.Visibility = Visibility.Collapsed;

                foreach (var row in selected)
                {
                    var workItem = _workItemList.FirstOrDefault(w => w.Id == row.Id);

                    // If item came from search (not in _workItemList), fetch it from the API
                    if (workItem == null)
                    {
                        try
                        {
                            workItem = await _tfsRest.GetWorkItemAsync(row.Id);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to fetch work item #{row.Id}: {ex.Message}");
                        }
                    }

                    if (workItem == null) continue;

                    string sizeText = CalculateAttachmentSize(workItem);
                    lblDownloading.Text = $"Downloading #{workItem.Id}";
                    lblSize.Text = $"{workItem.Attachments.Count} Attachment(s): {sizeText}";

                    string path = CreateFolder(ReadTopFolder(), workItem.Id.ToString()) + Path.DirectorySeparatorChar;
                    firstDownloadPath ??= path;

                    string htmlPath = Path.Combine(path, RemoveIllegalCharacters(workItem.Title ?? "") + ".html");
                    string url = workItem.HtmlUrl ?? "";
                    File.WriteAllText(htmlPath,
                        $"<h1><a href=\"{System.Net.WebUtility.HtmlEncode(url)}\">{System.Net.WebUtility.HtmlEncode(workItem.Title ?? "")}</a></h1>");

                    foreach (var att in workItem.Attachments)
                    {
                        try { await _tfsRest.DownloadAttachmentAsync(att, path); }
                        catch (Exception ex)
                        {
                            failedAttachments++;
                            Debug.WriteLine($"Attachment download failed: {ex.Message}");
                        }
                    }

                    row.Downloaded = true;
                }

                lblDownloading.Text = failedAttachments > 0
                    ? $"Download Complete ({failedAttachments} attachment(s) failed)"
                    : "Download Complete";
                lblSize.Text = "";
                ShowDownloadFolderLink(firstDownloadPath);
            }
            catch (Exception ex)
            {
                lblDownloading.Text = $"Download failed: {ex.Message}";
                lblSize.Text = "";
            }
            finally
            {
                progressBar.IsIndeterminate = false;
                progressBar.Visibility = Visibility.Collapsed;
                btnDownloadSelected.IsEnabled = true;
            }
        }

        private async void BtnDownloadSingle_Click(object sender, RoutedEventArgs e)
        {
            if (_tfsRest == null) return;
            if (double.IsNaN(txtFindSimilarId.Value)) return;
            int elementId = (int)txtFindSimilarId.Value;

            progressBar.IsIndeterminate = true;
            progressBar.Visibility = Visibility.Visible;
            btnOpenDownloadFolder.Visibility = Visibility.Collapsed;

            try
            {
                WorkItemDto? workItem;
                try
                {
                    workItem = await _tfsRest.GetWorkItemAsync(elementId);
                }
                catch (Exception ex)
                {
                    ShowMessage("Failed to fetch work item: " + ex.Message, "Error");
                    return;
                }

                if (workItem == null)
                {
                    ShowMessage($"Work item #{elementId} not found.");
                    return;
                }

                string sizeText = CalculateAttachmentSize(workItem);
                lblDownloading.Text = $"Downloading #{workItem.Id}";
                lblSize.Text = $"{workItem.Attachments.Count} Attachment(s): {sizeText}";

                string path = CreateFolder(ReadTopFolder(), workItem.Id.ToString()) + Path.DirectorySeparatorChar;
                string htmlPath = Path.Combine(path, RemoveIllegalCharacters(workItem.Title ?? "") + ".html");
                string url = workItem.HtmlUrl ?? "";
                File.WriteAllText(htmlPath,
                    $"<h1><a href=\"{System.Net.WebUtility.HtmlEncode(url)}\">{System.Net.WebUtility.HtmlEncode(workItem.Title ?? "")}</a></h1>");

                int failedAttachments = 0;
                foreach (var att in workItem.Attachments)
                {
                    try { await _tfsRest.DownloadAttachmentAsync(att, path); }
                    catch (Exception ex)
                    {
                        failedAttachments++;
                        Debug.WriteLine($"Attachment download failed: {ex.Message}");
                    }
                }

                lblDownloading.Text = failedAttachments > 0
                    ? $"Download Complete ({failedAttachments} attachment(s) failed)"
                    : "Download Complete";
                lblSize.Text = "";
                ShowDownloadFolderLink(path);
            }
            catch (Exception ex)
            {
                lblDownloading.Text = $"Download failed: {ex.Message}";
                lblSize.Text = "";
            }
            finally
            {
                progressBar.IsIndeterminate = false;
                progressBar.Visibility = Visibility.Collapsed;
            }
        }

        private void DataGridWorkItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Guard against events firing during _rows.Clear() or before UI is initialized
            if (dataGridWorkItems == null) return;

            var selectedItems = dataGridWorkItems.SelectedItems.OfType<WorkItemRow>().ToList();
            var first = selectedItems.FirstOrDefault();
            if (first != null)
                txtFindSimilarId.Value = first.Id;

            lblDownloadSelectedText.Text = selectedItems.Count > 0
                ? $"Download Selected ({selectedItems.Count})"
                : "Download Selected";
        }

        private async void BtnFindSimilar_Click(object sender, RoutedEventArgs e)
        {
            if (_semanticSearch == null || !_semanticSearch.IsReady || _bm25BacklogSearch == null)
            {
                ShowMessage("Build the search index first (Search tab \u2192 Update).", "Index Required");
                return;
            }

            int targetId = (int)txtFindSimilarId.Value;
            if (targetId <= 0 || double.IsNaN(txtFindSimilarId.Value))
            {
                ShowMessage("Enter a work item ID or select one from the grid.", "No Item Selected");
                return;
            }

            // Switch to the Search tab so it's visually clear results come from the index
            pivotMode.SelectedIndex = 1;

            ShowProgress();
            btnFindSimilar.IsEnabled = false;

            try
            {
                // 3-tier lookup: current list → embedding cache → API
                var workItem = _workItemList.FirstOrDefault(w => w.Id == targetId);

                string sourceText;
                if (workItem != null)
                {
                    sourceText = SemanticSearchService.BuildSearchableText(workItem);
                }
                else
                {
                    // Try the embedding cache — allows fully offline Find Similar
                    var cachedEntry = _semanticSearch.GetCacheEntries(false)
                        .FirstOrDefault(e => e.WorkItemId == targetId);

                    if (cachedEntry != null)
                    {
                        sourceText = cachedEntry.SearchableText ?? cachedEntry.Title;
                        workItem = new WorkItemDto
                        {
                            Id = cachedEntry.WorkItemId,
                            Title = cachedEntry.Title,
                            State = cachedEntry.State,
                            TypeName = cachedEntry.TypeName,
                            CreatedBy = cachedEntry.CreatedBy,
                            CreatedDate = cachedEntry.CreatedDate,
                            IterationPath = cachedEntry.IterationPath,
                            HtmlUrl = cachedEntry.HtmlUrl
                        };
                    }
                    else if (_tfsRest != null)
                    {
                        workItem = await _tfsRest.GetWorkItemAsync(targetId);
                        sourceText = workItem != null
                            ? SemanticSearchService.BuildSearchableText(workItem)
                            : "";
                    }
                    else
                    {
                        sourceText = "";
                    }
                }

                if (workItem == null)
                {
                    string reason = _tfsRest != null
                        ? $"Work item #{targetId} does not exist."
                        : $"Work item #{targetId} is not in the search index.\nConnect to a project to look up items outside the cached backlog.";
                    ShowMessage(reason, "Not Found");
                    return;
                }
                bool excludeDone = chkExcludeDone.IsChecked == true;
                int topN = (int)numTopResults.Value;

                // Run both searches in parallel on background threads
                int fetchN = topN * 2;
                var semanticTask = Task.Run(() =>
                    _semanticSearch!.FindSimilar(workItem, topN: fetchN, excludeDone: excludeDone));
                var bm25Task = Task.Run(() =>
                    _bm25BacklogSearch!.Search(sourceText, topN: fetchN, excludeDone: excludeDone));

                await Task.WhenAll(semanticTask, bm25Task);

                var merged = MergeByReciprocalRankFusion(semanticTask.Result, bm25Task.Result, topN);

                // Display results
                ApplyDynamicColumns([]);
                _rows.Clear();

                // Source item first with [Source] tag
                var sourceRow = BuildRow(workItem);
                sourceRow.Title = $"[Source] {sourceRow.Title}";
                sourceRow.FieldValues["System.Title"] = sourceRow.Title;
                sourceRow.RowBackground = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 202, 80, 16));
                _rows.Add(sourceRow);

                foreach (var (entry, score) in merged)
                    _rows.Add(BuildRowFromCacheEntry(entry, $"[{score:P0}]"));

                _listMode = ListMode.Compare;
                _lastCompareSource = $"#{workItem.Id} {workItem.Title}";
                lblItemCount.Text = $"{merged.Count} similar items";
                UpdateContextBadge();
            }
            catch (Exception ex)
            {
                ShowMessage("Find similar failed: " + ex.Message, "Error");
            }
            finally
            {
                HideProgress();
                btnFindSimilar.IsEnabled = true;
            }
        }

        private void NumHighlightDays_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_rows.Count > 0)
                HighlightRows();
        }

        private void DataGridWorkItems_Sorting(object sender, DataGridColumnEventArgs e)
        {
            var column = e.Column;
            var direction = column.SortDirection == DataGridSortDirection.Ascending
                ? DataGridSortDirection.Descending
                : DataGridSortDirection.Ascending;

            // Clear sort indicators on all other columns
            foreach (var col in dataGridWorkItems.Columns)
            {
                if (col != column)
                    col.SortDirection = null;
            }
            column.SortDirection = direction;

            // Determine the property/field to sort by from the column's binding path
            string sortTag = (column as DataGridBoundColumn)?.Binding is Binding b
                ? b.Path.Path
                : "";

            var sorted = direction == DataGridSortDirection.Ascending
                ? _rows.OrderBy(r => GetSortValue(r, sortTag)).ToList()
                : _rows.OrderByDescending(r => GetSortValue(r, sortTag)).ToList();

            _rows.Clear();
            foreach (var row in sorted)
                _rows.Add(row);
        }

        private static object GetSortValue(WorkItemRow row, string path)
        {
            return path switch
            {
                "Id" => row.Id,
                "Title" => row.Title,
                "State" => row.State,
                "CreatedBy" => row.CreatedBy,
                "CreatedDateShort" => row.CreatedDate,
                "TypeName" => row.TypeName,
                "IterationShort" => row.IterationShort,
                _ when path.StartsWith("FieldValues[") =>
                    row.FieldValues.TryGetValue(path[12..^1], out var v) ? v : "",
                _ => row.Id
            };
        }

        private void DataGridWorkItems_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (dataGridWorkItems.SelectedItem is not WorkItemRow row) return;
            if (string.IsNullOrEmpty(row.HtmlUrl)) return;

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = row.HtmlUrl,
                UseShellExecute = true
            });
        }

        /// <summary>
        /// Reloads the search cache from disk. Called after Settings page rebuilds the index.
        /// </summary>
        public async void ReloadSearchCache()
        {
            await TryLoadExistingCacheAsync();
        }


        // ── Backlog Search (Semantic + BM25) ────────────────────────────

        private async void BtnUpdateIndex_Click(object sender, RoutedEventArgs e)
        {
            if (_tfsRest == null)
            {
                ShowMessage("Connect to a project first.", "Not Connected");
                return;
            }

            var settings = AppSettings.Default;
            string modelDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Models");

            if (!File.Exists(Path.Combine(modelDir, "model.onnx")))
            {
                ShowMessage(
                    "Embedding model not found.\n\n" +
                    "Place 'model.onnx' and 'vocab.txt' in:\n" +
                    modelDir,
                    "Model Missing");
                return;
            }

            btnUpdateIndex.IsEnabled = false;
            txtSemanticSearch.IsEnabled = false;
            ShowProgress();

            try
            {
                _semanticSearch?.Dispose();
                string cacheDir = Path.Combine(AppContext.BaseDirectory, "EmbeddingCache");
                _semanticSearch = new SemanticSearchService(modelDir, cacheDir);
                _semanticSearch.StatusUpdated += msg =>
                DispatcherQueue.TryEnqueue(() => lblCacheStatus.Text = FormatCacheLabel(msg));

                string areaPath = settings.SearchAreaPath?.Trim() ?? "";

                var (added, total) = await _semanticSearch.BuildOrUpdateCacheAsync(
                    _tfsRest,
                    settings.Organization,
                    settings.Project,
                    areaPath,
                    progressCallback: (current, count) =>
                    {
                        DispatcherQueue.TryEnqueue(() =>
                            lblCacheStatus.Text = FormatCacheLabel($"Embedding {current}/{count}…"));
                    });

                _bm25BacklogSearch = new Bm25SearchService();
                _bm25BacklogSearch.BuildIndex(_semanticSearch.GetCacheEntries(false));

                txtSemanticSearch.IsEnabled = true;
                btnFindSimilar.IsEnabled = true;
                lblCacheStatus.Text = FormatCacheLabel(added > 0
                    ? $"Ready — added {added} new items, {total} total indexed"
                    : $"Ready — {total} items indexed, cache up to date");
            }
            catch (Exception ex)
            {
                lblCacheStatus.Text = $"Update failed: {ex.Message}";
            }

            HideProgress();
            btnUpdateIndex.IsEnabled = true;
        }

        private void CmbSearchMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (txtSemanticSearch == null) return;
            txtSemanticSearch.PlaceholderText = cmbSearchMode.SelectedIndex switch
            {
                2 => "Search by keywords \u2014 e.g. 'cant points'\u2026",
                1 => "Search by meaning \u2014 e.g. 'crash when opening large files'\u2026",
                _ => "Search by meaning + keywords \u2014 e.g. 'cant not in reports'\u2026"
            };
        }

        private async void TxtSemanticSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            switch (cmbSearchMode.SelectedIndex)
            {
                case 2: await RunBm25BacklogSearchAsync(); break;
                case 1: await RunSemanticSearchAsync(); break;
                default: await RunHybridBacklogSearchAsync(); break;
            }
        }

        /// <summary>
        /// Merges two ranked result lists using Reciprocal Rank Fusion (k=60).
        /// Items found by both lists get boosted; items in only one still appear.
        /// Returns (entry, normalizedScore) pairs ordered by fused score.
        /// </summary>
        private static List<(EmbeddingCacheEntry Entry, float Score)> MergeByReciprocalRankFusion(
            List<SemanticSearchResult> semanticResults,
            List<Bm25SearchResult> bm25Results,
            int topN)
        {
            const float rrfK = 60f;
            var rrfScores = new Dictionary<int, float>();
            var entryMap = new Dictionary<int, EmbeddingCacheEntry>();

            for (int rank = 0; rank < semanticResults.Count; rank++)
            {
                int id = semanticResults[rank].CacheEntry.WorkItemId;
                rrfScores[id] = 1f / (rrfK + rank + 1);
                entryMap[id] = semanticResults[rank].CacheEntry;
            }

            for (int rank = 0; rank < bm25Results.Count; rank++)
            {
                int id = bm25Results[rank].CacheEntry.WorkItemId;
                if (rrfScores.ContainsKey(id))
                    rrfScores[id] += 1f / (rrfK + rank + 1);
                else
                    rrfScores[id] = 1f / (rrfK + rank + 1);
                entryMap.TryAdd(id, bm25Results[rank].CacheEntry);
            }

            var ranked = rrfScores
                .OrderByDescending(kvp => kvp.Value)
                .Take(topN)
                .ToList();

            float maxRrf = ranked.Count > 0 ? ranked[0].Value : 1f;
            return ranked
                .Where(kvp => entryMap.ContainsKey(kvp.Key))
                .Select(kvp => (entryMap[kvp.Key], kvp.Value / maxRrf))
                .ToList();
        }

        private async Task RunHybridBacklogSearchAsync()
        {
            if (_semanticSearch == null || !_semanticSearch.IsReady
                || _bm25BacklogSearch == null || _bm25BacklogSearch.DocumentCount == 0)
                return;

            string query = txtSemanticSearch.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(query))
            {
                ClearBacklogSearch();
                return;
            }

            ShowProgress();

            try
            {
                bool excludeDone = chkExcludeDone.IsChecked == true;
                int topN = (int)numTopResults.Value;
                // Fetch more candidates from each engine so RRF has enough overlap
                int fetchN = topN * 2;

                var semanticTask = Task.Run(() =>
                    _semanticSearch.Search(query, fetchN, excludeDone));
                var bm25Task = Task.Run(() =>
                    _bm25BacklogSearch.Search(query, fetchN, excludeDone));

                await Task.WhenAll(semanticTask, bm25Task);

                var merged = MergeByReciprocalRankFusion(semanticTask.Result, bm25Task.Result, topN);

                ApplyDynamicColumns([]);

                _rows.Clear();
                foreach (var (entry, score) in merged)
                    _rows.Add(BuildRowFromCacheEntry(entry, $"[{score:P0}]"));

                _listMode = ListMode.SearchBacklog;
                _lastSearchQuery = query;
                lblItemCount.Text = $"{merged.Count} matches (hybrid)";
                UpdateContextBadge();
            }
            catch (Exception ex)
            {
                ShowMessage("Search failed: " + ex.Message, "Error");
            }
            finally
            {
                HideProgress();
            }
        }

        private async Task RunSemanticSearchAsync()
        {
            if (_semanticSearch == null || !_semanticSearch.IsReady) return;

            string query = txtSemanticSearch.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(query))
            {
                ClearBacklogSearch();
                return;
            }

            ShowProgress();

            try
            {
                bool excludeDone = chkExcludeDone.IsChecked == true;
                int topN = (int)numTopResults.Value;

                var results = await Task.Run(() =>
                    _semanticSearch.Search(query, topN, excludeDone));

                // Reset to default columns for backlog search
                ApplyDynamicColumns([]);

                _rows.Clear();
                foreach (var r in results)
                    _rows.Add(BuildRowFromCacheEntry(r.CacheEntry, $"[{r.Score:P0}]"));

                _listMode = ListMode.SearchBacklog;
                _lastSearchQuery = query;
                lblItemCount.Text = $"{results.Count} matches";
                UpdateContextBadge();
            }
            catch (Exception ex)
            {
                ShowMessage("Search failed: " + ex.Message, "Error");
            }
            finally
            {
                HideProgress();
            }
        }

        private async Task RunBm25BacklogSearchAsync()
        {
            if (_bm25BacklogSearch == null || _bm25BacklogSearch.DocumentCount == 0) return;

            string query = txtSemanticSearch.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(query))
            {
                ClearBacklogSearch();
                return;
            }

            ShowProgress();

            try
            {
                bool excludeDone = chkExcludeDone.IsChecked == true;
                int topN = (int)numTopResults.Value;

                var results = await Task.Run(() =>
                    _bm25BacklogSearch.Search(query, topN, excludeDone));

                // Reset to default columns for backlog search
                ApplyDynamicColumns([]);

                _rows.Clear();
                foreach (var r in results)
                    _rows.Add(BuildRowFromCacheEntry(r.CacheEntry, $"[{r.Score:F1}]"));

                _listMode = ListMode.SearchBacklog;
                _lastSearchQuery = query;
                lblItemCount.Text = $"{results.Count} matches (BM25)";
                UpdateContextBadge();
            }
            catch (Exception ex)
            {
                ShowMessage("Search failed: " + ex.Message, "Error");
            }
            finally
            {
                HideProgress();
            }
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            ClearBacklogSearch();
        }

        private void ClearBacklogSearch()
        {
            txtSemanticSearch.Text = "";

            // Restore query columns when going back to query results
            ApplyDynamicColumns(_queryColumns);

            _rows.Clear();
            foreach (var row in BuildRows(_workItemList))
                _rows.Add(row);

            _listMode = ListMode.Query;
            lblItemCount.Text = $"{_workItemList.Count} items";
            UpdateContextBadge();
        }

        // ── Query Search (BM25 within query results) ────────────────────

        private async void TxtQuerySearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (_bm25QuerySearch == null || _bm25QuerySearch.DocumentCount == 0)
            {
                ShowMessage("Run a query first to enable search within results.", "No Query Loaded");
                return;
            }

            string query = txtQuerySearch.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(query))
            {
                ClearQuerySearch();
                return;
            }

            ShowProgress();

            try
            {
                var results = await Task.Run(() =>
                    _bm25QuerySearch.Search(query, _bm25QuerySearch.DocumentCount));

                // Keep query columns for search-within-query results
                ApplyDynamicColumns(_queryColumns);

                _rows.Clear();
                foreach (var r in results)
                    _rows.Add(BuildRowFromCacheEntry(r.CacheEntry, $"[{r.Score:F1}]"));

                _listMode = ListMode.SearchQuery;
                _lastSearchQuery = query;
                lblItemCount.Text = $"{results.Count}/{_bm25QuerySearch.DocumentCount} matches in query";
                UpdateContextBadge();
                HighlightRows();
            }
            catch (Exception ex)
            {
                ShowMessage("Search failed: " + ex.Message, "Error");
            }
            finally
            {
                HideProgress();
            }
        }

        private void BtnClearQuerySearch_Click(object sender, RoutedEventArgs e)
        {
            ClearQuerySearch();
        }

        private void ClearQuerySearch()
        {
            txtQuerySearch.Text = "";

            ApplyDynamicColumns(_queryColumns);

            _rows.Clear();
            foreach (var row in BuildRows(_workItemList))
                _rows.Add(row);

            _listMode = ListMode.Query;
            lblItemCount.Text = $"{_workItemList.Count} items";
            UpdateContextBadge();
            HighlightRows();
        }

        private static string FormatCacheLabel(string message)
        {
            string path = AppSettings.Default.SearchAreaPath?.Trim() ?? "";
            return string.IsNullOrEmpty(path)
                ? message
                : $"{message} | {path}";
        }

        private void ShowProgress(bool indeterminate = true)
        {
            progressBar.IsIndeterminate = indeterminate;
            progressBar.Visibility = Visibility.Visible;
        }

        private void HideProgress()
        {
            progressBar.IsIndeterminate = false;
            progressBar.Value = 0;
            progressBar.Visibility = Visibility.Collapsed;
        }
    }
}