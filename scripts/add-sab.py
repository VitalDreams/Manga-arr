#!/usr/bin/env python3
"""Add SABnzbd to MangaArr with correct field names"""
import json, subprocess

def run(cmd):
    r = subprocess.run(cmd, shell=True, capture_output=True, text=True, timeout=15)
    return r.stdout.strip()

APIKEY = run("docker exec mangaarr cat /root/.config/Readarr/config.xml | grep ApiKey | sed 's/.*<ApiKey>//;s/<.*//'")

def api(method, path, data=None):
    cmd = f'docker exec mangaarr curl -s -X {method} http://localhost:8787/api/v1{path} -H "X-Api-Key: {APIKEY}" -H "Content-Type: application/json"'
    if data:
        with open('/tmp/api_body.json', 'w') as f:
            json.dump(data, f)
        run('docker cp /tmp/api_body.json mangaarr:/tmp/api_body.json')
        cmd += ' -d @/tmp/api_body.json'
    return run(cmd)

# SABnzbd - use Sonarr's exact field names
sab = {
    "enable": True,
    "name": "SABnzbd",
    "implementation": "Sabnzbd",
    "configContract": "SabnzbdSettings",
    "fields": [
        {"name": "host", "value": "192.168.2.150"},
        {"name": "port", "value": 8080},
        {"name": "apiKey", "value": "9822f25bdd2c480b872c6eabb1acab70"},
        {"name": "musicCategory", "value": "manga"},
        {"name": "recentMusicPriority", "value": -100},
        {"name": "olderMusicPriority", "value": -100},
        {"name": "useSsl", "value": False}
    ],
    "priority": 1,
    "removeCompletedDownloads": False,
    "removeFailedDownloads": False
}
r = api("POST", "/downloadclient", sab)
print("SABnzbd:", r[:300])

# Verify
r = api("GET", "/downloadclient")
try:
    clients = json.loads(r)
    for c in clients:
        print(f"  - {c['name']} ({c['implementation']})")
except:
    print(r[:200])
