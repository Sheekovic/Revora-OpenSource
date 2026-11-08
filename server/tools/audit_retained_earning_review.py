"""Validate the manual review index; never infer earning from receipt presence."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def report(root=ROOT, review=None):
    def read(path):
        return json.loads((root / path).read_text(encoding="utf-8-sig"))

    if review is None:
        review = read("docs/research/RETAINED_EARNING_REVIEW.json")
    if review["uid"] != 4294907001 or review["compatible_career_verified"] is not False:
        raise ValueError("Unexpected review identity or unsupported completion claim")
    for path, expected in review["reviewed_input_sha256"].items():
        if hashlib.sha256((root / path).read_bytes()).hexdigest() != expected:
            raise ValueError("Review input changed: " + path)
    receipts = read("docs/research/RETAINED_CAREER_RECEIPTS.json")["receipts"]
    sources = {r["achievement_id"]: r["source"] for r in receipts}
    if len(sources) != len(receipts):
        raise ValueError("Duplicate receipt identity")
    fixture = (root / "tools/CheckCandidateCareerRites.cs").read_text(encoding="utf-8-sig")
    reviewed = set()
    for group in review["groups"]:
        ids = group["ids"]
        if not ids or len(ids) != len(set(ids)) or reviewed.intersection(ids):
            raise ValueError("Empty or duplicate reviewed identities")
        if any(sources.get(i) not in group["sources"] for i in ids):
            raise ValueError("Reviewed identity/source mismatch")
        if not group["finding"] or not group["evidence"] or not group["fixture_symbols"]:
            raise ValueError("Review lacks finding or evidence")
        for path in group["evidence"]:
            if path not in review["reviewed_input_sha256"]:
                raise ValueError("Unhashed review evidence: " + path)
        for symbol in group["fixture_symbols"]:
            if " " + symbol + "(" not in fixture:
                raise ValueError("Missing fixture symbol: " + symbol)
        reviewed.update(ids)
    observed = read("docs/research/RETAINED_OBSERVED_PROVENANCE.json")
    observed_ids = [i for group in observed["groups"] for i in group["ids"]]
    if (len(observed_ids) != len(set(observed_ids)) or reviewed.intersection(observed_ids)
            or set(observed_ids) != {i for i, source in sources.items() if source == "observed-state"}):
        raise ValueError("Observed-state review does not exactly partition its receipts")
    reviewed.update(observed_ids)
    pending = {i: source for i, source in sources.items() if i not in reviewed}
    findings = review.get("open_findings", [])
    finding_ids = set()
    for finding in findings:
        ids = finding["ids"]
        if (not ids or len(ids) != len(set(ids)) or finding_ids.intersection(ids)
                or any(pending.get(i) != finding["source"] for i in ids)
                or not finding["finding"] or not finding["next_action"]):
            raise ValueError("Open finding must reference unique pending identities and an action")
        finding_ids.update(ids)
    return {
        "scope": "Evidence review inventory, not independently earned count or completion percentage",
        "stored_identities": len(sources),
        "prerequisite_reviewed": len(reviewed) - len(observed_ids),
        "observed_state_reviewed": len(observed_ids),
        "remaining_source_review": len(pending),
        "remaining_by_source": dict(sorted(Counter(pending.values()).items())),
        "open_findings": findings,
        "compatible_career_verified": False,
        "limitation": "References and hashes prove index consistency only; manual review retains all fixture and native limitations.",
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    result = report()
    text = json.dumps(result, indent=2) + "\n"
    if args.check:
        if not args.output or args.output.read_text(encoding="utf-8") != text:
            raise SystemExit("Review queue is missing or stale")
    elif args.output:
        args.output.write_bytes(text.encode("utf-8"))
    print(json.dumps({k: v for k, v in result.items() if k != "remaining_by_source"}, indent=2))
