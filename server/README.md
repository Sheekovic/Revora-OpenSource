# Revora6711-3D

Live Conquer Online **patch 6711 DX9** game server used by Revora.

This is Revora's maintained .NET Framework 4.8 game-server tree, separate from the modern `Revora.Host` projects under `website/`. The required client is genuine 6711 DX9 `Conquer.exe blacknull`.

This component is part of the [Revora monorepo](../README.md). See [build prerequisites](../docs/SERVER_BUILD.md) for assemblies that must be supplied before building a fresh checkout. Revora-authored code is licensed under the root [MIT License](../LICENSE); third-party components retain their own terms.

Player-facing text in this tree is English. Ordinary players use the same services as staff; GM commands stay GM-only.

## What is in this repository

| Path | Role |
|---|---|
| `Game/Features/` | City guards, living-world adventurers, RevoraMaster, expeditions |
| `Npcs.cs` | NPC dialog, including Sereth and Prizewell |
| `Client/GameState.cs` | Sessions, rename, disconnect, heartbeat save |
| `docs/` | What we shipped and how to run it |
| `Featuers/` | Earlier design notes (spelling is historical) |

Read **[docs/FEATURES.md](docs/FEATURES.md)** for the live feature list and **[docs/WORKLOG.md](docs/WORKLOG.md)** for the work that produced it.

## Build

Windows, Visual Studio 2022 MSBuild, Debug|x86:

```text
MSBuild Revora.csproj /p:Configuration=Debug /p:Platform=x86 /p:ImportDirectoryBuildProps=false /p:TreatWarningsAsErrors=false /p:ImportDirectoryBuildTargets=false /p:Nullable=disable
```

Output is `bin\Debug\Revora.exe`. Run it with cwd `bin\Debug`. Stop any previous `Revora.exe` before rebuilding.

## Local config (not in Git)

Copy the examples and fill in your own values:

- `app.config.example` → `app.config`
- `Configuracion.ini.example` → `bin\Debug\Configuracion.ini`

Never commit passwords, SQL dumps, or client archives.

Runtime map/item tables that the process loads from `bin\Debug\database\` stay on the operator machine. They are not part of this source repository.

## License boundary

Do not copy untrusted legacy trees into this repo. Keep research checkouts out of Git.
