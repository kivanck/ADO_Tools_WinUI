namespace ADO_Tools.UI
{
    partial class SoftwareDownloaderForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.ComboBox cmbBuilds;
        private System.Windows.Forms.TextBox txtDownloadFolder;
        private System.Windows.Forms.Button btnBrowseDownloadFolder;
        private System.Windows.Forms.Button btnUpdate;
        private System.Windows.Forms.TextBox txtLog;
        private System.Windows.Forms.Button btnLoadBuilds;
        private System.Windows.Forms.CheckBox checkBoxCleanUninstall;
        private System.Windows.Forms.Label lblBuildCount;
        private System.Windows.Forms.NumericUpDown numBuildCount;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SoftwareDownloaderForm));
            cmbBuilds = new ComboBox();
            txtDownloadFolder = new TextBox();
            btnBrowseDownloadFolder = new Button();
            btnUpdate = new Button();
            txtLog = new TextBox();
            btnLoadBuilds = new Button();
            checkBoxCleanUninstall = new CheckBox();
            lblBuildCount = new Label();
            numBuildCount = new NumericUpDown();
            btnShowBentleySoftware = new Button();
            groupBox1 = new GroupBox();
            lblDefinitionID = new Label();
            lblProject = new Label();
            txtProject = new TextBox();
            cmbProductName = new ComboBox();
            btnAddDefinition = new Button();
            txtDefinitionId = new TextBox();
            groupBox2 = new GroupBox();
            btnRemoveDefinition = new Button();
            checkBoxDownloadOnly = new CheckBox();
            ((System.ComponentModel.ISupportInitialize)numBuildCount).BeginInit();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            SuspendLayout();
            // 
            // cmbBuilds
            // 
            cmbBuilds.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbBuilds.Font = new Font("Cascadia Code", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            cmbBuilds.Location = new Point(12, 163);
            cmbBuilds.Name = "cmbBuilds";
            cmbBuilds.Size = new Size(565, 24);
            cmbBuilds.TabIndex = 9;
            // 
            // txtDownloadFolder
            // 
            txtDownloadFolder.Location = new Point(19, 22);
            txtDownloadFolder.Name = "txtDownloadFolder";
            txtDownloadFolder.Size = new Size(435, 23);
            txtDownloadFolder.TabIndex = 13;
            // 
            // btnBrowseDownloadFolder
            // 
            btnBrowseDownloadFolder.Location = new Point(469, 21);
            btnBrowseDownloadFolder.Name = "btnBrowseDownloadFolder";
            btnBrowseDownloadFolder.Size = new Size(90, 23);
            btnBrowseDownloadFolder.TabIndex = 14;
            btnBrowseDownloadFolder.Text = "Browse";
            btnBrowseDownloadFolder.Click += btnBrowseDownloadFolder_Click;
            // 
            // btnUpdate
            // 
            btnUpdate.Location = new Point(12, 193);
            btnUpdate.Name = "btnUpdate";
            btnUpdate.Size = new Size(120, 32);
            btnUpdate.TabIndex = 16;
            btnUpdate.Text = "Run Update";
            btnUpdate.Click += btnUpdate_Click;
            // 
            // txtLog
            // 
            txtLog.Location = new Point(12, 293);
            txtLog.Multiline = true;
            txtLog.Name = "txtLog";
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Size = new Size(565, 273);
            txtLog.TabIndex = 17;
            // 
            // btnLoadBuilds
            // 
            btnLoadBuilds.BackColor = SystemColors.Control;
            btnLoadBuilds.FlatAppearance.BorderColor = Color.FromArgb(224, 224, 224);
            btnLoadBuilds.FlatStyle = FlatStyle.Popup;
            btnLoadBuilds.Location = new Point(12, 122);
            btnLoadBuilds.Name = "btnLoadBuilds";
            btnLoadBuilds.Size = new Size(278, 27);
            btnLoadBuilds.TabIndex = 10;
            btnLoadBuilds.Text = "Load Builds";
            btnLoadBuilds.UseVisualStyleBackColor = false;
            btnLoadBuilds.Click += btnLoadBuilds_Click;
            // 
            // checkBoxCleanUninstall
            // 
            checkBoxCleanUninstall.Location = new Point(451, 198);
            checkBoxCleanUninstall.Name = "checkBoxCleanUninstall";
            checkBoxCleanUninstall.Size = new Size(120, 23);
            checkBoxCleanUninstall.TabIndex = 15;
            checkBoxCleanUninstall.Text = "Clean Uninstall";
            checkBoxCleanUninstall.UseVisualStyleBackColor = true;
            // 
            // lblBuildCount
            // 
            lblBuildCount.Location = new Point(357, 120);
            lblBuildCount.Name = "lblBuildCount";
            lblBuildCount.Size = new Size(124, 30);
            lblBuildCount.TabIndex = 7;
            lblBuildCount.Text = "# of builds to show";
            lblBuildCount.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // numBuildCount
            // 
            numBuildCount.Location = new Point(487, 126);
            numBuildCount.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numBuildCount.Name = "numBuildCount";
            numBuildCount.Size = new Size(84, 23);
            numBuildCount.TabIndex = 8;
            numBuildCount.TextAlign = HorizontalAlignment.Center;
            numBuildCount.Value = new decimal(new int[] { 30, 0, 0, 0 });
            // 
            // btnShowBentleySoftware
            // 
            btnShowBentleySoftware.Location = new Point(487, 23);
            btnShowBentleySoftware.Name = "btnShowBentleySoftware";
            btnShowBentleySoftware.Size = new Size(90, 89);
            btnShowBentleySoftware.TabIndex = 11;
            btnShowBentleySoftware.Text = "Show Existing Products";
            btnShowBentleySoftware.Click += btnShowBentleySoftware_Click;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(txtDownloadFolder);
            groupBox1.Controls.Add(btnBrowseDownloadFolder);
            groupBox1.Location = new Point(12, 233);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(565, 54);
            groupBox1.TabIndex = 20;
            groupBox1.TabStop = false;
            groupBox1.Text = "Download Folder";
            // 
            // lblDefinitionID
            // 
            lblDefinitionID.Location = new Point(6, 61);
            lblDefinitionID.Name = "lblDefinitionID";
            lblDefinitionID.Size = new Size(90, 23);
            lblDefinitionID.TabIndex = 18;
            lblDefinitionID.Text = "Definition ID";
            lblDefinitionID.TextAlign = ContentAlignment.BottomLeft;
            // 
            // lblProject
            // 
            lblProject.Location = new Point(253, 61);
            lblProject.Name = "lblProject";
            lblProject.Size = new Size(90, 23);
            lblProject.TabIndex = 5;
            lblProject.Text = "Project";
            lblProject.TextAlign = ContentAlignment.BottomLeft;
            // 
            // txtProject
            // 
            txtProject.Location = new Point(313, 61);
            txtProject.Name = "txtProject";
            txtProject.Size = new Size(123, 23);
            txtProject.TabIndex = 6;
            // 
            // cmbProductName
            // 
            cmbProductName.Location = new Point(6, 22);
            cmbProductName.Name = "cmbProductName";
            cmbProductName.Size = new Size(363, 23);
            cmbProductName.TabIndex = 0;
            // 
            // btnAddDefinition
            // 
            btnAddDefinition.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnAddDefinition.Location = new Point(384, 21);
            btnAddDefinition.Name = "btnAddDefinition";
            btnAddDefinition.Size = new Size(23, 23);
            btnAddDefinition.TabIndex = 4;
            btnAddDefinition.Text = "+";
            // 
            // txtDefinitionId
            // 
            txtDefinitionId.Location = new Point(93, 61);
            txtDefinitionId.Name = "txtDefinitionId";
            txtDefinitionId.Size = new Size(123, 23);
            txtDefinitionId.TabIndex = 2;
            txtDefinitionId.TextChanged += txtDefinitionId_TextChanged;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(btnRemoveDefinition);
            groupBox2.Controls.Add(txtDefinitionId);
            groupBox2.Controls.Add(cmbProductName);
            groupBox2.Controls.Add(btnAddDefinition);
            groupBox2.Controls.Add(txtProject);
            groupBox2.Controls.Add(lblDefinitionID);
            groupBox2.Controls.Add(lblProject);
            groupBox2.Location = new Point(12, 12);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(469, 100);
            groupBox2.TabIndex = 21;
            groupBox2.TabStop = false;
            groupBox2.Text = "Product Selection";
            // 
            // btnRemoveDefinition
            // 
            btnRemoveDefinition.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnRemoveDefinition.Location = new Point(413, 21);
            btnRemoveDefinition.Name = "btnRemoveDefinition";
            btnRemoveDefinition.Size = new Size(23, 23);
            btnRemoveDefinition.TabIndex = 19;
            btnRemoveDefinition.Text = "-";
            btnRemoveDefinition.UseVisualStyleBackColor = true;
            btnRemoveDefinition.Click += btnRemoveDefinition_Click;
            // 
            // checkBoxDownloadOnly
            // 
            checkBoxDownloadOnly.AutoSize = true;
            checkBoxDownloadOnly.Location = new Point(325, 200);
            checkBoxDownloadOnly.Name = "checkBoxDownloadOnly";
            checkBoxDownloadOnly.Size = new Size(108, 19);
            checkBoxDownloadOnly.TabIndex = 22;
            checkBoxDownloadOnly.Text = "Download Only";
            checkBoxDownloadOnly.UseVisualStyleBackColor = true;
            // 
            // SoftwareDownloaderForm
            // 
            ClientSize = new Size(595, 595);
            Controls.Add(checkBoxDownloadOnly);
            Controls.Add(groupBox2);
            Controls.Add(btnLoadBuilds);
            Controls.Add(cmbBuilds);
            Controls.Add(groupBox1);
            Controls.Add(lblBuildCount);
            Controls.Add(numBuildCount);
            Controls.Add(btnShowBentleySoftware);
            Controls.Add(checkBoxCleanUninstall);
            Controls.Add(btnUpdate);
            Controls.Add(txtLog);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "SoftwareDownloaderForm";
            Text = "ADO Software Update Tool";
            ((System.ComponentModel.ISupportInitialize)numBuildCount).EndInit();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }
        private Button btnShowBentleySoftware;
        private GroupBox groupBox1;
        private Label lblDefinitionID;
        private Label lblProject;
        private TextBox txtProject;
        private ComboBox cmbProductName;
        private Button btnAddDefinition;
        private TextBox txtDefinitionId;
        private GroupBox groupBox2;
        private Button btnRemoveDefinition;
        private CheckBox checkBoxDownloadOnly;
    }
}
