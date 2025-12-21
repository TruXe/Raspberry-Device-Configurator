using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.Json;

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
            
            // Připojení event handlerů pro menu
            _downloadConfigMenuItem.Click += DownloadConfigMenuItem_Click;
            _uploadConfigMenuItem.Click += UploadConfigMenuItem_Click;
            
            // ZAKOMENTOVÁNO: Automatické načtení konfigurace při startu
            // Uživatel musí manuálně kliknout na skenování
            // LoadSavedConfigOnStartup();
        }

        private async void ScanButton_Click(object? sender, EventArgs e)
        {
            await PerformHostnameScanAsync();
        }

        /// <summary>
        /// Provede skenování podle hostname s progress barem.
        /// </summary>
        private async Task PerformHostnameScanAsync(string targetHostname = null, string expectedNewIp = null)
        {
            DebugLogger.Log("PerformHostnameScanAsync: Začátek skenování podle hostname");
            _scanButton.Enabled = false;
            _scanRangeButton.Enabled = false;
            _statusLabel.Text = "Skenování sítě podle hostname...";
            _statusLabel.ForeColor = Color.Blue;
            _scanProgressBar.Visible = true;
            _scanProgressBar.Value = 0;
            _scanProgressBar.Style = ProgressBarStyle.Marquee; // Animovaný progress bar
            _scanProgressBar.MarqueeAnimationSpeed = 30;
            _devicesListBox.Items.Clear();
            
            // Pokud není cílové hostname, vynulujeme vybrané zařízení
            if (string.IsNullOrEmpty(targetHostname))
            {
                _selectedDevice = null;
                _currentConfig = null;
                UpdateConfigDisplay();
            }

            var startTime = DateTime.Now;

            try
            {
                // Spustíme skenování
                var devices = await _scanner.ScanNetworkAsync();
                
                var elapsed = (DateTime.Now - startTime).TotalSeconds;
                DebugLogger.Log($"PerformHostnameScanAsync: Skenování dokončeno za {elapsed:F1}s, nalezeno {devices.Count} zařízení");

                // Aktualizujeme UI s výsledky
                _scanProgressBar.Style = ProgressBarStyle.Continuous;
                _scanProgressBar.Value = 100;
                _scanProgressBar.Visible = false;

                if (devices.Count == 0)
                {
                    _statusLabel.Text = "Nebyla nalezena žádná zařízení. Zkuste znovu nebo použijte skenování IP rozsahu.";
                    _statusLabel.ForeColor = Color.Orange;
                }
                else
                {
                    // Seřazení podle statusu (OK první, pak ERROR)
                    devices = devices.OrderByDescending(d => d.Status == "OK").ToList();
                    
                    foreach (var device in devices)
                    {
                        _devicesListBox.Items.Add(device);
                    }
                    _statusLabel.Text = $"Nalezeno {devices.Count} zařízení.";
                    _statusLabel.ForeColor = Color.Green;
                    
                    // Pokud máme cílové hostname nebo očekávanou novou IP, zkusíme najít zařízení
                    NetworkDevice foundDevice = null;
                    
                    if (!string.IsNullOrEmpty(targetHostname))
                    {
                        // Hledáme podle hostname
                        foundDevice = devices.FirstOrDefault(d => 
                            d.Hostname.Equals(targetHostname, StringComparison.OrdinalIgnoreCase));
                        
                        if (foundDevice != null)
                        {
                            DebugLogger.Log($"PerformHostnameScanAsync: Nalezeno zařízení podle hostname: {foundDevice.Hostname} ({foundDevice.IPAddress})");
                        }
                    }
                    
                    // Pokud máme očekávanou novou IP, zkusíme najít zařízení podle IP
                    if (foundDevice == null && !string.IsNullOrEmpty(expectedNewIp))
                    {
                        foundDevice = devices.FirstOrDefault(d => d.IPAddress == expectedNewIp);
                        
                        if (foundDevice != null)
                        {
                            DebugLogger.Log($"PerformHostnameScanAsync: Nalezeno zařízení podle nové IP: {foundDevice.Hostname} ({foundDevice.IPAddress})");
                        }
                    }
                    
                    // Pokud jsme našli zařízení, automaticky ho vybereme a načteme konfiguraci
                    if (foundDevice != null)
                    {
                        int index = _devicesListBox.Items.IndexOf(foundDevice);
                        if (index >= 0)
                        {
                            _devicesListBox.SelectedIndex = index;
                            _selectedDevice = foundDevice;
                            DebugLogger.Log($"PerformHostnameScanAsync: Automaticky vybráno zařízení {foundDevice.Hostname} ({foundDevice.IPAddress})");
                            
                            // Automaticky načteme konfiguraci z nového zařízení
                            string newDeviceIp = foundDevice.IPAddress;
                            Task.Run(async () =>
                            {
                                try
                                {
                                    DebugLogger.Log($"PerformHostnameScanAsync: Načítám konfiguraci z {newDeviceIp}");
                                    await LoadDeviceConfigAsync();
                                    this.Invoke((MethodInvoker)delegate
                                    {
                                        _statusLabel.Text = $"Zařízení nalezeno a připojeno na {newDeviceIp}. Konfigurace načtena.";
                                        _statusLabel.ForeColor = Color.Green;
                                    });
                                }
                                catch (Exception ex)
                                {
                                    DebugLogger.Log($"PerformHostnameScanAsync: Chyba při načítání konfigurace: {ex.Message}");
                                    this.Invoke((MethodInvoker)delegate
                                    {
                                        _statusLabel.Text = $"Zařízení nalezeno na {newDeviceIp}, ale konfigurace se nepodařilo načíst: {ex.Message}";
                                        _statusLabel.ForeColor = Color.Orange;
                                    });
                                }
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"PerformHostnameScanAsync: Výjimka: {ex.GetType().Name}: {ex.Message}");
                _scanProgressBar.Style = ProgressBarStyle.Continuous;
                _scanProgressBar.Visible = false;
                _statusLabel.Text = $"Chyba při skenování: {ex.Message}";
                _statusLabel.ForeColor = Color.Red;
            }
            finally
            {
                _scanButton.Enabled = true;
                _scanRangeButton.Enabled = true;
            }
        }

        /// <summary>
        /// Opakovaně se pokouší najít zařízení na nové IP adrese nebo podle hostname během 60 sekund.
        /// Během timeoutu pinguje novou IP, dokud nebude dostupná.
        /// </summary>
        private async Task FindDeviceAfterIpChangeAsync(string targetHostname, string expectedNewIp)
        {
            const int totalTimeoutMs = 60000; // 60 sekund celkový timeout
            const int pingIntervalMs = 2000; // 2 sekundy mezi pingy
            const int initialDelayMs = 3000; // 3 sekundy počáteční čekání na restart
            
            DebugLogger.Log($"FindDeviceAfterIpChangeAsync: Začátek hledání zařízení. Hostname: {targetHostname}, Očekávaná IP: {expectedNewIp}");
            
            var startTime = DateTime.Now;
            int pingAttemptNumber = 0;
            bool deviceFound = false;
            bool ipReachable = false;
            
            // Počáteční čekání na restart zařízení
            this.Invoke((MethodInvoker)delegate
            {
                _statusLabel.Text = $"IP adresa změněna na {expectedNewIp}. Čekám na restart zařízení...";
                _statusLabel.ForeColor = Color.Blue;
                _scanProgressBar.Visible = true;
                _scanProgressBar.Style = ProgressBarStyle.Continuous;
                _scanProgressBar.Value = 0;
                _scanProgressBar.Minimum = 0;
                _scanProgressBar.Maximum = 100;
            });
            
            // Počáteční čekání s progress barem
            for (int i = 0; i < initialDelayMs; i += 100)
            {
                await Task.Delay(100);
                var progress = (int)((i / (double)initialDelayMs) * 10); // 0-10% pro počáteční čekání
                this.Invoke((MethodInvoker)delegate
                {
                    _scanProgressBar.Value = progress;
                });
            }
            
            // Fáze 1: Pingování nové IP adresy, dokud nebude dostupná
            if (!string.IsNullOrEmpty(expectedNewIp))
            {
                DebugLogger.Log($"FindDeviceAfterIpChangeAsync: Začínám pingování IP {expectedNewIp}");
                
                while ((DateTime.Now - startTime).TotalMilliseconds < totalTimeoutMs && !ipReachable)
                {
                    pingAttemptNumber++;
                    var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                    var remaining = (totalTimeoutMs - elapsed) / 1000.0;
                    
                    // Vypočítáme progress: 10-90% pro pingování (10% je pro počáteční čekání, 90% pro pingování)
                    var pingProgress = 10 + (int)((elapsed / (double)totalTimeoutMs) * 80);
                    pingProgress = Math.Min(90, Math.Max(10, pingProgress));
                    
                    this.Invoke((MethodInvoker)delegate
                    {
                        _statusLabel.Text = $"Pinguji IP {expectedNewIp}... (pokus {pingAttemptNumber}, zbývá {remaining:F0}s)";
                        _statusLabel.ForeColor = Color.Blue;
                        _scanProgressBar.Value = pingProgress;
                    });
                    
                    try
                    {
                        using (var ping = new System.Net.NetworkInformation.Ping())
                        {
                            var reply = await ping.SendPingAsync(expectedNewIp, 2000);
                            
                            if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                            {
                                DebugLogger.Log($"FindDeviceAfterIpChangeAsync: IP {expectedNewIp} je dostupná (ping úspěšný)!");
                                ipReachable = true;
                                
                                this.Invoke((MethodInvoker)delegate
                                {
                                    _statusLabel.Text = $"IP {expectedNewIp} je dostupná. Připojuji se...";
                                    _statusLabel.ForeColor = Color.Blue;
                                    _scanProgressBar.Value = 90; // 90% - IP je dostupná, připojujeme se
                                });
                                break;
                            }
                            else
                            {
                                DebugLogger.Log($"FindDeviceAfterIpChangeAsync: IP {expectedNewIp} ještě neodpovídá (status: {reply.Status})");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log($"FindDeviceAfterIpChangeAsync: Chyba při pingování: {ex.Message}");
                    }
                    
                    // Počkáme před dalším pingem
                    var remainingMs = totalTimeoutMs - (int)(DateTime.Now - startTime).TotalMilliseconds;
                    if (remainingMs > 0)
                    {
                        var waitTime = Math.Min(pingIntervalMs, remainingMs);
                        await Task.Delay(waitTime);
                    }
                }
            }
            
            // Fáze 2: Pokud je IP dostupná, zkusíme připojení a skenování
            if (ipReachable && !string.IsNullOrEmpty(expectedNewIp))
            {
                DebugLogger.Log($"FindDeviceAfterIpChangeAsync: IP je dostupná, zkouším připojení a načtení konfigurace");
                
                this.Invoke((MethodInvoker)delegate
                {
                    _scanProgressBar.Value = 95; // 95% - připojujeme se k serveru
                });
                
                try
                {
                    // Zkusíme přímé připojení na novou IP
                    DebugLogger.Log($"FindDeviceAfterIpChangeAsync: Zkouším přímé připojení na {expectedNewIp}");
                    var testResponse = await _client.SendRequestAsync(expectedNewIp, "get_config");
                    
                    if (testResponse.Status == "ok" && !string.IsNullOrEmpty(testResponse.DataJson))
                    {
                        DebugLogger.Log($"FindDeviceAfterIpChangeAsync: Zařízení nalezeno na IP {expectedNewIp}!");
                        
                        this.Invoke((MethodInvoker)delegate
                        {
                            _scanProgressBar.Value = 100; // 100% - zařízení nalezeno
                        });
                        
                        // Aktualizujeme vybrané zařízení a listbox
                        this.Invoke((MethodInvoker)delegate
                        {
                            if (_selectedDevice != null)
                            {
                                // Aktualizujeme IP adresu v existujícím zařízení
                                _selectedDevice.IPAddress = expectedNewIp;
                                _selectedDevice.Status = "OK";
                                
                                // Aktualizujeme listbox - najdeme zařízení a aktualizujeme ho, nebo přidáme nové
                                bool deviceInList = false;
                                for (int i = 0; i < _devicesListBox.Items.Count; i++)
                                {
                                    if (_devicesListBox.Items[i] is NetworkDevice device && 
                                        (device.IPAddress == expectedNewIp || device == _selectedDevice))
                                    {
                                        // Aktualizujeme existující zařízení
                                        _devicesListBox.Items[i] = _selectedDevice;
                                        deviceInList = true;
                                        _devicesListBox.SelectedIndex = i;
                                        break;
                                    }
                                }
                                
                                // Pokud zařízení není v listboxu, přidáme ho
                                if (!deviceInList)
                                {
                                    _devicesListBox.Items.Add(_selectedDevice);
                                    _devicesListBox.SelectedIndex = _devicesListBox.Items.Count - 1;
                                }
                                
                                // Obnovíme zobrazení listboxu
                                _devicesListBox.Refresh();
                            }
                        });
                        
                        // Načteme konfiguraci (mimo Invoke, protože LoadDeviceConfigAsync už používá Invoke)
                        try
                        {
                            await LoadDeviceConfigAsync();
                            this.Invoke((MethodInvoker)delegate
                            {
                                _statusLabel.Text = $"Zařízení nalezeno a připojeno na {expectedNewIp}. Konfigurace načtena.";
                                _statusLabel.ForeColor = Color.Green;
                                _scanProgressBar.Value = 100;
                                _scanProgressBar.Visible = false;
                            });
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.Log($"FindDeviceAfterIpChangeAsync: Chyba při načítání konfigurace: {ex.Message}");
                            this.Invoke((MethodInvoker)delegate
                            {
                                _statusLabel.Text = $"Zařízení nalezeno na {expectedNewIp}, ale konfigurace se nepodařilo načíst.";
                                _statusLabel.ForeColor = Color.Orange;
                                _scanProgressBar.Value = 100;
                                _scanProgressBar.Visible = false;
                            });
                        }
                        
                        deviceFound = true;
                    }
                    else
                    {
                        DebugLogger.Log($"FindDeviceAfterIpChangeAsync: Připojení na {expectedNewIp} selhalo, zkouším skenování podle hostname");
                        
                        this.Invoke((MethodInvoker)delegate
                        {
                            _statusLabel.Text = $"Připojení selhalo. Skenuji síť podle hostname...";
                            _scanProgressBar.Value = 95; // 95% - skenujeme síť
                        });
                        
                        // Pokud přímé připojení nefunguje, zkusíme skenování podle hostname
                        if (!string.IsNullOrEmpty(targetHostname))
                        {
                            DebugLogger.Log($"FindDeviceAfterIpChangeAsync: Zkouším skenování podle hostname '{targetHostname}'");
                            
                            var devices = await _scanner.ScanNetworkAsync();
                            
                            // Hledáme podle hostname nebo nové IP
                            var foundDevice = devices.FirstOrDefault(d => 
                                (!string.IsNullOrEmpty(d.Hostname) && d.Hostname.Equals(targetHostname, StringComparison.OrdinalIgnoreCase)) ||
                                d.IPAddress == expectedNewIp);
                            
                            if (foundDevice != null)
                            {
                                DebugLogger.Log($"FindDeviceAfterIpChangeAsync: Zařízení nalezeno při skenování: {foundDevice.Hostname} ({foundDevice.IPAddress})");
                                
                                this.Invoke((MethodInvoker)delegate
                                {
                                    // Aktualizujeme seznam zařízení
                                    _devicesListBox.Items.Clear();
                                    foreach (var device in devices.OrderByDescending(d => d.Status == "OK"))
                                    {
                                        _devicesListBox.Items.Add(device);
                                    }
                                    
                                    // Vybereme nalezené zařízení
                                    int index = _devicesListBox.Items.IndexOf(foundDevice);
                                    if (index >= 0)
                                    {
                                        _devicesListBox.SelectedIndex = index;
                                        _selectedDevice = foundDevice;
                                        
                                        // Načteme konfiguraci
                                        Task.Run(async () =>
                                        {
                                            try
                                            {
                                                await LoadDeviceConfigAsync();
                                                this.Invoke((MethodInvoker)delegate
                                                {
                                                    _statusLabel.Text = $"Zařízení nalezeno a připojeno na {foundDevice.IPAddress}. Konfigurace načtena.";
                                                    _statusLabel.ForeColor = Color.Green;
                                                    _scanProgressBar.Visible = false;
                                                });
                                            }
                                            catch (Exception ex)
                                            {
                                                DebugLogger.Log($"FindDeviceAfterIpChangeAsync: Chyba při načítání konfigurace: {ex.Message}");
                                                this.Invoke((MethodInvoker)delegate
                                                {
                                                    _statusLabel.Text = $"Zařízení nalezeno na {foundDevice.IPAddress}, ale konfigurace se nepodařilo načíst.";
                                                    _statusLabel.ForeColor = Color.Orange;
                                                    _scanProgressBar.Visible = false;
                                                });
                                            }
                                        });
                                    }
                                });
                                
                                deviceFound = true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"FindDeviceAfterIpChangeAsync: Chyba při připojení: {ex.Message}");
                }
            }
            
            // Pokud jsme nenašli zařízení po 60 sekundách nebo IP nebyla dostupná
            if (!deviceFound)
            {
                if (!ipReachable)
                {
                    DebugLogger.Log($"FindDeviceAfterIpChangeAsync: IP {expectedNewIp} nebyla dostupná během 60 sekund");
                    this.Invoke((MethodInvoker)delegate
                    {
                        _statusLabel.Text = $"IP {expectedNewIp} nebyla dostupná během 60 sekund. Zkuste manuální skenování.";
                        _statusLabel.ForeColor = Color.Orange;
                        _scanProgressBar.Value = 100;
                        _scanProgressBar.Visible = false;
                    });
                }
                else
                {
                    DebugLogger.Log($"FindDeviceAfterIpChangeAsync: Zařízení nebylo nalezeno během 60 sekund");
                    this.Invoke((MethodInvoker)delegate
                    {
                        _statusLabel.Text = $"Zařízení nebylo nalezeno během 60 sekund. Zkuste manuální skenování.";
                        _statusLabel.ForeColor = Color.Orange;
                        _scanProgressBar.Value = 100;
                        _scanProgressBar.Visible = false;
                    });
                }
                
                // Vynulujeme vybrané zařízení
                this.Invoke((MethodInvoker)delegate
                {
                    _selectedDevice = null;
                    _currentConfig = null;
                    UpdateConfigDisplay();
                });
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
            _statusLabel.ForeColor = Color.Blue;
            _scanProgressBar.Visible = true;
            _scanProgressBar.Style = ProgressBarStyle.Marquee; // Animovaný progress bar pro načítání
            _scanProgressBar.MarqueeAnimationSpeed = 30; // Rychlost animace

            try
            {
                // Nejdříve zkusíme získat odpověď přímo pro lepší diagnostiku
                var response = await _client.SendRequestAsync(_selectedDevice.IPAddress, "get_config");
                
                // Debug informace
                DebugLogger.Log($"LoadDeviceConfigAsync: Odpověď serveru - Status: '{response.Status}', Error: '{response.Error}', Data: {response.Data?.GetRawText() ?? "null"}");
                
                if (string.IsNullOrEmpty(response.Status))
                {
                    _scanProgressBar.Style = ProgressBarStyle.Continuous; // Vrátíme zpět na Continuous
                    _scanProgressBar.Visible = false;
                    _statusLabel.Text = $"Server vrátil prázdný status. Zkontrolujte připojení.";
                    _statusLabel.ForeColor = Color.Orange;
                    _refreshButton.Enabled = true;
                    _currentConfig = null;
                    UpdateConfigDisplay();
                    return;
                }
                
                if (response.Status == "error")
                {
                    string errorMsg = response.Error ?? "Neznámá chyba";
                    _scanProgressBar.Style = ProgressBarStyle.Continuous; // Vrátíme zpět na Continuous
                    _scanProgressBar.Visible = false;
                    
                    // ZAKOMENTOVÁNO: Automatické vyhledávání hostname
                    // Pokud je to timeout nebo chyba připojení, spustíme automatické skenování
                    // if (IsConnectionError(errorMsg))
                    // {
                    //     DebugLogger.Log($"LoadDeviceConfigAsync: Server neodpovídá ({errorMsg}). Spouštím automatické skenování sítě.");
                    //     string savedHostname = _selectedDevice?.Hostname ?? "";
                    //     _statusLabel.Text = $"Server neodpovídá. Spouštím skenování sítě pro nalezení zařízení...";
                    //     _statusLabel.ForeColor = Color.Orange;
                    //     _refreshButton.Enabled = true;
                    //     _currentConfig = null;
                    //     UpdateConfigDisplay();
                    //     
                    //     // Spustíme automatické skenování
                    //     await PerformHostnameScanAsync(savedHostname, null);
                    //     return;
                    // }
                    
                    _statusLabel.Text = $"Chyba serveru: {errorMsg}";
                    _statusLabel.ForeColor = Color.Red;
                    _refreshButton.Enabled = true;
                    _currentConfig = null;
                    UpdateConfigDisplay();
                    return;
                }
                
                if (response.Status != "ok")
                {
                    _scanProgressBar.Style = ProgressBarStyle.Continuous; // Vrátíme zpět na Continuous
                    _scanProgressBar.Visible = false;
                    _statusLabel.Text = $"Neočekávaný status serveru: '{response.Status}'. Očekáváno 'ok' nebo 'error'.";
                    _statusLabel.ForeColor = Color.Orange;
                    _refreshButton.Enabled = true;
                    _currentConfig = null;
                    UpdateConfigDisplay();
                    return;
                }

                if (string.IsNullOrEmpty(response.DataJson))
                {
                    _scanProgressBar.Style = ProgressBarStyle.Continuous; // Vrátíme zpět na Continuous
                    _scanProgressBar.Visible = false;
                    _statusLabel.Text = "Server odpověděl OK, ale bez dat. Zkontrolujte kompatibilitu verzí.";
                    _statusLabel.ForeColor = Color.Orange;
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
                    _scanProgressBar.Style = ProgressBarStyle.Continuous; // Vrátíme zpět na Continuous
                    _scanProgressBar.Visible = false;
                    _statusLabel.Text = "Konfigurace načtena úspěšně.";
                    _statusLabel.ForeColor = Color.Green;
                }
                else
                {
                    DebugLogger.Log($"LoadDeviceConfigAsync: Konfigurace se nepodařilo parsovat");
                    _scanProgressBar.Style = ProgressBarStyle.Continuous; // Vrátíme zpět na Continuous
                    _scanProgressBar.Visible = false;
                    _statusLabel.Text = "Server odpověděl, ale data nelze parsovat. Zkontrolujte kompatibilitu verzí.";
                    _statusLabel.ForeColor = Color.Orange;
                    _refreshButton.Enabled = true;
                    _currentConfig = null;
                    UpdateConfigDisplay();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"LoadDeviceConfigAsync: Výjimka: {ex.GetType().Name}: {ex.Message}");
                _scanProgressBar.Style = ProgressBarStyle.Continuous; // Vrátíme zpět na Continuous
                _scanProgressBar.Visible = false;
                
                // ZAKOMENTOVÁNO: Automatické vyhledávání hostname
                // Pokud je to chyba připojení, spustíme automatické skenování
                // if (IsConnectionException(ex))
                // {
                //     DebugLogger.Log($"LoadDeviceConfigAsync: Chyba připojení ({ex.Message}). Spouštím automatické skenování sítě.");
                //     string savedHostname = _selectedDevice?.Hostname ?? "";
                //     _statusLabel.Text = $"Chyba připojení. Spouštím skenování sítě pro nalezení zařízení...";
                //     _statusLabel.ForeColor = Color.Orange;
                //     _refreshButton.Enabled = true;
                //     _currentConfig = null;
                //     UpdateConfigDisplay();
                //     
                //     // Spustíme automatické skenování
                //     await PerformHostnameScanAsync(savedHostname, null);
                //     return;
                // }
                
                _statusLabel.Text = $"Chyba při načítání konfigurace: {ex.Message}";
                _statusLabel.ForeColor = Color.Red;
                _refreshButton.Enabled = true;
                _currentConfig = null;
                UpdateConfigDisplay();
            }
        }

        /// <summary>
        /// Zkontroluje, zda je chybová zpráva typu timeout nebo chyba připojení.
        /// </summary>
        private bool IsConnectionError(string errorMsg)
        {
            if (string.IsNullOrEmpty(errorMsg))
                return false;
            
            string lowerError = errorMsg.ToLowerInvariant();
            return lowerError.Contains("timeout") ||
                   lowerError.Contains("připojení") ||
                   lowerError.Contains("connection") ||
                   lowerError.Contains("nelze se připojit") ||
                   lowerError.Contains("cannot connect") ||
                   lowerError.Contains("spojení uzavřeno") ||
                   lowerError.Contains("connection closed") ||
                   lowerError.Contains("žádná data ze serveru") ||
                   lowerError.Contains("timeout při čtení odpovědi");
        }

        /// <summary>
        /// Zkontroluje, zda je výjimka typu chyba připojení.
        /// </summary>
        private bool IsConnectionException(Exception ex)
        {
            if (ex == null)
                return false;
            
            // SocketException nebo TimeoutException jsou chyby připojení
            if (ex is System.Net.Sockets.SocketException || ex is System.TimeoutException)
                return true;
            
            // Zkontrolujeme také vnitřní výjimky
            if (ex.InnerException != null)
            {
                if (ex.InnerException is System.Net.Sockets.SocketException || 
                    ex.InnerException is System.TimeoutException)
                    return true;
            }
            
            // Zkontrolujeme zprávu výjimky
            string lowerMsg = ex.Message.ToLowerInvariant();
            return lowerMsg.Contains("timeout") ||
                   lowerMsg.Contains("connection") ||
                   lowerMsg.Contains("připojení") ||
                   lowerMsg.Contains("socket");
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
                
                // Nejdřív zjistíme, zda se bude měnit IP adresa
                string? newStaticIp = null;
                string? oldIpAddress = null;
                bool willChangeIp = false;
                
                if (!string.IsNullOrWhiteSpace(_staticIpTextBox.Text))
                {
                    oldIpAddress = _selectedDevice.IPAddress;
                    newStaticIp = _staticIpTextBox.Text.Trim();
                    willChangeIp = (newStaticIp != oldIpAddress);
                }

                // Změna hostname (pouze pokud se nemění IP, protože po změně IP se zařízení restartuje)
                if (!willChangeIp && !string.IsNullOrWhiteSpace(_hostnameTextBox.Text) && 
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

                // Změna hesla uživatele (pouze pokud se nemění IP)
                if (!willChangeIp && !string.IsNullOrWhiteSpace(_passwordTextBox.Text))
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

                // Změna root hesla (pouze pokud se nemění IP)
                if (!willChangeIp && !string.IsNullOrWhiteSpace(_rootPasswordTextBox.Text))
                {
                    DebugLogger.Log("SaveButton_Click: Změna root hesla");
                    var response = await _client.SetRootPasswordAsync(_selectedDevice.IPAddress, _rootPasswordTextBox.Text);
                    if (response.Status != "ok")
                    {
                        success = false;
                        errorMessage += $"Root heslo: {response.Error}; ";
                    }
                }

                // Změna SSH stavu (pouze pokud se nemění IP)
                if (!willChangeIp && _sshEnabledCheckBox.Checked != _currentConfig?.SshEnabled)
                {
                    DebugLogger.Log($"SaveButton_Click: Změna SSH na {_sshEnabledCheckBox.Checked}");
                    var response = await _client.SetSshEnabledAsync(_selectedDevice.IPAddress, _sshEnabledCheckBox.Checked);
                    if (response.Status != "ok")
                    {
                        success = false;
                        errorMessage += $"SSH: {response.Error}; ";
                    }
                }

                // Změna root login stavu (pouze pokud se nemění IP)
                if (!willChangeIp && _rootLoginCheckBox.Checked != _currentConfig?.RootLoginEnabled)
                {
                    DebugLogger.Log($"SaveButton_Click: Změna root login na {_rootLoginCheckBox.Checked}");
                    var response = await _client.SetRootLoginAsync(_selectedDevice.IPAddress, _rootLoginCheckBox.Checked);
                    if (response.Status != "ok")
                    {
                        success = false;
                        errorMessage += $"Root login: {response.Error}; ";
                    }
                }

                // Nastavení statické IP adresy - JAKO POSLEDNÍ, protože po změně IP se zařízení restartuje
                if (!string.IsNullOrWhiteSpace(_staticIpTextBox.Text))
                {
                    DebugLogger.Log($"SaveButton_Click: Nastavení statické IP {newStaticIp}");
                    string netmask = string.IsNullOrWhiteSpace(_netmaskTextBox.Text) ? "255.255.255.0" : _netmaskTextBox.Text.Trim();
                    string? gateway = string.IsNullOrWhiteSpace(_gatewayTextBox.Text) ? null : _gatewayTextBox.Text.Trim();
                    
                    var response = await _client.SetStaticIpAsync(
                        _selectedDevice.IPAddress, 
                        newStaticIp, 
                        netmask, 
                        gateway
                    );
                    
                    // Při změně statické IP se zařízení restartuje, takže timeout je očekávaný
                    // Pokud je timeout, považujeme to za úspěch (požadavek byl odeslán)
                    if (response.Status != "ok")
                    {
                        // Pokud je timeout při změně IP, považujeme to za úspěch (zařízení se restartuje)
                        if (response.Error != null && response.Error.Contains("Timeout"))
                        {
                            DebugLogger.Log($"SaveButton_Click: Timeout při změně statické IP - to je očekávané, zařízení se restartuje. Považuji za úspěch.");
                            // Považujeme to za úspěch, protože požadavek byl odeslán a zařízení se restartuje
                        }
                        else
                        {
                            // Jiná chyba než timeout - skutečná chyba
                            success = false;
                            errorMessage += $"Statická IP: {response.Error}; ";
                            newStaticIp = null; // Nebudeme sledovat změnu, pokud nastavení selhalo
                            willChangeIp = false;
                        }
                    }
                }

                if (success)
                {
                    DebugLogger.Log("SaveButton_Click: Všechny změny úspěšně uloženy");
                    
                    // Pokud byla nastavena statická IP, aktualizujeme vybrané zařízení na novou IP
                    if (newStaticIp != null && oldIpAddress != null && newStaticIp != oldIpAddress)
                    {
                        DebugLogger.Log($"SaveButton_Click: IP adresa se změnila z {oldIpAddress} na {newStaticIp}. Spouštím hledání zařízení na nové IP.");
                        
                        // Zastavíme všechny pokusy o připojení k původní IP
                        StopIpPolling();
                        
                        // Spustíme hledání zařízení na nové IP a podle hostname během 60 sekund
                        string savedHostname = _selectedDevice?.Hostname ?? "";
                        await FindDeviceAfterIpChangeAsync(savedHostname, newStaticIp);
                    }
                    else
                    {
                        _statusLabel.Text = "Změny byly úspěšně uloženy. Načítám aktualizovanou konfiguraci...";
                        // Obnovení konfigurace pokud se IP nezměnila
                        await LoadDeviceConfigAsync();
                    }
                    
                    _passwordTextBox.Text = "";
                    _rootPasswordTextBox.Text = "";
                    _staticIpTextBox.Text = "";
                    _gatewayTextBox.Text = "";
                    
                    // Spustíme nové skenování podle hostname
                    // ZAKOMENTOVÁNO: Automatické vyhledávání hostname po uložení změn
                    // Předáme hostname a novou IP pro automatické vyhledání
                    // DebugLogger.Log("SaveButton_Click: Spouštím nové skenování podle hostname");
                    // await PerformHostnameScanAsync(savedHostname, newIp);
                }
                else
                {
                    DebugLogger.Log($"SaveButton_Click: Chyby při ukládání: {errorMessage}");
                    
                    // ZAKOMENTOVÁNO: Automatické vyhledávání hostname při timeoutu/chybě připojení
                    // Zkontrolujeme, zda je to timeout nebo chyba připojení
                    // if (IsConnectionError(errorMessage))
                    // {
                    //     DebugLogger.Log($"SaveButton_Click: Detekována chyba připojení/timeout. Spouštím automatické skenování hostname.");
                    //     string savedHostname = _selectedDevice?.Hostname ?? "";
                    //     _statusLabel.Text = $"Server neodpovídá. Spouštím skenování sítě pro nalezení zařízení...";
                    //     _statusLabel.ForeColor = Color.Orange;
                    //     
                    //     // Vynulujeme vybrané zařízení, protože se nemůžeme připojit
                    //     _selectedDevice = null;
                    //     _currentConfig = null;
                    //     UpdateConfigDisplay();
                    //     
                    //     // Spustíme automatické skenování
                    //     await PerformHostnameScanAsync(savedHostname, null);
                    // }
                    // else
                    // {
                        _statusLabel.Text = $"Chyba při ukládání: {errorMessage}";
                        _statusLabel.ForeColor = Color.Red;
                    // }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"SaveButton_Click: Výjimka: {ex.GetType().Name}: {ex.Message}");
                
                // ZAKOMENTOVÁNO: Automatické vyhledávání hostname při výjimce
                // Pokud je to chyba připojení, spustíme automatické skenování
                // if (IsConnectionException(ex))
                // {
                //     DebugLogger.Log($"SaveButton_Click: Chyba připojení při ukládání. Spouštím automatické skenování hostname.");
                //     string savedHostname = _selectedDevice?.Hostname ?? "";
                //     _statusLabel.Text = $"Chyba připojení. Spouštím skenování sítě pro nalezení zařízení...";
                //     _statusLabel.ForeColor = Color.Orange;
                //     
                //     // Vynulujeme vybrané zařízení, protože se nemůžeme připojit
                //     _selectedDevice = null;
                //     _currentConfig = null;
                //     UpdateConfigDisplay();
                //     
                //     // Spustíme automatické skenování
                //     await PerformHostnameScanAsync(savedHostname, null);
                // }
                // else
                // {
                    _statusLabel.Text = $"Chyba: {ex.Message}";
                    _statusLabel.ForeColor = Color.Red;
                // }
            }
            finally
            {
                _saveButton.Enabled = true;
                //await PerformHostnameScanAsync();
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
                                        await Task.Run(async () =>
                                        {
                                            try
                                            {
                                                // Nejdříve aktualizujeme IP adresu v zařízení
                                                if (_selectedDevice != null)
                                                {
                                                    this.Invoke((MethodInvoker)delegate
                                                    {
                                                        _selectedDevice.IPAddress = newIpAddress;
                                                        _selectedDevice.Status = "OK";
                                                        
                                                        // Aktualizujeme seznam zařízení
                                                        int selectedIndex = _devicesListBox.SelectedIndex;
                                                        if (selectedIndex >= 0 && selectedIndex < _devicesListBox.Items.Count)
                                                        {
                                                            _devicesListBox.Items[selectedIndex] = _selectedDevice;
                                                        }
                                                        
                                                        _statusLabel.Text = $"IP adresa změněna! Načítám konfiguraci z {newIpAddress}...";
                                                        _statusLabel.ForeColor = Color.Blue;
                                                    });
                                                    
                                                    // Počkáme chvíli, aby se síť stabilizovala
                                                    await Task.Delay(2000); // Zvýšeno na 2 sekundy pro stabilizaci
                                                    
                                                    // Načteme konfiguraci přímo z nové IP adresy
                                                    DebugLogger.Log($"StartIpPolling: Načítám konfiguraci z {newIpAddress}");
                                                    DeviceConfig? newConfig = null;
                                                    
                                                    // Zkusíme načíst konfiguraci několikrát (max 3 pokusy)
                                                    for (int retry = 0; retry < 3; retry++)
                                                    {
                                                        try
                                                        {
                                                            newConfig = await _client.GetConfigAsync(newIpAddress);
                                                            if (newConfig != null)
                                                            {
                                                                DebugLogger.Log($"StartIpPolling: Konfigurace úspěšně načtena na pokus {retry + 1}");
                                                                break;
                                                            }
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            DebugLogger.Log($"StartIpPolling: Pokus {retry + 1} selhal: {ex.Message}");
                                                            if (retry < 2) // Nečekáme po posledním pokusu
                                                            {
                                                                await Task.Delay(1000);
                                                            }
                                                        }
                                                    }
                                                    
                                                    if (newConfig != null)
                                                    {
                                                        DebugLogger.Log($"StartIpPolling: Konfigurace úspěšně načtena z {newIpAddress}");
                                                        DebugLogger.Log($"StartIpPolling: Nová konfigurace - IP: {newConfig.LocalIP}, Hostname: {newConfig.Hostname}");
                                                        
                                                        // Aktualizujeme UI na UI vlákně
                                                        this.Invoke((MethodInvoker)delegate
                                                        {
                                                            _currentConfig = newConfig;
                                                            
                                                            // Explicitně aktualizujeme IP adresu v konfiguraci, pokud server vrátil jinou
                                                            if (newConfig.LocalIP != newIpAddress)
                                                            {
                                                                DebugLogger.Log($"StartIpPolling: Server vrátil IP {newConfig.LocalIP}, očekáváno {newIpAddress}, aktualizuji...");
                                                                newConfig.LocalIP = newIpAddress;
                                                            }
                                                            
                                                            // Vynutíme aktualizaci UI
                                                            UpdateConfigDisplay();
                                                            
                                                            // Zkontrolujeme, zda se IP adresa skutečně zobrazuje
                                                            if (_ipLabel.Tag is Label ipValueLabel)
                                                            {
                                                                DebugLogger.Log($"StartIpPolling: Zobrazená IP v UI: {ipValueLabel.Text}");
                                                                if (ipValueLabel.Text != newIpAddress)
                                                                {
                                                                    DebugLogger.Log($"StartIpPolling: WARNING - IP v UI ({ipValueLabel.Text}) se neshoduje s očekávanou ({newIpAddress}), vynucuji aktualizaci...");
                                                                    ipValueLabel.Text = newIpAddress;
                                                                }
                                                            }
                                                            
                                                            _refreshButton.Enabled = true;
                                                            _saveButton.Enabled = true;
                                                            _statusLabel.Text = $"IP adresa byla úspěšně změněna na {newIpAddress}! Konfigurace načtena.";
                                                            _statusLabel.ForeColor = Color.Green;
                                                            
                                                            DebugLogger.Log($"StartIpPolling: UI aktualizováno, zobrazená IP: {_currentConfig.LocalIP}");
                                                            
                                                            // Vynutíme refresh UI
                                                            this.Refresh();
                                                        });
                                                    }
                                                    else
                                                    {
                                                        DebugLogger.Log($"StartIpPolling: Konfigurace se nepodařilo parsovat z {newIpAddress}");
                                                        this.Invoke((MethodInvoker)delegate
                                                        {
                                                            _statusLabel.Text = $"IP adresa změněna na {newIpAddress}, ale konfigurace se nepodařilo parsovat.";
                                                            _statusLabel.ForeColor = Color.Orange;
                                                        });
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                DebugLogger.Log($"StartIpPolling: Chyba při načítání konfigurace: {ex.Message}");
                                                this.Invoke((MethodInvoker)delegate
                                                {
                                                    _statusLabel.Text = $"IP adresa změněna na {newIpAddress}, ale konfigurace se nepodařilo načíst: {ex.Message}";
                                                    _statusLabel.ForeColor = Color.Orange;
                                                });
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

        /// <summary>
        /// Spustí automatické nové skenování podle hostname po uložení konfigurace.
        /// </summary>
        /// <param name="targetHostname">Hostname zařízení, které má být automaticky vybráno po skenování</param>
        /// <param name="expectedNewIp">Očekávaná nová IP adresa (pokud byla změněna)</param>
        private void StartAutoRescan(string targetHostname = null, string expectedNewIp = null)
        {
            // Zastavíme předchozí polling, pokud běží
            StopIpPolling();

            _ipPollingCancellation = new System.Threading.CancellationTokenSource();
            var cancellationToken = _ipPollingCancellation.Token;

            Task.Run(async () =>
            {
                DebugLogger.Log("StartAutoRescan: Začátek automatického skenování podle hostname");
                
                var startTime = DateTime.Now;
                
                this.Invoke((MethodInvoker)delegate
                {
                    _statusLabel.Text = "Skenování sítě podle hostname... (minimálně 1 minuta)";
                    _statusLabel.ForeColor = Color.Blue;
                    _scanProgressBar.Visible = true;
                    _scanProgressBar.Value = 0;
                    _scanButton.Enabled = false;
                    _scanRangeButton.Enabled = false;
                    _devicesListBox.Items.Clear();
                    _selectedDevice = null;
                    _currentConfig = null;
                    UpdateConfigDisplay();
                });

                try
                {
                    // Spustíme skenování
                    var devices = await _scanner.ScanNetworkAsync();
                    
                    var elapsed = (DateTime.Now - startTime).TotalSeconds;
                    DebugLogger.Log($"StartAutoRescan: Skenování dokončeno za {elapsed:F1}s, nalezeno {devices.Count} zařízení");

                    if (cancellationToken.IsCancellationRequested)
                        return;

                    // Aktualizujeme UI s výsledky
                    this.Invoke((MethodInvoker)delegate
                    {
                        _scanProgressBar.Style = ProgressBarStyle.Continuous;
                        _scanProgressBar.Value = 100;
                        _scanProgressBar.Visible = false;
                        _devicesListBox.Items.Clear();
                        
                        if (devices.Count == 0)
                        {
                            _statusLabel.Text = "Skenování dokončeno. Nebyla nalezena žádná zařízení.";
                            _statusLabel.ForeColor = Color.Orange;
                        }
                        else
                        {
                            // Seřazení podle statusu (OK první, pak ERROR)
                            devices = devices.OrderByDescending(d => d.Status == "OK").ToList();
                            
                            foreach (var device in devices)
                            {
                                _devicesListBox.Items.Add(device);
                            }
                            _statusLabel.Text = $"Skenování dokončeno. Nalezeno {devices.Count} zařízení.";
                            _statusLabel.ForeColor = Color.Green;
                            
                            // Pokud máme cílové hostname nebo očekávanou novou IP, zkusíme najít zařízení
                            NetworkDevice foundDevice = null;
                            
                            if (!string.IsNullOrEmpty(targetHostname))
                            {
                                // Hledáme podle hostname
                                foundDevice = devices.FirstOrDefault(d => 
                                    d.Hostname.Equals(targetHostname, StringComparison.OrdinalIgnoreCase));
                                
                                if (foundDevice != null)
                                {
                                    DebugLogger.Log($"StartAutoRescan: Nalezeno zařízení podle hostname: {foundDevice.Hostname} ({foundDevice.IPAddress})");
                                }
                            }
                            
                            // Pokud máme očekávanou novou IP, zkusíme najít zařízení podle IP
                            if (foundDevice == null && !string.IsNullOrEmpty(expectedNewIp))
                            {
                                foundDevice = devices.FirstOrDefault(d => d.IPAddress == expectedNewIp);
                                
                                if (foundDevice != null)
                                {
                                    DebugLogger.Log($"StartAutoRescan: Nalezeno zařízení podle nové IP: {foundDevice.Hostname} ({foundDevice.IPAddress})");
                                }
                            }
                            
                            // Pokud bylo dříve vybrané zařízení a nenašli jsme podle parametrů, zkusíme podle hostname
                            if (foundDevice == null && _selectedDevice != null && !string.IsNullOrEmpty(_selectedDevice.Hostname))
                            {
                                foundDevice = devices.FirstOrDefault(d => 
                                    d.Hostname.Equals(_selectedDevice.Hostname, StringComparison.OrdinalIgnoreCase));
                                
                                if (foundDevice != null)
                                {
                                    DebugLogger.Log($"StartAutoRescan: Nalezeno zařízení podle původního hostname: {foundDevice.Hostname} ({foundDevice.IPAddress})");
                                }
                            }
                            
                            // Pokud jsme našli zařízení, automaticky ho vybereme a načteme konfiguraci
                            if (foundDevice != null)
                            {
                                int index = _devicesListBox.Items.IndexOf(foundDevice);
                                if (index >= 0)
                                {
                                    _devicesListBox.SelectedIndex = index;
                                    
                                    // Aktualizujeme vybrané zařízení na novou IP adresu
                                    _selectedDevice = foundDevice;
                                    DebugLogger.Log($"StartAutoRescan: Automaticky vybráno zařízení {foundDevice.Hostname} ({foundDevice.IPAddress})");
                                    
                                    // Automaticky načteme konfiguraci z nového zařízení
                                    // Použijeme novou IP adresu místo původní
                                    string newDeviceIp = foundDevice.IPAddress;
                                    Task.Run(async () =>
                                    {
                                        try
                                        {
                                            DebugLogger.Log($"StartAutoRescan: Načítám konfiguraci z {newDeviceIp}");
                                            
                                            // Načteme konfiguraci přímo z nové IP adresy
                                            var response = await _client.SendRequestAsync(newDeviceIp, "get_config");
                                            if (response.Status == "ok")
                                            {
                                                var config = await _client.GetConfigAsync(newDeviceIp);
                                                
                                                this.Invoke((MethodInvoker)delegate
                                                {
                                                    _currentConfig = config;
                                                    UpdateConfigDisplay();
                                                    _statusLabel.Text = $"Zařízení nalezeno a připojeno na {newDeviceIp}. Konfigurace načtena.";
                                                    _statusLabel.ForeColor = Color.Green;
                                                });
                                            }
                                            else
                                            {
                                                // Pokud je to chyba připojení, zobrazíme zprávu, ale nevyhodíme výjimku
                                                // (skenování už běží)
                                                string errorMsg = response.Error ?? "Neznámá chyba";
                                                if (IsConnectionError(errorMsg))
                                                {
                                                    DebugLogger.Log($"StartAutoRescan: Server na {newDeviceIp} neodpovídá ({errorMsg}). Skenování pokračuje.");
                                                    this.Invoke((MethodInvoker)delegate
                                                    {
                                                        _statusLabel.Text = $"Zařízení nalezeno na {newDeviceIp}, ale server neodpovídá. Skenování pokračuje...";
                                                        _statusLabel.ForeColor = Color.Orange;
                                                    });
                                                }
                                                else
                                                {
                                                    throw new Exception(errorMsg);
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            DebugLogger.Log($"StartAutoRescan: Chyba při načítání konfigurace: {ex.Message}");
                                            this.Invoke((MethodInvoker)delegate
                                            {
                                                _statusLabel.Text = $"Zařízení nalezeno na {newDeviceIp}, ale konfigurace se nepodařilo načíst: {ex.Message}";
                                                _statusLabel.ForeColor = Color.Orange;
                                            });
                                        }
                                    });
                                }
                            }
                        }
                        
                        _scanButton.Enabled = true;
                        _scanRangeButton.Enabled = true;
                    });
                    
                    DebugLogger.Log($"StartAutoRescan: Skenování dokončeno, nalezeno {devices.Count} zařízení");
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"StartAutoRescan: Chyba při skenování: {ex.Message}");
                    this.Invoke((MethodInvoker)delegate
                    {
                        _scanProgressBar.Visible = false;
                        _statusLabel.Text = $"Chyba při skenování: {ex.Message}";
                        _statusLabel.ForeColor = Color.Red;
                        _scanButton.Enabled = true;
                        _scanRangeButton.Enabled = true;
                    });
                }
                finally
                {
                    StopIpPolling();
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Stáhne aktuální konfiguraci do JSON souboru.
        /// </summary>
        private void DownloadConfigMenuItem_Click(object? sender, EventArgs e)
        {
            if (_currentConfig == null)
            {
                MessageBox.Show("Nejprve načtěte konfiguraci ze zařízení.", "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "JSON soubory (*.json)|*.json|Všechny soubory (*.*)|*.*";
                saveDialog.FilterIndex = 1;
                saveDialog.DefaultExt = "json";
                saveDialog.FileName = $"device_config_{_currentConfig.Hostname}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                saveDialog.Title = "Uložit konfiguraci";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var options = new System.Text.Json.JsonSerializerOptions
                        {
                            WriteIndented = true,
                            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                        };

                        // Vytvoříme objekt s konfigurací pro uložení
                        var configToSave = new
                        {
                            device = new
                            {
                                ipAddress = _selectedDevice?.IPAddress ?? "",
                                hostname = _currentConfig.Hostname,
                                localIp = _currentConfig.LocalIP,
                                port = _currentConfig.Port,
                                sshEnabled = _currentConfig.SshEnabled,
                                rootLoginEnabled = _currentConfig.RootLoginEnabled,
                                rootPasswordSet = _currentConfig.RootPasswordSet
                            },
                            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        };

                        string json = System.Text.Json.JsonSerializer.Serialize(configToSave, options);
                        System.IO.File.WriteAllText(saveDialog.FileName, json, System.Text.Encoding.UTF8);

                        MessageBox.Show($"Konfigurace byla úspěšně uložena do:\n{saveDialog.FileName}", "Úspěch", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        DebugLogger.Log($"DownloadConfigMenuItem_Click: Konfigurace uložena do {saveDialog.FileName}");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Chyba při ukládání konfigurace:\n{ex.Message}", "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        DebugLogger.Log($"DownloadConfigMenuItem_Click: Chyba při ukládání: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Nahraje konfiguraci z JSON souboru a aplikuje ji na zařízení.
        /// </summary>
        private async void UploadConfigMenuItem_Click(object? sender, EventArgs e)
        {
            if (_selectedDevice == null)
            {
                MessageBox.Show("Nejprve vyberte zařízení ze seznamu.", "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var openDialog = new OpenFileDialog())
            {
                openDialog.Filter = "JSON soubory (*.json)|*.json|Všechny soubory (*.*)|*.*";
                openDialog.FilterIndex = 1;
                openDialog.Title = "Nahrát konfiguraci";

                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string json = System.IO.File.ReadAllText(openDialog.FileName, System.Text.Encoding.UTF8);
                        DebugLogger.Log($"UploadConfigMenuItem_Click: Načítám konfiguraci z {openDialog.FileName}");

                        using (var doc = System.Text.Json.JsonDocument.Parse(json))
                        {
                            var root = doc.RootElement;
                            
                            // Zkusíme najít device objekt
                            if (root.TryGetProperty("device", out var deviceElement))
                            {
                                root = deviceElement;
                            }

                            // Načteme hodnoty z JSON
                            string? hostname = root.TryGetProperty("hostname", out var h) ? h.GetString() : null;
                            string? localIp = root.TryGetProperty("localIp", out var ip) ? ip.GetString() : null;
                            bool? sshEnabled = root.TryGetProperty("sshEnabled", out var ssh) ? ssh.GetBoolean() : null;
                            bool? rootLoginEnabled = root.TryGetProperty("rootLoginEnabled", out var rootLogin) ? rootLogin.GetBoolean() : null;

                            _statusLabel.Text = "Nahrávám konfiguraci ze souboru...";
                            _statusLabel.ForeColor = Color.Blue;

                            // Aplikujeme konfiguraci
                            bool success = true;
                            string errorMessage = "";

                            // Hostname
                            if (!string.IsNullOrEmpty(hostname))
                            {
                                var response = await _client.SetHostnameAsync(_selectedDevice.IPAddress, hostname);
                                if (response.Status != "ok")
                                {
                                    success = false;
                                    errorMessage += $"Hostname: {response.Error}; ";
                                }
                            }

                            // SSH
                            if (sshEnabled.HasValue)
                            {
                                var response = await _client.SetSshEnabledAsync(_selectedDevice.IPAddress, sshEnabled.Value);
                                if (response.Status != "ok")
                                {
                                    success = false;
                                    errorMessage += $"SSH: {response.Error}; ";
                                }
                            }

                            // Root login
                            if (rootLoginEnabled.HasValue)
                            {
                                var response = await _client.SetRootLoginAsync(_selectedDevice.IPAddress, rootLoginEnabled.Value);
                                if (response.Status != "ok")
                                {
                                    success = false;
                                    errorMessage += $"Root login: {response.Error}; ";
                                }
                            }

                            // Statická IP
                            string? oldIpForUpload = null;
                            string? newIpForUpload = null;
                            if (!string.IsNullOrEmpty(localIp) && localIp != _currentConfig?.LocalIP)
                            {
                                oldIpForUpload = _selectedDevice.IPAddress;
                                newIpForUpload = localIp;
                                string netmask = "255.255.255.0"; // Výchozí
                                string? gateway = null;

                                var response = await _client.SetStaticIpAsync(
                                    _selectedDevice.IPAddress,
                                    localIp,
                                    netmask,
                                    gateway
                                );
                                
                                // Při změně statické IP se zařízení restartuje, takže timeout je očekávaný
                                // Pokud je timeout, považujeme to za úspěch (požadavek byl odeslán)
                                if (response.Status != "ok")
                                {
                                    // Pokud je timeout při změně IP, považujeme to za úspěch (zařízení se restartuje)
                                    if (response.Error != null && response.Error.Contains("Timeout"))
                                    {
                                        DebugLogger.Log($"UploadConfigMenuItem_Click: Timeout při změně statické IP - to je očekávané, zařízení se restartuje. Považuji za úspěch.");
                                        // Považujeme to za úspěch, protože požadavek byl odeslán a zařízení se restartuje
                                    }
                                    else
                                    {
                                        // Jiná chyba než timeout - skutečná chyba
                                        success = false;
                                        errorMessage += $"Statická IP: {response.Error}; ";
                                        newIpForUpload = null; // Nebudeme aktualizovat IP, pokud nastavení selhalo
                                    }
                                }
                            }

                            if (success)
                            {
                                // Pokud se změnila IP adresa, spustíme hledání zařízení na nové IP
                                if (newIpForUpload != null && oldIpForUpload != null && newIpForUpload != oldIpForUpload)
                                {
                                    DebugLogger.Log($"UploadConfigMenuItem_Click: IP adresa se změnila z {oldIpForUpload} na {newIpForUpload}. Spouštím hledání zařízení na nové IP.");
                                    
                                    // Spustíme hledání zařízení na nové IP a podle hostname během 60 sekund
                                    string savedHostname = _selectedDevice?.Hostname ?? "";
                                    await FindDeviceAfterIpChangeAsync(savedHostname, newIpForUpload);
                                }
                                else
                                {
                                    _statusLabel.Text = "Konfigurace byla úspěšně nahrána. Načítám aktualizovanou konfiguraci...";
                                    _statusLabel.ForeColor = Color.Green;
                                    
                                    // Načteme aktualizovanou konfiguraci
                                    await LoadDeviceConfigAsync();
                                }
                                
                                MessageBox.Show("Konfigurace byla úspěšně nahrána a aplikována na zařízení.", "Úspěch", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                _statusLabel.Text = $"Chyba při nahrávání konfigurace: {errorMessage}";
                                _statusLabel.ForeColor = Color.Red;
                                MessageBox.Show($"Chyba při nahrávání konfigurace:\n{errorMessage}", "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                    catch (System.Text.Json.JsonException ex)
                    {
                        MessageBox.Show($"Chyba při parsování JSON souboru:\n{ex.Message}", "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        DebugLogger.Log($"UploadConfigMenuItem_Click: Chyba parsování JSON: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Chyba při nahrávání konfigurace:\n{ex.Message}", "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        DebugLogger.Log($"UploadConfigMenuItem_Click: Chyba: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Načte uloženou konfiguraci při startu aplikace a automaticky načte nastavení.
        /// </summary>
        private async void LoadSavedConfigOnStartup()
        {
            try
            {
                string configPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DeviceConfigurator",
                    "last_config.json"
                );

                if (System.IO.File.Exists(configPath))
                {
                    DebugLogger.Log($"LoadSavedConfigOnStartup: Načítám uloženou konfiguraci z {configPath}");
                    
                    string json = System.IO.File.ReadAllText(configPath, System.Text.Encoding.UTF8);

                    using (var doc = System.Text.Json.JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        
                        JsonElement deviceElement;
                        if (root.TryGetProperty("device", out deviceElement))
                        {
                            root = deviceElement;
                        }

                        // Načteme IP adresu pro automatické připojení
                        string? ipAddress = root.TryGetProperty("ipAddress", out var ip) ? ip.GetString() : null;
                        string? hostname = root.TryGetProperty("hostname", out var h) ? h.GetString() : null;

                        if (!string.IsNullOrEmpty(ipAddress))
                        {
                            DebugLogger.Log($"LoadSavedConfigOnStartup: Nalezena uložená IP adresa: {ipAddress}, hostname: {hostname}");
                            
                            // Vytvoříme dočasné zařízení pro automatické načtení
                            var savedDevice = new NetworkDevice
                            {
                                IPAddress = ipAddress,
                                Hostname = hostname ?? "",
                                Status = "OK"
                            };
                            
                            _selectedDevice = savedDevice;
                            
                            // Automaticky načteme konfiguraci
                            _statusLabel.Text = $"Načítám konfiguraci z uloženého zařízení {ipAddress}...";
                            await LoadDeviceConfigAsync();
                            
                            // Přidáme zařízení do seznamu
                            _devicesListBox.Items.Add(savedDevice);
                            _devicesListBox.SelectedItem = savedDevice;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"LoadSavedConfigOnStartup: Chyba při načítání uložené konfigurace: {ex.Message}");
                // Chyba při načítání není kritická, pokračujeme normálně
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopIpPolling();
            
            // Uložíme aktuální konfiguraci při ukončení
            if (_currentConfig != null && _selectedDevice != null)
            {
                try
                {
                    string configDir = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "DeviceConfigurator"
                    );
                    
                    if (!System.IO.Directory.Exists(configDir))
                    {
                        System.IO.Directory.CreateDirectory(configDir);
                    }
                    
                    string configPath = System.IO.Path.Combine(configDir, "last_config.json");
                    
                    var options = new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                    };

                    var configToSave = new
                    {
                        device = new
                        {
                            ipAddress = _selectedDevice.IPAddress,
                            hostname = _currentConfig.Hostname,
                            localIp = _currentConfig.LocalIP,
                            port = _currentConfig.Port,
                            sshEnabled = _currentConfig.SshEnabled,
                            rootLoginEnabled = _currentConfig.RootLoginEnabled,
                            rootPasswordSet = _currentConfig.RootPasswordSet
                        },
                        savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    };

                    string json = System.Text.Json.JsonSerializer.Serialize(configToSave, options);
                    System.IO.File.WriteAllText(configPath, json, System.Text.Encoding.UTF8);
                    DebugLogger.Log($"OnFormClosing: Konfigurace uložena do {configPath}");
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"OnFormClosing: Chyba při ukládání konfigurace: {ex.Message}");
                }
            }
            
            base.OnFormClosing(e);
        }
    }
}

