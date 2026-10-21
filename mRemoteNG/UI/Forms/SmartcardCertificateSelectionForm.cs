using mRemoteNG.Resources.Language;
using mRemoteNG.Tools;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace mRemoteNG.UI.Forms
{
    /// <summary>
    /// Picks the certificate used for SSH authentication with PuTTY CAC. Only
    /// certificates whose key is on a card are shown by default, because the
    /// thumbprint has to belong to the card rather than to some leftover store entry.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class SmartcardCertificateSelectionForm : Form
    {
        private readonly ListView _certificateList;
        private readonly CheckBox _showAllCheckBox;
        private readonly Button _okButton;
        private readonly Label _hintLabel;

        /// <summary>The selected certificate in PuTTY CAC form, e.g. CAPI:&lt;thumbprint&gt;.</summary>
        public string SelectedIdentifier { get; private set; } = "";

        public SmartcardCertificateSelectionForm(string currentIdentifier = "")
        {
            Text = Language.SelectSmartcardCertificate;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(700, 320);
            ClientSize = new Size(900, 420);
            ShowIcon = false;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;

            _certificateList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false
            };
            _certificateList.Columns.Add("Subject", 260);
            _certificateList.Columns.Add("UPN", 180);
            _certificateList.Columns.Add("Issuer", 180);
            _certificateList.Columns.Add("Expires", 90);
            _certificateList.Columns.Add("Thumbprint", 260);
            _certificateList.Columns.Add("Key", 90);
            _certificateList.DoubleClick += (_, _) => AcceptSelection();
            _certificateList.SelectedIndexChanged += (_, _) => UpdateOkButton();

            _hintLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(8, 8, 8, 0),
                AutoEllipsis = true
            };

            _showAllCheckBox = new CheckBox
            {
                Text = Language.SmartcardShowAllCertificates,
                AutoSize = true,
                Location = new Point(12, 14)
            };
            _showAllCheckBox.CheckedChanged += (_, _) => LoadCertificates();

            _okButton = new Button
            {
                Text = Language._Ok,
                DialogResult = DialogResult.None,
                Anchor = AnchorStyles.Right,
                Size = new Size(100, 30),
                Enabled = false
            };
            _okButton.Click += (_, _) => AcceptSelection();

            Button cancelButton = new()
            {
                Text = Language._Cancel,
                DialogResult = DialogResult.Cancel,
                Anchor = AnchorStyles.Right,
                Size = new Size(100, 30)
            };

            Panel buttonPanel = new() { Dock = DockStyle.Bottom, Height = 56 };
            buttonPanel.Controls.Add(_showAllCheckBox);
            buttonPanel.Controls.Add(_okButton);
            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Resize += (_, _) =>
            {
                cancelButton.Location = new Point(buttonPanel.ClientSize.Width - cancelButton.Width - 12, 13);
                _okButton.Location = new Point(cancelButton.Left - _okButton.Width - 8, 13);
            };

            Controls.Add(_certificateList);
            Controls.Add(_hintLabel);
            Controls.Add(buttonPanel);

            AcceptButton = _okButton;
            CancelButton = cancelButton;

            SelectedIdentifier = currentIdentifier ?? "";
            LoadCertificates();
        }

        private void LoadCertificates()
        {
            _certificateList.BeginUpdate();
            _certificateList.Items.Clear();

            IReadOnlyList<SmartcardCertificateStore.CertificateEntry> certificates =
                SmartcardCertificateStore.List(!_showAllCheckBox.Checked);

            foreach (SmartcardCertificateStore.CertificateEntry certificate in certificates)
            {
                ListViewItem item = new(certificate.Subject)
                {
                    Tag = certificate
                };
                item.SubItems.Add(certificate.UserPrincipalName);
                item.SubItems.Add(certificate.Issuer);
                item.SubItems.Add(certificate.NotAfter.ToString("yyyy-MM-dd"));
                item.SubItems.Add(certificate.Thumbprint);
                item.SubItems.Add(certificate.OnSmartCard ? "card" : "software");

                if (certificate.IsExpired)
                    item.ForeColor = Color.Gray;

                if (string.Equals(certificate.CapiIdentifier, SelectedIdentifier, StringComparison.OrdinalIgnoreCase))
                    item.Selected = true;

                _certificateList.Items.Add(item);
            }

            _certificateList.EndUpdate();

            _hintLabel.Text = certificates.Count == 0
                ? Language.SmartcardNoCertificatesFound
                : Language.PropertyDescriptionSmartcardCertificate;

            UpdateOkButton();
        }

        private void UpdateOkButton()
        {
            _okButton.Enabled = _certificateList.SelectedItems.Count > 0;
        }

        private void AcceptSelection()
        {
            if (_certificateList.SelectedItems.Count == 0)
                return;

            if (_certificateList.SelectedItems[0].Tag is not SmartcardCertificateStore.CertificateEntry certificate)
                return;

            SelectedIdentifier = certificate.CapiIdentifier;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
