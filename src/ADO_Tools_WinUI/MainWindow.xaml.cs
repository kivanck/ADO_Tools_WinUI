using System;
using ADO_Tools_WinUI.Pages;
using ADO_Tools_WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace ADO_Tools_WinUI
{
    public sealed partial class MainWindow : Window
    {
        private SettingsWindow? _settingsWindow;

        public MainWindow()
        {
            InitializeComponent();

            // Extend content into the title bar area to reclaim vertical space
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            // Set the window/taskbar icon
            AppWindow.SetIcon("Assets/ADOToolsSquare256Pixel.ico");

            // Resize using DPI-aware logical size so it looks correct at any scaling
            ResizeWindowToLogicalSize(1280, 970);

            Closed += MainWindow_Closed;
        }

        private void ResizeWindowToLogicalSize(int logicalWidth, int logicalHeight)
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var dpi = PInvoke.User32.GetDpiForWindow(hwnd);
            var scalingFactor = dpi / 96.0;

            var physicalWidth = (int)(logicalWidth * scalingFactor);
            var physicalHeight = (int)(logicalHeight * scalingFactor);

            AppWindow.Resize(new Windows.Graphics.SizeInt32(physicalWidth, physicalHeight));
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            // If the settings window is already open, bring it to front
            if (_settingsWindow != null)
            {
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new SettingsWindow();

            _settingsWindow.WireIndexRebuilt(() =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (FindWorkItemsPage() is WorkItemsPage page)
                        page.ReloadSearchCache();
                });
            });

            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Activate();
        }

        private WorkItemsPage? FindWorkItemsPage()
        {
            foreach (var item in MainTabView.TabItems)
            {
                if (item is TabViewItem tabItem && tabItem.Content is WorkItemsPage page)
                    return page;
            }
            return null;
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            _settingsWindow?.Close();
            AppSettings.Default.Save();
        }

        private static class PInvoke
        {
            public static class User32
            {
                [System.Runtime.InteropServices.DllImport("user32.dll")]
                public static extern uint GetDpiForWindow(nint hwnd);
            }
        }
    }
}

