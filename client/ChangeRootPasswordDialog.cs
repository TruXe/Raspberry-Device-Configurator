using System;
using System.Drawing;
using System.Windows.Forms;

namespace DeviceConfigurator
{
    /// <summary>
    /// Dialog pro změnu root hesla přes uživatele.
    /// </summary>
    public partial class ChangeRootPasswordDialog : Form
    {
        private TextBox _usernameTextBox;
        private TextBox _userPasswordTextBox;
        private TextBox _newRootPasswordTextBox;
        private TextBox _confirmRootPasswordTextBox;
        private Button _okButton;
        private Button _cancelButton;
        private Label _usernameLabel;
        private Label _userPasswordLabel;
        private Label _newRootPasswordLabel;
        private Label _confirmRootPasswordLabel;

        public string Username => _usernameTextBox.Text.Trim();
        public string UserPassword => _userPasswordTextBox.Text;
        public string NewRootPassword => _newRootPasswordTextBox.Text;

        public ChangeRootPasswordDialog()
        {
            InitializeComponent();
            UpdateLanguageUI();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form
            this.Text = Localization.GetString("ChangeRootPasswordTitle");
            this.Size = new Size(400, 280);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = SystemColors.Control;

            // Username label
            _usernameLabel = new Label
            {
                Text = Localization.GetString("ChangeRootPasswordUsernameLabel"),
                Location = new Point(10, 15),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 9F)
            };
            this.Controls.Add(_usernameLabel);

            // Username textbox
            _usernameTextBox = new TextBox
            {
                Location = new Point(170, 12),
                Size = new Size(210, 23),
                Font = new Font("Consolas", 9F)
            };
            this.Controls.Add(_usernameTextBox);

            // User password label
            _userPasswordLabel = new Label
            {
                Text = Localization.GetString("ChangeRootPasswordUserPasswordLabel"),
                Location = new Point(10, 50),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 9F)
            };
            this.Controls.Add(_userPasswordLabel);

            // User password textbox
            _userPasswordTextBox = new TextBox
            {
                Location = new Point(170, 47),
                Size = new Size(210, 23),
                Font = new Font("Consolas", 9F),
                UseSystemPasswordChar = true
            };
            this.Controls.Add(_userPasswordTextBox);

            // New root password label
            _newRootPasswordLabel = new Label
            {
                Text = Localization.GetString("ChangeRootPasswordNewPasswordLabel"),
                Location = new Point(10, 85),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 9F)
            };
            this.Controls.Add(_newRootPasswordLabel);

            // New root password textbox
            _newRootPasswordTextBox = new TextBox
            {
                Location = new Point(170, 82),
                Size = new Size(210, 23),
                Font = new Font("Consolas", 9F),
                UseSystemPasswordChar = true
            };
            this.Controls.Add(_newRootPasswordTextBox);

            // Confirm root password label
            _confirmRootPasswordLabel = new Label
            {
                Text = Localization.GetString("ChangeRootPasswordConfirmLabel"),
                Location = new Point(10, 120),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 9F)
            };
            this.Controls.Add(_confirmRootPasswordLabel);

            // Confirm root password textbox
            _confirmRootPasswordTextBox = new TextBox
            {
                Location = new Point(170, 117),
                Size = new Size(210, 23),
                Font = new Font("Consolas", 9F),
                UseSystemPasswordChar = true
            };
            this.Controls.Add(_confirmRootPasswordTextBox);

            // OK button
            _okButton = new Button
            {
                Text = Localization.GetString("OKButton"),
                Location = new Point(220, 160),
                Size = new Size(75, 30),
                DialogResult = DialogResult.OK,
                UseVisualStyleBackColor = true
            };
            _okButton.Click += OkButton_Click;
            this.Controls.Add(_okButton);
            this.AcceptButton = _okButton;

            // Cancel button
            _cancelButton = new Button
            {
                Text = Localization.GetString("CancelButton"),
                Location = new Point(305, 160),
                Size = new Size(75, 30),
                DialogResult = DialogResult.Cancel,
                UseVisualStyleBackColor = true
            };
            this.Controls.Add(_cancelButton);
            this.CancelButton = _cancelButton;

            this.ResumeLayout(false);
        }

        private void UpdateLanguageUI()
        {
            this.Text = Localization.GetString("ChangeRootPasswordTitle");
            _usernameLabel.Text = Localization.GetString("ChangeRootPasswordUsernameLabel");
            _userPasswordLabel.Text = Localization.GetString("ChangeRootPasswordUserPasswordLabel");
            _newRootPasswordLabel.Text = Localization.GetString("ChangeRootPasswordNewPasswordLabel");
            _confirmRootPasswordLabel.Text = Localization.GetString("ChangeRootPasswordConfirmLabel");
            _okButton.Text = Localization.GetString("OKButton");
            _cancelButton.Text = Localization.GetString("CancelButton");
        }

        private void OkButton_Click(object? sender, EventArgs e)
        {
            // Validace
            if (string.IsNullOrWhiteSpace(_usernameTextBox.Text))
            {
                MessageBox.Show(
                    Localization.GetString("EnterUsername"),
                    Localization.GetString("Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            if (string.IsNullOrWhiteSpace(_userPasswordTextBox.Text))
            {
                MessageBox.Show(
                    Localization.GetString("EnterUserPassword"),
                    Localization.GetString("Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            if (string.IsNullOrWhiteSpace(_newRootPasswordTextBox.Text))
            {
                MessageBox.Show(
                    Localization.GetString("EnterNewRootPassword"),
                    Localization.GetString("Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            if (_newRootPasswordTextBox.Text != _confirmRootPasswordTextBox.Text)
            {
                MessageBox.Show(
                    Localization.GetString("PasswordsDoNotMatch"),
                    Localization.GetString("Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }
        }
    }
}

