using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Windows.Forms;
using ADO_Tools.Services;

namespace ADO_Tools.UI
{
    public partial class SoftwareDownloaderForm : Form
    {
        private Dictionary<string, string> buildIdMap = new Dictionary<string, string>();

        private readonly string personalAccessToken;
        private readonly string organization;
        private bool lastInstallerRunning = false;
        private int installerProgressTicks = 0;
        private int maxAsterisks = 10;


        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
        private InstallFunctions CreateInstallFunctionsWithLogging()
        {
            var installFunctions = new InstallFunctions();
            installFunctions.StatusUpdated += (msg) =>
            {
                void UpdateLog()
                {
                    var lines = txtLog.Lines;
                    // Find the last non-empty line index
                    int lastIndex = lines.Length - 1;
                    while (lastIndex >= 0 && string.IsNullOrWhiteSpace(lines[lastIndex]))
                        lastIndex--;

                    if (msg.StartsWith("Windows Installer is running."))
                    {
                        if (lastIndex >= 0 && lines[lastIndex].StartsWith("Windows Installer is running."))
                        {
                            lines[lastIndex] = msg;
                            txtLog.Lines = lines;
                            txtLog.SelectionStart = txtLog.Text.Length;
                            txtLog.ScrollToCaret();
                        }
                        else
                        {
                            txtLog.AppendText(msg + Environment.NewLine);
                        }
                    }
                    else
                    {
                        txtLog.AppendText(msg + Environment.NewLine);
                    }
                }

                if (txtLog.InvokeRequired)
                {
                    txtLog.Invoke(new Action(UpdateLog));
                }
                else
                {
                    UpdateLog();
                }
            };
            return installFunctions;
        }

        private TFSFunctions CreateTFSFunctionsWithLogging()
        {
            var tfsFunctions = new TFSFunctions();
            tfsFunctions.StatusUpdated += (msg) =>
            {
                if (txtLog.InvokeRequired)
                {
                    txtLog.Invoke(new Action(() => txtLog.AppendText(msg + Environment.NewLine)));
                }
                else
                {
                    txtLog.AppendText(msg + Environment.NewLine);
                }
            };
            return tfsFunctions;
        }
        public List<TFSFunctions.BuildInfo> Builds { get; private set; } = new();

        public SoftwareDownloaderForm(string PAT, string ORG)
        {
            InitializeComponent();

            //make build combobox characters fix width so it looks neat
            //cmbBuilds.Font = new System.Drawing.Font("Consolas", cmbBuilds.Font.Size);
            personalAccessToken = PAT;
            organization = ORG;

            // Load persisted values
            cmbProductName.Text = Properties.Settings.Default.ProductName;
            txtDefinitionId.Text = Properties.Settings.Default.DefinitionId;
            txtProject.Text = Properties.Settings.Default.Project;
            txtDownloadFolder.Text = Properties.Settings.Default.DownloadFolder;        

            // Define defaults
            var defaults = new[] {
                "OpenRail Designer|6098|civil",
                "OpenRoads Designer|6057|civil",
                "Overhead Line Designer|6289|civil"
            };

            // Ensure UserDefinitionList exists
            if (Properties.Settings.Default.UserDefinitionList == null)
                Properties.Settings.Default.UserDefinitionList = new StringCollection();

            // Add any missing defaults to UserDefinitionList
            foreach (var def in defaults)
            {
                // Only add if not already present (by full string match)
                if (!Properties.Settings.Default.UserDefinitionList.Cast<string>().Any(existing => existing == def))
                    Properties.Settings.Default.UserDefinitionList.Add(def);
            }
            Properties.Settings.Default.Save();

            // Always load UI from UserDefinitionList
            cmbProductName.Items.Clear();
            foreach (var def in Properties.Settings.Default.UserDefinitionList)
            {
                var parts = def.Split('|');
                if (parts.Length == 3 && !cmbProductName.Items.Contains(parts[0]))
                    cmbProductName.Items.Add(parts[0]);
            }

            // When product changes, set definition ID and project
            cmbProductName.SelectedIndexChanged += (s, e) =>
            {
                var selected = cmbProductName.Text;
                var def = Properties.Settings.Default.UserDefinitionList
                    .Cast<string>()
                    .FirstOrDefault(d => d.StartsWith(selected + "|"));
                if (def != null)
                {
                    var parts = def.Split('|');
                    txtDefinitionId.Text = parts[1];
                    txtProject.Text = parts[2];
                }
                Properties.Settings.Default.ProductName = cmbProductName.Text;
                Properties.Settings.Default.DefinitionId = txtDefinitionId.Text;
                Properties.Settings.Default.Project = txtProject.Text;
                Properties.Settings.Default.Save();
            };

            // Handle Definition ID changes
            txtDefinitionId.TextChanged += (s, e) =>
            {
                UpdateCurrentDefinitionInUserList();
                Properties.Settings.Default.DefinitionId = txtDefinitionId.Text;
                Properties.Settings.Default.Save();
            };

            // Handle Project changes
            txtProject.TextChanged += (s, e) =>
            {
                UpdateCurrentDefinitionInUserList();
                Properties.Settings.Default.Project = txtProject.Text;
                Properties.Settings.Default.Save();
            };

            // Add new definition
            btnAddDefinition.Click += (s, e) =>
            {
                using (var dialog = new AddDefinitionDialog())
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        var def = $"{dialog.DefinitionName}|{dialog.DefinitionId}|{dialog.Project}";
                        if (!cmbProductName.Items.Contains(dialog.DefinitionName))
                        {
                            cmbProductName.Items.Add(dialog.DefinitionName);
                            if (Properties.Settings.Default.UserDefinitionList == null)
                                Properties.Settings.Default.UserDefinitionList = new StringCollection();
                            Properties.Settings.Default.UserDefinitionList.Add(def);
                            Properties.Settings.Default.Save();
                        }
                    }
                }
            };

            // Remove definition
            ////btnRemoveDefinition.Click += (s, e) =>
            ////{
            ////    var productName = cmbProductName.Text;
            ////    if (string.IsNullOrWhiteSpace(productName))
            ////        return;

            ////    var list = Properties.Settings.Default.UserDefinitionList;
            ////    if (list == null)
            ////        return;

            ////    // Find the index of the current product
            ////    int idx = -1;
            ////    for (int i = 0; i < list.Count; i++)
            ////    {
            ////        var parts = list[i].Split('|');
            ////        if (parts.Length == 3 && parts[0] == productName)
            ////        {
            ////            idx = i;
            ////            break;
            ////        }
            ////    }

            ////    if (idx >= 0)
            ////    {
            ////        list.RemoveAt(idx);
            ////        Properties.Settings.Default.UserDefinitionList = list;
            ////        Properties.Settings.Default.Save();

            ////        cmbProductName.Items.Remove(productName);

            ////        // Optionally clear the fields if nothing is selected
            ////        if (cmbProductName.Items.Count > 0)
            ////        {
            ////            cmbProductName.SelectedIndex = 0;
            ////        }
            ////        else
            ////        {
            ////            cmbProductName.Text = "";
            ////            txtDefinitionId.Text = "";
            ////            txtProject.Text = "";
            ////        }
            ////    }
            ////};
        }

        private void btnLoadBuilds_Click(object sender, EventArgs e)
        {
            LoadBuilds();
        }
        private async void LoadBuilds()
        {
            var installFunctions = CreateInstallFunctionsWithLogging();
            installFunctions.UpdateStatus("Loading builds...");

            var tfsFunctions = CreateTFSFunctionsWithLogging();
            int numberofBuilds = (int)numBuildCount.Value; // <-- Use the persisted/user value

            string project = txtProject.Text;

            if (string.IsNullOrWhiteSpace(organization))
            {
                MessageBox.Show("Organization cannot be empty.");
                installFunctions.UpdateStatus("Organization cannot be empty.");
                return;
            }

            if (string.IsNullOrWhiteSpace(project))
            {
                MessageBox.Show("Project cannot be empty.");
                installFunctions.UpdateStatus("Project cannot be empty.");
                return;
            }

            if (!int.TryParse(txtDefinitionId.Text, out int definitionId))
            {
                MessageBox.Show("Invalid Definition ID.");
                installFunctions.UpdateStatus("Invalid Definition ID.");
                return;
            }


            installFunctions.UpdateStatus("Fetching available builds...");


            // Fetch builds from TFS/Azure DevOps
            Builds = await tfsFunctions.GetAvailableBuildsAsync(organization, project, definitionId, personalAccessToken, numberofBuilds);

            cmbBuilds.Items.Clear();
            buildIdMap.Clear();

            foreach (var build in Builds)
            {
                string display = $"{build.ProductName,-25}|{build.DisplayVersion,-13}|{build.Result,12}|{build.FinishTime,20}";
                cmbBuilds.Items.Add(display);
                buildIdMap[display] = build.BuildId;
            }

            if (cmbBuilds.Items.Count > 0)
            {
                cmbBuilds.SelectedIndex = 0;
                installFunctions.UpdateStatus("Builds loaded successfully.");
            }
            else
            {
                installFunctions.UpdateStatus("No builds found.");
            }
        }


        // When definition ID or project changes, update the corresponding entry in UserDefinitionList
        private void UpdateCurrentDefinitionInUserList()
        {
            var productName = cmbProductName.Text;
            if (string.IsNullOrWhiteSpace(productName))
                return;

            var list = Properties.Settings.Default.UserDefinitionList;
            if (list == null)
                return;

            // Find the index of the current product
            int idx = -1;
            for (int i = 0; i < list.Count; i++)
            {
                var parts = list[i].Split('|');
                if (parts.Length == 3 && parts[0] == productName)
                {
                    idx = i;
                    break;
                }
            }

            if (idx >= 0)
            {
                // Update the entry with the new values
                list[idx] = $"{productName}|{txtDefinitionId.Text}|{txtProject.Text}";
                Properties.Settings.Default.UserDefinitionList = list;
                Properties.Settings.Default.Save();
            }
        }

        private async void btnUpdate_Click(object sender, EventArgs e)
        {

            //if (InstallFunctions.IsInstallerRunning())
            //{
            //    MessageBox.Show("A Windows Installer process is currently running. Please wait for it to finish before starting another operation.");
            //    return;
            //}

            var installFunctionsWithLogging = CreateInstallFunctionsWithLogging();
            var tfsFunctions = CreateTFSFunctionsWithLogging(); // Remove progress bar variant

            if (cmbBuilds.SelectedItem == null || string.IsNullOrWhiteSpace(txtDownloadFolder.Text))
            {
                MessageBox.Show("Please select a build and download folder.");
                return;
            }

            // Get installed Bentley software (for potential use in update logic)
            var bentleySoftware = installFunctionsWithLogging.GetInstalledBentleySoftware().ToList();



            // Get the corresponding build ID for the selected combobox item
            string selectedComboboxBuild = cmbBuilds.SelectedItem.ToString();
            string buildId = buildIdMap[selectedComboboxBuild];


            // Find the selected build info
            var selectedBuildInfo = Builds.FirstOrDefault(b => b.BuildId == buildId);

            if (selectedBuildInfo.Result != "succeeded")
            {
                MessageBox.Show(selectedBuildInfo.DisplayVersion + " is not a succesful build. Please select another version");
                return;
            }




            //Create download and extract folders
            string downloadFolder = txtDownloadFolder.Text;
            downloadFolder = Path.Combine(downloadFolder, selectedBuildInfo.ProductName, selectedBuildInfo.DisplayVersion);
            string extractFolder = Path.Combine(downloadFolder, "Extracted");
            Directory.CreateDirectory(extractFolder);




            // Get artifact metadata
            string project = txtProject.Text;
            //string baseUrl = $"https://dev.azure.com/{organization}/{project}/_apis";
            //string artifactsUrl = $"{baseUrl}/build/builds/{buildId}/artifacts?api-version=7.1";



            // Download and extract artifacts
            await tfsFunctions.DownloadLatestBuildArtifacts(organization, project, buildId, downloadFolder, extractFolder, personalAccessToken, installFunctionsWithLogging);


            string productName = selectedBuildInfo.ProductName;
            string setupFile = Directory.GetFiles(extractFolder, "*.exe", SearchOption.AllDirectories)
                .FirstOrDefault(f => Path.GetFileName(f).ToLower().Contains("setup") &&
                                     Path.GetFileName(f).ToLower().Contains(productName.ToLower()));


            if (setupFile == null)
            {
                MessageBox.Show("Setup file not found. Update process aborted!");
                return;
            }


            //string setupFileName = Path.GetFileName(setupFile);

            // Update software
            //await installFunctionsWithLogging.UpdateSoftwareAsync(selectedBuildInfo, matchingInstalledBuild, setupFile, extractFolder, setupFileName, productName, checkBoxCleanUninstall.Checked);



            // Check if the selected build matches any installed Bentley software
            installFunctionsWithLogging.UpdateStatus($"Searching for installed version {selectedBuildInfo.MajorVersion}.{selectedBuildInfo.MajorVersionSequence}.* ...");

            var matchingInstalledBuild = bentleySoftware.FirstOrDefault(installed =>
                installed.DisplayName.Replace(" ", "").StartsWith(selectedBuildInfo.ProductName, StringComparison.OrdinalIgnoreCase) &&
                installed.MajorVersion == selectedBuildInfo.MajorVersion &&
                installed.MajorVersionSequence == selectedBuildInfo.MajorVersionSequence
            //&&                installed.MinorVersion == selectedBuildInfo.MinorVersion
            );


            // Uninstall operation (if matching build found)
            if (matchingInstalledBuild != null)
            {
                installFunctionsWithLogging.UpdateStatus($"Installed version found {selectedBuildInfo.DisplayVersion}");

                bool uninstallOk = await installFunctionsWithLogging.UninstallSoftwareAsync(matchingInstalledBuild, checkBoxCleanUninstall.Checked);
                if (!uninstallOk)
                {
                    // If uninstall fails when there is a version installed already, we should not proceed with installation to avoid conflicts
                    MessageBox.Show("Uninstallation failed. Update process aborted!");
                    return;
                }
            }
            else
            {
                installFunctionsWithLogging.UpdateStatus("No matching installed version found. Proceeding with installation.");
            }


            // Install operation (after uninstall, or standalone)
            //string setupPath = Path.Combine(extractFolder, setupFileName);
            if (File.Exists(setupFile))
            {
                await installFunctionsWithLogging.InstallSoftwareAsync(setupFile);
            }
            else
            {
                installFunctionsWithLogging.UpdateStatus("Setup file not found after extraction.");
            }


        }





        private void btnBrowseDownloadFolder_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtDownloadFolder.Text = dialog.SelectedPath;
                }
            }
        }





        private void btnShowBentleySoftware_Click(object sender, EventArgs e)
        {
            var installFunctions = CreateInstallFunctionsWithLogging();
            //var bentleySoftware = installFunctions.GetInstalledBentleySoftware()
            //      .Select(s => (s.DisplayName, s.DisplayVersion)).ToList();

            var bentleySoftware = installFunctions.GetInstalledBentleySoftware().ToList();
            var dialog = new InstalledSoftwareDialog(bentleySoftware);
            dialog.ShowDialog(this);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Save current values
            Properties.Settings.Default.ProductName = cmbProductName.Text;
            Properties.Settings.Default.DefinitionId = txtDefinitionId.Text;
            Properties.Settings.Default.Project = txtProject.Text;
            Properties.Settings.Default.DownloadFolder = txtDownloadFolder.Text;
            Properties.Settings.Default.Save();
            base.OnFormClosing(e);
        }

        private void txtDefinitionId_TextChanged(object sender, EventArgs e)
        {

        }

        private void btnRemoveDefinition_Click(object sender, EventArgs e)
        {
            var productName = cmbProductName.Text;
            if (string.IsNullOrWhiteSpace(productName))
                return;

            var list = Properties.Settings.Default.UserDefinitionList;
            if (list == null)
                return;

            // Find the index of the current product
            int idx = -1;
            for (int i = 0; i < list.Count; i++)
            {
                var parts = list[i].Split('|');
                if (parts.Length == 3 && parts[0] == productName)
                {
                    idx = i;
                    break;
                }
            }

            if (idx >= 0)
            {
                list.RemoveAt(idx);
                Properties.Settings.Default.UserDefinitionList = list;
                Properties.Settings.Default.Save();

                cmbProductName.Items.Remove(productName);

                // Optionally clear the fields if nothing is selected
                if (cmbProductName.Items.Count > 0)
                {
                    cmbProductName.SelectedIndex = 0;
                }
                else
                {
                    cmbProductName.Text = "";
                    txtDefinitionId.Text = "";
                    txtProject.Text = "";
                }
            }
        }

        ////private void InstallerCheckTimer_Tick(object sender, EventArgs e)
        ////{
        ////    bool installerRunning = InstallFunctions.IsInstallerRunning();
        ////    btnUpdate.Enabled = !installerRunning;

        ////    if (installerRunning)
        ////    {
        ////        installerProgressTicks++;
        ////        if (installerProgressTicks > maxAsterisks) installerProgressTicks = 1;

        ////        string progressLine = "Windows Installer is running. Please wait..." + new string('*', installerProgressTicks);

        ////        var lines = txtLog.Lines.ToList();
        ////        if (lines.Count > 0 && lines.Last().StartsWith("Windows Installer is running"))
        ////            lines[lines.Count - 1] = progressLine;
        ////        else
        ////            lines.Add(progressLine);
        ////        txtLog.Lines = lines.ToArray();
        ////    }
        ////    else if (lastInstallerRunning)
        ////    {
        ////        // Only stop the timer and reset when installer just finished
        ////        installerCheckTimer.Stop();
        ////        installerProgressTicks = 0;

        ////        // Remove the installer status line if present
        ////        var lines = txtLog.Lines.ToList();
        ////        if (lines.Count > 0 && lines.Last().StartsWith("Windows Installer is running"))
        ////        {
        ////            lines.RemoveAt(lines.Count - 1);
        ////            txtLog.Lines = lines.ToArray();
        ////        }
        ////    }

        ////    lastInstallerRunning = installerRunning;
        ////}


    }
}
