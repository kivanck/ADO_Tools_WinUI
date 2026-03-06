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
        public MainWindow()
        {
            InitializeComponent();

            // Extend content into the title bar area to reclaim vertical space
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

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

        private async void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsPage = new SettingsPage();
            settingsPage.LoadSettings();

            var dialog = new ContentDialog
            {
                Title = "Settings",
                Content = settingsPage,
                CloseButtonText = "Close",
                XamlRoot = Content.XamlRoot
            };

            await dialog.ShowAsync();

            settingsPage.SaveSettings();
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
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

