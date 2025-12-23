using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace DeviceConfigurator
{
    /// <summary>
    /// Třída pro skenování lokální sítě a hledání zařízení podle hostname.
    /// </summary>
    public class NetworkScanner
    {
        private readonly string[] _targetHostnames = { "rasp", "local", "master" };
        private readonly int _scanTimeout = 2000; // 2 sekundy

        /// <summary>
        /// Skenuje lokální síť a hledá zařízení podle hostname.
        /// </summary>
        /// <param name="customHostname">Volitelný vlastní hostname pro skenování. Pokud je prázdný, použijí se výchozí hostname.</param>
        public async Task<List<NetworkDevice>> ScanNetworkAsync(string? customHostname = null)
        {
            DebugLogger.Log($"ScanNetworkAsync: Začátek skenování podle hostname (custom: {customHostname ?? "none"})");
            var devices = new List<NetworkDevice>();
            var foundIps = new HashSet<string>(); // Abychom neopakovali stejné IP

            // Určení, které hostname použít
            var hostnamesToScan = new List<string>();
            if (!string.IsNullOrWhiteSpace(customHostname))
            {
                hostnamesToScan.Add(customHostname.Trim());
            }
            else
            {
                hostnamesToScan.AddRange(_targetHostnames);
            }

            // 1. Nejdříve zkusíme přímý DNS lookup pro každý target hostname
            DebugLogger.Log($"ScanNetworkAsync: Zkouším přímý DNS lookup pro {hostnamesToScan.Count} hostname(s)");
            foreach (var targetHostname in hostnamesToScan)
            {
                try
                {
                    // Zkusíme bez přípony
                    try
                    {
                        IPAddress[] addresses = await Dns.GetHostAddressesAsync(targetHostname);
                        foreach (var addr in addresses)
                        {
                            // Filtrujeme pouze IPv4 adresy
                            if (addr.AddressFamily != AddressFamily.InterNetwork)
                            {
                                continue;
                            }

                            string ip = addr.ToString();
                            if (!foundIps.Contains(ip))
                            {
                                DebugLogger.Log($"ScanNetworkAsync: Nalezeno přes DNS ({targetHostname}): {ip}");
                                devices.Add(new NetworkDevice
                                {
                                    IPAddress = ip,
                                    Hostname = targetHostname,
                                    Status = "OK"
                                });
                                foundIps.Add(ip);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log($"ScanNetworkAsync: DNS lookup pro {targetHostname} selhal: {ex.Message}");
                    }

                    // Zkusíme s .local příponou
                    try
                    {
                        IPAddress[] addresses = await Dns.GetHostAddressesAsync($"{targetHostname}.local");
                        foreach (var addr in addresses)
                        {
                            // Filtrujeme pouze IPv4 adresy
                            if (addr.AddressFamily != AddressFamily.InterNetwork)
                            {
                                continue;
                            }

                            string ip = addr.ToString();
                            if (!foundIps.Contains(ip))
                            {
                                DebugLogger.Log($"ScanNetworkAsync: Nalezeno přes DNS ({targetHostname}.local): {ip}");
                                devices.Add(new NetworkDevice
                                {
                                    IPAddress = ip,
                                    Hostname = targetHostname,
                                    Status = "OK"
                                });
                                foundIps.Add(ip);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log($"ScanNetworkAsync: DNS lookup pro {targetHostname}.local selhal: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"ScanNetworkAsync: Chyba při DNS lookup pro {targetHostname}: {ex.Message}");
                }
            }

            // 2. Pokud jsme něco našli, zkontrolujeme, zda zařízení skutečně odpovídají
            if (devices.Count > 0)
            {
                DebugLogger.Log($"ScanNetworkAsync: Ověřuji {devices.Count} nalezených zařízení pomocí ping");
                var verifyTasks = devices.Select(async device =>
                {
                    bool isReachable = await PingHostAsync(device.IPAddress);
                    if (!isReachable)
                    {
                        DebugLogger.Log($"ScanNetworkAsync: Zařízení {device.IPAddress} neodpovídá na ping");
                        return null;
                    }
                    device.Status = "OK";
                    return device;
                }).ToList();

                var verifiedDevices = await Task.WhenAll(verifyTasks);
                devices = verifiedDevices.Where(d => d != null).ToList()!;
            }

            // 3. Pokud jsme nic nenašli přes DNS, zkusíme skenovat lokální síť
            if (devices.Count == 0)
            {
                DebugLogger.Log("ScanNetworkAsync: DNS lookup nenašel zařízení, zkouším skenovat lokální síť");
                var localNetwork = GetLocalNetwork();

                if (localNetwork != null)
                {
                    DebugLogger.Log($"ScanNetworkAsync: Lokální síť: {localNetwork.BaseIP}.x");

                    // Skenujeme pouze část rozsahu (např. 1-50) pro rychlejší skenování
                    var tasks = new List<Task<NetworkDevice?>>();

                    for (int i = 1; i <= 50; i++) // Omezeno na 50 pro rychlejší skenování
                    {
                        string ip = $"{localNetwork.BaseIP}.{i}";
                        if (!foundIps.Contains(ip))
                        {
                            tasks.Add(CheckDeviceAsync(ip, hostnamesToScan));
                        }
                    }

                    DebugLogger.Log($"ScanNetworkAsync: Spuštěno {tasks.Count} úloh pro skenování");
                    var results = await Task.WhenAll(tasks);

                    foreach (var device in results)
                    {
                        if (device != null && !foundIps.Contains(device.IPAddress))
                        {
                            devices.Add(device);
                            foundIps.Add(device.IPAddress);
                        }
                    }
                }
            }

            DebugLogger.Log($"ScanNetworkAsync: Celkem nalezeno {devices.Count} zařízení");
            return devices;
        }

        /// <summary>
        /// Zkontroluje, zda zařízení na dané IP adrese odpovídá hledaným hostname.
        /// </summary>
        private async Task<NetworkDevice?> CheckDeviceAsync(string ipAddress, List<string>? hostnamesToScan = null)
        {
            try
            {
                // Ověření, že jde o IPv4 adresu
                if (!IPAddress.TryParse(ipAddress, out IPAddress? parsedIp) || 
                    parsedIp.AddressFamily != AddressFamily.InterNetwork)
                {
                    DebugLogger.Log($"CheckDeviceAsync: {ipAddress} není IPv4 adresa, ignoruji");
                    return null;
                }

                // 1. Ping test
                if (!await PingHostAsync(ipAddress))
                {
                    DebugLogger.Log($"CheckDeviceAsync: {ipAddress} neodpovídá na ping");
                    return null;
                }

                DebugLogger.Log($"CheckDeviceAsync: {ipAddress} odpovídá na ping, kontroluji hostname");

                // 2. DNS reverse lookup
                string? hostname = await GetHostnameAsync(ipAddress);

                if (hostname != null)
                {
                    DebugLogger.Log($"CheckDeviceAsync: {ipAddress} - reverse DNS: {hostname}");
                    
                    // 3. Kontrola, zda hostname odpovídá hledaným hodnotám (přesná shoda nebo obsahuje)
                    // Pokud je zadán custom hostname, použijeme ho, jinak použijeme výchozí seznam
                    string hostnameLower = hostname.ToLower();
                    var targetsToCheck = hostnamesToScan ?? _targetHostnames.ToList();
                    
                    foreach (var target in targetsToCheck)
                    {
                        string targetLower = target.ToLower();
                        // Přesná shoda nebo hostname začíná targetem (např. "raspberry" obsahuje "rasp")
                        if (hostnameLower == targetLower || 
                            hostnameLower.StartsWith(targetLower) ||
                            hostnameLower.Contains(targetLower))
                        {
                            DebugLogger.Log($"CheckDeviceAsync: Nalezeno zařízení: {ipAddress} ({hostname}) - shoda s {target}");
                            return new NetworkDevice
                            {
                                IPAddress = ipAddress,
                                Hostname = hostname,
                                Status = "OK"
                            };
                        }
                    }
                }
                else
                {
                    DebugLogger.Log($"CheckDeviceAsync: {ipAddress} - reverse DNS selhal");
                }

                // 4. Zkusíme také zkontrolovat, zda zařízení má konfigurační server (port 7777)
                // To je dobrý indikátor, že jde o Raspberry Pi s naším serverem
                bool hasConfigServer = await CheckConfigServerAsync(ipAddress);
                if (hasConfigServer)
                {
                    DebugLogger.Log($"CheckDeviceAsync: {ipAddress} má konfigurační server, přidávám jako zařízení");
                    return new NetworkDevice
                    {
                        IPAddress = ipAddress,
                        Hostname = hostname ?? Localization.GetString("UnknownHostname"),
                        Status = "OK"
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"CheckDeviceAsync: Výjimka pro {ipAddress}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Zkontroluje, zda zařízení má běžící konfigurační server na portu 7777.
        /// </summary>
        private async Task<bool> CheckConfigServerAsync(string ipAddress)
        {
            try
            {
                using (var client = new System.Net.Sockets.TcpClient())
                {
                    var connectTask = client.ConnectAsync(ipAddress, 7777);
                    var timeoutTask = Task.Delay(1000); // 1 sekunda timeout

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

        /// <summary>
        /// Otestuje, zda je host dostupný pomocí ping.
        /// </summary>
        private async Task<bool> PingHostAsync(string ipAddress)
        {
            try
            {
                // Ověření, že jde o IPv4 adresu
                if (!IPAddress.TryParse(ipAddress, out IPAddress? parsedIp) || 
                    parsedIp.AddressFamily != AddressFamily.InterNetwork)
                {
                    DebugLogger.Log($"PingHostAsync: {ipAddress} není IPv4 adresa, ignoruji");
                    return false;
                }

                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(ipAddress, _scanTimeout);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Získá hostname z IP adresy pomocí reverse DNS lookup.
        /// </summary>
        private async Task<string?> GetHostnameAsync(string ipAddress)
        {
            try
            {
                // Ověření, že jde o IPv4 adresu
                if (!IPAddress.TryParse(ipAddress, out IPAddress? parsedIp) || 
                    parsedIp.AddressFamily != AddressFamily.InterNetwork)
                {
                    DebugLogger.Log($"GetHostnameAsync: {ipAddress} není IPv4 adresa, ignoruji");
                    return null;
                }

                IPHostEntry hostEntry = await Dns.GetHostEntryAsync(ipAddress);
                return hostEntry.HostName;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Získá informace o lokální síti.
        /// </summary>
        private NetworkInfo? GetLocalNetwork()
        {
            try
            {
                // Získání všech síťových rozhraní
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

                foreach (var ni in interfaces)
                {
                    // Filtrování pouze aktivních Ethernet/WiFi rozhraní
                    if (ni.OperationalStatus != OperationalStatus.Up ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    IPInterfaceProperties props = ni.GetIPProperties();
                    foreach (UnicastIPAddressInformation addr in props.UnicastAddresses)
                    {
                        // Pouze IPv4 adresy
                        if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                        {
                            continue;
                        }

                        IPAddress ip = addr.Address;
                        IPAddress? mask = addr.IPv4Mask;

                        if (mask != null)
                        {
                            byte[] ipBytes = ip.GetAddressBytes();
                            byte[] maskBytes = mask.GetAddressBytes();
                            byte[] networkBytes = new byte[4];

                            for (int i = 0; i < 4; i++)
                            {
                                networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
                            }

                            string baseIP = $"{networkBytes[0]}.{networkBytes[1]}.{networkBytes[2]}";
                            return new NetworkInfo { BaseIP = baseIP };
                        }
                    }
                }
            }
            catch
            {
                // Ignorujeme chyby
            }

            return null;
        }

        /// <summary>
        /// Skenuje zadaný IP rozsah a testuje dostupnost pomocí ping.
        /// Formát: "192.168.0.1-255" nebo "10.0.0.1-255"
        /// </summary>
        public async Task<List<NetworkDevice>> ScanIpRangeAsync(string ipRange)
        {
            DebugLogger.Log($"ScanIpRangeAsync: Začátek skenování rozsahu {ipRange}");
            var devices = new List<NetworkDevice>();

            try
            {
                // Parsování IP rozsahu (např. "192.168.0.1-255")
                var (baseIp, startRange, endRange) = ParseIpRange(ipRange);
                if (baseIp == null)
                {
                    DebugLogger.Log($"ScanIpRangeAsync: Neplatný formát IP rozsahu: {ipRange}");
                    return devices;
                }

                DebugLogger.Log($"ScanIpRangeAsync: Parsování úspěšné - {baseIp}.{startRange}-{endRange}");

                // Paralelní ping test všech IP adres v rozsahu
                var tasks = new List<Task<NetworkDevice?>>();

                for (int i = startRange; i <= endRange; i++)
                {
                    string ip = $"{baseIp}.{i}";
                    tasks.Add(CheckIpWithPingAsync(ip));
                }

                DebugLogger.Log($"ScanIpRangeAsync: Spuštěno {tasks.Count} úloh pro ping test");
                var results = await Task.WhenAll(tasks);

                foreach (var device in results)
                {
                    if (device != null)
                    {
                        devices.Add(device);
                    }
                }

                int okCount = devices.Count(d => d.Status == "OK");
                int errorCount = devices.Count(d => d.Status == "ERROR");
                DebugLogger.Log($"ScanIpRangeAsync: Dokončeno - {okCount} OK, {errorCount} ERROR (celkem {devices.Count})");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"ScanIpRangeAsync: Výjimka: {ex.GetType().Name}: {ex.Message}");
            }

            return devices;
        }

        /// <summary>
        /// Otestuje IP adresu pomocí ping a vrátí NetworkDevice s statusem.
        /// </summary>
        private async Task<NetworkDevice?> CheckIpWithPingAsync(string ipAddress)
        {
            try
            {
                // Ověření, že jde o IPv4 adresu
                if (!IPAddress.TryParse(ipAddress, out IPAddress? parsedIp) || 
                    parsedIp.AddressFamily != AddressFamily.InterNetwork)
                {
                    DebugLogger.Log($"CheckIpWithPingAsync: {ipAddress} není IPv4 adresa, ignoruji");
                    return null;
                }

                bool isReachable = await PingHostAsync(ipAddress);
                
                // Zkusíme získat hostname (volitelné)
                string? hostname = null;
                if (isReachable)
                {
                    hostname = await GetHostnameAsync(ipAddress);
                }

                return new NetworkDevice
                {
                    IPAddress = ipAddress,
                    Hostname = hostname ?? Localization.GetString("UnknownHostname"),
                    Status = isReachable ? "OK" : "ERROR"
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Parsuje IP rozsah ve formátu "192.168.0.1-255" nebo "10.0.0.1-255".
        /// </summary>
        private (string? baseIp, int startRange, int endRange) ParseIpRange(string ipRange)
        {
            try
            {
                // Odstranění mezer
                ipRange = ipRange.Trim();

                // Hledání poslední tečky a pomlčky
                int lastDotIndex = ipRange.LastIndexOf('.');
                if (lastDotIndex == -1)
                {
                    return (null, 0, 0);
                }

                string baseIp = ipRange.Substring(0, lastDotIndex);
                string rangePart = ipRange.Substring(lastDotIndex + 1);

                // Parsování rozsahu (např. "1-255" nebo jen "255")
                int startRange, endRange;

                if (rangePart.Contains('-'))
                {
                    string[] parts = rangePart.Split('-');
                    if (parts.Length == 2)
                    {
                        startRange = int.Parse(parts[0].Trim());
                        endRange = int.Parse(parts[1].Trim());
                    }
                    else
                    {
                        return (null, 0, 0);
                    }
                }
                else
                {
                    // Pokud není pomlčka, použijeme stejnou hodnotu pro start i end
                    startRange = endRange = int.Parse(rangePart.Trim());
                }

                // Validace rozsahu
                if (startRange < 1 || endRange > 255 || startRange > endRange)
                {
                    return (null, 0, 0);
                }

                return (baseIp, startRange, endRange);
            }
            catch
            {
                return (null, 0, 0);
            }
        }

        private class NetworkInfo
        {
            public string BaseIP { get; set; } = string.Empty;
        }
    }

    /// <summary>
    /// Reprezentuje síťové zařízení nalezené při skenování.
    /// </summary>
    public class NetworkDevice
    {
        public string IPAddress { get; set; } = string.Empty;
        public string Hostname { get; set; } = string.Empty;
        public string Status { get; set; } = "UNKNOWN"; // OK, ERROR, UNKNOWN

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Status) && Status != "UNKNOWN")
            {
                // Pro lepší čitelnost: OK = zeleně, ERROR = červeně (v textu)
                string statusText = Status == "OK" ? "[OK]" : "[ERROR]";
                return $"{statusText} {IPAddress} - {Hostname}";
            }
            return $"{Hostname} ({IPAddress})";
        }
    }
}

