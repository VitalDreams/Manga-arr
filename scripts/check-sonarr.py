#!/usr/bin/env python3
"""Copy Sonarr's Prowlarr indexer config to MangaArr"""
import json, subprocess

def run(cmd):
    r = subprocess.run(cmd, shell=True, capture_output=True, text=True, timeout=15)
    return r.stdout.strip()

MANGAARR_KEY = "776783798e714d04bc46b7cb8b34c6b5"

# Get Sonarr's first indexer config
r = run('docker exec sonarr curl -s "http://localhost:8989/api/v3/indexer/2" -H "X-Api-Key: 92b2aea4d3c84dfdaafd967467a61048"')
sonarr_idx = json.loads(r)

# Print key fields
for f in sonarr_idx.get('fields', []):
    print(f['name'], '=', f.get('value', '?'))
print('implementation =', sonarr_idx.get('implementation', '?'))
print('configContract =', sonarr_idx.get('configContract', '?'))
