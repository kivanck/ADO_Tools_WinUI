using System;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ADO_Tools.Services;
using ADO_Tools_WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace ADO_Tools_WinUI.Pages
{
    public sealed partial class SoftwareDownloadPage : Page
    {
        private List<BuildInfoViewModel> _allBuildViewModels = new();
        private List<TFSFunctions.BuildInfo> _builds = new();
        private readonly ObservableCollection<LogEntryViewModel> _logEntries = new();

       

        public SoftwareDownloadPage()
        {
            InitializeComponent();
            lvLog.ItemsSource = _logEntries;
            Loaded += SoftwareDownloadPage_Loaded;
        }

        private void SoftwareDownloadPage_Loaded(object sender, RoutedEventArgs e)
        {
            var settings = AppSettings.Default;

            txtDownloadFolder.Text = settings.DownloadFolder;
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

        private static readonly string[] DefaultDefinitions =
        {
            "OpenRail Designer|6098|civil",
            "OpenRoads Designer|6057|civil",
            "Overhead Line Designer|6289|civil",
            "Microstation|5311|PowerPlatform"
        };

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

        private void PersistCurrentSettings()
        {
            var s = AppSettings.Default;
            s.ProductName = (cmbProductName.SelectedItem as ComboBoxItem)?.Content as string ?? "";
            s.DefinitionId = txtDefinitionId.Text;
            s.Project = txtProject.Text;
            s.DownloadFolder = txtDownloadFolder.Text;
            s.BuildCount = (int)numBuildCount.Value;
            s.Save();
        }

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

        private InstallFunctions CreateInstallFunctionsWithLogging()
        {
            var inst = new InstallFunctions();
            inst.StatusUpdated += AppendLog;
            inst.InstallerRunningChanged += OnInstallerRunningChanged;
            return inst;
        }

        private void OnInstallerRunningChanged(bool isRunning, int elapsedSeconds)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                installerInfoBar.IsOpen = isRunning;
                if (isRunning)
                {
                    txtInstallerElapsed.Text = $"Elapsed: {elapsedSeconds}s";
                }
            });
        }

        private TFSFunctions CreateTFSFunctionsWithLogging()
        {
            var tfs = new TFSFunctions();
            tfs.StatusUpdated += AppendLog;

            tfs.ConfirmAsync = async (message, title) =>
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

            tfs.ShowMessageAsync = async (message, title) =>
            {
                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = message,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            };

            tfs.ProgressUpdated += Tfs_ProgressUpdated;

            return tfs;
        }

        private void AppendLog(string message)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                bool isProgress = LogEntryViewModel.IsProgressMessage(message);

                if (isProgress)
                {
                    UpdateDownloadStatus(message);
                }
                else if (downloadStatusPanel.Visibility == Visibility.Visible)
                {
                    HideDownloadStatus();
                }

                // For progress lines, update the last entry in-place instead of adding a new row
                if (isProgress && _logEntries.Count > 0 && _logEntries[^1].IsProgressEntry)
                {
                    _logEntries[^1].Update(message);
                    return;
                }

                var entry = LogEntryViewModel.Create(message);
                _logEntries.Add(entry);
                txtLogCount.Text = $"({_logEntries.Count})";

                if (_logEntries.Count > 0)
                    lvLog.ScrollIntoView(_logEntries[^1]);
            });
        }

        private void UpdateDownloadStatus(string message)
        {
            if (message.StartsWith("Windows Installer is running", StringComparison.OrdinalIgnoreCase))
            {
                txtDownloadSpeed.Text = "";
                txtDownloadStatus.Text = message;
                return;
            }

            // Existing "Downloaded:" parsing logic
            var speedPart = message.Substring(message.LastIndexOf(" at ") + 4);
            txtDownloadSpeed.Text = speedPart;

            var atIndex = message.IndexOf(" at ");
            var statsPart = message.Replace("Downloaded: ", "").Substring(0, atIndex - "Downloaded: ".Length + 1);
            txtDownloadStatus.Text = statsPart;
        }

        private void HideDownloadStatus()
        {
            downloadStatusPanel.Visibility = Visibility.Collapsed;
            downloadProgressBar.Value = 0;
            txtDownloadStatus.Text = "";
            txtDownloadSpeed.Text = "";
        }

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

        // ── Event Handlers ───────────────────────────────────────────────

        private void CmbProductName_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbProductName.SelectedItem is not ComboBoxItem item) return;
            var selected = (string)item.Content;

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
            progressBar.IsIndeterminate = true;
            progressBar.Visibility = Visibility.Visible;

            var tfs = CreateTFSFunctionsWithLogging();
            int top = (int)numBuildCount.Value;

            AppendLog("Fetching available builds...");

            _builds = await tfs.GetAvailableBuildsAsync(org, project, definitionId, pat, top);

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

            progressBar.IsIndeterminate = false;
            progressBar.Visibility = Visibility.Collapsed;
            btnLoadBuilds.IsEnabled = true;
            PersistCurrentSettings();
        }

        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (lvBuilds.SelectedItem is not BuildInfoViewModel selectedVm || string.IsNullOrWhiteSpace(txtDownloadFolder.Text))
            {
                ShowMessage("Please select a build and download folder.");
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

            btnUpdate.IsEnabled = false;
            progressBar.IsIndeterminate = true;
            progressBar.Visibility = Visibility.Visible;

            var installFunctions = CreateInstallFunctionsWithLogging();
            var tfsFunctions = CreateTFSFunctionsWithLogging();

            string downloadFolder = Path.Combine(
                txtDownloadFolder.Text,
                selectedBuildInfo.ProductName,
                selectedBuildInfo.DisplayVersion);
            string extractFolder = Path.Combine(downloadFolder, "Extracted");
            Directory.CreateDirectory(extractFolder);

            string project = txtProject.Text.Trim();
            var settings = AppSettings.Default;

            // Download and extract the build artifacts
            await tfsFunctions.DownloadLatestBuildArtifacts(
                settings.Organization, project, selectedBuildInfo.BuildId,
                downloadFolder, extractFolder,
                settings.PersonalAccessToken, installFunctions);

            if (toggleDownloadOnly.IsOn)
            {
                AppendLog("Download complete (download-only mode).");
                btnUpdate.IsEnabled = true;
                progressBar.IsIndeterminate = false;
                progressBar.Visibility = Visibility.Collapsed;
                PersistCurrentSettings();
                return;
            }

            string productName = selectedBuildInfo.ProductName;
            string setupFile = Directory.GetFiles(extractFolder, "*.exe", SearchOption.AllDirectories)
                .FirstOrDefault(f => Path.GetFileName(f).ToLower().Contains("setup") &&
                                     Path.GetFileName(f).ToLower().Contains(productName.ToLower()));

            if (setupFile == null)
            {
                ShowMessage("Setup file not found. Update process aborted!");
                btnUpdate.IsEnabled = true;
                progressBar.IsIndeterminate = false;
                progressBar.Visibility = Visibility.Collapsed;
                return;
            }

            var bentleySoftware = installFunctions.GetInstalledBentleySoftware().ToList();

            installFunctions.UpdateStatus(
                $"Searching for installed version {selectedBuildInfo.MajorVersion}.{selectedBuildInfo.MajorVersionSequence}.* ...");

            var matchingInstalled = bentleySoftware.FirstOrDefault(installed =>
                installed.DisplayName.Replace(" ", "").StartsWith(selectedBuildInfo.ProductName, StringComparison.OrdinalIgnoreCase) &&
                installed.MajorVersion == selectedBuildInfo.MajorVersion &&
                installed.MajorVersionSequence == selectedBuildInfo.MajorVersionSequence);

            if (matchingInstalled != null)
            {
                installFunctions.UpdateStatus($"Installed version found {matchingInstalled.DisplayVersion}");
                bool cleanUninstall = toggleCleanUninstall.IsOn;
                bool uninstallOk = await installFunctions.UninstallSoftwareAsync(matchingInstalled, cleanUninstall);
                if (!uninstallOk)
                {
                    ShowMessage("Uninstallation failed. Update process aborted!");
                    btnUpdate.IsEnabled = true;
                    progressBar.IsIndeterminate = false;
                    progressBar.Visibility = Visibility.Collapsed;
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
            progressBar.IsIndeterminate = false;
            progressBar.Visibility = Visibility.Collapsed;
            PersistCurrentSettings();
        }

        private async void BtnBrowseDownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            picker.SuggestedStartLocation = PickerLocationId.Downloads;
            picker.FileTypeFilter.Add("*");

            // WinUI 3 requires initializing the picker with the window handle
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                txtDownloadFolder.Text = folder.Path;
                PersistCurrentSettings();
            }
        }

        private async void BtnShowBentleySoftware_Click(object sender, RoutedEventArgs e)
        {
            var software = new InstallFunctions().GetInstalledBentleySoftware().ToList();

            if (software.Count == 0)
            {
                ShowMessage("No Bentley software found on this machine.");
                return;
            }

            // Column headers
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
                            && logBlock.Text.Contains("Windows Installer is running."))
                        {
                            var lines = logBlock.Text.Split(Environment.NewLine).ToList();
                            int idx = lines.FindLastIndex(l => l.StartsWith("Windows Installer is running."));
                            if (idx >= 0)
                                lines[idx] = msg;
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

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            _logEntries.Clear();
            txtLogCount.Text = string.Empty;
            HideDownloadStatus();
        }

        private void Tfs_ProgressUpdated(double percentage)
        {
            // Ensure we update the UI on the main thread
            DispatcherQueue.TryEnqueue(() =>
            {

                // -- Progress Bar Logic --
                if (downloadStatusPanel.Visibility == Visibility.Collapsed)
                {
                    downloadStatusPanel.Visibility = Visibility.Visible;
                    downloadProgressBar.Maximum = 100; // Since we are passing percentage (0-100)
                }
                downloadProgressBar.Value = percentage;

                // Optionally hide them when it reaches 100%
                if (percentage >= 100)
                {
                    
                }
            });
        }
    }
}