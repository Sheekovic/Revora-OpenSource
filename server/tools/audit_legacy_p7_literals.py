"""Read-only candidate inventory, not a C# reachability or economy verifier.

Includes comment/dead-code references intentionally. Does not discover computed
IDs, aliases, arbitrary containers, or data-driven grants. JSON goes to stdout;
redirect it to an ignored artifact if retaining the per-line inventory.
"""
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
CATALOG = ROOT / "bin/Debug/database/itemtype-new.txt"
FILES = ("Npcs.cs", "Network/GamePackets/ItemUsage.cs",
         "Network/GamePackets/ItemUsageOld.cs", "Network/GamePackets/ItemUsageRenew.cs")


def main():
    raw = CATALOG.read_bytes()
    identities = set()
    for line in raw.decode("utf-8", errors="replace").splitlines():
        fields = line.split("@@")
        if len(fields) > 55 and fields[0].isdigit() and fields[55] == "7":
            if 800000 <= int(fields[0]) < 900000:
                identities.add(fields[0])
    if not identities:
        raise ValueError("No P7 catalog identities; inspect the input format")
    packs = {"3004247", "3004248"}
    pattern = re.compile(r"(?<![\w])(" + "|".join(sorted(identities | packs)) + r")(?![\w])")
    result = {"catalog_sha256": hashlib.sha256(raw).hexdigest(),
              "p7_identity_count": len(identities), "files": []}
    for relative in FILES:
        source = (ROOT / relative).read_bytes()
        regions, references = [], []
        for number, line in enumerate(source.decode("utf-8", errors="replace").splitlines(), 1):
            trimmed = line.strip()
            if trimmed.startswith("#region"):
                regions.append(trimmed[7:].strip())
            elif trimmed.startswith("#endregion") and regions:
                regions.pop()
            found = pattern.findall(line)
            if found:
                references.append({"line": number, "ids": found,
                                   "region": " / ".join(regions),
                                   "line_comment": trimmed.startswith("//")})
        result["files"].append({"path": relative,
                                "sha256": hashlib.sha256(source).hexdigest(),
                                "references": references})
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
