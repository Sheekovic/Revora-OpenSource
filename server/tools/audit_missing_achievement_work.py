"""Map missing producers to bounded work packages; never certify player access."""
import argparse
import hashlib
import json
from pathlib import Path

from audit_achievement_producers import inventory
from ClientPatches.prepare_shop_buy_hotfix import transform

CLIENT_HASH = "df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc"
# Numeric identities reviewed against the hash-bound client catalog. Descriptions
# are Revora work instructions, not copied client strings or recovered scripts.
PACKAGES = [
    ("social", [10119, 10128, 10129, 10130],
     "Validate summon ownership, atomic booth purchase, mutual marriage and paired movement before recording their distinct receipts."),
    ("guild", list(range(10221, 10229)) + [10231, 10232, 10303],
     "Review authoritative appointments, arsenal ownership, enemy membership, paid rewards and event combat boundaries."),
    ("arena_occupation", [10302],
     "Resolve which event and occupation outcome the client identity means before adding a durable earning producer; Guild War, CTF and custom pole events are not interchangeable evidence."),
    ("clan", list(range(10304, 10310)),
     "Record accepted membership and five distinct territory outcomes from durable event results."),
    ("rank_and_honor", [10318, 10320, 11120, 11123],
     "Resolve rank and title identities; distinguish first honor payment from pending prizes and rank snapshots."),
    ("scythe", [11212],
     "Validate installed Scythe family 511 progression and persistence; display-only identity 550 is not a supported alias."),
    ("equipment_state", list(range(10409, 10420)) + [10428, 10429, 10430, 10432, 10503],
     "Define valid owned equipment slots, alternate sets and exclusions; validate whole-set thresholds and durable observations."),
    ("equipment_transactions", list(range(10420, 10427)) + [10431, 10501, 10502],
     "Attach success/failure evidence to committed upgrade, socket, refinery and soul operations without duplicate charges."),
    ("monster_combat", list(range(10520, 10526)) + [10527, 10528],
     "Verify KO counters, monster-origin loot, unarmed eligibility and exact boss identities using owned combat outcomes."),
    ("player_combat", [10605, 10606, 10607, 10611, 10902, 10919],
     "Capture victim state at legitimate defeat, accepted revival and authoritative aura creation; exclude simulated actors."),
    ("quest_aggregates", [10930, 10931],
     "For each listed missing aggregate, select a compatible quest edition with complete prerequisite graphs and per-character receipts; completed aggregates are excluded from this package."),
    ("arena", list(range(11101, 11110)) + [11111, 11112, 11309],
     "Persist opponent class, daily participation and ordered wins from actual arena matches; define restart-safe day boundaries."),
    ("racing", [11113, 11122],
     "Validate course completion and frozen winning results before rewarding participation and victory separately."),
    ("flower_ranks", list(range(11124, 11128)),
     "Resolve four authoritative flower queen rankings; do not substitute the custom Mr./Ms. contest without evidence."),
    ("wardrobe_collection", list(range(11224, 11233)) + [11301],
     "Define distinct owned design counts and temporary-item policy; exercise earned deposits, withdrawal, reload and threshold replay."),
]


def report(root, client_bytes):
    if hashlib.sha256(client_bytes).hexdigest() != CLIENT_HASH:
        raise ValueError("Achievement catalog changed; review identities first")
    plain = transform(client_bytes)
    if transform(plain, True) != client_bytes:
        raise ValueError("Achievement codec round trip failed")
    native = []
    for line in plain.splitlines():
        fields = line.split()
        if len(fields) > 1 and fields[1].isdigit() and int(fields[1]) > 0:
            native.append(int(fields[1]))
    rows = inventory(root)
    if len(native) != len(set(native)) or set(native) != {r["achievement_id"] for r in rows}:
        raise ValueError("Client and server achievement identities differ")
    missing = {r["achievement_id"] for r in rows if r["source_status"] == "no-trusted-producer"}
    planned = [i for _, ids, _ in PACKAGES for i in ids]
    if len(planned) != len(set(planned)) or missing - set(planned):
        raise ValueError("Missing or duplicate work-package classification")
    packages = [dict(package=name, missing_ids=sorted(missing.intersection(ids)), required_work=work)
                for name, ids, work in PACKAGES if missing.intersection(ids)]
    return dict(client_sha256=CLIENT_HASH, catalog_count=len(rows),
                source_producer_count=len(rows)-len(missing), missing_producer_count=len(missing),
                minimum_additional_identities_for_320=max(0, 320-(len(rows)-len(missing))),
                playable_route_verified=False,
                limitation="Source presence includes staged services and does not prove compatible per-character earning or native acceptance.",
                packages=packages)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--client", type=Path, required=True)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    result = json.dumps(report(args.root, (args.client / "ini/Achievement.dat").read_bytes()), indent=2) + "\n"
    if args.check:
        if args.output.read_text(encoding="utf-8") != result:
            raise SystemExit("Missing achievement work inventory is stale")
    else:
        args.output.write_text(result, encoding="utf-8")
    print("Missing achievement work inventory verified against source and hash-bound client.")
