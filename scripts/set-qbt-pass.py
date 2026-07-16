#!/usr/bin/env python3
"""Set qBittorrent password and verify"""
import hashlib, base64, os, subprocess, time

# Generate hash
salt = os.urandom(16)
key = hashlib.pbkdf2_hmac('sha256', b'adminadmin', salt, 100000, dklen=32)
h = base64.b64encode(salt).decode() + ':' + base64.b64encode(key).decode()

# Write to config
path = '/opt/containers/media-stack/config/qbittorrent/qBittorrent/qBittorrent.conf'
content = open(path).read()
# Remove any existing password line
lines = [l for l in content.split('\n') if 'Password_PBKDF2' not in l]
# Add before [RSS] section
out = []
for line in lines:
    if line.strip() == '[RSS]':
        out.append(f'WebUI\\Password_PBKDF2=@ByteArray({h})')
    out.append(line)
open(path, 'w').write('\n'.join(out))
print(f"Password hash set: {h}")

# Restart
subprocess.run(['docker', 'restart', 'qbittorrent'], capture_output=True)
time.sleep(8)

# Verify
r = subprocess.run('docker exec qbittorrent curl -s -X POST http://localhost:8080/api/v2/auth/login -d username=admin -d password=adminadmin -c /tmp/qc && docker exec qbittorrent curl -s -b /tmp/qc http://localhost:8080/api/v2/app/version', shell=True, capture_output=True, text=True)
print(f"Result: {r.stdout}")
