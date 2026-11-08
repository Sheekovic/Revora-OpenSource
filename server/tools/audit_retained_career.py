"""Report stored receipts without equating them to independently earned achievements."""
import argparse
import collections
import csv
import hashlib
import json
from pathlib import Path


def report(root, snapshot):
    data = json.loads(snapshot.read_text(encoding="utf-8"))
    if data["checkpoint"] != 70:
        raise ValueError("Expected the completed retained sign-in checkpoint")
    catalog = list(csv.DictReader((root / "docs/research/ACHIEVEMENT_PRODUCER_INVENTORY.csv").open(encoding="utf-8")))
    known = {int(row["achievement_id"]) for row in catalog}
    receipts = data["receipts"]
    ids = [row["achievement_id"] for row in receipts]
    if len(ids) != len(set(ids)) or set(ids) - known:
        raise ValueError("Duplicate or unknown achievement receipt")
    sources = {row["achievement_id"]: row["source"] for row in receipts}
    if any(not isinstance(source, str) or not source for source in sources.values()):
        raise ValueError("Missing receipt source")
    missing = [row for row in catalog if int(row["achievement_id"]) not in sources]
    counts = collections.Counter(row["source_reference"].split(":")[0] for row in missing)
    return {
        "scope": "Private retained career; receipt presence is not independent earning proof",
        "sign_in_days": data["checkpoint"], "promotion_school_step": data.get("school_step", 0),
        "passive_checkpoint": data.get("passive_checkpoint", False),
        "portal_checkpoint": data.get("portal_checkpoint", False),
        "snapshot_sha256": hashlib.sha256(snapshot.read_bytes()).hexdigest(),
        "catalog_sha256": hashlib.sha256((root / "docs/research/ACHIEVEMENT_PRODUCER_INVENTORY.csv").read_bytes()).hexdigest(),
        "catalog_count": len(known), "stored_identity_count": len(ids),
        "missing_identity_count": len(missing), "compatible_career_verified": False,
        "source_counts": dict(sorted(collections.Counter(sources.values()).items())),
        "missing_by_primary_producer": dict(sorted(counts.items(), key=lambda item: (-item[1], item[0]))),
        "identities": [{"id": int(row["achievement_id"]),
                        "receipt_source": sources.get(int(row["achievement_id"])),
                        "producer": row["source_reference"]} for row in catalog],
        "limitations": [
            "Starting-profile, incidental travel and observed-state receipts require provenance review; no independently earned total is inferred.",
            "Documented mastery/quest routes remain evidence even when their receipt source is observed-state.",
            "Missing receipts include unavailable routes; this report does not authorize activation or grant credit.",
            "This dated snapshot is not a live completion percentage or native-client acceptance."]}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--snapshot", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = report(args.root, args.snapshot)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("Stored identities:", result["stored_identity_count"], "Missing:", result["missing_identity_count"])
