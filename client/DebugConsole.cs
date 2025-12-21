using System;
using System.Drawing;
using System.Windows.Forms;

namespace DeviceConfigurator
{
    /// <summary>
    /// Debug konzole pro zobrazení debug zpráv.
    /// </summary>
    public partial class DebugConsole : Form
    {
        private TextBox _debugTextBox;
        private Button _clearButton;
        private Button _closeButton;
        private CheckBox _autoScrollCheckBox;

        public DebugConsole()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Formulář
            this.Text = "Debug Console";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(400, 300);

            // TextBox pro debug zprávy
            _debugTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9),
                Location = new Point(12, 12),
                Size = new Size(776, 520),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                WordWrap = false
            };
            this.Controls.Add(_debugTextBox);

            // CheckBox pro auto-scroll
            _autoScrollCheckBox = new CheckBox
            {
                Text = "Automatické scrollování",
                Checked = true,
                Location = new Point(12, 540),
                Size = new Size(200, 20),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            this.Controls.Add(_autoScrollCheckBox);

            // Tlačítko pro vymazání
            _clearButton = new Button
            {
                Text = "Vymazat",
                Location = new Point(600, 540),
                Size = new Size(90, 25),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            _clearButton.Click += ClearButton_Click;
            this.Controls.Add(_clearButton);

            // Tlačítko pro zavření
            _closeButton = new Button
            {
                Text = "Zavřít",
                Location = new Point(696, 540),
                Size = new Size(90, 25),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            _closeButton.Click += CloseButton_Click;
            this.Controls.Add(_closeButton);

            this.ResumeLayout(false);
        }

        /// <summary>
        /// Přidá zprávu do debug konzole.
        /// </summary>
        public void AddMessage(string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(AddMessage), message);
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string logMessage = $"[{timestamp}] {message}\r\n";
            
            _debugTextBox.AppendText(logMessage);

            // Auto-scroll pokud je zapnutý
            if (_autoScrollCheckBox.Checked)
            {
                _debugTextBox.SelectionStart = _debugTextBox.Text.Length;
                _debugTextBox.ScrollToCaret();
            }
        }

        private void ClearButton_Click(object? sender, EventArgs e)
        {
            _debugTextBox.Clear();
        }

        private void CloseButton_Click(object? sender, EventArgs e)
        {
            this.Hide();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Místo zavření pouze schováme okno
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
            }
            base.OnFormClosing(e);
        }
    }
}

