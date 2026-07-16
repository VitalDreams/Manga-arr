#!/usr/bin/env python3
"""Add MangaArr as application in Prowlarr"""
import json, subprocess

def run(cmd):
    r = subprocess.run(cmd, shell=True, capture_output=True, text=True, timeout=15)
    return r.stdout.strip()

PROWLARR_KEY = "f8ece1e648c44b53b58bdf9da2ccf294"
MANGAARR_KEY = "776783798e714d04bc46b7cb8b34c6b5"

data = {
    "configContract": "ReadarrSettings",
    "name": "MangaArr",
    "fields": [
        {"name": "prowlarrUrl", "value": "http://192.168.2.150:9696"},
        {"name": "baseUrl", "value": "http://192.168.2.150:8192"},
        {"name": "apiKey", "value": MANGAARR_KEY},
        {"name": "syncCategories", "value": [7030, 7000]}
    ],
    "syncLevel": "fullSync",
    "tags": []
}

with open('/tmp/prowlarr_app.json', 'w') as f:
    json.dump(data, f)

run('docker cp /tmp/prowlarr_app.json prowlarr:/tmp/prowlarr_app.json')
r = run(f'docker exec prowlarr curl -s -X POST http://localhost:9696/api/v1/applications -H "X-Api-Key: {PROWLARR_KEY}" -H "Content-Type: application/json" -d @/tmp/prowlarr_app.json')
print("Result:", r[:400])

# Verify
r = run(f'docker exec prowlarr curl -s http://localhost:9696/api/v1/applications -H "X-Api-Key: {PROWLARR_KEY}"')
try:
    apps = json.loads(r)
    print("\nApplications:")
    for a in apps:
        print(f"  - {a['name']} ({a['configContract']})")
except:
    print(r[:200])
