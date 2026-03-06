using System;
using System.IO;
using System.Security.Policy;
using System.Windows.Forms;

namespace ADO_Tools.UI
{
    public partial class MainSelectionForm : Form
    {
        public MainSelectionForm()
        {
            InitializeComponent();
            // Load persisted PAT
            txtPAT.Text = Properties.Settings.Default.PersonalAccessToken;
            txtOrganization.Text = Properties.Settings.Default.Organization;


            txtPAT.Leave += async (s, e) => await ValidatePATOnLeaveAsync();
            

        }

        private async void btnReadWorkItems_Click(object sender, EventArgs e)
        {
            if (await ValidatePATOnLeaveAsync())
            {
                var form = new ReadWorkItemsForm(txtPAT.Text.Trim(), txtOrganization.Text.Trim());
                form.Show();
            }
                
        }

        private async void btnSoftwareDownload_Click(object sender, EventArgs e)
        {
            if (await ValidatePATOnLeaveAsync())
            {
                var form = new SoftwareDownloaderForm(txtPAT.Text.Trim(), txtOrganization.Text.Trim());
                form.Show();
            }
        }

        private void MainSelectionForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveParameters();
        }

        private void SaveParameters()
        {
            Properties.Settings.Default.PersonalAccessToken = txtPAT.Text.Trim();
            Properties.Settings.Default.Organization = txtOrganization.Text.Trim();
            Properties.Settings.Default.Save();
        }

        private async Task<bool> ValidatePATAsync(string pat, string organization)
        {
            

            try
            {
                var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = false // Prevent following redirects to sign-in page
                };
                using (var client = new HttpClient(handler))
                {
                    var authToken = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{pat}"));
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);
                    var response = await client.GetAsync($"https://dev.azure.com/{organization}/_apis/projects?api-version=7.1");

                    // Only 200 means success; 203 or 302 means not authenticated
                    return response.StatusCode == System.Net.HttpStatusCode.OK;
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> ValidatePATOnLeaveAsync()
        {
            string pat = txtPAT.Text.Trim();
            string organization = txtOrganization.Text.Trim();


            if (string.IsNullOrEmpty(pat))
            {
                MessageBox.Show("Please provide a Personal Access Token (PAT).", "Missing information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            bool isValid = await ValidatePATAsync(pat, organization);
            if (!isValid)
            {
                MessageBox.Show("Invalid Personal Access Token.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return isValid;
        }
    }
}