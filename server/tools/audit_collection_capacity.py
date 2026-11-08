"""Read-only wardrobe catalog capacity audit; counts are not earning evidence."""
import argparse
import hashlib
import json
from pathlib import Path
from audit_five_star_rewards import decode, rows


def item_key(row):
    # Localized/custom display names do not change numeric identity or eligibility.
    return [row[0]] + row[2:6]


def reviewed_mount_proficiency(identity, category, server_row, client_row):
    # ItemHandler's reborn and first-life equip paths exempt SteedArmor from
    # weapon proficiency. Restrict this reconciliation to the four reviewed
    # 6711 identities and exact numeric preimages, not arbitrary mismatches.
    return (category == b"2" and identity in (200621, 200622, 200623, 200624)
            and item_key(server_row) == [str(identity).encode(), b"0", b"1", b"0", b"0"]
            and item_key(client_row) == [str(identity).encode(), b"0", b"0", b"0", b"0"])


def compare(server, client, server_items, client_items):
    for catalog in (server, client):
        if any(len(row) < 8 or row[2] not in (b"1", b"2") for row in catalog.values()):
            raise ValueError("Unreviewed wardrobe category or truncated row")
    result = {}
    for category, name in ((b"1", "garments"), (b"2", "mounts")):
        loaded = {i for i, r in server.items() if r[2] == category}
        native = {i for i, r in client.items() if r[2] == category}
        compatible = {i for i in native if i in server_items and i in client_items
                      and len(server_items[i]) >= 6 and len(client_items[i]) >= 6
                      and (item_key(server_items[i]) == item_key(client_items[i])
                           or reviewed_mount_proficiency(i, category, server_items[i], client_items[i]))}
        shared = compatible & loaded
        exclusions = []
        for identity in sorted(native-compatible):
            reasons = []
            if identity not in server_items:
                reasons.append("missing-server-item")
            if identity not in client_items:
                reasons.append("missing-client-item")
            if identity in server_items and identity in client_items:
                if len(server_items[identity]) < 6 or len(client_items[identity]) < 6:
                    reasons.append("truncated-item-identity")
                elif item_key(server_items[identity]) != item_key(client_items[identity]):
                    reasons.append("item-identity-or-eligibility-disagreement")
            exclusions.append(dict(id=identity,reasons=reasons))
        result[name] = dict(server_designs=len(loaded), client_designs=len(native),
                            reviewed_nonbinding_proficiency_ids=sorted(i for i in compatible
                                if item_key(server_items[i]) != item_key(client_items[i])),
                            shared_category_designs=len(loaded & native),
                            shared_compatible_item_designs=len(shared),
                            shared_compatible_item_ids=sorted(shared),
                            compatible_client_designs=len(compatible),
                            compatible_client_ids=sorted(compatible),
                            compatible_display_name_differences=sorted(i for i in compatible if server_items[i][1] != client_items[i][1]),
                            compatible_missing_server_wardrobe=sorted(compatible-loaded),
                            missing_metadata=[dict(id=i,category=int(client[i][2]),index=int(client[i][1]),stars=int(client[i][7]))
                                              for i in sorted(compatible-loaded)],
                            incompatible_client_item_ids=sorted(native-compatible),
                            incompatible_client_items=exclusions,
                            server_only_or_wrong_category=sorted(loaded-native),
                            shared_star_disagreements=sorted(i for i in loaded & native if server[i][7] != client[i][7]),
                            thresholds=[dict(count=n, shared_catalog_can_support=len(shared) >= n,
                                             repaired_catalog_can_support=len(compatible) >= n,
                                             ordinary_earning_verified=False) for n in (1,10,50,100,200)])
    return result


def audit(root):
    paths = dict(server_wardrobe=root/"Source/Revora-6711/bin/Debug/database/coat_storage_type.txt",
                 client_wardrobe=root/"6711/ini/coat_storage_type.dat",
                 server_items=root/"Source/Revora-6711/bin/Debug/database/itemtype-new.txt",
                 client_items=root/"6711/ini/itemtype.dat",
                 server_subitems=root/"Source/Revora-6711/bin/Debug/database/ItemtypeSub.txt",
                 client_subitems=root/"6711/ini/ItemtypeSub.dat")
    data = {k: p.read_bytes() for k, p in paths.items()}
    server_items=rows(data["server_items"],b"@@")
    server_items.update(rows(data["server_subitems"],b"@@"))
    client_items=rows(decode(data["client_items"]),b"@@")
    client_subitems=rows(decode(data["client_subitems"]),b"@@")
    # Do not infer client precedence for conflicting definitions.
    ambiguous={i for i in client_items.keys() & client_subitems.keys() if client_items[i][:6] != client_subitems[i][:6]}
    client_items.update(client_subitems)
    for identity in ambiguous: client_items.pop(identity)
    categories = compare(rows(data["server_wardrobe"],None), rows(decode(data["client_wardrobe"]),b"@@"),
                         server_items,client_items)
    return dict(hashes={k:hashlib.sha256(v).hexdigest() for k,v in data.items()},
                scope="Numeric catalog capacity only. Excludes supplier, rendering, rental-policy and per-character earning acceptance.",
                categories=categories)


if __name__ == "__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root",type=Path,default=Path(__file__).resolve().parents[3])
    parser.add_argument("--output",type=Path,required=True)
    parser.add_argument("--check",action="store_true")
    args=parser.parse_args()
    result=audit(args.root)
    serialized=json.dumps(result,indent=2)+"\n"
    if args.check:
        if args.output.read_text(encoding="utf-8") != serialized:
            raise SystemExit("Collection capacity evidence changed; review catalogs")
    else:
        args.output.write_text(serialized,encoding="utf-8")
    for category, values in result["categories"].items():
        print(category+": shared compatible="+str(values["shared_compatible_item_designs"])
              +", compatible client="+str(values["compatible_client_designs"]))
