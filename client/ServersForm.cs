using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeviceConfigurator
{
    /// <summary>
    /// Formulář pro správu Raspberry Pi serverů.
    /// </summary>
    public partial class ServersForm : Form
    {
        private TreeView _serversTreeView;
        private Button _addButton;
        private Button _removeButton;
        private Button _editButton;
        private Button _refreshButton;
        private Label _statusLabel;
        private ImageList _statusImageList;
        private System.Threading.CancellationTokenSource? _refreshCancellation;
        private string ServersFilePath => AppSettings.GetServersFilePath();
        private List<ServerInfo> _servers = new List<ServerInfo>();

        public ServersForm()
        {
            InitializeComponent();
            LoadServers();
            UpdateTreeView();
            UpdateLanguageUI();
            StartAutoRefresh();
            // Automaticky načíst status serverů po otevření formuláře
            _ = RefreshAllServersAsync();
        }

        private void UpdateLanguageUI()
        {
            this.Text = Localization.GetString("ServersFormTitle");
            _addButton.Text = Localization.GetString("AddButton");
            _editButton.Text = Localization.GetString("EditButton");
            _removeButton.Text = Localization.GetString("RemoveButton");
            _refreshButton.Text = Localization.GetString("RefreshServersButton");
            _statusLabel.Text = Localization.GetString("ReadyStatusServers");
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Formulář
            this.Text = "Raspberry Servery"; // Bude aktualizováno v UpdateLanguageUI
            this.Size = new Size(600, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(500, 400);

            // ImageList pro status indikátory
            _statusImageList = new ImageList
            {
                ImageSize = new Size(16, 16),
                ColorDepth = ColorDepth.Depth32Bit
            };
            
            // Vytvoření zelené tečky (online)
            Bitmap greenDot = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(greenDot))
            {
                g.Clear(Color.Transparent);
                g.FillEllipse(new SolidBrush(Color.Green), 2, 2, 12, 12);
            }
            _statusImageList.Images.Add("online", greenDot);

            // Vytvoření červené tečky (offline)
            Bitmap redDot = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(redDot))
            {
                g.Clear(Color.Transparent);
                g.FillEllipse(new SolidBrush(Color.Red), 2, 2, 12, 12);
            }
            _statusImageList.Images.Add("offline", redDot);

            // Context menu pro TreeView
            ContextMenuStrip serversContextMenu = new ContextMenuStrip();
            ToolStripMenuItem downloadConfigMenuItem = new ToolStripMenuItem
            {
                Text = Localization.GetString("DownloadConfigMenu")
            };
            downloadConfigMenuItem.Click += ServersTreeView_DownloadConfig;
            serversContextMenu.Items.Add(downloadConfigMenuItem);

            ToolStripMenuItem uploadConfigMenuItem = new ToolStripMenuItem
            {
                Text = Localization.GetString("UploadConfigMenu")
            };
            uploadConfigMenuItem.Click += ServersTreeView_UploadConfig;
            serversContextMenu.Items.Add(uploadConfigMenuItem);

            ToolStripMenuItem changeRootPasswordMenuItem = new ToolStripMenuItem
            {
                Text = Localization.GetString("ChangeRootPasswordMenu")
            };
            changeRootPasswordMenuItem.Click += ServersTreeView_ChangeRootPassword;
            serversContextMenu.Items.Add(changeRootPasswordMenuItem);

            // TreeView
            _serversTreeView = new TreeView
            {
                Location = new Point(12, 12),
                Size = new Size(560, 400),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ImageList = _statusImageList,
                ShowLines = true,
                ShowRootLines = true,
                ShowPlusMinus = false,
                ContextMenuStrip = serversContextMenu
            };
            _serversTreeView.AfterSelect += ServersTreeView_AfterSelect;
            this.Controls.Add(_serversTreeView);

            // Tlačítka
            int buttonY = 420;
            int buttonWidth = 120;
            int buttonHeight = 30;
            int buttonSpacing = 10;

            _addButton = new Button
            {
                Text = "Přidat", // Bude aktualizováno v UpdateLanguageUI
                Location = new Point(12, buttonY),
                Size = new Size(buttonWidth, buttonHeight),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                UseVisualStyleBackColor = true
            };
            _addButton.Click += AddButton_Click;
            this.Controls.Add(_addButton);

            _editButton = new Button
            {
                Text = "Editovat", // Bude aktualizováno v UpdateLanguageUI
                Location = new Point(12 + buttonWidth + buttonSpacing, buttonY),
                Size = new Size(buttonWidth, buttonHeight),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                UseVisualStyleBackColor = true,
                Enabled = false
            };
            _editButton.Click += EditButton_Click;
            this.Controls.Add(_editButton);

            _removeButton = new Button
            {
                Text = "Odebrat", // Bude aktualizováno v UpdateLanguageUI
                Location = new Point(12 + (buttonWidth + buttonSpacing) * 2, buttonY),
                Size = new Size(buttonWidth, buttonHeight),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                UseVisualStyleBackColor = true,
                Enabled = false
            };
            _removeButton.Click += RemoveButton_Click;
            this.Controls.Add(_removeButton);

            _refreshButton = new Button
            {
                Text = "Aktualizovat", // Bude aktualizováno v UpdateLanguageUI
                Location = new Point(12 + (buttonWidth + buttonSpacing) * 3, buttonY),
                Size = new Size(buttonWidth, buttonHeight),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                UseVisualStyleBackColor = true
            };
            _refreshButton.Click += RefreshButton_Click;
            this.Controls.Add(_refreshButton);

            // Status label
            _statusLabel = new Label
            {
                Text = "Připraveno", // Bude aktualizováno v UpdateLanguageUI
                Location = new Point(12, buttonY + buttonHeight + 5),
                Size = new Size(560, 20),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            this.Controls.Add(_statusLabel);

            this.ResumeLayout(false);
        }

        private void ServersTreeView_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            bool hasSelection = _serversTreeView.SelectedNode != null;
            _editButton.Enabled = hasSelection;
            _removeButton.Enabled = hasSelection;
        }

        private void AddButton_Click(object? sender, EventArgs e)
        {
            using (var dialog = new ServerEditDialog())
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    var server = new ServerInfo
                    {
                        Name = dialog.ServerName,
                        IPAddress = dialog.IPAddress,
                        Hostname = dialog.Hostname,
                        Port = dialog.Port
                    };
                    _servers.Add(server);
                    SaveServers();
                    UpdateTreeView();
                    _ = CheckServerStatusAsync(server);
                }
            }
        }

        private void EditButton_Click(object? sender, EventArgs e)
        {
            if (_serversTreeView.SelectedNode?.Tag is ServerInfo server)
            {
                using (var dialog = new ServerEditDialog(server))
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        server.Name = dialog.ServerName;
                        server.IPAddress = dialog.IPAddress;
                        server.Hostname = dialog.Hostname;
                        server.Port = dialog.Port;
                        SaveServers();
                        UpdateTreeView();
                        _ = CheckServerStatusAsync(server);
                    }
                }
            }
        }

        private void RemoveButton_Click(object? sender, EventArgs e)
        {
            if (_serversTreeView.SelectedNode?.Tag is ServerInfo server)
            {
                var result = MessageBox.Show(
                    $"Opravdu chcete odebrat server '{server.Name}'?",
                    "Potvrzení",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    _servers.Remove(server);
                    SaveServers();
                    UpdateTreeView();
                }
            }
        }

        private async void RefreshButton_Click(object? sender, EventArgs e)
        {
            await RefreshAllServersAsync();
        }

        private void UpdateTreeView()
        {
            _serversTreeView.Nodes.Clear();

            foreach (var server in _servers)
            {
                var node = new TreeNode
                {
                    Text = $"{server.Name} ({server.IPAddress})",
                    Tag = server,
                    ImageIndex = server.IsOnline ? 0 : 1,
                    SelectedImageIndex = server.IsOnline ? 0 : 1
                };
                _serversTreeView.Nodes.Add(node);
            }

            _serversTreeView.ExpandAll();
        }

        private async Task CheckServerStatusAsync(ServerInfo server)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(server.IPAddress, 2000);
                    server.IsOnline = reply.Status == IPStatus.Success;
                    server.LastChecked = DateTime.Now;
                }
            }
            catch
            {
                server.IsOnline = false;
                server.LastChecked = DateTime.Now;
            }

            this.Invoke((MethodInvoker)delegate
            {
                UpdateTreeView();
            });
        }

        private async Task RefreshAllServersAsync()
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)async delegate
                {
                    await RefreshAllServersAsync();
                });
                return;
            }

            _refreshButton.Enabled = false;
            _statusLabel.Text = "Kontroluji status serverů...";
            _statusLabel.ForeColor = Color.Blue;

            var tasks = _servers.Select(server => CheckServerStatusAsync(server)).ToArray();
            await Task.WhenAll(tasks);

            _statusLabel.Text = $"Aktualizace dokončena. Online: {_servers.Count(s => s.IsOnline)}, Offline: {_servers.Count(s => !s.IsOnline)}";
            _statusLabel.ForeColor = Color.Green;
            _refreshButton.Enabled = true;
        }

        private void StartAutoRefresh()
        {
            _refreshCancellation = new CancellationTokenSource();
            var cancellationToken = _refreshCancellation.Token;

            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(15000, cancellationToken); // Každých 15 sekund
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await RefreshAllServersAsync();
                    }
                }
            }, cancellationToken);
        }

        private void LoadServers()
        {
            try
            {
                if (File.Exists(ServersFilePath))
                {
                    var json = File.ReadAllText(ServersFilePath);
                    _servers = JsonSerializer.Deserialize<List<ServerInfo>>(json) ?? new List<ServerInfo>();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"LoadServers: Chyba při načítání serverů: {ex.Message}");
                _servers = new List<ServerInfo>();
            }
        }

        private void SaveServers()
        {
            try
            {
                var json = JsonSerializer.Serialize(_servers, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ServersFilePath, json);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"SaveServers: Chyba při ukládání serverů: {ex.Message}");
                MessageBox.Show($"Chyba při ukládání serverů: {ex.Message}", "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _refreshCancellation?.Cancel();
            _refreshCancellation?.Dispose();
            base.OnFormClosing(e);
        }

        private async void ServersTreeView_DownloadConfig(object? sender, EventArgs e)
        {
            if (_serversTreeView.SelectedNode?.Tag is not ServerInfo server)
            {
                MessageBox.Show(
                    Localization.GetString("SelectDeviceFirst"),
                    Localization.GetString("Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var client = new ConfigClient();
                var config = await client.GetConfigAsync(server.IPAddress);

                if (config == null)
                {
                    MessageBox.Show(
                        Localization.GetString("ErrorLoadingConfig"),
                        Localization.GetString("Error"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                using (var saveDialog = new SaveFileDialog())
                {
                    saveDialog.Filter = "JSON soubory (*.json)|*.json|Všechny soubory (*.*)|*.*";
                    saveDialog.FileName = $"config_{server.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                    saveDialog.DefaultExt = "json";

                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        await System.IO.File.WriteAllTextAsync(saveDialog.FileName, json);
                        MessageBox.Show(
                            Localization.GetString("ConfigSaved"),
                            Localization.GetString("Success"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(Localization.GetString("ErrorSavingConfig"), ex.Message),
                    Localization.GetString("Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private async void ServersTreeView_UploadConfig(object? sender, EventArgs e)
        {
            if (_serversTreeView.SelectedNode?.Tag is not ServerInfo server)
            {
                MessageBox.Show(
                    Localization.GetString("SelectDeviceFirst"),
                    Localization.GetString("Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            using (var openDialog = new OpenFileDialog())
            {
                openDialog.Filter = "JSON soubory (*.json)|*.json|Všechny soubory (*.*)|*.*";
                openDialog.DefaultExt = "json";

                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var json = await System.IO.File.ReadAllTextAsync(openDialog.FileName);
                        var config = System.Text.Json.JsonSerializer.Deserialize<DeviceConfig>(json);

                        if (config == null)
                        {
                            MessageBox.Show(
                                "Nepodařilo se načíst konfiguraci ze souboru.",
                                Localization.GetString("Error"),
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                            return;
                        }

                        var client = new ConfigClient();
                        bool success = true;
                        string errorMessage = "";

                        // Aplikujeme konfiguraci
                        if (!string.IsNullOrWhiteSpace(config.Hostname))
                        {
                            var response = await client.SetHostnameAsync(server.IPAddress, config.Hostname);
                            if (response.Status != "ok")
                            {
                                success = false;
                                errorMessage += $"Hostname: {response.Error}; ";
                            }
                        }

                        if (config.SshEnabled.HasValue)
                        {
                            var response = await client.SetSshEnabledAsync(server.IPAddress, config.SshEnabled.Value);
                            if (response.Status != "ok")
                            {
                                success = false;
                                errorMessage += $"SSH: {response.Error}; ";
                            }
                        }

                        if (config.RootLoginEnabled.HasValue)
                        {
                            var response = await client.SetRootLoginAsync(server.IPAddress, config.RootLoginEnabled.Value);
                            if (response.Status != "ok")
                            {
                                success = false;
                                errorMessage += $"Root login: {response.Error}; ";
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(config.StaticIP))
                        {
                            var response = await client.SetStaticIpAsync(
                                server.IPAddress,
                                config.StaticIP,
                                config.Netmask ?? "255.255.255.0",
                                config.Gateway
                            );
                            if (response.Status != "ok")
                            {
                                success = false;
                                errorMessage += $"Static IP: {response.Error}; ";
                            }
                        }

                        if (success)
                        {
                            MessageBox.Show(
                                Localization.GetString("ConfigLoadedFromFile"),
                                Localization.GetString("Success"),
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show(
                                $"Chyby při nahrávání konfigurace: {errorMessage}",
                                Localization.GetString("Error"),
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            $"Chyba při nahrávání konfigurace: {ex.Message}",
                            Localization.GetString("Error"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
            }
        }

        private async void ServersTreeView_ChangeRootPassword(object? sender, EventArgs e)
        {
            if (_serversTreeView.SelectedNode?.Tag is not ServerInfo server)
            {
                MessageBox.Show(
                    Localization.GetString("SelectDeviceFirst"),
                    Localization.GetString("Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            using (var dialog = new ChangeRootPasswordDialog())
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        _statusLabel.Text = Localization.GetString("ChangingRootPassword");
                        _statusLabel.ForeColor = Color.Blue;

                        var client = new ConfigClient();
                        var response = await client.SetRootPasswordAsync(server.IPAddress, dialog.NewRootPassword);

                        if (response.Status == "ok")
                        {
                            _statusLabel.Text = Localization.GetString("RootPasswordChanged");
                            _statusLabel.ForeColor = Color.Green;
                            MessageBox.Show(
                                Localization.GetString("RootPasswordChanged"),
                                Localization.GetString("Success"),
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                        else
                        {
                            _statusLabel.Text = string.Format(Localization.GetString("ErrorChangingRootPassword"), response.Error);
                            _statusLabel.ForeColor = Color.Red;
                            MessageBox.Show(
                                string.Format(Localization.GetString("ErrorChangingRootPassword"), response.Error),
                                Localization.GetString("Error"),
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        _statusLabel.Text = string.Format(Localization.GetString("ErrorChangingRootPassword"), ex.Message);
                        _statusLabel.ForeColor = Color.Red;
                        MessageBox.Show(
                            string.Format(Localization.GetString("ErrorChangingRootPassword"), ex.Message),
                            Localization.GetString("Error"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
            }
        }
    }
}

