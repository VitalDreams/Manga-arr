#!/usr/bin/env python3
"""Fix qBittorrent password with correct PBKDF2 hash"""
import hashlib, base64, os, subprocess, time, http.cookiejar, urllib.request, urllib.parse

# Generate correct PBKDF2 hash for 'adminadmin'
password = 'adminadmin'
salt = os.urandom(16)
key = hashlib.pbkdf2_hmac('sha256', password.encode(), salt, 100000, dklen=32)
hash_str = base64.b64encode(salt).decode() + ':' + base64.b64encode(key).decode()
print(f"Generated hash: {hash_str}")

# Update config
path = '/opt/containers/media-stack/config/qbittorrent/qBittorrent/qBittorrent.conf'
lines = open(path).readlines()
out = []
for line in lines:
    if 'Password_PBKDF2' not in line:
        out.append(line)
out.append(f'WebUI\\Password_PBKDF2="@ByteArray({hash_str})"\n')
open(path, 'w').writelines(out)
print("Config updated")

# Restart
subprocess.run(['docker', 'restart', 'qbittorrent'], capture_output=True)
time.sleep(8)

# Test login
jar = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
try:
    req = urllib.request.Request('http://192.168.2.150:8085/api/v2/auth/login',
        data=urllib.parse.urlencode({'username': 'admin', 'password': password}).encode())
    resp = opener.open(req, timeout=5)
    result = resp.read().decode()
    print(f"Login result: {result}")
    if result.strip() == 'Ok.':
        resp2 = opener.open('http://192.168.2.150:8085/api/v2/app/version', timeout=5)
        print(f"qBittorrent version: {resp2.read().decode()}")
except Exception as e:
    print(f"Error: {e}")
