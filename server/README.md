# Raspberry Pi Configuration Server

Python 3 server pro vzdálenou konfiguraci Raspberry Pi zařízení.

## Požadavky

- Python 3.6 nebo vyšší
- Raspberry Pi OS nebo Debian/Ubuntu
- Root oprávnění pro systémové změny

## Instalace

### Automatická instalace

```bash
cd server
sudo ./install.sh
```

### Manuální instalace

1. Zkopírujte soubory:
```bash
sudo mkdir -p /opt/raspberry-config-server
sudo cp raspberry_config_server.py /opt/raspberry-config-server/
sudo chmod +x /opt/raspberry-config-server/raspberry_config_server.py
```

2. Nainstalujte systemd službu:
```bash
sudo cp raspberry-config-server.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable raspberry-config-server
sudo systemctl start raspberry-config-server
```

## Správa služby

### Kontrola stavu
```bash
sudo systemctl status raspberry-config-server
```

### Zobrazení logů
```bash
# Všechny logy
sudo journalctl -u raspberry-config-server -f

# Posledních 50 řádků
sudo journalctl -u raspberry-config-server -n 50

# Logy od určitého času
sudo journalctl -u raspberry-config-server --since "1 hour ago"
```

### Restart služby
```bash
sudo systemctl restart raspberry-config-server
```

### Zastavení služby
```bash
sudo systemctl stop raspberry-config-server
```

### Spuštění služby
```bash
sudo systemctl start raspberry-config-server
```

### Zakázání automatického startu
```bash
sudo systemctl disable raspberry-config-server
```

## Konfigurace

### Změna autentizačního tokenu

**DŮLEŽITÉ**: Změňte výchozí token!

Upravte soubor `/opt/raspberry-config-server/raspberry_config_server.py`:
```python
AUTH_TOKEN = "vas_novy_bezpecny_token"
```

Po změně restartujte službu:
```bash
sudo systemctl restart raspberry-config-server
```

### Zapnutí/vypnutí debug zpráv

Upravte soubor `/opt/raspberry-config-server/raspberry_config_server.py`:
```python
DEBUG = True  # Zapnout debug zprávy
# nebo
DEBUG = False  # Vypnout debug zprávy
```

Po změně restartujte službu:
```bash
sudo systemctl restart raspberry-config-server
```

### Změna portu

Upravte soubor `/opt/raspberry-config-server/raspberry_config_server.py`:
```python
SERVER_PORT = 7777  # Změňte na požadovaný port
```

Po změně:
1. Aktualizujte systemd službu (pokud je potřeba)
2. Restartujte službu
3. Otevřete port ve firewallu (pokud používáte)

## Podporované příkazy

Server podporuje následující příkazy:

- `get_config` - Získání aktuální konfigurace
- `set_hostname` - Změna hostname
- `set_password` - Změna hesla uživatele
- `set_root_password` - Změna root hesla
- `set_ssh_enabled` - Zapnutí/vypnutí SSH
- `set_root_login` - Povolení/zakázání SSH root loginu
- `set_static_ip` - Nastavení statické IPv4 adresy

## Podporované síťové služby

Server automaticky detekuje a podporuje:

- **dhcpcd** - Standardní pro Raspberry Pi OS
- **NetworkManager** - Používá `nmcli`
- **systemd-networkd** - Připraveno pro budoucí implementaci

## Logy

Logy jsou uloženy v:
- Systemd journal: `journalctl -u raspberry-config-server`
- Log soubor: `/var/log/raspberry_config_server.log`

## Aktualizace

1. Zkopírujte nový soubor:
```bash
sudo cp raspberry_config_server.py /opt/raspberry-config-server/
```

2. Restartujte službu:
```bash
sudo systemctl restart raspberry-config-server
```

3. Zkontrolujte logy:
```bash
sudo journalctl -u raspberry-config-server -f
```

## Odinstalace

1. Zastavte a zakážte službu:
```bash
sudo systemctl stop raspberry-config-server
sudo systemctl disable raspberry-config-server
```

2. Odstraňte soubory:
```bash
sudo rm /etc/systemd/system/raspberry-config-server.service
sudo rm -rf /opt/raspberry-config-server
sudo systemctl daemon-reload
```

## Řešení problémů

### Služba se nespustí

1. Zkontrolujte logy:
```bash
sudo journalctl -u raspberry-config-server -n 50
```

2. Zkontrolujte syntaxi Python souboru:
```bash
sudo python3 -m py_compile /opt/raspberry-config-server/raspberry_config_server.py
```

3. Zkontrolujte oprávnění:
```bash
ls -la /opt/raspberry-config-server/
```

### Port je již používán

1. Zkontrolujte, co používá port 7777:
```bash
sudo netstat -tulpn | grep 7777
# nebo
sudo ss -tulpn | grep 7777
```

2. Změňte port v konfiguraci nebo zastavte konfliktní službu

### Statická IP se nezměnila

1. Zkontrolujte, která síťová služba běží:
```bash
systemctl is-active dhcpcd
systemctl is-active NetworkManager
systemctl is-active systemd-networkd
```

2. Zkontrolujte logy serveru pro detaily
3. Zkontrolujte konfigurační soubory:
   - dhcpcd: `/etc/dhcpcd.conf`
   - NetworkManager: `nmcli connection show`

