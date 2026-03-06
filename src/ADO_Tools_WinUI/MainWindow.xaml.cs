using System;
using ADO_Tools_WinUI.Pages;
using ADO_Tools_WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ADO_Tools_WinUI
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Set a reasonable desktop window size
            var appWindow = this.AppWindow;
            appWindow.Resize(new Windows.Graphics.SizeInt32(1100, 700));

            Closed += MainWindow_Closed;
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
    }
}
