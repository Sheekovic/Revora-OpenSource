"""Emit a rollback-only PostgreSQL regression for the actual claim capacity queries.

Run emitted SQL on a disposable database with psql -v ON_ERROR_STOP=1.
Temporary items shadow the real table; no persistent player state is modified.
"""
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[1]
services = ("CtfResults", "MarketErrandSystem", "NobleWarSystem", "SteedRaceClaims", "DuelPitSystem")
sql = ["BEGIN;", "CREATE TEMP TABLE items(entityid bigint, position int, warehouse int, inwardrobe int) ON COMMIT DROP;",
       "INSERT INTO items SELECT 1,0,0,1 FROM generate_series(1,299);",
       "INSERT INTO items SELECT 1,0,0,0 FROM generate_series(1,38);",
       "INSERT INTO items VALUES(1,0,0,NULL),(1,4,0,0),(1,0,8,0),(2,0,0,0);"]
for expected in (39, 40):
    if expected == 40:
        sql.append("INSERT INTO items VALUES(1,0,0,0);")
    for service in services:
        source = (root / "Game/Features" / (service + ".cs")).read_text(encoding="utf-8-sig")
        queries = re.findall(r'"(SELECT count\(\*\) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0[^"\r\n]*)"', source)
        if len(queries) != 1:
            raise RuntimeError(f"Expected one capacity query in {service}, got {len(queries)}")
        query = queries[0].replace("@uid", "1")
        sql.append(f"DO $$ BEGIN IF ({query}) <> {expected} THEN RAISE EXCEPTION '{service}: expected {expected} bag slots'; END IF; END $$;")
sql.extend(["ROLLBACK;", "SELECT 'PASS: five claim queries; 39/40 bag boundary, 299 wardrobe items, NULL legacy flag, equipment, warehouse and other owner';"])
sys.stdout.write("\n".join(sql) + "\n")
