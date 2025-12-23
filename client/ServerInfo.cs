using System;

namespace DeviceConfigurator
{
    /// <summary>
    /// Reprezentuje informace o Raspberry Pi serveru.
    /// </summary>
    public class ServerInfo
    {
        public string Name { get; set; } = "";
        public string IPAddress { get; set; } = "";
        public string Hostname { get; set; } = "";
        public int Port { get; set; } = 7777;
        public bool IsOnline { get; set; } = false;
        public DateTime LastChecked { get; set; } = DateTime.MinValue;
    }
}


