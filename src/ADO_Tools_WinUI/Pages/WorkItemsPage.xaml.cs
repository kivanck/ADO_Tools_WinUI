using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ADO_Tools.Models;
using ADO_Tools.Services;
using ADO_Tools.Utilities;
using ADO_Tools_WinUI.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;

namespace ADO_Tools_WinUI.Pages
{
    public sealed partial class WorkItemsPage : Page
    {
        private enum ListMode { Query, SearchQuery, SearchBacklog }

        private TfsRestClient? _tfsRest;
        private List<QueryDto> _queryList = new();
        private List<WorkItemDto> _workItemList = new();
        private readonly ObservableCollection<WorkItemRow> _rows = new();
        private SemanticSearchService? _semanticSearch;
        private Bm25SearchService? _bm25BacklogSearch;
        private Bm25SearchService? _bm25QuerySearch;
        private QuerySearchCache? _querySearchCache;
        private ListMode _listMode = ListMode.Query;
        private string _lastQueryName = "";
        private string _lastSearchQuery = "";
        private List<string> _queryColumns = [];

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

        private void WorkItemsPage_Loaded(object sender, RoutedEventArgs e)
        {
            var s = AppSettings.Default;
            txtProjectName.Text = s.Project;
            txtRootFolder.Text = s.RootFolder;
            txtAreaPath.Text = s.SearchAreaPath;
            listWorkItems.ItemsSource = _rows;
        }

        // ?? View Model Row ??????????????????????????????????????????????

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
            }
        }

        private void PersistSettings()
        {
            var s = AppSettings.Default;
            s.Project = txtProjectName.Text.Trim();
            s.RootFolder = txtRootFolder.Text.Trim();
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
            string folder = txtRootFolder.Text.Trim();
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

            // Force refresh by reassigning source
            listWorkItems.ItemsSource = null;
            listWorkItems.ItemsSource = _rows;
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
        /// Rebuilds the ListView header and item templates to match the given column list.
        /// Falls back to default columns when columns is empty (backlog search mode).
        /// </summary>
        private void ApplyDynamicColumns(List<string> columns)
        {
            if (columns.Count == 0)
                columns = DefaultColumns;

            var colDefs = string.Join("\n",
                columns.Select(c =>
                {
                    if (c == "System.Id") return "<ColumnDefinition Width='70' />";
                    if (c == "System.Title") return "<ColumnDefinition Width='*' />";
                    return "<ColumnDefinition Width='Auto' MinWidth='80' />";
                }));

            var headers = string.Join("\n",
                columns.Select((c, i) =>
                {
                    string name = FieldDisplayNames.TryGetValue(c, out var display) ? display : c.Split('.').Last();
                    return $"<TextBlock Grid.Column='{i}' Text='{EscapeXml(name)}' FontWeight='SemiBold' />";
                }));

            string headerXaml = $@"<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
                <Grid Padding='12,8' ColumnSpacing='8'>
                    <Grid.ColumnDefinitions>{colDefs}</Grid.ColumnDefinitions>
                    {headers}
                </Grid>
            </DataTemplate>";

            listWorkItems.HeaderTemplate = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(headerXaml);

            // Build item template — bind known fields directly, others via FieldValues
            var cells = string.Join("\n",
                columns.Select((c, i) =>
                {
                    string trimming = c == "System.Title" || c == "System.IterationPath"
                        ? " TextTrimming='CharacterEllipsis'" : "";

                    string binding = c switch
                    {
                        "System.Id" => "{Binding Id}",
                        "System.Title" => "{Binding Title}",
                        "System.State" => "{Binding State}",
                        "System.CreatedBy" => "{Binding CreatedBy}",
                        "System.CreatedDate" => "{Binding CreatedDateShort}",
                        "System.WorkItemType" => "{Binding TypeName}",
                        "System.IterationPath" => "{Binding IterationShort}",
                        _ => ""
                    };

                    if (!string.IsNullOrEmpty(binding))
                        return $"<TextBlock Grid.Column='{i}' Text='{binding}'{trimming} />";

                    // For dynamic fields, bind via FieldValues indexer
                    return $"<TextBlock Grid.Column='{i}' Text='{{Binding FieldValues[{EscapeXml(c)}]}}'{trimming} />";
                }));

            string itemColDefs = string.Join("", columns.Select(c =>
            {
                if (c == "System.Id") return "<ColumnDefinition Width='70' />";
                if (c == "System.Title") return "<ColumnDefinition Width='*' />";
                return "<ColumnDefinition Width='Auto' MinWidth='80' />";
            }));

            string itemXaml = $@"<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
                <Grid Padding='12,6' ColumnSpacing='8' Background='{{Binding RowBackground}}'>
                    <Grid.ColumnDefinitions>{itemColDefs}</Grid.ColumnDefinitions>
                    {cells}
                </Grid>
            </DataTemplate>";

            listWorkItems.ItemTemplate = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(itemXaml);
        }

        private static string EscapeXml(string text)
        {
            return text.Replace("&", "&amp;").Replace("<", "&lt;")
                       .Replace(">", "&gt;").Replace("'", "&apos;")
                       .Replace("\"", "&quot;");
        }

        /// <summary>
        /// Builds the BM25 query search cache for the current query results.
        /// Loads existing cache, only re-processes new/changed items.
        /// </summary>
        private async Task BuildQuerySearchIndexAsync(string queryId, List<WorkItemDto> items)
        {
            string cacheDir = Path.Combine(AppContext.BaseDirectory, "QueryCache");
            _querySearchCache = new QuerySearchCache(queryId, cacheDir);
            _querySearchCache.TryLoad();

            var needsUpdate = _querySearchCache.GetItemsNeedingUpdate(items);

            if (needsUpdate.Count > 0)
            {
                foreach (var wi in needsUpdate)
                {
                    string searchText = SemanticSearchService.BuildSearchableText(wi);
                    _querySearchCache.AddOrUpdate(wi, searchText);
                }
                await _querySearchCache.SaveAsync();
            }

            _bm25QuerySearch = new Bm25SearchService();
            _bm25QuerySearch.BuildIndex(_querySearchCache.GetAsBm25Entries());
        }

        // ?? Event Handlers ??????????????????????????????????????????????

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
            progressBar.IsIndeterminate = true;
            progressBar.Visibility = Visibility.Visible;

            try
            {
                _tfsRest = new TfsRestClient(settings.Organization, project, settings.PersonalAccessToken);
                _queryList = await _tfsRest.GetQueriesAsync();

                cmbQueries.Items.Clear();
                foreach (var q in _queryList)
                    cmbQueries.Items.Add(new ComboBoxItem { Content = q.Path });

                if (cmbQueries.Items.Count > 0)
                    cmbQueries.SelectedIndex = 0;

                btnReadItems.IsEnabled = true;
                btnDownloadSelected.IsEnabled = true;
                btnDownloadSingle.IsEnabled = true;
                btnCompare.IsEnabled = true;
                btnBuildIndex.IsEnabled = true;
                btnForceRebuild.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ShowMessage("Failed to connect or load queries: " + ex.Message, "Error");
                btnReadItems.IsEnabled = false;
                btnDownloadSelected.IsEnabled = false;
                btnDownloadSingle.IsEnabled = false;
                btnCompare.IsEnabled = false;
                btnBuildIndex.IsEnabled = false;
                btnForceRebuild.IsEnabled = false;
            }

            progressBar.IsIndeterminate = false;
            progressBar.Visibility = Visibility.Collapsed;
            btnConnect.IsEnabled = true;
            PersistSettings();
        }

        private async void BtnReadItems_Click(object sender, RoutedEventArgs e)
        {
            if (_tfsRest == null || _queryList.Count == 0) return;

            var selectedItem = cmbQueries.SelectedItem as ComboBoxItem;
            if (selectedItem == null) return;
            string queryPath = (string)selectedItem.Content;

            var q = _queryList.FirstOrDefault(x => x.Path == queryPath);
            if (q == null) return;

            string wiql = q.Wiql ?? "";

            lblItemCount.Text = "Reading…";
            btnReadItems.IsEnabled = false;
            progressBar.IsIndeterminate = true;
            progressBar.Visibility = Visibility.Visible;
            _rows.Clear();

            try
            {
                var result = await _tfsRest.QueryWorkItemsAsync(wiql);
                _workItemList = result.WorkItems;
                _queryColumns = result.Columns;
                q.Columns = result.Columns;
            }
            catch (Exception ex)
            {
                ShowMessage("Failed to run query: " + ex.Message, "Error");
                _workItemList = new List<WorkItemDto>();
                _queryColumns = [];
            }

            _listMode = ListMode.Query;
            _lastQueryName = queryPath.Contains('/') ? queryPath[(queryPath.LastIndexOf('/') + 1)..] : queryPath;

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

                // Build BM25 search index for this query (incremental, cached per query)
                if (!string.IsNullOrEmpty(q.Id))
                {
                    await BuildQuerySearchIndexAsync(q.Id, _workItemList);
                    txtQuerySearch.IsEnabled = true;
                    lblItemCount.Text = $"{_workItemList.Count} items (searchable)";
                }
            }

            UpdateContextBadge();
            progressBar.IsIndeterminate = false;
            progressBar.Visibility = Visibility.Collapsed;
            btnReadItems.IsEnabled = true;
        }

        private async void BtnDownloadSelected_Click(object sender, RoutedEventArgs e)
        {
            if (_tfsRest == null) return;
            var selected = listWorkItems.SelectedItems.OfType<WorkItemRow>().ToList();
            if (selected.Count == 0)
            {
                ShowMessage("Select one or more work items first.");
                return;
            }

            progressBar.IsIndeterminate = true;
            progressBar.Visibility = Visibility.Visible;
            btnDownloadSelected.IsEnabled = false;

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
                        System.Diagnostics.Debug.WriteLine($"Failed to fetch work item #{row.Id}: {ex.Message}");
                    }
                }

                if (workItem == null) continue;

                string sizeText = CalculateAttachmentSize(workItem);
                lblDownloading.Text = $"Downloading #{workItem.Id}";
                lblSize.Text = $"{workItem.Attachments.Count} Attachment(s): {sizeText}";

                string path = CreateFolder(ReadTopFolder(), workItem.Id.ToString()) + Path.DirectorySeparatorChar;

                string htmlPath = Path.Combine(path, RemoveIllegalCharacters(workItem.Title ?? "") + ".html");
                string url = workItem.HtmlUrl ?? "";
                File.WriteAllText(htmlPath,
                    $"<h1><a href=\"{System.Net.WebUtility.HtmlEncode(url)}\">{System.Net.WebUtility.HtmlEncode(workItem.Title ?? "")}</a></h1>");

                foreach (var att in workItem.Attachments)
                {
                    try { await _tfsRest.DownloadAttachmentAsync(att, path); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Attachment download failed: {ex.Message}"); }
                }

                row.Downloaded = true;
            }

            lblDownloading.Text = "Download Complete";
            lblSize.Text = "";
            progressBar.IsIndeterminate = false;
            progressBar.Visibility = Visibility.Collapsed;
            btnDownloadSelected.IsEnabled = true;
        }

        private async void BtnDownloadSingle_Click(object sender, RoutedEventArgs e)
        {
            if (_tfsRest == null) return;
            if (double.IsNaN(txtSingleItemId.Value)) return;
            int elementId = (int)txtSingleItemId.Value;

            progressBar.IsIndeterminate = true;
            progressBar.Visibility = Visibility.Visible;

            WorkItemDto? workItem;
            try
            {
                workItem = await _tfsRest.GetWorkItemAsync(elementId);
            }
            catch (Exception ex)
            {
                ShowMessage("Failed to fetch work item: " + ex.Message, "Error");
                progressBar.IsIndeterminate = false;
                progressBar.Visibility = Visibility.Collapsed;
                return;
            }

            if (workItem == null)
            {
                ShowMessage($"Work item #{elementId} not found.");
                progressBar.IsIndeterminate = false;
                progressBar.Visibility = Visibility.Collapsed;
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

            foreach (var att in workItem.Attachments)
            {
                try { await _tfsRest.DownloadAttachmentAsync(att, path); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Attachment download failed: {ex.Message}"); }
            }

            lblDownloading.Text = "Download Complete";
            lblSize.Text = "";
            progressBar.IsIndeterminate = false;
            progressBar.Visibility = Visibility.Collapsed;
        }

        private async void BtnCompare_Click(object sender, RoutedEventArgs e)
        {
            if (_tfsRest == null || _queryList.Count == 0) return;

            var selected = listWorkItems.SelectedItems.OfType<WorkItemRow>().FirstOrDefault();
            if (selected == null)
            {
                ShowMessage("Select a work item to compare.");
                return;
            }

            var workItem = _workItemList.FirstOrDefault(w => w.Id == selected.Id);
            if (workItem == null) return;

            // Ask user which query to compare against
            var queryCmb = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, PlaceholderText = "Select query" };
            foreach (var q in _queryList)
                queryCmb.Items.Add(new ComboBoxItem { Content = q.Path });
            if (queryCmb.Items.Count > 0) queryCmb.SelectedIndex = 0;

            var topNBox = new NumberBox { Header = "Show top matches", Value = 20, Minimum = 1, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, Width = 150 };

            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(new TextBlock { Text = $"Finding items similar to #{workItem.Id}: {workItem.Title}", TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(queryCmb);
            panel.Children.Add(topNBox);

            var setupDialog = new ContentDialog
            {
                Title = "Compare Work Items",
                Content = panel,
                PrimaryButtonText = "Find Similar",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            if (await setupDialog.ShowAsync() != ContentDialogResult.Primary) return;

            var selectedQuery = queryCmb.SelectedItem as ComboBoxItem;
            if (selectedQuery == null) return;
            var queryPath = (string)selectedQuery.Content;
            var q2 = _queryList.FirstOrDefault(x => x.Path == queryPath);
            if (q2 == null) return;

            progressBar.IsIndeterminate = true;
            progressBar.Visibility = Visibility.Visible;

            List<WorkItemDto> compareItems;
            try
            {
                string wiql = q2.Wiql ?? "";
                var result = await _tfsRest.QueryWorkItemsAsync(wiql);
                compareItems = result.WorkItems;
            }
            catch (Exception ex)
            {
                ShowMessage("Failed to read query: " + ex.Message, "Error");
                progressBar.IsIndeterminate = false;
                progressBar.Visibility = Visibility.Collapsed;
                return;
            }

            var comparer = new wordCompare();
            var matches = new List<(WorkItemDto Item, int Score)>();

            foreach (var candidate in compareItems)
            {
                if (candidate.Title == workItem.Title) continue;
                int score = comparer.compareStrings(workItem.Title ?? "", candidate.Title ?? "");
                if (score > 0)
                    matches.Add((candidate, score));
            }

            matches = matches.OrderByDescending(m => m.Score).Take((int)topNBox.Value).ToList();

            progressBar.IsIndeterminate = false;
            progressBar.Visibility = Visibility.Collapsed;

            // Show results in a dialog
            var resultList = new ListView { SelectionMode = ListViewSelectionMode.None, MaxHeight = 400 };
            resultList.HeaderTemplate = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(
                @"<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
                    <Grid Padding='8,4' ColumnSpacing='8'>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width='50'/>
                            <ColumnDefinition Width='60'/>
                            <ColumnDefinition Width='*'/>
                            <ColumnDefinition Width='80'/>
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column='0' Text='Score' FontWeight='SemiBold'/>
                        <TextBlock Grid.Column='1' Text='ID' FontWeight='SemiBold'/>
                        <TextBlock Grid.Column='2' Text='Title' FontWeight='SemiBold'/>
                        <TextBlock Grid.Column='3' Text='State' FontWeight='SemiBold'/>
                    </Grid>
                </DataTemplate>");

            foreach (var (item, score) in matches)
            {
                var row = new Grid { Padding = new Thickness(8, 4, 8, 4), ColumnSpacing = 8 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

                var tbScore = new TextBlock { Text = score.ToString() };
                Grid.SetColumn(tbScore, 0);
                var tbId = new TextBlock { Text = item.Id.ToString() };
                Grid.SetColumn(tbId, 1);
                var tbTitle = new TextBlock { Text = item.Title ?? "", TextTrimming = TextTrimming.CharacterEllipsis };
                Grid.SetColumn(tbTitle, 2);
                var tbState = new TextBlock { Text = item.State ?? "" };
                Grid.SetColumn(tbState, 3);

                row.Children.Add(tbScore);
                row.Children.Add(tbId);
                row.Children.Add(tbTitle);
                row.Children.Add(tbState);
                resultList.Items.Add(row);
            }

            if (matches.Count == 0)
                resultList.Items.Add(new TextBlock { Text = "No matches found." });

            await new ContentDialog
            {
                Title = $"Similar Items (top {matches.Count})",
                Content = resultList,
                CloseButtonText = "Close",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
        }

        private void NumHighlightDays_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_rows.Count > 0)
                HighlightRows();
        }

        private void ListWorkItems_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (listWorkItems.SelectedItem is not WorkItemRow row) return;
            if (string.IsNullOrEmpty(row.HtmlUrl)) return;

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = row.HtmlUrl,
                UseShellExecute = true
            });
        }

        private void ListWorkItems_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Selection handled by ListView built-in selection
        }

        private async void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                txtRootFolder.Text = folder.Path;
                PersistSettings();
            }
        }

        // ?? Query Search (BM25 within query results) ????????????????????

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
                BtnClearQuerySearch_Click(null!, null!);
                return;
            }

            progressBar.IsIndeterminate = true;
            progressBar.Visibility = Visibility.Visible;

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
            progressBar.IsIndeterminate = false;
            progressBar.Visibility = Visibility.Collapsed;
        }

        private void BtnClearQuerySearch_Click(object sender, RoutedEventArgs e)
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

        // ?? Backlog Search (Semantic + BM25) ????????????????????????????

        private async void BtnBuildIndex_Click(object sender, RoutedEventArgs e)
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

            btnBuildIndex.IsEnabled = false;
            txtSemanticSearch.IsEnabled = false;
            progressBar.IsIndeterminate = true;
            progressBar.Visibility = Visibility.Visible;

            try
            {
                _semanticSearch?.Dispose();
                string cacheDir = Path.Combine(AppContext.BaseDirectory, "EmbeddingCache");
                _semanticSearch = new SemanticSearchService(modelDir, cacheDir);
                _semanticSearch.StatusUpdated += msg =>
                    DispatcherQueue.TryEnqueue(() => lblCacheStatus.Text = msg);

                string areaPath = txtAreaPath.Text.Trim();
                settings.SearchAreaPath = areaPath;
                settings.Save();

                await _semanticSearch.BuildOrUpdateCacheAsync(
                    _tfsRest,
                    settings.Organization,
                    settings.Project,
                    areaPath,
                    progressCallback: (current, total) =>
                    {
                        DispatcherQueue.TryEnqueue(() =>
                            lblCacheStatus.Text = $"Embedding {current}/{total}…");
                    });

                _bm25BacklogSearch = new Bm25SearchService();
                _bm25BacklogSearch.BuildIndex(_semanticSearch.GetCacheEntries(false));

                txtSemanticSearch.IsEnabled = true;
                lblCacheStatus.Text = $"Ready — {_semanticSearch.CachedItemCount} items indexed (Semantic + BM25)";
            }
            catch (Exception ex)
            {
                var errorBox = new TextBox
                {
                    Text = ex.Message,
                    TextWrapping = TextWrapping.Wrap,
                    IsReadOnly = true,
                    BorderThickness = new Thickness(0)
                };
                await new ContentDialog
                {
                    Title = "Error",
                    Content = errorBox,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                }.ShowAsync();
                lblCacheStatus.Text = "Index build failed";
            }

            progressBar.IsIndeterminate = false;
            progressBar.Visibility = Visibility.Collapsed;
            btnBuildIndex.IsEnabled = true;
        }

        private void CmbSearchMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (txtSemanticSearch == null) return;
            txtSemanticSearch.PlaceholderText = cmbSearchMode.SelectedIndex == 1
                ? "Search by keywords \u2014 e.g. 'cant points'\u2026"
                : "Search by meaning \u2014 e.g. 'crash when opening large files'\u2026";
        }

        private async void TxtSemanticSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            bool useKeyword = cmbSearchMode.SelectedIndex == 1;
            if (useKeyword)
                await RunBm25BacklogSearchAsync();
            else
                await RunSemanticSearchAsync();
        }

        private async Task RunSemanticSearchAsync()
        {
            if (_semanticSearch == null || !_semanticSearch.IsReady) return;

            string query = txtSemanticSearch.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(query))
            {
                BtnClearSearch_Click(null!, null!);
                return;
            }

            progressBar.IsIndeterminate = true;
            progressBar.Visibility = Visibility.Visible;

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
            progressBar.IsIndeterminate = false;
            progressBar.Visibility = Visibility.Collapsed;
        }

        private async Task RunBm25BacklogSearchAsync()
        {
            if (_bm25BacklogSearch == null || _bm25BacklogSearch.DocumentCount == 0) return;

            string query = txtSemanticSearch.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(query))
            {
                BtnClearSearch_Click(null!, null!);
                return;
            }

            progressBar.IsIndeterminate = true;
            progressBar.Visibility = Visibility.Visible;

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
            progressBar.IsIndeterminate = false;
            progressBar.Visibility = Visibility.Collapsed;
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
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

        private async void BtnForceRebuild_Click(object sender, RoutedEventArgs e)
        {
            if (_tfsRest == null)
            {
                ShowMessage("Connect to a project first.", "Not Connected");
                return;
            }

            var confirm = new ContentDialog
            {
                Title = "Force Rebuild",
                Content = "This will delete the existing embedding cache and re-index all work items from scratch. This may take a while.\n\nContinue?",
                PrimaryButtonText = "Rebuild",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

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

            btnBuildIndex.IsEnabled = false;
            btnForceRebuild.IsEnabled = false;
            txtSemanticSearch.IsEnabled = false;
            progressBar.IsIndeterminate = true;
            progressBar.Visibility = Visibility.Visible;

            try
            {
                _semanticSearch?.Dispose();
                string cacheDir = Path.Combine(AppContext.BaseDirectory, "EmbeddingCache");
                _semanticSearch = new SemanticSearchService(modelDir, cacheDir);
                _semanticSearch.StatusUpdated += msg =>
                    DispatcherQueue.TryEnqueue(() => lblCacheStatus.Text = msg);

                string areaPath = txtAreaPath.Text.Trim();
                settings.SearchAreaPath = areaPath;
                settings.Save();

                await _semanticSearch.BuildOrUpdateCacheAsync(
                    _tfsRest,
                    settings.Organization,
                    settings.Project,
                    areaPath,
                    forceRebuild: true,
                    progressCallback: (current, total) =>
                    {
                        DispatcherQueue.TryEnqueue(() =>
                            lblCacheStatus.Text = $"Embedding {current}/{total}…");
                    });

                _bm25BacklogSearch = new Bm25SearchService();
                _bm25BacklogSearch.BuildIndex(_semanticSearch.GetCacheEntries(false));

                txtSemanticSearch.IsEnabled = true;
                lblCacheStatus.Text = $"Ready — {_semanticSearch.CachedItemCount} items indexed (rebuilt, Semantic + BM25)";
            }
            catch (Exception ex)
            {
                var errorBox = new TextBox
                {
                    Text = ex.Message,
                    TextWrapping = TextWrapping.Wrap,
                    IsReadOnly = true,
                    BorderThickness = new Thickness(0)
                };
                await new ContentDialog
                {
                    Title = "Error",
                    Content = errorBox,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                }.ShowAsync();
                lblCacheStatus.Text = "Index rebuild failed";
            }

            progressBar.IsIndeterminate = false;
            progressBar.Visibility = Visibility.Collapsed;
            btnBuildIndex.IsEnabled = true;
            btnForceRebuild.IsEnabled = true;
        }
    }
}