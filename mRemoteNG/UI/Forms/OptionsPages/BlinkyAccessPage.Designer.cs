

using mRemoteNG.UI.Controls;

namespace mRemoteNG.UI.Forms.OptionsPages
{

    public sealed partial class BlinkyAccessPage : OptionsPage
    {

        //UserControl overrides dispose to clean up the component list.
        [System.Diagnostics.DebuggerNonUserCode()]
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && components != null)
                {
                    components.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        //Required by the Windows Form Designer
        private System.ComponentModel.Container components = null;

        //NOTE: The following procedure is required by the Windows Form Designer
        //It can be modified using the Windows Form Designer.
        //Do not modify it using the code editor.
        [System.Diagnostics.DebuggerStepThrough()]
        private void InitializeComponent()
        {
            lblExplanation = new MrngLabel();
            lblPortalAddress = new MrngLabel();
            txtPortalAddress = new MrngTextBox();
            lblAccessToken = new MrngLabel();
            txtAccessToken = new MrngTextBox();
            lblTokenHint = new MrngLabel();
            chkSyncOnStartup = new MrngCheckBox();
            btnTest = new MrngButton();
            lblTestResult = new MrngLabel();
            pnlOptions = new System.Windows.Forms.Panel();
            pnlOptions.SuspendLayout();
            SuspendLayout();
            //
            // lblExplanation
            //
            lblExplanation.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblExplanation.Location = new System.Drawing.Point(6, 4);
            lblExplanation.Name = "lblExplanation";
            lblExplanation.Size = new System.Drawing.Size(580, 32);
            lblExplanation.TabIndex = 0;
            lblExplanation.Text = "Connections you may open are read from a Blinky Access portal over its API.";
            //
            // lblPortalAddress
            //
            lblPortalAddress.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblPortalAddress.Location = new System.Drawing.Point(6, 48);
            lblPortalAddress.Name = "lblPortalAddress";
            lblPortalAddress.Size = new System.Drawing.Size(120, 20);
            lblPortalAddress.TabIndex = 1;
            lblPortalAddress.Text = "Portal address:";
            //
            // txtPortalAddress
            //
            txtPortalAddress.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            txtPortalAddress.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtPortalAddress.Location = new System.Drawing.Point(132, 45);
            txtPortalAddress.Name = "txtPortalAddress";
            txtPortalAddress.Size = new System.Drawing.Size(330, 22);
            txtPortalAddress.TabIndex = 2;
            //
            // lblAccessToken
            //
            lblAccessToken.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblAccessToken.Location = new System.Drawing.Point(6, 79);
            lblAccessToken.Name = "lblAccessToken";
            lblAccessToken.Size = new System.Drawing.Size(120, 20);
            lblAccessToken.TabIndex = 3;
            lblAccessToken.Text = "Access token:";
            //
            // txtAccessToken
            //
            txtAccessToken.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            txtAccessToken.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtAccessToken.Location = new System.Drawing.Point(132, 76);
            txtAccessToken.Name = "txtAccessToken";
            txtAccessToken.Size = new System.Drawing.Size(330, 22);
            txtAccessToken.TabIndex = 4;
            //
            // lblTokenHint
            //
            lblTokenHint.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblTokenHint.Location = new System.Drawing.Point(132, 102);
            lblTokenHint.Name = "lblTokenHint";
            lblTokenHint.Size = new System.Drawing.Size(454, 46);
            lblTokenHint.TabIndex = 5;
            lblTokenHint.Text = "Only the beginning of the stored token is shown.";
            //
            // chkSyncOnStartup
            //
            chkSyncOnStartup._mice = MrngCheckBox.MouseState.OUT;
            chkSyncOnStartup.AutoSize = true;
            chkSyncOnStartup.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            chkSyncOnStartup.Location = new System.Drawing.Point(9, 156);
            chkSyncOnStartup.Name = "chkSyncOnStartup";
            chkSyncOnStartup.Size = new System.Drawing.Size(200, 17);
            chkSyncOnStartup.TabIndex = 6;
            chkSyncOnStartup.Text = "Synchronize at startup";
            chkSyncOnStartup.UseVisualStyleBackColor = true;
            //
            // btnTest
            //
            btnTest._mice = MrngButton.MouseState.OUT;
            btnTest.Location = new System.Drawing.Point(9, 186);
            btnTest.Name = "btnTest";
            btnTest.Size = new System.Drawing.Size(120, 25);
            btnTest.TabIndex = 7;
            btnTest.Text = "Test";
            btnTest.UseVisualStyleBackColor = true;
            btnTest.Click += btnTest_Click;
            //
            // lblTestResult
            //
            lblTestResult.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblTestResult.Location = new System.Drawing.Point(135, 186);
            lblTestResult.Name = "lblTestResult";
            lblTestResult.Size = new System.Drawing.Size(451, 46);
            lblTestResult.TabIndex = 8;
            lblTestResult.Text = "";
            //
            // pnlOptions
            //
            pnlOptions.Controls.Add(lblExplanation);
            pnlOptions.Controls.Add(lblPortalAddress);
            pnlOptions.Controls.Add(txtPortalAddress);
            pnlOptions.Controls.Add(lblAccessToken);
            pnlOptions.Controls.Add(txtAccessToken);
            pnlOptions.Controls.Add(lblTokenHint);
            pnlOptions.Controls.Add(chkSyncOnStartup);
            pnlOptions.Controls.Add(btnTest);
            pnlOptions.Controls.Add(lblTestResult);
            pnlOptions.Dock = System.Windows.Forms.DockStyle.Top;
            pnlOptions.Location = new System.Drawing.Point(0, 0);
            pnlOptions.Name = "pnlOptions";
            pnlOptions.Size = new System.Drawing.Size(610, 245);
            pnlOptions.TabIndex = 0;
            //
            // BlinkyAccessPage
            //
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            Controls.Add(pnlOptions);
            Name = "BlinkyAccessPage";
            Size = new System.Drawing.Size(610, 490);
            pnlOptions.ResumeLayout(false);
            pnlOptions.PerformLayout();
            ResumeLayout(false);
        }

        internal MrngLabel lblExplanation;
        internal MrngLabel lblPortalAddress;
        internal MrngTextBox txtPortalAddress;
        internal MrngLabel lblAccessToken;
        internal MrngTextBox txtAccessToken;
        internal MrngLabel lblTokenHint;
        internal MrngCheckBox chkSyncOnStartup;
        internal MrngButton btnTest;
        internal MrngLabel lblTestResult;
        internal System.Windows.Forms.Panel pnlOptions;
    }
}
