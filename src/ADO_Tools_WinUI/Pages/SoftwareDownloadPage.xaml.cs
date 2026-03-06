using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ADO_Tools.Services;
using ADO_Tools_WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace ADO_Tools_WinUI.Pages
{
    public sealed partial class SoftwareDownloadPage : Page
    {
        private readonly Dictionary<string, string> _buildIdMap = new();
        private List<TFSFunctions.BuildInfo> _builds = new();

        public SoftwareDownloadPage()
        {
            InitializeComponent();
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

        // ?? Helpers ??????????????????????????????????????????????????????

        private static readonly string[] DefaultDefinitions =
        {
            "OpenRail Designer|6098|civil",
            "OpenRoads Designer|6057|civil",
            "Overhead Line Designer|6289|civil"
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
            return inst;
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

            return tfs;
        }

        private void AppendLog(string message)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                txtLog.Text += message + Environment.NewLine;
                logScrollViewer.ChangeView(null, logScrollViewer.ScrollableHeight, null);
            });
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

        // ?? Event Handlers ???????????????????????????????????????????????

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
            var nameBox = new TextBox { Header = "Product Name", PlaceholderText = "e.g. OpenRoads Designer" };
            var idBox = new TextBox { Header = "Definition ID", PlaceholderText = "e.g. 6057" };
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

            cmbBuilds.Items.Clear();
            _buildIdMap.Clear();

            foreach (var build in _builds)
            {
                string display = $"{build.ProductName,-25}|{build.DisplayVersion,-13}|{build.Result,12}|{build.FinishTime,20}";
                cmbBuilds.Items.Add(new ComboBoxItem { Content = display });
                _buildIdMap[display] = build.BuildId;
            }

            if (cmbBuilds.Items.Count > 0)
            {
                cmbBuilds.SelectedIndex = 0;
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
            if (cmbBuilds.SelectedItem is not ComboBoxItem selectedItem || string.IsNullOrWhiteSpace(txtDownloadFolder.Text))
            {
                ShowMessage("Please select a build and download folder.");
                return;
            }

            var selectedDisplay = (string)selectedItem.Content;
            if (!_buildIdMap.TryGetValue(selectedDisplay, out var buildId))
            {
                ShowMessage("Could not resolve the selected build.");
                return;
            }

            var selectedBuildInfo = _builds.FirstOrDefault(b => b.BuildId == buildId);
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

            await tfsFunctions.DownloadLatestBuildArtifacts(
                settings.Organization, project, buildId,
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
            var installFunctions = new InstallFunctions();
            var software = installFunctions.GetInstalledBentleySoftware().ToList();

            if (software.Count == 0)
            {
                ShowMessage("No Bentley software found on this machine.");
                return;
            }

            var listView = new ListView
            {
                SelectionMode = ListViewSelectionMode.None,
                MaxHeight = 400
            };

            foreach (var sw in software)
            {
                listView.Items.Add(new TextBlock
                {
                    Text = $"{sw.DisplayName}  —  {sw.DisplayVersion}",
                    TextWrapping = TextWrapping.NoWrap
                });
            }

            var dialog = new ContentDialog
            {
                Title = "Installed Bentley Software",
                Content = listView,
                CloseButtonText = "Close",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            txtLog.Text = string.Empty;
        }
    }
}