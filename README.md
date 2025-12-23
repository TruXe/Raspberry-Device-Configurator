# Device Configurator - Raspberry Pi Remote Configuration

A comprehensive remote configuration tool for managing Raspberry Pi devices in a local network. This application provides an intuitive Windows-based GUI for discovering, configuring, and monitoring Raspberry Pi devices.

## 🎯 Overview

Device Configurator consists of two main components:
- **Client**: C# WinForms application (.NET 6+) for Windows
- **Server**: Python 3 server running on Raspberry Pi devices

The application enables remote configuration of Raspberry Pi devices including network settings, SSH configuration, user management, and static IP assignment with automatic tracking of IP changes.

## ✨ Key Features

### Network Discovery
- **Hostname-based scanning**: Automatically discovers devices by hostname (rasp, local, master, or custom hostname)
- **IP range scanning**: Scan specific IP ranges (e.g., 192.168.0.1-255)
- **Real-time status indicators**: TreeView with color-coded status dots:
  - 🟢 Green: Device online (ping OK, port 7777 responding)
  - 🟠 Orange: Device reachable (ping OK, but port 7777 not responding)
  - 🔴 Red: Device offline (ping failed)

### Device Configuration
- **Network settings**: Configure static IPv4 address, netmask, and gateway
- **Hostname management**: Change device hostname
- **SSH configuration**: Enable/disable SSH, configure root login
- **User management**: Set user passwords and root password
- **Automatic IP tracking**: After changing static IP, the application automatically detects and connects to the new IP address
- **Context menu actions**: Right-click on devices in TreeView for quick access to:
  - Download configuration (save to JSON file)
  - Upload configuration (load from JSON file)
  - Change root password (via user authentication)

### Server Management
- **Server list**: Manage a list of Raspberry Pi servers
- **Status monitoring**: Automatic status check every 15 seconds
- **Persistent storage**: Server list saved to `C:\RDC\data\configuration\servers.json`
- **CRUD operations**: Add, edit, remove servers with custom names, IPs, hostnames, and ports
- **Context menu actions**: Right-click on servers for:
  - Download configuration
  - Upload configuration
  - Change root password

### User Interface
- **Multi-language support**: Czech and English (default: English)
- **Modern UI**: Clean, organized layout with docked progress bar
- **Debug console**: Real-time debug logging for troubleshooting
- **Configuration import/export**: Save and load device configurations as JSON files
- **Context menus**: Right-click functionality on TreeView nodes for quick actions

### Application Settings
- **Persistent configuration**: Settings stored in `C:\RDC\data\configuration\`
- **Language preference**: Remembers selected language
- **Server list**: Persists across application restarts

## 🏗️ Architecture

### Communication Protocol
- **Protocol**: TCP on port 7777
- **Data format**: JSON over TCP
- **Authentication**: Token-based authentication
- **Timeout**: 60 seconds for read operations

### Network Services Support
The server automatically detects and supports various network managers:
- **dhcpcd** (standard for Raspberry Pi OS) - modifies `/etc/dhcpcd.conf`
- **NetworkManager** - uses `nmcli` for configuration
- **systemd-networkd** - prepared for future implementation

## 📦 Installation

### Python Server (Raspberry Pi)

#### Automatic Installation (Recommended)

1. **Clone or download the project:**
   ```bash
   git clone <repository-url>
   cd DC-Source
   ```

2. **Run the installation script:**
   ```bash
   cd server
   sudo ./install.sh
   ```

The installation script automatically:
- Creates directory `/opt/raspberry-config-server`
- Copies server files
- Sets proper permissions
- Creates log file
- Installs and starts systemd service

#### Manual Installation

1. **Copy files:**
   ```bash
   sudo mkdir -p /opt/raspberry-config-server
   sudo cp raspberry_config_server.py /opt/raspberry-config-server/
   sudo chmod +x /opt/raspberry-config-server/raspberry_config_server.py
   ```

2. **Install systemd service:**
   ```bash
   sudo cp raspberry-config-server.service /etc/systemd/system/
   sudo systemctl daemon-reload
   sudo systemctl enable raspberry-config-server
   sudo systemctl start raspberry-config-server
   ```

3. **Check status:**
   ```bash
   sudo systemctl status raspberry-config-server
   ```

4. **View logs:**
   ```bash
   sudo journalctl -u raspberry-config-server -f
   ```

### C# Client (Windows)

#### Requirements
- .NET 6 SDK or higher
- Windows with WinForms support

#### Build and Run

1. **Build the application:**
   ```bash
   cd client
   dotnet build
   ```

2. **Run the application:**
   ```bash
   dotnet run
   ```

Or open the solution in Visual Studio and run from there.

## 🚀 Usage

### Main Application

1. **Start the application** (from Visual Studio or using `dotnet run`)

2. **Scan for devices:**
   - **By hostname**: Enter a custom hostname (optional) and click "Scan by hostname"
   - **By IP range**: Enter IP range (e.g., `192.168.0.1-255`) and click "Scan range"

3. **Select a device:**
   - Click on a device in the TreeView
   - Configuration will be automatically loaded
   - Status indicators show device availability

4. **Edit configuration:**
   - Change hostname, passwords, SSH settings, or static IP address
   - For static IP: Enter IP address, netmask (default: 255.255.255.0), and gateway (optional)

5. **Save changes:**
   - Click "Save changes"
   - If static IP was changed, the application automatically tracks the IP change and loads new configuration

6. **Context menu actions (right-click on device):**
   - **Download configuration**: Save current device configuration to JSON file
   - **Upload configuration**: Load configuration from JSON file and apply to device
   - **Change root password**: Open dialog to change root password (requires user credentials)

### Server Management

1. **Open server management:**
   - Click "Raspberry" → "Servers" in the menu

2. **Add a server:**
   - Click "Add" button
   - Enter server name, IP address, hostname, and port
   - Click "OK"

3. **Monitor servers:**
   - Server status is automatically checked every 15 seconds
   - Status indicators show online/offline state
   - Click "Refresh" for manual status update

4. **Context menu actions (right-click on server):**
   - **Download configuration**: Download and save server configuration
   - **Upload configuration**: Upload and apply configuration from file
   - **Change root password**: Change root password via user authentication

### Static IP Address Tracking

After setting a static IP address:
- Application automatically pings the new IP address every 5 seconds
- After successful change, new configuration is automatically loaded
- Status message shows progress: "Waiting for IP address change to 10.0.0.50... (25s)"
- Timeout is 5 minutes (60 attempts)

### Configuration Import/Export

1. **Export configuration:**
   - Right-click on device → "Download configuration"
   - Or use menu: "Config" → "Download configuration"
   - Choose save location
   - Configuration is saved as JSON

2. **Import configuration:**
   - Right-click on device → "Upload configuration"
   - Or use menu: "Config" → "Upload configuration"
   - Select JSON file
   - Configuration is applied to selected device

### Change Root Password

1. **Via context menu:**
   - Right-click on device or server in TreeView
   - Select "Change root password"

2. **Enter credentials:**
   - Username (e.g., "rasp")
   - User password
   - New root password
   - Confirm new root password

3. **Apply:**
   - Click "OK" to change root password
   - Application will connect to device and update root password

### Debug Console

- Click "Debug Console" button to open debug window
- Debug window shows all debug messages in real-time
- Use "Clear" to clear messages
- "Auto-scroll" automatically scrolls to new messages

### Language Settings

- Click "Language" in the menu
- Select "Čeština" (Czech) or "English"
- Language change takes effect after application restart
- Default language: English

## ⚙️ Configuration

### Server Configuration

#### Change Authentication Token

**IMPORTANT**: Change the default authentication token!

**Server** (`raspberry_config_server.py`):
```python
AUTH_TOKEN = "your_new_secure_token"
```

**Client** (`ConfigClient.cs`):
```csharp
private readonly string _authToken = "your_new_secure_token";
```

After changing, restart the server:
```bash
sudo systemctl restart raspberry-config-server
```

#### Enable/Disable Debug Messages

In `raspberry_config_server.py`:
```python
DEBUG = True  # Enable debug messages
# or
DEBUG = False  # Disable debug messages
```

After changing, restart the server:
```bash
sudo systemctl restart raspberry-config-server
```

#### Change Port

In `raspberry_config_server.py`:
```python
SERVER_PORT = 7777  # Change to desired port
```

Update client `ConfigClient.cs` accordingly and restart both.

### Client Configuration

#### Application Settings Location
- Settings: `C:\RDC\data\configuration\settings.json`
- Servers: `C:\RDC\data\configuration\servers.json`

Settings are automatically created on first run.

## 🔒 Security Warnings

⚠️ **IMPORTANT SECURITY WARNINGS**

1. **Authentication Token:**
   - Default token (`raspberry_config_secret_2024`) is for testing only!
   - **Always change** the authentication token to a strong, random string
   - Change token in both `raspberry_config_server.py` and `ConfigClient.cs`

2. **SSH Root Login:**
   - Enabling SSH root login is a **security risk**
   - Use only in trusted local networks

3. **Network Security:**
   - Server listens on `0.0.0.0` (all interfaces)
   - Consider using firewall to restrict access
   - Recommended: firewall rule to allow only from trusted IP addresses

4. **Passwords:**
   - Passwords are transmitted over TCP in plaintext (JSON)
   - Use only in trusted local networks
   - For production use, consider implementing TLS/SSL encryption

5. **Root Password Change:**
   - Root password change requires user credentials
   - User credentials are transmitted in plaintext
   - Ensure secure network environment

## 🐛 Troubleshooting

### Server Issues

#### Server won't start
- Check logs: `sudo journalctl -u raspberry-config-server -n 50`
- Check syntax: `sudo python3 -m py_compile /opt/raspberry-config-server/raspberry_config_server.py`
- Verify permissions: `ls -l /opt/raspberry-config-server/raspberry_config_server.py`

#### Server not responding
- Check if service is running: `sudo systemctl status raspberry-config-server`
- Check firewall: `sudo ufw status` or `sudo iptables -L`
- Verify port is open: `sudo netstat -tlnp | grep 7777`

### Client Issues

#### Client can't find devices
- Ensure device is on the same network
- Check firewall on Raspberry Pi
- Use IP range scanning instead of hostname scanning
- Check if port 7777 is accessible: `telnet <raspberry-ip> 7777`

#### Configuration not loading
- Open Debug Console to see detailed error messages
- Verify device is selected in TreeView
- Check network connectivity
- Verify authentication token matches on both client and server

#### Status indicators not updating
- Status check runs every 15 seconds automatically
- Click "Refresh" button for manual update
- Check if device responds to ping
- Verify port 7777 is open on device

#### Context menu not working
- Ensure device/server is selected in TreeView
- Right-click directly on the TreeView node
- Check if device is online (green/orange indicator)

### Debug Information

#### Server Debug Messages
Check logs:
```bash
sudo journalctl -u raspberry-config-server -f
```

Debug messages start with `DEBUG:` prefix.

#### Client Debug Messages
1. Open Debug Console by clicking "Debug Console" button
2. Try the operation again
3. Review debug messages in console

## 📁 Project Structure

```
DC-Source/
├── client/                          # C# WinForms client
│   ├── MainForm.cs                  # Main application form
│   ├── MainForm.Designer.cs         # UI layout definition
│   ├── ConfigClient.cs              # TCP communication with server
│   ├── NetworkScanner.cs            # Network scanning functionality
│   ├── Localization.cs              # Multi-language support
│   ├── AppSettings.cs               # Application settings management
│   ├── ServersForm.cs               # Server management form
│   ├── ServerEditDialog.cs          # Server add/edit dialog
│   ├── ServerInfo.cs                # Server information class
│   ├── ChangeRootPasswordDialog.cs  # Root password change dialog
│   ├── DebugConsole.cs              # Debug console window
│   ├── DebugLogger.cs               # Debug logging utility
│   ├── Program.cs                   # Application entry point
│   └── DeviceConfigurator.csproj   # Project file
├── server/                           # Python server
│   ├── raspberry_config_server.py   # Main server application
│   ├── raspberry-config-server.service  # Systemd service file
│   ├── install.sh                   # Installation script
│   └── README.md                    # Server documentation
├── docs/                             # Additional documentation
├── README.md                         # This file
├── LICENSE                           # MIT License
└── CONTRIBUTING.md                   # Contribution guide
```

## 🔄 Supported Operations

### Server Commands
- `get_config` - Get current device configuration
- `set_hostname` - Change device hostname
- `set_user_password` - Set password for a user
- `set_root_password` - Set root password
- `set_ssh_enabled` - Enable/disable SSH
- `set_root_login` - Enable/disable SSH root login
- `set_static_ip` - Configure static IPv4 address

### Server Response Status
- `ok` - Operation successful
- `error` - Operation failed (with error message)

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🤝 Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.

## 📧 Support

For issues, questions, or contributions, please open an issue on the project repository.

---

**Note**: This application is designed for use in trusted local networks. For production deployments, implement additional security measures such as TLS/SSL encryption and stronger authentication mechanisms.
