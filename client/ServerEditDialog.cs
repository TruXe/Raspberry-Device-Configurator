using System;
using System.Drawing;
using System.Windows.Forms;

namespace DeviceConfigurator
{
    /// <summary>
    /// Dialog pro přidání/editaci serveru.
    /// </summary>
    public partial class ServerEditDialog : Form
    {
        private TextBox _nameTextBox;
        private TextBox _ipTextBox;
        private TextBox _hostnameTextBox;
        private NumericUpDown _portNumericUpDown;
        private Button _okButton;
        private Button _cancelButton;
        private Label _nameLabel;
        private Label _ipLabel;
        private Label _hostnameLabel;
        private Label _portLabel;

        public string ServerName => _nameTextBox.Text.Trim();
        public string IPAddress => _ipTextBox.Text.Trim();
        public string Hostname => _hostnameTextBox.Text.Trim();
        public int Port => (int)_portNumericUpDown.Value;

        public ServerEditDialog(ServerInfo? existingServer = null)
        {
            InitializeComponent();
            UpdateLanguageUI();

            if (existingServer != null)
            {
                _nameTextBox.Text = existingServer.Name;
                _ipTextBox.Text = existingServer.IPAddress;
                _hostnameTextBox.Text = existingServer.Hostname;
                _portNumericUpDown.Value = existingServer.Port;
            }
        }

        private void UpdateLanguageUI()
        {
            this.Text = Localization.GetString("ServerEditDialogTitle");
            _nameLabel.Text = Localization.GetString("ServerNameLabel");
            _ipLabel.Text = Localization.GetString("ServerIPLabel");
            _hostnameLabel.Text = Localization.GetString("ServerHostnameLabel");
            _portLabel.Text = Localization.GetString("ServerPortLabel");
            _okButton.Text = Localization.GetString("OKButton");
            _cancelButton.Text = Localization.GetString("CancelButton");
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Formulář
            this.Text = "Přidat/Editovat Server"; // Bude aktualizováno v UpdateLanguageUI
            this.Size = new Size(400, 280);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            int labelY = 20;
            int textBoxY = 45;
            int spacing = 50;

            // Název serveru
            _nameLabel = new Label
            {
                Text = "Název:", // Bude aktualizováno v UpdateLanguageUI
                Location = new Point(15, labelY),
                Size = new Size(100, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            this.Controls.Add(_nameLabel);

            _nameTextBox = new TextBox
            {
                Location = new Point(15, textBoxY),
                Size = new Size(350, 23),
                Font = new Font("Segoe UI", 9F)
            };
            this.Controls.Add(_nameTextBox);

            // IP adresa
            _ipLabel = new Label
            {
                Text = "IP adresa:", // Bude aktualizováno v UpdateLanguageUI
                Location = new Point(15, labelY + spacing),
                Size = new Size(100, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            this.Controls.Add(_ipLabel);

            _ipTextBox = new TextBox
            {
                Location = new Point(15, textBoxY + spacing),
                Size = new Size(350, 23),
                Font = new Font("Consolas", 9F)
            };
            this.Controls.Add(_ipTextBox);

            // Hostname
            _hostnameLabel = new Label
            {
                Text = "Hostname:", // Bude aktualizováno v UpdateLanguageUI
                Location = new Point(15, labelY + spacing * 2),
                Size = new Size(100, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            this.Controls.Add(_hostnameLabel);

            _hostnameTextBox = new TextBox
            {
                Location = new Point(15, textBoxY + spacing * 2),
                Size = new Size(350, 23),
                Font = new Font("Segoe UI", 9F)
            };
            this.Controls.Add(_hostnameTextBox);

            // Port
            _portLabel = new Label
            {
                Text = "Port:", // Bude aktualizováno v UpdateLanguageUI
                Location = new Point(15, labelY + spacing * 3),
                Size = new Size(100, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            this.Controls.Add(_portLabel);

            _portNumericUpDown = new NumericUpDown
            {
                Location = new Point(15, textBoxY + spacing * 3),
                Size = new Size(100, 23),
                Minimum = 1,
                Maximum = 65535,
                Value = 7777,
                Font = new Font("Segoe UI", 9F)
            };
            this.Controls.Add(_portNumericUpDown);

            // Tlačítka
            _okButton = new Button
            {
                Text = "OK", // Bude aktualizováno v UpdateLanguageUI
                Location = new Point(220, 240),
                Size = new Size(70, 30),
                DialogResult = DialogResult.OK,
                UseVisualStyleBackColor = true
            };
            _okButton.Click += OkButton_Click;
            this.Controls.Add(_okButton);

            _cancelButton = new Button
            {
                Text = "Zrušit", // Bude aktualizováno v UpdateLanguageUI
                Location = new Point(295, 240),
                Size = new Size(70, 30),
                DialogResult = DialogResult.Cancel,
                UseVisualStyleBackColor = true
            };
            this.Controls.Add(_cancelButton);

            this.AcceptButton = _okButton;
            this.CancelButton = _cancelButton;

            this.ResumeLayout(false);
        }

        private void OkButton_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
            {
                MessageBox.Show(Localization.GetString("EnterServerName"), Localization.GetString("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            if (string.IsNullOrWhiteSpace(_ipTextBox.Text))
            {
                MessageBox.Show(Localization.GetString("EnterIPAddress"), Localization.GetString("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            // Základní validace IP adresy
            if (!System.Net.IPAddress.TryParse(_ipTextBox.Text.Trim(), out _))
            {
                MessageBox.Show(Localization.GetString("InvalidIPAddress"), Localization.GetString("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }
        }
    }
}

