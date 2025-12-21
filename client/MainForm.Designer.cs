using System.Drawing;
using System.Windows.Forms;

namespace DeviceConfigurator
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        // Hlavní sekce
        private GroupBox _devicesGroupBox;
        private GroupBox _scanGroupBox;
        private GroupBox _configGroupBox;
        private GroupBox _editGroupBox;
        
        // Seznam zařízení
        private ListBox _devicesListBox;
        private Label _devicesLabel;
        
        // Skenování
        private Label _ipRangeLabel;
        private TextBox _ipRangeTextBox;
        private Button _scanRangeButton;
        private Button _scanButton;
        private Button _refreshButton;
        private Button _debugConsoleButton;
        
        // Aktuální konfigurace (read-only)
        private Label _ipLabel;
        private Label _hostnameLabel;
        private Label _portLabel;
        private Label _sshStatusLabel;
        private Label _rootLoginLabel;
        private Label _rootPasswordLabel;
        
        // Úprava konfigurace
        private Label _newHostnameLabel;
        private TextBox _hostnameTextBox;
        private Label _usernameLabel;
        private TextBox _usernameTextBox;
        private Label _passwordLabel;
        private TextBox _passwordTextBox;
        private Label _rootPasswordEditLabel;
        private TextBox _rootPasswordTextBox;
        private Label _staticIpLabel;
        private TextBox _staticIpTextBox;
        private Label _netmaskLabel;
        private TextBox _netmaskTextBox;
        private Label _gatewayLabel;
        private TextBox _gatewayTextBox;
        private CheckBox _sshEnabledCheckBox;
        private CheckBox _rootLoginCheckBox;
        private Button _saveButton;
        
        // Status
        private Label _statusLabel;
        private Panel _statusPanel;
        private ProgressBar _scanProgressBar;
        
        // Menu
        private MenuStrip _mainMenuStrip;
        private ToolStripMenuItem _configMenu;
        private ToolStripMenuItem _downloadConfigMenuItem;
        private ToolStripMenuItem _uploadConfigMenuItem;

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
            this.SuspendLayout();

            // Formulář
            this.Text = "Device Configurator - Raspberry Pi";
            this.Size = new Size(1000, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(900, 700);
            this.BackColor = SystemColors.Control;

            // ========== MENU STRIP ==========
            _mainMenuStrip = new MenuStrip();
            _mainMenuStrip.Location = new Point(0, 0);
            _mainMenuStrip.Size = new Size(1000, 24);
            _mainMenuStrip.Text = "mainMenuStrip";
            _mainMenuStrip.Dock = DockStyle.Top;
            
            // Config menu
            _configMenu = new ToolStripMenuItem();
            _configMenu.Text = "Config";
            _configMenu.Size = new Size(50, 20);
            
            // Download config
            _downloadConfigMenuItem = new ToolStripMenuItem();
            _downloadConfigMenuItem.Text = "Stáhnout konfiguraci";
            _downloadConfigMenuItem.Size = new Size(200, 22);
            
            // Upload config
            _uploadConfigMenuItem = new ToolStripMenuItem();
            _uploadConfigMenuItem.Text = "Nahrát konfiguraci";
            _uploadConfigMenuItem.Size = new Size(200, 22);
            
            _configMenu.DropDownItems.Add(_downloadConfigMenuItem);
            _configMenu.DropDownItems.Add(_uploadConfigMenuItem);
            _mainMenuStrip.Items.Add(_configMenu);
            
            this.MainMenuStrip = _mainMenuStrip;
            
            // ========== LEVÁ STRANA - SEZNAM ZAŘÍZENÍ A SKENOVÁNÍ ==========
            
            // GroupBox pro seznam zařízení
            _devicesGroupBox = new GroupBox
            {
                Text = "Nalezená zařízení",
                Location = new Point(12, 28), // Posunuto dolů o 24px pro menu
                Size = new Size(360, 320),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left
            };

            _devicesLabel = new Label
            {
                Text = "Vyberte zařízení ze seznamu:",
                Location = new Point(10, 20),
                Size = new Size(300, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _devicesGroupBox.Controls.Add(_devicesLabel);

            _devicesListBox = new ListBox
            {
                Location = new Point(10, 45),
                Size = new Size(340, 265),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Consolas", 9F)
            };
            _devicesListBox.SelectedIndexChanged += DevicesListBox_SelectedIndexChanged;
            _devicesGroupBox.Controls.Add(_devicesListBox);

            this.Controls.Add(_devicesGroupBox);

            // GroupBox pro skenování
            _scanGroupBox = new GroupBox
            {
                Text = "Skenování sítě",
                Location = new Point(12, 360),
                Size = new Size(360, 160),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            // IP rozsah
            _ipRangeLabel = new Label
            {
                Text = "IP rozsah:",
                Location = new Point(10, 25),
                Size = new Size(100, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _scanGroupBox.Controls.Add(_ipRangeLabel);

            _ipRangeTextBox = new TextBox
            {
                Location = new Point(10, 48),
                Size = new Size(240, 23),
                Text = "192.168.0.1-255",
                Font = new Font("Consolas", 9F)
            };
            _scanGroupBox.Controls.Add(_ipRangeTextBox);

            _scanRangeButton = new Button
            {
                Text = "Skenovat rozsah",
                Location = new Point(258, 46),
                Size = new Size(92, 25),
                UseVisualStyleBackColor = true
            };
            _scanRangeButton.Click += ScanRangeButton_Click;
            _scanGroupBox.Controls.Add(_scanRangeButton);

            // Tlačítka pro skenování
            _scanButton = new Button
            {
                Text = "Skenovat podle hostname",
                Location = new Point(10, 80),
                Size = new Size(170, 30),
                UseVisualStyleBackColor = true
            };
            _scanButton.Click += ScanButton_Click;
            _scanGroupBox.Controls.Add(_scanButton);

            _refreshButton = new Button
            {
                Text = "Obnovit konfiguraci",
                Location = new Point(188, 80),
                Size = new Size(162, 30),
                Enabled = false,
                UseVisualStyleBackColor = true
            };
            _refreshButton.Click += RefreshButton_Click;
            _scanGroupBox.Controls.Add(_refreshButton);

            // Debug tlačítko
            _debugConsoleButton = new Button
            {
                Text = "Debug Console",
                Location = new Point(10, 120),
                Size = new Size(340, 25),
                UseVisualStyleBackColor = true,
                BackColor = Color.LightGray
            };
            _debugConsoleButton.Click += DebugConsoleButton_Click;
            _scanGroupBox.Controls.Add(_debugConsoleButton);

            this.Controls.Add(_scanGroupBox);

            // ========== PRAVÁ STRANA - KONFIGURACE ==========

            // GroupBox pro aktuální konfiguraci (read-only)
            _configGroupBox = new GroupBox
            {
                Text = "Aktuální konfigurace",
                Location = new Point(380, 28), // Posunuto dolů o 24px pro menu
                Size = new Size(600, 200),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // IP adresa
            _ipLabel = new Label
            {
                Text = "IP adresa:",
                Location = new Point(15, 25),
                Size = new Size(120, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _configGroupBox.Controls.Add(_ipLabel);

            Label ipValueLabel = new Label
            {
                Text = "-",
                Location = new Point(140, 25),
                Size = new Size(440, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = SystemColors.ControlText
            };
            _configGroupBox.Controls.Add(ipValueLabel);
            _ipLabel.Tag = ipValueLabel; // Pro pozdější aktualizaci

            // Hostname
            _hostnameLabel = new Label
            {
                Text = "Hostname:",
                Location = new Point(15, 50),
                Size = new Size(120, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _configGroupBox.Controls.Add(_hostnameLabel);

            Label hostnameValueLabel = new Label
            {
                Text = "-",
                Location = new Point(140, 50),
                Size = new Size(440, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _configGroupBox.Controls.Add(hostnameValueLabel);
            _hostnameLabel.Tag = hostnameValueLabel;

            // Port
            _portLabel = new Label
            {
                Text = "Port:",
                Location = new Point(15, 75),
                Size = new Size(120, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _configGroupBox.Controls.Add(_portLabel);

            Label portValueLabel = new Label
            {
                Text = "-",
                Location = new Point(140, 75),
                Size = new Size(440, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _configGroupBox.Controls.Add(portValueLabel);
            _portLabel.Tag = portValueLabel;

            // SSH status
            _sshStatusLabel = new Label
            {
                Text = "SSH:",
                Location = new Point(15, 100),
                Size = new Size(120, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _configGroupBox.Controls.Add(_sshStatusLabel);

            Label sshValueLabel = new Label
            {
                Text = "-",
                Location = new Point(140, 100),
                Size = new Size(440, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _configGroupBox.Controls.Add(sshValueLabel);
            _sshStatusLabel.Tag = sshValueLabel;

            // Root login status
            _rootLoginLabel = new Label
            {
                Text = "Root login:",
                Location = new Point(15, 125),
                Size = new Size(120, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _configGroupBox.Controls.Add(_rootLoginLabel);

            Label rootLoginValueLabel = new Label
            {
                Text = "-",
                Location = new Point(140, 125),
                Size = new Size(440, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _configGroupBox.Controls.Add(rootLoginValueLabel);
            _rootLoginLabel.Tag = rootLoginValueLabel;

            // Root password status
            _rootPasswordLabel = new Label
            {
                Text = "Root heslo:",
                Location = new Point(15, 150),
                Size = new Size(120, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _configGroupBox.Controls.Add(_rootPasswordLabel);

            Label rootPasswordValueLabel = new Label
            {
                Text = "-",
                Location = new Point(140, 150),
                Size = new Size(440, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _configGroupBox.Controls.Add(rootPasswordValueLabel);
            _rootPasswordLabel.Tag = rootPasswordValueLabel;

            this.Controls.Add(_configGroupBox);

            // GroupBox pro úpravu konfigurace
            _editGroupBox = new GroupBox
            {
                Text = "Úprava konfigurace",
                Location = new Point(380, 236), // Posunuto dolů o 24px pro menu
                Size = new Size(600, 450),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // Nový hostname
            _newHostnameLabel = new Label
            {
                Text = "Nový hostname:",
                Location = new Point(15, 30),
                Size = new Size(200, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _editGroupBox.Controls.Add(_newHostnameLabel);

            _hostnameTextBox = new TextBox
            {
                Location = new Point(15, 55),
                Size = new Size(570, 23),
                Font = new Font("Segoe UI", 9F)
            };
            _editGroupBox.Controls.Add(_hostnameTextBox);

            // Uživatelské jméno
            _usernameLabel = new Label
            {
                Text = "Uživatelské jméno:",
                Location = new Point(15, 90),
                Size = new Size(200, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _editGroupBox.Controls.Add(_usernameLabel);

            _usernameTextBox = new TextBox
            {
                Location = new Point(15, 115),
                Size = new Size(570, 23),
                Font = new Font("Segoe UI", 9F)
            };
            _editGroupBox.Controls.Add(_usernameTextBox);

            // Heslo uživatele
            _passwordLabel = new Label
            {
                Text = "Heslo uživatele:",
                Location = new Point(15, 150),
                Size = new Size(200, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _editGroupBox.Controls.Add(_passwordLabel);

            _passwordTextBox = new TextBox
            {
                Location = new Point(15, 175),
                Size = new Size(570, 23),
                PasswordChar = '*',
                Font = new Font("Segoe UI", 9F)
            };
            _editGroupBox.Controls.Add(_passwordTextBox);

            // Root heslo
            _rootPasswordEditLabel = new Label
            {
                Text = "Root heslo:",
                Location = new Point(15, 210),
                Size = new Size(200, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _editGroupBox.Controls.Add(_rootPasswordEditLabel);

            _rootPasswordTextBox = new TextBox
            {
                Location = new Point(15, 235),
                Size = new Size(570, 23),
                PasswordChar = '*',
                Font = new Font("Segoe UI", 9F)
            };
            _editGroupBox.Controls.Add(_rootPasswordTextBox);

            // Statická IP adresa
            _staticIpLabel = new Label
            {
                Text = "Statická IPv4 adresa:",
                Location = new Point(15, 270),
                Size = new Size(200, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _editGroupBox.Controls.Add(_staticIpLabel);

            _staticIpTextBox = new TextBox
            {
                Location = new Point(15, 295),
                Size = new Size(280, 23),
                Font = new Font("Consolas", 9F)
            };
            _editGroupBox.Controls.Add(_staticIpTextBox);

            // Netmask
            _netmaskLabel = new Label
            {
                Text = "Maska sítě:",
                Location = new Point(305, 270),
                Size = new Size(100, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _editGroupBox.Controls.Add(_netmaskLabel);

            _netmaskTextBox = new TextBox
            {
                Location = new Point(305, 295),
                Size = new Size(130, 23),
                Font = new Font("Consolas", 9F),
                Text = "255.255.255.0"
            };
            _editGroupBox.Controls.Add(_netmaskTextBox);

            // Gateway
            _gatewayLabel = new Label
            {
                Text = "Brána (Gateway):",
                Location = new Point(445, 270),
                Size = new Size(140, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _editGroupBox.Controls.Add(_gatewayLabel);

            _gatewayTextBox = new TextBox
            {
                Location = new Point(445, 295),
                Size = new Size(140, 23),
                Font = new Font("Consolas", 9F)
            };
            _editGroupBox.Controls.Add(_gatewayTextBox);

            // SSH checkbox
            _sshEnabledCheckBox = new CheckBox
            {
                Text = "Povolit SSH",
                Location = new Point(15, 330),
                Size = new Size(200, 25),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _editGroupBox.Controls.Add(_sshEnabledCheckBox);

            // Root login checkbox
            _rootLoginCheckBox = new CheckBox
            {
                Text = "Povolit SSH root login",
                Location = new Point(15, 360),
                Size = new Size(200, 25),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _editGroupBox.Controls.Add(_rootLoginCheckBox);

            // Tlačítko pro uložení
            _saveButton = new Button
            {
                Text = "Uložit změny",
                Location = new Point(15, 400),
                Size = new Size(570, 40),
                Enabled = false,
                UseVisualStyleBackColor = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _saveButton.FlatAppearance.BorderSize = 0;
            _saveButton.Click += SaveButton_Click;
            _editGroupBox.Controls.Add(_saveButton);

            this.Controls.Add(_editGroupBox);

            // ========== STATUS BAR ==========
            
            _statusPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                BackColor = SystemColors.ControlDark,
                BorderStyle = BorderStyle.FixedSingle
            };

            _statusLabel = new Label
            {
                Text = "Připraveno. Zadejte IP rozsah nebo klikněte na 'Skenovat síť' pro začátek.",
                Location = new Point(10, 5),
                Size = new Size(980, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = SystemColors.ControlText,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom
            };
            _statusPanel.Controls.Add(_statusLabel);
            this.Controls.Add(_statusPanel);

            // ========== PROGRESS BAR (DOCKED AT BOTTOM, ABOVE STATUS BAR) ==========
            
            _scanProgressBar = new ProgressBar
            {
                Dock = DockStyle.Bottom,
                Height = 25,
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Visible = false
            };
            this.Controls.Add(_scanProgressBar);
            
            // Důležité: Progress bar musí být přidán po status panelu, aby byl nad ním
            this.Controls.SetChildIndex(_scanProgressBar, 0);
            this.Controls.SetChildIndex(_statusPanel, 1);
            
            this.Controls.Add(_mainMenuStrip);

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion
    }
}
