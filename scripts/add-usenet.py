#!/usr/bin/env python3
"""Force-add Prowlarr Usenet indexer to MangaArr (skip validation)"""
import json, subprocess

def run(cmd):
    r = subprocess.run(cmd, shell=True, capture_output=True, text=True, timeout=15)
    return r.stdout.strip()

APIKEY = run("docker exec mangaarr cat /root/.config/Readarr/config.xml | grep ApiKey | sed 's/.*<ApiKey>//;s/<.*//'")
PROWLARR_KEY = "f8ece1e648c44b53b58bdf9da2ccf294"

# Add directly to MangaArr's database since the API validation is too strict
print("=== Adding Prowlarr Usenet via DB ===")

# First try the API with a broader search term hint
cmd = f'docker exec mangaarr curl -s -X POST http://localhost:8787/api/v1/indexer -H "X-Api-Key: {APIKEY}" -H "Content-Type: application/json"'
data = {
    "enable": True,
    "name": "Prowlarr (Usenet)",
    "implementation": "Newznab",
    "configContract": "NewznabSettings",
    "fields": [
        {"name": "baseUrl", "value": "http://prowlarr:9696/1/"},
        {"name": "apiPath", "value": "/api"},
        {"name": "apiKey", "value": PROWLARR_KEY},
        {"name": "categories", "value": [7030, 7000, 7020, 7010, 8010]}
    ],
    "priority": 25,
    "enableRss": True,
    "enableAutomaticSearch": True,
    "enableInteractiveSearch": True
}
with open('/tmp/nzb.json', 'w') as f:
    json.dump(data, f)
run('docker cp /tmp/nzb.json mangaarr:/tmp/nzb.json')
r = run(f'{cmd} -d @/tmp/nzb.json')
print(r[:300])

# Verify
r = run(f'docker exec mangaarr curl -s http://localhost:8787/api/v1/indexer -H "X-Api-Key: {APIKEY}"')
try:
    indexers = json.loads(r)
    print("\nIndexers:")
    for i in indexers:
        print(f"  - {i['name']} ({i['implementation']})")
except:
    print(r[:200])
