"""Compose a private Pirate journal delta over Tempest v2; no install or signing."""
import json
import tempfile
from pathlib import Path, PurePosixPath
from prepare_pirate_journal import ROOT, PREIMAGE, DX9, rewrite, sha

WORK = ROOT / "Source/Revora-6711/obj/chi-refill-validation"
BASE = "tempest-wings-composed-v2"
DESTINATION = "pirate-journal-composed-v1"
MANIFEST = "4758432c1cf0627bdc501d7791224e3c82dba8cde606d593b1351a8e3f746100"
JOURNAL = "c54419fbe46aa9ea166d3111a6ecc7528b53002da33a1cb166bf3ffa08cccbe4"
AFTER = "4e54218fe0e58b75406cda15c71f4d4f633698db7eca17681b9a50618aac224b"


def checked(path, expected):
    data = path.read_bytes()
    if sha(data) != expected:
        raise ValueError("Reviewed input differs: " + str(path))
    return data


def prepare():
    live = ROOT / "6711/ini/Questinfo.ini"
    checked(live, PREIMAGE)
    checked(ROOT / "6711/Env_DX9/Conquer.exe", DX9)
    base = WORK / BASE
    source = json.loads(checked(base / "manifest.json", MANIFEST))
    outputs, seen, verified = [], set(), []
    journal = None
    for row in source["outputs"]:
        relative = row["path"]
        path = PurePosixPath(relative)
        if path.is_absolute() or str(path) != relative or ".." in path.parts or "\\" in relative or ":" in relative or relative in seen:
            raise ValueError("Invalid or duplicate bundle destination")
        seen.add(relative)
        data = checked(base / relative, row["sha256"])
        if len(data) != row["bytes"]:
            raise ValueError("Bundle size differs")
        verified.append((base / relative, row["sha256"]))
        if relative == "client/ini/Questinfo.ini":
            if sha(data) != JOURNAL:
                raise ValueError("Journal base differs")
            journal = rewrite(data)
            if sha(journal) != AFTER:
                raise ValueError("Journal rewrite differs")
            outputs.append(dict(destination=relative, source=DESTINATION+"/"+relative, sha256=AFTER, bytes=len(journal)))
        else:
            outputs.append(dict(destination=relative, source=BASE+"/"+relative, sha256=row["sha256"], bytes=row["bytes"]))
    if journal is None or len(outputs) != 10:
        raise ValueError("Incomplete reviewed bundle")
    manifest = dict(purpose="Private resolved quest bundle with Pirate journal correction", approved_dx9=DX9,
                    source_root="obj/chi-refill-validation", base=BASE, base_manifest_sha256=MANIFEST,
                    outputs=outputs, missions_changed=[6868], installed=False, signed=False, published=False,
                    native_acceptance=False, standalone=False,
                    rollback="Active files unchanged. Retain the pinned base bundle and installed originals; transactional installation remains pending.")
    artifacts = {"client/ini/Questinfo.ini": journal,
                 "manifest.json": (json.dumps(manifest, indent=2)+"\n").encode()}
    destination = WORK / DESTINATION
    if destination.exists():
        for name, data in artifacts.items():
            if (destination / name).read_bytes() != data:
                raise ValueError("Existing private composition differs")
    else:
        temporary = Path(tempfile.mkdtemp(prefix=".pirate-journal-", dir=WORK))
        for name, data in artifacts.items():
            target = temporary / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
            if target.read_bytes() != data:
                raise ValueError("Composition readback failed")
        for path, digest in verified:
            checked(path, digest)
        checked(base / "manifest.json", MANIFEST)
        checked(live, PREIMAGE)
        temporary.rename(destination)
    for row in outputs:
        checked(WORK / row["source"], row["sha256"])
    checked(live, PREIMAGE)
    print(json.dumps(dict(journal=AFTER, resolved_files=len(outputs), inherited_files=9,
                         installed=False, signed=False, published=False), indent=2))
    return manifest


if __name__ == "__main__":
    prepare()
