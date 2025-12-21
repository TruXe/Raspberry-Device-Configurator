#!/bin/bash
#
# Installation script for Raspberry Pi Configuration Server
# This script installs the server and sets up systemd service
#

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Raspberry Pi Configuration Server${NC}"
echo -e "${GREEN}Installation Script${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then 
    echo -e "${RED}Error: Please run as root (use sudo)${NC}"
    exit 1
fi

# Get script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
SERVER_DIR="/opt/raspberry-config-server"

echo -e "${YELLOW}Step 1: Creating installation directory...${NC}"
mkdir -p "$SERVER_DIR"

echo -e "${YELLOW}Step 2: Copying server files...${NC}"
cp "$SCRIPT_DIR/raspberry_config_server.py" "$SERVER_DIR/"
chmod +x "$SERVER_DIR/raspberry_config_server.py"

# Check if Python 3 is installed
if ! command -v python3 &> /dev/null; then
    echo -e "${RED}Error: Python 3 is not installed${NC}"
    echo -e "${YELLOW}Installing Python 3...${NC}"
    apt-get update
    apt-get install -y python3
fi

# Check Python version (should be 3.6+)
PYTHON_VERSION=$(python3 -c 'import sys; print(".".join(map(str, sys.version_info[:2])))')
echo -e "${GREEN}Python version: $PYTHON_VERSION${NC}"

# Create log directory and file
echo -e "${YELLOW}Step 3: Setting up log file...${NC}"
touch /var/log/raspberry_config_server.log
chmod 644 /var/log/raspberry_config_server.log

# Install systemd service
echo -e "${YELLOW}Step 4: Installing systemd service...${NC}"
cp "$SCRIPT_DIR/raspberry-config-server.service" /etc/systemd/system/
systemctl daemon-reload

# Enable service
echo -e "${YELLOW}Step 5: Enabling service...${NC}"
systemctl enable raspberry-config-server.service

# Start service
echo -e "${YELLOW}Step 6: Starting service...${NC}"
systemctl start raspberry-config-server.service

# Wait a moment for service to start
sleep 2

# Check service status
if systemctl is-active --quiet raspberry-config-server; then
    echo -e "${GREEN}✓ Service is running${NC}"
else
    echo -e "${RED}✗ Service failed to start${NC}"
    echo -e "${YELLOW}Checking logs...${NC}"
    journalctl -u raspberry-config-server -n 20 --no-pager
    exit 1
fi

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Installation completed successfully!${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo "Service status:"
systemctl status raspberry-config-server --no-pager -l
echo ""
echo "Useful commands:"
echo "  Check status:    sudo systemctl status raspberry-config-server"
echo "  View logs:       sudo journalctl -u raspberry-config-server -f"
echo "  Restart service: sudo systemctl restart raspberry-config-server"
echo "  Stop service:    sudo systemctl stop raspberry-config-server"
echo ""
echo -e "${YELLOW}IMPORTANT: Change the AUTH_TOKEN in raspberry_config_server.py!${NC}"
echo -e "${YELLOW}Current token is only for testing purposes.${NC}"
echo ""

