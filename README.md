# Device Configurator - Raspberry Pi Remote Configuration

Projekt pro vzdálenou konfiguraci Raspberry Pi zařízení v lokální síti.

## Architektura

- **Client**: C# WinForms aplikace (.NET 6+)
- **Server**: Python 3 server běžící na Raspberry Pi
- **Komunikace**: TCP na portu 7777
- **Protokol**: JSON přes TCP

## Funkce

### Podporované operace

- **Skenování sítě**: Automatické hledání zařízení podle hostname nebo IP rozsahu
- **Zobrazení konfigurace**: IP adresa, hostname, SSH status, root login status
- **Změna hostname**: Nastavení nového hostname
- **Správa hesel**: Změna hesla pro libovolného uživatele a root
- **SSH konfigurace**: Zapnutí/vypnutí SSH, povolení root loginu
- **Statická IP adresa**: Nastavení statické IPv4 adresy s automatickým sledováním změny
- **Automatická aktualizace**: Po změně IP adresy se automaticky načte nová konfigurace

## Funkce

### Debug funkcionalita

**Server (Python):**
- Debug zprávy se zobrazují v logu (`/var/log/raspberry_config_server.log` a `journalctl`)
- Zapnutí/vypnutí: nastavte `DEBUG = True/False` v `raspberry_config_server.py` (řádek 20)
- Debug zprávy obsahují detailní informace o:
  - Připojení klientů
  - Přijatých požadavcích
  - Zpracování příkazů
  - Odesílaných odpovědích

**Client (C#):**
- Debug konzole pro zobrazení debug zpráv
- Otevření: klikněte na tlačítko **"Debug Console"** v hlavním okně
- Debug zprávy obsahují:
  - Síťové operace (připojení, odesílání, čtení)
  - Parsování JSON
  - Chybové stavy
  - Všechny operace s konfigurací

## Struktura projektu

```
DC-Source/
├── client/                 # C# WinForms klient
│   ├── MainForm.cs        # Hlavní formulář
│   ├── ConfigClient.cs    # TCP komunikace
│   ├── NetworkScanner.cs  # Skenování sítě
│   └── README.md          # Dokumentace klienta
├── server/                 # Python server
│   ├── raspberry_config_server.py  # Hlavní server
│   ├── raspberry-config-server.service  # Systemd služba
│   ├── install.sh         # Instalační skript
│   └── README.md          # Dokumentace serveru
├── docs/                   # Dokumentace (volitelné)
├── README.md              # Tento soubor
├── LICENSE                # MIT License
└── CONTRIBUTING.md        # Průvodce přispíváním
```

## Instalace a spuštění

### Python Server (Raspberry Pi)

#### Požadavky

- Raspberry Pi s Raspberry Pi OS (nebo jinou Debian-based distribucí)
- Python 3.6 nebo vyšší
- dhcpcd (síťový správce pro statickou IP konfiguraci)
- sudo oprávnění

#### Automatická instalace (doporučeno)

1. **Naklonujte nebo stáhněte projekt:**
   ```bash
   git clone https://github.com/<your-username>/DC-Source.git
   cd DC-Source
   ```

2. **Instalace dhcpcd (pokud není nainstalován):**
   ```bash
   sudo apt-get update
   sudo apt-get install -y dhcpcd5
   sudo systemctl enable dhcpcd
   sudo systemctl start dhcpcd
   ```

3. **Spusťte instalační skript:**
   ```bash
   cd server
   sudo chmod +x install.sh
   sudo ./install.sh
   ```

Instalační skript automaticky:
- Zkontroluje a případně nainstaluje Python 3
- Vytvoří adresář `/opt/raspberry-config-server`
- Zkopíruje server soubory
- Nastaví oprávnění
- Vytvoří log soubor
- Nainstaluje a spustí systemd službu

#### Manuální instalace

1. **Aktualizace systému:**
   ```bash
   sudo apt-get update
   sudo apt-get upgrade -y
   ```

2. **Instalace dhcpcd (pokud není nainstalován):**
   ```bash
   sudo apt-get install -y dhcpcd5
   sudo systemctl enable dhcpcd
   sudo systemctl start dhcpcd
   ```

3. **Kontrola, zda je dhcpcd aktivní:**
   ```bash
   sudo systemctl status dhcpcd
   ```
   Pokud není aktivní, spusťte:
   ```bash
   sudo systemctl enable dhcpcd
   sudo systemctl start dhcpcd
   ```

4. **Instalace Python 3 (pokud není nainstalován):**
   ```bash
   sudo apt-get install -y python3 python3-pip
   ```

5. **Kopírování souborů:**
   ```bash
   sudo mkdir -p /opt/raspberry-config-server
   sudo cp raspberry_config_server.py /opt/raspberry-config-server/
   sudo chmod +x /opt/raspberry-config-server/raspberry_config_server.py
   ```

6. **Vytvoření log souboru:**
   ```bash
   sudo touch /var/log/raspberry_config_server.log
   sudo chmod 644 /var/log/raspberry_config_server.log
   ```

7. **Instalace systemd service:**
   ```bash
   sudo cp raspberry-config-server.service /etc/systemd/system/
   sudo systemctl daemon-reload
   sudo systemctl enable raspberry-config-server
   sudo systemctl start raspberry-config-server
   ```

8. **Kontrola stavu:**
   ```bash
   sudo systemctl status raspberry-config-server
   ```

9. **Zobrazení logů (včetně debug zpráv):**
   ```bash
   sudo journalctl -u raspberry-config-server -f
   ```

#### Ověření instalace

1. **Zkontrolujte, zda server běží:**
   ```bash
   sudo systemctl is-active raspberry-config-server
   ```
   Mělo by vrátit: `active`

2. **Zkontrolujte, zda server naslouchá na portu 7777:**
   ```bash
   sudo netstat -tlnp | grep 7777
   ```
   Nebo:
   ```bash
   sudo ss -tlnp | grep 7777
   ```

3. **Test připojení z jiného počítače:**
   ```bash
   telnet <raspberry-pi-ip> 7777
   ```
   Nebo:
   ```bash
   nc -zv <raspberry-pi-ip> 7777
   ```

#### Řešení problémů s instalací

**Problém: dhcpcd není nainstalován nebo není aktivní**

```bash
# Instalace dhcpcd
sudo apt-get install -y dhcpcd5

# Aktivace a spuštění služby
sudo systemctl enable dhcpcd
sudo systemctl start dhcpcd

# Kontrola stavu
sudo systemctl status dhcpcd
```

**Problém: Server se nespustí**

```bash
# Zkontrolujte logy
sudo journalctl -u raspberry-config-server -n 50

# Zkontrolujte syntaxi Python souboru
sudo python3 -m py_compile /opt/raspberry-config-server/raspberry_config_server.py

# Zkontrolujte oprávnění
ls -la /opt/raspberry-config-server/
```

**Problém: Port 7777 je již používán**

```bash
# Zjistěte, který proces používá port
sudo lsof -i :7777
# nebo
sudo netstat -tlnp | grep 7777

# Pokud je to jiný proces, buď ho zastavte, nebo změňte port v raspberry_config_server.py
```

### C# Client (Windows)

1. **Požadavky:**
   - .NET 6 SDK nebo vyšší
   - Windows s WinForms podporou

2. **Kompilace:**
   ```bash
   cd client
   dotnet build
   ```

3. **Spuštění:**
   ```bash
   dotnet run
   ```

## Použití

### Client aplikace

1. **Spusťte aplikaci** (buď z Visual Studio nebo pomocí `dotnet run`)
2. **Skenování sítě:**
   - Klikněte na **"Skenovat síť"** pro automatické hledání podle hostname (rasp, local, master)
   - Nebo zadejte IP rozsah (např. `192.168.0.1-255`) a klikněte na **"Skenovat IP rozsah"**
3. **Výběr zařízení:**
   - Vyberte zařízení ze seznamu (zobrazí se IP adresa, hostname a status)
   - Konfigurace se automaticky načte
4. **Úprava konfigurace:**
   - Změňte hostname, hesla, SSH nastavení nebo statickou IP adresu
   - Pro statickou IP zadejte: IP adresu, netmask (výchozí 255.255.255.0), gateway (volitelné)
5. **Uložení změn:**
   - Klikněte na **"Uložit změny"**
   - Pokud jste změnili statickou IP, aplikace automaticky sleduje změnu a načte novou konfiguraci

### Statická IP adresa

Po nastavení statické IP adresy:
- Aplikace automaticky kontroluje novou IP adresu každých 5 sekund
- Po úspěšné změně se automaticky načte nová konfigurace
- Status zpráva zobrazuje průběh: "Čekám na změnu IP adresy na 10.0.0.50... (25s)"
- Timeout je 5 minut (60 pokusů)

### Debug Console

- Klikněte na tlačítko **"Debug Console"** pro otevření debug okna
- Debug okno zobrazuje všechny debug zprávy v reálném čase
- Použijte **"Vymazat"** pro vymazání zpráv
- **"Automatické scrollování"** automaticky scrolluje na nové zprávy

## Konfigurace

### Zapnutí/vypnutí debug zpráv na serveru

V souboru `raspberry_config_server.py`:
```python
DEBUG = True  # Zapnout debug zprávy
# nebo
DEBUG = False  # Vypnout debug zprávy
```

### Změna autentizačního tokenu

**DŮLEŽITÉ**: Změňte výchozí autentizační token!

**Server** (`raspberry_config_server.py`):
```python
AUTH_TOKEN = "vas_novy_bezpecny_token"
```

**Client** (`ConfigClient.cs`):
```csharp
private readonly string _authToken = "vas_novy_bezpecny_token";
```

## Podporované síťové služby

Server automaticky detekuje a podporuje různé síťové správce:

- **dhcpcd** (standardní pro Raspberry Pi OS) - upravuje `/etc/dhcpcd.conf`
- **NetworkManager** - používá `nmcli` pro konfiguraci
- **systemd-networkd** - připraveno pro budoucí implementaci

## Bezpečnostní upozornění

⚠️ **DŮLEŽITÉ BEZPEČNOSTNÍ VAROVÁNÍ**

1. **Autentizační token:**
   - Výchozí token (`raspberry_config_secret_2024`) je pouze pro testování!
   - **Vždy změňte** autentizační token na silný, náhodný řetězec
   - Změňte token v `raspberry_config_server.py` (řádek 18) a `ConfigClient.cs`

2. **SSH Root Login:**
   - Povolení SSH root loginu je **bezpečnostní riziko**
   - Používejte pouze v důvěryhodných lokálních sítích

3. **Síťová bezpečnost:**
   - Server naslouchá na `0.0.0.0` (všechny rozhraní)
   - Zvažte použití firewallu pro omezení přístupu
   - Doporučujeme použít firewall pravidlo pro povolení pouze z důvěryhodných IP adres

4. **Hesla:**
   - Hesla se přenášejí přes TCP v plaintextu (JSON)
   - Používejte pouze v důvěryhodných lokálních sítích
   - Pro produkční použití zvažte implementaci TLS/SSL šifrování

## Řešení problémů

### Debug zprávy na serveru

Zkontrolujte logy:
```bash
sudo journalctl -u raspberry-config-server -f
```

Debug zprávy začínají s `DEBUG:` prefixem.

### Debug zprávy na klientovi

1. Otevřete Debug Console kliknutím na tlačítko **"Debug Console"**
2. Zkuste znovu provést operaci
3. Podívejte se na debug zprávy v konzoli

### Server se nespustí

- Zkontrolujte logy: `sudo journalctl -u raspberry-config-server -n 50`
- Zkontrolujte syntaxi: `sudo python3 -m py_compile /opt/raspberry-config-server/raspberry_config_server.py`

### Klient nenajde zařízení

- Zkontrolujte, zda je zařízení v téže síti
- Zkontrolujte firewall na Raspberry Pi
- Použijte skenování IP rozsahu místo skenování podle hostname

