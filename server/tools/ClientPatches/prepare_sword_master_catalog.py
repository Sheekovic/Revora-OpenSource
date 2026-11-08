"""Compose the reviewed Sword Soul tooltip into a private, non-installing candidate."""
import hashlib
import json
import os
from pathlib import Path
import sys
import tempfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from stage_summer_overlay import transform

PROJECT = Path(__file__).resolve().parents[2]
CLIENT = PROJECT.parents[1] / "6711"
WORK = PROJECT / "obj/chi-refill-validation"
INSTALLED_SERVER = "1ed7a23b7a34b5e3706277f97e421e3cdf0b269eeb5be7367a4ebc0e4f22b3ef"
INSTALLED_CLIENT = "d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a"
COMPOSED_SERVER = "9cb94ddc59c9fa557ecc69e623f78ad2c770fb355cc4d2551f98cab6212f9816"
COMPOSED_CLIENT = "2b633bc5a137be8067d57e41fa3ffd07a73f64765b68bf736fd92b7cd933a465"
QUEST_HASH = "09dd2f2d01d5b775696f1d532eaeb0a2854aeb3adc024ec6a00e216e2b8aaf82"
DX9_HASH = "3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60"
DESCRIPTION = (
    "Lv100+. Use in your own house once daily. Gather your team; defeat SwordMaster within 30 minutes. "
    "Leaving or disconnecting ends the hunt. Nearby teammates earn 20 Study Points. "
    "Drops two random P1-P3 souls/accessories and one bound P7 soul."
).replace(" ", "~").encode("ascii")
OLD = b"Lv99+~players~can~use~it~in~their~house~to~summon~the~SwordMaster,~once~a~day.~Defeat~it~to~get~Dragon/Martial~Souls!"


def sha(data):
    return hashlib.sha256(data).hexdigest()


def checked(path, expected):
    data = path.read_bytes()
    if sha(data) != expected:
        raise ValueError("Reviewed profile changed: " + str(path))
    return data


def rewrite(data):
    rows, found = [], 0
    for row in data.splitlines(keepends=True):
        fields = row.split(b"@@")
        if fields[0] != b"720842":
            rows.append(row)
            continue
        found += 1
        if found != 1 or len(fields) != 66 or fields[1] != b"SwordSoul" or fields[52] != b"Treasure":
            raise ValueError("Ambiguous Sword Soul identity")
        if fields[53] not in (OLD, OLD.replace(b"+~", bytes([253]), 1), DESCRIPTION):
            raise ValueError("Unreviewed Sword Soul description")
        fields[53] = DESCRIPTION
        rows.append(b"@@".join(fields))
    if found != 1:
        raise ValueError("Missing Sword Soul")
    return b"".join(rows)


def prepare():
    sources = [
        (PROJECT / "bin/Debug/database/itemtype-new.txt", INSTALLED_SERVER),
        (CLIENT / "ini/itemtype.dat", INSTALLED_CLIENT),
        (WORK / "anniversary-bead-catalog/server/database/itemtype-new.txt", COMPOSED_SERVER),
        (WORK / "anniversary-bead-catalog/client/ini/itemtype.dat", COMPOSED_CLIENT),
        (WORK / "dis-city-difficulty-overlay-v2/ini/Questinfo.ini", QUEST_HASH),
        (CLIENT / "Env_DX9/Conquer.exe", DX9_HASH),
    ]
    contents = [checked(path, expected) for path, expected in sources]
    server = rewrite(contents[2])
    client_plain = rewrite(transform(contents[3]))
    client = transform(client_plain, True)
    if transform(client) != client_plain or rewrite(server) != server or rewrite(client_plain) != client_plain:
        raise ValueError("Sword Soul catalog round trip failed")
    outputs = {"server/database/itemtype-new.txt": server, "client/ini/itemtype.dat": client,
               "client/ini/Questinfo.ini": contents[4]}
    destination = WORK / "sword-master-catalog"
    if destination.exists():
        raise ValueError("Existing private candidate must not be overwritten")
    manifest = dict(purpose="Sword Soul tooltip composed with existing reviewed quest content", item=720842, fields=[53],
                    signed=False, installed=False, published=False, native_acceptance="pending",
                    inherited="Anniversary, Magnolia bound-P7 custom packs, Summer, Labyrinth, Dis City and reviewed quest journal",
                    sources=[dict(path=str(path), sha256=expected) for path, expected in sources],
                    outputs={relative: sha(data) for relative, data in outputs.items()},
                    rollback="Installed files are unchanged. Any future installation must validate and back up installed preimages transactionally.")
    with tempfile.TemporaryDirectory(prefix="sword-master-catalog-", dir=WORK) as temporary:
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
