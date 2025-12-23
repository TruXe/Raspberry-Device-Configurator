using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace DeviceConfigurator
{
    public static class Localization
    {
        public enum Language
        {
            Czech,
            English
        }

        private static Language _currentLanguage = Language.English;

        public static Language CurrentLanguage => _currentLanguage;

        public static void SetLanguage(Language language)
        {
            _currentLanguage = language;
            
            // Nastavíme culture pro aktuální vlákno
            var culture = language == Language.Czech ? new CultureInfo("cs-CZ") : new CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }

        public static void LoadLanguage()
        {
            var settings = AppSettings.LoadSettings();
            // Výchozí jazyk je angličtina, pokud není explicitně nastavena čeština
            var language = (settings.Language != null && settings.Language.Equals("Czech", StringComparison.OrdinalIgnoreCase)) 
                ? Language.Czech 
                : Language.English;
            SetLanguage(language);
        }

        public static string GetString(string key)
        {
            // Jednoduchý slovník pro lokalizaci
            var strings = _currentLanguage == Language.Czech ? _czechStrings : _englishStrings;
            return strings.TryGetValue(key, out var value) ? value : key;
        }

        private static readonly Dictionary<string, string> _czechStrings = new Dictionary<string, string>
        {
            // Form
            { "FormTitle", "Device Configurator - Raspberry Pi" },
            
            // Menu
            { "LanguageMenu", "Jazyk" },
            { "LanguageCzech", "Čeština" },
            { "LanguageEnglish", "English" },
            { "LanguageMenuTooltip", "Nastavení jazyka programu" },
            { "ConfigMenu", "Config" },
            { "ConfigMenuTooltip", "Nahrávání a stahování konfigurace" },
            { "DownloadConfig", "Stáhnout konfiguraci" },
            { "UploadConfig", "Nahrát konfiguraci" },
            { "RaspberryMenu", "Raspberry" },
            { "ServersMenuItem", "Servery" },
            
            // GroupBoxes
            { "DevicesGroupBox", "Nalezená zařízení" },
            { "ScanGroupBox", "Skenování sítě" },
            { "ConfigGroupBox", "Aktuální konfigurace" },
            { "EditGroupBox", "Úprava konfigurace" },
            
            // Labels
            { "DevicesLabel", "Vyberte zařízení ze seznamu:" },
            { "IpRangeLabel", "IP rozsah:" },
            { "IpLabel", "IP adresa:" },
            { "HostnameLabel", "Hostname:" },
            { "PortLabel", "Port:" },
            { "SshStatusLabel", "SSH:" },
            { "RootLoginLabel", "Root login:" },
            { "RootPasswordLabel", "Root heslo:" },
            { "NewHostnameLabel", "Nový hostname:" },
            { "UsernameLabel", "Uživatelské jméno:" },
            { "PasswordLabel", "Heslo uživatele:" },
            { "RootPasswordEditLabel", "Root heslo:" },
            { "StaticIpLabel", "Statická IPv4 adresa:" },
            { "NetmaskLabel", "Maska sítě:" },
            { "GatewayLabel", "Brána (Gateway):" },
            
            // Buttons
            { "ScanButton", "Skenovat podle hostname" },
            { "ScanRangeButton", "Skenovat rozsah" },
            { "SaveButton", "Uložit změny" },
            { "RefreshConfigButton", "Obnovit konfiguraci" },
            { "DebugConsoleButton", "Debug Console" },
            
            // CheckBoxes
            { "SshEnabledCheckBox", "Povolit SSH" },
            { "RootLoginCheckBox", "Povolit SSH root login" },
            
            // Status messages
            { "ReadyStatusMain", "Připraveno. Zadejte IP rozsah nebo klikněte na 'Skenovat síť' pro začátek." },
            { "ScanningNetwork", "Skenování sítě podle hostname..." },
            { "NoDevicesFound", "Nebyla nalezena žádná zařízení." },
            { "LoadingConfig", "Načítání konfigurace..." },
            { "ConfigLoaded", "Konfigurace načtena." },
            { "SavingChanges", "Ukládání změn..." },
            { "ChangesSaved", "Změny byly uloženy." },
            { "ErrorLoadingConfig", "Chyba při načítání konfigurace." },
            { "ErrorSavingConfig", "Chyba při ukládání konfigurace." },
            
            // Dialog messages
            { "SelectDeviceFirst", "Nejprve vyberte zařízení." },
            { "SelectDeviceAndLoadConfig", "Nejprve vyberte zařízení a načtěte konfiguraci." },
            { "ConfigSaved", "Konfigurace byla uložena." },
            { "ConfigLoadedFromFile", "Konfigurace byla načtena ze souboru." },
            { "LanguageChanged", "Jazyk změněn na češtinu. Změna se projeví po restartu aplikace." },
            
            // ServersForm
            { "ServersFormTitle", "Raspberry Servery" },
            { "AddButton", "Přidat" },
            { "EditButton", "Editovat" },
            { "RemoveButton", "Odebrat" },
            { "RefreshServersButton", "Aktualizovat" },
            { "ReadyStatusServers", "Připraveno" },
            
            // ServerEditDialog
            { "ServerEditDialogTitle", "Přidat/Editovat Server" },
            { "ServerNameLabel", "Název:" },
            { "ServerIPLabel", "IP adresa:" },
            { "ServerHostnameLabel", "Hostname:" },
            { "ServerPortLabel", "Port:" },
            { "OKButton", "OK" },
            { "CancelButton", "Zrušit" },
            { "EnterServerName", "Zadejte název serveru." },
            { "EnterIPAddress", "Zadejte IP adresu." },
            { "InvalidIPAddress", "Neplatná IP adresa." },
            
            // Hostname scanning
            { "HostnameScanLabel", "Hostname:" },
            { "EnterHostname", "Zadejte hostname pro skenování." },
            { "UnknownHostname", "Neznámý" },
            
            // Context menu
            { "DownloadConfigMenu", "Stáhnout konfiguraci" },
            { "UploadConfigMenu", "Nahrát konfiguraci" },
            { "ChangeRootPasswordMenu", "Změnit root heslo" },
            
            // Change root password dialog
            { "ChangeRootPasswordTitle", "Změna root hesla" },
            { "ChangeRootPasswordUsernameLabel", "Uživatelské jméno:" },
            { "ChangeRootPasswordUserPasswordLabel", "Heslo uživatele:" },
            { "ChangeRootPasswordNewPasswordLabel", "Nové root heslo:" },
            { "ChangeRootPasswordConfirmLabel", "Potvrzení root hesla:" },
            { "EnterUsername", "Zadejte uživatelské jméno." },
            { "EnterUserPassword", "Zadejte heslo uživatele." },
            { "EnterNewRootPassword", "Zadejte nové root heslo." },
            { "PasswordsDoNotMatch", "Hesla se neshodují." },
            { "ChangingRootPassword", "Měním root heslo..." },
            { "RootPasswordChanged", "Root heslo bylo úspěšně změněno." },
            { "ErrorChangingRootPassword", "Chyba při změně root hesla: {0}" },
            { "Success", "Úspěch" },
        };

        private static readonly Dictionary<string, string> _englishStrings = new Dictionary<string, string>
        {
            // Form
            { "FormTitle", "Device Configurator - Raspberry Pi" },
            
            // Menu
            { "LanguageMenu", "Language" },
            { "LanguageCzech", "Čeština" },
            { "LanguageEnglish", "English" },
            { "LanguageMenuTooltip", "Program language settings" },
            { "ConfigMenu", "Config" },
            { "ConfigMenuTooltip", "Upload and download configuration" },
            { "DownloadConfig", "Download configuration" },
            { "UploadConfig", "Upload configuration" },
            { "RaspberryMenu", "Raspberry" },
            { "ServersMenuItem", "Servers" },
            
            // GroupBoxes
            { "DevicesGroupBox", "Found Devices" },
            { "ScanGroupBox", "Network Scanning" },
            { "ConfigGroupBox", "Current Configuration" },
            { "EditGroupBox", "Edit Configuration" },
            
            // Labels
            { "DevicesLabel", "Select a device from the list:" },
            { "IpRangeLabel", "IP range:" },
            { "IpLabel", "IP address:" },
            { "HostnameLabel", "Hostname:" },
            { "PortLabel", "Port:" },
            { "SshStatusLabel", "SSH:" },
            { "RootLoginLabel", "Root login:" },
            { "RootPasswordLabel", "Root password:" },
            { "NewHostnameLabel", "New hostname:" },
            { "UsernameLabel", "Username:" },
            { "PasswordLabel", "User password:" },
            { "RootPasswordEditLabel", "Root password:" },
            { "StaticIpLabel", "Static IPv4 address:" },
            { "NetmaskLabel", "Netmask:" },
            { "GatewayLabel", "Gateway:" },
            
            // Buttons
            { "ScanButton", "Scan by hostname" },
            { "ScanRangeButton", "Scan range" },
            { "SaveButton", "Save changes" },
            { "RefreshConfigButton", "Refresh configuration" },
            { "DebugConsoleButton", "Debug Console" },
            
            // CheckBoxes
            { "SshEnabledCheckBox", "Enable SSH" },
            { "RootLoginCheckBox", "Enable SSH root login" },
            
            // Status messages
            { "ReadyStatusMain", "Ready. Enter IP range or click 'Scan network' to begin." },
            { "ScanningNetwork", "Scanning network by hostname..." },
            { "NoDevicesFound", "No devices found." },
            { "LoadingConfig", "Loading configuration..." },
            { "ConfigLoaded", "Configuration loaded." },
            { "SavingChanges", "Saving changes..." },
            { "ChangesSaved", "Changes have been saved." },
            { "ErrorLoadingConfig", "Error loading configuration." },
            { "ErrorSavingConfig", "Error saving configuration." },
            { "Error", "Error" },
            
            // Dialog messages
            { "SelectDeviceFirst", "Please select a device first." },
            { "SelectDeviceAndLoadConfig", "Please select a device and load configuration first." },
            { "ConfigSaved", "Configuration has been saved." },
            { "ConfigLoadedFromFile", "Configuration has been loaded from file." },
            { "LanguageChanged", "Language changed to English. The change will take effect after restarting the application." },
            
            // ServersForm
            { "ServersFormTitle", "Raspberry Servers" },
            { "AddButton", "Add" },
            { "EditButton", "Edit" },
            { "RemoveButton", "Remove" },
            { "RefreshServersButton", "Refresh" },
            { "ReadyStatusServers", "Ready" },
            
            // ServerEditDialog
            { "ServerEditDialogTitle", "Add/Edit Server" },
            { "ServerNameLabel", "Name:" },
            { "ServerIPLabel", "IP address:" },
            { "ServerHostnameLabel", "Hostname:" },
            { "ServerPortLabel", "Port:" },
            { "OKButton", "OK" },
            { "CancelButton", "Cancel" },
            { "EnterServerName", "Please enter server name." },
            { "EnterIPAddress", "Please enter IP address." },
            { "InvalidIPAddress", "Invalid IP address." },
            
            // Hostname scanning
            { "HostnameScanLabel", "Hostname:" },
            { "EnterHostname", "Please enter hostname for scanning." },
            { "UnknownHostname", "Unknown" },
            
            // Context menu
            { "DownloadConfigMenu", "Download configuration" },
            { "UploadConfigMenu", "Upload configuration" },
            { "ChangeRootPasswordMenu", "Change root password" },
            
            // Change root password dialog
            { "ChangeRootPasswordTitle", "Change Root Password" },
            { "ChangeRootPasswordUsernameLabel", "Username:" },
            { "ChangeRootPasswordUserPasswordLabel", "User password:" },
            { "ChangeRootPasswordNewPasswordLabel", "New root password:" },
            { "ChangeRootPasswordConfirmLabel", "Confirm root password:" },
            { "EnterUsername", "Please enter username." },
            { "EnterUserPassword", "Please enter user password." },
            { "EnterNewRootPassword", "Please enter new root password." },
            { "PasswordsDoNotMatch", "Passwords do not match." },
            { "ChangingRootPassword", "Changing root password..." },
            { "RootPasswordChanged", "Root password has been changed successfully." },
            { "ErrorChangingRootPassword", "Error changing root password: {0}" },
            { "Success", "Success" },
        };
    }
}

