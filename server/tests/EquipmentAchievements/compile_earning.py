"""Compile the bounded equipment earning fixture without importing owner WIP."""
import os
from pathlib import Path
import re
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
output = root / "obj/mrms2017-validation/bin"
stage = root / "obj/equipment-earning-test-sources"
runner = subprocess.check_output(["git", "show", "HEAD:tests/PotencyHunt/Verify-PotencyHunt.ps1"], cwd=root).decode("utf-8-sig")
line = next(line for line in runner.splitlines() if "/main:PotencyHuntTests" in line)
sources = []
for base, relative in re.findall(r"\(Join-Path \$(project|PSScriptRoot) '([^']+)'\)", line):
    relative = relative.replace(chr(92), "/")
    path = ("tests/PotencyHunt/" if base == "PSScriptRoot" else "") + relative
    if path in ("tests/PotencyHunt/RuneAcquisitionTests.cs", "tests/PotencyHunt/EquipmentSetEarningTests.cs"):
        # This batch's shared entry helper; all other shared fixtures use HEAD.
        sources.append(str(root / path))
    else:
        content = subprocess.check_output(["git", "show", "HEAD:" + path], cwd=root)
        destination = stage / path
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(content)
        sources.append(str(destination))
if len(sources) < 30:
    raise SystemExit("Expected shared fixture source list was not found")
earning_source = str(root / "tests/PotencyHunt/EquipmentSetEarningTests.cs")
if earning_source not in sources:
    sources.append(earning_source)
csc = Path(os.environ["WINDIR"]) / "Microsoft.NET/Framework64/v4.0.30319/csc.exe"
command = [str(csc), "/nologo", "/target:exe", "/platform:x64", "/unsafe", "/warnaserror",
           "/main:EquipmentSetEarningRunner", "/out:" + str(output / "EquipmentSetEarning.Tests.exe")]
command += ["/reference:" + str(output / name) for name in ("Revora.exe", "Npgsql.dll", "protobuf-net.dll")]
command += ["/reference:" + name for name in ("System.Drawing.dll", "System.Data.dll", "System.Core.dll", "System.Numerics.dll")]
result = subprocess.run(command + sources, cwd=root, timeout=60)
sys.exit(result.returncode)
