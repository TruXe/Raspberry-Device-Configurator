# Jak restartovat službu raspberry-config-server

## Pokud jste změnili service soubor (.service)

1. **Zkopírujte nový service soubor:**
   ```bash
   sudo cp raspberry-config-server.service /etc/systemd/system/
   ```

2. **Znovu načtěte konfiguraci systemd:**
   ```bash
   sudo systemctl daemon-reload
   ```

3. **Restartujte službu:**
   ```bash
   sudo systemctl restart raspberry-config-server
   ```

## Pokud jste změnili Python skript (raspberry_config_server.py)

1. **Zkopírujte nový skript:**
   ```bash
   sudo cp raspberry_config_server.py /opt/raspberry-config-server/
   ```

2. **Restartujte službu:**
   ```bash
   sudo systemctl restart raspberry-config-server
   ```

## Užitečné příkazy pro správu služby

### Zobrazení stavu služby:
```bash
sudo systemctl status raspberry-config-server
```

### Zobrazení logů (včetně debug zpráv):
```bash
sudo journalctl -u raspberry-config-server -f
```

### Zastavení služby:
```bash
sudo systemctl stop raspberry-config-server
```

### Spuštění služby:
```bash
sudo systemctl start raspberry-config-server
```

### Restart služby (bez změny konfigurace):
```bash
sudo systemctl restart raspberry-config-server
```

### Znovu načtení konfigurace (po změně .service souboru):
```bash
sudo systemctl daemon-reload
```

### Povolení automatického spuštění při bootu:
```bash
sudo systemctl enable raspberry-config-server
```

### Zakázání automatického spuštění při bootu:
```bash
sudo systemctl disable raspberry-config-server
```

## Kompletní postup po změně service souboru:

```bash
# 1. Zkopírovat nový service soubor
sudo cp raspberry-config-server.service /etc/systemd/system/

# 2. Znovu načíst konfiguraci
sudo systemctl daemon-reload

# 3. Restartovat službu
sudo systemctl restart raspberry-config-server

# 4. Zkontrolovat stav
sudo systemctl status raspberry-config-server
```

## Kompletní postup po změně Python skriptu:

```bash
# 1. Zkopírovat nový skript
sudo cp raspberry_config_server.py /opt/raspberry-config-server/

# 2. Restartovat službu
sudo systemctl restart raspberry-config-server

# 3. Zkontrolovat stav
sudo systemctl status raspberry-config-server
```

