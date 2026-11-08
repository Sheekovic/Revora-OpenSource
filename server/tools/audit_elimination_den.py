"""Read-only audit of the explicit Revora Elimination den terrain adaptation."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
from audit_elimination_world import routes
from stage_gloomy_jail import index_records

TERRAIN = "608dc4f689d0eff861bcaf5e48a0585714e9bd254c03209a04dd92872e3f19d2"
DMAP = "561be32ea449aa5310ced4c92f70aacc066dc79223be95abbd4c5b09caa52d46"
ARCHIVE = "4de18909269b55d24bc86720d4395f71dfa5e4a95ed96a52e501d86e3f062346"


def exact(path, expected):
    data = path.read_bytes()
    if hashlib.sha256(data).hexdigest() != expected:
        raise ValueError("Reviewed input changed: " + str(path))
    return data


def audit(root, client, runtime):
    source = (root / "Game/Features/EliminationRules.cs").read_text(encoding="utf-8")
    def value(name):
        return int(re.search(r"\b" + name + r"=(\d+)", source)[1])
    identity = value("DenBase")
    entry = (identity, b"map/map/mine.7z", 256)
    for path in (client / "ini/GameMap.dat", runtime / "database/GameMap.dat"):
        if entry not in index_records(path.read_bytes()):
            raise ValueError("Reviewed mine map binding missing")
    exact(client / "map/map/mine.7z", ARCHIVE)
    native = exact(client / "map/map/mine.DMap", DMAP)
    terrain = exact(runtime / "database/maps/1021.map", TERRAIN)
    w, h = struct.unpack_from("<II", native, 268)
    if (w, h) != (96, 96):
        raise ValueError("Reviewed dimensions changed")
    cells = bytes(struct.unpack_from("<H", native, 276 + y*(w*6+4) + x*6)[0]
                  for y in range(h) for x in range(w))
    if struct.pack("<II", w, h) + cells != terrain:
        raise ValueError("Client/server terrain disagreement")
    sites = [tuple(map(int, p)) for p in re.findall(r"new HerbSite\((\d+),(\d+)\)",
             re.search(r"DenSites=\{(.*?)\};", source, re.S)[1])]
    if len(sites) != value("DenLackeys") or len(set(sites)) != len(sites):
        raise ValueError("Distinct lackey sites required")
    landing = value("DenLandingX"), value("DenLandingY")
    exit_site = value("DenExitX"), value("DenExitY")
    leader = value("DenLeaderX"), value("DenLeaderY")
    return dict(base=identity, adaptation=True, landing=landing,
                terrain_sha256=TERRAIN, native_acceptance=False,
                routes=routes(terrain, [exit_site]+sites+[leader], landing, [exit_site]+sites+[leader], reach=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--client", type=Path, required=True)
    parser.add_argument("--runtime", type=Path, default=Path("bin/Debug"))
    args = parser.parse_args()
    print(json.dumps(audit(Path(__file__).resolve().parents[1], args.client, args.runtime), indent=2))
