namespace ADO_Tools.UI
{
    partial class ReadWorkItemsForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ReadWorkItemsForm));
            connectProject = new Button();
            button_readItems = new Button();
            Combobox_Quaries = new ComboBox();
            label1 = new Label();
            listView_WorkItems = new ListView();
            downloadItems = new Button();
            label_downloading = new Label();
            label_size = new Label();
            folderBrowserDialog1 = new FolderBrowserDialog();
            buttonFolderSelect = new Button();
            label2 = new Label();
            button_Compare = new Button();
            textBox_Days = new TextBox();
            label3 = new Label();
            label4 = new Label();
            label_numberofItems = new Label();
            label6 = new Label();
            textBox_rootFolder = new TextBox();
            textBox_ProjectName = new TextBox();
            textBox_SingleItem = new TextBox();
            button_DownloadSingle = new Button();
            SuspendLayout();
            // 
            // connectProject
            // 
            connectProject.Location = new Point(378, 32);
            connectProject.Name = "connectProject";
            connectProject.Size = new Size(116, 23);
            connectProject.TabIndex = 0;
            connectProject.Text = "Connect Project";
            connectProject.UseVisualStyleBackColor = true;
            connectProject.Click += connectProject_Click;
            // 
            // button_readItems
            // 
            button_readItems.Enabled = false;
            button_readItems.Location = new Point(500, 119);
            button_readItems.Name = "button_readItems";
            button_readItems.Size = new Size(75, 23);
            button_readItems.TabIndex = 1;
            button_readItems.Text = "Read Items";
            button_readItems.UseVisualStyleBackColor = true;
            button_readItems.Click += button_readItems_Click;
            // 
            // Combobox_Quaries
            // 
            Combobox_Quaries.FormattingEnabled = true;
            Combobox_Quaries.Location = new Point(15, 119);
            Combobox_Quaries.Name = "Combobox_Quaries";
            Combobox_Quaries.Size = new Size(479, 23);
            Combobox_Quaries.TabIndex = 2;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(15, 100);
            label1.Name = "label1";
            label1.Size = new Size(71, 15);
            label1.TabIndex = 4;
            label1.Text = "My Quarries";
            // 
            // listView_WorkItems
            // 
            listView_WorkItems.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            listView_WorkItems.Location = new Point(15, 153);
            listView_WorkItems.Name = "listView_WorkItems";
            listView_WorkItems.Size = new Size(1194, 319);
            listView_WorkItems.TabIndex = 5;
            listView_WorkItems.UseCompatibleStateImageBehavior = false;
            listView_WorkItems.ColumnClick += listView_WorkItems_ColumnClick;
            listView_WorkItems.DoubleClick += listView_WorkItems_DoubleClick;
            // 
            // downloadItems
            // 
            downloadItems.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            downloadItems.Enabled = false;
            downloadItems.Location = new Point(1106, 120);
            downloadItems.Name = "downloadItems";
            downloadItems.Size = new Size(103, 22);
            downloadItems.TabIndex = 6;
            downloadItems.Text = "Download Items";
            downloadItems.UseVisualStyleBackColor = true;
            downloadItems.Click += downloadItems_Click;
            // 
            // label_downloading
            // 
            label_downloading.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            label_downloading.AutoSize = true;
            label_downloading.Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label_downloading.ForeColor = SystemColors.Highlight;
            label_downloading.Location = new Point(983, 24);
            label_downloading.Name = "label_downloading";
            label_downloading.Size = new Size(173, 18);
            label_downloading.TabIndex = 7;
            label_downloading.Text = "Downloading #102654";
            // 
            // label_size
            // 
            label_size.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            label_size.AutoSize = true;
            label_size.Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label_size.ForeColor = SystemColors.Highlight;
            label_size.Location = new Point(983, 42);
            label_size.Name = "label_size";
            label_size.Size = new Size(194, 18);
            label_size.TabIndex = 8;
            label_size.Text = "3 Attachments: 32.56 mb";
            // 
            // folderBrowserDialog1
            // 
            folderBrowserDialog1.Description = "Select download directory.";
            // 
            // buttonFolderSelect
            // 
            buttonFolderSelect.Location = new Point(500, 74);
            buttonFolderSelect.Name = "buttonFolderSelect";
            buttonFolderSelect.Size = new Size(26, 23);
            buttonFolderSelect.TabIndex = 9;
            buttonFolderSelect.Text = "...";
            buttonFolderSelect.UseVisualStyleBackColor = true;
            buttonFolderSelect.Click += buttonFolderSelect_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(15, 58);
            label2.Name = "label2";
            label2.Size = new Size(125, 15);
            label2.TabIndex = 11;
            label2.Text = "Download Root Folder";
            // 
            // button_Compare
            // 
            button_Compare.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button_Compare.Location = new Point(1011, 121);
            button_Compare.Name = "button_Compare";
            button_Compare.Size = new Size(75, 23);
            button_Compare.TabIndex = 12;
            button_Compare.Text = "Compare";
            button_Compare.UseVisualStyleBackColor = true;
            button_Compare.Click += button_Compare_Click;
            // 
            // textBox_Days
            // 
            textBox_Days.Location = new Point(693, 124);
            textBox_Days.Name = "textBox_Days";
            textBox_Days.Size = new Size(30, 23);
            textBox_Days.TabIndex = 13;
            textBox_Days.Text = "5";
            textBox_Days.TextAlign = HorizontalAlignment.Center;
            textBox_Days.Leave += textBox_Days_Leave;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label3.Location = new Point(627, 126);
            label3.Name = "label3";
            label3.Size = new Size(59, 16);
            label3.TabIndex = 14;
            label3.Text = "Highlight";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label4.Location = new Point(729, 126);
            label4.Name = "label4";
            label4.Size = new Size(37, 16);
            label4.TabIndex = 15;
            label4.Text = "days";
            // 
            // label_numberofItems
            // 
            label_numberofItems.AutoSize = true;
            label_numberofItems.Location = new Point(412, 103);
            label_numberofItems.Name = "label_numberofItems";
            label_numberofItems.Size = new Size(71, 15);
            label_numberofItems.TabIndex = 16;
            label_numberofItems.Text = "0 items read";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(15, 16);
            label6.Name = "label6";
            label6.Size = new Size(79, 15);
            label6.TabIndex = 19;
            label6.Text = "Project Name";
            // 
            // textBox_rootFolder
            // 
            textBox_rootFolder.Location = new Point(15, 74);
            textBox_rootFolder.Name = "textBox_rootFolder";
            textBox_rootFolder.Size = new Size(479, 23);
            textBox_rootFolder.TabIndex = 10;
            // 
            // textBox_ProjectName
            // 
            textBox_ProjectName.Location = new Point(15, 32);
            textBox_ProjectName.Name = "textBox_ProjectName";
            textBox_ProjectName.Size = new Size(125, 23);
            textBox_ProjectName.TabIndex = 3;
            // 
            // textBox_SingleItem
            // 
            textBox_SingleItem.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            textBox_SingleItem.Location = new Point(986, 93);
            textBox_SingleItem.Name = "textBox_SingleItem";
            textBox_SingleItem.Size = new Size(100, 23);
            textBox_SingleItem.TabIndex = 21;
            // 
            // button_DownloadSingle
            // 
            button_DownloadSingle.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button_DownloadSingle.Enabled = false;
            button_DownloadSingle.Location = new Point(1106, 91);
            button_DownloadSingle.Name = "button_DownloadSingle";
            button_DownloadSingle.Size = new Size(103, 22);
            button_DownloadSingle.TabIndex = 22;
            button_DownloadSingle.Text = "Download Single";
            button_DownloadSingle.UseVisualStyleBackColor = true;
            button_DownloadSingle.Click += button_DownloadSingle_Click;
            // 
            // ReadWorkItemsForm
            // 
            AutoScaleMode = AutoScaleMode.Inherit;
            ClientSize = new Size(1221, 505);
            Controls.Add(button_DownloadSingle);
            Controls.Add(textBox_SingleItem);
            Controls.Add(label6);
            Controls.Add(label_numberofItems);
            Controls.Add(label4);
            Controls.Add(label3);
            Controls.Add(textBox_Days);
            Controls.Add(button_Compare);
            Controls.Add(label2);
            Controls.Add(textBox_rootFolder);
            Controls.Add(buttonFolderSelect);
            Controls.Add(label_size);
            Controls.Add(label_downloading);
            Controls.Add(downloadItems);
            Controls.Add(listView_WorkItems);
            Controls.Add(label1);
            Controls.Add(textBox_ProjectName);
            Controls.Add(Combobox_Quaries);
            Controls.Add(button_readItems);
            Controls.Add(connectProject);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "ReadWorkItemsForm";
            Text = "Read ADO";
            FormClosing += Form1_FormClosing;
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button connectProject;
        private System.Windows.Forms.Button button_readItems;
        private System.Windows.Forms.ComboBox Combobox_Quaries;
        private System.Windows.Forms.TextBox textBox_ProjectName;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ListView listView_WorkItems;
        private System.Windows.Forms.Button downloadItems;
        private System.Windows.Forms.Label label_downloading;
        private System.Windows.Forms.Label label_size;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
        private System.Windows.Forms.Button buttonFolderSelect;
        private System.Windows.Forms.TextBox textBox_rootFolder;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button button_Compare;
        private System.Windows.Forms.TextBox textBox_Days;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label_numberofItems;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox textBox_SingleItem;
        private System.Windows.Forms.Button button_DownloadSingle;
    }
}

