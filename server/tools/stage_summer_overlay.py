"""Prepare a private, unsigned 6711 Summer item overlay; never install it."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import tempfile

SOURCE_SHA256 = "d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a"
MATERIALS = {
    3303704: "Take 30 bound fragments to Liora in Twin City (460,281) for permanent bound Glow Wings in one of four random colors. Earn fragments from her daily hunt.",
    3303705: "Take 99 bound fragments to Liora in Twin City (460,281) for permanent bound Siren Song. Earn fragments from her daily hunt.",
    3303706: "Take 10 bound fragments to Liora in Twin City (460,281) for one Magic Rainbow Wand. Earn fragments from her daily hunt.",
    3303707: "Take 5 bound ingredients to Liora in Twin City (460,281) for one Wings Unbind Solution. Earn ingredients from her daily hunt.",
}
TOOLS = {
    3303764: "Ask Liora in Twin City (460,281) to spend this wand and dye an unopened Glow Wings package to another color you choose.",
    3303765: "Ask Liora in Twin City (460,281) to unbind an unopened wing package. Glow Wings always succeed; Siren Song has a 1 in 6 chance. Every attempt spends one solution.",
}
NAMES = ("Emerald Glow Wings", "Orange Glow Wings", "Flame Glow Wings", "Bright Glow Wings", "Siren Song")
DESCRIPTIONS = dict(MATERIALS)
DESCRIPTIONS.update(TOOLS)
for start in (3303772, 3303797):
    for color, name in enumerate(NAMES):
        DESCRIPTIONS[start + color] = (
            "Activate this unopened package to permanently add " + name + " to your Wardrobe. "
            + ("Ask Liora about dyeing or unbinding before activation." if start == 3303772
               else "This unbound package can be traded before activation.")
        )
IDENTITIES = frozenset(DESCRIPTIONS) - frozenset(MATERIALS)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def key_bytes():
    seed = 0x2537
    result = []
    for _ in range(128):
        seed = (seed * 214013 + 2531011) & 0xFFFFFFFF
        result.append((seed >> 16) & 255)
    return result


def transform(data, encode=False):
    key = key_bytes()
    out = bytearray(len(data))
    for i, value in enumerate(data):
        shift = i % 8
        if encode:
            out[i] = (((value << shift) | (value >> (8 - shift))) & 255) ^ key[i % 128]
        else:
            value ^= key[i % 128]
            out[i] = ((value >> shift) | (value << (8 - shift))) & 255
    return bytes(out)


def revise(plain):
    rows = []
    changed = []
    seen = set()
    for row in plain.splitlines(keepends=True):
        fields = row.split(b"@@")
        try:
            item = int(fields[0])
        except ValueError:
            rows.append(row)
            continue
        if item not in DESCRIPTIONS:
            rows.append(row)
            continue
        if item in seen or len(fields) != 66 or fields[46] != b"10000":
            raise ValueError("Unexpected or repeated Summer item row: " + str(item))
        seen.add(item)
        revised = fields[:]
        revised[53] = DESCRIPTIONS[item].replace(" ", "~").encode("ascii")
        if item in IDENTITIES:
            revised[46] = b"1"
        modified = [i for i in range(66) if fields[i] != revised[i]]
        expected = [46, 53] if item in IDENTITIES else [53]
        if modified != expected:
            raise ValueError("Unexpected changed fields")
        output = b"@@".join(revised)
        changed.append({"item": item, "fields": modified,
                        "row_before_sha256": digest(row), "row_after_sha256": digest(output)})
        rows.append(output)
    if seen != set(DESCRIPTIONS):
        raise ValueError("Summer item catalog is incomplete")
    return b"".join(rows), changed


def stage(source, destination):
    source = source.resolve(strict=True)
    destination = destination.resolve()
    client = source.parent.parent
    if source.name.lower() != "itemtype.dat" or source.parent.name.lower() != "ini":
        raise ValueError("Select the approved client's ini/itemtype.dat")
    if destination == client or client in destination.parents or destination.exists():
        raise ValueError("Use a new separate staging directory outside the client")
    original = source.read_bytes()
    if digest(original) != SOURCE_SHA256:
        raise ValueError("Client itemtype preimage is not the reviewed 6711 profile")
    plain, changed = revise(transform(original))
    encoded = transform(plain, encode=True)
    if transform(encoded) != plain:
        raise ValueError("Client overlay round trip failed")
    manifest = {
        "purpose": "Private Summer tooltip and item-identity overlay; not signed or approved for release",
        "source": str(source), "source_sha256": SOURCE_SHA256,
        "destination": "ini/itemtype.dat", "output_sha256": digest(encoded),
        "changes": changed, "live_client_acceptance": "pending",
        "rollback": "Original client was not modified. Do not install this unsigned private stage.",
    }
    destination.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="summer-stage-", dir=destination.parent) as temporary:
        work = Path(temporary)
        (work / "ini").mkdir()
        (work / "ini/itemtype.dat").write_bytes(encoded)
        (work / "review.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        if digest(source.read_bytes()) != SOURCE_SHA256:
            raise ValueError("Source changed during staging")
        # Windows rename fails if the destination appeared concurrently.
        os.rename(work, destination)
    return manifest


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    args = parser.parse_args()
    result = stage(args.source, args.destination)
    print("Staged 16 reviewed Summer rows; SHA256 " + result["output_sha256"])
    print("Unsigned private stage only. Original client unchanged; live acceptance pending.")
