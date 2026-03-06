using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using ADO_Tools_WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ADO_Tools_WinUI.Pages
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
        }

        public void LoadSettings()
        {
            txtOrganization.Text = AppSettings.Default.Organization;
            txtProject.Text = AppSettings.Default.Project;
            txtPAT.Password = AppSettings.Default.PersonalAccessToken;
            txtRootFolder.Text = AppSettings.Default.RootFolder;
            txtDownloadFolder.Text = AppSettings.Default.DownloadFolder;
        }

        public void SaveSettings()
        {
            AppSettings.Default.Organization = txtOrganization.Text.Trim();
            AppSettings.Default.Project = txtProject.Text.Trim();
            AppSettings.Default.PersonalAccessToken = txtPAT.Password.Trim();
            AppSettings.Default.RootFolder = txtRootFolder.Text.Trim();
            AppSettings.Default.DownloadFolder = txtDownloadFolder.Text.Trim();
            AppSettings.Default.Save();
        }

        private async void BtnValidate_Click(object sender, RoutedEventArgs e)
        {
            string pat = txtPAT.Password.Trim();
            string org = txtOrganization.Text.Trim();

            if (string.IsNullOrEmpty(pat) || string.IsNullOrEmpty(org))
            {
                ValidationStatus.Message = "Organisation and PAT are both required.";
                ValidationStatus.Severity = InfoBarSeverity.Warning;
                ValidationStatus.IsOpen = true;
                return;
            }

            btnValidate.IsEnabled = false;
            ValidationStatus.Message = "Validating...";
            ValidationStatus.Severity = InfoBarSeverity.Informational;
            ValidationStatus.IsOpen = true;

            bool isValid = await ValidatePATAsync(pat, org);

            if (isValid)
            {
                ValidationStatus.Message = "Connection successful.";
                ValidationStatus.Severity = InfoBarSeverity.Success;
                SaveSettings();
            }
            else
            {
                ValidationStatus.Message = "Authentication failed. Check your PAT and Organisation.";
                ValidationStatus.Severity = InfoBarSeverity.Error;
            }

            btnValidate.IsEnabled = true;
        }

        private static async Task<bool> ValidatePATAsync(string pat, string organization)
        {
            try
            {
                var handler = new HttpClientHandler { AllowAutoRedirect = false };
                using var client = new HttpClient(handler);
                var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
                var response = await client.GetAsync(
                    $"https://dev.azure.com/{organization}/_apis/projects?api-version=7.1");
                return response.StatusCode == System.Net.HttpStatusCode.OK;
            }
            catch
            {
                return false;
            }
        }
    }
}