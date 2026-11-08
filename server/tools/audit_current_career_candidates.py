"""Reconcile current saved identities; never treat receipt counts as earning proof."""
import argparse
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
INPUTS = ["docs/research/CURRENT_CAREER_RECEIPTS.json",
          "Game/Features/AchievementCatalog.cs",
          "docs/research/RETAINED_COMPATIBILITY_PLAN.json",
          "docs/research/RETAINED_COLLECTION_200.json"]


def report():
    data = json.loads((ROOT / INPUTS[0]).read_text(encoding="utf-8-sig"))
    catalog = (ROOT / INPUTS[1]).read_text(encoding="utf-8-sig")
    masks = re.search(r"Masks = \{(.*?)\};", catalog, re.S).group(1)
    words = [int(value, 16) for value in re.findall(r"0x([0-9A-Fa-f]+)u", masks)]
    identities = {(101 + word) * 100 + bit + 1 for word, mask in enumerate(words)
                  for bit in range(32) if mask & (1 << bit)}
    if len(identities) != 337:
        raise ValueError("Unexpected catalog; review the candidate model")
    previous = json.loads((ROOT / INPUTS[2]).read_text(encoding="utf-8-sig"))
    collection = json.loads((ROOT / INPUTS[3]).read_text(encoding="utf-8-sig"))
    if collection["result"] != "passed" or collection["mounts"] < 200 or collection["achievement"] != 11232:
        raise ValueError("200-mount exclusion cannot be removed without its earning evidence")
    early = set(previous["retained_promotion_routes_unproven"])
    gated = {10726, 11010, 11031}
    candidates = identities - early - gated
    def owned(key):
        rows = data[key]
        values = {row["achievement_id"] for row in rows}
        if len(values) != len(rows) or not values <= identities:
            raise ValueError("Duplicated or unknown saved achievement")
        return values
    retained, fresh = owned("retained_receipts"), owned("fresh_receipts")
    if 11232 not in retained:
        raise ValueError("Collection receipt absent from the current retained export")
    return {
        "scope": "Current saved-state reconciliation, not a jointly earned career certificate",
        "input_sha256": {name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest() for name in INPUTS},
        "fresh_checkpoint": data["checkpoint"],
        "catalog_count": len(identities), "target": 320,
        "retained_early_promotions_excluded_as_unproven": sorted(early),
        "activation_gated_exclusions": sorted(gated),
        "retained_conservative_candidates": len(candidates),
        "candidate_margin": len(candidates) - 320,
        "retained_saved_receipts": len(retained),
        "retained_candidate_receipts_present": len(retained & candidates),
        "retained_candidate_receipts_absent": sorted(candidates - retained),
        "fresh_saved_receipts": len(fresh),
        "fresh_candidate_receipts_present": len(fresh & candidates),
        "compatible_career_verified": False,
        "limitations": [
            "Early-rank exclusions belong to the retained route, not a claim of global impossibility.",
            "Saved rows include mixed-provenance historical evidence; counts alone prove no earning path.",
            "Activation gates remain even if private opt-in receipts exist.",
            "The fresh and retained characters are separate identities; their receipts must not be added together.",
            "Native/client/deployment and remaining compatibility boundaries remain open."
        ]
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    result = report()
    destination = ROOT / "docs/research/CURRENT_CAREER_CANDIDATES.json"
    if args.check:
        if json.loads(destination.read_text()) != result:
            raise SystemExit("Candidate reconciliation is stale")
    else:
        destination.write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps({key: result[key] for key in ["retained_conservative_candidates", "candidate_margin", "retained_candidate_receipts_absent", "fresh_saved_receipts", "compatible_career_verified"]}))
