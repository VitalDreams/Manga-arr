#!/usr/bin/env python3
"""Insert Prowlarr Usenet indexer directly into MangaArr database"""
import json, subprocess

def run(cmd):
    r = subprocess.run(cmd, shell=True, capture_output=True, text=True, timeout=15)
    return r.stdout.strip()

APIKEY = "f8ece1e648c44b53b58bdf9da2ccf294"

# Check existing indexers
r = run('docker exec mangaarr sqlite3 /root/.config/Readarr/readarr.db "SELECT Id, Name, Implementation FROM Indexers;"')
print("Current indexers:", r)

# Build fields JSON
fields = json.dumps([
    {"name": "baseUrl", "value": "http://prowlarr:9696/1/"},
    {"name": "apiPath", "value": "/api"},
    {"name": "apiKey", "value": APIKEY},
    {"name": "categories", "value": "7030,7000"},
    {"name": "minimumSeeders", "value": "0"}
])

# Escape single quotes for SQL
fields_escaped = fields.replace("'", "''")

sql = f"""INSERT INTO Indexers (Enable, Name, Implementation, ConfigContract, Priority, EnableRss, EnableAutomaticSearch, EnableInteractiveSearch, Fields) VALUES (1, 'Prowlarr (Usenet)', 'Newznab', 'NewznabSettings', 25, 1, 1, 1, '{fields_escaped}');"""

run(f"docker exec mangaarr sqlite3 /root/.config/Readarr/readarr.db \"{sql}\"")
print("Inserted")

# Verify
r = run('docker exec mangaarr sqlite3 /root/.config/Readarr/readarr.db "SELECT Id, Name, Implementation FROM Indexers;"')
print("After insert:", r)
