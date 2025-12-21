# Device Configurator - Raspberry Pi Remote Configuration

Project for remote configuration of Raspberry Pi devices on a local network.

## Architecture

- **Client**: C# WinForms application (.NET 6+)
- **Server**: Python 3 server running on Raspberry Pi
- **Communication**: TCP on port 7777
- **Protocol**: JSON over TCP

## Features

### Supported Operations

- **Network Scanning**: Automatic device discovery by hostname or IP range
- **Configuration Display**: IP address, hostname, SSH status, root login status
- **Hostname Change**: Set new hostname
- **Password Management**: Change password for any user and root
- **SSH Configuration**: Enable/disable SSH, allow root login
- **Static IP Address**: Set static IPv4 address with automatic change tracking
- **Automatic Update**: After IP address change, new configuration is automatically loaded
- **Multi-language Support**: Czech and English language support

## Features

### Debug Functionality

**Server (Python):**
- Debug messages are displayed in logs (`/var/log/raspberry_config_server.log` and `journalctl`)
- Enable/disable: set `DEBUG = True/False` in `raspberry_config_server.py` (line 20)
- Debug messages contain detailed information about:
  - Client connections
  - Received requests
  - Command processing
  - Sent responses

**Client (C#):**
- Debug console for displaying debug messages
- Open: click the **"Debug Console"** button in the main window
- Debug messages include:
  - Network operations (connection, sending, reading)
  - JSON parsing
  - Error states
  - All configuration operations

## Project Structure

```
DC-Source/
├── client/                 # C# WinForms client
│   ├── MainForm.cs        # Main form
│   ├── ConfigClient.cs    # TCP communication
│   ├── NetworkScanner.cs  # Network scanning
│   ├── Localization.cs    # Localization management
│   ├── Resources/         # Localization resources
│   └── README.md          # Client documentation
├── server/                 # Python server
│   ├── raspberry_config_server.py  # Main server
│   ├── raspberry-config-server.service  # Systemd service
│   ├── install.sh         # Installation script
│   └── README.md          # Server documentation
├── docs/                   # Documentation (optional)
├── README.md              # This file
├── LICENSE                # MIT License
└── CONTRIBUTING.md        # Contribution guide
```

## Installation and Running

### Python Server (Raspberry Pi)

#### Requirements

- Raspberry Pi with Raspberry Pi OS (or other Debian-based distribution)
- Python 3.6 or higher
- dhcpcd (network manager for static IP configuration)
- sudo privileges

#### Automatic Installation (recommended)

1. **Clone or download the project:**
   ```bash
   git clone https://github.com/<your-username>/DC-Source.git
   cd DC-Source
   ```

2. **Install dhcpcd (if not installed):**
   ```bash
   sudo apt-get update
   sudo apt-get install -y dhcpcd5
   sudo systemctl enable dhcpcd
   sudo systemctl start dhcpcd
   ```

3. **Run the installation script:**
   ```bash
   cd server
   sudo chmod +x install.sh
   sudo ./install.sh
   ```

The installation script automatically:
- Checks and optionally installs Python 3
- Creates directory `/opt/raspberry-config-server`
- Copies server files
- Sets permissions
- Creates log file
- Installs and starts systemd service

#### Manual Installation

1. **System update:**
   ```bash
   sudo apt-get update
   sudo apt-get upgrade -y
   ```

2. **Install dhcpcd (if not installed):**
   ```bash
   sudo apt-get install -y dhcpcd5
   sudo systemctl enable dhcpcd
   sudo systemctl start dhcpcd
   ```

3. **Check if dhcpcd is active:**
   ```bash
   sudo systemctl status dhcpcd
   ```
   If not active, run:
   ```bash
   sudo systemctl enable dhcpcd
   sudo systemctl start dhcpcd
   ```

4. **Install Python 3 (if not installed):**
   ```bash
   sudo apt-get install -y python3 python3-pip
   ```

5. **Copy files:**
   ```bash
   sudo mkdir -p /opt/raspberry-config-server
   sudo cp raspberry_config_server.py /opt/raspberry-config-server/
   sudo chmod +x /opt/raspberry-config-server/raspberry_config_server.py
   ```

6. **Create log file:**
   ```bash
   sudo touch /var/log/raspberry_config_server.log
   sudo chmod 644 /var/log/raspberry_config_server.log
   ```

7. **Install systemd service:**
   ```bash
   sudo cp raspberry-config-server.service /etc/systemd/system/
   sudo systemctl daemon-reload
   sudo systemctl enable raspberry-config-server
   sudo systemctl start raspberry-config-server
   ```

8. **Check status:**
   ```bash
   sudo systemctl status raspberry-config-server
   ```

9. **View logs (including debug messages):**
   ```bash
   sudo journalctl -u raspberry-config-server -f
   ```

#### Installation Verification

1. **Check if server is running:**
   ```bash
   sudo systemctl is-active raspberry-config-server
   ```
   Should return: `active`

2. **Check if server is listening on port 7777:**
   ```bash
   sudo netstat -tlnp | grep 7777
   ```
   Or:
   ```bash
   sudo ss -tlnp | grep 7777
   ```

3. **Test connection from another computer:**
   ```bash
   telnet <raspberry-pi-ip> 7777
   ```
   Or:
   ```bash
   nc -zv <raspberry-pi-ip> 7777
   ```

#### Troubleshooting Installation

**Problem: dhcpcd is not installed or not active**

```bash
# Install dhcpcd
sudo apt-get install -y dhcpcd5

# Enable and start service
sudo systemctl enable dhcpcd
sudo systemctl start dhcpcd

# Check status
sudo systemctl status dhcpcd
```

**Problem: Server won't start**

```bash
# Check logs
sudo journalctl -u raspberry-config-server -n 50

# Check Python file syntax
sudo python3 -m py_compile /opt/raspberry-config-server/raspberry_config_server.py

# Check permissions
ls -la /opt/raspberry-config-server/
```

**Problem: Port 7777 is already in use**

```bash
# Find which process is using the port
sudo lsof -i :7777
# or
sudo netstat -tlnp | grep 7777

# If it's another process, either stop it or change the port in raspberry_config_server.py
```

### C# Client (Windows)

1. **Requirements:**
   - .NET 6 SDK or higher
   - Windows with WinForms support

2. **Compilation:**
   ```bash
   cd client
   dotnet build
   ```

3. **Running:**
   ```bash
   dotnet run
   ```

## Usage

### Client Application

1. **Start the application** (either from Visual Studio or using `dotnet run`)
2. **Network Scanning:**
   - Click **"Scan by Hostname"** for automatic discovery by hostname (rasp, local, master)
   - Or enter IP range (e.g., `192.168.0.1-255`) and click **"Scan Range"**
3. **Device Selection:**
   - Select device from the list (displays IP address, hostname, and status)
   - Configuration is automatically loaded
4. **Configuration Edit:**
   - Change hostname, passwords, SSH settings, or static IP address
   - For static IP enter: IP address, netmask (default 255.255.255.0), gateway (optional)
5. **Save Changes:**
   - Click **"Save Changes"**
   - If you changed static IP, the application automatically tracks the change and loads new configuration

### Language Selection

The application supports multiple languages:
- **English** (default)
- **Czech**

To change language:
- Go to **Language** menu in the menu bar
- Select **English** or **Čeština**
- The UI will update immediately

### Static IP Address

After setting static IP address:
- Application automatically checks the new IP address every 2 seconds
- After successful change, new configuration is automatically loaded
- Status message shows progress: "Pinging IP 10.0.0.50... (attempt 1, 58s remaining)"
- Timeout is 60 seconds

### Debug Console

- Click the **"Debug Console"** button to open debug window
- Debug window displays all debug messages in real-time
- Use **"Clear"** to clear messages
- **"Auto Scroll"** automatically scrolls to new messages

## Configuration

### Enable/disable debug messages on server

In file `raspberry_config_server.py`:
```python
DEBUG = True  # Enable debug messages
# or
DEBUG = False  # Disable debug messages
```

### Change authentication token

**IMPORTANT**: Change the default authentication token!

**Server** (`raspberry_config_server.py`):
```python
AUTH_TOKEN = "your_new_secure_token"
```

**Client** (`ConfigClient.cs`):
```csharp
private readonly string _authToken = "your_new_secure_token";
```

## Supported Network Services

Server automatically detects and supports various network managers:

- **dhcpcd** (standard for Raspberry Pi OS) - modifies `/etc/dhcpcd.conf`
- **NetworkManager** - uses `nmcli` for configuration
- **systemd-networkd** - prepared for future implementation

## Security Warning

⚠️ **IMPORTANT SECURITY WARNING**

1. **Authentication Token:**
   - Default token (`raspberry_config_secret_2024`) is for testing only!
   - **Always change** the authentication token to a strong, random string
   - Change token in `raspberry_config_server.py` (line 18) and `ConfigClient.cs`

2. **SSH Root Login:**
   - Enabling SSH root login is a **security risk**
   - Use only in trusted local networks

3. **Network Security:**
   - Server listens on `0.0.0.0` (all interfaces)
   - Consider using firewall to limit access
   - We recommend using firewall rule to allow only from trusted IP addresses

4. **Passwords:**
   - Passwords are transmitted over TCP in plaintext (JSON)
   - Use only in trusted local networks
   - For production use, consider implementing TLS/SSL encryption

## Troubleshooting

### Debug messages on server

Check logs:
```bash
sudo journalctl -u raspberry-config-server -f
```

Debug messages start with `DEBUG:` prefix.

### Debug messages on client

1. Open Debug Console by clicking the **"Debug Console"** button
2. Try performing the operation again
3. Look at debug messages in the console

### Server won't start

- Check logs: `sudo journalctl -u raspberry-config-server -n 50`
- Check syntax: `sudo python3 -m py_compile /opt/raspberry-config-server/raspberry_config_server.py`

### Client can't find device

- Check if device is on the same network
- Check firewall on Raspberry Pi
- Use IP range scanning instead of hostname scanning
