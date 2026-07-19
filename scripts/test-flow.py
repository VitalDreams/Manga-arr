#!/usr/bin/env python3
"""Add Berserk and auto-import"""
import json, subprocess

def run(cmd):
    r = subprocess.run(cmd, shell=True, capture_output=True, text=True, timeout=30)
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

# Delete old entries
print("=== DELETE OLD ===")
for i in range(1, 10):
    api("DELETE", f"/manga/{i}")

# Add Berserk
print("=== ADD BERSERK ===")
add_data = {
    "foreignMangaId": "801513ba-a712-498c-8f57-cae55b38cc92",
    "title": "Berserk",
    "titleSlug": "berserk",
    "path": "/manga/Berserk",
    "rootFolderPath": "/manga",
    "qualityProfileId": 1,
    "metadataProfileId": 1,
    "monitored": True
}
r = api("POST", "/manga", add_data)
print(r[:300])

# Check CleanName
print("\n=== CLEANNAME ===")
r = run('docker exec mangaarr sqlite3 /root/.config/Readarr/readarr.db "SELECT Id, CleanName FROM MangaSeries;"')
print(r)

# Auto-import
print("\n=== AUTO-IMPORT ===")
import_data = {"scanDirectories": ["/manga", "/downloads/complete/manga"]}
r = api("POST", "/manga/autoimport", import_data)
print(r[:500])

# Library
print("\n=== LIBRARY ===")
r = api("GET", "/manga")
try:
    manga = json.loads(r)
    print(f"{len(manga)} manga in library")
    for m in manga:
        print(f"  - {m.get('title', '?')} (CleanName: {m.get('cleanName', '?')})")
except:
    print(r[:200])
