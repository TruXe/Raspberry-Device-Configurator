using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DeviceConfigurator
{
    /// <summary>
    /// Klient pro komunikaci s Raspberry Pi konfiguračním serverem přes TCP.
    /// </summary>
    public class ConfigClient
    {
        private readonly string _authToken = "raspberry_config_secret_2024"; // Musí odpovídat serveru!
        private readonly int _port = 7777;
        private readonly int _timeout = 10000; // 10 sekund - zvýšeno pro spolehlivější komunikaci

        /// <summary>
        /// Odešle požadavek na server a vrátí odpověď.
        /// </summary>
        public async Task<ConfigResponse> SendRequestAsync(string ipAddress, string command, object? parameters = null)
        {
            DebugLogger.Log($"SendRequestAsync: Připojování k {ipAddress}:{_port}, příkaz: {command}");
            
            try
            {
                using (var client = new TcpClient())
                {
                    // Připojení s timeoutem
                    DebugLogger.Log($"SendRequestAsync: Vytvářím TCP připojení...");
                    var connectTask = client.ConnectAsync(ipAddress, _port);
                    var timeoutTask = Task.Delay(_timeout);

                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        DebugLogger.Log($"SendRequestAsync: Timeout při připojování");
                        return new ConfigResponse
                        {
                            Status = "error",
                            Error = "Timeout při připojování k serveru"
                        };
                    }

                    await connectTask;
                    DebugLogger.Log($"SendRequestAsync: Připojení úspěšné");

                    // Vytvoření requestu
                    var request = new
                    {
                        command = command,
                        auth_token = _authToken,
                        @params = parameters ?? new { }
                    };

                    string requestJson = JsonSerializer.Serialize(request);
                    byte[] requestBytes = Encoding.UTF8.GetBytes(requestJson);
                    DebugLogger.Log($"SendRequestAsync: Request JSON ({requestBytes.Length} bajtů): {requestJson}");

                    // Odeslání requestu
                    NetworkStream stream = client.GetStream();
                    stream.WriteTimeout = _timeout;
                    stream.ReadTimeout = _timeout;
                    
                    DebugLogger.Log($"SendRequestAsync: Odesílám request...");
                    await stream.WriteAsync(requestBytes, 0, requestBytes.Length);
                    await stream.FlushAsync();
                    DebugLogger.Log($"SendRequestAsync: Request odeslán");

                    // Počkáme krátce, aby server mohl odeslat odpověď
                    await Task.Delay(100);

                    // Čtení odpovědi - Python server pošle všechna data najednou
                    DebugLogger.Log($"SendRequestAsync: Čtu odpověď...");
                    System.IO.MemoryStream responseStream = new System.IO.MemoryStream();
                    byte[] buffer = new byte[4096];
                    int totalBytesRead = 0;
                    
                    try
                    {
                        // První čtení - obvykle obsahuje všechna data
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        DebugLogger.Log($"SendRequestAsync: První čtení: {bytesRead} bajtů");
                        
                        if (bytesRead > 0)
                        {
                            responseStream.Write(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;
                        }
                        
                        // Pokud jsou ještě data dostupná, přečteme je
                        // Zkusíme několikrát, protože data mohou přijít postupně
                        for (int i = 0; i < 5 && totalBytesRead < 8192; i++)
                        {
                            if (stream.DataAvailable)
                            {
                                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                                DebugLogger.Log($"SendRequestAsync: Další čtení #{i + 1}: {bytesRead} bajtů");
                                
                                if (bytesRead == 0)
                                    break;
                                
                                responseStream.Write(buffer, 0, bytesRead);
                                totalBytesRead += bytesRead;
                            }
                            else
                            {
                                // Počkáme krátce na další data
                                await Task.Delay(50);
                                if (!stream.DataAvailable)
                                    break;
                            }
                        }
                        
                        DebugLogger.Log($"SendRequestAsync: Celkem přečteno: {totalBytesRead} bajtů");
                    }
                    catch (System.IO.IOException ex)
                    {
                        DebugLogger.Log($"SendRequestAsync: IOException při čtení: {ex.Message}");
                        // Spojení bylo uzavřeno - to je v pořádku, pokud máme data
                        if (totalBytesRead == 0)
                        {
                            return new ConfigResponse { Status = "error", Error = $"Spojení uzavřeno před přijetím dat: {ex.Message}" };
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log($"SendRequestAsync: Výjimka při čtení: {ex.GetType().Name}: {ex.Message}");
                        if (totalBytesRead == 0)
                        {
                            return new ConfigResponse { Status = "error", Error = $"Chyba při čtení dat: {ex.Message}" };
                        }
                    }
                    
                    if (totalBytesRead == 0)
                    {
                        DebugLogger.Log($"SendRequestAsync: Žádná data ze serveru");
                        return new ConfigResponse { Status = "error", Error = "Žádná data ze serveru" };
                    }
                    
                    string responseJson = Encoding.UTF8.GetString(responseStream.ToArray(), 0, totalBytesRead);
                    DebugLogger.Log($"SendRequestAsync: Přijatá odpověď ({totalBytesRead} bajtů): {responseJson}");

                    // Parsování odpovědi
                    try
                    {
                        using (JsonDocument doc = JsonDocument.Parse(responseJson))
                        {
                            JsonElement root = doc.RootElement;
                            
                            var response = new ConfigResponse();
                            
                            // Extrakce status
                            if (root.TryGetProperty("status", out JsonElement statusElement))
                            {
                                response.Status = statusElement.GetString() ?? string.Empty;
                            }
                            
                            // Extrakce error
                            if (root.TryGetProperty("error", out JsonElement errorElement))
                            {
                                response.Error = errorElement.GetString();
                            }
                            
                            // Extrakce data - uložíme jako JSON string, protože JsonElement je vázán na JsonDocument
                            if (root.TryGetProperty("data", out JsonElement dataElement) && dataElement.ValueKind != JsonValueKind.Null)
                            {
                                // Uložíme JSON string, který pak můžeme parsovat znovu při použití
                                response.DataJson = dataElement.GetRawText();
                            }
                            
                            DebugLogger.Log($"SendRequestAsync: Parsování úspěšné. Status: '{response.Status}', Error: {response.Error ?? "null"}, Data: {(response.DataJson != null ? "ano" : "ne")}");
                            return response;
                        }
                    }
                    catch (JsonException ex)
                    {
                        DebugLogger.Log($"SendRequestAsync: Chyba parsování JSON: {ex.Message}");
                        return new ConfigResponse { Status = "error", Error = $"Chyba parsování JSON: {ex.Message}. Data: {responseJson.Substring(0, Math.Min(200, responseJson.Length))}..." };
                    }
                }
            }
            catch (SocketException ex)
            {
                DebugLogger.Log($"SendRequestAsync: SocketException: {ex.Message} (ErrorCode: {ex.SocketErrorCode})");
                return new ConfigResponse
                {
                    Status = "error",
                    Error = $"Chyba připojení: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"SendRequestAsync: Obecná výjimka: {ex.GetType().Name}: {ex.Message}");
                return new ConfigResponse
                {
                    Status = "error",
                    Error = $"Chyba: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Získá aktuální konfiguraci zařízení.
        /// </summary>
        public async Task<DeviceConfig?> GetConfigAsync(string ipAddress)
        {
            DebugLogger.Log($"GetConfigAsync: Začátek pro {ipAddress}");
            
            var response = await SendRequestAsync(ipAddress, "get_config");

            if (response.Status == "ok")
            {
                try
                {
                    if (string.IsNullOrEmpty(response.DataJson))
                    {
                        DebugLogger.Log("GetConfigAsync: Odpověď neobsahuje data");
                        return null;
                    }

                    // Deserializace dat konfigurace z JSON stringu
                    DebugLogger.Log($"GetConfigAsync: Deserializuji JSON: {response.DataJson}");
                    
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    
                    // Deserializace z JSON stringu
                    var config = JsonSerializer.Deserialize<DeviceConfig>(response.DataJson, options);
                    
                    if (config == null)
                    {
                        DebugLogger.Log("GetConfigAsync: Deserializace vrátila null");
                    }
                    else
                    {
                        DebugLogger.Log($"GetConfigAsync: Konfigurace načtena - IP: {config.LocalIP}, Hostname: {config.Hostname}, SSH: {config.SshEnabled}");
                    }
                    
                    return config;
                }
                catch (JsonException ex)
                {
                    DebugLogger.Log($"GetConfigAsync: Chyba při deserializaci JSON: {ex.Message}");
                    DebugLogger.Log($"GetConfigAsync: JSON data: {response.DataJson ?? "null"}");
                    return null;
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"GetConfigAsync: Obecná chyba při deserializaci: {ex.GetType().Name}: {ex.Message}");
                    DebugLogger.Log($"GetConfigAsync: JSON data: {response.DataJson ?? "null"}");
                    return null;
                }
            }

            if (response.Status == "error")
            {
                DebugLogger.Log($"GetConfigAsync: Chyba serveru: {response.Error}");
            }
            else
            {
                DebugLogger.Log($"GetConfigAsync: Neočekávaný status: {response.Status}");
            }

            return null;
        }

        /// <summary>
        /// Změní hostname zařízení.
        /// </summary>
        public async Task<ConfigResponse> SetHostnameAsync(string ipAddress, string hostname)
        {
            DebugLogger.Log($"SetHostnameAsync: {ipAddress} -> {hostname}");
            return await SendRequestAsync(ipAddress, "set_hostname", new { hostname });
        }

        /// <summary>
        /// Nastaví heslo uživatele.
        /// </summary>
        public async Task<ConfigResponse> SetPasswordAsync(string ipAddress, string username, string password)
        {
            DebugLogger.Log($"SetPasswordAsync: {ipAddress}, uživatel: {username}");
            return await SendRequestAsync(ipAddress, "set_password", new { username, password });
        }

        /// <summary>
        /// Nastaví statickou IPv4 adresu.
        /// </summary>
        public async Task<ConfigResponse> SetStaticIpAsync(string ipAddress, string staticIp, string netmask, string? gateway = null, string[]? dnsServers = null)
        {
            DebugLogger.Log($"SetStaticIpAsync: {ipAddress}, statická IP: {staticIp}, netmask: {netmask}, gateway: {gateway}");
            return await SendRequestAsync(ipAddress, "set_static_ip", new 
            { 
                ip_address = staticIp,
                netmask = netmask,
                gateway = gateway,
                dns_servers = dnsServers ?? new[] { "8.8.8.8", "8.8.4.4" }
            });
        }

        /// <summary>
        /// Nastaví ROOT heslo.
        /// </summary>
        public async Task<ConfigResponse> SetRootPasswordAsync(string ipAddress, string password)
        {
            DebugLogger.Log($"SetRootPasswordAsync: {ipAddress}");
            return await SendRequestAsync(ipAddress, "set_root_password", new { password });
        }

        /// <summary>
        /// Povolí nebo zakáže SSH.
        /// </summary>
        public async Task<ConfigResponse> SetSshEnabledAsync(string ipAddress, bool enabled)
        {
            DebugLogger.Log($"SetSshEnabledAsync: {ipAddress}, enabled: {enabled}");
            return await SendRequestAsync(ipAddress, "set_ssh_enabled", new { enabled });
        }

        /// <summary>
        /// Povolí nebo zakáže SSH root login.
        /// </summary>
        public async Task<ConfigResponse> SetRootLoginAsync(string ipAddress, bool enabled)
        {
            DebugLogger.Log($"SetRootLoginAsync: {ipAddress}, enabled: {enabled}");
            return await SendRequestAsync(ipAddress, "set_root_login", new { enabled });
        }
    }

    /// <summary>
    /// Odpověď ze serveru.
    /// </summary>
    public class ConfigResponse
    {
        public string Status { get; set; } = string.Empty;
        public JsonElement? Data { get; set; } // Pro zpětnou kompatibilitu, ale nepoužívá se
        public string? DataJson { get; set; } // JSON string pro data (nezávislý na JsonDocument)
        public string? Error { get; set; }
    }

    /// <summary>
    /// Konfigurace zařízení.
    /// </summary>
    public class DeviceConfig
    {
        [System.Text.Json.Serialization.JsonPropertyName("local_ip")]
        public string LocalIP { get; set; } = string.Empty;
        
        [System.Text.Json.Serialization.JsonPropertyName("hostname")]
        public string Hostname { get; set; } = string.Empty;
        
        [System.Text.Json.Serialization.JsonPropertyName("port")]
        public int Port { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("ssh_enabled")]
        public bool? SshEnabled { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("root_login_enabled")]
        public bool? RootLoginEnabled { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("root_password_set")]
        public bool RootPasswordSet { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("static_ip")]
        public string? StaticIP { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("netmask")]
        public string? Netmask { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("gateway")]
        public string? Gateway { get; set; }
    }
}

