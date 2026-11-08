"""Inventory Star Tower scenes read-only; absent scenes are evidence, not success."""
import argparse
import hashlib
import json
import struct
import subprocess
from pathlib import Path
from stage_gloomy_jail import index_records

POINTS = {
    1782: [(51, 70)], 1783: [(52, 50)], 1784: [(55, 55), (61, 68)],
    1785: [(96, 120), (70, 60)], 1786: [(113, 122), (62, 81)],
    1787: [(48, 37), (56, 64)],
}


def audit(client, runtime, sevenzip):
    client_index = index_records((client / "ini/GameMap.dat").read_bytes())
    server_index = index_records((runtime / "database/GameMap.dat").read_bytes())
    result = []
    for mid, points in POINTS.items():
        bindings = [[(r[1].decode("ascii"), r[2]) for r in records if r[0] == mid]
                    for records in (client_index, server_index)]
        floor = runtime / f"database/maps/{mid}.map"
        row = dict(map=mid, client_bindings=bindings[0], server_bindings=bindings[1],
                   server_floor_exists=floor.exists(), verified=False)
        result.append(row)
        if len(bindings[0]) != 1 or bindings[0] != bindings[1] or not floor.exists():
            row["problem"] = "Missing or conflicting scene binding/floor"
            continue
        relative = bindings[0][0][0]
        parts = Path(relative.replace("\\", "/"))
        if parts.is_absolute() or ".." in parts.parts or ":" in relative:
            raise ValueError("Unsafe map path")
        archive = client / parts
        member = archive.stem + ".DMap"
        native = subprocess.check_output([str(sevenzip), "e", "-so", str(archive), member], timeout=30)
        server = floor.read_bytes()
        w, h = struct.unpack_from("<II", server)
        if not 0 < w <= 4096 or not 0 < h <= 4096 or len(server) != 8+w*h:
            raise ValueError("Invalid server floor dimensions/length")
        if struct.unpack_from("<II", native, 268) != (w, h) or len(native) < 276+h*(w*6+4):
            raise ValueError("Native scene dimensions/length mismatch")
        native_blocked = lambda x, y: struct.unpack_from("<H", native, 276+y*(w*6+4)+x*6)[0] != 0
        row.update(dimensions=[w,h], sha256=dict(server=hashlib.sha256(server).hexdigest(),
                   archive=hashlib.sha256(archive.read_bytes()).hexdigest(), native=hashlib.sha256(native).hexdigest()),
                   differing_cells=sum((server[8+y*w+x] != 0) != native_blocked(x,y) for y in range(h) for x in range(w)),
                   points=[dict(x=x,y=y,walkable=0<=x<w and 0<=y<h and server[8+y*w+x]==0 and not native_blocked(x,y)) for x,y in points])
        row["verified"] = row["differing_cells"] == 0 and all(p["walkable"] for p in row["points"])
    return dict(scope="Read-only scene bindings, hashes, full blocked-cell comparison and journal target cells. Connectivity, NPC footprints, portals and native rendering are not validated.", scenes=result,
                all_scenes_available=all(r["verified"] for r in result), native_acceptance=False)


if __name__ == "__main__":
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--client", type=Path, required=True)
    p.add_argument("--runtime", type=Path, default=Path("bin/Debug"))
    p.add_argument("--sevenzip", type=Path, default=Path("C:/Program Files/7-Zip/7z.exe"))
    p.add_argument("--output", type=Path, required=True)
    a = p.parse_args()
    result = audit(a.client, a.runtime, a.sevenzip)
    a.output.write_text(json.dumps(result, indent=2)+"\n", encoding="utf-8")
    for row in result["scenes"]:
        print(row["map"], "floor/targets verified" if row["verified"] else row.get("problem", "Terrain or target discrepancy"))
