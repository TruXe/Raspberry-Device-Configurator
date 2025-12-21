using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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

        public MainForm()
        {
            InitializeComponent();
            _scanner = new NetworkScanner();
            _client = new ConfigClient();
            
            // Zobrazit debug konzoli při startu (volitelné)
            // DebugLogger.ShowConsole();
        }

        private async void ScanButton_Click(object? sender, EventArgs e)
        {
            DebugLogger.Log("ScanButton_Click: Začátek skenování podle hostname");
            _scanButton.Enabled = false;
            _scanRangeButton.Enabled = false;
            _statusLabel.Text = "Skenování sítě podle hostname...";
            _devicesListBox.Items.Clear();
            _selectedDevice = null;
            _currentConfig = null;
            UpdateConfigDisplay();

            try
            {
                var devices = await _scanner.ScanNetworkAsync();

                if (devices.Count == 0)
                {
                    _statusLabel.Text = "Nebyla nalezena žádná zařízení. Zkuste znovu nebo použijte skenování IP rozsahu.";
                }
                else
                {
                    foreach (var device in devices)
                    {
                        _devicesListBox.Items.Add(device);
                    }
                    _statusLabel.Text = $"Nalezeno {devices.Count} zařízení.";
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
            _devicesListBox.Items.Clear();
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
                    
                    foreach (var device in devices)
                    {
                        _devicesListBox.Items.Add(device);
                    }
                    
                    int okCount = devices.Count(d => d.Status == "OK");
                    int errorCount = devices.Count(d => d.Status == "ERROR");
                    _statusLabel.Text = $"Skenování dokončeno: {okCount} OK, {errorCount} ERROR (celkem {devices.Count} zařízení).";
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

        private async void DevicesListBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_devicesListBox.SelectedItem is NetworkDevice device)
            {
                DebugLogger.Log($"DevicesListBox_SelectedIndexChanged: Vybráno zařízení: {device.IPAddress} ({device.Status})");
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
                sshValueLabel.Text = _currentConfig.SshEnabled ? "Povoleno" : "Zakázáno";
                sshValueLabel.ForeColor = _currentConfig.SshEnabled ? Color.Green : Color.Red;
            }
            
            if (_rootLoginLabel.Tag is Label rootLoginValueLabel)
            {
                rootLoginValueLabel.Text = _currentConfig.RootLoginEnabled ? "Povoleno" : "Zakázáno";
                rootLoginValueLabel.ForeColor = _currentConfig.RootLoginEnabled ? Color.Green : Color.Red;
            }
            
            if (_rootPasswordLabel.Tag is Label rootPasswordValueLabel)
            {
                rootPasswordValueLabel.Text = _currentConfig.RootPasswordSet ? "Nastaveno" : "Nenastaveno";
                rootPasswordValueLabel.ForeColor = _currentConfig.RootPasswordSet ? Color.Green : Color.Orange;
            }

            // Aktualizace editovatelných polí
            _hostnameTextBox.Text = _currentConfig.Hostname;
            _sshEnabledCheckBox.Checked = _currentConfig.SshEnabled;
            _rootLoginCheckBox.Checked = _currentConfig.RootLoginEnabled;
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
                                                
                                                // Aktualizujeme seznam zařízení
                                                int selectedIndex = _devicesListBox.SelectedIndex;
                                                if (selectedIndex >= 0 && selectedIndex < _devicesListBox.Items.Count)
                                                {
                                                    _devicesListBox.Items[selectedIndex] = _selectedDevice;
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopIpPolling();
            base.OnFormClosing(e);
        }
    }
}

