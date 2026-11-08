"""Stage private unsigned item tips for owner-defined Magnolia rewards."""
import argparse
import hashlib
import json
from pathlib import Path
from stage_summer_overlay import transform

SOURCE = "d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a"
IDS = {721269, 720957}
TIP = b"Revora~reward~pack:~one~random~bound~P7~Dragon~Soul,~1,000~RVPs~and~5,000~CPs.~The~soul~is~always~bound.~Right~click~to~open."


def revise(plain):
    seen = set()
    rows = []
    for row in plain.splitlines(keepends=True):
        fields = row.split(b"@@")
        try:
            identity = int(fields[0])
        except ValueError:
            rows.append(row)
            continue
        if identity in IDS:
            if identity in seen or len(fields) != 66:
                raise ValueError("Unexpected custom pack row")
            seen.add(identity)
            fields[53] = TIP
            row = b"@@".join(fields)
        rows.append(row)
    if seen != IDS:
        raise ValueError("Custom pack identities missing")
    return b"".join(rows)


def stage(client, destination):
    client = client.resolve(strict=True)
    destination = destination.resolve()
    if destination == client or client in destination.parents or destination.exists():
        raise ValueError("Use a new private destination outside the active client")
    original = (client / "ini/itemtype.dat").read_bytes()
    if hashlib.sha256(original).hexdigest() != SOURCE:
        raise ValueError("Reviewed client item catalog changed")
    plain = transform(original)
    changed = revise(plain)
    output = transform(changed, encode=True)
    if transform(output) != changed:
        raise ValueError("Item cipher round trip failed")
    (destination / "ini").mkdir(parents=True)
    (destination / "ini/itemtype.dat").write_bytes(output)
    result = dict(private=True, signed=False, deployed=False, source_sha256=SOURCE,
                  target_sha256=hashlib.sha256(output).hexdigest(), items=sorted(IDS), changed_fields=[53])
    (destination / "review.json").write_text(json.dumps(result, indent=2)+"\n", encoding="utf-8")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--client", type=Path, required=True)
    parser.add_argument("--destination", type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(stage(args.client, args.destination), indent=2))
