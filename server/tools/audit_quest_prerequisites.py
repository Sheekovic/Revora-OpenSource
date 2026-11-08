"""Read the reviewed 6711 quest catalog and report numeric prerequisite gaps."""
import argparse
from collections import defaultdict
import hashlib
import json
from pathlib import Path
import re

SOURCE_SHA256 = "f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14"



def newcomer_candidate(by_id):
    """Numeric candidate evidence only; never certify achievement10930's rule."""
    ids = [3478, 3479, 3480, 3481]
    rows = []
    for item in ids:
        copies = by_id.get(item, [])
        if len(copies) != 1:
            return {"mission_ids": ids, "unique_complete_chain": False,
                    "reason": "missing or duplicate candidate mission", "original_aggregate_verified": False}
        node = copies[0]
        def npc(key):
            parts = node.get(key, "").split(",")[:4]
            return list(map(int, parts)) if len(parts) == 4 and all(p.isdigit() for p in parts) else None
        rows.append({"mission_id": item,
                     "listed_predecessors": list(map(int, re.findall(r"\d+", node.get("Prequest", "")))),
                     "type": int(node.get("TypeId", "0")),
                     "minimum_level": int(node.get("Lv_min", "0")),
                     "maximum_level": int(node.get("Lv_max", "0")),
                     "profession": node.get("Profession"), "sex": node.get("Sex"),
                     "begin_npc_map_xy": npc("BeginNpcId"), "finish_npc_map_xy": npc("FinishNpcId")})
    linear = all(r["listed_predecessors"] == [0 if i == 0 else ids[i-1]] for i, r in enumerate(rows))
    return {"mission_ids": ids, "unique_complete_chain": linear, "rows": rows,
            "original_aggregate_verified": False,
            "limitation": "Listed linear prerequisites do not establish achievement10930 or live NPC availability."}


def analyze(text):
    nodes = []
    for block in re.split(r"(?m)^\[", text)[1:]:
        fields = dict(line.split("=", 1) for line in block.splitlines()[1:] if "=" in line)
        if fields.get("MissionId", "").isdigit():
            nodes.append(fields)
    by_id = defaultdict(list)
    for node in nodes:
        by_id[int(node["MissionId"])].append(node)
    result = {"records": len(nodes), "unique_mission_ids": len(by_id),
              "duplicate_mission_ids": sorted(item for item, copies in by_id.items() if len(copies) > 1), "categories": {}}
    for category in (1, 2, 4):
        selected = [n for n in nodes if n.get("TypeId") == str(category)]
        missing, reversals = set(), []
        for node in selected:
            for parent in map(int, re.findall(r"\d+", node.get("Prequest", ""))):
                if parent == 0:
                    continue
                if parent not in by_id:
                    missing.add(parent)
                elif int(node.get("Lv_max", "200")) < min(int(p.get("Lv_min", "0")) for p in by_id[parent]):
                    reversals.append({"parent": parent, "child": int(node["MissionId"]), "child_max_level": int(node["Lv_max"])})
        result["categories"][str(category)] = {
            "record_count": len(selected),
            "blank_name_ids": sorted(int(n["MissionId"]) for n in selected if not n.get("Name", "").strip()),
            "missing_listed_predecessors": sorted(missing),
            "level_reversals": reversals,
            "named_root_ids": sorted(int(n["MissionId"]) for n in selected if n.get("Prequest", "0") == "0" and n.get("Name", "").strip()),
        }
    result["newcomer_intro_candidate"] = newcomer_candidate(by_id)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    raw = args.source.read_bytes()
    if hashlib.sha256(raw).hexdigest() != SOURCE_SHA256:
        raise SystemExit("Quest catalog differs from the reviewed profile")
    result = {"source_sha256": SOURCE_SHA256, "interpretation": "Listed edges only; branch semantics and live acceptance are not inferred.", **analyze(raw.decode("cp1252"))}
    output = json.dumps(result, indent=2) + "\n"
    if args.output:
        args.output.write_text(output, encoding="utf-8")
    print(output)
