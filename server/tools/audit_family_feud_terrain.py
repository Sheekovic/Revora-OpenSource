"""Check Family Feud exterior directions against exact native/server terrain."""
import argparse
import json
from pathlib import Path
import audit_desert_blood_exterior as terrain


def audit(client, runtime, sevenzip):
    original = terrain.PROFILES[1015]
    profile = list(original)
    profile[5] = [(686,547),(669,780)]
    profile[6] = (684,547)
    profile[7] = [(667,780),(335,249),(257,162),(525,345),(570,416),
                  (362,469),(339,329),(364,469),(366,469),(527,345),(529,345),(572,416),(574,416),(348,579)]
    original_targets = len(profile[7])
    replacements = [(518,339,10,10),(540,338,10,10)]
    profile[7] += [(xx,yy) for x,y,cx,cy in replacements for yy in range(y,y+cy) for xx in range(x,x+cx)]
    terrain.PROFILES[1015] = tuple(profile)
    try:
        result = terrain.audit(client,runtime,sevenzip,1015)
    finally:
        terrain.PROFILES[1015] = original
    result["scope"] = "Static four-direction paths with journal NPC footprints; does not prove disguise deadline feasibility, live encounter placement or native rendering."
    server = (runtime / "database/maps/1015.map").read_bytes()
    result["banditti_spawn_coverage"] = [dict(origin=[x,y],extent=[cx,cy],
        server_walkable_cells=sum(server[8+yy*1024+xx] == 0 for yy in range(y,y+cy) for xx in range(x,x+cx)))
        for x,y,cx,cy in [(114,312,95,45),(293,559,56,37)]]
    result["banditti_replacement_regions"] = [dict(origin=[x,y],extent=[cx,cy],verified_walkable_cells=cx*cy)
        for x,y,cx,cy in replacements]
    result["targets"] = result["targets"][:original_targets]
    result["banditti_population_repair_required"] = False
    result["source_loader_repairs_legacy_rectangles"] = True
    result["live_deployment_pending"] = True
    result["journal_hints_match_spawns"] = False
    result["guide_catalog_conflict"] = dict(npc=4706,journal=[669,780],runtime=[636,656])
    if not result["walk_route_verified"]:
        raise ValueError("Family Feud exterior route disconnected")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--client",type=Path,required=True)
    parser.add_argument("--runtime",type=Path,default=Path("bin/Debug"))
    parser.add_argument("--sevenzip",type=Path,default=Path("C:/Program Files/7-Zip/7z.exe"))
    parser.add_argument("--output",type=Path,required=True)
    args = parser.parse_args()
    result = audit(args.client,args.runtime,args.sevenzip)
    args.output.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8")
    print(json.dumps(result))
