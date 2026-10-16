namespace ExternalConnectors.BT
{
    public partial class BTConnectionForm : Form
    {
        public BTConnectionForm()
        {
            InitializeComponent();
        }

        private void BTConnectionForm_Activated(object sender, EventArgs e)
        {
            if (tbAPIKey.Text.Length == 0)
                tbAPIKey.Focus();
            else if (tbRunAs.Text.Length == 0)
                tbRunAs.Focus();
            else
                btnOK.Focus();
        }
    }
}
