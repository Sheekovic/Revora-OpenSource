"""Stage the reviewed Purple Banshee tooltip and exact server separator repair."""
import json
import os
from pathlib import Path
import tempfile

from prepare_sword_master_catalog import (
    CLIENT, PROJECT, WORK, DX9_HASH, INSTALLED_CLIENT, INSTALLED_SERVER,
    QUEST_HASH, checked, sha, transform,
)

COMPOSED_SERVER = "0319015be2ce7b8fbd88c0146022f3d9056e0945d9e45c2f37ad4a2aac2a232a"
COMPOSED_CLIENT = "6e2b38adbb5caed754a35d7e7c7ddea646d57ac17289ed7c7d73d485bc70f730"
OLD_HEAD = b"Right~click~to~summon~a~Purple~Banshee~in~your~house~once~a~day.~Defeat~it,you"
OLD_TAIL = b"will~get~200~study~points~and~may~get~P5/P6~dragon~souls~or~refinery~items,teammates~will~gain~50~study~points."
OLD = OLD_HEAD + b" " + OLD_TAIL
DESCRIPTION = (
    "Right click in your own house to summon Purple Banshee. One attempt per Pacific day; "
    "30-minute hunt. Leaving, disconnecting or losing the whole team ends the hunt. "
    "Victory: 200 Study Points for the summoner, 50 per teammate; two drops, each 60% P5/P6 soul "
    "or 40% Lv5/6 refinery, plus one bound P7 soul. Banshee Spirit costs 35,000 Honor."
).replace(" ", "~").encode("ascii")


def rewrite(data, server=False):
    result, found = [], 0
    for row in data.splitlines(keepends=True):
        fields = row.split(b"@@")
        if fields[0] != b"3004259":
            result.append(row)
            continue
        found += 1
        if found != 1 or len(fields) not in (66, 67) or fields[1] != b"BansheeSpirit" or fields[52] != b"Item":
            raise ValueError("Unreviewed Banshee Spirit record")
        if len(fields) == 67:
            if not server or fields[53:55] != [OLD_HEAD, OLD_TAIL]:
                raise ValueError("Unreviewed Banshee Spirit separator defect")
            fields[53:55] = [OLD]
        if len(fields) != 66 or fields[53] not in (OLD, DESCRIPTION):
            raise ValueError("Unreviewed Banshee Spirit description")
        fields[53] = DESCRIPTION
        result.append(b"@@".join(fields))
    if found != 1:
        raise ValueError("Missing Banshee Spirit")
    return b"".join(result)


def prepare():
    sources = [
        (PROJECT / "bin/Debug/database/itemtype-new.txt", INSTALLED_SERVER),
        (CLIENT / "ini/itemtype.dat", INSTALLED_CLIENT),
        (WORK / "anniversary-weekend-catalog/server/database/itemtype-new.txt", COMPOSED_SERVER),
        (WORK / "anniversary-weekend-catalog/client/ini/itemtype.dat", COMPOSED_CLIENT),
        (WORK / "anniversary-weekend-catalog/client/ini/Questinfo.ini", QUEST_HASH),
        (CLIENT / "Env_DX9/Conquer.exe", DX9_HASH),
    ]
    contents = [checked(path, expected) for path, expected in sources]
    server = rewrite(contents[2], server=True)
    client_plain = rewrite(transform(contents[3]))
    client = transform(client_plain, True)
    if transform(client) != client_plain or rewrite(server, server=True) != server or rewrite(client_plain) != client_plain:
        raise ValueError("Purple Banshee catalog round trip failed")
    outputs = {"server/database/itemtype-new.txt": server, "client/ini/itemtype.dat": client,
               "client/ini/Questinfo.ini": contents[4]}
    destination = WORK / "purple-banshee-catalog"
    if destination.exists():
        raise ValueError("Existing private candidate must not be overwritten")
    manifest = dict(purpose="Purple Banshee contract and exact server description separator repair",
                    item=3004259, fields=[53], server_repair="Join reviewed split description fields 53/54; preserve all following logical fields", signed=False, installed=False, published=False,
                    native_acceptance="pending", inherited="All reviewed Anniversary weekend, Sword Master and earlier quest catalog/journal content",
                    sources=[dict(path=str(path), sha256=expected) for path, expected in sources],
                    outputs={relative: sha(data) for relative, data in outputs.items()},
                    rollback="Installed files are unchanged. Future installation must verify and back up exact preimages transactionally.")
    with tempfile.TemporaryDirectory(prefix="purple-banshee-catalog-", dir=WORK) as temporary:
        stage = Path(temporary)
        for relative, data in outputs.items():
            target = stage / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
        (stage / "review.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        for path, expected in sources:
            checked(path, expected)
        if destination.exists():
            raise ValueError("Staging destination appeared during preparation")
        os.rename(stage, destination)
    print(json.dumps(manifest["outputs"], indent=2))


if __name__ == "__main__":
    prepare()
