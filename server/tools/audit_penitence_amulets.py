"""Read-only 6711 Penitence Amulet contract audit; emits no proprietary rows."""
import argparse
import json
from pathlib import Path

from audit_warrior_epic import digest, item_rows, require


def audit(client, runtime):
    identities = {720128, 723727}
    inputs = {
        "client": (client / "ini/itemtype.dat", True,
                   "d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a"),
        "server": (runtime / "database/itemtype.txt", False,
                   "594ffe5f252c9445a6087004372519a6f150f089c7bff42d4ec30400911a0e88"),
    }
    catalogs, hashes = {}, {}
    for label, (path, encrypted, expected) in inputs.items():
        data = path.read_bytes()
        hashes[label] = digest(data)
        require(hashes[label] == expected, "Reviewed catalog changed: " + label)
        catalogs[label] = item_rows(data, encrypted, identities)
        for identity, fields in catalogs[label].items():
            require(fields[1] == b"PenitenceAmulet", "Unexpected item identity")
            text = b" ".join(fields).replace(b"~", b" ").lower()
            for phrase in (b"by 30 pts", b"once per hour", b"once per 30 min in deity",
                           b"no restriction for vip3+"):
                require(phrase in text, "Amulet rule changed: " + str(identity))
    for identity in identities:
        require(catalogs["client"][identity][:8] == catalogs["server"][identity][:8],
                "Client/server identity requirements differ")
    regions = (client / "ini/region.ini").read_bytes()
    hashes["client_regions"] = digest(regions)
    require(hashes["client_regions"] == "939c68eb8a7d00b6a21adbf494ab421140b8f7b7f139f5eb7a2c9645ef404618",
            "Reviewed region catalog changed")
    deity = [line.split() for line in regions.splitlines() if b"Deityland" in line]
    require(deity and all(row[0] == b"10250" for row in deity), "Deityland map identity changed")
    require((runtime / "database/maps/10250.map").is_file(), "Server Deityland terrain missing")
    return {"sha256": hashes, "items": sorted(identities), "pk_reduction": 30,
            "default_cooldown_minutes": 60, "deity_cooldown_minutes": 30,
            "catalog_vip_exemption_minimum": 3,
            "deity_map": 10250,
            "limits": "Catalog/map identity only; VIP exception precedence remains an interpretation. No live mutations or native use validation."}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--client", type=Path, default=Path(__file__).resolve().parents[3] / "6711")
    parser.add_argument("--runtime", type=Path, default=Path(__file__).resolve().parents[1] / "bin/Debug")
    args = parser.parse_args()
    print(json.dumps(audit(args.client, args.runtime), indent=2))
