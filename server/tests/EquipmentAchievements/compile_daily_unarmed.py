"""Compile the bounded daily-boss unarmed fixture without importing owner WIP."""
import os
from pathlib import Path
import re
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
output = root / "obj/mrms2017-validation/bin"
stage = root / "obj/daily-unarmed-test-sources"
runner = subprocess.check_output(["git", "show", "HEAD:tests/PotencyHunt/Verify-PotencyHunt.ps1"], cwd=root).decode("utf-8-sig")
line = next(line for line in runner.splitlines() if "/main:PotencyHuntTests" in line)
sources = []
for base, relative in re.findall(r"\(Join-Path \$(project|PSScriptRoot) '([^']+)'\)", line):
    relative = relative.replace(chr(92), "/")
    path = ("tests/PotencyHunt/" if base == "PSScriptRoot" else "") + relative
    if path in ("tests/PotencyHunt/DailyUnarmedTests.cs", "tests/PotencyHunt/PowerTests.cs", "tests/PotencyHunt/SnakeTests.cs", "tests/PotencyHunt/LabyrinthTests.cs", "tests/PotencyHunt/DemonTests.cs", "tests/PotencyHunt/GuildBeastTests.cs", "tests/PotencyHunt/ConquerPointBagTests.cs", "tests/PotencyHunt/ExpeditionTests.cs"):
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
earning_source = str(root / "tests/PotencyHunt/DailyUnarmedTests.cs")
if earning_source not in sources:
    sources.append(earning_source)
power = "--power" in sys.argv[1:]
snake = "--snake" in sys.argv[1:]
lab = "--lab" in sys.argv[1:]
demon = "--demon" in sys.argv[1:]
summer = "--summer" in sys.argv[1:]
beast = "--beast" in sys.argv[1:]
book = "--book" in sys.argv[1:]
extraction = "--extraction" in sys.argv[1:]
sources.append(str(root / "tests/PotencyHunt/FiveRebirthArtsTests.cs"))
name = "RebirthArts" if "--rebirth-arts" in sys.argv[1:] else "NobilityAccess" if "--nobility" in sys.argv[1:] else "ExtractionUnarmed" if extraction else "BookUnarmed" if book else "BeastUnarmed" if beast else "SummerUnarmed" if summer else "DemonUnarmed" if demon else "LabUnarmed" if lab else "SnakeUnarmed" if snake else "PowerUnarmed" if power else "DailyUnarmed"
csc = Path(os.environ["WINDIR"]) / "Microsoft.NET/Framework64/v4.0.30319/csc.exe"
command = [str(csc), "/nologo", "/target:exe", "/platform:x64", "/unsafe", "/warnaserror",
           "/main:" + (name + "Runner"), "/out:" + str(output / (name + ".Tests.exe"))]
command += ["/reference:" + str(output / name) for name in ("Revora.exe", "Npgsql.dll", "protobuf-net.dll")]
command += ["/reference:" + name for name in ("System.Drawing.dll", "System.Data.dll", "System.Core.dll", "System.Numerics.dll")]
result = subprocess.run(command + sources, cwd=root, timeout=60)
sys.exit(result.returncode)
