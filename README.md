# Revora

Revora's game server, website, and Windows launcher in one repository.

**[Live HTML/JavaScript website](https://sheekovic.github.io/Revora-OpenSource/)**

## A note from Sheekovic

I have put **hundreds of hours of modification, development, testing, and hard
work** into Revora. It has also cost me **hundreds of dollars out of my own
pocket**. In my view, Revora is now the best Conquer source available in the
scene, and I am choosing to release it **free and open source** for everyone.

If you use or appreciate this work, please at least **give this repository a
star**. You can also follow me and, if you would like to support the time and
money behind the project, make a voluntary PayPal donation.

**[Star Revora](https://github.com/Sheekovic/Revora-OpenSource)** ·
**[Follow @Sheekovic](https://github.com/Sheekovic)** ·
**[Donate via PayPal](https://paypal.me/AhmeedWahballah)**

| Component | Location | Technology | Start here |
| --- | --- | --- | --- |
| PlayRevora Launcher | [`launcher/`](launcher/) | .NET 8, WPF, Windows | [Launcher guide](launcher/README.md) |
| Revora website | [`website/site/`](website/site/) | HTML, CSS, vanilla JavaScript | [Website guide](docs/WEBSITE.md) |
| Revora 6711 3D server | [`server/`](server/) | C#, .NET Framework 4.8, Windows | [Server guide](server/README.md) |
| Optional legacy web backend | [`website/src/`](website/src/) | ASP.NET Core, .NET 10, PostgreSQL | [Connection guide](docs/CONNECT_COMPONENTS.md) |
| Client / Server.dat editor | [`tools/ClientEndpoint/`](tools/ClientEndpoint/) | Windows GUI and CLI, C# | [Download and guide](docs/CLIENT_ENDPOINT_TOOL.md) |

The primary website has **no ASP.NET dependency, npm packages, bundler, or build
step**. Open `website/site/index.html` directly or use the live GitHub Pages
preview. The earlier ASP.NET/shared projects are preserved as an optional
backend/reference; they are not required to run the frontend.

## Build and test

For the guided local setup, start with **[START-REVORA.cmd](START-REVORA.cmd)**
and the [one-click setup guide](docs/QUICKSTART.md). Its default opens the static
website. Add `-WithDatabase` to provision isolated PostgreSQL with local secrets,
separate roles/databases, and checksum-tracked migrations.
The full game server additionally needs the missing assemblies, a lawfully
supplied runtime/database, and a matching 6711 client profile.

Want an AI agent to finish wiring your own environment? Give it
[this task brief](docs/AI_AGENT_SETUP.md).

Run commands from this repository's root in PowerShell:

```powershell
# Windows; .NET 8 SDK or a newer compatible SDK
./scripts/Build.ps1 -Component Launcher
./scripts/Test.ps1 -Component Launcher

# The public website: Node standard-library tests, no package installation
node --test tests/site-server.test.mjs

# Optional preserved ASP.NET backend only; .NET 10 SDK
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

CI checks the public source with Gitleaks, validates the repository/static site,
exercises real PostgreSQL provisioning and rerun behavior, builds/tests the
launcher and optional legacy web backend, builds the administration app, and
tests/packages the client endpoint and signed Server.dat editor.
GitHub Pages deploys only `website/site/`. CI does not claim a complete
game-server build or playable-client qualification.

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
