# Revora

Revora's game server, website, and Windows launcher in one repository.

| Component | Location | Technology | Start here |
| --- | --- | --- | --- |
| PlayRevora Launcher | [`launcher/`](launcher/) | .NET 8, WPF, Windows | [Launcher guide](launcher/README.md) |
| Revora website | [`website/`](website/) | ASP.NET Core, .NET 10, PostgreSQL | [Website guide](docs/WEBSITE.md) |
| Revora 6711 3D server | [`server/`](server/) | C#, .NET Framework 4.8, Windows | [Server guide](server/README.md) |

Each component keeps its existing relative paths, project files, tests, and
documentation. The website tree includes the shared application, persistence,
and earlier server projects that its existing solution depends on.

## Build and test

Run commands from this repository's root in PowerShell:

```powershell
# Windows; .NET 8 SDK or a newer compatible SDK
./scripts/Build.ps1 -Component Launcher
./scripts/Test.ps1 -Component Launcher

# .NET 10 SDK; PostgreSQL is needed for database-backed runtime features
./scripts/Build.ps1 -Component Website
./scripts/Test.ps1 -Component Website

# Optional .NET 10 administration application shipped with the 3D server
./scripts/Build.ps1 -Component Admin

# Windows; Visual Studio MSBuild and the .NET Framework 4.8 developer pack
./scripts/Build.ps1 -Component Server
```

The game server also needs the external assemblies listed in
[server build prerequisites](docs/SERVER_BUILD.md). They are not all present in
the source repositories, so a fresh checkout cannot yet build the game server
without those operator-supplied dependencies. Game tables, accounts, and client
files are not distributed here.

CI validates the repository, builds and tests the launcher and website, and
builds the administration application. It does not run a game server or claim a
complete game-server build.

## Compatibility and configuration

The game server targets genuine patch **6711 DX9**. The imported launcher's
current signed-profile configuration and several historical website tools
target **7630**. Consolidating the repositories does not make those profiles
interchangeable. A reviewed 6711 profile and an end-to-end client test are
required before claiming launcher/server compatibility.

Use each component's example configuration and keep local secrets outside Git.
Copy `server/app.config.example` to `server/app.config` for a local server
build, and configure runtime settings as described in the server guide.
Production services and existing download endpoints are operated separately
from this source repository.

## Contributing

Read [CONTRIBUTING.md](CONTRIBUTING.md), [SECURITY.md](SECURITY.md), and the
instructions inside the component you change. Use issues and pull requests in
this repository for future work.

## License and origin

Revora-authored material is available under the [MIT License](LICENSE).
Third-party components retain their own terms; see
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

**This product includes software developed by CptSky.**

See [MIGRATION.md](docs/MIGRATION.md) for the exact source commits and the
clean-snapshot import policy. Proprietary game clients, assets, production
data, private signing keys, and unsupported legacy binaries are not included.
