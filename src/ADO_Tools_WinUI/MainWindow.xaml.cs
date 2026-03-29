using System;
using ADO_Tools_WinUI.Pages;
using ADO_Tools_WinUI.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace ADO_Tools_WinUI
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Extend content into the title bar area to reclaim vertical space
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            // Set the window/taskbar icon
            AppWindow.SetIcon("Assets/ADOToolsSquare256Pixel.ico");

            // Restore persisted window size or use defaults
            RestoreWindowSize();

            // Wire IndexRebuilt so Work Items page reloads the search cache
            settingsPage.IndexRebuilt += () =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (FindWorkItemsPage() is WorkItemsPage page)
                        page.ReloadSearchCache();
                });
            };

            MainTabView.SelectionChanged += MainTabView_SelectionChanged;
            Closed += MainWindow_Closed;
            AppWindow.Changed += AppWindow_Changed;
        }

        private void RestoreWindowSize()
        {
            var s = AppSettings.Default;

            if (s.IsMaximized)
            {
                // Set a reasonable initial size first, then maximize
                ResizeWindowToLogicalSize((int)s.WindowWidth, (int)s.WindowHeight);
                if (AppWindow.Presenter is OverlappedPresenter presenter)
                    presenter.Maximize();
            }
            else
            {
                ResizeWindowToLogicalSize((int)s.WindowWidth, (int)s.WindowHeight);
            }
        }

        private void SaveWindowSize()
        {
            var s = AppSettings.Default;

            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                s.IsMaximized = presenter.State == OverlappedPresenterState.Maximized;
            }

            // Only save dimensions when not maximized (so restoring remembers the windowed size)
            if (!s.IsMaximized)
            {
                var hwnd = WindowNative.GetWindowHandle(this);
                var dpi = PInvoke.User32.GetDpiForWindow(hwnd);
                var scalingFactor = dpi / 96.0;

                s.WindowWidth = (int)(AppWindow.Size.Width / scalingFactor);
                s.WindowHeight = (int)(AppWindow.Size.Height / scalingFactor);
            }
        }

        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidSizeChange || args.DidPresenterChange)
                SaveWindowSize();
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

        private void MainTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool goingToSettings = MainTabView.SelectedItem == settingsTab;
            bool leavingSettings = e.RemovedItems.Count > 0 && e.RemovedItems[0] == settingsTab;

            if (goingToSettings)
                settingsPage.LoadSettings();

            if (leavingSettings)
                settingsPage.SaveSettings();
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
            // Save settings if we're closing while on the settings tab
            if (MainTabView.SelectedItem == settingsTab)
                settingsPage.SaveSettings();

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

