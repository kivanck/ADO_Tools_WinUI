using System;
using ADO_Tools_WinUI.Pages;
using ADO_Tools_WinUI.Services;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace ADO_Tools_WinUI
{
    public sealed partial class SettingsWindow : Window
    {
        private readonly SettingsPage _settingsPage;

        public SettingsWindow()
        {
            InitializeComponent();

            AppWindow.SetIcon("Assets/ADOToolsSquare256Pixel.ico");
            Title = "Settings — ADO Tools";

            ResizeWindowToLogicalSize(600, 820);

            _settingsPage = new SettingsPage();
            _settingsPage.Margin = new Thickness(20);
            ((Microsoft.UI.Xaml.Controls.Grid)Content).Children.Add(_settingsPage);

            _settingsPage.LoadSettings();

            Closed += SettingsWindow_Closed;
        }

        /// <summary>
        /// Raised when the search index is rebuilt so the main window can refresh.
        /// </summary>
        public event Action? IndexRebuilt;

        /// <summary>
        /// Wires the <see cref="SettingsPage.IndexRebuilt"/> event to this window's event.
        /// Call this after construction from the main window.
        /// </summary>
        public void WireIndexRebuilt(Action handler)
        {
            IndexRebuilt += handler;
            _settingsPage.IndexRebuilt += () => IndexRebuilt?.Invoke();
        }

        private void SettingsWindow_Closed(object sender, WindowEventArgs args)
        {
            _settingsPage.SaveSettings();
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
