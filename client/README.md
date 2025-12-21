# Device Configurator - C# Client

WinForms aplikace pro vzdálenou konfiguraci Raspberry Pi zařízení.

## Požadavky

- .NET 6 SDK nebo vyšší
- Windows s WinForms podporou
- Visual Studio 2022 nebo novější (volitelné)

## Kompilace

### Pomocí .NET CLI

```bash
cd client
dotnet build
```

### Pomocí Visual Studio

1. Otevřete `DeviceConfigurator.sln` ve Visual Studio
2. Stiskněte F5 nebo klikněte na "Spustit"

## Spuštění

### Pomocí .NET CLI

```bash
cd client
dotnet run
```

### Spuštění zkompilované aplikace

```bash
cd client/bin/Debug/net6.0-windows
./DeviceConfigurator.exe
```

## Použití

1. **Skenování sítě:**
   - Klikněte na "Skenovat síť" pro automatické hledání podle hostname
   - Nebo zadejte IP rozsah (např. `192.168.0.1-255`) a klikněte na "Skenovat IP rozsah"

2. **Výběr zařízení:**
   - Vyberte zařízení ze seznamu
   - Konfigurace se automaticky načte

3. **Úprava konfigurace:**
   - Změňte požadované hodnoty
   - Pro statickou IP zadejte: IP adresu, netmask, gateway (volitelné)

4. **Uložení změn:**
   - Klikněte na "Uložit změny"
   - Pokud jste změnili statickou IP, aplikace automaticky sleduje změnu

## Debug Console

Klikněte na tlačítko "Debug Console" pro zobrazení debug zpráv v reálném čase.

## Konfigurace

### Změna autentizačního tokenu

Upravte soubor `ConfigClient.cs`:
```csharp
private readonly string _authToken = "vas_novy_bezpecny_token";
```

### Změna portu

Upravte soubor `ConfigClient.cs`:
```csharp
private readonly int _port = 7777; // Změňte na požadovaný port
```

## Struktura projektu

- `MainForm.cs` - Hlavní formulář a logika aplikace
- `MainForm.Designer.cs` - UI definice
- `ConfigClient.cs` - TCP komunikace se serverem
- `NetworkScanner.cs` - Skenování sítě a hledání zařízení
- `DebugLogger.cs` - Debug logging
- `DebugConsole.cs` - Debug konzole okno

## Řešení problémů

### Aplikace nenajde zařízení

- Zkontrolujte, zda je zařízení v téže síti
- Zkontrolujte firewall na Raspberry Pi
- Použijte skenování IP rozsahu místo skenování podle hostname
- Otevřete Debug Console pro detailní logy

### Nelze se připojit k serveru

- Zkontrolujte, zda server běží: `sudo systemctl status raspberry-config-server`
- Zkontrolujte, zda je port 7777 otevřený
- Zkontrolujte autentizační token v `ConfigClient.cs`

### Statická IP se nezměnila

- Aplikace automaticky sleduje změnu po dobu 5 minut
- Zkontrolujte Debug Console pro detailní logy
- Zkontrolujte logy serveru na Raspberry Pi

