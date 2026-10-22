using mRemoteNG.Connection;
using mRemoteNG.Connection.Protocol;
using mRemoteNG.Resources.Language;
using mRemoteNG.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace mRemoteNG.UI.Forms
{
    /// <summary>
    /// Creates an SSH connection that authenticates with a certificate on a smart card,
    /// the same thing the New-SmartCardSshCommand.ps1 helper prints a command line for.
    ///
    /// The point of the dialog is that the parts which are easy to get wrong are either
    /// filled in or checked: the thumbprint comes off the card rather than being typed,
    /// the login name is taken from the certificate, and the three prerequisites that
    /// produce the same unhelpful "Server refused our key" are reported up front.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class SmartcardConnectionForm : Form
    {
        private readonly ListView _certificateList;
        private readonly TextBox _hostBox;
        private readonly TextBox _portBox;
        private readonly TextBox _userBox;
        private readonly TextBox _nameBox;
        private readonly CheckBox _agentForwardingBox;
        private readonly CheckBox _showStaleBox;
        private readonly Label _checksLabel;
        private readonly Button _okButton;

        private bool _nameEdited;

        /// <summary>The connection described by the dialog, once it was accepted.</summary>
        public ConnectionInfo Connection { get; private set; }

        public SmartcardConnectionForm()
        {
            Text = Language.SmartcardNewConnection;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(940, 620);
            MinimumSize = new Size(760, 520);
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
            _certificateList.Columns.Add("", 60);
            _certificateList.Columns.Add("Subject", 250);
            _certificateList.Columns.Add("UPN", 190);
            _certificateList.Columns.Add("Expires", 90);
            _certificateList.Columns.Add("Thumbprint", 270);
            _certificateList.SelectedIndexChanged += (_, _) => OnCertificateSelected();

            _showStaleBox = new CheckBox
            {
                Text = Language.SmartcardShowStaleCertificates,
                AutoSize = true,
                Location = new Point(12, 10)
            };
            _showStaleBox.CheckedChanged += (_, _) => LoadCertificates();

            _hostBox = new TextBox();
            _portBox = new TextBox { Text = "22" };
            _userBox = new TextBox();
            _nameBox = new TextBox();
            _nameBox.TextChanged += (_, _) => _nameEdited = _nameBox.Focused;
            _hostBox.TextChanged += (_, _) => { UpdateSuggestedName(); UpdateOkButton(); };

            _agentForwardingBox = new CheckBox
            {
                Text = Language.SshAgentForwarding,
                Checked = true,
                AutoSize = true
            };
            _agentForwardingBox.CheckedChanged += (_, _) => UpdateChecks();

            _checksLabel = new Label
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(4),
                Font = new Font(FontFamily.GenericMonospace, 8.5f)
            };

            TableLayoutPanel fields = new()
            {
                Dock = DockStyle.Top,
                Height = 150,
                ColumnCount = 2,
                Padding = new Padding(8)
            };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddField(fields, Language.Hostname, _hostBox);
            AddField(fields, Language.Port, _portBox);
            AddField(fields, Language.Username, _userBox);
            AddField(fields, Language.Name, _nameBox);
            fields.Controls.Add(new Label { Text = "", AutoSize = true });
            fields.Controls.Add(_agentForwardingBox);

            _okButton = new Button
            {
                Text = Language._Ok,
                Size = new Size(110, 30),
                Anchor = AnchorStyles.Right,
                Enabled = false
            };
            _okButton.Click += (_, _) => Accept();

            Button cancelButton = new()
            {
                Text = Language._Cancel,
                DialogResult = DialogResult.Cancel,
                Size = new Size(110, 30),
                Anchor = AnchorStyles.Right
            };

            Panel buttonPanel = new() { Dock = DockStyle.Bottom, Height = 52 };
            buttonPanel.Controls.Add(_showStaleBox);
            buttonPanel.Controls.Add(_okButton);
            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Resize += (_, _) =>
            {
                cancelButton.Location = new Point(buttonPanel.ClientSize.Width - cancelButton.Width - 12, 11);
                _okButton.Location = new Point(cancelButton.Left - _okButton.Width - 8, 11);
            };

            Panel checksPanel = new() { Dock = DockStyle.Bottom, Height = 110 };
            checksPanel.Controls.Add(_checksLabel);

            Panel listPanel = new() { Dock = DockStyle.Fill };
            listPanel.Controls.Add(_certificateList);

            Controls.Add(listPanel);
            Controls.Add(fields);
            Controls.Add(checksPanel);
            Controls.Add(buttonPanel);

            AcceptButton = _okButton;
            CancelButton = cancelButton;

            LoadCertificates();
        }

        private static void AddField(TableLayoutPanel panel, string label, Control control)
        {
            panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 3, 3) });
            control.Dock = DockStyle.Fill;
            panel.Controls.Add(control);
        }

        private void LoadCertificates()
        {
            _certificateList.BeginUpdate();
            _certificateList.Items.Clear();

            IReadOnlyList<SmartcardCertificateStore.CertificateEntry> certificates = SmartcardCertificateStore.List();

            foreach (SmartcardCertificateStore.CertificateEntry certificate in certificates)
            {
                if (!certificate.ContainerPresent && !_showStaleBox.Checked)
                    continue;

                ListViewItem item = new(certificate.ContainerPresent ? "card" : "stale") { Tag = certificate };
                item.SubItems.Add(certificate.Subject);
                item.SubItems.Add(certificate.UserPrincipalName);
                item.SubItems.Add(certificate.NotAfter.ToString("yyyy-MM-dd"));
                item.SubItems.Add(certificate.Thumbprint);

                if (!certificate.ContainerPresent || certificate.IsExpired)
                    item.ForeColor = Color.Gray;

                _certificateList.Items.Add(item);
            }

            if (_certificateList.Items.Count > 0)
                _certificateList.Items[0].Selected = true;

            _certificateList.EndUpdate();
            OnCertificateSelected();
        }

        private SmartcardCertificateStore.CertificateEntry SelectedCertificate =>
            _certificateList.SelectedItems.Count > 0
                ? _certificateList.SelectedItems[0].Tag as SmartcardCertificateStore.CertificateEntry
                : null;

        private void OnCertificateSelected()
        {
            SmartcardCertificateStore.CertificateEntry certificate = SelectedCertificate;

            if (certificate != null && !string.IsNullOrEmpty(certificate.UserPrincipalName) &&
                string.IsNullOrEmpty(_userBox.Text))
            {
                // the UPN is what SSSD matches the certificate against, so it is also
                // the login name the server expects
                _userBox.Text = certificate.UserPrincipalName;
            }

            UpdateChecks();
            UpdateOkButton();
        }

        private void UpdateSuggestedName()
        {
            if (_nameEdited)
                return;

            string host = _hostBox.Text.Trim();
            _nameBox.Text = host.Contains('.') ? host[..host.IndexOf('.')] : host;
        }

        private void UpdateOkButton()
        {
            _okButton.Enabled = SelectedCertificate != null && !string.IsNullOrWhiteSpace(_hostBox.Text);
        }

        /// <summary>
        /// Reports the three things that each produce the same "Server refused our key"
        /// on the server side, so they can be told apart before connecting.
        /// </summary>
        private void UpdateChecks()
        {
            List<string> lines = [];

            string puttyPath = PuttyBase.PuttyPath ?? "";
            bool isBundledPuttyNg = PuttyTypeDetector.GetPuttyType(puttyPath) == PuttyTypeDetector.PuttyType.PuttyNg;
            lines.Add(isBundledPuttyNg
                ? "FAIL  SSH client: the bundled PuTTYNG cannot read certificates. Set Options, Advanced, custom PuTTY path to PuTTY CAC."
                : $"OK    SSH client: {puttyPath}");

            SmartcardCertificateStore.CertificateEntry certificate = SelectedCertificate;
            if (certificate == null)
                lines.Add("FAIL  Certificate: none selected.");
            else if (!certificate.ContainerPresent)
                lines.Add("FAIL  Certificate: its key container is not on any inserted card, so this entry cannot authenticate.");
            else if (certificate.IsExpired)
                lines.Add($"WARN  Certificate: expired on {certificate.NotAfter:yyyy-MM-dd}.");
            else
                lines.Add($"OK    Certificate: key container present on an inserted card, valid until {certificate.NotAfter:yyyy-MM-dd}.");

            if (_agentForwardingBox.Checked)
            {
                bool pageantRunning = Process.GetProcessesByName("pageant").Length > 0;
                lines.Add(pageantRunning
                    ? "OK    Agent forwarding: Pageant is running; the certificate is loaded into it when connecting."
                    : "WARN  Agent forwarding: Pageant is not running. It will be started when connecting.");
            }

            _checksLabel.Text = string.Join(Environment.NewLine, lines);
        }

        private void Accept()
        {
            SmartcardCertificateStore.CertificateEntry certificate = SelectedCertificate;
            if (certificate == null)
                return;

            if (!int.TryParse(_portBox.Text.Trim(), out int port) || port <= 0 || port > 65535)
                port = 22;

            ConnectionInfo connection = new();
            DefaultConnectionInfo.Instance.SaveTo(connection);
            DefaultConnectionInheritance.Instance.SaveTo(connection.Inheritance);

            // everything this dialog sets is explicit, never inherited from a folder
            connection.Inheritance.Port = false;
            connection.Inheritance.Username = false;
            connection.Inheritance.Protocol = false;
            connection.Inheritance.SmartcardCertificate = false;
            connection.Inheritance.SshAgentForwarding = false;

            connection.Name = string.IsNullOrWhiteSpace(_nameBox.Text) ? _hostBox.Text.Trim() : _nameBox.Text.Trim();
            connection.Hostname = _hostBox.Text.Trim();
            connection.Port = port;
            connection.Username = _userBox.Text.Trim();
            connection.Protocol = ProtocolType.SSH2;
            connection.SmartcardCertificate = certificate.CapiIdentifier;
            connection.SshAgentForwarding = _agentForwardingBox.Checked;
            connection.Description = certificate.Subject;

            Connection = connection;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
