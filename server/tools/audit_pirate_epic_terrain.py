"""Audit the five Pirate Epic floors, optionally stage private base-grid derivatives.

Does not install terrain or certify native rendering, object collision or entry.
Only exact reviewed archives/layouts are accepted; proprietary bytes stay local.
"""
import argparse
from collections import deque
import hashlib
import json
import tempfile
from pathlib import Path
import struct
import subprocess

from stage_gloomy_jail import index_records

INDEX = "86a54928a9682617371542cdb5a72707c799b92a5c820e3581183adaa94f32b0"
MAPS = [
    (10266, "pirate02_new", "779906391ff9385738a7cf2a7ceb69a00b427b41e70cd2f31bef4db917c42e99", "47e5085d44ead13d9fa62c0d53fe3b14eb0a496f07b46a999121e9c1d59e2fd1", 13),
    (10267, "pirate03_new", "3cebc628fa2e415b64eee0285e9199d28c06b7132b20ce897a1a331e23ef8ff2", "7c11a86cc76d899b2f2770a6cdaa1092bc37fc04cb188c91bcdf79c2388d7430", 4),
    (10268, "pirate04_new", "23500828dc31279a8d9e97ea820061ef5710ecdc978978ba5d34d7b700dddab6", "4c4df4d604e2282c1f5fe25f5eb25376292f9de1bf93a3384104df381f795017", 7),
    (10269, "pirate05_new", "b6b17bc674b778b76c00b439ff6e9cf5e6c30f652ff3149886ec3266884501dc", "330acdb64038eb93b729963eb33866ab8e2a43657953faa6f6d706e770a1e645", 8),
    (10270, "pirate06_new", "b21a51f64d97a9da09acd2dfb0ae5d3ff4e1bb6ec2e3a7cc10cd9007341fbca9", "b5fc1fede29e24e367f857f4d0f5c56ca3f2b62f6539254e1d0f4355ab5dd2d1", 8),
]


def inspect_floor(data, name, expected_objects):
    if len(data) < 276 or struct.unpack_from("<II", data) != (1006, 0):
        raise ValueError("Unreviewed DMap version")
    puzzle = data[8:268].split(b"\0", 1)[0].replace(b"\\", b"/")
    if puzzle != ("map/puzzle/" + name + ".pul").encode():
        raise ValueError("Unexpected puzzle identity")
    w, h = struct.unpack_from("<II", data, 268)
    if (w, h) != (100, 100):
        raise ValueError("Unexpected dimensions")
    end = 276 + h * (w * 6 + 4)
    if len(data) != end + 24 + expected_objects * 424:
        raise ValueError("Unexpected DMap layout length")
    # Observed version-1006 tail, not the legacy version-1004 object grammar.
    # Keep unknown zero fields opaque instead of inventing semantic meanings.
    if struct.unpack_from("<III", data, end) != (0, 0, expected_objects):
        raise ValueError("Unreviewed object prefix")
    for i in range(expected_objects):
        if struct.unpack_from("<I", data, end + 12 + i * 424)[0] != 4:
            raise ValueError("Unreviewed object kind")
    if data[-12:] != bytes(12):
        raise ValueError("Unreviewed trailer")
    cells = [struct.unpack_from("<H", data, 276 + y * (w * 6 + 4) + x * 6)[0]
             for y in range(h) for x in range(w)]
    if not set(cells) <= {0, 1}:
        raise ValueError("Unreviewed access flag")
    remaining = {i for i, flag in enumerate(cells) if flag == 0}
    components = []
    largest = set()
    while remaining:
        seed = min(remaining)
        remaining.remove(seed)
        queue, count = deque([seed]), 0
        component = set()
        while queue:
            pos = queue.popleft()
            count += 1
            component.add(pos)
            x, y = pos % w, pos // w
            for nx, ny in ((x-1, y), (x+1, y), (x, y-1), (x, y+1)):
                nxt = ny * w + nx
                if 0 <= nx < w and 0 <= ny < h and nxt in remaining:
                    remaining.remove(nxt)
                    queue.append(nxt)
        components.append(count)
        if len(component) > len(largest):
            largest = component
    # Choose an interior point in the largest base-grid component. This is a
    # proposed Revora landing, not an original-coordinate or collision claim.
    clearance = {}
    edge = deque()
    for pos in largest:
        x, y = pos % w, pos // w
        if any(not (0 <= nx < w and 0 <= ny < h) or ny*w+nx not in largest
               for nx, ny in ((x-1,y),(x+1,y),(x,y-1),(x,y+1))):
            clearance[pos] = 0
            edge.append(pos)
    while edge:
        pos = edge.popleft()
        x, y = pos % w, pos // w
        for nx, ny in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
            nxt = ny*w+nx
            if 0 <= nx < w and 0 <= ny < h and nxt in largest and nxt not in clearance:
                clearance[nxt] = clearance[pos]+1
                edge.append(nxt)
    landing = min(largest, key=lambda pos: (-clearance[pos], pos))
    lx, ly = landing % w, landing // w
    # Reserve a distinct NPC footprint and a reachable interaction point.
    candidates = []
    for dx, dy in ((4,0),(-4,0),(0,4),(0,-4)):
        nx, ny = lx+dx, ly+dy
        if all(0 <= x < w and 0 <= y < h and y*w+x in largest
               for x in range(nx-1,nx+2) for y in range(ny-1,ny+2)):
            candidates.append([nx,ny])
    if not candidates:
        raise ValueError("No interior landing/NPC candidate")
    return dict(dimensions=[w, h], floor_walkable_cells=cells.count(0),
                cardinal_component_sizes=sorted(components, reverse=True),
                observed_object_kind=4, observed_object_count=expected_objects,
                object_collision_verified=False,
                legacy_reader_object_count=struct.unpack_from("<I", data, end+4)[0],
                proposed_revora_landing=[lx,ly],
                proposed_revora_search_npc=candidates[0],
                landing_base_clearance=clearance[landing],
                travel_coordinates_original=False)


def audit(client, database, sevenzip, stage=None):
    raw = (client / "ini/GameMap.dat").read_bytes()
    if hashlib.sha256(raw).hexdigest() != INDEX:
        raise ValueError("Unreviewed client map index")
    records, reports, floors = index_records(raw), [], {}
    for identity, name, archive_hash, native_hash, count in MAPS:
        if [r for r in records if r[0] == identity] != [
                (identity, ("map/map/" + name + ".7z").encode(), 128)]:
            raise ValueError("Map identity mismatch")
        archive = client / "map/map" / (name + ".7z")
        if hashlib.sha256(archive.read_bytes()).hexdigest() != archive_hash:
            raise ValueError("Unreviewed archive")
        data = subprocess.check_output([str(sevenzip), "e", "-so", str(archive)], timeout=15)
        if hashlib.sha256(data).hexdigest() != native_hash:
            raise ValueError("Unreviewed extracted content")
        report = inspect_floor(data, name, count)
        floor = database / "maps" / (str(identity) + ".map")
        report.update(map=identity, archive_sha256=archive_hash, dmap_sha256=native_hash,
                      server_floor_present=floor.is_file())
        reports.append(report)
        # Private base-access derivative only. No semantic claim about opaque
        # object/trailer fields, native collision or production readiness.
        w, h = report['dimensions']
        cells = bytes(struct.unpack_from("<H", data, 276 + y * (w * 6 + 4) + x * 6)[0]
                      for y in range(h) for x in range(w))
        floor_bytes = struct.pack("<II", w, h) + cells
        floors[str(identity) + '.map'] = floor_bytes
        report['staged_base_floor_sha256'] = hashlib.sha256(floor_bytes).hexdigest()
    result = dict(maps=reports, hub_identity_verified=False, ordinary_entry_verified=False,
                  native_acceptance=False, earning_route_verified=False,
                  staging_scope="base access grid only; disposable travel prototype allowed; production requires object/native collision validation")
    if stage is not None:
        stage = stage.resolve()
        for root in (client.resolve(), database.resolve()):
            if stage == root or root in stage.parents or stage in root.parents:
                raise ValueError("Staging must be separate from client and database")
        if stage.exists():
            raise ValueError("Staging destination must not exist")
        stage.parent.mkdir(parents=True, exist_ok=True)
        temporary = Path(tempfile.mkdtemp(prefix=stage.name + '.partial-', dir=stage.parent))
        for name, content in floors.items():
            (temporary / name).write_bytes(content)
            if (temporary / name).read_bytes() != content:
                raise IOError("Staged floor read-back failed")
        (temporary / 'manifest.json').write_text(json.dumps(result, indent=2) + "\n", encoding='utf-8')
        # Windows rename fails if the destination appeared concurrently. No
        # existing client, database or staging tree is overwritten/deleted.
        temporary.rename(stage)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--client", type=Path, required=True)
    parser.add_argument("--database", type=Path, required=True)
    parser.add_argument("--sevenzip", type=Path, default=Path("C:/Program Files/7-Zip/7z.exe"))
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--stage", type=Path, help="New separate private base-grid destination; never the runtime maps folder")
    args = parser.parse_args()
    result = audit(args.client, args.database, args.sevenzip, args.stage)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("Reviewed five Pirate Epic floors; hub, object collision and admission remain unverified.")
