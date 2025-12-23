using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeviceConfigurator
{
    /// <summary>
    /// Hlavní formulář aplikace Device Configurator.
    /// </summary>
    public partial class MainForm : Form
    {
        private NetworkScanner _scanner;
        private ConfigClient _client;
        private NetworkDevice? _selectedDevice;
        private DeviceConfig? _currentConfig;
        private System.Threading.CancellationTokenSource? _ipPollingCancellation;
        private System.Threading.CancellationTokenSource? _statusCheckCancellation;
        private Dictionary<string, TreeNode> _deviceNodes = new Dictionary<string, TreeNode>();

        public MainForm()
        {
            InitializeComponent();
            _scanner = new NetworkScanner();
            _client = new ConfigClient();
            
            // Načteme nastavení jazyka a aktualizujeme UI
            Localization.LoadLanguage();
            UpdateLanguageUI();
            UpdateLanguageMenuCheckmarks();
            
            // Zobrazit debug konzoli při startu (volitelné)
            // DebugLogger.ShowConsole();
        }

        private async void ScanButton_Click(object? sender, EventArgs e)
        {
            string hostname = _hostnameScanTextBox.Text.Trim();
            DebugLogger.Log($"ScanButton_Click: Začátek skenování podle hostname: {hostname}");
            _scanButton.Enabled = false;
            _scanRangeButton.Enabled = false;
            
            if (string.IsNullOrWhiteSpace(hostname))
            {
                _statusLabel.Text = Localization.GetString("EnterHostname");
                _scanButton.Enabled = true;
                _scanRangeButton.Enabled = true;
                return;
            }
            
            _statusLabel.Text = $"Skenování sítě podle hostname '{hostname}'...";
            _devicesTreeView.Nodes.Clear();
            _deviceNodes.Clear();
            _selectedDevice = null;
            _currentConfig = null;
            UpdateConfigDisplay();

            try
            {
                var devices = await _scanner.ScanNetworkAsync(hostname);

                if (devices.Count == 0)
                {
                    _statusLabel.Text = "Nebyla nalezena žádná zařízení. Zkuste znovu nebo použijte skenování IP rozsahu.";
                }
                else
                {
                    // Před zobrazením zkontrolujeme port 7777 pro každé zařízení
                    await CheckDevicesPortsAsync(devices);
                    UpdateDevicesTreeView(devices);
                    _statusLabel.Text = $"Nalezeno {devices.Count} zařízení.";
                    StartStatusCheck();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"ScanButton_Click: Výjimka: {ex.GetType().Name}: {ex.Message}");
                _statusLabel.Text = $"Chyba při skenování: {ex.Message}";
            }
            finally
            {
                _scanButton.Enabled = true;
                _scanRangeButton.Enabled = true;
            }
        }

        private async void ScanRangeButton_Click(object? sender, EventArgs e)
        {
            string ipRange = _ipRangeTextBox.Text.Trim();
            DebugLogger.Log($"ScanRangeButton_Click: IP rozsah: {ipRange}");
            
            if (string.IsNullOrWhiteSpace(ipRange))
            {
                _statusLabel.Text = "Zadejte IP rozsah (např. 192.168.0.1-255)";
                return;
            }

            _scanButton.Enabled = false;
            _scanRangeButton.Enabled = false;
            _statusLabel.Text = $"Skenování IP rozsahu {ipRange}...";
            _devicesTreeView.Nodes.Clear();
            _deviceNodes.Clear();
            _selectedDevice = null;
            _currentConfig = null;
            UpdateConfigDisplay();

            try
            {
                var devices = await _scanner.ScanIpRangeAsync(ipRange);

                if (devices.Count == 0)
                {
                    _statusLabel.Text = $"Nebyla nalezena žádná dostupná zařízení v rozsahu {ipRange}.";
                }
                else
                {
                    // Seřazení podle statusu (OK první, pak ERROR)
                    devices = devices.OrderByDescending(d => d.Status == "OK").ToList();
                    
                    // Před zobrazením zkontrolujeme port 7777 pro každé zařízení
                    await CheckDevicesPortsAsync(devices);
                    UpdateDevicesTreeView(devices);
                    
                    int okCount = devices.Count(d => d.Status == "OK");
                    int warningCount = devices.Count(d => d.Status == "WARNING");
                    int errorCount = devices.Count(d => d.Status == "ERROR");
                    _statusLabel.Text = $"Skenování dokončeno: {okCount} OK, {warningCount} WARNING, {errorCount} ERROR (celkem {devices.Count} zařízení).";
                    StartStatusCheck();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"ScanRangeButton_Click: Výjimka: {ex.GetType().Name}: {ex.Message}");
                _statusLabel.Text = $"Chyba při skenování IP rozsahu: {ex.Message}";
            }
            finally
            {
                _scanButton.Enabled = true;
                _scanRangeButton.Enabled = true;
            }
        }

        private async void DevicesTreeView_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is NetworkDevice device)
            {
                DebugLogger.Log($"DevicesTreeView_AfterSelect: Vybráno zařízení: {device.IPAddress} ({device.Status})");
                _selectedDevice = device;
                
                // Pokud je status ERROR, nezkoušíme načítat konfiguraci
                if (device.Status == "ERROR")
                {
                    _statusLabel.Text = $"Zařízení {device.IPAddress} není dostupné (ERROR).";
                    _currentConfig = null;
                    UpdateConfigDisplay();
                    _refreshButton.Enabled = false;
                    _saveButton.Enabled = false;
                    return;
                }
                
                await LoadDeviceConfigAsync();
            }
        }

        private async Task LoadDeviceConfigAsync()
        {
            if (_selectedDevice == null)
            {
                return;
            }

            DebugLogger.Log($"LoadDeviceConfigAsync: Začátek pro {_selectedDevice.IPAddress}");
            _refreshButton.Enabled = false;
            _statusLabel.Text = $"Načítání konfigurace z {_selectedDevice.IPAddress}...";

            try
            {
                // Nejdříve zkusíme získat odpověď přímo pro lepší diagnostiku
                var response = await _client.SendRequestAsync(_selectedDevice.IPAddress, "get_config");
                
                // Debug informace
                DebugLogger.Log($"LoadDeviceConfigAsync: Odpověď serveru - Status: '{response.Status}', Error: '{response.Error}', Data: {response.Data?.GetRawText() ?? "null"}");
                
                if (string.IsNullOrEmpty(response.Status))
                {
                    _statusLabel.Text = $"Server vrátil prázdný status. Zkontrolujte připojení.";
                    _refreshButton.Enabled = true;
                    _currentConfig = null;
                    UpdateConfigDisplay();
                    return;
                }
                
                if (response.Status == "error")
                {
                    string errorMsg = response.Error ?? "Neznámá chyba";
                    _statusLabel.Text = $"Chyba serveru: {errorMsg}";
                    _refreshButton.Enabled = true;
                    _currentConfig = null;
                    UpdateConfigDisplay();
                    return;
                }
                
                if (response.Status != "ok")
                {
                    _statusLabel.Text = $"Neočekávaný status serveru: '{response.Status}'. Očekáváno 'ok' nebo 'error'.";
                    _refreshButton.Enabled = true;
                    _currentConfig = null;
                    UpdateConfigDisplay();
                    return;
                }
                
                if (string.IsNullOrEmpty(response.DataJson))
                {
                    _statusLabel.Text = "Server odpověděl OK, ale bez dat. Zkontrolujte kompatibilitu verzí.";
                    _refreshButton.Enabled = true;
                    _currentConfig = null;
                    UpdateConfigDisplay();
                    return;
                }

                // Nyní zkusíme parsovat konfiguraci
                _currentConfig = await _client.GetConfigAsync(_selectedDevice.IPAddress);

                if (_currentConfig != null)
                {
                    DebugLogger.Log($"LoadDeviceConfigAsync: Konfigurace úspěšně načtena");
                    UpdateConfigDisplay();
                    _refreshButton.Enabled = true;
                    _saveButton.Enabled = true;
                    _statusLabel.Text = "Konfigurace načtena úspěšně.";
                }
                else
                {
                    DebugLogger.Log($"LoadDeviceConfigAsync: Konfigurace se nepodařilo parsovat");
                    _statusLabel.Text = "Server odpověděl, ale data nelze parsovat. Zkontrolujte kompatibilitu verzí.";
                    _refreshButton.Enabled = true;
                    _currentConfig = null;
                    UpdateConfigDisplay();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"LoadDeviceConfigAsync: Výjimka: {ex.GetType().Name}: {ex.Message}");
                _statusLabel.Text = $"Chyba při načítání konfigurace: {ex.Message}";
                _refreshButton.Enabled = true;
                _currentConfig = null;
                UpdateConfigDisplay();
            }
        }

        private void UpdateConfigDisplay()
        {
            if (_currentConfig == null)
            {
                // Aktualizace value labels (uložené v Tag)
                if (_ipLabel.Tag is Label ipValue) ipValue.Text = "-";
                if (_hostnameLabel.Tag is Label hostnameValue) hostnameValue.Text = "-";
                if (_portLabel.Tag is Label portValue) portValue.Text = "-";
                if (_sshStatusLabel.Tag is Label sshValue) sshValue.Text = "-";
                if (_rootLoginLabel.Tag is Label rootLoginValue) rootLoginValue.Text = "-";
                if (_rootPasswordLabel.Tag is Label rootPasswordValue) rootPasswordValue.Text = "-";
                
                _hostnameTextBox.Text = "";
                _passwordTextBox.Text = "";
                _rootPasswordTextBox.Text = "";
                _sshEnabledCheckBox.Checked = false;
                _rootLoginCheckBox.Checked = false;
                return;
            }

            // Aktualizace value labels
            if (_ipLabel.Tag is Label ipValueLabel) 
                ipValueLabel.Text = _currentConfig.LocalIP;
            
            if (_hostnameLabel.Tag is Label hostnameValueLabel) 
                hostnameValueLabel.Text = _currentConfig.Hostname;
            
            if (_portLabel.Tag is Label portValueLabel) 
                portValueLabel.Text = _currentConfig.Port.ToString();
            
            if (_sshStatusLabel.Tag is Label sshValueLabel)
            {
                bool sshEnabled = _currentConfig.SshEnabled ?? false;
                sshValueLabel.Text = sshEnabled ? "Povoleno" : "Zakázáno";
                sshValueLabel.ForeColor = sshEnabled ? Color.Green : Color.Red;
            }
            
            if (_rootLoginLabel.Tag is Label rootLoginValueLabel)
            {
                bool rootLoginEnabled = _currentConfig.RootLoginEnabled ?? false;
                rootLoginValueLabel.Text = rootLoginEnabled ? "Povoleno" : "Zakázáno";
                rootLoginValueLabel.ForeColor = rootLoginEnabled ? Color.Green : Color.Red;
            }
            
            if (_rootPasswordLabel.Tag is Label rootPasswordValueLabel)
            {
                rootPasswordValueLabel.Text = _currentConfig.RootPasswordSet ? "Nastaveno" : "Nenastaveno";
                rootPasswordValueLabel.ForeColor = _currentConfig.RootPasswordSet ? Color.Green : Color.Orange;
            }

            // Aktualizace editovatelných polí
            _hostnameTextBox.Text = _currentConfig.Hostname;
            _sshEnabledCheckBox.Checked = _currentConfig.SshEnabled ?? false;
            _rootLoginCheckBox.Checked = _currentConfig.RootLoginEnabled ?? false;
        }

        private async void RefreshButton_Click(object? sender, EventArgs e)
        {
            DebugLogger.Log("RefreshButton_Click: Obnovuji konfiguraci");
            await LoadDeviceConfigAsync();
        }

        private async void SaveButton_Click(object? sender, EventArgs e)
        {
            if (_selectedDevice == null)
            {
                return;
            }

            DebugLogger.Log($"SaveButton_Click: Ukládání změn pro {_selectedDevice.IPAddress}");
            _saveButton.Enabled = false;
            _statusLabel.Text = "Ukládání změn...";

            try
            {
                bool success = true;
                string errorMessage = "";

                // Změna hostname
                if (!string.IsNullOrWhiteSpace(_hostnameTextBox.Text) && 
                    _hostnameTextBox.Text != _currentConfig?.Hostname)
                {
                    DebugLogger.Log($"SaveButton_Click: Změna hostname na {_hostnameTextBox.Text}");
                    var response = await _client.SetHostnameAsync(_selectedDevice.IPAddress, _hostnameTextBox.Text);
                    if (response.Status != "ok")
                    {
                        success = false;
                        errorMessage += $"Hostname: {response.Error}; ";
                    }
                }

                // Změna hesla uživatele
                if (!string.IsNullOrWhiteSpace(_passwordTextBox.Text))
                {
                    string username = _usernameTextBox.Text.Trim();
                    if (string.IsNullOrWhiteSpace(username))
                    {
                        success = false;
                        errorMessage += "Uživatelské jméno není zadáno; ";
                    }
                    else
                    {
                        DebugLogger.Log($"SaveButton_Click: Změna hesla uživatele {username}");
                        var response = await _client.SetPasswordAsync(_selectedDevice.IPAddress, username, _passwordTextBox.Text);
                        if (response.Status != "ok")
                        {
                            success = false;
                            errorMessage += $"Heslo: {response.Error}; ";
                        }
                    }
                }

                // Změna root hesla
                if (!string.IsNullOrWhiteSpace(_rootPasswordTextBox.Text))
                {
                    DebugLogger.Log("SaveButton_Click: Změna root hesla");
                    var response = await _client.SetRootPasswordAsync(_selectedDevice.IPAddress, _rootPasswordTextBox.Text);
                    if (response.Status != "ok")
                    {
                        success = false;
                        errorMessage += $"Root heslo: {response.Error}; ";
                    }
                }

                // Změna SSH stavu
                if (_sshEnabledCheckBox.Checked != _currentConfig?.SshEnabled)
                {
                    DebugLogger.Log($"SaveButton_Click: Změna SSH na {_sshEnabledCheckBox.Checked}");
                    var response = await _client.SetSshEnabledAsync(_selectedDevice.IPAddress, _sshEnabledCheckBox.Checked);
                    if (response.Status != "ok")
                    {
                        success = false;
                        errorMessage += $"SSH: {response.Error}; ";
                    }
                }

                // Změna root login stavu
                if (_rootLoginCheckBox.Checked != _currentConfig?.RootLoginEnabled)
                {
                    DebugLogger.Log($"SaveButton_Click: Změna root login na {_rootLoginCheckBox.Checked}");
                    var response = await _client.SetRootLoginAsync(_selectedDevice.IPAddress, _rootLoginCheckBox.Checked);
                    if (response.Status != "ok")
                    {
                        success = false;
                        errorMessage += $"Root login: {response.Error}; ";
                    }
                }

                // Nastavení statické IP adresy
                string? newStaticIp = null;
                string? oldIpAddress = null;
                if (!string.IsNullOrWhiteSpace(_staticIpTextBox.Text))
                {
                    DebugLogger.Log($"SaveButton_Click: Nastavení statické IP {_staticIpTextBox.Text}");
                    oldIpAddress = _selectedDevice.IPAddress;
                    newStaticIp = _staticIpTextBox.Text.Trim();
                    string netmask = string.IsNullOrWhiteSpace(_netmaskTextBox.Text) ? "255.255.255.0" : _netmaskTextBox.Text.Trim();
                    string? gateway = string.IsNullOrWhiteSpace(_gatewayTextBox.Text) ? null : _gatewayTextBox.Text.Trim();
                    
                    var response = await _client.SetStaticIpAsync(
                        _selectedDevice.IPAddress, 
                        newStaticIp, 
                        netmask, 
                        gateway
                    );
                    if (response.Status != "ok")
                    {
                        success = false;
                        errorMessage += $"Statická IP: {response.Error}; ";
                        newStaticIp = null; // Nebudeme sledovat změnu, pokud nastavení selhalo
                    }
                }

                if (success)
                {
                    DebugLogger.Log("SaveButton_Click: Všechny změny úspěšně uloženy");
                    
                    // Pokud byla nastavena statická IP, spustíme polling pro sledování změny
                    if (newStaticIp != null && oldIpAddress != null && newStaticIp != oldIpAddress)
                    {
                        _statusLabel.Text = $"Změny uloženy. Čekám na změnu IP adresy na {newStaticIp}...";
                        DebugLogger.Log($"SaveButton_Click: Spouštím polling pro sledování změny IP z {oldIpAddress} na {newStaticIp}");
                        StartIpPolling(oldIpAddress, newStaticIp);
                    }
                    else
                    {
                        _statusLabel.Text = "Změny byly úspěšně uloženy.";
                        // Obnovení konfigurace
                        await LoadDeviceConfigAsync();
                    }
                    
                    _passwordTextBox.Text = "";
                    _rootPasswordTextBox.Text = "";
                    _staticIpTextBox.Text = "";
                    _gatewayTextBox.Text = "";
                }
                else
                {
                    DebugLogger.Log($"SaveButton_Click: Chyby při ukládání: {errorMessage}");
                    _statusLabel.Text = $"Chyba při ukládání: {errorMessage}";
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"SaveButton_Click: Výjimka: {ex.GetType().Name}: {ex.Message}");
                _statusLabel.Text = $"Chyba: {ex.Message}";
            }
            finally
            {
                _saveButton.Enabled = true;
            }
        }

        private void DebugConsoleButton_Click(object? sender, EventArgs e)
        {
            DebugLogger.ShowConsole();
        }

        /// <summary>
        /// Spustí polling pro sledování změny IP adresy po nastavení statické IP.
        /// </summary>
        private void StartIpPolling(string oldIpAddress, string newIpAddress)
        {
            // Zastavíme předchozí polling, pokud běží
            StopIpPolling();

            _ipPollingCancellation = new System.Threading.CancellationTokenSource();
            var cancellationToken = _ipPollingCancellation.Token;

            Task.Run(async () =>
            {
                DebugLogger.Log($"StartIpPolling: Začátek sledování změny IP z {oldIpAddress} na {newIpAddress}");
                int attempts = 0;
                const int maxAttempts = 60; // 60 pokusů = 5 minut (5 sekund mezi pokusy)
                const int delayMs = 5000; // 5 sekund mezi pokusy

                while (attempts < maxAttempts && !cancellationToken.IsCancellationRequested)
                {
                    attempts++;
                    await Task.Delay(delayMs, cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        DebugLogger.Log($"StartIpPolling: Pokus {attempts}/{maxAttempts} - kontrola IP {newIpAddress}");

                        // Zkusíme ping na novou IP adresu
                        using (var ping = new System.Net.NetworkInformation.Ping())
                        {
                            var reply = await ping.SendPingAsync(newIpAddress, 2000);
                            if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                            {
                                DebugLogger.Log($"StartIpPolling: Nová IP adresa {newIpAddress} odpovídá na ping!");
                                
                                // Zkusíme se připojit k serveru na nové IP adrese
                                try
                                {
                                    var testConfig = await _client.GetConfigAsync(newIpAddress);
                                    if (testConfig != null)
                                    {
                                        DebugLogger.Log($"StartIpPolling: Úspěšně připojeno k serveru na {newIpAddress}!");
                                        
                                        // Aktualizujeme UI na UI vlákně
                                        this.Invoke((MethodInvoker)async delegate
                                        {
                                            // Aktualizujeme vybrané zařízení s novou IP adresou
                                            if (_selectedDevice != null)
                                            {
                                                string oldHostname = _selectedDevice.Hostname;
                                                _selectedDevice.IPAddress = newIpAddress;
                                                _selectedDevice.Status = "OK";
                                                
                                                // Aktualizujeme TreeView
                                                if (_deviceNodes.TryGetValue(_selectedDevice.IPAddress, out var node))
                                                {
                                                    node.Tag = _selectedDevice;
                                                    UpdateTreeNode(node, _selectedDevice);
                                                }
                                                
                                                // Načteme novou konfiguraci na nové IP adrese
                                                _statusLabel.Text = $"IP adresa změněna! Načítám konfiguraci z {newIpAddress}...";
                                                _statusLabel.ForeColor = Color.Blue;
                                                
                                                try
                                                {
                                                    await LoadDeviceConfigAsync();
                                                    _statusLabel.Text = $"IP adresa byla úspěšně změněna na {newIpAddress}!";
                                                    _statusLabel.ForeColor = Color.Green;
                                                }
                                                catch (Exception ex)
                                                {
                                                    DebugLogger.Log($"StartIpPolling: Chyba při načítání konfigurace: {ex.Message}");
                                                    _statusLabel.Text = $"IP adresa změněna na {newIpAddress}, ale konfigurace se nepodařilo načíst: {ex.Message}";
                                                    _statusLabel.ForeColor = Color.Orange;
                                                }
                                            }
                                        });
                                        
                                        StopIpPolling();
                                        return;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    DebugLogger.Log($"StartIpPolling: Chyba při připojení k serveru na {newIpAddress}: {ex.Message}");
                                }
                            }
                            else
                            {
                                DebugLogger.Log($"StartIpPolling: IP {newIpAddress} ještě neodpovídá (status: {reply.Status})");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log($"StartIpPolling: Chyba při kontrole IP: {ex.Message}");
                    }

                    // Aktualizujeme status na UI vlákně
                    if (attempts % 6 == 0) // Každých 30 sekund
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            _statusLabel.Text = $"Čekám na změnu IP adresy na {newIpAddress}... ({attempts * 5}s)";
                        });
                    }
                }

                // Pokud jsme dosáhli maximálního počtu pokusů
                if (attempts >= maxAttempts && !cancellationToken.IsCancellationRequested)
                {
                    DebugLogger.Log($"StartIpPolling: Dosáhnut maximální počet pokusů. IP adresa se možná nezměnila.");
                    this.Invoke((MethodInvoker)delegate
                    {
                        _statusLabel.Text = $"IP adresa se nezměnila během 5 minut. Zkontrolujte připojení ručně.";
                        _statusLabel.ForeColor = Color.Orange;
                    });
                }

                StopIpPolling();
            }, cancellationToken);
        }

        /// <summary>
        /// Zastaví polling pro sledování změny IP adresy.
        /// </summary>
        private void StopIpPolling()
        {
            if (_ipPollingCancellation != null)
            {
                _ipPollingCancellation.Cancel();
                _ipPollingCancellation.Dispose();
                _ipPollingCancellation = null;
                DebugLogger.Log("StopIpPolling: Polling zastaven");
            }
        }

        private void ServersMenuItem_Click(object? sender, EventArgs e)
        {
            using (var serversForm = new ServersForm())
            {
                serversForm.ShowDialog(this);
            }
        }

        private void LanguageCzechMenuItem_Click(object? sender, EventArgs e)
        {
            var settings = AppSettings.LoadSettings();
            settings.Language = "Czech";
            AppSettings.SaveSettings(settings);
            Localization.SetLanguage(Localization.Language.Czech);
            UpdateLanguageUI();
            UpdateLanguageMenuCheckmarks();
            MessageBox.Show(Localization.GetString("LanguageChanged"), Localization.GetString("LanguageMenu"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void LanguageEnglishMenuItem_Click(object? sender, EventArgs e)
        {
            var settings = AppSettings.LoadSettings();
            settings.Language = "English";
            AppSettings.SaveSettings(settings);
            Localization.SetLanguage(Localization.Language.English);
            UpdateLanguageUI();
            UpdateLanguageMenuCheckmarks();
            MessageBox.Show(Localization.GetString("LanguageChanged"), Localization.GetString("LanguageMenu"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateLanguageUI()
        {
            // Form title
            this.Text = Localization.GetString("FormTitle");
            
            // Menu
            _languageMenu.Text = Localization.GetString("LanguageMenu");
            _languageMenu.ToolTipText = Localization.GetString("LanguageMenuTooltip");
            _languageCzechMenuItem.Text = Localization.GetString("LanguageCzech");
            _languageEnglishMenuItem.Text = Localization.GetString("LanguageEnglish");
            
            _configMenu.Text = Localization.GetString("ConfigMenu");
            _configMenu.ToolTipText = Localization.GetString("ConfigMenuTooltip");
            _downloadConfigMenuItem.Text = Localization.GetString("DownloadConfig");
            _uploadConfigMenuItem.Text = Localization.GetString("UploadConfig");
            
            _raspberryMenu.Text = Localization.GetString("RaspberryMenu");
            _serversMenuItem.Text = Localization.GetString("ServersMenuItem");
            
            // GroupBoxes
            _devicesGroupBox.Text = Localization.GetString("DevicesGroupBox");
            _scanGroupBox.Text = Localization.GetString("ScanGroupBox");
            _configGroupBox.Text = Localization.GetString("ConfigGroupBox");
            _editGroupBox.Text = Localization.GetString("EditGroupBox");
            
            // Labels
            _devicesLabel.Text = Localization.GetString("DevicesLabel");
            _ipRangeLabel.Text = Localization.GetString("IpRangeLabel");
            _hostnameScanLabel.Text = Localization.GetString("HostnameScanLabel");
            _ipLabel.Text = Localization.GetString("IpLabel");
            _hostnameLabel.Text = Localization.GetString("HostnameLabel");
            _portLabel.Text = Localization.GetString("PortLabel");
            _sshStatusLabel.Text = Localization.GetString("SshStatusLabel");
            _rootLoginLabel.Text = Localization.GetString("RootLoginLabel");
            _rootPasswordLabel.Text = Localization.GetString("RootPasswordLabel");
            _newHostnameLabel.Text = Localization.GetString("NewHostnameLabel");
            _usernameLabel.Text = Localization.GetString("UsernameLabel");
            _passwordLabel.Text = Localization.GetString("PasswordLabel");
            _rootPasswordEditLabel.Text = Localization.GetString("RootPasswordEditLabel");
            _staticIpLabel.Text = Localization.GetString("StaticIpLabel");
            _netmaskLabel.Text = Localization.GetString("NetmaskLabel");
            _gatewayLabel.Text = Localization.GetString("GatewayLabel");
            
            // Buttons
            _scanButton.Text = Localization.GetString("ScanButton");
            _scanRangeButton.Text = Localization.GetString("ScanRangeButton");
            _saveButton.Text = Localization.GetString("SaveButton");
            _refreshButton.Text = Localization.GetString("RefreshConfigButton");
            _debugConsoleButton.Text = Localization.GetString("DebugConsoleButton");
            
            // CheckBoxes
            _sshEnabledCheckBox.Text = Localization.GetString("SshEnabledCheckBox");
            _rootLoginCheckBox.Text = Localization.GetString("RootLoginCheckBox");
            
            // Status
            _statusLabel.Text = Localization.GetString("ReadyStatusMain");
        }

        private void UpdateDevicesTreeView(List<NetworkDevice> devices)
        {
            _devicesTreeView.BeginUpdate();
            _devicesTreeView.Nodes.Clear();
            _deviceNodes.Clear();

            foreach (var device in devices)
            {
                var node = new TreeNode
                {
                    Text = $"{device.Hostname} ({device.IPAddress})",
                    Tag = device
                };
                UpdateTreeNode(node, device);
                _devicesTreeView.Nodes.Add(node);
                _deviceNodes[device.IPAddress] = node;
            }

            _devicesTreeView.EndUpdate();
        }

        private void UpdateTreeNode(TreeNode node, NetworkDevice device)
        {
            // 0 = online (green), 1 = offline (red), 2 = warning (orange/yellow)
            int imageIndex;
            if (device.Status == "OK")
            {
                imageIndex = 0; // Zelená - ping OK a port 7777 OK
            }
            else if (device.Status == "WARNING")
            {
                imageIndex = 2; // Oranžová - ping OK, ale port 7777 nedostupný
            }
            else
            {
                imageIndex = 1; // Červená - ping selhal
            }
            
            node.ImageIndex = imageIndex;
            node.SelectedImageIndex = imageIndex;
            node.Text = $"{device.Hostname} ({device.IPAddress})";
        }

        private void StartStatusCheck()
        {
            StopStatusCheck();
            _statusCheckCancellation = new CancellationTokenSource();
            var token = _statusCheckCancellation.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(5000, token); // Kontrola každých 5 sekund

                        if (token.IsCancellationRequested) break;

                        var devices = new List<NetworkDevice>();
                        this.Invoke((MethodInvoker)delegate
                        {
                            foreach (TreeNode node in _devicesTreeView.Nodes)
                            {
                                if (node.Tag is NetworkDevice device)
                                {
                                    devices.Add(device);
                                }
                            }
                        });

                        foreach (var device in devices)
                        {
                            if (token.IsCancellationRequested) break;

                            bool pingOk = await PingDeviceAsync(device.IPAddress);
                            if (pingOk)
                            {
                                // Ping OK, zkontrolujeme port 7777
                                bool portOk = await CheckPortAsync(device.IPAddress, 7777);
                                device.Status = portOk ? "OK" : "WARNING";
                            }
                            else
                            {
                                device.Status = "ERROR";
                            }

                            this.Invoke((MethodInvoker)delegate
                            {
                                if (_deviceNodes.TryGetValue(device.IPAddress, out var node))
                                {
                                    UpdateTreeNode(node, device);
                                }
                            });
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log($"StartStatusCheck: Chyba při kontrole statusu: {ex.Message}");
                    }
                }
            }, token);
        }

        private void StopStatusCheck()
        {
            if (_statusCheckCancellation != null)
            {
                _statusCheckCancellation.Cancel();
                _statusCheckCancellation.Dispose();
                _statusCheckCancellation = null;
            }
        }

        private async Task<bool> PingDeviceAsync(string ipAddress)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(ipAddress, 2000); // 2 sekundy timeout
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> CheckPortAsync(string ipAddress, int port)
        {
            try
            {
                using (var client = new System.Net.Sockets.TcpClient())
                {
                    var connectTask = client.ConnectAsync(ipAddress, port);
                    var timeoutTask = Task.Delay(2000); // 2 sekundy timeout

                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        return false; // Timeout
                    }

                    await connectTask;
                    return client.Connected;
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task CheckDevicesPortsAsync(List<NetworkDevice> devices)
        {
            // Kontrolujeme port 7777 pro všechna zařízení, která mají status OK (ping funguje)
            var tasks = devices.Where(d => d.Status == "OK").Select(async device =>
            {
                bool portOk = await CheckPortAsync(device.IPAddress, 7777);
                if (!portOk)
                {
                    device.Status = "WARNING"; // Ping OK, ale port 7777 nedostupný
                }
            }).ToList();

            await Task.WhenAll(tasks);
        }

        private void UpdateLanguageMenuCheckmarks()
        {
            var settings = AppSettings.LoadSettings();
            _languageCzechMenuItem.Checked = settings.Language == "Czech";
            _languageEnglishMenuItem.Checked = settings.Language == "English";
        }

        private async void DownloadConfigMenuItem_Click(object? sender, EventArgs e)
        {
            if (_currentConfig == null || _selectedDevice == null)
            {
                MessageBox.Show("Nejprve vyberte zařízení a načtěte konfiguraci.", "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "JSON soubory (*.json)|*.json|Všechny soubory (*.*)|*.*";
                saveDialog.FileName = $"config_{_selectedDevice.Hostname}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                saveDialog.DefaultExt = "json";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(_currentConfig, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        await System.IO.File.WriteAllTextAsync(saveDialog.FileName, json);
                        MessageBox.Show("Konfigurace byla úspěšně uložena.", "Úspěch", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Chyba při ukládání konfigurace: {ex.Message}", "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private async void UploadConfigMenuItem_Click(object? sender, EventArgs e)
        {
            if (_selectedDevice == null)
            {
                MessageBox.Show("Nejprve vyberte zařízení.", "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                            MessageBox.Show("Nepodařilo se načíst konfiguraci ze souboru.", "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        // Aplikujeme konfiguraci
                        bool ipChanged = false;
                        string? newIpAddress = null;
                        string? oldIpAddress = _selectedDevice.IPAddress;

                        // Změna hostname
                        if (!string.IsNullOrWhiteSpace(config.Hostname) && config.Hostname != _currentConfig?.Hostname)
                        {
                            var response = await _client.SetHostnameAsync(_selectedDevice.IPAddress, config.Hostname);
                            if (response.Status != "ok")
                            {
                                MessageBox.Show($"Chyba při nastavení hostname: {response.Error}", "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                        }

                        // Změna SSH
                        if (config.SshEnabled.HasValue && config.SshEnabled != _currentConfig?.SshEnabled)
                        {
                            var response = await _client.SetSshEnabledAsync(_selectedDevice.IPAddress, config.SshEnabled.Value);
                            if (response.Status != "ok")
                            {
                                MessageBox.Show($"Chyba při nastavení SSH: {response.Error}", "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                        }

                        // Změna root login
                        if (config.RootLoginEnabled.HasValue && config.RootLoginEnabled != _currentConfig?.RootLoginEnabled)
                        {
                            var response = await _client.SetRootLoginAsync(_selectedDevice.IPAddress, config.RootLoginEnabled.Value);
                            if (response.Status != "ok")
                            {
                                MessageBox.Show($"Chyba při nastavení root loginu: {response.Error}", "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                        }

                        // Změna statické IP (musí být poslední, protože restartuje zařízení)
                        // Zkontrolujeme, zda máme statickou IP v aktuální konfiguraci
                        string? staticIp = _staticIpTextBox.Text.Trim();
                        string? netmask = _netmaskTextBox.Text.Trim();
                        string? gateway = _gatewayTextBox.Text.Trim();
                        
                        if (!string.IsNullOrWhiteSpace(config.LocalIP) && 
                            config.LocalIP != _currentConfig?.LocalIP &&
                            !string.IsNullOrWhiteSpace(netmask))
                        {
                            ipChanged = true;
                            newIpAddress = config.LocalIP;
                            var response = await _client.SetStaticIpAsync(
                                _selectedDevice.IPAddress,
                                config.LocalIP,
                                netmask,
                                gateway);
                            
                            if (response.Status != "ok")
                            {
                                MessageBox.Show($"Chyba při nastavení statické IP: {response.Error}", "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                ipChanged = false;
                            }
                            else
                            {
                                // Aktualizujeme IP adresu v _selectedDevice
                                _selectedDevice.IPAddress = newIpAddress;
                                _currentConfig = config;
                                _currentConfig.LocalIP = newIpAddress;
                            }
                        }

                        if (ipChanged && !string.IsNullOrEmpty(newIpAddress))
                        {
                            // Spustíme hledání zařízení na nové IP adrese
                            _statusLabel.Text = $"IP adresa se mění na {newIpAddress}. Hledám zařízení...";
                            _statusLabel.ForeColor = Color.Blue;
                            
                            // Použijeme FindDeviceAfterIpChangeAsync pokud existuje, jinak jednodušší metodu
                            await Task.Delay(3000); // Počkáme 3 sekundy na restart
                            
                            // Zkusíme načíst konfiguraci z nové IP
                            try
                            {
                                var newConfig = await _client.GetConfigAsync(newIpAddress);
                                if (newConfig != null)
                                {
                                    _currentConfig = newConfig;
                                    UpdateConfigDisplay();
                                    _statusLabel.Text = $"Konfigurace byla úspěšně nahrána a IP adresa změněna na {newIpAddress}!";
                                    _statusLabel.ForeColor = Color.Green;
                                    MessageBox.Show($"Konfigurace byla úspěšně nahrána. IP adresa byla změněna na {newIpAddress}.", "Úspěch", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                }
                            }
                            catch
                            {
                                _statusLabel.Text = $"Konfigurace nahrána, ale zařízení ještě není dostupné na nové IP {newIpAddress}.";
                                _statusLabel.ForeColor = Color.Orange;
                            }
                        }
                        else
                        {
                            // Načteme aktualizovanou konfiguraci
                            await LoadDeviceConfigAsync();
                            MessageBox.Show("Konfigurace byla úspěšně nahrána.", "Úspěch", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Chyba při nahrávání konfigurace: {ex.Message}", "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopIpPolling();
            StopStatusCheck();
            base.OnFormClosing(e);
        }

        private async void DevicesTreeView_DownloadConfig(object? sender, EventArgs e)
        {
            if (_currentConfig == null || _selectedDevice == null)
            {
                MessageBox.Show(
                    Localization.GetString("SelectDeviceAndLoadConfig"),
                    Localization.GetString("Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "JSON soubory (*.json)|*.json|Všechny soubory (*.*)|*.*";
                saveDialog.FileName = $"config_{_selectedDevice.Hostname}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                saveDialog.DefaultExt = "json";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(_currentConfig, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        await System.IO.File.WriteAllTextAsync(saveDialog.FileName, json);
                        MessageBox.Show(
                            Localization.GetString("ConfigSaved"),
                            Localization.GetString("Success"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            $"Chyba při ukládání konfigurace: {ex.Message}",
                            Localization.GetString("Error"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
            }
        }

        private async void DevicesTreeView_UploadConfig(object? sender, EventArgs e)
        {
            if (_selectedDevice == null)
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

                        // Aplikujeme konfiguraci
                        bool ipChanged = false;
                        string? newIpAddress = null;
                        string? oldIpAddress = _selectedDevice.IPAddress;

                        // Změna hostname
                        if (!string.IsNullOrWhiteSpace(config.Hostname) && config.Hostname != _currentConfig?.Hostname)
                        {
                            var response = await _client.SetHostnameAsync(_selectedDevice.IPAddress, config.Hostname);
                            if (response.Status != "ok")
                            {
                                MessageBox.Show(
                                    $"Chyba při nastavení hostname: {response.Error}",
                                    Localization.GetString("Error"),
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning);
                            }
                        }

                        // Změna SSH
                        if (config.SshEnabled.HasValue && config.SshEnabled != _currentConfig?.SshEnabled)
                        {
                            var response = await _client.SetSshEnabledAsync(_selectedDevice.IPAddress, config.SshEnabled.Value);
                            if (response.Status != "ok")
                            {
                                MessageBox.Show(
                                    $"Chyba při nastavení SSH: {response.Error}",
                                    Localization.GetString("Error"),
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning);
                            }
                        }

                        // Změna root login
                        if (config.RootLoginEnabled.HasValue && config.RootLoginEnabled != _currentConfig?.RootLoginEnabled)
                        {
                            var response = await _client.SetRootLoginAsync(_selectedDevice.IPAddress, config.RootLoginEnabled.Value);
                            if (response.Status != "ok")
                            {
                                MessageBox.Show(
                                    $"Chyba při nastavení root loginu: {response.Error}",
                                    Localization.GetString("Error"),
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning);
                            }
                        }

                        // Změna statické IP
                        if (!string.IsNullOrWhiteSpace(config.StaticIP) && config.StaticIP != _currentConfig?.StaticIP)
                        {
                            string netmask = config.Netmask ?? "255.255.255.0";
                            var response = await _client.SetStaticIpAsync(
                                _selectedDevice.IPAddress,
                                config.StaticIP,
                                netmask,
                                config.Gateway
                            );
                            if (response.Status == "ok")
                            {
                                ipChanged = true;
                                newIpAddress = config.StaticIP;
                            }
                            else
                            {
                                MessageBox.Show(
                                    $"Chyba při nastavení statické IP: {response.Error}",
                                    Localization.GetString("Error"),
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning);
                            }
                        }

                        if (ipChanged && newIpAddress != null && oldIpAddress != null)
                        {
                            _statusLabel.Text = $"Konfigurace nahrána. Čekám na změnu IP adresy na {newIpAddress}...";
                            StartIpPolling(oldIpAddress, newIpAddress);
                        }
                        else
                        {
                            MessageBox.Show(
                                Localization.GetString("ConfigLoadedFromFile"),
                                Localization.GetString("Success"),
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                            await LoadDeviceConfigAsync();
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

        private async void DevicesTreeView_ChangeRootPassword(object? sender, EventArgs e)
        {
            if (_selectedDevice == null)
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

                        var response = await _client.SetRootPasswordAsync(_selectedDevice.IPAddress, dialog.NewRootPassword);

                        if (response.Status == "ok")
                        {
                            _statusLabel.Text = Localization.GetString("RootPasswordChanged");
                            _statusLabel.ForeColor = Color.Green;
                            MessageBox.Show(
                                Localization.GetString("RootPasswordChanged"),
                                Localization.GetString("Success"),
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                            
                            // Obnovit konfiguraci
                            await LoadDeviceConfigAsync();
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
                        DebugLogger.Log($"DevicesTreeView_ChangeRootPassword: Chyba: {ex.Message}");
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

