using BlinkyAccess.Client;
using mRemoteNG.Config.BlinkyAccess;
using mRemoteNG.Resources.Language;
using System;
using System.Runtime.Versioning;
using System.Threading;

namespace mRemoteNG.UI.Forms.OptionsPages
{
    /// <summary>
    /// The Blinky Access portal address, its access token and the startup switch.
    ///
    /// The token field never shows a whole token: a stored one is displayed as its
    /// beginning only, and is left alone unless the user types something else over it.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed partial class BlinkyAccessPage
    {
        /// <summary>
        /// What the token field was filled with when the page loaded. While the field
        /// still reads exactly this, the stored token has not been touched.
        /// </summary>
        private string _shownTokenPreview = "";

        public BlinkyAccessPage()
        {
            InitializeComponent();
            ApplyTheme();
            PageIcon = Resources.ImageConverter.GetImageAsIcon(Properties.Resources.SyncArrow_16x);
        }

        public override string PageName
        {
            get => Language.BlinkyAccessRootName;
            set { }
        }

        public override void ApplyLanguage()
        {
            base.ApplyLanguage();

            lblExplanation.Text = Language.BlinkyAccessExplanation;
            lblPortalAddress.Text = Language.BlinkyAccessPortalAddress;
            lblAccessToken.Text = Language.BlinkyAccessAccessToken;
            lblTokenHint.Text = Language.BlinkyAccessTokenHint;
            chkSyncOnStartup.Text = Language.BlinkyAccessSyncOnStartup;
            btnTest.Text = Language.BlinkyAccessTestConnection;
        }

        public override void LoadSettings()
        {
            txtPortalAddress.Text = BlinkyAccessSettings.PortalAddress;
            chkSyncOnStartup.Checked = BlinkyAccessSettings.SyncOnStartup;

            _shownTokenPreview = BlinkyAccessSettings.GetTokenPreview();
            txtAccessToken.Text = _shownTokenPreview;
            lblTestResult.Text = "";
        }

        public override void SaveSettings()
        {
            base.SaveSettings();

            BlinkyAccessSettings.PortalAddress = txtPortalAddress.Text;
            BlinkyAccessSettings.SyncOnStartup = chkSyncOnStartup.Checked;

            if (TokenWasEdited())
            {
                // an emptied field means "forget the token", which SetToken handles
                BlinkyAccessSettings.SetToken(txtAccessToken.Text);
                _shownTokenPreview = BlinkyAccessSettings.GetTokenPreview();
                txtAccessToken.Text = _shownTokenPreview;
            }
        }

        private bool TokenWasEdited()
        {
            return !string.Equals(txtAccessToken.Text, _shownTokenPreview, StringComparison.Ordinal);
        }

        /// <summary>
        /// Makes the one API call the synchronisation depends on, with what is on screen
        /// rather than what is saved, so a new address or token can be checked before
        /// the dialog is closed.
        /// </summary>
        private void btnTest_Click(object sender, EventArgs e)
        {
            string portalAddress = txtPortalAddress.Text.Trim();

            if (!BlinkyAccessOptions.IsUsablePortalUrl(portalAddress))
            {
                lblTestResult.Text = Language.BlinkyAccessAddressMustBeHttps;
                return;
            }

            // an unedited field holds a preview, not a token, so the stored one is used
            string token = TokenWasEdited() ? txtAccessToken.Text.Trim() : BlinkyAccessSettings.GetToken();

            if (token.Length == 0)
            {
                lblTestResult.Text = Language.BlinkyAccessNotConfigured;
                return;
            }

            btnTest.Enabled = false;
            lblTestResult.Text = Language.BlinkyAccessTestRunning;

            BlinkyAccessOptions options = new() { PortalUrl = portalAddress, Token = token };

            Thread thread = new(() =>
            {
                using BlinkyAccessClient client = new(options);
                BlinkyAccessTestResult result = client.TestConnection();
                ShowTestResult(result);
            })
            {
                IsBackground = true,
                Name = "BlinkyAccessTest"
            };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private void ShowTestResult(BlinkyAccessTestResult result)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(() => ShowTestResult(result));
                return;
            }

            lblTestResult.Text = result.Detail;
            btnTest.Enabled = true;
        }
    }
}
