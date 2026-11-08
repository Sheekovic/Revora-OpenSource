"""Keep source producer totals separate from the unproven 320-achievement career."""
import argparse
import hashlib
import json
from pathlib import Path
from audit_achievement_producers import inventory


def report(root):
    plan = json.loads((root / "docs/research/ACHIEVEMENT_CAREER_GAPS.json").read_text(encoding="utf-8"))
    for name, expected in plan["reviewed_input_sha256"].items():
        if hashlib.sha256((root / name).read_bytes()).hexdigest() != expected:
            raise ValueError("Career evidence changed; review classifications: " + name)
    rows = inventory(root)
    known = {r["achievement_id"] for r in rows}
    missing = {r["achievement_id"] for r in rows if r["source_status"] == "no-trusted-producer"}
    issues = plan["issues"]
    ids = [i["id"] for i in issues]
    if len(ids) != len(set(ids)) or set(ids) - known or set(ids) & missing:
        raise ValueError("Unknown, duplicate or double-counted career issue")
    if any(i["classification"] not in ("unavailable", "supplier-unproven", "admission-unproven") for i in issues):
        raise ValueError("Unsupported career classification")
    unavailable = {i["id"] for i in issues if i["classification"] == "unavailable"}
    suppliers = {i["id"] for i in issues if i["classification"] == "supplier-unproven"}
    admissions = {i["id"] for i in issues if i["classification"] == "admission-unproven"}
    ceiling = len(known - missing - unavailable)
    planning = len(known - missing - unavailable - suppliers - admissions)
    return dict(catalog_count=len(known), source_producer_count=len(known - missing),
                missing_producers=sorted(missing), reviewed_unavailable=sorted(unavailable),
                supplier_routes_unproven=sorted(suppliers),
                admission_routes_unproven=sorted(admissions),
                ceiling_after_known_unavailable=ceiling,
                unproven_planning_pool=planning, target=plan["target"],
                planning_margin=planning-plan["target"],
                compatible_career_verified=False,
                limitation="All remaining entries still require compatible ordinary earning evidence; neither count is a playable total.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--output", type=Path)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    text = json.dumps(report(args.root), indent=2) + "\n"
    if args.check:
        if not args.output or args.output.read_text(encoding="utf-8") != text:
            raise SystemExit("Career summary is missing or stale")
    elif args.output:
        args.output.write_text(text, encoding="utf-8")
    print(text, end="")
