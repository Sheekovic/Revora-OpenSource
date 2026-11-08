"""Inventory the sparse achievement catalog and reviewed direct producer calls.

This is source evidence, not a playable-route or client-acceptance certificate.
The output contains numeric interoperability facts and Revora source references;
it does not copy client descriptions or proprietary assets.
"""
import argparse
import csv
import io
from pathlib import Path
import re


def catalog_ids(source):
    section = re.search(r"Masks\s*=\s*\{(.*?)\};", source, re.S)
    if not section:
        raise ValueError("Achievement masks missing")
    masks = [int(value, 16) for value in re.findall(r"0x([0-9A-Fa-f]+)u", section[1])]
    if len(masks) != 13:
        raise ValueError("Unexpected catalog word count")
    ids = [(101 + group) * 100 + bit + 1 for group, mask in enumerate(masks) for bit in range(32) if mask & (1 << bit)]
    expected = re.search(r"const int Count\s*=\s*(\d+)", source)
    if not expected or len(ids) != int(expected[1]):
        raise ValueError("Sparse catalog count mismatch")
    return ids


def inventory(root):
    catalog = root / "Game/Features/AchievementCatalog.cs"
    source = catalog.read_text(encoding="utf-8-sig")
    ids = catalog_ids(source)
    producers = {item: [] for item in ids}
    observations = source[source.index("internal static int[] Observe"):]
    for match in re.finditer(r"ids\.Add\((\d+)\)", observations):
        item = int(match[1])
        if item not in producers:
            raise ValueError("Unknown observed achievement")
        producers[item].append("Game/Features/AchievementCatalog.cs:Observe")
    pattern = re.compile(r"ProgressionEvidence\.Complete\s*\(\s*[^,]+,\s*[^,]+,\s*[^,]+,\s*(\d+)\s*,")
    for path in sorted(list((root / "Game/Features").rglob("*.cs")) + [root / "Game/Attacking/Handle.cs"]):
        text = path.read_text(encoding="utf-8-sig")
        # The reviewed reset preservation loop reuses the same observable mastery
        # IDs; it adds no achievement identity. Permit only this exact call site.
        if path == catalog:
            shared_mastery = ('foreach (int id in ObserveMasteries(player))\n'
                              '                ProgressionEvidence.Complete(c, t, player.Entity.UID, id, "observed-state", "predicate-v1", 1);')
            if text.count(shared_mastery) != 1:
                raise ValueError("Mastery preservation producer changed; review required")
            text = text.replace(shared_mastery, shared_mastery.replace("ProgressionEvidence.Complete", "ReviewedMasteryObservation"))
        calls = list(re.finditer(r"ProgressionEvidence\.Complete\s*\(", text))
        matches = list(pattern.finditer(text))
        if len(matches) != len(calls):
            raise ValueError("A new dynamic producer needs explicit review: " + str(path.relative_to(root)))
        for match in matches:
            item = int(match[1])
            if item not in producers:
                raise ValueError("Unknown completion achievement")
            producers[item].append(path.relative_to(root).as_posix() + ":" + str(text.count("\n", 0, match.start()) + 1))
    return [{"achievement_id": item, "source_status": "producer-present" if producers[item] else "no-trusted-producer",
             "source_reference": ";".join(producers[item]), "live_acceptance": "pending"} for item in ids]


def serialize(rows):
    stream = io.StringIO(newline="")
    writer = csv.DictWriter(stream, fieldnames=["achievement_id", "source_status", "source_reference", "live_acceptance"], lineterminator="\n")
    writer.writeheader()
    writer.writerows(rows)
    return stream.getvalue()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--output", type=Path)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    rows = inventory(args.root)
    result = serialize(rows)
    if args.output:
        if args.check:
            if args.output.read_text(encoding="utf-8-sig") != result:
                raise SystemExit("Achievement inventory is stale")
        else:
            args.output.write_text(result, encoding="utf-8")
    present = sum(row["source_status"] == "producer-present" for row in rows)
    print(f"Catalog: {len(rows)}; producer present: {present}; missing producer: {len(rows)-present}.")
    print(f"Source upper bound {present}/320; this is not proof of a per-character route.")
