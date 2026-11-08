"""Report retained-career planning constraints without treating receipts as earned proof."""
import hashlib
import json
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
INPUTS = (
    "docs/research/RETAINED_CAREER_PROGRESS.json",
    "docs/research/ACHIEVEMENT_CAREER_GAPS.json",
    "Game/Features/RebirthRules.cs",
    "Game/Features/ReincarnationRules.cs",
    "Game/Features/ClassPromotion.cs",
    "Game/Features/PaidReincarnation.cs",
    "Game/Features/FirstRebirth.cs",
    "Game/Features/SecondRebirth.cs",
    "Network/GamePackets/Reincarnation.cs",
    "Game/Features/Reincarnation/Handle.cs",

)


def report():
    progress = json.loads((ROOT / INPUTS[0]).read_text(encoding="utf-8-sig"))
    gaps = json.loads((ROOT / INPUTS[1]).read_text(encoding="utf-8-sig"))
    records = progress["identities"]
    ids = {row["id"] for row in records}
    if len(ids) != len(records) or len(ids) != progress["catalog_count"]:
        raise ValueError("Catalog identities are incomplete or duplicated")
    unavailable = {row["id"] for row in gaps["issues"] if row["classification"] == "unavailable"}
    missing = {row["id"] for row in records if row["receipt_source"] is None}
    # These are gaps of the retained route, not declarations of global impossibility.
    promotion_gaps = {row["id"] for row in records
                      if row["id"] in missing and row["producer"].startswith("Game/Features/ClassPromotion.cs:")}
    excluded = unavailable | promotion_gaps
    candidates = ids - excluded
    outstanding = [row for row in records if row["id"] in candidates & missing]
    target = gaps["target"]
    return {
        "scope": "Snapshot-based retained-career planning; not an earned count or completion percentage",
        "input_sha256": {name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest() for name in INPUTS},
        "catalog_count": len(ids),
        "target": target,
        "currently_unavailable": sorted(unavailable),
        "retained_promotion_routes_unproven": sorted(promotion_gaps),
        "overlap_between_exclusions": sorted(unavailable & promotion_gaps),
        "conservative_candidate_count": len(candidates),
        "candidate_margin_over_target": len(candidates) - target,
        "candidate_receipts_present_mixed_provenance": len(candidates - missing),
        "candidate_receipts_absent": len(outstanding),
        "absent_by_primary_producer": dict(sorted(Counter(row["producer"].split(":")[0] for row in outstanding).items(), key=lambda pair: (-pair[1], pair[0]))),
        "early_promotion_route_audit": {
            "ordinary_rebirth_targets": [11, 21, 41, 51, 61, 71, 81, 132, 142, 161],
            "ordinary_packet_entry": "Network/GamePackets/Reincarnation.cs -> PaidReincarnation.Process -> ReincarnationRules.Profile",
            "constraint": "Reviewed rites do not return this existing character to unpromoted x0/100 or Taoist101; selecting a promoted class does not earn its trainer receipt.",
            "legacy_helpers": "Direct class setters/legacy constructors do not establish an ordinary earning route; bounded caller inspection found no alternative ordinary route for the retained profile.",
            "fresh_character_distinction": "Creation can select an unpromoted school on a new identity; that is not recovery of the retained character's missing receipts.",
            "global_impossibility_proven": False,
        },
        "compatible_career_verified": False,
        "limitations": [
            "Excluding an unproven promotion route is a planning assumption, not proof it is impossible.",
            "Candidate identities may still have incompatible prerequisites; this is not a playable upper/lower bound.",
            "Present receipts include supplied and observed state; they are not all independently earned.",
            "Private opt-in receipts do not override default activation or native/deployment gates.",
            "Receipt absence does not by itself identify an implementation defect.",
        ],
    }


if __name__ == "__main__":
    result = report()
    destination = ROOT / "docs/research/RETAINED_COMPATIBILITY_PLAN.json"
    destination.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: result[key] for key in (
        "conservative_candidate_count", "candidate_margin_over_target",
        "candidate_receipts_present_mixed_provenance", "candidate_receipts_absent",
        "compatible_career_verified")}, indent=2))
