#!/usr/bin/env python3
"""Set up MangaArr download clients"""
import json, subprocess

def run(cmd):
    r = subprocess.run(cmd, shell=True, capture_output=True, text=True, timeout=15)
    return r.stdout.strip()

APIKEY = run("docker exec mangaarr cat /root/.config/Readarr/config.xml | grep ApiKey | sed 's/.*<ApiKey>//;s/<.*//'")
print(f"API Key: {APIKEY}")

def api(method, path, data=None):
    cmd = f'docker exec mangaarr curl -s -X {method} http://localhost:8787/api/v1{path} -H "X-Api-Key: {APIKEY}" -H "Content-Type: application/json"'
    if data:
        with open('/tmp/api_body.json', 'w') as f:
            json.dump(data, f)
        run('docker cp /tmp/api_body.json mangaarr:/tmp/api_body.json')
        cmd += ' -d @/tmp/api_body.json'
    return run(cmd)

# 1. qBittorrent - no password, Docker internal networking
print("\n=== Adding qBittorrent ===")
qbt = {
    "enable": True,
    "name": "qBittorrent",
    "implementation": "QBittorrent",
    "configContract": "QBittorrentSettings",
    "fields": [
        {"name": "host", "value": "qbittorrent"},
        {"name": "port", "value": 8080},
        {"name": "username", "value": "admin"},
        {"name": "password", "value": ""},
        {"name": "tvCategory", "value": "manga"},
        {"name": "recentTvPriority", "value": 0},
        {"name": "olderTvPriority", "value": 0},
        {"name": "initialState", "value": 0},
        {"name": "useSsl", "value": False}
    ],
    "priority": 1,
    "removeCompletedDownloads": False,
    "removeFailedDownloads": True
}
r = api("POST", "/downloadclient", qbt)
print(r[:300])

# 2. SABnzbd
print("\n=== Adding SABnzbd ===")
sab = {
    "enable": True,
    "name": "SABnzbd",
    "implementation": "Sabnzbd",
    "configContract": "SabnzbdSettings",
    "fields": [
        {"name": "host", "value": "192.168.2.150"},
        {"name": "port", "value": 8080},
        {"name": "apiKey", "value": "9822f25bdd2c480b872c6eabb1acab70"},
        {"name": "tvCategory", "value": "tv"},
        {"name": "recentTvPriority", "value": -100},
        {"name": "olderTvPriority", "value": -100},
        {"name": "useSsl", "value": False}
    ],
    "priority": 1,
    "removeCompletedDownloads": False,
    "removeFailedDownloads": False
}
r = api("POST", "/downloadclient", sab)
print(r[:300])

# 3. Verify
print("\n=== Download Clients ===")
r = api("GET", "/downloadclient")
try:
    clients = json.loads(r)
    for c in clients:
        print(f"  - {c['name']} ({c['implementation']})")
except:
    print(r[:200])

print("\n=== Done ===")
