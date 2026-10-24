using System;
using System.Runtime.Versioning;
using System.Windows.Forms;
using mRemoteNG.App;
using mRemoteNG.Credential;
using mRemoteNG.Resources.Language;

namespace mRemoteNG.UI.Menu
{
    [SupportedOSPlatform("windows")]
    public class ToolsMenu : ToolStripMenuItem
    {
        private ToolStripMenuItem _mMenToolsSshTransfer;
        private ToolStripMenuItem _mMenToolsExternalApps;
        private ToolStripMenuItem _mMenToolsPortScan;
        private ToolStripMenuItem _mMenToolsUvncsc;
        private ToolStripMenuItem _mMenToolsBeyondTrustRefresh;
        private ToolStripMenuItem _mMenToolsBlinkyAccessSync;
        private ToolStripMenuItem _mMenToolsSmartcardConnection;

        public Form MainForm { get; set; }
        public ICredentialRepositoryList CredentialProviderCatalog { get; set; }

        public ToolsMenu()
        {
            Initialize();
        }

        private void Initialize()
        {
            _mMenToolsSshTransfer = new ToolStripMenuItem();
            _mMenToolsUvncsc = new ToolStripMenuItem();
            _mMenToolsExternalApps = new ToolStripMenuItem();
            _mMenToolsPortScan = new ToolStripMenuItem();
            _mMenToolsBeyondTrustRefresh = new ToolStripMenuItem();
            _mMenToolsBlinkyAccessSync = new ToolStripMenuItem();
            _mMenToolsSmartcardConnection = new ToolStripMenuItem();
            //
            // mMenTools
            //
            DropDownItems.AddRange(new ToolStripItem[]
            {
                _mMenToolsSshTransfer,
                _mMenToolsUvncsc,
                _mMenToolsExternalApps,
                _mMenToolsPortScan,
                _mMenToolsSmartcardConnection,
                _mMenToolsBeyondTrustRefresh,
                _mMenToolsBlinkyAccessSync
            });
            Name = "mMenTools";
            Size = new System.Drawing.Size(48, 20);
            Text = Language._Tools;
            // 
            // mMenToolsSSHTransfer
            // 
            _mMenToolsSshTransfer.Image = Properties.Resources.SyncArrow_16x;
            _mMenToolsSshTransfer.Name = "mMenToolsSSHTransfer";
            _mMenToolsSshTransfer.Size = new System.Drawing.Size(184, 22);
            _mMenToolsSshTransfer.Text = Language.SshFileTransfer;
            _mMenToolsSshTransfer.Click += mMenToolsSSHTransfer_Click;
            // 
            // mMenToolsUVNCSC
            // 
            _mMenToolsUvncsc.Name = "mMenToolsUVNCSC";
            _mMenToolsUvncsc.Size = new System.Drawing.Size(184, 22);
            _mMenToolsUvncsc.Text = Language.UltraVNCSingleClick;
            _mMenToolsUvncsc.Visible = false;
            _mMenToolsUvncsc.Click += mMenToolsUVNCSC_Click;
            // 
            // mMenToolsExternalApps
            // 
            _mMenToolsExternalApps.Image = Properties.Resources.Console_16x;
            _mMenToolsExternalApps.Name = "mMenToolsExternalApps";
            _mMenToolsExternalApps.Size = new System.Drawing.Size(184, 22);
            _mMenToolsExternalApps.Text = Language.ExternalTool;
            _mMenToolsExternalApps.Click += mMenToolsExternalApps_Click;
            // 
            // mMenToolsPortScan
            // 
            _mMenToolsPortScan.Image = Properties.Resources.SearchAndApps_16x;
            _mMenToolsPortScan.Name = "mMenToolsPortScan";
            _mMenToolsPortScan.Size = new System.Drawing.Size(184, 22);
            _mMenToolsPortScan.Text = Language.PortScan;
            _mMenToolsPortScan.Click += mMenToolsPortScan_Click;
            //
            // mMenToolsBeyondTrustRefresh
            //
            _mMenToolsBeyondTrustRefresh.Image = Properties.Resources.SyncArrow_16x;
            _mMenToolsBeyondTrustRefresh.Name = "mMenToolsBeyondTrustRefresh";
            _mMenToolsBeyondTrustRefresh.Size = new System.Drawing.Size(184, 22);
            _mMenToolsBeyondTrustRefresh.Text = Language.BeyondTrustRefresh;
            _mMenToolsBeyondTrustRefresh.Click += mMenToolsBeyondTrustRefresh_Click;
            //
            // mMenToolsBlinkyAccessSync
            //
            _mMenToolsBlinkyAccessSync.Image = Properties.Resources.SyncArrow_16x;
            _mMenToolsBlinkyAccessSync.Name = "mMenToolsBlinkyAccessSync";
            _mMenToolsBlinkyAccessSync.Size = new System.Drawing.Size(184, 22);
            _mMenToolsBlinkyAccessSync.Text = Language.BlinkyAccessSynchronize;
            _mMenToolsBlinkyAccessSync.Click += mMenToolsBlinkyAccessSync_Click;
            //
            // mMenToolsSmartcardConnection
            //
            _mMenToolsSmartcardConnection.Name = "mMenToolsSmartcardConnection";
            _mMenToolsSmartcardConnection.Size = new System.Drawing.Size(184, 22);
            _mMenToolsSmartcardConnection.Text = Language.SmartcardNewConnection;
            _mMenToolsSmartcardConnection.Click += mMenToolsSmartcardConnection_Click;
        }

        public void ApplyLanguage()
        {
            Text = Language._Tools;
            _mMenToolsSshTransfer.Text = Language.SshFileTransfer;
            _mMenToolsExternalApps.Text = Language.ExternalTool;
            _mMenToolsPortScan.Text = Language.PortScan;
            _mMenToolsBeyondTrustRefresh.Text = Language.BeyondTrustRefresh;
            _mMenToolsSmartcardConnection.Text = Language.SmartcardNewConnection;
            _mMenToolsBlinkyAccessSync.Text = Language.BlinkyAccessSynchronize;
        }

        #region Tools

        private void mMenToolsSSHTransfer_Click(object sender, EventArgs e)
        {
            AppWindows.Show(WindowType.SSHTransfer);
        }

        private void mMenToolsUVNCSC_Click(object sender, EventArgs e)
        {
            AppWindows.Show(WindowType.UltraVNCSC);
        }

        private void mMenToolsExternalApps_Click(object sender, EventArgs e)
        {
            AppWindows.Show(WindowType.ExternalApps);
        }

        private void mMenToolsPortScan_Click(object sender, EventArgs e)
        {
            AppWindows.Show(WindowType.PortScan);
        }

        private void mMenToolsBeyondTrustRefresh_Click(object sender, EventArgs e)
        {
            Config.BeyondTrust.BeyondTrustSyncManager.Instance.SyncAsync();
        }

        private void mMenToolsBlinkyAccessSync_Click(object sender, EventArgs e)
        {
            Config.BlinkyAccess.BlinkyAccessSyncManager.Instance.SyncAsync(interactive: true);
        }

        private void mMenToolsSmartcardConnection_Click(object sender, EventArgs e)
        {
            Forms.SmartcardConnectionCreator.CreateInteractive();
        }

        private void mMenToolsOptions_Click(object sender, EventArgs e)
        {
            AppWindows.Show(WindowType.Options);
        }

        #endregion
    }
}