using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Graphics;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace WinUIApp
{
    public sealed partial class SoftwareDownloaderWindow : Window
    {
        private readonly string _personalAccessToken;
        private readonly string _organization;

        public SoftwareDownloaderWindow(string pat, string organization)
        {
            InitializeComponent();

            // Replace system title bar with custom draggable region
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            _personalAccessToken = pat;
            _organization = organization;

            // Resize after content loads so we can read the DPI scale
            if (Content is FrameworkElement root)
            {
                root.Loaded += (s, e) =>
                {
                    var scale = root.XamlRoot.RasterizationScale; // 1.5, 2.0, etc.
                    AppWindow.Resize(new SizeInt32(
                        (int)(960 * scale),
                        (int)(870 * scale)
                    ));
                };
            }
        }

        private void CmbProductName_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // TODO: When product changes, update definition ID and project fields from stored definitions
        }

        private void BtnAddDefinition_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Show a ContentDialog to add a new product definition
        }

        private void BtnRemoveDefinition_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Remove the currently selected product definition
        }

        private void BtnLoadBuilds_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Load builds from Azure DevOps using the definition ID, project, and build count
        }

        private void BtnShowBentleySoftware_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Show installed Bentley software in a ContentDialog
        }

        private async void BtnBrowseDownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            picker.SuggestedStartLocation = PickerLocationId.Downloads;
            picker.FileTypeFilter.Add("*");

            // Associate the picker with this window
            var hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                txtDownloadFolderTest.Text = folder.Path;
            }
        }

        private void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Run the update process (download, extract, optionally uninstall + install)
            // Use toggleDownloadOnly.IsOn and toggleCleanUninstall.IsOn for options
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            txtLog.Text = string.Empty;
        }

        private void AppendLog(string message)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                txtLog.Text += message + Environment.NewLine;
                logScrollViewer.ChangeView(null, logScrollViewer.ScrollableHeight, null);
            });
        }
    }
}
