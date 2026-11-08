"""Stage a private classic-Labyrinth tooltip overlay on the reviewed Summer stage."""
import argparse
import json
import os
from pathlib import Path
import tempfile
from stage_summer_overlay import digest, transform

SOURCE_SHA256 = "c488ace14dffae62a6cebc95fcd48a9947a4accba5d129800d3e25c01cd6c210"
DESCRIPTIONS = {
    721533: "Collect from Slinger kills in the first Labyrinth floor through your personal satchel. Simon in Twin City (209,374) exchanges 17 bound Sun Diamonds for 2 bound Meteors.",
    721534: "Collect from Bladeling kills in the second Labyrinth floor through your personal satchel. Simon in Twin City (209,374) exchanges 17 bound Moon Diamonds for 4 bound Meteors.",
    721535: "Collect from AzureBird kills in the third Labyrinth floor through your personal satchel. Simon in Twin City (209,374) exchanges 17 bound Star Diamonds for one bound normal gem.",
    721536: "Collect from Minotaur kills in the fourth Labyrinth floor through your personal satchel. Simon in Twin City (209,374) exchanges 17 bound Cloud Diamonds for one bound Ancestor Box.",
    721537: "Collect from GoldGhost kills through your Labyrinth satchel. Give one bound Sky Token to the East General (488,372) on floor 1 to enter floor 2.",
    721538: "Collect from AgileRat kills through your Labyrinth satchel. Give one bound Earth Token to the West General (670,468) on floor 2 to enter floor 3.",
    721539: "Collect from FiendBat kills through your Labyrinth satchel. Give one bound Soul Token to the South General (788,545) on floor 3 to enter floor 4.",
    721540: "Simon's classic Labyrinth prize. Open with one empty bag space: 60% two bound Meteors, 30% one bound normal gem, 10% one bound DragonBall. Revora prize rules.",
    722880: "This permit belongs to the later Evil Labyrinth edition. Revora's classic Labyrinth uses a 2,000 Virtue entry fee and the Sky, Earth and Soul Tokens for passage.",
}


def revise(plain):
    rows, changes, seen = [], [], set()
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
        if item in seen or len(fields) != 66:
            raise ValueError("Unexpected or repeated Labyrinth row")
        seen.add(item)
        revised = fields[:]
        revised[53] = DESCRIPTIONS[item].replace(" ", "~").encode("ascii")
        output = b"@@".join(revised)
        if [i for i in range(66) if fields[i] != revised[i]] != [53]:
            raise ValueError("Unexpected changed fields")
        rows.append(output)
        changes.append({"item": item, "fields": [53], "row_before_sha256": digest(row), "row_after_sha256": digest(output)})
    if seen != set(DESCRIPTIONS):
        raise ValueError("Labyrinth tooltip catalog incomplete")
    return b"".join(rows), changes


def stage(source, destination):
    source, destination = source.resolve(strict=True), destination.resolve()
    if source.name.lower() != "itemtype.dat" or source.parent.name.lower() != "ini":
        raise ValueError("Select the reviewed Summer stage's ini/itemtype.dat")
    root = source.parent.parent
    if destination == root or root in destination.parents or destination.exists():
        raise ValueError("Use a new separate private staging directory")
    original = source.read_bytes()
    if digest(original) != SOURCE_SHA256:
        raise ValueError("Source is not the reviewed Summer overlay")
    plain, changes = revise(transform(original))
    output = transform(plain, True)
    if transform(output) != plain:
        raise ValueError("Overlay round trip failed")
    manifest = {"purpose": "Unsigned private Summer + classic Labyrinth tooltip stage", "source": str(source),
                "source_sha256": SOURCE_SHA256, "destination": "ini/itemtype.dat", "output_sha256": digest(output),
                "changes": changes, "live_client_acceptance": "pending", "rollback": "No installed client was changed."}
    destination.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="labyrinth-stage-", dir=destination.parent) as temporary:
        work = Path(temporary)
        (work / "ini").mkdir()
        (work / "ini/itemtype.dat").write_bytes(output)
        (work / "review.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        if digest(source.read_bytes()) != SOURCE_SHA256:
            raise ValueError("Source changed during staging")
        os.rename(work, destination)
    return manifest


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    args = parser.parse_args()
    result = stage(args.source, args.destination)
    print("Staged 9 Labyrinth descriptions; SHA256 " + result["output_sha256"])
