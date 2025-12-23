using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace DeviceConfigurator
{
    /// <summary>
    /// Správa nastavení aplikace a konfigurační složky.
    /// </summary>
    public static class AppSettings
    {
        private static readonly string ConfigDirectory = @"C:\RDC\data\configuration";
        private static readonly string SettingsFilePath = Path.Combine(ConfigDirectory, "settings.json");
        private static readonly string ServersFilePath = Path.Combine(ConfigDirectory, "servers.json");

        static AppSettings()
        {
            // Zajistíme, že konfigurační složka existuje
            if (!Directory.Exists(ConfigDirectory))
            {
                try
                {
                    Directory.CreateDirectory(ConfigDirectory);
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"AppSettings: Chyba při vytváření konfigurační složky: {ex.Message}");
                }
            }
        }

        public static string GetServersFilePath() => ServersFilePath;
        public static string GetSettingsFilePath() => SettingsFilePath;
        public static string GetConfigDirectory() => ConfigDirectory;

        public class Settings
        {
            public string Language { get; set; } = "English";
        }

        public static Settings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"AppSettings.LoadSettings: Chyba při načítání nastavení: {ex.Message}");
            }
            return new Settings();
        }

        public static void SaveSettings(Settings settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"AppSettings.SaveSettings: Chyba při ukládání nastavení: {ex.Message}");
                MessageBox.Show($"Chyba při ukládání nastavení: {ex.Message}", "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

