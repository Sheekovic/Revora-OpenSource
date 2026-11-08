# Game-server build prerequisites

The game server in `server/Revora.csproj` targets .NET Framework 4.8. Install
Visual Studio MSBuild and the .NET Framework 4.8 developer/targeting pack on
Windows. The separate `server/Admin/` project uses .NET 10.

## Dependencies missing from the source checkout

The existing project references the following operator-supplied assemblies.
Paths below are relative to `server/` and reflect the imported project file.

| Assembly | Existing expected location |
| --- | --- |
| BouncyCastle.Crypto | `bin/Debug/BouncyCastle.Crypto.dll` |
| cityLookUp | `bin/Debug/cityLookUp.dll` |
| CO2_CORE_DLL | `bin/Debug/CO2_CORE_DLL.dll` |
| CountryLookupProj | `bin/Debug/CountryLookupProj.dll` |
| HandEvaluator | `bin/Debug/HandEvaluator.dll` |
| KinSocket | `bin/Debug/KinSocket.dll` |
| NpcDialogs | `bin/Debug/NpcDialogs.dll` |
| protobuf-net | `bin/Debug/database/protobuf-net.dll` |
| Sockets | `bin/Debug/Sockets.dll` |
| SubSonic | `bin/Debug/SubSonic.dll` |
| SubSonic.Migrations | `bin/Debug/SubSonic.Migrations.dll` |
| Microsoft.Activities.Build | Existing MSBuild/assembly resolution |
| Microsoft.SqlServer.ConnectionInfo / Smo | Historical external SubCommander paths |
| msvcrt managed reference | Historical `D:/Sources/RAR/Royal/bin/Debug/msvcrt.dll` |

These historical external paths need explicit local resolution or a future
source/dependency migration. Do not download arbitrary replacement DLLs or
commit private legacy binaries. Establish provenance, compatible API versions,
and redistribution rights before replacing references.

`server/lib/` contains the existing Npgsql and Microsoft support libraries;
their notices are retained at the monorepo root. Their presence does not
resolve the missing assemblies above.

## Build

```powershell
Copy-Item server/app.config.example server/app.config
# Configure the ignored local app.config, and supply reviewed dependencies.
./scripts/Build.ps1 -Component Server -Configuration Debug
```

Output is `server/bin/Debug/Revora.exe` for Debug. Runtime configuration and
`database/` tables belong beside the executable and are operator-supplied.
Use `REVORA_DATABASE_PASSWORD` rather than committing a database password.

The migration build attempted on 2026-11-08 reached compilation and failed
because external assemblies/types were unavailable (including protobuf-net
attributes). This is a pre-existing source-checkout limitation, not a verified
working standalone server build. CI separately builds the .NET 10 Admin
application; it does not qualify the full game-server runtime.
