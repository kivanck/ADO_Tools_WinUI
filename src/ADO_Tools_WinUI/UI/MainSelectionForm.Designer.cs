namespace ADO_Tools.UI
{
    partial class MainSelectionForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.TextBox txtPAT;
        private System.Windows.Forms.Label lblPAT;
        private System.Windows.Forms.Button btnReadWorkItems;
        private System.Windows.Forms.Button btnSoftwareDownload;
        private System.Windows.Forms.Panel panelMain;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainSelectionForm));
            panelMain = new Panel();
            label7 = new Label();
            txtOrganization = new TextBox();
            lblOrganisation = new Label();
            lblTitle = new Label();
            lblPAT = new Label();
            txtPAT = new TextBox();
            btnReadWorkItems = new Button();
            btnSoftwareDownload = new Button();
            panelMain.SuspendLayout();
            SuspendLayout();
            // 
            // panelMain
            // 
            panelMain.BackColor = Color.White;
            panelMain.BorderStyle = BorderStyle.FixedSingle;
            panelMain.Controls.Add(label7);
            panelMain.Controls.Add(txtOrganization);
            panelMain.Controls.Add(lblOrganisation);
            panelMain.Controls.Add(lblTitle);
            panelMain.Controls.Add(lblPAT);
            panelMain.Controls.Add(txtPAT);
            panelMain.Controls.Add(btnReadWorkItems);
            panelMain.Controls.Add(btnSoftwareDownload);
            panelMain.Dock = DockStyle.Fill;
            panelMain.Location = new Point(0, 0);
            panelMain.Name = "panelMain";
            panelMain.Size = new Size(388, 233);
            panelMain.TabIndex = 0;
            // 
            // label7
            // 
            label7.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            label7.AutoSize = true;
            label7.Font = new Font("Times New Roman", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label7.ForeColor = SystemColors.ActiveBorder;
            label7.Location = new Point(238, 213);
            label7.Name = "label7";
            label7.Size = new Size(146, 15);
            label7.TabIndex = 21;
            label7.Text = "kivanc.karakas@bentley.com";
            // 
            // txtOrganization
            // 
            txtOrganization.Location = new Point(140, 59);
            txtOrganization.Name = "txtOrganization";
            txtOrganization.Size = new Size(216, 23);
            txtOrganization.TabIndex = 6;
            // 
            // lblOrganisation
            // 
            lblOrganisation.AutoSize = true;
            lblOrganisation.Font = new Font("Segoe UI", 10F);
            lblOrganisation.Location = new Point(36, 59);
            lblOrganisation.Name = "lblOrganisation";
            lblOrganisation.Size = new Size(91, 19);
            lblOrganisation.TabIndex = 5;
            lblOrganisation.Text = "Organisation:";
            // 
            // lblTitle
            // 
            lblTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(30, 30, 60);
            lblTitle.Location = new Point(0, 10);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(400, 40);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "ADO Tools";
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblPAT
            // 
            lblPAT.AutoSize = true;
            lblPAT.Font = new Font("Segoe UI", 10F);
            lblPAT.Location = new Point(36, 94);
            lblPAT.Name = "lblPAT";
            lblPAT.Size = new Size(181, 19);
            lblPAT.TabIndex = 1;
            lblPAT.Text = "Personal Access Token (PAT):";
            // 
            // txtPAT
            // 
            txtPAT.Font = new Font("Segoe UI", 10F);
            txtPAT.Location = new Point(36, 119);
            txtPAT.Name = "txtPAT";
            txtPAT.Size = new Size(320, 25);
            txtPAT.TabIndex = 2;
            // 
            // btnReadWorkItems
            // 
            btnReadWorkItems.BackColor = Color.FromArgb(0, 120, 215);
            btnReadWorkItems.FlatAppearance.BorderSize = 0;
            btnReadWorkItems.FlatStyle = FlatStyle.Flat;
            btnReadWorkItems.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnReadWorkItems.ForeColor = Color.White;
            btnReadWorkItems.Location = new Point(36, 164);
            btnReadWorkItems.Name = "btnReadWorkItems";
            btnReadWorkItems.Size = new Size(150, 35);
            btnReadWorkItems.TabIndex = 3;
            btnReadWorkItems.Text = "Read Work Items";
            btnReadWorkItems.UseVisualStyleBackColor = false;
            btnReadWorkItems.Click += btnReadWorkItems_Click;
            // 
            // btnSoftwareDownload
            // 
            btnSoftwareDownload.BackColor = Color.FromArgb(0, 153, 51);
            btnSoftwareDownload.FlatAppearance.BorderSize = 0;
            btnSoftwareDownload.FlatStyle = FlatStyle.Flat;
            btnSoftwareDownload.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnSoftwareDownload.ForeColor = Color.White;
            btnSoftwareDownload.Location = new Point(206, 164);
            btnSoftwareDownload.Name = "btnSoftwareDownload";
            btnSoftwareDownload.Size = new Size(150, 35);
            btnSoftwareDownload.TabIndex = 4;
            btnSoftwareDownload.Text = "Software Download";
            btnSoftwareDownload.UseVisualStyleBackColor = false;
            btnSoftwareDownload.Click += btnSoftwareDownload_Click;
            // 
            // MainSelectionForm
            // 
            AcceptButton = btnReadWorkItems;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(388, 233);
            Controls.Add(panelMain);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "MainSelectionForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "ADO Tools - Select Functionality";
            FormClosing += MainSelectionForm_FormClosing;
            panelMain.ResumeLayout(false);
            panelMain.PerformLayout();
            ResumeLayout(false);
        }
        private TextBox txtOrganization;
        private Label lblOrganisation;
        private Label label7;
    }
}