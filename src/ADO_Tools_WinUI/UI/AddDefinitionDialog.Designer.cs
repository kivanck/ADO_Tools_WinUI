namespace ADO_Tools.UI
{
    partial class AddDefinitionDialog
    {
        private System.ComponentModel.IContainer components = null;
        private TextBox txtId;
        private TextBox txtName;
        private TextBox txtProject;
        private Label lblId;
        private Label lblName;
        private Label lblProject;
        private Button btnOK;
        private Button btnCancel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AddDefinitionDialog));
            txtId = new TextBox();
            txtName = new TextBox();
            txtProject = new TextBox();
            lblId = new Label();
            lblName = new Label();
            lblProject = new Label();
            btnOK = new Button();
            btnCancel = new Button();
            SuspendLayout();
            // 
            // txtId
            // 
            txtId.Location = new Point(110, 42);
            txtId.Name = "txtId";
            txtId.Size = new Size(150, 23);
            txtId.TabIndex = 2;
            // 
            // txtName
            // 
            txtName.Location = new Point(110, 13);
            txtName.Name = "txtName";
            txtName.Size = new Size(150, 23);
            txtName.TabIndex = 1;
            // 
            // txtProject
            // 
            txtProject.Location = new Point(110, 71);
            txtProject.Name = "txtProject";
            txtProject.Size = new Size(150, 23);
            txtProject.TabIndex = 3;
            // 
            // lblId
            // 
            lblId.Location = new Point(12, 42);
            lblId.Name = "lblId";
            lblId.Size = new Size(100, 23);
            lblId.TabIndex = 0;
            lblId.Text = "Definition ID:";
            lblId.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lblName
            // 
            lblName.Location = new Point(12, 13);
            lblName.Name = "lblName";
            lblName.Size = new Size(100, 23);
            lblName.TabIndex = 2;
            lblName.Text = "Name:";
            lblName.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lblProject
            // 
            lblProject.Location = new Point(12, 71);
            lblProject.Name = "lblProject";
            lblProject.Size = new Size(100, 23);
            lblProject.TabIndex = 4;
            lblProject.Text = "Project:";
            lblProject.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // btnOK
            // 
            btnOK.Location = new Point(50, 110);
            btnOK.Name = "btnOK";
            btnOK.Size = new Size(75, 23);
            btnOK.TabIndex = 6;
            btnOK.Text = "OK";
            btnOK.Click += btnOK_Click;
            // 
            // btnCancel
            // 
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Location = new Point(150, 110);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(75, 23);
            btnCancel.TabIndex = 7;
            btnCancel.Text = "Cancel";
            // 
            // AddDefinitionDialog
            // 
            AcceptButton = btnOK;
            CancelButton = btnCancel;
            ClientSize = new Size(280, 150);
            Controls.Add(lblId);
            Controls.Add(txtId);
            Controls.Add(lblName);
            Controls.Add(txtName);
            Controls.Add(lblProject);
            Controls.Add(txtProject);
            Controls.Add(btnOK);
            Controls.Add(btnCancel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "AddDefinitionDialog";
            StartPosition = FormStartPosition.CenterParent;
            Text = "New Product Definition";
            ResumeLayout(false);
            PerformLayout();
        }
    }
}