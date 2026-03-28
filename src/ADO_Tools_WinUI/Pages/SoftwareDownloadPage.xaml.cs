using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ADO_Tools_WinUI.Models;
using ADO_Tools_WinUI.Services;

namespace ADO_Tools_WinUI.Pages
{
    /// <summary>
    /// Page that allows users to browse, download, and install Bentley software builds
    /// from Azure DevOps. Provides build filtering, download progress tracking,
    /// and integrated install/uninstall capabilities.
    /// </summary>
    public sealed partial class SoftwareDownloadPage : Page
    {
        // View-models for every build returned by the last query (unfiltered)
        private List<BuildInfoViewModel> _allBuildViewModels = new();

        // Raw build info objects from Azure DevOps (used to retrieve artifact details)
        private List<BuildInfo> _builds = new();

        // Observable log entries displayed in the log ListView at the bottom of the page
        private readonly ObservableCollection<LogEntryViewModel> _logEntries = new();

        // Cancellation token source for stopping in-progress downloads
        private CancellationTokenSource? _downloadCts;


        /// <summary>Initializes the page, binds the log list, and subscribes to the Loaded event.</summary>
        public SoftwareDownloadPage()
        {
            InitializeComponent();
            lvLog.ItemsSource = _logEntries;
            Loaded += SoftwareDownloadPage_Loaded;
        }

        /// <summary>
        /// Restores persisted settings (download folder, definition ID, project, build count)
        /// into the UI controls and pre-selects the last-used product.
        /// </summary>
        private void SoftwareDownloadPage_Loaded(object sender, RoutedEventArgs e)
        {
            var settings = AppSettings.Default;

            txtDefinitionId.Text = settings.DefinitionId;
            txtProject.Text = settings.Project;
            numBuildCount.Value = settings.BuildCount;

            EnsureDefaultDefinitions();
            PopulateProductCombo();

            // Pre-select the saved product
            if (!string.IsNullOrEmpty(settings.ProductName))
            {
                var match = cmbProductName.Items
                    .OfType<ComboBoxItem>()
                    .FirstOrDefault(i => (string)i.Content == settings.ProductName);
                if (match != null)
                    cmbProductName.SelectedItem = match;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Built-in product definitions shipped with the app.
        /// Format: "ProductName|DefinitionId|AzDevOpsProject"
        /// These are automatically added to the user's list if missing.
        /// </summary>
        private static readonly string[] DefaultDefinitions =
        {
            "OpenRail Designer|6098|civil",
            "OpenRoads Designer|6057|civil",
            "Overhead Line Designer|6289|civil",
            "Microstation|5311|PowerPlatform"
        };

        /// <summary>
        /// Ensures the built-in default product definitions exist in the user's saved list.
        /// Missing entries are appended and the settings are persisted.
        /// </summary>
        private static void EnsureDefaultDefinitions()
        {
            var list = AppSettings.Default.UserDefinitionList;
            foreach (var def in DefaultDefinitions)
            {
                if (!list.Contains(def))
                    list.Add(def);
            }
            AppSettings.Default.Save();
        }

        /// <summary>
        /// Rebuilds the product name combo box from the user's definition list.
        /// Duplicates are filtered out using a HashSet.
        /// </summary>
        private void PopulateProductCombo()
        {
            cmbProductName.Items.Clear();
            var seen = new HashSet<string>();
            foreach (var def in AppSettings.Default.UserDefinitionList)
            {
                var parts = def.Split('|');
                if (parts.Length == 3 && seen.Add(parts[0]))
                    cmbProductName.Items.Add(new ComboBoxItem { Content = parts[0] });
            }
        }

        /// <summary>
        /// Saves the current UI control values (product, definition ID, project, folder, count)
        /// to persistent application settings so they survive restarts.
        /// </summary>
        private void PersistCurrentSettings()
        {
            var s = AppSettings.Default;
            s.ProductName = (cmbProductName.SelectedItem as ComboBoxItem)?.Content as string ?? "";
            s.DefinitionId = txtDefinitionId.Text;
            s.Project = txtProject.Text;
            s.BuildCount = (int)numBuildCount.Value;
            s.Save();
        }

        /// <summary>
        /// Updates the definition ID and project for the currently selected product
        /// in the user's saved definition list (in case the user edited them manually).
        /// </summary>
        private void UpdateCurrentDefinitionInUserList()
        {
            var productName = (cmbProductName.SelectedItem as ComboBoxItem)?.Content as string;
            if (string.IsNullOrWhiteSpace(productName)) return;

            var list = AppSettings.Default.UserDefinitionList;
            var idx = list.FindIndex(d => d.Split('|')[0] == productName);
            if (idx >= 0)
            {
                list[idx] = $"{productName}|{txtDefinitionId.Text}|{txtProject.Text}";
                AppSettings.Default.Save();
            }
        }

        /// <summary>
        /// Creates an <see cref="InstallFunctions"/> instance wired up to the page's
        /// log output and installer info-bar so all status messages appear in the UI.
        /// </summary>
        private InstallFunctions CreateInstallFunctionsWithLogging()
        {
            var inst = new InstallFunctions();
            inst.StatusUpdated += AppendLog;
            inst.InstallerRunningChanged += OnInstallerRunningChanged;
            return inst;
        }

        /// <summary>
        /// Callback for <see cref="InstallFunctions.InstallerRunningChanged"/>.
        /// Shows or hides the installer info bar and updates the elapsed time display.
        /// </summary>
        private void OnInstallerRunningChanged(bool isRunning, int elapsedSeconds, string operationLabel)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                installerInfoBar.IsOpen = isRunning;
                if (isRunning)
                {
                    txtInstallerLabel.Text = $"{operationLabel} is running…";
                    txtInstallerElapsed.Text = $"Elapsed: {elapsedSeconds}s";
                }
            });
        }

        /// <summary>
        /// Creates a <see cref="BuildDownloadService"/> wired to the page's logging,
        /// confirmation dialogs, and download progress bar.
        /// </summary>
        private BuildDownloadService CreateBuildDownloadService()
        {
            var settings = AppSettings.Default;
            var client = new TfsRestClient(settings.Organization, txtProject.Text.Trim(), settings.PersonalAccessToken);
            var service = new BuildDownloadService(client);
            service.StatusUpdated += AppendLog;
            service.DownloadProgressUpdated += OnDownloadProgressUpdated;

            service.ConfirmAsync = async (message, title) =>
            {
                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = message,
                    PrimaryButtonText = "Yes",
                    CloseButtonText = "No",
                    XamlRoot = this.XamlRoot
                };
                return await dialog.ShowAsync() == ContentDialogResult.Primary;
            };

            return service;
        }

        /// <summary>
        /// Appends a log message to the on-screen log list.
        /// Progress-style messages (e.g. download percentages) update the last entry in-place
        /// to avoid flooding the list with rapidly changing lines.
        /// </summary>
        private void AppendLog(string message)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                bool isProgress = LogEntryViewModel.IsProgressMessage(message);

                // For progress lines, update the last entry in-place instead of adding a new row
                if (isProgress && _logEntries.Count > 0 && _logEntries[^1].IsProgressEntry)
                {
                    _logEntries[^1].Update(message);
                    return;
                }

                var entry = LogEntryViewModel.Create(message);
                _logEntries.Add(entry);
                txtLogCount.Text = $"({_logEntries.Count})";

                // Auto-scroll to the newest log entry
                if (_logEntries.Count > 0)
                    lvLog.ScrollIntoView(_logEntries[^1]);
            });
        }

        /// <summary>
        /// Parses a download progress message and updates the status panel text blocks.
        /// Expected format: "Downloaded: 45.2 MB / 120.0 MB at 5.3 MB/s"
        /// Also handles "Windows Installer/Uninstaller is running…" messages as a special case.
        /// </summary>
        private void UpdateDownloadStatus(DownloadProgressInfo info)
        {
            txtDownloadSpeed.Text = $"{info.SpeedMBps:F2} MB/s";
            string totalText = info.TotalMB > 0
                ? $"{info.DownloadedMB:F2} MB of ~{info.TotalMB:F2} MB"
                : $"{info.DownloadedMB:F2} MB";
            txtDownloadStatus.Text = totalText;
        }

        /// <summary>Collapses the download status panel and resets all progress indicators.</summary>
        private void HideDownloadStatus()
        {
            downloadStatusPanel.Visibility = Visibility.Collapsed;
            downloadProgressBar.Value = 0;
            txtDownloadStatus.Text = "";
            txtDownloadSpeed.Text = "";
        }

        /// <summary>Displays a simple informational dialog with an OK button.</summary>
        private async void ShowMessage(string message, string title = "")
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        /// <summary>
        /// Shows a progress indicator on the button
        /// </summary>
        private void ShowProgress()
        {
            progressBar.IsIndeterminate = true;
            progressBar.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Hides the progress indicator on the button
        /// </summary>
        private void HideProgress()
        {
            progressBar.IsIndeterminate = false;
            progressBar.Visibility = Visibility.Collapsed;
        }

        // ── Event Handlers ───────────────────────────────────────────────

        /// <summary>
        /// When the user picks a different product from the combo box, loads the
        /// corresponding definition ID and project into the text fields and saves settings.
        /// </summary>
        private void CmbProductName_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbProductName.SelectedItem is not ComboBoxItem item) return;
            var selected = (string)item.Content;

            // Look up the definition entry for the selected product name
            var def = AppSettings.Default.UserDefinitionList
                .FirstOrDefault(d => d.StartsWith(selected + "|"));
            if (def != null)
            {
                var parts = def.Split('|');
                txtDefinitionId.Text = parts[1];
                txtProject.Text = parts[2];
            }
            PersistCurrentSettings();
        }

        /// <summary>
        /// Opens a dialog that lets the user add a new product definition (name, definition ID, project)
        /// to the saved list and refreshes the combo box.
        /// </summary>
        private async void BtnAddDefinition_Click(object sender, RoutedEventArgs e)
        {
            var nameBox = new TextBox { Header = "Product Name", PlaceholderText = "e.g. OpenRail Designer" };
            var idBox = new TextBox { Header = "Definition ID", PlaceholderText = "e.g. 6098" };
            var projBox = new TextBox { Header = "Project", PlaceholderText = "e.g. civil" };

            var panel = new StackPanel { Spacing = 10 };
            panel.Children.Add(nameBox);
            panel.Children.Add(idBox);
            panel.Children.Add(projBox);

            var dialog = new ContentDialog
            {
                Title = "Add Product Definition",
                Content = panel,
                PrimaryButtonText = "Add",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var name = nameBox.Text.Trim();
                var id = idBox.Text.Trim();
                var proj = projBox.Text.Trim();

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(proj))
                {
                    ShowMessage("All fields are required.", "Validation");
                    return;
                }

                var entry = $"{name}|{id}|{proj}";
                if (!AppSettings.Default.UserDefinitionList.Any(d => d.StartsWith(name + "|")))
                {
                    AppSettings.Default.UserDefinitionList.Add(entry);
                    AppSettings.Default.Save();
                    PopulateProductCombo();
                }
            }
        }

        /// <summary>
        /// Removes the currently selected product definition from the saved list
        /// and refreshes the combo box, selecting the first remaining item.
        /// </summary>
        private void BtnRemoveDefinition_Click(object sender, RoutedEventArgs e)
        {
            if (cmbProductName.SelectedItem is not ComboBoxItem item) return;
            var productName = (string)item.Content;

            var list = AppSettings.Default.UserDefinitionList;
            var idx = list.FindIndex(d => d.Split('|')[0] == productName);
            if (idx >= 0)
            {
                list.RemoveAt(idx);
                AppSettings.Default.Save();
                PopulateProductCombo();

                if (cmbProductName.Items.Count > 0)
                    cmbProductName.SelectedIndex = 0;
                else
                {
                    txtDefinitionId.Text = "";
                    txtProject.Text = "";
                }
            }
        }

        /// <summary>
        /// Validates inputs, then queries Azure DevOps for available builds matching
        /// the selected product definition. Populates the build list and applies
        /// the latest-major-version highlight.
        /// </summary>
        private async void BtnLoadBuilds_Click(object sender, RoutedEventArgs e)
        {
            var settings = AppSettings.Default;
            var org = settings.Organization;
            var pat = settings.PersonalAccessToken;
            var project = txtProject.Text.Trim();

            if (string.IsNullOrWhiteSpace(org))
            {
                ShowMessage("Organization cannot be empty. Check Settings.");
                return;
            }
            if (string.IsNullOrWhiteSpace(pat))
            {
                ShowMessage("PAT is not set. Check Settings.");
                return;
            }
            if (string.IsNullOrWhiteSpace(project))
            {
                ShowMessage("Project cannot be empty.");
                return;
            }
            if (!int.TryParse(txtDefinitionId.Text, out int definitionId))
            {
                ShowMessage("Invalid Definition ID.");
                return;
            }

            btnLoadBuilds.IsEnabled = false;
            ShowProgress();

            var client = new TfsRestClient(org, project, pat);
            int top = (int)numBuildCount.Value;

            AppendLog("Fetching available builds...");

            try
            {
                _builds = await client.GetAvailableBuildsAsync(definitionId, top);
            }
            catch (System.Net.Http.HttpRequestException)
            {
                ShowMessage("Authentication failed. Please check your Personal Access Token.", "Authentication Error");
                _builds = new();
            }

            _allBuildViewModels = _builds.Select(BuildInfoViewModel.FromBuildInfo).ToList();

            int latestMajor = _allBuildViewModels.Count > 0
                ? _allBuildViewModels.Max(b => b.MajorVersion)
                : 0;
            foreach (var vm in _allBuildViewModels)
                vm.LatestMajorVersion = latestMajor;

            lvBuilds.ItemsSource = _allBuildViewModels;
            buildFilterBox.Text = string.Empty;

            if (_allBuildViewModels.Count > 0)
            {
                lvBuilds.SelectedIndex = 0;
                AppendLog("Builds loaded successfully.");
            }
            else
            {
                AppendLog("No builds found.");
            }

            HideProgress();
            btnLoadBuilds.IsEnabled = true;
            PersistCurrentSettings();
        }

        /// <summary>
        /// Main "Update" workflow:
        /// 1. Downloads build artifacts from Azure DevOps.
        /// 2. Extracts the ZIP contents.
        /// 3. Optionally uninstalls the currently installed version (with clean uninstall).
        /// 4. Runs the setup executable in quiet mode.
        /// If "Download Only" is toggled on, steps 3-4 are skipped.
        /// </summary>
        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            var downloadFolder = AppSettings.Default.DownloadFolder;

            if (lvBuilds.SelectedItem is not BuildInfoViewModel selectedVm || string.IsNullOrWhiteSpace(downloadFolder))
            {
                ShowMessage("Please select a build and set a download folder in Settings.");
                return;
            }

            var selectedBuildInfo = _builds.FirstOrDefault(b => b.BuildId == selectedVm.BuildId);
            if (selectedBuildInfo == null)
            {
                ShowMessage("Build info not found.");
                return;
            }

            if (selectedBuildInfo.Result != "succeeded")
            {
                ShowMessage($"{selectedBuildInfo.DisplayVersion} is not a successful build. Please select another version.");
                return;
            }

            // Create a new CancellationTokenSource for this download session
            _downloadCts?.Dispose();
            _downloadCts = new CancellationTokenSource();

            btnUpdate.IsEnabled = false;
            btnStopDownload.Visibility = Visibility.Visible;
            btnStopDownload.IsEnabled = true;
            ShowProgress();

            var installFunctions = CreateInstallFunctionsWithLogging();
            var downloadService = CreateBuildDownloadService();

            string buildDownloadFolder = Path.Combine(
                downloadFolder,
                selectedBuildInfo.ProductName,
                selectedBuildInfo.DisplayVersion);
            string extractFolder = Path.Combine(buildDownloadFolder, "Extracted");
            Directory.CreateDirectory(extractFolder);

            string project = txtProject.Text.Trim();
            var settings = AppSettings.Default;

            try
            {
                // Download and extract the build artifacts
                await downloadService.DownloadAndExtractArtifactsAsync(
                    selectedBuildInfo.BuildId,
                    buildDownloadFolder, extractFolder,
                    installFunctions,
                    _downloadCts.Token);
            }
            catch (OperationCanceledException)
            {
                AppendLog("Download was cancelled.");
                HideDownloadStatus();
                btnUpdate.IsEnabled = true;
                btnStopDownload.Visibility = Visibility.Collapsed;
                HideProgress();
                return;
            }

            if (toggleDownloadOnly.IsOn)
            {
                AppendLog("Download complete (download-only mode).");
                btnUpdate.IsEnabled = true;
                btnStopDownload.Visibility = Visibility.Collapsed;
                HideProgress();
                PersistCurrentSettings();
                return;
            }

            // Locate the setup executable among the extracted files
            // (must contain both "setup" and the product name in its filename)
            string productName = selectedBuildInfo.ProductName;
            string? setupFile = Directory.GetFiles(extractFolder, "*.exe", SearchOption.AllDirectories)
                .FirstOrDefault(f => Path.GetFileName(f).ToLower().Contains("setup") &&
                                      Path.GetFileName(f).ToLower().Contains(productName.ToLower()));

            if (setupFile == null)
            {
                ShowMessage("Setup file not found. Update process aborted!");
                btnUpdate.IsEnabled = true;
                btnStopDownload.Visibility = Visibility.Collapsed;
                HideProgress();
                return;
            }

            var bentleySoftware = installFunctions.GetInstalledBentleySoftware().ToList();

            installFunctions.UpdateStatus(
                $"Searching for installed version {selectedBuildInfo.MajorVersion}.{selectedBuildInfo.MajorVersionSequence}.* ...");

            // Find a currently installed build whose product name and major version match the selected build
            var matchingInstalled = bentleySoftware.FirstOrDefault(installed =>
                installed.DisplayName.Replace(" ", "").StartsWith(selectedBuildInfo.ProductName, StringComparison.OrdinalIgnoreCase) &&
                installed.MajorVersion == selectedBuildInfo.MajorVersion &&
                installed.MajorVersionSequence == selectedBuildInfo.MajorVersionSequence);

            //Uninstall exisitng version
            if (matchingInstalled != null)
            {
                installFunctions.UpdateStatus($"Installed version found {matchingInstalled.DisplayVersion}");
                bool cleanUninstall = toggleCleanUninstall.IsOn;
                bool uninstallOk = await installFunctions.UninstallSoftwareAsync(matchingInstalled, cleanUninstall);
                if (!uninstallOk)
                {
                    ShowMessage("Uninstallation failed. Update process aborted!");
                    btnUpdate.IsEnabled = true;
                    btnStopDownload.Visibility = Visibility.Collapsed;
                    HideProgress();
                    return;
                }
            }
            else
            {
                installFunctions.UpdateStatus("No matching installed version found. Proceeding with installation.");
            }

            if (File.Exists(setupFile))
            {
                await installFunctions.InstallSoftwareAsync(setupFile);
            }
            else
            {
                installFunctions.UpdateStatus("Setup file not found after extraction.");
            }

            btnUpdate.IsEnabled = true;
            btnStopDownload.Visibility = Visibility.Collapsed;
            HideProgress();
            PersistCurrentSettings();
        }

        /// <summary>Cancels the current download operation.</summary>
        private void BtnStopDownload_Click(object sender, RoutedEventArgs e)
        {
            _downloadCts?.Cancel();
            btnStopDownload.IsEnabled = false;
            AppendLog("Cancellation requested — waiting for download to stop…");
        }

        /// <summary>
        /// Shows a dialog listing all Bentley software installed on the machine.
        /// The user can select an entry and uninstall it (optionally with clean uninstall)
        /// directly from the dialog.
        /// </summary>
        private async void BtnShowBentleySoftware_Click(object sender, RoutedEventArgs e)
        {
            var software = new InstallFunctions().GetInstalledBentleySoftware().ToList();

            if (software.Count == 0)
            {
                ShowMessage("No Bentley software found on this machine.");
                return;
            }

            // Build the dialog UI programmatically: header row, list, uninstall controls, and log
            var headerGrid = new Grid { Padding = new Thickness(12, 8, 12, 8) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var headerName = new TextBlock
            {
                Text = "Product",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 12
            };
            var headerVersion = new TextBlock
            {
                Text = "Version",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 12
            };
            Grid.SetColumn(headerName, 0);
            Grid.SetColumn(headerVersion, 1);
            headerGrid.Children.Add(headerName);
            headerGrid.Children.Add(headerVersion);

            // Software list with DataTemplate for Name + Version columns
            var templateXaml =
                @"<DataTemplate
                    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
                    <Grid Padding=""4,6"" ColumnSpacing=""8"">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width=""2*"" />
                            <ColumnDefinition Width=""1*"" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column=""0""
                                   Text=""{Binding DisplayName}""
                                   Style=""{StaticResource BodyTextBlockStyle}""
                                   TextWrapping=""Wrap""
                                   VerticalAlignment=""Center"" />
                        <TextBlock Grid.Column=""1""
                                   Text=""{Binding DisplayVersion}""
                                   Style=""{StaticResource BodyTextBlockStyle}""
                                   Foreground=""DodgerBlue""
                                   TextWrapping=""Wrap""
                                   VerticalAlignment=""Center"" />
                    </Grid>
                </DataTemplate>";

            var listView = new ListView
            {
                SelectionMode = ListViewSelectionMode.Single,
                MaxHeight = 300,
                ItemsSource = software,
                ItemTemplate = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(templateXaml)
            };

            // Clean uninstall checkbox
            var cleanUninstallCheck = new CheckBox
            {
                Content = "Clean Uninstall",
                IsChecked = false,
                Margin = new Thickness(0, 8, 0, 0)
            };

            // Uninstall button
            var uninstallBtn = new Button
            {
                Content = "Uninstall Selected",
                Style = (Style)Application.Current.Resources["AccentButtonStyle"],
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 36,
                Margin = new Thickness(0, 4, 0, 0),
                IsEnabled = false
            };

            // Log output
            var logBlock = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Code, Consolas"),
                FontSize = 11,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };
            var logScroll = new ScrollViewer
            {
                MaxHeight = 120,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = logBlock,
                Margin = new Thickness(0, 8, 0, 0),
                Padding = new Thickness(10, 8, 10, 8),
                BorderThickness = new Thickness(1),
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                CornerRadius = new CornerRadius(4)
            };

            // Enable uninstall only when something is selected
            listView.SelectionChanged += (_, _) =>
            {
                uninstallBtn.IsEnabled = listView.SelectedItem != null;
            };

            // Uninstall handler
            uninstallBtn.Click += async (_, _) =>
            {
                if (listView.SelectedItem is not InstallFunctions.InstalledSoftwareInfo selected)
                    return;

                bool cleanUninstall = cleanUninstallCheck.IsChecked == true;
                uninstallBtn.IsEnabled = false;
                listView.IsEnabled = false;
                logBlock.Text = string.Empty;

                var inst = new InstallFunctions();
                inst.StatusUpdated += (msg) =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (msg.StartsWith("Windows Installer is running.", StringComparison.OrdinalIgnoreCase)
                            || msg.StartsWith("Windows Uninstaller is running.", StringComparison.OrdinalIgnoreCase))
                        {
                            var lines = logBlock.Text.Split(Environment.NewLine).ToList();
                            int idx = lines.FindLastIndex(l => l.StartsWith("Windows Installer is running.") || l.StartsWith("Windows Uninstaller is running."));
                            if (idx >= 0)
                                lines[idx] = msg;
                            else
                                lines.Add(msg);
                            logBlock.Text = string.Join(Environment.NewLine, lines);
                        }
                        else
                        {
                            logBlock.Text += (logBlock.Text.Length > 0 ? Environment.NewLine : "") + msg;
                        }
                        logScroll.ChangeView(null, logScroll.ScrollableHeight, null);
                        AppendLog(msg);
                    });
                };

                bool success = await inst.UninstallSoftwareAsync(selected, cleanUninstall);

                if (success)
                {
                    var refreshed = new InstallFunctions().GetInstalledBentleySoftware().ToList();
                    listView.ItemsSource = refreshed;
                    software = refreshed;
                }

                listView.IsEnabled = true;
                uninstallBtn.IsEnabled = listView.SelectedItem != null;
            };

            // Assemble layout
            var panel = new StackPanel { Spacing = 0, MinWidth = 460 };
            panel.Children.Add(headerGrid);
            panel.Children.Add(listView);
            panel.Children.Add(cleanUninstallCheck);
            panel.Children.Add(uninstallBtn);
            panel.Children.Add(logScroll);

            var dialog = new ContentDialog
            {
                Title = "Installed Bentley Software",
                Content = panel,
                CloseButtonText = "Close",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        /// <summary>
        /// Filters the build list in real time as the user types into the search box.
        /// Matches against product name, version, result status, and finish time.
        /// </summary>
        private void BuildFilterBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            var filter = sender.Text?.Trim();
            if (string.IsNullOrEmpty(filter))
            {
                lvBuilds.ItemsSource = _allBuildViewModels;
                return;
            }

            lvBuilds.ItemsSource = _allBuildViewModels
                .Where(b => b.ProductName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                         || b.DisplayVersion.Contains(filter, StringComparison.OrdinalIgnoreCase)
                         || b.Result.Contains(filter, StringComparison.OrdinalIgnoreCase)
                         || b.FinishTime.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>Clears all log entries and resets the download status panel.</summary>
        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            _logEntries.Clear();
            txtLogCount.Text = string.Empty;
            HideDownloadStatus();
        }

        /// <summary>
        /// Callback for <see cref="BuildDownloadService.DownloadProgressUpdated"/>.
        /// Updates the download progress bar on the UI thread.
        /// Shows the status panel when a download starts, and updates the bar value (0–100%).
        /// </summary>
        private void OnDownloadProgressUpdated(DownloadProgressInfo info)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                // Make the status panel visible on the first progress update
                if (downloadStatusPanel.Visibility == Visibility.Collapsed)
                {
                    downloadStatusPanel.Visibility = Visibility.Visible;
                    downloadProgressBar.Maximum = 100;
                }
                downloadProgressBar.Value = info.Percentage;
                UpdateDownloadStatus(info);

                if (info.Percentage >= 100)
                {
                    HideDownloadStatus();
                }
            });
        }
    }
}