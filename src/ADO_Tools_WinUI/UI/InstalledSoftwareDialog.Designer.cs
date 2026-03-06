namespace ADO_Tools.UI
{
    partial class InstalledSoftwareDialog
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.ListView listViewSoftware;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(InstalledSoftwareDialog));
            listViewSoftware = new ListView();
            btnUninstall = new Button();
            checkBoxCleanUninstall = new CheckBox();
            textLogInstalled = new TextBox();
            SuspendLayout();
            // 
            // listViewSoftware
            // 
            listViewSoftware.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            listViewSoftware.Location = new Point(12, 12);
            listViewSoftware.Name = "listViewSoftware";
            listViewSoftware.Size = new Size(374, 300);
            listViewSoftware.TabIndex = 0;
            listViewSoftware.UseCompatibleStateImageBehavior = false;
            listViewSoftware.View = View.Details;
            // 
            // btnUninstall
            // 
            btnUninstall.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnUninstall.Location = new Point(392, 286);
            btnUninstall.Name = "btnUninstall";
            btnUninstall.Size = new Size(105, 26);
            btnUninstall.TabIndex = 1;
            btnUninstall.Text = "Uninstall";
            btnUninstall.UseVisualStyleBackColor = true;
            btnUninstall.Click += btnUninstall_ClickAsync;
            // 
            // checkBoxCleanUninstall
            // 
            checkBoxCleanUninstall.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            checkBoxCleanUninstall.AutoSize = true;
            checkBoxCleanUninstall.Location = new Point(392, 261);
            checkBoxCleanUninstall.Name = "checkBoxCleanUninstall";
            checkBoxCleanUninstall.Size = new Size(105, 19);
            checkBoxCleanUninstall.TabIndex = 19;
            checkBoxCleanUninstall.Text = "Clean Uninstall";
            checkBoxCleanUninstall.UseVisualStyleBackColor = true;
            // 
            // textLogInstalled
            // 
            textLogInstalled.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textLogInstalled.Location = new Point(12, 320);
            textLogInstalled.Multiline = true;
            textLogInstalled.Name = "textLogInstalled";
            textLogInstalled.Size = new Size(485, 99);
            textLogInstalled.TabIndex = 20;
            // 
            // InstalledSoftwareDialog
            // 
            ClientSize = new Size(509, 430);
            Controls.Add(textLogInstalled);
            Controls.Add(checkBoxCleanUninstall);
            Controls.Add(btnUninstall);
            Controls.Add(listViewSoftware);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "InstalledSoftwareDialog";
            Text = "Installed Bentley Software";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnUninstall;
        private CheckBox checkBoxCleanUninstall;
        private TextBox textLogInstalled;
    }
}