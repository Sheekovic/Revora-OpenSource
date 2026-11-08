"""Compose the approved weekend calendar into private server/client catalogs."""
import json
import os
from pathlib import Path
import tempfile

from prepare_anniversary_catalog import FIELDS
from prepare_sword_master_catalog import (
    CLIENT, PROJECT, WORK, DX9_HASH, INSTALLED_CLIENT, INSTALLED_SERVER,
    QUEST_HASH, checked, sha, transform,
)

COMPOSED_SERVER = "d88ac85ad5d618373301223d27f1bba91ebf6ee1249c9ba8c4fef702e0f4b606"
COMPOSED_CLIENT = "a348140eb0b55876308948ddb63e30aae94b10ba7d2a013f5364003b893e7703"
OLD = FIELDS[53]
DESCRIPTION = (
    "Lv140, twice reborn, VIP6+. New beads: AnniversaryEnvoy, TwinCity (323,307), Sat-Sun Pacific. "
    "Gather 15,000 spirits; right click for 20 cards, 50,000 RVPs, 50 million silver, 100 Study Points "
    "and 2 days of blessing. New event beads also give a bound P7 soul. Existing beads can finish "
    "any day. Shares your daily Spirit Ball reward."
).replace(" ", "~").encode("ascii")


def rewrite(data):
    result, found = [], 0
    for row in data.splitlines(keepends=True):
        fields = row.split(b"@@")
        if fields[0] != b"3001172":
            result.append(row)
            continue
        found += 1
        if (found != 1 or len(fields) != 66 or fields[1] != b"AnniversaryBead"
                or fields[52] != b"RevoraEvent" or fields[53] not in (OLD, DESCRIPTION)):
            raise ValueError("Unreviewed Anniversary bead record")
        fields[53] = DESCRIPTION
        result.append(b"@@".join(fields))
    if found != 1:
        raise ValueError("Missing Anniversary bead")
    return b"".join(result)


def prepare():
    sources = [
        (PROJECT / "bin/Debug/database/itemtype-new.txt", INSTALLED_SERVER),
        (CLIENT / "ini/itemtype.dat", INSTALLED_CLIENT),
        (WORK / "sword-master-catalog/server/database/itemtype-new.txt", COMPOSED_SERVER),
        (WORK / "sword-master-catalog/client/ini/itemtype.dat", COMPOSED_CLIENT),
        (WORK / "sword-master-catalog/client/ini/Questinfo.ini", QUEST_HASH),
        (CLIENT / "Env_DX9/Conquer.exe", DX9_HASH),
    ]
    contents = [checked(path, expected) for path, expected in sources]
    server = rewrite(contents[2])
    client_plain = rewrite(transform(contents[3]))
    client = transform(client_plain, True)
    if transform(client) != client_plain or rewrite(server) != server or rewrite(client_plain) != client_plain:
        raise ValueError("Anniversary weekend catalog round trip failed")
    outputs = {"server/database/itemtype-new.txt": server, "client/ini/itemtype.dat": client,
               "client/ini/Questinfo.ini": contents[4]}
    destination = WORK / "anniversary-weekend-catalog"
    if destination.exists():
        raise ValueError("Existing private candidate must not be overwritten")
    manifest = dict(purpose="Anniversary weekend calendar, envoy location and existing-bead completion text",
                    item=3001172, fields=[53], signed=False, installed=False, published=False,
                    native_acceptance="pending", inherited="All reviewed Sword Master and earlier quest catalog/journal content",
                    sources=[dict(path=str(path), sha256=expected) for path, expected in sources],
                    outputs={relative: sha(data) for relative, data in outputs.items()},
                    rollback="Installed files are unchanged. Future installation must verify and back up exact preimages transactionally.")
    with tempfile.TemporaryDirectory(prefix="anniversary-weekend-catalog-", dir=WORK) as temporary:
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
