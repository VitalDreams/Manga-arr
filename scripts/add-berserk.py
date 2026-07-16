#!/usr/bin/env python3
"""Add Berserk to MangaArr library"""
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

# Add Berserk
add_data = {
    "foreignMangaId": "801513ba-a712-498c-8f57-cae55b38cc92",
    "title": "Berserk",
    "titleSlug": "berserk",
    "overview": "Guts, known as the Black Swordsman, seeks sanctuary from the demonic forces...",
    "author": "Miura Kentarou",
    "status": "ongoing",
    "year": 1989,
    "path": "/manga/Berserk",
    "rootFolderPath": "/manga",
    "qualityProfileId": 1,
    "metadataProfileId": 1,
    "monitored": True,
    "addOptions": {
        "searchForMissingVolumes": False
    }
}
r = api("POST", "/manga", add_data)
print("Add result:", r[:400])

# Verify
r = api("GET", "/manga")
try:
    manga = json.loads(r)
    print(f"{len(manga)} manga in library")
    for m in manga:
        print(f"  - {m['title']} ({m.get('year', '?')})")
except:
    print(r[:200])
