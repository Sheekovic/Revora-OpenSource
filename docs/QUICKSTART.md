# Start Revora

## Website in one click

Download/extract the source, then double-click **`START-REVORA.cmd`** on Windows.
The default starts the plain HTML/CSS/JavaScript website on
<http://127.0.0.1:5080> and opens it in your browser.

Node.js 22 or newer supplies the local HTTP preview, with no npm installation.
If Node is missing, the default Windows launcher opens `website/site/index.html`
directly instead. The website itself needs no .NET SDK or database.

The hosted preview is <https://sheekovic.github.io/Revora-OpenSource/>.

## Prepare PostgreSQL too

Install Node.js and Docker Desktop, start Docker's Linux-container engine, then
run from the extracted repository:

```powershell
# Inspect prerequisites without changing services or creating local credentials.
./START-REVORA.cmd -Mode Check -WithDatabase

# Provision the isolated development databases and start the static website.
./START-REVORA.cmd -WithDatabase

# Prepare database/schema only, without starting the website/game processes.
./START-REVORA.cmd -Mode Prepare -WithDatabase

# Stop only processes/container recorded by this setup. Preserve all data.
./START-REVORA.cmd -Mode Stop
```

PowerShell-only and Linux developer equivalent:

```powershell
pwsh ./scripts/Start-Revora.ps1 -WithDatabase -NoBrowser
```

The script creates a workspace-labelled official `postgres:17` container,
publishes PostgreSQL only on `127.0.0.1:25432`, and stores database files in an
owned named volume. It creates separate `revora_web_local` and
`revora6711_3d` databases, separate web/game logins and restricted web-role
permissions. The 47 existing website migrations are applied transactionally
with checksums; already-applied matching migrations are skipped.

Passwords are random, generated locally, and retained under ignored,
permission-restricted `.local/secrets/`. The PostgreSQL bootstrap password is
mounted read-only and supplied through `POSTGRES_PASSWORD_FILE`; it is not a
command-line argument or a literal container environment value.

Re-running preserves credentials and existing database contents. Port conflicts,
changed migrations, unowned containers/volumes and lost credentials produce an
error. The script does not reset an existing database, stop another service,
open firewall ports or install system software.

Use `.local/setup-report.json` to see exactly what became ready. No credentials
are included in that report. Protect `.local/` and Docker-volume backups: both
are private operator data and must never be uploaded to GitHub.

## The game server needs additional inputs

A fresh public source checkout still lacks some external assemblies and runtime
tables. Read [SERVER_BUILD.md](SERVER_BUILD.md). The setup does not invent a
compatible game schema or redistribute proprietary game data.

Once you have a **reviewed, lawfully possessed Windows server runtime** containing
`Revora.exe`, its approved dependencies and `database/` tables, you can stage it
without changing the original directory:

```powershell
./START-REVORA.cmd -WithDatabase `
  -GameRuntimeDirectory 'D:\MyReviewedRevoraRuntime' `
  -GameExecutableSha256 'YOUR_INDEPENDENTLY_REVIEWED_64_HEX_BUILD_HASH' `
  -GameSchemaSql 'D:\MyPrivateSetup\revora-game-schema.sql'
```

Replace the hash placeholder with an independently checked SHA-256. Verify
provenance and scan third-party binaries before using a runtime; a matching
hash identifies a build and does not establish that it is safe or compatible.

Supply a reviewed **schema-only** game export, without real account/player
records, password material, production data, SQL shell commands or unrelated
database/role changes. Import is accepted only into an empty managed game
database. Repeat later starts without `-GameSchemaSql`; an existing schema will
not be overwritten. Static-data loading must follow your runtime's documented,
lawful procedure.

The script copies the runtime into ignored `.local/game/runtime/`, verifies the
executable hash again, and writes a loopback `Configuracion.ini` with an empty
password field. The game password is passed only to the new game's environment.
It checks for the `accounts`, `entities` and `configuration` tables, then waits
for Account 9958 and Game 9959 listeners. Additional schema/data requirements
remain runtime-dependent.

**Listener readiness is not successful gameplay.** A genuine 6711 DX9 login,
character creation/entry, movement, save and relogin must be verified separately.
The imported launcher's 7630 profile must not be used as proof of 6711 support.

## Connect the pieces

Use [CONNECT_COMPONENTS.md](CONNECT_COMPONENTS.md) for the host/domain/database
map and [AI_AGENT_SETUP.md](AI_AGENT_SETUP.md) for a copyable agent task.
The optional legacy Admin application is not auto-started or given a default
administrator account; preserve its existing authentication/security boundary.

Image behavior and password-file configuration follow the
[official PostgreSQL image documentation](https://hub.docker.com/_/postgres).
