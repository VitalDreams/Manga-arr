#!/usr/bin/env python3
"""Add Fullmetal Alchemist to MangaArr library"""
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

# Step 1: Delete any existing FMA entries
print("=== CLEANUP EXISTING FMA ===")
r = api("GET", "/manga")
try:
    manga = json.loads(r)
    fma_entries = [m for m in manga if "fullmetal" in m.get("title", "").lower()]
    for m in fma_entries:
        mid = m["id"]
        print(f"  Deleting: {m['title']} (id={mid})")
        api("DELETE", f"/manga/{mid}")
    if not fma_entries:
        print("  No existing FMA entries found")
except:
    print("  Could not parse manga list, continuing...")

# Step 2: Add Fullmetal Alchemist
print("\n=== ADD FULLMETAL ALCHEMIST ===")
add_data = {
    "foreignMangaId": "d6824a24-4098-4019-aa12-77e597bf5a53",
    "title": "Fullmetal Alchemist",
    "titleSlug": "fullmetal-alchemist",
    "path": "/manga/Fullmetal Alchemist",
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

# Step 3: Verify the add succeeded
print("\n=== VERIFY ===")
r = api("GET", "/manga")
try:
    manga = json.loads(r)
    fma = [m for m in manga if "fullmetal" in m.get("title", "").lower()]
    if fma:
        m = fma[0]
        print(f"  Added: {m['title']} (id={m['id']})")
        print(f"  Path: {m.get('path', '?')}")
        print(f"  Monitored: {m.get('monitored', '?')}")
        print(f"  Foreign ID: {m.get('foreignMangaId', '?')}")
    else:
        print("  WARNING: FMA not found in library after add!")
except:
    print(r[:300])

# Step 4: Auto-import for existing FMA files
print("\n=== AUTO-IMPORT ===")
r = api("POST", "/manga/autoimport", {"scanDirectories": ["/manga", "/downloads/complete/manga"]})
try:
    result = json.loads(r)
    print(f"  Scanned: {result.get('filesScanned', 0)}")
    print(f"  Matched: {result.get('filesMatched', 0)}")
    print(f"  Imported: {result.get('filesImported', 0)}")
    print(f"  Moved: {result.get('filesMoved', 0)}")
    if result.get('importedFiles'):
        for f in result['importedFiles'][:5]:
            print(f"  + {f}")
    if result.get('errors'):
        for e in result['errors'][:3]:
            print(f"  ! {e}")
except:
    print(r[:500])

# Step 5: Report library status
print("\n=== LIBRARY STATUS ===")
r = api("GET", "/manga")
try:
    manga = json.loads(r)
    print(f"{len(manga)} manga in library:")
    for m in manga:
        print(f"  - {m.get('title', '?')} ({m.get('cleanName', '?')})")
except:
    print(r[:200])
