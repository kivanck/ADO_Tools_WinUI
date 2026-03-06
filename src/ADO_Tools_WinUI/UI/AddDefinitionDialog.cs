using System;
using System.Windows.Forms;

namespace ADO_Tools.UI
{
    public partial class AddDefinitionDialog : Form
    {
        public string DefinitionId => txtId.Text.Trim();
        public string DefinitionName => txtName.Text.Trim();
        public string Project => txtProject.Text.Trim();

        public AddDefinitionDialog()
        {
            InitializeComponent();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(DefinitionId) ||
                string.IsNullOrWhiteSpace(DefinitionName) ||
                string.IsNullOrWhiteSpace(Project))
            {
                MessageBox.Show("All fields are required.");
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}