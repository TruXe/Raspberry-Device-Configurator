using System;
using System.Globalization;
using System.Resources;
using System.Threading;

namespace DeviceConfigurator
{
    /// <summary>
    /// Správa lokalizace aplikace.
    /// </summary>
    public static class Localization
    {
        private static ResourceManager _resourceManager;
        private static CultureInfo _currentCulture;
        
        public enum Language
        {
            Czech,
            English
        }
        
        static Localization()
        {
            // Načteme uložený jazyk nebo použijeme výchozí (angličtina)
            var savedLanguage = LoadLanguage();
            _currentCulture = savedLanguage == Language.English ? new CultureInfo("en-US") : new CultureInfo("cs-CZ");
            Thread.CurrentThread.CurrentCulture = _currentCulture;
            Thread.CurrentThread.CurrentUICulture = _currentCulture;
            
            // Inicializujeme ResourceManager
            _resourceManager = new ResourceManager("DeviceConfigurator.Resources.Strings", typeof(Localization).Assembly);
        }
        
        /// <summary>
        /// Nastaví jazyk aplikace.
        /// </summary>
        public static void SetLanguage(Language language)
        {
            switch (language)
            {
                case Language.Czech:
                    _currentCulture = new CultureInfo("cs-CZ");
                    break;
                case Language.English:
                    _currentCulture = new CultureInfo("en-US");
                    break;
            }
            
            Thread.CurrentThread.CurrentCulture = _currentCulture;
            Thread.CurrentThread.CurrentUICulture = _currentCulture;
            
            // Uložíme výběr do nastavení
            var settingsPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DeviceConfigurator",
                "settings.json"
            );
            
            try
            {
                var settingsDir = System.IO.Path.GetDirectoryName(settingsPath);
                if (settingsDir != null && !System.IO.Directory.Exists(settingsDir))
                {
                    System.IO.Directory.CreateDirectory(settingsDir);
                }
                
                var settings = new
                {
                    Language = language.ToString()
                };
                
                string json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(settingsPath, json);
            }
            catch
            {
                // Ignorujeme chyby při ukládání nastavení
            }
        }
        
        /// <summary>
        /// Načte uložený jazyk z nastavení.
        /// </summary>
        public static Language LoadLanguage()
        {
            var settingsPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DeviceConfigurator",
                "settings.json"
            );
            
            try
            {
                if (System.IO.File.Exists(settingsPath))
                {
                    string json = System.IO.File.ReadAllText(settingsPath);
                    using (var doc = System.Text.Json.JsonDocument.Parse(json))
                    {
                        if (doc.RootElement.TryGetProperty("Language", out var langElement))
                        {
                            string? langStr = langElement.GetString();
                            if (langStr != null && Enum.TryParse<Language>(langStr, out var language))
                            {
                                return language;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Pokud se nepodaří načíst, použijeme výchozí
            }
            
            return Language.English; // Výchozí angličtina
        }
        
        /// <summary>
        /// Získá lokalizovaný řetězec.
        /// </summary>
        public static string GetString(string key)
        {
            try
            {
                string? value = _resourceManager.GetString(key, _currentCulture);
                return value ?? key; // Pokud není nalezen, vrátíme klíč
            }
            catch
            {
                return key;
            }
        }
        
        /// <summary>
        /// Získá aktuální jazyk.
        /// </summary>
        public static Language GetCurrentLanguage()
        {
            if (_currentCulture.Name.StartsWith("cs"))
                return Language.Czech;
            else
                return Language.English;
        }
    }
}

