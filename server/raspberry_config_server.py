#!/usr/bin/env python3
"""
Raspberry Pi Configuration Server
Naslouchá na TCP portu 7777 a přijímá konfigurační požadavky.
"""

import socket
import json
import subprocess
import sys
import os
import time
import logging
from typing import Dict, Any, Optional

# Konfigurace
SERVER_PORT = 7777
AUTH_TOKEN = "raspberry_config_secret_2024"  # Změňte v produkci!
SSH_CONFIG_PATH = "/etc/ssh/sshd_config"
LOG_FILE = "/var/log/raspberry_config_server.log"
DEBUG = True  # Zapnout/vypnout debug zprávy

# Nastavení logování
log_level = logging.DEBUG if DEBUG else logging.INFO
logging.basicConfig(
    level=log_level,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler(LOG_FILE),
        logging.StreamHandler(sys.stdout)
    ]
)
logger = logging.getLogger(__name__)


class ConfigServer:
    """Hlavní třída serveru pro konfiguraci Raspberry Pi."""
    
    def __init__(self, port: int = SERVER_PORT, auth_token: str = AUTH_TOKEN):
        self.port = port
        self.auth_token = auth_token
        self.socket = None
        
    def start(self):
        """Spustí server a začne naslouchat na portu."""
        try:
            self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            self.socket.bind(('0.0.0.0', self.port))
            self.socket.listen(5)
            logger.info(f"Server naslouchá na portu {self.port}")
            if DEBUG:
                logger.debug(f"DEBUG: Server inicializován, port={self.port}, auth_token nastaven")
            
            while True:
                try:
                    client_socket, address = self.socket.accept()
                    logger.info(f"Připojení od {address}")
                    if DEBUG:
                        logger.debug(f"DEBUG: Nové připojení od {address[0]}:{address[1]}")
                    self.handle_client(client_socket, address)
                except Exception as e:
                    logger.error(f"Chyba při zpracování klienta: {e}")
                    if DEBUG:
                        logger.debug(f"DEBUG: Výjimka v accept loop: {type(e).__name__}: {e}")
                    
        except Exception as e:
            logger.error(f"Chyba serveru: {e}")
            if DEBUG:
                logger.debug(f"DEBUG: Kritická chyba serveru: {type(e).__name__}: {e}")
            raise
        finally:
            if self.socket:
                self.socket.close()
    
    def handle_client(self, client_socket: socket.socket, address: tuple):
        """Zpracuje požadavek od klienta."""
        try:
            if DEBUG:
                logger.debug(f"DEBUG: Začátek zpracování klienta {address}")
            
            # Načtení dat
            data = b""
            chunk_count = 0
            while True:
                chunk = client_socket.recv(4096)
                if not chunk:
                    break
                data += chunk
                chunk_count += 1
                if DEBUG:
                    logger.debug(f"DEBUG: Přijat chunk #{chunk_count}, velikost: {len(chunk)} bajtů")
                
                # Kontrola, zda máme kompletní JSON (jednoduchá kontrola)
                try:
                    json.loads(data.decode('utf-8'))
                    if DEBUG:
                        logger.debug(f"DEBUG: Kompletní JSON přijat, celková velikost: {len(data)} bajtů")
                    break
                except json.JSONDecodeError:
                    if DEBUG:
                        logger.debug(f"DEBUG: JSON ještě není kompletní, pokračuji v čtení...")
                    continue
            
            if not data:
                if DEBUG:
                    logger.debug(f"DEBUG: Žádná data od klienta {address}")
                return
            
            if DEBUG:
                logger.debug(f"DEBUG: Celkem přijato {len(data)} bajtů v {chunk_count} chunkech")
            
            # Parsování JSON
            try:
                request = json.loads(data.decode('utf-8'))
                if DEBUG:
                    logger.debug(f"DEBUG: JSON parsován úspěšně. Command: {request.get('command')}, Auth token: {'nastaven' if request.get('auth_token') else 'chybí'}")
            except json.JSONDecodeError as e:
                if DEBUG:
                    logger.debug(f"DEBUG: Chyba parsování JSON: {e}")
                response = self.create_error_response(f"Neplatný JSON: {e}")
                response_json = json.dumps(response)
                client_socket.sendall(response_json.encode('utf-8'))
                if DEBUG:
                    logger.debug(f"DEBUG: Odeslána chybová odpověď: {response_json}")
                return
            
            # Zpracování požadavku
            if DEBUG:
                logger.debug(f"DEBUG: Zpracovávám požadavek: {request.get('command')}")
            response = self.process_request(request)
            
            # Odeslání odpovědi - použijeme sendall pro zajištění odeslání všech dat
            response_json = json.dumps(response)
            response_bytes = response_json.encode('utf-8')
            if DEBUG:
                logger.debug(f"DEBUG: Odesílám odpověď, velikost: {len(response_bytes)} bajtů")
                logger.debug(f"DEBUG: Odpověď: {response_json}")
            
            client_socket.sendall(response_bytes)
            
            # Počkáme, aby klient mohl přečíst data před shutdown
            time.sleep(0.2)
            
            # Shutdown socketu pro write - signalizuje, že už nebudeme posílat data
            try:
                client_socket.shutdown(socket.SHUT_WR)
                if DEBUG:
                    logger.debug(f"DEBUG: Socket shutdown (SHUT_WR) proveden")
            except:
                if DEBUG:
                    logger.debug(f"DEBUG: Socket už může být uzavřen při shutdown")
                pass  # Socket už může být uzavřen
            
        except Exception as e:
            logger.error(f"Chyba při zpracování klienta {address}: {e}")
            if DEBUG:
                logger.debug(f"DEBUG: Výjimka při zpracování klienta: {type(e).__name__}: {e}")
            try:
                error_response = self.create_error_response(str(e))
                error_json = json.dumps(error_response)
                error_bytes = error_json.encode('utf-8')
                client_socket.sendall(error_bytes)
                time.sleep(0.2)
                try:
                    client_socket.shutdown(socket.SHUT_WR)
                except:
                    pass
                if DEBUG:
                    logger.debug(f"DEBUG: Odeslána chybová odpověď: {error_json}")
            except Exception as send_error:
                logger.error(f"Chyba při odesílání chybové odpovědi: {send_error}")
                if DEBUG:
                    logger.debug(f"DEBUG: Chyba při odesílání chybové odpovědi: {type(send_error).__name__}: {send_error}")
        finally:
            # Počkáme ještě chvíli před úplným zavřením
            time.sleep(0.1)
            try:
                client_socket.close()
                if DEBUG:
                    logger.debug(f"DEBUG: Socket uzavřen pro klienta {address}")
            except:
                pass
    
    def process_request(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Zpracuje požadavek a vrátí odpověď."""
        if DEBUG:
            logger.debug(f"DEBUG: process_request - kontrola autentizace")
        
        # Kontrola autentizace
        if request.get('auth_token') != self.auth_token:
            if DEBUG:
                logger.debug(f"DEBUG: Neplatný auth token. Očekáváno: {self.auth_token[:10]}..., obdrženo: {request.get('auth_token', 'chybí')[:10] if request.get('auth_token') else 'chybí'}...")
            return self.create_error_response("Neplatný autentizační token")
        
        command = request.get('command')
        params = request.get('params', {})
        
        if DEBUG:
            logger.debug(f"DEBUG: Příkaz: {command}, Parametry: {params}")
        
        # Zpracování příkazů
        handlers = {
            'get_config': self.get_config,
            'set_hostname': self.set_hostname,
            'set_password': self.set_password,
            'set_root_password': self.set_root_password,
            'set_ssh_enabled': self.set_ssh_enabled,
            'set_root_login': self.set_root_login,
            'set_static_ip': self.set_static_ip,
        }
        
        handler = handlers.get(command)
        if not handler:
            if DEBUG:
                logger.debug(f"DEBUG: Neznámý příkaz: {command}")
            return self.create_error_response(f"Neznámý příkaz: {command}")
        
        try:
            if DEBUG:
                logger.debug(f"DEBUG: Volám handler pro příkaz {command}")
            result = handler(params)
            if DEBUG:
                logger.debug(f"DEBUG: Handler vrátil: status={result.get('status')}")
            return result
        except Exception as e:
            logger.error(f"Chyba při zpracování příkazu {command}: {e}")
            if DEBUG:
                logger.debug(f"DEBUG: Výjimka v handleru {command}: {type(e).__name__}: {e}")
            return self.create_error_response(str(e))
    
    def create_error_response(self, error_message: str) -> Dict[str, Any]:
        """Vytvoří chybovou odpověď."""
        if DEBUG:
            logger.debug(f"DEBUG: Vytvářím chybovou odpověď: {error_message}")
        return {
            "status": "error",
            "error": error_message
        }
    
    def create_success_response(self, data: Dict[str, Any]) -> Dict[str, Any]:
        """Vytvoří úspěšnou odpověď."""
        if DEBUG:
            logger.debug(f"DEBUG: Vytvářím úspěšnou odpověď s daty: {list(data.keys())}")
        return {
            "status": "ok",
            "data": data
        }
    
    # ========== Implementace příkazů ==========
    
    def get_config(self, params: Dict[str, Any]) -> Dict[str, Any]:
        """Získá aktuální konfiguraci zařízení."""
        try:
            if DEBUG:
                logger.debug("DEBUG: get_config - začátek")
            
            # Získání IP adresy
            if DEBUG:
                logger.debug("DEBUG: get_config - získávám IP adresu")
            local_ip = self.get_local_ip()
            if DEBUG:
                logger.debug(f"DEBUG: get_config - IP adresa: {local_ip}")
            
            # Získání hostname
            if DEBUG:
                logger.debug("DEBUG: get_config - získávám hostname")
            hostname = self.get_hostname()
            if DEBUG:
                logger.debug(f"DEBUG: get_config - hostname: {hostname}")
            
            # Získání stavu SSH
            if DEBUG:
                logger.debug("DEBUG: get_config - kontroluji SSH")
            ssh_enabled = self.is_ssh_enabled()
            if DEBUG:
                logger.debug(f"DEBUG: get_config - SSH enabled: {ssh_enabled}")
            
            # Získání stavu root loginu
            if DEBUG:
                logger.debug("DEBUG: get_config - kontroluji root login")
            root_login_enabled = self.is_root_login_enabled()
            if DEBUG:
                logger.debug(f"DEBUG: get_config - root login enabled: {root_login_enabled}")
            
            # Kontrola, zda je root heslo nastaveno - VYPNUTO pro rychlejší načítání
            # root_password_set = self.is_root_password_set()
            root_password_set = False  # Výchozí hodnota bez kontroly
            if DEBUG:
                logger.debug(f"DEBUG: get_config - root password set: {root_password_set} (vypnuto)")
            
            config_data = {
                "local_ip": local_ip,
                "hostname": hostname,
                "port": self.port,
                "ssh_enabled": ssh_enabled,
                "root_login_enabled": root_login_enabled,
                "root_password_set": root_password_set
            }
            
            if DEBUG:
                logger.debug(f"DEBUG: get_config - kompletní data: {config_data}")
            
            return self.create_success_response(config_data)
        except Exception as e:
            if DEBUG:
                logger.debug(f"DEBUG: get_config - výjimka: {type(e).__name__}: {e}")
            return self.create_error_response(f"Chyba při získávání konfigurace: {e}")
    
    def set_hostname(self, params: Dict[str, Any]) -> Dict[str, Any]:
        """Změní hostname zařízení."""
        hostname = params.get('hostname')
        if not hostname:
            return self.create_error_response("Hostname není zadán")
        
        if DEBUG:
            logger.debug(f"DEBUG: set_hostname - nový hostname: {hostname}")
        
        # Validace hostname
        if not self.is_valid_hostname(hostname):
            if DEBUG:
                logger.debug(f"DEBUG: set_hostname - neplatný formát hostname")
            return self.create_error_response("Neplatný formát hostname")
        
        try:
            if DEBUG:
                logger.debug("DEBUG: set_hostname - volám hostnamectl")
            # Změna hostname pomocí hostnamectl
            result = subprocess.run(
                ['sudo', 'hostnamectl', 'set-hostname', hostname],
                capture_output=True,
                text=True,
                check=True
            )
            if DEBUG:
                logger.debug(f"DEBUG: set_hostname - hostnamectl úspěšný")
            
            # Aktualizace /etc/hosts
            self.update_hosts_file(hostname)
            
            logger.info(f"Hostname změněn na: {hostname}")
            return self.create_success_response({
                "message": "Hostname changed successfully",
                "new_hostname": hostname
            })
        except subprocess.CalledProcessError as e:
            if DEBUG:
                logger.debug(f"DEBUG: set_hostname - chyba: {e.stderr}")
            return self.create_error_response(f"Chyba při změně hostname: {e.stderr}")
    
    def set_password(self, params: Dict[str, Any]) -> Dict[str, Any]:
        """Nastaví heslo běžného uživatele."""
        username = params.get('username', 'pi')
        password = params.get('password')
        
        if not password:
            return self.create_error_response("Heslo není zadáno")
        
        if DEBUG:
            logger.debug(f"DEBUG: set_password - uživatel: {username}, heslo nastaveno: {'ano' if password else 'ne'}")
        
        try:
            # Nastavení hesla pomocí chpasswd
            process = subprocess.Popen(
                ['sudo', 'chpasswd'],
                stdin=subprocess.PIPE,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True
            )
            stdout, stderr = process.communicate(input=f"{username}:{password}\n")
            
            if process.returncode != 0:
                if DEBUG:
                    logger.debug(f"DEBUG: set_password - chyba: {stderr}")
                return self.create_error_response(f"Chyba při nastavení hesla: {stderr}")
            
            logger.info(f"Heslo pro uživatele {username} bylo změněno")
            if DEBUG:
                logger.debug(f"DEBUG: set_password - úspěšně")
            return self.create_success_response({
                "message": "Password changed successfully"
            })
        except Exception as e:
            if DEBUG:
                logger.debug(f"DEBUG: set_password - výjimka: {type(e).__name__}: {e}")
            return self.create_error_response(f"Chyba při nastavení hesla: {e}")
    
    def set_root_password(self, params: Dict[str, Any]) -> Dict[str, Any]:
        """Nastaví ROOT heslo."""
        password = params.get('password')
        
        if not password:
            return self.create_error_response("Heslo není zadáno")
        
        if DEBUG:
            logger.debug("DEBUG: set_root_password - nastavuji root heslo")
        
        try:
            # Nastavení root hesla pomocí chpasswd
            process = subprocess.Popen(
                ['sudo', 'chpasswd'],
                stdin=subprocess.PIPE,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True
            )
            stdout, stderr = process.communicate(input=f"root:{password}\n")
            
            if process.returncode != 0:
                if DEBUG:
                    logger.debug(f"DEBUG: set_root_password - chyba: {stderr}")
                return self.create_error_response(f"Chyba při nastavení root hesla: {stderr}")
            
            logger.info("Root heslo bylo změněno")
            if DEBUG:
                logger.debug("DEBUG: set_root_password - úspěšně")
            return self.create_success_response({
                "message": "Root password changed successfully"
            })
        except Exception as e:
            if DEBUG:
                logger.debug(f"DEBUG: set_root_password - výjimka: {type(e).__name__}: {e}")
            return self.create_error_response(f"Chyba při nastavení root hesla: {e}")
    
    def set_ssh_enabled(self, params: Dict[str, Any]) -> Dict[str, Any]:
        """Povolí nebo zakáže SSH."""
        enabled = params.get('enabled', False)
        
        if DEBUG:
            logger.debug(f"DEBUG: set_ssh_enabled - enabled: {enabled}")
        
        try:
            if enabled:
                # Povolení SSH
                if DEBUG:
                    logger.debug("DEBUG: set_ssh_enabled - povoluji SSH")
                subprocess.run(['sudo', 'systemctl', 'enable', 'ssh'], check=True)
                subprocess.run(['sudo', 'systemctl', 'start', 'ssh'], check=True)
                logger.info("SSH byl povolen")
            else:
                # Zakázání SSH
                if DEBUG:
                    logger.debug("DEBUG: set_ssh_enabled - zakazuji SSH")
                subprocess.run(['sudo', 'systemctl', 'stop', 'ssh'], check=True)
                subprocess.run(['sudo', 'systemctl', 'disable', 'ssh'], check=True)
                logger.info("SSH byl zakázán")
            
            if DEBUG:
                logger.debug("DEBUG: set_ssh_enabled - úspěšně")
            return self.create_success_response({
                "message": f"SSH {'enabled' if enabled else 'disabled'} successfully",
                "ssh_enabled": enabled
            })
        except subprocess.CalledProcessError as e:
            if DEBUG:
                logger.debug(f"DEBUG: set_ssh_enabled - chyba: {e}")
            return self.create_error_response(f"Chyba při změně stavu SSH: {e}")
    
    def set_root_login(self, params: Dict[str, Any]) -> Dict[str, Any]:
        """Povolí nebo zakáže SSH root login."""
        enabled = params.get('enabled', False)
        
        if DEBUG:
            logger.debug(f"DEBUG: set_root_login - enabled: {enabled}")
        
        try:
            if DEBUG:
                logger.debug("DEBUG: set_root_login - čtu SSH config")
            # Načtení aktuální konfigurace
            with open(SSH_CONFIG_PATH, 'r') as f:
                lines = f.readlines()
            
            # Úprava konfigurace
            new_lines = []
            permit_root_found = False
            
            for line in lines:
                if line.strip().startswith('PermitRootLogin'):
                    new_lines.append(f"PermitRootLogin {'yes' if enabled else 'no'}\n")
                    permit_root_found = True
                else:
                    new_lines.append(line)
            
            # Pokud nebyl nalezen, přidáme na konec
            if not permit_root_found:
                new_lines.append(f"PermitRootLogin {'yes' if enabled else 'no'}\n")
            
            if DEBUG:
                logger.debug(f"DEBUG: set_root_login - permit_root_found: {permit_root_found}")
            
            # Zápis do dočasného souboru
            temp_file = SSH_CONFIG_PATH + '.tmp'
            with open(temp_file, 'w') as f:
                f.writelines(new_lines)
            
            # Kopírování s sudo
            if DEBUG:
                logger.debug("DEBUG: set_root_login - kopíruji config s sudo")
            subprocess.run(['sudo', 'cp', temp_file, SSH_CONFIG_PATH], check=True)
            os.remove(temp_file)
            
            # Restart SSH služby
            if DEBUG:
                logger.debug("DEBUG: set_root_login - restartuji SSH")
            subprocess.run(['sudo', 'systemctl', 'restart', 'ssh'], check=True)
            
            logger.info(f"SSH root login {'povolen' if enabled else 'zakázán'}")
            if DEBUG:
                logger.debug("DEBUG: set_root_login - úspěšně")
            return self.create_success_response({
                "message": f"Root login {'enabled' if enabled else 'disabled'} successfully",
                "root_login_enabled": enabled
            })
        except Exception as e:
            if DEBUG:
                logger.debug(f"DEBUG: set_root_login - výjimka: {type(e).__name__}: {e}")
            return self.create_error_response(f"Chyba při změně root loginu: {e}")
    
    # ========== Pomocné metody ==========
    
    def get_local_ip(self) -> str:
        """Získá lokální IP adresu."""
        try:
            if DEBUG:
                logger.debug("DEBUG: get_local_ip - zkouším hostname -I")
            # Získání IP adresy pomocí hostname -I
            result = subprocess.run(
                ['hostname', '-I'],
                capture_output=True,
                text=True,
                check=True
            )
            # Vrátí první IP adresu
            ip = result.stdout.strip().split()[0]
            if DEBUG:
                logger.debug(f"DEBUG: get_local_ip - IP z hostname -I: {ip}")
            return ip
        except:
            if DEBUG:
                logger.debug("DEBUG: get_local_ip - fallback na socket")
            # Fallback na socket
            s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            try:
                s.connect(('8.8.8.8', 80))
                ip = s.getsockname()[0]
            except:
                ip = '127.0.0.1'
            finally:
                s.close()
            if DEBUG:
                logger.debug(f"DEBUG: get_local_ip - IP ze socketu: {ip}")
            return ip
    
    def get_hostname(self) -> str:
        """Získá hostname zařízení."""
        try:
            if DEBUG:
                logger.debug("DEBUG: get_hostname - zkouším hostname")
            result = subprocess.run(
                ['hostname'],
                capture_output=True,
                text=True,
                check=True
            )
            hostname = result.stdout.strip()
            if DEBUG:
                logger.debug(f"DEBUG: get_hostname - hostname: {hostname}")
            return hostname
        except:
            hostname = socket.gethostname()
            if DEBUG:
                logger.debug(f"DEBUG: get_hostname - fallback na socket.gethostname(): {hostname}")
            return hostname
    
    def is_ssh_enabled(self) -> bool:
        """Zkontroluje, zda je SSH povoleno."""
        try:
            if DEBUG:
                logger.debug("DEBUG: is_ssh_enabled - kontroluji systemctl")
            result = subprocess.run(
                ['systemctl', 'is-active', 'ssh'],
                capture_output=True,
                text=True
            )
            enabled = result.returncode == 0
            if DEBUG:
                logger.debug(f"DEBUG: is_ssh_enabled - výsledek: {enabled}")
            return enabled
        except Exception as e:
            if DEBUG:
                logger.debug(f"DEBUG: is_ssh_enabled - výjimka: {e}")
            return False
    
    def is_root_login_enabled(self) -> bool:
        """Zkontroluje, zda je povolen SSH root login."""
        try:
            if DEBUG:
                logger.debug(f"DEBUG: is_root_login_enabled - čtu {SSH_CONFIG_PATH}")
            with open(SSH_CONFIG_PATH, 'r') as f:
                content = f.read()
                for line in content.split('\n'):
                    line = line.strip()
                    if line.startswith('PermitRootLogin'):
                        value = line.split()[1].lower()
                        enabled = value in ('yes', 'true', '1')
                        if DEBUG:
                            logger.debug(f"DEBUG: is_root_login_enabled - PermitRootLogin={value}, enabled={enabled}")
                        return enabled
            if DEBUG:
                logger.debug("DEBUG: is_root_login_enabled - PermitRootLogin nenalezen, vracím False")
            return False
        except Exception as e:
            if DEBUG:
                logger.debug(f"DEBUG: is_root_login_enabled - výjimka: {e}")
            return False
    
    def is_root_password_set(self) -> bool:
        """Zkontroluje, zda je nastaveno root heslo."""
        try:
            if DEBUG:
                logger.debug("DEBUG: is_root_password_set - kontroluji /etc/shadow")
            # Kontrola, zda root účet má heslo (pokud je v /etc/shadow)
            result = subprocess.run(
                ['sudo', 'grep', '^root:', '/etc/shadow'],
                capture_output=True,
                text=True
            )
            if result.returncode == 0:
                # Pokud hash není prázdný a není '!', má heslo
                shadow_line = result.stdout.strip()
                if shadow_line:
                    parts = shadow_line.split(':')
                    if len(parts) > 1:
                        password_hash = parts[1]
                        has_password = password_hash not in ('', '!', '*')
                        if DEBUG:
                            logger.debug(f"DEBUG: is_root_password_set - hash: {password_hash[:10]}..., has_password: {has_password}")
                        return has_password
            if DEBUG:
                logger.debug("DEBUG: is_root_password_set - root nenalezen nebo bez hesla")
            return False
        except Exception as e:
            if DEBUG:
                logger.debug(f"DEBUG: is_root_password_set - výjimka: {e}")
            return False
    
    def is_valid_hostname(self, hostname: str) -> bool:
        """Validuje formát hostname."""
        if not hostname or len(hostname) > 253:
            return False
        # Jednoduchá validace - alfanumerické znaky, pomlčky, tečky
        import re
        pattern = r'^[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?)*$'
        return bool(re.match(pattern, hostname))
    
    def set_static_ip(self, params: Dict[str, Any]) -> Dict[str, Any]:
        """Nastaví statickou IPv4 adresu."""
        ip_address = params.get('ip_address')
        netmask = params.get('netmask', '255.255.255.0')
        gateway = params.get('gateway')
        dns_servers = params.get('dns_servers', ['8.8.8.8', '8.8.4.4'])
        
        if not ip_address:
            return self.create_error_response("IP adresa není zadána")
        
        if DEBUG:
            logger.debug(f"DEBUG: set_static_ip - IP: {ip_address}, Netmask: {netmask}, Gateway: {gateway}")
        
        try:
            # Validace IP adresy
            if not self.is_valid_ip(ip_address):
                return self.create_error_response(f"Neplatná IP adresa: {ip_address}")
            
            # Získání názvu síťového rozhraní (obvykle eth0 nebo wlan0)
            interface = self.get_active_interface()
            if not interface:
                return self.create_error_response("Nepodařilo se zjistit aktivní síťové rozhraní")
            
            if DEBUG:
                logger.debug(f"DEBUG: set_static_ip - rozhraní: {interface}")
            
            # Zjistíme, která síťová služba běží
            # Preferujeme dhcpcd, pokud je nainstalován a aktivní (standardní pro Raspberry Pi)
            active_service = None
            services_to_check = [
                ('dhcpcd', 'dhcpcd'),  # Preferujeme dhcpcd jako první
                ('NetworkManager', 'NetworkManager'),
                ('systemd-networkd', 'systemd-networkd')
            ]
            
            for service_name, service_id in services_to_check:
                try:
                    # Zkontrolujeme, zda je služba aktivní
                    result = subprocess.run(
                        ['systemctl', 'is-active', service_id],
                        capture_output=True,
                        text=True,
                        timeout=2
                    )
                    if result.returncode == 0 and 'active' in result.stdout:
                        active_service = service_id
                        if DEBUG:
                            logger.debug(f"DEBUG: set_static_ip - nalezena aktivní služba: {service_id}")
                        break
                except:
                    continue
            
            # Pokud žádná služba není aktivní, zkontrolujeme, zda je dhcpcd alespoň nainstalován
            if not active_service:
                try:
                    result = subprocess.run(
                        ['which', 'dhcpcd'],
                        capture_output=True,
                        text=True,
                        timeout=2
                    )
                    if result.returncode == 0:
                        active_service = 'dhcpcd'
                        if DEBUG:
                            logger.debug("DEBUG: set_static_ip - dhcpcd je nainstalován, použijeme ho")
                except:
                    pass
            
            # Použijeme správnou metodu podle aktivní služby
            if active_service == 'NetworkManager':
                # NetworkManager - použijeme nmcli
                return self._set_static_ip_nmcli(interface, ip_address, netmask, gateway, dns_servers)
            elif active_service == 'systemd-networkd':
                # systemd-networkd - použijeme jeho konfigurační soubory
                return self._set_static_ip_systemd_networkd(interface, ip_address, netmask, gateway, dns_servers)
            else:
                # dhcpcd nebo fallback - použijeme /etc/dhcpcd.conf
                return self._set_static_ip_dhcpcd(interface, ip_address, netmask, gateway, dns_servers)
            
        except subprocess.CalledProcessError as e:
            error_msg = e.stderr if e.stderr else (e.stdout if e.stdout else str(e))
            if DEBUG:
                logger.debug(f"DEBUG: set_static_ip - chyba: {error_msg}")
            return self.create_error_response(f"Chyba při nastavení statické IP: {error_msg}")
        except Exception as e:
            if DEBUG:
                logger.debug(f"DEBUG: set_static_ip - výjimka: {type(e).__name__}: {e}")
            import traceback
            error_details = traceback.format_exc()
            logger.error(f"Chyba při nastavení statické IP: {error_details}")
            return self.create_error_response(f"Chyba při nastavení statické IP: {str(e)}")
    
    def _set_static_ip_nmcli(self, interface: str, ip_address: str, netmask: str, gateway: str, dns_servers: list) -> Dict[str, Any]:
        """Nastaví statickou IP pomocí NetworkManager (nmcli)."""
        if DEBUG:
            logger.debug(f"DEBUG: _set_static_ip_nmcli - použití nmcli pro {interface}")
        
        try:
            # Získání názvu připojení pro rozhraní
            result = subprocess.run(
                ['nmcli', '-t', '-f', 'NAME,DEVICE', 'connection', 'show', '--active'],
                capture_output=True,
                text=True,
                timeout=5
            )
            
            connection_name = None
            for line in result.stdout.strip().split('\n'):
                if line and ':' in line:
                    parts = line.split(':')
                    if len(parts) >= 2 and parts[1] == interface:
                        connection_name = parts[0]
                        break
            
            if not connection_name:
                # Pokud nenajdeme aktivní připojení, zkusíme najít jakékoli připojení pro toto rozhraní
                result = subprocess.run(
                    ['nmcli', '-t', '-f', 'NAME,DEVICE', 'connection', 'show'],
                    capture_output=True,
                    text=True,
                    timeout=5
                )
                for line in result.stdout.strip().split('\n'):
                    if line and ':' in line:
                        parts = line.split(':')
                        if len(parts) >= 2 and parts[1] == interface:
                            connection_name = parts[0]
                            break
            
            if not connection_name:
                return self.create_error_response(f"Nepodařilo se najít NetworkManager připojení pro {interface}")
            
            if DEBUG:
                logger.debug(f"DEBUG: _set_static_ip_nmcli - název připojení: {connection_name}")
            
            # Vypočítání CIDR notace
            cidr = self.netmask_to_cidr(netmask)
            ip_with_cidr = f"{ip_address}/{cidr}"
            
            # Nastavení statické IP adresy
            subprocess.run(
                ['sudo', 'nmcli', 'connection', 'modify', connection_name, 
                 'ipv4.addresses', ip_with_cidr],
                check=True,
                timeout=10
            )
            
            # Nastavení metody na manual (statická)
            subprocess.run(
                ['sudo', 'nmcli', 'connection', 'modify', connection_name,
                 'ipv4.method', 'manual'],
                check=True,
                timeout=10
            )
            
            # Nastavení gateway
            if gateway:
                subprocess.run(
                    ['sudo', 'nmcli', 'connection', 'modify', connection_name,
                     'ipv4.gateway', gateway],
                    check=True,
                    timeout=10
                )
            
            # Nastavení DNS serverů
            if dns_servers:
                dns_str = ' '.join(dns_servers)
                subprocess.run(
                    ['sudo', 'nmcli', 'connection', 'modify', connection_name,
                     'ipv4.dns', dns_str],
                    check=True,
                    timeout=10
                )
            
            # Aktivace změn - použijeme reload a reapply místo up (bezpečnější)
            try:
                # Nejdříve zkusíme reload a reapply (rychlejší a bezpečnější)
                subprocess.run(
                    ['sudo', 'nmcli', 'connection', 'reload'],
                    check=True,
                    timeout=5
                )
                subprocess.run(
                    ['sudo', 'nmcli', 'device', 'reapply', interface],
                    check=True,
                    timeout=15
                )
                if DEBUG:
                    logger.debug("DEBUG: _set_static_ip_nmcli - změny aktivovány pomocí reload/reapply")
            except subprocess.CalledProcessError:
                # Pokud reapply selže, zkusíme connection up jako fallback
                if DEBUG:
                    logger.debug("DEBUG: _set_static_ip_nmcli - reapply selhal, zkouším connection up")
                subprocess.run(
                    ['sudo', 'nmcli', 'connection', 'down', connection_name],
                    check=False,
                    timeout=5
                )
                subprocess.run(
                    ['sudo', 'nmcli', 'connection', 'up', connection_name],
                    check=True,
                    timeout=30
                )
                if DEBUG:
                    logger.debug("DEBUG: _set_static_ip_nmcli - změny aktivovány pomocí connection up")
            
            logger.info(f"Statická IP adresa {ip_address} byla nastavena pro {interface} pomocí NetworkManager")
            if DEBUG:
                logger.debug("DEBUG: _set_static_ip_nmcli - úspěšně")
            
            return self.create_success_response({
                "message": "Static IP configured successfully",
                "ip_address": ip_address,
                "interface": interface,
                "method": "NetworkManager"
            })
            
        except subprocess.CalledProcessError as e:
            error_msg = e.stderr if e.stderr else (e.stdout if e.stdout else str(e))
            if DEBUG:
                logger.debug(f"DEBUG: _set_static_ip_nmcli - chyba: {error_msg}")
            return self.create_error_response(f"Chyba při nastavení statické IP pomocí NetworkManager: {error_msg}")
        except Exception as e:
            if DEBUG:
                logger.debug(f"DEBUG: _set_static_ip_nmcli - výjimka: {e}")
            return self.create_error_response(f"Chyba při nastavení statické IP pomocí NetworkManager: {str(e)}")
    
    def _set_static_ip_systemd_networkd(self, interface: str, ip_address: str, netmask: str, gateway: str, dns_servers: list) -> Dict[str, Any]:
        """Nastaví statickou IP pomocí systemd-networkd."""
        if DEBUG:
            logger.debug(f"DEBUG: _set_static_ip_systemd_networkd - použití systemd-networkd pro {interface}")
        
        # TODO: Implementovat pro systemd-networkd
        return self.create_error_response("systemd-networkd není ještě podporován")
    
    def _set_static_ip_dhcpcd(self, interface: str, ip_address: str, netmask: str, gateway: str, dns_servers: list) -> Dict[str, Any]:
        """Nastaví statickou IP pomocí dhcpcd."""
        if DEBUG:
            logger.debug(f"DEBUG: _set_static_ip_dhcpcd - použití dhcpcd pro {interface}")
        
        try:
            # Vytvoření konfiguračního souboru pro dhcpcd (Raspberry Pi OS)
            dhcpcd_conf = '/etc/dhcpcd.conf'
            
            # Záloha původního souboru
            subprocess.run(['sudo', 'cp', dhcpcd_conf, f"{dhcpcd_conf}.backup"], check=True)
            
            # Načtení aktuální konfigurace
            with open(dhcpcd_conf, 'r') as f:
                lines = f.readlines()
            
            # Odstranění starých statických IP konfigurací pro toto rozhraní
            new_lines = []
            in_interface_block = False
            for line in lines:
                stripped = line.strip()
                
                # Začátek bloku pro naše rozhraní
                if stripped == f"interface {interface}":
                    in_interface_block = True
                    continue
                
                # Konec bloku (nový interface nebo prázdný řádek po statických konfiguracích)
                if in_interface_block:
                    if stripped.startswith('interface ') or (stripped == '' and not any('static' in l for l in new_lines[-3:] if l.strip())):
                        in_interface_block = False
                    elif stripped.startswith(('static ip_address', 'static routers', 'static domain_name_servers')):
                        continue  # Odstraníme staré statické konfigurace
                    elif not stripped.startswith('static'):
                        in_interface_block = False
                
                if not in_interface_block or not stripped.startswith('static'):
                    new_lines.append(line)
            
            # Přidání nové statické IP konfigurace
            new_lines.append(f"\n# Static IP configuration for {interface}\n")
            new_lines.append(f"interface {interface}\n")
            new_lines.append(f"static ip_address={ip_address}/{self.netmask_to_cidr(netmask)}\n")
            if gateway:
                new_lines.append(f"static routers={gateway}\n")
            if dns_servers:
                dns_str = ' '.join(dns_servers)
                new_lines.append(f"static domain_name_servers={dns_str}\n")
            
            # Zápis nové konfigurace
            temp_file = dhcpcd_conf + '.tmp'
            with open(temp_file, 'w') as f:
                f.writelines(new_lines)
            
            subprocess.run(['sudo', 'cp', temp_file, dhcpcd_conf], check=True)
            os.remove(temp_file)
            
            # Restart dhcpcd služby
            restart_success = False
            restart_message = ""
            try:
                if DEBUG:
                    logger.debug("DEBUG: _set_static_ip_dhcpcd - restartuji dhcpcd")
                result = subprocess.run(
                    ['sudo', 'systemctl', 'restart', 'dhcpcd'],
                    capture_output=True,
                    text=True,
                    timeout=10
                )
                if result.returncode == 0:
                    restart_success = True
                    restart_message = "Služba dhcpcd byla restartována"
                    if DEBUG:
                        logger.debug("DEBUG: _set_static_ip_dhcpcd - dhcpcd úspěšně restartován")
                else:
                    restart_message = "Konfigurace uložena do /etc/dhcpcd.conf. Pro aktivaci je nutný restart sítě (např. reboot nebo 'sudo systemctl restart dhcpcd')."
                    if DEBUG:
                        logger.debug(f"DEBUG: _set_static_ip_dhcpcd - restart dhcpcd selhal: {result.stderr}")
            except Exception as e:
                restart_message = f"Konfigurace uložena do /etc/dhcpcd.conf. Pro aktivaci je nutný restart sítě: {str(e)}"
                if DEBUG:
                    logger.debug(f"DEBUG: _set_static_ip_dhcpcd - chyba při restartu: {e}")
            
            if not restart_success:
                logger.warning(f"Statická IP konfigurace uložena, ale restart služby selhal. {restart_message}")
            
            logger.info(f"Statická IP adresa {ip_address} byla nastavena pro {interface} pomocí dhcpcd")
            if DEBUG:
                logger.debug("DEBUG: _set_static_ip_dhcpcd - úspěšně")
            
            response_data = {
                "message": "Static IP configured successfully" if restart_success else "Static IP configuration saved, but service restart failed",
                "ip_address": ip_address,
                "interface": interface,
                "method": "dhcpcd",
                "restart_success": restart_success
            }
            if restart_message:
                response_data["restart_message"] = restart_message
            
            return self.create_success_response(response_data)
        except subprocess.CalledProcessError as e:
            error_msg = e.stderr if e.stderr else (e.stdout if e.stdout else str(e))
            if DEBUG:
                logger.debug(f"DEBUG: _set_static_ip_dhcpcd - chyba: {error_msg}")
            return self.create_error_response(f"Chyba při nastavení statické IP pomocí dhcpcd: {error_msg}")
        except Exception as e:
            if DEBUG:
                logger.debug(f"DEBUG: _set_static_ip_dhcpcd - výjimka: {e}")
            return self.create_error_response(f"Chyba při nastavení statické IP pomocí dhcpcd: {str(e)}")
    
    def get_active_interface(self) -> Optional[str]:
        """Získá název aktivního síťového rozhraní."""
        try:
            if DEBUG:
                logger.debug("DEBUG: get_active_interface - zkouším ip route get 8.8.8.8")
            # Zkusíme zjistit rozhraní pomocí ip route
            result = subprocess.run(
                ['ip', 'route', 'get', '8.8.8.8'],
                capture_output=True,
                text=True,
                check=True
            )
            if DEBUG:
                logger.debug(f"DEBUG: get_active_interface - ip route output: {result.stdout}")
            for line in result.stdout.split('\n'):
                if 'dev' in line:
                    parts = line.split()
                    if 'dev' in parts:
                        idx = parts.index('dev')
                        if idx + 1 < len(parts):
                            interface = parts[idx + 1]
                            if DEBUG:
                                logger.debug(f"DEBUG: get_active_interface - nalezeno rozhraní: {interface}")
                            return interface
            if DEBUG:
                logger.debug("DEBUG: get_active_interface - ip route nevrátil rozhraní")
        except Exception as e:
            if DEBUG:
                logger.debug(f"DEBUG: get_active_interface - chyba při ip route: {e}")
        
        # Fallback - zkusíme eth0 nebo wlan0
        if DEBUG:
            logger.debug("DEBUG: get_active_interface - zkouším fallback metody")
        for interface in ['eth0', 'wlan0', 'enp0s3', 'ens33', 'enp1s0']:
            try:
                result = subprocess.run(
                    ['ip', 'link', 'show', interface],
                    capture_output=True,
                    text=True
                )
                if result.returncode == 0:
                    # Zkontrolujeme, zda je rozhraní UP
                    if 'state UP' in result.stdout or 'UP' in result.stdout:
                        if DEBUG:
                            logger.debug(f"DEBUG: get_active_interface - nalezeno aktivní rozhraní: {interface}")
                        return interface
            except Exception as e:
                if DEBUG:
                    logger.debug(f"DEBUG: get_active_interface - chyba při kontrole {interface}: {e}")
                continue
        
        if DEBUG:
            logger.debug("DEBUG: get_active_interface - žádné rozhraní nenalezeno")
        return None
    
    def is_valid_ip(self, ip: str) -> bool:
        """Validuje IPv4 adresu."""
        try:
            parts = ip.split('.')
            if len(parts) != 4:
                return False
            for part in parts:
                num = int(part)
                if num < 0 or num > 255:
                    return False
            return True
        except:
            return False
    
    def netmask_to_cidr(self, netmask: str) -> int:
        """Převede netmask na CIDR notaci."""
        try:
            parts = netmask.split('.')
            if len(parts) != 4:
                return 24  # Výchozí
            binary_str = ''
            for part in parts:
                binary_str += format(int(part), '08b')
            return binary_str.count('1')
        except:
            return 24  # Výchozí /24
    
    def update_hosts_file(self, hostname: str):
        """Aktualizuje /etc/hosts s novým hostname."""
        try:
            if DEBUG:
                logger.debug(f"DEBUG: update_hosts_file - hostname: {hostname}")
            hosts_file = '/etc/hosts'
            with open(hosts_file, 'r') as f:
                lines = f.readlines()
            
            new_lines = []
            localhost_found = False
            
            for line in lines:
                if line.strip().startswith('127.0.1.1'):
                    new_lines.append(f"127.0.1.1\t{hostname}\n")
                    localhost_found = True
                else:
                    new_lines.append(line)
            
            if not localhost_found:
                new_lines.append(f"127.0.1.1\t{hostname}\n")
            
            temp_file = hosts_file + '.tmp'
            with open(temp_file, 'w') as f:
                f.writelines(new_lines)
            
            subprocess.run(['sudo', 'cp', temp_file, hosts_file], check=True)
            os.remove(temp_file)
            if DEBUG:
                logger.debug("DEBUG: update_hosts_file - úspěšně")
        except Exception as e:
            logger.warning(f"Nepodařilo se aktualizovat /etc/hosts: {e}")
            if DEBUG:
                logger.debug(f"DEBUG: update_hosts_file - výjimka: {e}")


def main():
    """Hlavní funkce pro spuštění serveru."""
    server = ConfigServer()
    try:
        server.start()
    except KeyboardInterrupt:
        logger.info("Server ukončen uživatelem")
    except Exception as e:
        logger.error(f"Kritická chyba serveru: {e}")
        sys.exit(1)


if __name__ == '__main__':
    main()

