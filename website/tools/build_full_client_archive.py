#!/usr/bin/env python3
"""Build a deterministic full Revora client archive from signed allowlists only."""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import tempfile
import zipfile

FIXED_ZIP_TIME = (1980, 1, 1, 0, 0, 0)
FORBIDDEN_PATHS = {
    "timedelay.exe",
    "zftqat/tqat.exe",
    "zftqat/updata.dll",
    "revora.testchannel",
}
FORBIDDEN_PARTS = {
    ".revora-launcher", "screenshots", "logs", "crashdumps", "backups", "quarantine",
}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def signed_payload(path: Path, public_key: Path, expected_key_id: str) -> dict:
    envelope = json.loads(path.read_text(encoding="utf-8"))
    if envelope.get("keyId") != expected_key_id or envelope.get("algorithm") != "ECDSA-P256-SHA256":
        raise ValueError(f"Unexpected signing identity: {path}")
    payload = base64.b64decode(envelope["payload"], validate=True)
    signature = base64.b64decode(envelope["signature"], validate=True)
    with tempfile.TemporaryDirectory(prefix="revora-signature-") as temporary:
        payload_path = Path(temporary, "payload.json")
        signature_path = Path(temporary, "signature.der")
        payload_path.write_bytes(payload)
        signature_path.write_bytes(signature)
        result = subprocess.run(
            ["openssl", "dgst", "-sha256", "-verify", str(public_key), "-keyform", "DER",
             "-signature", str(signature_path), str(payload_path)],
            check=False, capture_output=True, text=True,
        )
        if result.returncode != 0 or "Verified OK" not in result.stdout:
            raise ValueError(f"Signature verification failed: {path}")
    return json.loads(payload)


def safe_path(value: str) -> PurePosixPath:
    if not value or "\\" in value or value.startswith("/"):
        raise ValueError(f"Unsafe archive path: {value!r}")
    path = PurePosixPath(value)
    if str(path) != value or any(part in ("", ".", "..") for part in path.parts):
        raise ValueError(f"Non-normalized archive path: {value!r}")
    if any(part.lower() in FORBIDDEN_PARTS for part in path.parts):
        raise ValueError(f"Forbidden archive category: {value!r}")
    if value.lower() in FORBIDDEN_PATHS:
        raise ValueError(f"Explicitly forbidden archive path: {value!r}")
    return path


def copy_verified(source: Path, target: Path, expected_size: int, expected_hash: str) -> None:
    if source.is_symlink() or not source.is_file():
        raise ValueError(f"Missing or unsafe source object: {source}")
    stat = source.stat()
    if stat.st_size != expected_size or sha256(source) != expected_hash:
        raise ValueError(f"Source object does not match its signed declaration: {source.name}")
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, target)
    os.utime(target, (315532800, 315532800))


def write_readme(path: Path) -> None:
    content = """Revora Full Client 5165 — Launcher 1.0.7

English
1. Extract every file into a new writable folder. Do not run from inside this ZIP and do not use Program Files.
2. Run Revora.Launcher.exe normally. Administrator rights are not required.
3. Close any running Revora/Conquer game before repair or update.
4. The launcher verifies the included signed client and downloads only newer, missing or corrupt managed files.
5. Enter your username and password inside the genuine game client.

Do not download loose Conquer.exe files from third-party sources.

العربية
1. فك ضغط جميع الملفات داخل مجلد جديد قابل للكتابة. لا تشغّل اللعبة من داخل ZIP ولا تستخدم Program Files.
2. شغّل Revora.Launcher.exe بشكل عادي؛ لا تحتاج إلى صلاحيات المسؤول.
3. أغلق أي لعبة Revora/Conquer تعمل قبل الإصلاح أو التحديث.
4. يتحقق اللانشر من العميل الموقّع المرفق وينزّل فقط الملفات الأحدث أو الناقصة أو التالفة.
5. أدخل اسم المستخدم وكلمة المرور داخل عميل اللعبة الأصلي.

لا تحمّل ملف Conquer.exe منفردًا من مصادر خارجية.
"""
    path.write_text(content, encoding="utf-8", newline="\n")
    os.utime(path, (315532800, 315532800))


def build_zip(staging: Path, destination: Path) -> None:
    if destination.exists():
        destination.unlink()
    files = sorted((entry for entry in staging.rglob("*") if entry.is_file()),
                   key=lambda entry: entry.relative_to(staging).as_posix())
    with zipfile.ZipFile(destination, "w", compression=zipfile.ZIP_DEFLATED,
                         compresslevel=9, allowZip64=True) as archive:
        for source in files:
            relative = source.relative_to(staging).as_posix()
            info = zipfile.ZipInfo(relative, FIXED_ZIP_TIME)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            info.create_system = 3
            with source.open("rb") as input_file, archive.open(info, "w", force_zip64=True) as output:
                shutil.copyfileobj(input_file, output, 1024 * 1024)


def validate_zip(path: Path, expected: dict[str, tuple[int, str]]) -> None:
    with zipfile.ZipFile(path, "r") as archive:
        names = archive.namelist()
        if len(names) != len(expected) or len({name.casefold() for name in names}) != len(names):
            raise ValueError("Archive entries are missing, duplicated or case-colliding.")
        if names != sorted(names):
            raise ValueError("Archive ordering is not deterministic.")
        for info in archive.infolist():
            safe_path(info.filename)
            if info.is_dir() or info.filename not in expected:
                raise ValueError(f"Unexpected archive entry: {info.filename}")
            expected_size, expected_hash = expected[info.filename]
            if info.file_size != expected_size:
                raise ValueError(f"Archive size mismatch: {info.filename}")
            digest = hashlib.sha256()
            with archive.open(info) as source:
                for block in iter(lambda: source.read(1024 * 1024), b""):
                    digest.update(block)
            if digest.hexdigest() != expected_hash:
                raise ValueError(f"Archive hash mismatch: {info.filename}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--client-manifest", type=Path, required=True)
    parser.add_argument("--client-object-root", type=Path, required=True)
    parser.add_argument("--launcher-manifest", type=Path, required=True)
    parser.add_argument("--launcher-object-root", type=Path, required=True)
    parser.add_argument("--public-key", type=Path, required=True)
    parser.add_argument("--key-id", required=True)
    parser.add_argument("--staging", type=Path, required=True)
    parser.add_argument("--output-a", type=Path, required=True)
    parser.add_argument("--output-b", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    arguments = parser.parse_args()

    client = signed_payload(arguments.client_manifest, arguments.public_key, arguments.key_id)
    launcher = signed_payload(arguments.launcher_manifest, arguments.public_key, arguments.key_id)
    if client.get("channel") != "stable" or launcher.get("channel") != "stable":
        raise ValueError("Both signed manifests must identify the stable channel.")
    if launcher.get("version") != "1.0.7":
        raise ValueError("The launcher manifest is not version 1.0.7.")

    staging = arguments.staging.resolve()
    if staging.exists():
        shutil.rmtree(staging)
    staging.mkdir(parents=True, mode=0o700)
    expected: dict[str, tuple[int, str]] = {}
    casefolded: set[str] = set()

    def reserve(relative: str, size: int, digest: str) -> Path:
        normalized = safe_path(relative)
        folded = relative.casefold()
        if folded in casefolded:
            raise ValueError(f"Duplicate or case-colliding path: {relative}")
        casefolded.add(folded)
        expected[relative] = (size, digest)
        return staging.joinpath(*normalized.parts)

    for file in client["files"]:
        if file.get("compression") != "none":
            raise ValueError(f"Full-client assembly does not accept compressed source objects: {file['path']}")
        target = reserve(file["path"], int(file["size"]), file["sha256"])
        source = arguments.client_object_root / file["objectPath"]
        copy_verified(source, target, int(file["size"]), file["sha256"])

    for file in launcher["files"]:
        relative = file["path"]
        target = reserve(relative, int(file["size"]), file["sha256"])
        source = arguments.launcher_object_root / "objects" / "sha256" / file["sha256"][:2] / file["sha256"]
        copy_verified(source, target, int(file["size"]), file["sha256"])

    readme = staging / "README-REVORA.txt"
    write_readme(readme)
    expected["README-REVORA.txt"] = (readme.stat().st_size, sha256(readme))
    if "readme-revora.txt" in casefolded:
        raise ValueError("The bilingual README collides with a signed entry.")
    casefolded.add("readme-revora.txt")

    build_zip(staging, arguments.output_a)
    build_zip(staging, arguments.output_b)
    first_hash, second_hash = sha256(arguments.output_a), sha256(arguments.output_b)
    if first_hash != second_hash:
        raise ValueError("Independent full-client archive builds were not byte-identical.")
    validate_zip(arguments.output_a, expected)
    validate_zip(arguments.output_b, expected)

    evidence = {
        "schemaVersion": 1,
        "clientReleaseId": client["releaseId"],
        "launcherReleaseId": launcher["releaseId"],
        "clientBuild": client["clientBuild"],
        "launcherVersion": launcher["version"],
        "managedClientFiles": len(client["files"]),
        "managedClientBytes": sum(int(file["size"]) for file in client["files"]),
        "launcherFiles": len(launcher["files"]),
        "archiveEntries": len(expected),
        "extractedBytes": sum(size for size, _ in expected.values()),
        "archiveBytes": arguments.output_a.stat().st_size,
        "sha256": first_hash,
        "deterministicComparisonSha256": second_hash,
        "clientManifestSha256": sha256(arguments.client_manifest),
        "launcherManifestSha256": sha256(arguments.launcher_manifest),
    }
    arguments.evidence.write_text(json.dumps(evidence, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(evidence, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
