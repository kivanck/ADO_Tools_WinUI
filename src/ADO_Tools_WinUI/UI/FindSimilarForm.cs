using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ADO_Tools.Models;
using ADO_Tools.Services;
using ADO_Tools.Utilities;

namespace ADO_Tools.UI
{
    public partial class FindSimilarForm : Form
    {
        List<QueryDto> queryListOriginal;
        WorkItemDto workItemOriginal;
        TfsRestClient tfsRestClient;
        List<WorkItemDto> workItemList = new List<WorkItemDto>();
        string topFolderOriginal;
        private ListViewColumnSorter lvwColumnSorter;

        public FindSimilarForm(TfsRestClient restClient, List<QueryDto> queryList, WorkItemDto workItem, string topFolder)
        {
            InitializeComponent();
            label_originalItem.Text = workItem.Title;
            label_downloading.Text = "";
            label_size.Text = "";
            label_numberofItems.Text = "";

            lvwColumnSorter = new ListViewColumnSorter();
            this.listView_CompareItems.ListViewItemSorter = lvwColumnSorter;

            tfsRestClient = restClient;
            workItemOriginal = workItem;
            queryListOriginal = queryList;
            topFolderOriginal = topFolder;
            //Fill query combobox
            foreach (var queryItem in queryListOriginal)
            {
                comboBox_CompareQuarry.Items.Add(queryItem.Path);
            }
            if (comboBox_CompareQuarry.Items.Count > 0) comboBox_CompareQuarry.SelectedIndex = 0;
        }

        private async void button_FindItems_Click(object sender, EventArgs e)
        {
            label_numberofItems.Text = "Comparing...";
            this.Refresh();

            wordCompare wordCompare = new wordCompare();
            await readQuarryItems();

            listView_CompareItems.Clear();
            listView_CompareItems.FullRowSelect = true;

            listView_CompareItems.View = View.Details;
            listView_CompareItems.Columns.Add("Item Number", 100, HorizontalAlignment.Left);
            listView_CompareItems.Columns.Add("Match Score", 100, HorizontalAlignment.Left);
            listView_CompareItems.Columns.Add("Title", 500, HorizontalAlignment.Left);
            listView_CompareItems.Columns.Add("State", 100, HorizontalAlignment.Left);
            listView_CompareItems.Columns.Add("CreatedBy", 100, HorizontalAlignment.Center);
            listView_CompareItems.Columns.Add("Type", 100, HorizontalAlignment.Center);
            listView_CompareItems.Columns.Add("Path", 200, HorizontalAlignment.Center);

            int totalWidth = 0;
            for (int i = 0; i < listView_CompareItems.Columns.Count; i++)
            {
                listView_CompareItems.Columns[i].TextAlign = HorizontalAlignment.Left;
                totalWidth += listView_CompareItems.Columns[i].Width;
            }
            this.Width = totalWidth + 75;

            string themeOriginal = workItemOriginal.Fields.ContainsKey("Theme.Description") ? Convert.ToString(workItemOriginal.Fields["Theme.Description"]) : string.Empty;
            string str1 = workItemOriginal.Title;

            List<WorkItemMatched> workItemMatchedList = new List<WorkItemMatched>();

            if (workItemList == null)
            {
                label_numberofItems.Text = "";
                return;
            }

            foreach (var workItem in workItemList)
            {
                string str2 = workItem.Title;
                string themeworkItem = workItem.Fields.ContainsKey("Theme.Description") ? Convert.ToString(workItem.Fields["Theme.Description"]) : string.Empty;
                if (((themeworkItem == "") || (themeworkItem.Equals(themeOriginal))) && !(str1.Equals(str2)))
                {
                    int maxMatch = wordCompare.compareStrings(str1, str2);

                    if (maxMatch > 0)
                    {
                        WorkItemMatched workItemMatched = new WorkItemMatched();
                        workItemMatched.WorkItem = workItem;
                        workItemMatched.MatchScore = maxMatch;

                        workItemMatchedList.Add(workItemMatched);
                    }
                }
            }

            workItemMatchedList = workItemMatchedList.OrderByDescending(WorkItemMatched => WorkItemMatched.MatchScore).ToList();

            int topMatchNumber = Int32.Parse(textBox_topMatchNumber.Text);
            int maxItems = workItemMatchedList.Count < topMatchNumber ? workItemMatchedList.Count : topMatchNumber;

            for (int i = 0; i < maxItems; i++)
            {
                var workItemForList = workItemMatchedList[i].WorkItem;

                ListViewItem item1 = new ListViewItem(workItemForList.Id.ToString(), 0);

                item1.SubItems.Add(workItemMatchedList[i].MatchScore.ToString());
                item1.SubItems.Add(workItemForList.Title);
                item1.SubItems.Add(workItemForList.State);
                item1.SubItems.Add(workItemForList.CreatedBy);
                item1.SubItems.Add(workItemForList.TypeName);

                string iterationPath = (workItemForList.IterationPath ?? string.Empty).Replace("Civil Design\\Civil Designer Products\\", "");
                item1.SubItems.Add(iterationPath);

                listView_CompareItems.Items.Add(item1);
            }

            if (maxItems == 0)
            {
                label_numberofItems.Text = "No matches found";
            }
            else
            {
                label_numberofItems.Text = "Top " + maxItems + " matches are displayed";
            }
        }

        private async Task readQuarryItems()
        {
            string queryPath = comboBox_CompareQuarry.Text;
            string wiql = "";

            if (queryListOriginal == null)
            {
                label_numberofItems.Text = "";
                this.Refresh();
                return;
            }
            var q = queryListOriginal.FirstOrDefault(x => x.Path == queryPath);
            if (q == null)
            {
                label_numberofItems.Text = "";
                return;
            }
            wiql = q.Wiql ?? string.Empty;
            wiql = wiql.Replace("[System.TeamProject] = @project and ", "");

            try
            {
                // query work items using REST client
                var result = await tfsRestClient.QueryWorkItemsAsync(wiql);
                workItemList = result.WorkItems;
                label_numberofItems.Text = workItemList.Count + " items read";
            }
            catch (Exception ex)
            {
                label_numberofItems.Text = "0 items read";
                MessageBox.Show("Failed to read query items: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void button_form2downloaditems_Click(object sender, EventArgs e)
        {
            var selectedItems = this.listView_CompareItems.SelectedItems;

            foreach (ListViewItem item in selectedItems)
            {
                if (!int.TryParse(item.SubItems[0].Text, out int elementID)) continue;

                var wi = workItemList.FirstOrDefault(w => w.Id == elementID);
                if (wi == null) continue;

                string path = System.IO.Path.Combine(topFolderOriginal, elementID.ToString());
                System.IO.Directory.CreateDirectory(path);

                foreach (var att in wi.Attachments)
                {
                    try
                    {
                        await tfsRestClient.DownloadAttachmentAsync(att, path);
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

        private void textBox_Days_TextChanged(object sender, EventArgs e)
        {

        }

        private void listView_CompareItems_DoubleClick(object sender, EventArgs e)
        {
            var selectedItems = this.listView_CompareItems.SelectedItems;

            if (selectedItems.Count == 0) return;

            int elementID = Int32.Parse(selectedItems[0].Text);

            string url = "https://dev.azure.com/bentleycs/" + "Civil" + "/_workitems/edit/" + elementID;

            System.Diagnostics.Process.Start(url);
        }

        private void listView_CompareItems_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column == lvwColumnSorter.SortColumn)
            {
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
                lvwColumnSorter.SortColumn = e.Column;
                lvwColumnSorter.Order = SortOrder.Ascending;
            }

            this.listView_CompareItems.Sort();
        }
    }
}
