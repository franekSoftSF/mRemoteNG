namespace ExternalConnectors.BT
{
    partial class BTConnectionForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            tableLayoutPanel1 = new TableLayoutPanel();
            label1 = new Label();
            tbServerURL = new TextBox();
            label2 = new Label();
            tbAPIKey = new TextBox();
            label3 = new Label();
            tbRunAs = new TextBox();
            label4 = new Label();
            tbRunAsPassword = new TextBox();
            label5 = new Label();
            tbChallenge = new TextBox();
            tableLayoutPanel2 = new TableLayoutPanel();
            btnOK = new Button();
            btnCancel = new Button();
            lblHint = new Label();
            tableLayoutPanel1.SuspendLayout();
            tableLayoutPanel2.SuspendLayout();
            SuspendLayout();
            //
            // tableLayoutPanel1
            //
            tableLayoutPanel1.ColumnCount = 2;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68F));
            tableLayoutPanel1.Controls.Add(label1, 0, 0);
            tableLayoutPanel1.Controls.Add(tbServerURL, 1, 0);
            tableLayoutPanel1.Controls.Add(label2, 0, 1);
            tableLayoutPanel1.Controls.Add(tbAPIKey, 1, 1);
            tableLayoutPanel1.Controls.Add(label3, 0, 2);
            tableLayoutPanel1.Controls.Add(tbRunAs, 1, 2);
            tableLayoutPanel1.Controls.Add(label4, 0, 3);
            tableLayoutPanel1.Controls.Add(tbRunAsPassword, 1, 3);
            tableLayoutPanel1.Controls.Add(label5, 0, 4);
            tableLayoutPanel1.Controls.Add(tbChallenge, 1, 4);
            tableLayoutPanel1.Dock = DockStyle.Top;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Margin = new Padding(5);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 5;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            tableLayoutPanel1.Size = new Size(914, 212);
            tableLayoutPanel1.TabIndex = 0;
            //
            // label1
            //
            label1.AutoSize = true;
            label1.Dock = DockStyle.Fill;
            label1.Location = new Point(5, 0);
            label1.Margin = new Padding(5, 0, 5, 0);
            label1.Name = "label1";
            label1.Size = new Size(282, 42);
            label1.TabIndex = 10;
            label1.Text = "Password Safe URL";
            label1.TextAlign = ContentAlignment.MiddleLeft;
            //
            // tbServerURL
            //
            tbServerURL.Dock = DockStyle.Fill;
            tbServerURL.Location = new Point(297, 5);
            tbServerURL.Margin = new Padding(5);
            tbServerURL.Name = "tbServerURL";
            tbServerURL.Size = new Size(612, 27);
            tbServerURL.TabIndex = 0;
            //
            // label2
            //
            label2.AutoSize = true;
            label2.Dock = DockStyle.Fill;
            label2.Location = new Point(5, 42);
            label2.Margin = new Padding(5, 0, 5, 0);
            label2.Name = "label2";
            label2.Size = new Size(282, 42);
            label2.TabIndex = 11;
            label2.Text = "API Key";
            label2.TextAlign = ContentAlignment.MiddleLeft;
            //
            // tbAPIKey
            //
            tbAPIKey.Dock = DockStyle.Fill;
            tbAPIKey.Location = new Point(297, 47);
            tbAPIKey.Margin = new Padding(5);
            tbAPIKey.Name = "tbAPIKey";
            tbAPIKey.Size = new Size(612, 27);
            tbAPIKey.TabIndex = 1;
            tbAPIKey.UseSystemPasswordChar = true;
            //
            // label3
            //
            label3.AutoSize = true;
            label3.Dock = DockStyle.Fill;
            label3.Location = new Point(5, 84);
            label3.Margin = new Padding(5, 0, 5, 0);
            label3.Name = "label3";
            label3.Size = new Size(282, 42);
            label3.TabIndex = 12;
            label3.Text = "Run As user";
            label3.TextAlign = ContentAlignment.MiddleLeft;
            //
            // tbRunAs
            //
            tbRunAs.Dock = DockStyle.Fill;
            tbRunAs.Location = new Point(297, 89);
            tbRunAs.Margin = new Padding(5);
            tbRunAs.Name = "tbRunAs";
            tbRunAs.Size = new Size(612, 27);
            tbRunAs.TabIndex = 2;
            //
            // label4
            //
            label4.AutoSize = true;
            label4.Dock = DockStyle.Fill;
            label4.Location = new Point(5, 126);
            label4.Margin = new Padding(5, 0, 5, 0);
            label4.Name = "label4";
            label4.Size = new Size(282, 42);
            label4.TabIndex = 13;
            label4.Text = "Run As password (optional)";
            label4.TextAlign = ContentAlignment.MiddleLeft;
            //
            // tbRunAsPassword
            //
            tbRunAsPassword.Dock = DockStyle.Fill;
            tbRunAsPassword.Location = new Point(297, 131);
            tbRunAsPassword.Margin = new Padding(5);
            tbRunAsPassword.Name = "tbRunAsPassword";
            tbRunAsPassword.Size = new Size(612, 27);
            tbRunAsPassword.TabIndex = 3;
            tbRunAsPassword.UseSystemPasswordChar = true;
            //
            // label5
            //
            label5.AutoSize = true;
            label5.Dock = DockStyle.Fill;
            label5.Location = new Point(5, 168);
            label5.Margin = new Padding(5, 0, 5, 0);
            label5.Name = "label5";
            label5.Size = new Size(282, 44);
            label5.TabIndex = 14;
            label5.Text = "2FA challenge (optional)";
            label5.TextAlign = ContentAlignment.MiddleLeft;
            //
            // tbChallenge
            //
            tbChallenge.Dock = DockStyle.Fill;
            tbChallenge.Location = new Point(297, 173);
            tbChallenge.Margin = new Padding(5);
            tbChallenge.Name = "tbChallenge";
            tbChallenge.Size = new Size(612, 27);
            tbChallenge.TabIndex = 4;
            //
            // tableLayoutPanel2
            //
            tableLayoutPanel2.ColumnCount = 5;
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 106F));
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 26F));
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 107F));
            tableLayoutPanel2.Controls.Add(btnOK, 1, 0);
            tableLayoutPanel2.Controls.Add(btnCancel, 3, 0);
            tableLayoutPanel2.Dock = DockStyle.Bottom;
            tableLayoutPanel2.Location = new Point(0, 300);
            tableLayoutPanel2.Margin = new Padding(5);
            tableLayoutPanel2.Name = "tableLayoutPanel2";
            tableLayoutPanel2.RowCount = 1;
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel2.Size = new Size(914, 67);
            tableLayoutPanel2.TabIndex = 1;
            //
            // btnOK
            //
            btnOK.Anchor = AnchorStyles.Right;
            btnOK.DialogResult = DialogResult.OK;
            btnOK.Location = new Point(337, 16);
            btnOK.Margin = new Padding(5);
            btnOK.Name = "btnOK";
            btnOK.Size = new Size(101, 35);
            btnOK.TabIndex = 5;
            btnOK.Text = "OK";
            btnOK.UseVisualStyleBackColor = true;
            //
            // btnCancel
            //
            btnCancel.Anchor = AnchorStyles.Left;
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Location = new Point(474, 16);
            btnCancel.Margin = new Padding(5);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(101, 35);
            btnCancel.TabIndex = 6;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            //
            // lblHint
            //
            lblHint.AutoSize = true;
            lblHint.Dock = DockStyle.Fill;
            lblHint.Location = new Point(0, 212);
            lblHint.Margin = new Padding(5, 0, 5, 0);
            lblHint.Name = "lblHint";
            lblHint.Size = new Size(914, 88);
            lblHint.TabIndex = 2;
            lblHint.Text = "URL is the base URL, like https://ps.domain.local/BeyondTrust\r\n" +
                "Run As is a BeyondInsight user allowed to use the API key.\r\n" +
                "The password is only needed if the API registration requires it.\r\n" +
                "Only accounts with \"Enable for API Access\" can be listed.";
            //
            // BTConnectionForm
            //
            AcceptButton = btnOK;
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = btnCancel;
            ClientSize = new Size(914, 367);
            Controls.Add(lblHint);
            Controls.Add(tableLayoutPanel2);
            Controls.Add(tableLayoutPanel1);
            Margin = new Padding(5);
            Name = "BTConnectionForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "BeyondTrust Password Safe API Login Data";
            Activated += BTConnectionForm_Activated;
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            tableLayoutPanel2.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label lblHint;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;

        public System.Windows.Forms.TextBox tbServerURL;
        public System.Windows.Forms.TextBox tbAPIKey;
        public System.Windows.Forms.TextBox tbRunAs;
        public System.Windows.Forms.TextBox tbRunAsPassword;
        public System.Windows.Forms.TextBox tbChallenge;
    }
}
