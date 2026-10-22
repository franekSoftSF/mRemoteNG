using BeyondTrust.PasswordSafe.Client;

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

        /// <summary>
        /// Runs the calls the connector depends on and shows how each one went, so a
        /// setup problem is visible here rather than as a failed connection later.
        /// </summary>
        private void btnTest_Click(object sender, EventArgs e)
        {
            PasswordSafeOptions options = new()
            {
                Url = tbServerURL.Text,
                ApiKey = tbAPIKey.Text,
                RunAsUser = tbRunAs.Text,
                RunAsPassword = tbRunAsPassword.Text,
                Challenge = tbChallenge.Text,
                Timeout = TimeSpan.FromSeconds(30)
            };

            Cursor previousCursor = Cursor;
            Cursor = Cursors.WaitCursor;
            btnTest.Enabled = false;
            tbTestResult.Text = "Testing...";

            try
            {
                using PasswordSafeClient client = new(options);
                ConnectionTestResult result = client.TestConnection();

                tbTestResult.Lines = result.ToString().Split(Environment.NewLine);
            }
            catch (Exception ex)
            {
                tbTestResult.Text = "Test failed: " + ex.Message;
            }
            finally
            {
                btnTest.Enabled = true;
                Cursor = previousCursor;
            }
        }
    }
}
