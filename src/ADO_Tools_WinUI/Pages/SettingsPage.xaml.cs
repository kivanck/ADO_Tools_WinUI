using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using ADO_Tools.Services;
using ADO_Tools_WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ADO_Tools_WinUI.Pages
{
    public sealed partial class SettingsPage : Page
    {
        public event Action? IndexRebuilt;

        public SettingsPage()
        {
            InitializeComponent();
        }

        public void LoadSettings()
        {
            var s = AppSettings.Default;
            txtOrganization.Text = s.Organization;
            txtProject.Text = s.Project;
            txtPAT.Password = s.PersonalAccessToken;
            txtRootFolder.Text = s.RootFolder;
            txtDownloadFolder.Text = s.DownloadFolder;
            txtSearchAreaPath.Text = s.SearchAreaPath;

            if (DateTimeOffset.TryParse(s.SearchCutoffDate, out var cutoff))
                dpCutoffDate.Date = cutoff;
            else
                dpCutoffDate.Date = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);

            txtSearchResultColumns.Text = string.Join(", ", s.SearchResultColumns);

            UpdateSettingsCacheStatus();
        }

        public void SaveSettings()
        {
            var s = AppSettings.Default;
            s.Organization = txtOrganization.Text.Trim();
            s.Project = txtProject.Text.Trim();
            s.PersonalAccessToken = txtPAT.Password.Trim();
            s.RootFolder = txtRootFolder.Text.Trim();
            s.DownloadFolder = txtDownloadFolder.Text.Trim();
            s.SearchAreaPath = txtSearchAreaPath.Text.Trim();
            s.SearchCutoffDate = dpCutoffDate.Date?.ToString("yyyy-MM-dd") ?? "2023-01-01";

            var cols = txtSearchResultColumns.Text
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(c => c.Length > 0)
                .ToList();
            s.SearchResultColumns = cols.Count > 0 ? cols : s.SearchResultColumns;

            s.Save();
        }

        private void UpdateSettingsCacheStatus()
        {
            var s = AppSettings.Default;
            if (string.IsNullOrWhiteSpace(s.Organization) || string.IsNullOrWhiteSpace(s.Project))
            {
                lblSettingsCacheStatus.Text = "Configure connection settings first.";
                return;
            }

            string modelDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Models");
            if (!File.Exists(Path.Combine(modelDir, "model.onnx")))
            {
                lblSettingsCacheStatus.Text = "Embedding model not found.";
                return;
            }

            try
            {
                string cacheDir = Path.Combine(AppContext.BaseDirectory, "EmbeddingCache");
                using var search = new SemanticSearchService(modelDir, cacheDir);
                string areaPath = s.SearchAreaPath?.Trim() ?? "";
                if (search.TryLoadCache(s.Organization, s.Project, areaPath))
                    lblSettingsCacheStatus.Text = $"Current cache: {search.CachedItemCount} items indexed.";
                else
                    lblSettingsCacheStatus.Text = "No cache found. Click Build Index to create one.";
            }
            catch
            {
                lblSettingsCacheStatus.Text = "Unable to read cache.";
            }
        }

        private async void BtnSettingsBuildIndex_Click(object sender, RoutedEventArgs e)
        {
            await RunIndexBuildAsync(forceRebuild: false);
        }

        private async void BtnSettingsForceRebuild_Click(object sender, RoutedEventArgs e)
        {
            // Can't use ContentDialog here because SettingsPage is already inside one.
            // Use a two-click confirmation: disable Build, rename Force Rebuild to "Confirm Rebuild?"
            if (btnSettingsForceRebuild.Tag is not "confirming")
            {
                btnSettingsForceRebuild.Tag = "confirming";
                ((StackPanel)btnSettingsForceRebuild.Content).Children.OfType<TextBlock>().First().Text = "Confirm Rebuild?";
                btnSettingsBuildIndex.IsEnabled = false;
                lblSettingsCacheStatus.Text = "Click again to confirm full rebuild, or wait to cancel.";

                // Auto-cancel after 5 seconds
                await Task.Delay(5000);
                if (btnSettingsForceRebuild.Tag is "confirming")
                {
                    btnSettingsForceRebuild.Tag = null;
                    ((StackPanel)btnSettingsForceRebuild.Content).Children.OfType<TextBlock>().First().Text = "Force Rebuild";
                    btnSettingsBuildIndex.IsEnabled = true;
                    lblSettingsCacheStatus.Text = "";
                }
                return;
            }

            // Second click — confirmed
            btnSettingsForceRebuild.Tag = null;
            ((StackPanel)btnSettingsForceRebuild.Content).Children.OfType<TextBlock>().First().Text = "Force Rebuild";
            await RunIndexBuildAsync(forceRebuild: true);
        }

        private async Task RunIndexBuildAsync(bool forceRebuild)
        {
            SaveSettings();
            var s = AppSettings.Default;

            if (string.IsNullOrWhiteSpace(s.Organization) || string.IsNullOrWhiteSpace(s.PersonalAccessToken) || string.IsNullOrWhiteSpace(s.Project))
            {
                lblSettingsCacheStatus.Text = "Organisation, Project, and PAT are required.";
                return;
            }

            string modelDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Models");
            if (!File.Exists(Path.Combine(modelDir, "model.onnx")))
            {
                lblSettingsCacheStatus.Text = "Embedding model not found.";
                return;
            }

            btnSettingsBuildIndex.IsEnabled = false;
            btnSettingsForceRebuild.IsEnabled = false;
            indexProgressRing.IsActive = true;

            try
            {
                string cacheDir = Path.Combine(AppContext.BaseDirectory, "EmbeddingCache");
                using var search = new SemanticSearchService(modelDir, cacheDir);
                search.StatusUpdated += msg =>
                    DispatcherQueue.TryEnqueue(() => lblSettingsCacheStatus.Text = msg);

                var tfs = new TfsRestClient(s.Organization, s.Project, s.PersonalAccessToken);
                string areaPath = s.SearchAreaPath?.Trim() ?? "";

                var (added, total) = await search.BuildOrUpdateCacheAsync(
                    tfs,
                    s.Organization,
                    s.Project,
                    areaPath,
                    forceRebuild: forceRebuild,
                    progressCallback: (current, count) =>
                    {
                        DispatcherQueue.TryEnqueue(() =>
                            lblSettingsCacheStatus.Text = $"Embedding {current}/{count}…");
                    });

                lblSettingsCacheStatus.Text = forceRebuild
                    ? $"Rebuilt — {total} items indexed."
                    : added > 0
                        ? $"Updated — added {added} new items, {total} total indexed."
                        : $"Cache is up to date — {total} items indexed.";

                IndexRebuilt?.Invoke();
            }
            catch (Exception ex)
            {
                lblSettingsCacheStatus.Text = $"Index build failed: {ex.Message}";
            }

            indexProgressRing.IsActive = false;
            btnSettingsBuildIndex.IsEnabled = true;
            btnSettingsForceRebuild.IsEnabled = true;
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