using ADO_Tools.Models;
using ADO_Tools.Services;

namespace ADO_Tools.UI
{
    public partial class ReadWorkItemsForm : Form
    {
        private readonly string personalAccessToken;
        private readonly string organization;

        List<QueryDto> queryList;
        TfsRestClient tfsRest;
        List<WorkItemDto> workItemList = new List<WorkItemDto>();
        private ListViewColumnSorter lvwColumnSorter;

        public ReadWorkItemsForm(string PAT, string ORG)
        {
            InitializeComponent();
            personalAccessToken = PAT;
            organization = ORG;

            // Load persisted values
            textBox_ProjectName.Text = Properties.Settings.Default.Project;
            textBox_rootFolder.Text = Properties.Settings.Default.RootFolder;


            label_downloading.Text = "";
            label_size.Text = "";
            label_numberofItems.Text = "";
            

            lvwColumnSorter = new ListViewColumnSorter();
            this.listView_WorkItems.ListViewItemSorter = lvwColumnSorter;

        }

        private async void connectProject_Click(object sender, EventArgs e)
        {
            label_downloading.Text = "";
            label_size.Text = "";
            label_numberofItems.Text = "";

            string projectName = textBox_ProjectName.Text?.Trim();

            if (string.IsNullOrEmpty(projectName))
            {
                MessageBox.Show("Please provide Project Name.", "Missing information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                tfsRest = new TfsRestClient(organization, projectName, personalAccessToken);
                queryList = await tfsRest.GetQueriesAsync();

                Combobox_Quaries.Items.Clear();
                foreach (var queryItem in queryList)
                {
                    Combobox_Quaries.Items.Add(queryItem.Path);
                }
                if (Combobox_Quaries.Items.Count > 0) Combobox_Quaries.SelectedIndex = 0;

                button_DownloadSingle.Enabled = true;
                button_readItems.Enabled = true;

                downloadItems.Enabled = true;

            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to connect or load queries: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                button_DownloadSingle.Enabled = false;
                downloadItems.Enabled = false;
            }
        }

        private async void button_readItems_Click(object sender, EventArgs e)
        {
            label_numberofItems.Text = "Reading...";
            this.Refresh();

            string queryPath = Combobox_Quaries.Text;
            string wiql = "";

            listView_WorkItems.Clear();
            listView_WorkItems.FullRowSelect = true;

            if (queryList == null)
            {
                label_numberofItems.Text = "";
                this.Refresh();
                return;
            }

            var q = queryList.FirstOrDefault(x => x.Path == queryPath);
            if (q == null)
            {
                label_numberofItems.Text = "";
                return;
            }

            wiql = q.Wiql ?? string.Empty;
            wiql = wiql.Replace("[System.TeamProject] = @project and ", "");

            try
            {
                workItemList = await tfsRest.QueryWorkItemsAsync(wiql);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to run query: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                workItemList = new List<WorkItemDto>();
            }

            if (workItemList == null || workItemList.Count == 0)
            {
                label_numberofItems.Text = "0 items read";
                return;
            }

            label_numberofItems.Text = workItemList.Count.ToString() + " items read";
            populateWorkItems();
            shadeElements();
        }

        private void populateWorkItems()
        {
            listView_WorkItems.View = View.Details;
            listView_WorkItems.Columns.Add("Item Number", 100, HorizontalAlignment.Left);
            listView_WorkItems.Columns.Add("Title", 500, HorizontalAlignment.Left);
            listView_WorkItems.Columns.Add("State", 100, HorizontalAlignment.Left);
            listView_WorkItems.Columns.Add("Created By", 100, HorizontalAlignment.Center);
            listView_WorkItems.Columns.Add("Created Date", 100, HorizontalAlignment.Center);
            listView_WorkItems.Columns.Add("Type", 100, HorizontalAlignment.Center);
            listView_WorkItems.Columns.Add("Path", 200, HorizontalAlignment.Center);

            int totalWidth = 0;
            for (int i = 0; i < listView_WorkItems.Columns.Count; i++)
            {
                listView_WorkItems.Columns[i].TextAlign = HorizontalAlignment.Left;
                totalWidth += listView_WorkItems.Columns[i].Width;
            }
            this.Width = totalWidth + 75;

            foreach (var workItem in workItemList)
            {
                ListViewItem item1 = new ListViewItem(workItem.Id.ToString(), 0);
                item1.SubItems.Add(workItem.Title);
                item1.SubItems.Add(workItem.State);
                item1.SubItems.Add(workItem.CreatedBy);
                item1.SubItems.Add(workItem.CreatedDate.ToShortDateString());
                item1.SubItems.Add(workItem.TypeName);

                string iterationPath = (workItem.IterationPath ?? string.Empty).Replace("Civil Design\\Civil Designer Products\\", "");
                item1.SubItems.Add(iterationPath);

                listView_WorkItems.Items.Add(item1);
            }
        }

        private string convertFlatList(string wiql)
        {
            int orderByLocation = wiql.IndexOf("order by");
            if (orderByLocation < 0) return wiql;
            string orderBy = wiql.Substring(orderByLocation);

            int hierarchyLocation = wiql.IndexOf("([System.Links.LinkType] = 'System.LinkTypes.Hierarchy-Forward')");
            if (hierarchyLocation > 4)
            {
                string trimmedWiql = wiql.Remove(hierarchyLocation - 4);
                wiql = trimmedWiql + orderBy;
            }

            wiql = wiql.Replace(" mode (Recursive)", "");
            wiql = wiql.Replace("WorkItemLinks", "WorkItems");
            wiql = wiql.Replace("Source.", "");
            wiql = wiql.Replace("[System.TeamProject] = 'Civil Design' and ", "");

            return wiql;

        }

        private async void downloadItems_Click(object sender, EventArgs e)
        {
            var selectedItems = this.listView_WorkItems.SelectedItems;

            foreach (ListViewItem item in selectedItems)
            {
                if (!int.TryParse(item.SubItems[0].Text, out int elementID)) continue;

                var workItem = workItemList.FirstOrDefault(w => w.Id == elementID);
                if (workItem == null) continue;

                string totalAttachmentSize = calculateAttachmentSize(workItem);
                label_downloading.Text = "Downloading #" + workItem.Id;
                label_size.Text = workItem.Attachments.Count.ToString() + " Attachment(s): " + totalAttachmentSize;
                this.Refresh();

                string path = createFolder(readTopFolder(), workItem.Id.ToString()) + "\\";

                string htmlPath = Path.Combine(path, removeIllegalChracters(workItem.Title) + ".html");


                string url;
                url = workItem.HtmlUrl;

                File.WriteAllText(htmlPath, $"<h1><a href=\"{System.Net.WebUtility.HtmlEncode(url)}\">{System.Net.WebUtility.HtmlEncode(workItem.Title)}</a></h1>");

                foreach (var att in workItem.Attachments)
                {
                    try
                    {
                        await tfsRest.DownloadAttachmentAsync(att, path);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Attachment download failed: {ex.Message}");
                    }
                }

                item.ForeColor = Color.Gray;
                this.Refresh();
            }

            label_downloading.Text = "Download Complete";
            label_size.Text = "";
            this.Refresh();
        }

        private void buttonFolderSelect_Click(object sender, EventArgs e)
        {
            DialogResult result = folderBrowserDialog1.ShowDialog();
            if (result == DialogResult.OK)
            {
                this.textBox_rootFolder.Text = folderBrowserDialog1.SelectedPath + "\\";
            }

        }

        private string readTopFolder()
        {
            string topFolder = this.textBox_rootFolder.Text;
            if (!(Directory.Exists(topFolder)))
            {
                topFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            }


            if (!(topFolder.EndsWith("\\")))
            {
                topFolder = topFolder + "\\";
            }

            return topFolder;
        }


        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.Save();
        }

        private void button_Compare_Click(object sender, EventArgs e)
        {
            var selectedItems = this.listView_WorkItems.SelectedItems;
            if (selectedItems.Count == 0) return;
            if (!int.TryParse(selectedItems[0].Text, out int elementID)) return;

            var workItem = workItemList.FirstOrDefault(w => w.Id == elementID);
            if (workItem == null) return;

            // open Form2 using REST client
            FindSimilarForm frm = new FindSimilarForm(tfsRest, queryList, workItem, readTopFolder());
            frm.Show();
        }

        private void textBox_Days_Leave(object sender, EventArgs e)
        {
            shadeElements();
        }

        private void shadeElements()
        {
            DateTime localDate = DateTime.Now;

            foreach (ListViewItem listItem in listView_WorkItems.Items)
            {
                if (!int.TryParse(listItem.Text, out int elementID)) continue;

                var workItem = workItemList.FirstOrDefault(w => w.Id == elementID);
                if (workItem == null) continue;

                DateTime createdDate = workItem.CreatedDate;
                DateTime checkDate = createdDate.AddDays(Convert.ToDouble(textBox_Days.Text));

                int result = DateTime.Compare(localDate, checkDate);

                if (result < 0) //In time period, Shade
                {
                    listItem.BackColor = Color.LightBlue;
                }
                else
                {
                    listItem.BackColor = Color.White;
                }
            }
        }

        private void listView_WorkItems_DoubleClick(object sender, EventArgs e)
        {
            var selectedItems = this.listView_WorkItems.SelectedItems;

            if (selectedItems.Count == 0) return;

            if (!int.TryParse(selectedItems[0].Text, out int elementID)) return;

            var workItem = workItemList.FirstOrDefault(w => w.Id == elementID);
            if (workItem == null) return;

            string url;
            url = workItem.HtmlUrl;

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }

        private void listView_WorkItems_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Determine if clicked column is already the column that is being sorted.
            if (e.Column == lvwColumnSorter.SortColumn)
            {
                // Reverse the current sort direction for this column.
                if (lvwColumnSorter.Order == SortOrder.Ascending)
                {
                    lvwColumnSorter.Order = SortOrder.Descending;
                }
                else
                {
                    lvwColumnSorter.Order = SortOrder.Ascending;
                }
            }
            else
            {
                // Set the column number that is to be sorted; default to ascending.
                lvwColumnSorter.SortColumn = e.Column;
                lvwColumnSorter.Order = SortOrder.Ascending;
            }

            // Perform the sort with these new sort options.
            this.listView_WorkItems.Sort();
        }

        private async void button_DownloadSingle_Click(object sender, EventArgs e)
        {
            if (!Int32.TryParse(textBox_SingleItem.Text, out int elementID))
            {
                return;
            }

            WorkItemDto workItem = await tfsRest.GetWorkItemAsync(elementID);

            string totalAttachmentSize = calculateAttachmentSize(workItem);
            label_downloading.Text = "Downloading #" + workItem.Id;
            label_size.Text = workItem.Attachments.Count.ToString() + " Attachment(s): " + totalAttachmentSize;
            this.Refresh();

            string path = createFolder(readTopFolder(), workItem.Id.ToString()) + "\\";

            string htmlPath = Path.Combine(path, removeIllegalChracters(workItem.Title) + ".html");


            string url;
            url = workItem.HtmlUrl;

            File.WriteAllText(htmlPath, $"<h1><a href=\"{System.Net.WebUtility.HtmlEncode(url)}\">{System.Net.WebUtility.HtmlEncode(workItem.Title)}</a></h1>");





            foreach (var att in workItem.Attachments)
            {
                try
                {
                    await tfsRest.DownloadAttachmentAsync(att, path);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Attachment download failed: {ex.Message}");
                }
            }

            label_downloading.Text = "Download Complete";
            label_size.Text = "";
            this.Refresh();
        }





        private string createFolder(string topFolder, string subFolder)
        {
            string pathString = System.IO.Path.Combine(topFolder, subFolder);
            System.IO.Directory.CreateDirectory(pathString);
            return pathString;
        }

        private string removeIllegalChracters(string illegal)
        {
            string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            var r = new System.Text.RegularExpressions.Regex(string.Format("[{0}]", System.Text.RegularExpressions.Regex.Escape(regexSearch)));
            illegal = r.Replace(illegal, "").Trim();

            int maxLength = 200;
            if (illegal.Length > maxLength)
            {
                illegal = illegal.Substring(0, maxLength);
            }

            return illegal;
        }

        private string calculateAttachmentSize(WorkItemDto workItem)
        {
            double fileSize = 0;
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };

            foreach (var atm in workItem.Attachments)
            {
                fileSize += atm.Length; // may be 0 if not populated by REST
            }

            int order = 0;
            while (fileSize >= 1024 && order < sizes.Length - 1)
            {
                order++;
                fileSize = fileSize / 1024;
            }

            string result = String.Format("{0:0.##} {1}", fileSize, sizes[order]);

            return result;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Save current values
            Properties.Settings.Default.Project = textBox_ProjectName.Text;
            Properties.Settings.Default.RootFolder = textBox_rootFolder.Text;

            Properties.Settings.Default.Save();
            base.OnFormClosing(e);
        }
    }
}
