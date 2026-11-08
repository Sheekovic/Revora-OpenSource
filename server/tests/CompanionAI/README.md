# Companion AI validation

Run from `Source/Revora-6711` with Windows PowerShell. This legacy project
targets .NET Framework 4.8 / x86. Build into the existing isolated validation
directory; do not overwrite the running `bin/Debug/Revora.exe`.

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' Revora.csproj /t:Build /p:Configuration=Debug /p:Platform=x86 /p:ImportDirectoryBuildProps=false /p:TreatWarningsAsErrors=false /p:ImportDirectoryBuildTargets=false /p:Nullable=disable /p:OutputPath=obj/chi-refill-validation/bin/ /p:IntermediateOutputPath=obj/chi-refill-validation/obj/ /nologo /verbosity:quiet /clp:ErrorsOnly
& tests/CompanionAI/Verify-Companions.ps1
```

The runner hashes the actual isolated server assembly and compiles an x86
fixture beside it. Navigation tests compile the production navigation source.
Integration tests invoke the actual whisper, decision, movement, combat and
cleanup methods using in-memory maps, ordinary non-GM characters, synthetic
companions and disconnected sockets. Only the three relevant skills are parsed
from the local spell table. No server entry point, configuration loader, pet
timers, database, or listening socket is started. A legacy static initializer
prints that optional `GeoIP.dat` is absent in the isolated directory; these
fixtures do not use geolocation.

`Verify-Guards.ps1` and `Verify-Retaliation.ps1` additionally load Backfire 12680
from the same installed catalog. Their ordinary defenders have fixture-seeded
skills and fists, so these establish combat behavior, not skill acquisition.
The runners copy the ignored runtime assembly-binding configuration beside their
isolated test executables; they still never call the configuration loader or
connect to the database. Guard/adventurer counter tests check one-use damage,
stamina, death cleanup and rejection of unregistered/replaced fake actors.
The separate disposable-database Warrior suite tests real instructor learning,
persisted lethal guard transfers and all three summon ownership representations.

Coverage includes walls and diagonal corners, search limits and retry backoff,
clear-ground jumps, blocked jumps, close-range walking and jump distance limits,
accepted owner attacks and expiration, ownership races, passive/hold/retreat,
melee and magic damage, Cure costs and cooldown, attack restrictions, synthetic
item persistence exclusion, delayed separation recovery, invalid landing tiles,
map changes, event exclusion, death scheduling and logout.

These are deterministic scenarios with assertions on effects and invariants;
combat calculations still use the server's random damage rolls. Rendering,
network delivery, full equipment/stat materialization, autonomous revival, and
load with real populations require a separate genuine Patch-6711 client test.
Companion state is intentionally session-only; no database migration is needed.

Before activation, use an ordinary character to hire, assist a monster attack,
observe Cure/Thunder/Tornado and health updates, hold behind terrain, cross a
normal map portal, dismiss and log out. Also test companion death/reappearance
and nearby-player visibility. Activation requires a backed-up, graceful server
restart approved by the owner. Deploy only the rebuilt executable and symbols;
preserve runtime settings, dependencies and player data.
