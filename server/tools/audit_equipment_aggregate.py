"""Audit the approved Revora equipment aggregate mapping and source hooks."""
import argparse
import hashlib
import json
from pathlib import Path
import re
from audit_quest_prerequisites import SOURCE_SHA256

# Reviewed final transaction mappings, not inferred from client journal status.
CHAINS = [('connector', 1718, 1722, 6, 3, 'ConnectorQuest.cs'), ('spun_gold', 1723, 1726, 8, 2, 'SpunGoldStore.cs'), ('azure_pill', 1727, 1732, 7, 2, 'AzurePillQuest.cs'), ('artisan_feather', 1733, 1737, 6, 2, 'ArtisanFeatherQuest.cs'), ('mystic_ring', 1738, 1744, 10, 2, 'MysticRingStore.Rewards.cs'), ('frantic_monkeys', 1745, 1750, 10, 2, 'FranticMonkeysStore.Rewards.cs'), ('soul_general', 1751, 1757, 12, 2, 'SoulGeneralStore.Rewards.cs'), ('mad_man', 1781, 1786, 9, 3, 'MadManStore.Rewards.cs'), ('ghost_city', 1787, 1794, 11, 3, 'GhostCityStore.Rewards.cs'), ('tomboy', 1795, 1799, 6, 3, 'TomboyStore.Rewards.cs'), ('desert_blood', 1800, 1807, 11, 3, 'DesertBloodStore.Rewards.cs'), ('hero_wings', 1808, 1818, 12, 3, 'HeroWingsStore.Rewards.cs'), ('family_feud', 1819, 1827, 10, 3, 'FamilyFeudStore.Rewards.cs'), ('desert_treasure', 1828, 1834, 8, 3, 'DesertTreasureStore.Rewards.cs'), ('desert_expedition', 1835, 1839, 6, 3, 'DesertExpeditionStore.Rewards.cs'), ('power_book', 1840, 1852, 14, 2, 'PowerBookRewards.cs'), ('defense_talisman', 520, 528, 10, 2, 'DefenseTalismanStore.Final.cs'), ('attack_talisman', 400, 409, 11, 2, 'AttackTalismanStore.Garden.cs'), ('tempest_wings', 200, 200, 4, 2, 'TempestWingsStore.Encounter.cs')]

def audit(raw,root):
    if hashlib.sha256(raw).hexdigest()!=SOURCE_SHA256:
        raise ValueError("Unreviewed quest catalog")
    return coverage(raw.decode("cp1252"),root)


def coverage(text,root):
    named={};blank=[]
    for block in re.split(r"(?m)^\[",text)[1:]:
        pairs=[line.split("=",1) for line in block.splitlines()[1:] if "=" in line]
        fields=dict(pairs)
        if fields.get("TypeId")!="4":continue
        for key in ("TypeId","MissionId","Name","Prequest"):
            if len([p for p in pairs if p[0]==key])!=1:raise ValueError("Ambiguous equipment field")
        identity=int(fields["MissionId"])
        if identity in named or identity in blank:raise ValueError("Duplicate equipment mission")
        if fields["Name"].strip():named[identity]=fields
        else:blank.append(identity)
    expected=[i for _,first,last,_,_,_ in CHAINS for i in range(first,last+1)]
    if len(expected)!=len(set(expected)) or set(named)!=set(expected):raise ValueError("Named equipment coverage changed")
    if sorted(blank)!=list(range(950,968)):raise ValueError("Equipment placeholders changed")
    result=[]
    for key,first,last,stage,count,source in CHAINS:
        for identity in range(first,last+1):
            parent=527 if identity==400 else 0 if identity==first else identity-1
            if named[identity]["Prequest"]!=str(parent):raise ValueError("Listed chain predecessor changed")
        path=root/"Game/Features"/source
        source_bytes=path.read_bytes()
        if ("revora_"+key).encode() not in source_bytes:raise ValueError("Mapped receipt source changed")
        result.append(dict(key=key,missions=list(range(first,last+1)),final_stage=stage,
            table="revora_"+key,scope="per_character" if key in ("defense_talisman","attack_talisman","tempest_wings") else "per_rebirth",
            reward_count=count,reward_receipt="revora_power_book_rewards(uid,life,pack_uid,soul_uid)" if key=="power_book" else "rewards bigint[]",
            source=str(path.relative_to(root)).replace("\\","/"),source_sha256=hashlib.sha256(source_bytes).hexdigest()))
    return dict(achievement=10931,source_sha256=SOURCE_SHA256,chains=result,named_missions=len(named),
        excluded_unnamed_missions=sorted(blank),original_aggregate_rule_verified=False,owner_edition_decision="approved-cross-life-2026-09-30",
        candidate_rule="All19 restored chains, across saved rebirths for the same character; final stages plus complete reward receipts.",
        caveats=["Catalog category coverage is not proof of original achievement rules", "Source hashes locate reviewed final transactions, not semantic runtime proof", "Fan original predecessor527 differs from reviewed Revora final Tower528", "Missing tables or incomplete receipts cannot qualify", "Consumed reward items must not erase durable completion"],
        enabled=True,native_acceptance=False)


if __name__=="__main__":
    p=argparse.ArgumentParser(description=__doc__);p.add_argument("--client",type=Path,required=True);p.add_argument("--root",type=Path,default=Path(__file__).resolve().parents[1]);p.add_argument("--output",type=Path,required=True);a=p.parse_args()
    result=audit((a.client/"ini/Questinfo.ini").read_bytes(),a.root)
    a.output.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8")
    print("Approved aggregate:19 reviewed chains cover132 named missions;18 placeholders excluded. Native acceptance pending.")
