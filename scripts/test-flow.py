#!/usr/bin/env python3
"""Clean test: add Berserk + auto-import"""
import json, subprocess

def run(cmd):
    r = subprocess.run(cmd, shell=True, capture_output=True, text=True, timeout=60)
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

# Clean slate
print("=== CLEANUP ===")
run('docker exec mangaarr sqlite3 /root/.config/Readarr/readarr.db "DELETE FROM MangaFiles; DELETE FROM Volumes; DELETE FROM MangaSeries; DELETE FROM MangaMetadata;"')

# Add Berserk
print("=== ADD BERSERK ===")
r = api("POST", "/manga", {
    "foreignMangaId": "801513ba-a712-498c-8f57-cae55b38cc92",
    "title": "Berserk",
    "titleSlug": "berserk",
    "path": "/manga/Berserk",
    "rootFolderPath": "/manga",
    "qualityProfileId": 1,
    "metadataProfileId": 1,
    "monitored": True
})
print(r[:150])

# Auto-import
print("\n=== AUTO-IMPORT ===")
r = api("POST", "/manga/autoimport", {"scanDirectories": ["/manga", "/downloads/complete/manga"]})
try:
    result = json.loads(r)
    print(f"Scanned: {result.get('filesScanned',0)}")
    print(f"Matched: {result.get('filesMatched',0)}")
    print(f"Imported: {result.get('filesImported',0)}")
    print(f"Moved: {result.get('filesMoved',0)}")
    if result.get('importedFiles'):
        for f in result['importedFiles'][:5]:
            print(f"  + {f}")
    if result.get('errors'):
        for e in result['errors'][:3]:
            print(f"  ! {e}")
except:
    print(r[:500])

# Library
print("\n=== LIBRARY ===")
r = api("GET", "/manga")
try:
    manga = json.loads(r)
    print(f"{len(manga)} manga in library")
    for m in manga:
        print(f"  - {m.get('title', '?')} ({m.get('cleanName', '?')})")
except:
    print(r[:200])
