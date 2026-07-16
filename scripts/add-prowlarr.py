#!/usr/bin/env python3
"""Add Prowlarr indexers - try different URL formats"""
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

# Try Newznab with different URL formats
urls = [
    "http://192.168.2.150:9696/1/api",
    "http://192.168.2.150:9696/api",
    "http://prowlarr:9696/1/api",
    "http://prowlarr:9696/api",
]

for url in urls:
    print(f"\n=== Trying Newznab: {url} ===")
    newznab = {
        "enable": True,
        "name": "Prowlarr Usenet",
        "implementation": "Newznab",
        "configContract": "NewznabSettings",
        "fields": [
            {"name": "baseUrl", "value": url},
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
        print(f"SUCCESS with {url}")
        break
    elif "error" in r.lower():
        print(r[:150])
    else:
        print(r[:150])
