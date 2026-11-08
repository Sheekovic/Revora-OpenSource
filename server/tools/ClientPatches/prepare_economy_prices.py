"""Stage progression shop CP displays from the authoritative server catalog.

Never changes installed files or publishes a patch. Only field 36 is changed;
the existing native buy-path silver sentinel is deliberately preserved.
"""
import json
import re
import sys
from pathlib import Path
from prepare_shop_buy_hotfix import transform, sha

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from audit_warrior_epic import item_rows, sections, require

PROJECT = Path(__file__).resolve().parents[2]
CLIENT = PROJECT.parents[1] / "6711"
WORK = PROJECT / "obj/chi-refill-validation/economy-prices-active-catalog"


def stock(data, identity):
    matches = [v for _, v in sections(data) if v.get(b"ID") == str(identity).encode()]
    require(len(matches) == 1, "Missing or duplicate progression shop")
    shop = matches[0]
    require(shop.get(b"MoneyType") == b"1", "Shop currency changed")
    count = int(shop[b"ItemAmount"])
    values = [int(shop[("Item%d" % i).encode()]) for i in range(count)]
    require(len(set(values)) == count, "Duplicate shop item")
    return set(values)


def prepare():
    server = PROJECT / "bin/Debug/database"
    constants = (PROJECT / "ServerBase/Constants.cs").read_text()
    require('ItemBaseInfosPath = "database\\\\itemtype-new.txt"' in constants, "Active catalog path changed")
    before = (CLIENT / "ini/itemtype.dat").read_bytes()
    catalog = (server / "itemtype-new.txt").read_bytes()
    require(sha(before) == "d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a", "Client preimage changed")
    require(sha(catalog) == "1ed7a23b7a34b5e3706277f97e421e3cdf0b269eeb5be7367a4ebc0e4f22b3ef", "Server economy changed; review prices again")
    exe_hash = sha((CLIENT / "Env_DX9/Conquer.exe").read_bytes())
    require(exe_hash == "3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60", "Unapproved client")
    client_shops = (CLIENT / "ini/Shop.dat").read_bytes()
    server_shops = (server / "shops/Shop.dat").read_bytes()
    identities = set()
    for identity in (91008, 91009):
        items = stock(server_shops, identity)
        require(items == stock(client_shops, identity), "Client/server stock differs")
        identities.update(items)
    source = item_rows(catalog, False, identities)
    native = item_rows(before, True, identities)
    plain = transform(before)
    require(transform(plain, True) == before, "Preimage round trip failed")
    changes, lines, unavailable = [], [], []
    for line in plain.splitlines(keepends=True):
        if re.match(rb"^\d+@@", line):
            fields = line.split(b"@@")
            identity = int(fields[0])
            if identity in identities:
                require(fields[:8] == source[identity][:8], "Item identity differs")
                price = int(source[identity][36])
                require(0 <= price <= 0xffffffff, "Invalid server CP price")
                if price == 0:
                    unavailable.append(identity)
                    lines.append(line)
                    continue
                if int(fields[36]) != price:
                    changes.append(dict(id=identity, before=int(fields[36]), after=price))
                    fields[36] = str(price).encode()
                    line = b"@@".join(fields)
        lines.append(line)
    updated = b"".join(lines)
    after = transform(updated, True)
    require(transform(after) == updated, "Output round trip failed")
    actual = item_rows(after, True, identities)
    for identity in identities:
        if identity not in unavailable:
            require(actual[identity][36] == source[identity][36], "Price parity failed")
        require(actual[identity][:36] + actual[identity][37:] == native[identity][:36] + native[identity][37:], "Non-price fields changed")
    # Validate whole-file preservation, including unrelated rows and line endings.
    restored = []
    for line in updated.splitlines(keepends=True):
        if re.match(rb"^\d+@@", line):
            fields = line.split(b"@@")
            identity = int(fields[0])
            if identity in identities:
                fields[36] = native[identity][36]
                line = b"@@".join(fields)
        restored.append(line)
    require(b"".join(restored) == plain, "Changes exceed CP price fields")
    for relative, data in (("originals/ini/itemtype.dat", before), ("staged/ini/itemtype.dat", after)):
        path = WORK / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        if path.exists():
            require(path.read_bytes() == data, "Refusing to overwrite differing staged artifact")
        else:
            path.write_bytes(data)
    manifest = dict(status="private staging; unsigned; not deployed", shops=[91008, 91009],
                    inspected_items=len(identities), verified_priced_items=len(identities)-len(unavailable),
                    unavailable_zero_price_items=sorted(unavailable), catalog_parity=not unavailable, release_ready=False,
                    changes=changes, before=sha(before), after=sha(after),
                    server_catalog=sha(catalog), client_shops=sha(client_shops), server_shops=sha(server_shops),
                    approved_dx9=exe_hash, destination="ini/itemtype.dat",
                    limitation="Existing native silver affordability requirement remains. Genuine-client purchase acceptance pending.")
    (WORK / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    prepare()
