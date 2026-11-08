"""Audit the fresh career without equating receipt presence with full earning proof."""
import argparse
import hashlib
import json
from collections import Counter
from pathlib import Path
from audit_achievement_producers import inventory


def report(root):
    receipt_path = root / "docs/research/FRESH_CAREER_RECEIPTS.json"
    plan_path = root / "docs/research/RETAINED_COMPATIBILITY_PLAN.json"
    evidence = json.loads(receipt_path.read_text(encoding="utf-8"))
    plan = json.loads(plan_path.read_text(encoding="utf-8-sig"))
    catalog = inventory(root)
    known = {row["achievement_id"] for row in catalog}
    receipts = evidence["receipts"]
    present = {row["achievement_id"] for row in receipts}
    if len(present) != len(receipts) or present - known:
        raise ValueError("Duplicate or unknown fresh receipt identity")
    unavailable = set(plan["currently_unavailable"])
    old_gaps = set(plan["retained_promotion_routes_unproven"])
    recovered = old_gaps & {r["achievement_id"] for r in receipts if r["source"] == "class-promotion-v1"}
    early_gaps = old_gaps - recovered
    candidates = known - unavailable - early_gaps
    absent = [row for row in catalog if row["achievement_id"] not in present]
    return {
        "scope": "Fresh private career planning; stored receipts are not an independent completion percentage",
        "checkpoint": evidence["checkpoint"],
        "snapshot_sha256": evidence["snapshot_sha256"],
        "input_sha256": {str(p.relative_to(root)).replace("\\", "/"): hashlib.sha256(p.read_bytes()).hexdigest()
                         for p in [receipt_path, plan_path, root / "Game/Features/AchievementCatalog.cs"]},
        "profile": evidence["profile"],
        "catalog_count": len(known), "target": plan["target"],
        "stored_identity_count": len(present), "absent_identity_count": len(known - present),
        "source_counts": dict(sorted(Counter(r["source"] for r in receipts).items())),
        "currently_unavailable": sorted(unavailable),
        "early_promotion_receipts_recovered_by_fresh_creation": sorted(recovered),
        "early_promotion_routes_still_unproven": sorted(early_gaps),
        "conservative_candidate_count": len(candidates),
        "candidate_margin_over_target": len(candidates) - plan["target"],
        "candidate_receipts_absent": len(candidates - present),
        "absent_by_primary_producer": dict(sorted(Counter(row["source_reference"].split(":")[0] for row in absent).items(), key=lambda p: (-p[1],p[0]))),
        "absent_identities": [{"id": row["achievement_id"], "source_reference": row["source_reference"],
                              "planning_excluded": row["achievement_id"] in unavailable | early_gaps} for row in absent],
        "compatible_career_verified": False,
        "limitations": [
            "The eleven remaining early-promotion exclusions inherit reviewed rite constraints; this is a planning assumption, not a global impossibility proof.",
            "Controlled travel coordinates generated region receipts; their presence does not prove independently navigated ordinary travel.",
            "Observed-state and equipment receipts require their documented profile/ownership context, not just row presence.",
            "The fresh journey preserves ordinary creation/earning but controls some timing, proximity and combat cooldowns; native acceptance remains pending.",
            "Source presence and candidate count do not prove jointly earnable prerequisites or public release readiness."
        ]
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    result = report(args.root.resolve())
    text = json.dumps(result, indent=2) + "\n"
    if args.check:
        if args.output.read_text(encoding="utf-8") != text:
            raise SystemExit("Fresh compatibility report is stale")
    else:
        args.output.write_text(text, encoding="utf-8")
    print(json.dumps({key: result[key] for key in ["stored_identity_count", "absent_identity_count", "early_promotion_receipts_recovered_by_fresh_creation", "conservative_candidate_count", "candidate_receipts_absent"]}))
