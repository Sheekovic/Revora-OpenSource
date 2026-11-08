"""Prepare matching Anniversary bead catalog text; never install or open an event."""
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
SERVER_HASH = "1ed7a23b7a34b5e3706277f97e421e3cdf0b269eeb5be7367a4ebc0e4f22b3ef"
CLIENT_HASH = "d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a"
COMPOSED_HASH = "799b083e2effd8c80d99f07f22bd70091d3ce29319a3770d1258b70987434164"
DX9_HASH = "3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60"
FIELDS = {1: b"AnniversaryBead", 52: b"RevoraEvent", 53: (
    "Gather 15,000 spirits while level 140, twice reborn and VIP 6+. Right click to claim twenty cards, "
    "50,000 RVPs, 50 million silver, 100 Study Points and two days of blessing. Newly issued event beads "
    "also grant one bound P7 soul. Shares your daily Spirit Ball reward."
).replace(" ", "~").encode("ascii")}


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
        if fields[0] != b"3001172":
            rows.append(row); continue
        found += 1
        if found != 1 or len(fields) != 66:
            raise ValueError("Ambiguous Anniversary bead row")
        if fields[1] not in (b"VirtuePack(A)", FIELDS[1]):
            raise ValueError("Unreviewed bead identity")
        for index, value in FIELDS.items():
            fields[index] = value
        rows.append(b"@@".join(fields))
    if found != 1:
        raise ValueError("Missing Anniversary bead")
    return b"".join(rows)


def prepare():
    constants = (PROJECT / "ServerBase/Constants.cs").read_text()
    if 'ItemBaseInfosPath = "database\\\\itemtype-new.txt"' not in constants:
        raise ValueError("Loaded catalog path changed")
    sources = [(PROJECT / "bin/Debug/database/itemtype-new.txt", SERVER_HASH),
               (CLIENT / "ini/itemtype.dat", CLIENT_HASH),
               (WORK / "dis-city-difficulty-overlay-v2/ini/itemtype.dat", COMPOSED_HASH),
               (CLIENT / "Env_DX9/Conquer.exe", DX9_HASH)]
    server, installed, composed, executable = [checked(path, expected) for path, expected in sources]
    server_after = rewrite(server)
    client_plain = rewrite(transform(composed))
    client_after = transform(client_plain, True)
    if transform(client_after) != client_plain or rewrite(client_plain) != client_plain or rewrite(server_after) != server_after:
        raise ValueError("Anniversary catalog round trip failed")
    destination = WORK / "anniversary-bead-catalog"
    if destination.exists():
        raise ValueError("Existing private candidate must not be overwritten")
    manifest = dict(purpose="private Anniversary bead identity correction", item=3001172,
                    fields=sorted(FIELDS), signed=False, installed=False, published=False,
                    native_acceptance="pending", admission="unchanged; calendar and missing NPC remain unresolved",
                    sources=[dict(path=str(path), sha256=expected) for path, expected in sources],
                    outputs={"server/database/itemtype-new.txt": sha(server_after), "client/ini/itemtype.dat": sha(client_after)},
                    rollback="No installed files changed. Future installation must validate and back up both installed catalogs transactionally.")
    with tempfile.TemporaryDirectory(prefix="anniversary-catalog-", dir=WORK) as temporary:
        stage = Path(temporary)
        for relative, content in [("server/database/itemtype-new.txt", server_after), ("client/ini/itemtype.dat", client_after)]:
            target = stage / relative; target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(content)
        (stage / "review.json").write_text(json.dumps(manifest, indent=2) + "\n")
        for path, expected in sources:
            checked(path, expected)
        os.rename(stage, destination)
    print(json.dumps(manifest["outputs"], indent=2))


if __name__ == "__main__":
    prepare()
