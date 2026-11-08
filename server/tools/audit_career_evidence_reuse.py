"""Map missing fresh receipts to retained evidence without upgrading its claims."""
import argparse
import hashlib
import json
from pathlib import Path


def report(root):
    names = ["FRESH_COMPATIBILITY_PLAN", "RETAINED_EARNING_REVIEW",
             "RETAINED_OBSERVED_PROVENANCE"]
    paths = [root / "docs/research" / (name + ".json") for name in names]
    plan, earning, observed = [json.loads(p.read_text(encoding="utf-8")) for p in paths]
    candidates = {r["id"] for r in plan["absent_identities"] if not r["planning_excluded"]}
    groups = []
    covered = set()
    for kind, source in [("earning", earning), ("observed", observed)]:
        for index, group in enumerate(source["groups"]):
            ids = sorted(candidates.intersection(group["ids"]))
            if not ids:
                continue
            if covered.intersection(ids):
                raise ValueError("Overlapping evidence groups")
            covered.update(ids)
            groups.append({"index_kind": kind, "group_index": index,
                           "ids": ids, "evidence": group["evidence"],
                           "fixture_symbols": group.get("fixture_symbols", []),
                           "retained_finding": group.get("finding", group.get("details")),
                           "decision": "Reuse route evidence; review prerequisites before replay, not fresh credit"})
    changed = []
    missing = []
    for kind, source in [("earning", earning), ("observed", observed)]:
        for name, digest in source["reviewed_input_sha256"].items():
            path = root / name
            if not path.is_file():
                missing.append({"index": kind, "path": name})
            elif hashlib.sha256(path.read_bytes()).hexdigest() != digest:
                changed.append({"index": kind, "path": name})
    return {
        "scope": "Evidence reuse map; no new earning, runtime pass or compatibility certification",
        "fresh_checkpoint": plan["checkpoint"],
        "input_sha256": {p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in paths},
        "fresh_candidate_receipts_absent": len(candidates),
        "mapped_to_retained_review": len(covered),
        "unmapped": sorted(candidates - covered),
        "changed_review_inputs": changed, "missing_review_inputs": missing,
        "groups": groups,
        "compatible_career_verified": False,
        "limits": [
            "Fresh export predates the completed solo Arena batch; absence is not current database absence.",
            "Existing source review is not independently earned evidence or proof of joint compatibility.",
            "Retained supplied starting profile/funding and peer qualifications remain disclosed.",
            "Changed inputs require scoped inspection, not automatic invalidation or whole-career replay.",
            "A mapped route may still have activation, catalog, native or release gates."]}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    output = root / "docs/research/CAREER_EVIDENCE_REUSE.json"
    data = report(root)
    text = json.dumps(data, indent=2) + "\n"
    if args.check:
        if output.read_text(encoding="utf-8") != text:
            raise SystemExit("Evidence reuse report is stale")
    else:
        output.write_text(text, encoding="utf-8")
    print(json.dumps({k: data[k] for k in ["fresh_candidate_receipts_absent", "mapped_to_retained_review", "unmapped", "changed_review_inputs", "missing_review_inputs"]}, indent=2))
