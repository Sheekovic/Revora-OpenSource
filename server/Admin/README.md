# Live game administration

This replaces the deployed September 3 WKConquer admin adapter. The old panel
queried MySQL identities and flat character INIs, while the live game and website
use PostgreSQL `127.0.0.1:25432/revora6711_3d`.

The service reads connection credentials in memory from the live game's ignored
`bin/Debug/Configuracion.ini`. Credentials are never copied to this project.
Database identity is pinned, transactions are read-only and repeatable-read,
and only explicit account/character display fields are selected. Startup checks
the database and schema before READY. No account passwords or recovery fields
are selected. Account creation dates and launcher session counts are omitted:
they are not represented in the current live schema.

Character writes are temporarily disabled, including direct POST requests.
Checking `online=0` alone does not prevent a concurrent login loading stale data.
A coordinated server mutation mechanism is required before enabling edits.

Existing `C:\Revora\Admin\data\administrator.json` and `keys` are preserved.
The Data Protection application name `Revora.WKAdmin`, TOTP purpose, security
stamp, Argon2 verification and secure cookies retain compatibility. The service
requires an existing administrator; it cannot reset or bootstrap production login.
Test-only data provisioning uses the security store with synthetic credentials.

## Build and validation

Run from the repository root, with .NET 10:

```powershell
dotnet publish Admin/Revora.Admin.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:ImportDirectoryBuildProps=false -p:ImportDirectoryBuildTargets=false -o obj/admin-database-diagnostic/publish
dotnet run --project Admin.Tests/Revora.Admin.Tests.csproj -p:ImportDirectoryBuildProps=false -p:ImportDirectoryBuildTargets=false -- $PWD
```

Tests use a separate loopback 5082 host, temporary synthetic administrator and
isolated Data Protection keys. They exercise real password/TOTP/CSRF login and
an authenticated read-only live database dashboard, not live account mutations.
`--verify-database` performs only the startup read and prints aggregate counts.

Defaults: loopback port 5081, existing admin data directory and live game INI.
`REVORA_ADMIN_URL`, `REVORA_ADMIN_DATA`, `REVORA_ADMIN_GAME_INI` support isolated
validation; listeners must remain loopback HTTP and the database must match the
pinned live target. nginx remains the public TLS endpoint.

## Provenance and rollback

The prior administrator source was absent. Its Revora-owned authentication and
visual layout were recovered by static decompilation from the deployed executable
SHA-256 `F80FE937419D5461CC33D45EBD3D4F8251CAED07221B1D4FE1DBB3EDDADAE7C2`.
The old MySQL/INI adapters were replaced with the explicit PostgreSQL reader.
Recovered binaries and diagnostic artifacts remain ignored and are not committed.

Deployment changes only the `Revora Admin` scheduled-task application. Preserve
the current executable and task XML in a dated directory under
`C:\Revora\Admin\backups`; leave `data` and keys untouched. Stop the task,
replace the application, restart and verify login/anonymous protection. Rollback:
stop the task, restore that saved executable to `app\Revora.Admin.exe`, restart.
The game process, website and nginx do not require a restart.

Deployed September 20, 2026. Backup: `C:\Revora\Admin\backups\database-fix-20260920-085314`.
Published executable SHA-256:
`B34186D3C1D4143C27D557BAB2408DA3594AA1B639AF09544D2B1FC006910B55`.
All 12 integration assertions passed. The deployed database check returned four
accounts and three characters. Public HTTPS `/login` returned 200 and anonymous
`/` redirected to login (302). The existing game PID 428 was not restarted.
The owner's actual password/TOTP login was not attempted; compatibility was
verified using a separate synthetic administrator and the preserved formats.
