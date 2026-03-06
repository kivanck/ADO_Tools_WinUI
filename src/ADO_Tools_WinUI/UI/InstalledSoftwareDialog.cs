using ADO_Tools.Services;

namespace ADO_Tools.UI
{
    public partial class InstalledSoftwareDialog : Form
    {
        // Store the list as a private field
        private readonly List<InstallFunctions.InstalledSoftwareInfo> _bentleySoftware;

        private InstallFunctions CreateInstallFunctionsWithLogging()
        {
            var installFunctions = new InstallFunctions();
            installFunctions.StatusUpdated += (msg) =>
            {
                void UpdateLog()
                {
                    var lines = textLogInstalled.Lines;
                    // Find the last non-empty line index
                    int lastIndex = lines.Length - 1;
                    while (lastIndex >= 0 && string.IsNullOrWhiteSpace(lines[lastIndex]))
                        lastIndex--;

                    if (msg.StartsWith("Windows Installer is running."))
                    {
                        if (lastIndex >= 0 && lines[lastIndex].StartsWith("Windows Installer is running."))
                        {
                            lines[lastIndex] = msg;
                            textLogInstalled.Lines = lines;
                            textLogInstalled.SelectionStart = textLogInstalled.Text.Length;
                            textLogInstalled.ScrollToCaret();
                        }
                        else
                        {
                            textLogInstalled.AppendText(msg + Environment.NewLine);
                        }
                    }
                    else
                    {
                        textLogInstalled.AppendText(msg + Environment.NewLine);
                    }
                }

                if (textLogInstalled.InvokeRequired)
                {
                    textLogInstalled.Invoke(new Action(UpdateLog));
                }
                else
                {
                    UpdateLog();
                }
            };
            return installFunctions;
        }

        public InstalledSoftwareDialog(List<InstallFunctions.InstalledSoftwareInfo> bentleySoftware)
        {
            InitializeComponent();
            _bentleySoftware = bentleySoftware; // Save for later use

            listViewSoftware.View = View.Details;
            listViewSoftware.Columns.Add("Name", 300);
            listViewSoftware.Columns.Add("Version", 100);

            foreach (var sw in bentleySoftware)
            {
                var item = new ListViewItem(sw.DisplayName);
                item.SubItems.Add(sw.DisplayVersion);
                listViewSoftware.Items.Add(item);
            }
        }

        private async void btnUninstall_ClickAsync(object sender, EventArgs e)
        {
            if (listViewSoftware.SelectedItems.Count == 0)
                return;

            string displayName = listViewSoftware.SelectedItems[0].Text;
            string version = listViewSoftware.SelectedItems[0].SubItems[1].Text;
            bool cleanUninstall = checkBoxCleanUninstall.Checked;

            // Find the selected software in the list
            var selectedSoftware = _bentleySoftware
                .FirstOrDefault(sw => sw.DisplayName == displayName && sw.DisplayVersion == version);

            if (selectedSoftware != null)
            {
                var installFunctionsWithLogging = CreateInstallFunctionsWithLogging();
                // Use selectedSoftware.QuietUninstallString or other properties as needed
                // Example:
                bool uninstallOk = await installFunctionsWithLogging.UninstallSoftwareAsync(selectedSoftware, cleanUninstall);
            }
        }
    }
}
