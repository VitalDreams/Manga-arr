#!/usr/bin/env python3
"""Add Prowlarr Torznab indexer to MangaArr (copy from Sonarr pattern)"""
import json, subprocess

def run(cmd):
    r = subprocess.run(cmd, shell=True, capture_output=True, text=True, timeout=15)
    return r.stdout.strip()

APIKEY = run("docker exec mangaarr cat /root/.config/Readarr/config.xml | grep ApiKey | sed 's/.*<ApiKey>//;s/<.*//'")
PROWLARR_KEY = "f8ece1e648c44b53b58bdf9da2ccf294"

def api(method, path, data=None):
    cmd = f'docker exec mangaarr curl -s -X {method} http://localhost:8787/api/v1{path} -H "X-Api-Key: {APIKEY}" -H "Content-Type: application/json"'
    if data:
        with open('/tmp/api_body.json', 'w') as f:
            json.dump(data, f)
        run('docker cp /tmp/api_body.json mangaarr:/tmp/api_body.json')
        cmd += ' -d @/tmp/api_body.json'
    return run(cmd)

# Add Torznab indexer (same pattern as Sonarr)
print("=== Adding Torznab (Prowlarr Torrents) ===")
torznab = {
    "enable": True,
    "name": "Prowlarr (Torrents)",
    "implementation": "Torznab",
    "configContract": "TorznabSettings",
    "fields": [
        {"name": "baseUrl", "value": "http://prowlarr:9696/1/"},
        {"name": "apiPath", "value": "/api"},
        {"name": "apiKey", "value": PROWLARR_KEY},
        {"name": "categories", "value": [7030, 7000]},
        {"name": "minimumSeeders", "value": 1}
    ],
    "priority": 25,
    "enableRss": True,
    "enableAutomaticSearch": True,
    "enableInteractiveSearch": True
}
r = api("POST", "/indexer", torznab)
if '"id"' in r:
    print("SUCCESS!")
else:
    print(r[:300])

# Add Newznab indexer (for Usenet)
print("\n=== Adding Newznab (Prowlarr Usenet) ===")
newznab = {
    "enable": True,
    "name": "Prowlarr (Usenet)",
    "implementation": "Newznab",
    "configContract": "NewznabSettings",
    "fields": [
        {"name": "baseUrl", "value": "http://prowlarr:9696/1/"},
        {"name": "apiPath", "value": "/api"},
        {"name": "apiKey", "value": PROWLARR_KEY},
        {"name": "categories", "value": [7030, 7000]}
    ],
    "priority": 25,
    "enableRss": True,
    "enableAutomaticSearch": True,
    "enableInteractiveSearch": True
}
r = api("POST", "/indexer", newznab)
if '"id"' in r:
    print("SUCCESS!")
else:
    print(r[:300])

# Summary
print("\n=== FULL SETUP ===")
for ep, label in [("/downloadclient", "Download Clients"), ("/indexer", "Indexers"), ("/rootfolder", "Root Folders")]:
    r = api("GET", ep)
    try:
        items = json.loads(r)
        print(f"\n{label}:")
        for item in items:
            print(f"  - {item.get('name', item.get('path', '?'))}")
    except:
        pass
print("\n=== DONE ===")
