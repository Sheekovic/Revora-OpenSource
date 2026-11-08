# AMP Integration

Status: `RevoraDevelopment01` exists and is stopped. The durable Host works directly as `amp`, but AMP lifecycle validation is blocked because its rootless wrapper exposed the instance management UI and SFTP on `0.0.0.0:8081` and `0.0.0.0:2224` despite loopback metadata. Both starts were stopped and no game port was opened. Authenticated ADS reconfiguration or a reviewed stopped-ADS native conversion is required before retrying.

The offline artifact audit, permission model, exact guarded installation plan, and proposed Development settings are in `AMP_INSTALLATION_READINESS.md`.

## Detected installation facts

Read-only inspection on 2026-08-28 found:

- OS: Debian GNU/Linux 13.6 (`trixie`), x86-64.
- AMP Instance Manager and ADS: `2.8.0.4`, Mainline/Release, built 2026-07-24.
- Controller instance: `ADS01`, the only registered instance, running non-containerized as user/group `amp`.
- ADS data: `/home/amp/.ampdata/instances/ADS01` with directory mode `0700`.
- ADS management listener: `127.0.0.1:8080`, reverse-proxied by nginx on existing HTTP/HTTPS listeners.
- Other ADS port declarations: TCP 2223 for AMP SFTP and UDP 12820 for ADS metrics. No changes were made.
- Systemd: `ampinstmgr.service` runs as `amp`; `ampfirewall.service` runs the AMP firewall updater as root; `amptasks.service` runs as `amp`.
- GenericModule plugin is installed under ADS. Official templates are cached below `/home/amp/.ampdata/instances/ADS01/Plugins/ADSModule/DeploymentTemplates/CubeCoders-AMPTemplates-main`.
- Current firewall INPUT policy is accept, with AMP-commented rules. This is an inherited risk; no Revora rules were created.
- The `amp` account is already in its primary group plus `tty` and `docker`. Revora must not use or expand those inherited privileges.

No AMP configuration, service, firewall, user, template, or instance was changed during inspection.

## Supported local template method

AMP's official Generic Module documentation permits a local-only repository directory directly under the ADS `DeploymentTemplates` directory, with a name beginning `LOCAL` and ending `-main`. For this host, a future narrowly scoped location would be:

`/home/amp/.ampdata/instances/ADS01/Plugins/ADSModule/DeploymentTemplates/LOCALRevora-main`

Installation is deferred. Before it occurs, validate all files and artifact behavior, record owner/mode/SHA-256 of the deployment-template directory metadata, create a narrow timestamped backup of any same-name directory if it exists, and confirm whether ADS notices a new directory without restart. A controller restart or Hostinger-managed file change requires approval.

## Template files

- `deploy/amp/revora.kvp`: Generic Module metadata, executable/lifecycle, console regex, ports, disabled updates, and retry policy.
- `deploy/amp/revoraconfig.json`: editable non-secret settings manifest.
- `deploy/amp/revorametaconfig.json`: AutoMap from settings into `config/revora.json`.
- `deploy/amp/revoraports.json`: separate account/game TCP reservations.
- `deploy/amp/revoraupdates.json`: empty; application updates disabled.
- `deploy/amp/config/revora.json`: safe artifact default configuration.
- `deploy/amp/config/revora.secrets.example.json`: documents the protected secret indirection without containing a secret.

## Exposed settings

The panel exposes instance/realm name, Development or Staging profile, explicit runtime-adapter selection (Disabled by default), two bind addresses and TCP ports, maximum players, shutdown timeout, save interval, non-debug log level, retention target, registration toggle (off), maintenance mode (on), and approved migration toggle (off). Production is not an option in this template revision.

## Secrets

Database credentials are absent from AMP settings, template files, arguments, and logs. Each future instance has `config/revora.secrets.json`, mode `0600`, owned by its AMP runtime user. The file will identify or hold the minimum required database credential using the final persistence adapter's reviewed format. It must be provisioned out-of-band and must not enter Git or routine archive backups.

## Ports and bindings

Template development defaults are TCP 9958 (Account) and 9959 (Game), both `127.0.0.1`. They are provisional Revora development reservations—not historical Conquer defaults. No health, metrics, admin, database, or debug port is exposed. AMP reservation does not imply public firewall authorization.

Staging and future production require distinct ports and configuration. Any non-loopback bind and firewall action require explicit approval after protocol hardening.

## Start, readiness, and stop

AMP starts `app/Revora.Host --config config/revora.json` directly. No shell, framework host, Wine, Proton, restore, or build occurs. Hostinger requires AMP-managed containerized instances, so the template permits AMP's existing rootless container mode. Revora does not invoke container tooling itself. Before the first start, operators must verify the host process is owned by `amp`, the container is unprivileged, host networking is disabled, mounts are instance-scoped, and no additional Linux capabilities are granted.

The host validates configuration/directories/secret permissions, establishes persistence, starts Account, starts Game, and verifies each health state. Only then does it print exactly `[Revora] READY`, matched by AMP's anchored JavaScript-regex-compatible expression.

AMP sends the literal stdin command `stop`. SIGTERM and SIGINT also request the same graceful path. Account and Game stop accepting, persistence is flushed within the configured timeout, resources close, `[Revora] STOPPED` is printed, and success exits zero. AMP's kill action remains emergency-only. Automatic retry is zero in the template to prevent crash loops.

The default `RuntimeAdapter` is `Disabled` and remains fail-closed. An operator must explicitly select `InMemory`, and validation permits that adapter only for Development/Test with both listeners on loopback. That harness can reach true listener readiness but implements no authentication, packet parsing, cryptography, gameplay, or durable storage.

## Console and health

Allowed commands: `help`, `status`, `health`, `version`, `players`, `save`, `stop`. Parsing is exact and does not invoke a shell. Health reports process, persistence, Account listener, Game listener, and lifecycle state as starting, ready, degraded, draining, stopped, or failed. Persistence queue detail will be added with the adapter.

## Updates

Automatic update installation is disabled (`[]`). Future releases must be trusted, versioned self-contained `linux-x64` archives with published SHA-256, staged extraction, preserved config/data, graceful stop, atomic replacement, readiness verification, and rollback. AMP must never update production using Git or source compilation. Enabling updates and publishing a release require approval.

## Backups and scheduled tasks

See `BACKUP_AND_RESTORE.md`. Recommended schedules are documented in `OPERATIONS.md`; none are installed because no instance exists. AMP's application `save` is suitable before a consistent DB dump, but AMP cannot make a transactionally consistent external database backup by copying files.

## Multiple environments

Development, Staging, and future Production are separate AMP instances with distinct work directories, ports, config, `0600` secrets, database/schema/role, logs, and backup scope. Only Development is authorized now, but creation is deferred until the artifact passes readiness tests. Production does not exist.

## Rollback and limitations

Template rollback means removing/restoring only the `LOCALRevora-main` directory after stopping a future Revora instance, then refreshing ADS by the least disruptive supported method. Artifact rollback means restoring a previously verified version while preserving config/data and verifying READY.

Known limitations: no .NET 10 SDK is installed system-wide. A checksum-verified Microsoft .NET SDK 10.0.400 is held under `/root/.local/share/revora-dotnet` for clean builds and local publishing; AMP never uses it. The in-memory adapter is non-durable and intentionally protocol-neutral; reviewed database and 5165 runtime behavior remain absent. The template schema has passed JSON/KVP and project-specific consistency checks, but no official validator was found in the installed AMP CLI or tree. No live template/instance test has occurred; update/DB backup automation is intentionally absent; AMP's host firewall posture is permissive; inherited `amp` Docker membership increases blast radius and is not used by Revora.

## Revora Web template

`deploy/amp-web/` contains the separate `LOCALRevoraWeb-main` candidate for AMP 2.8.0.4. It launches `app/Revora.Web --config config/revora-web.json` directly, reserves loopback TCP 5080, anchors readiness to `^\[Revora.Web\] READY$`, sends the exact stdin command `stop`, sets retry count to zero and has an empty update source. Registration and indexing are off; maintenance is on.

The web artifact is self-contained `linux-x64` and runs as `amp` from a path containing spaces. Data-protection keys are isolated under the instance `data/keys` directory (mode `0700`), not the shared AMP profile or immutable application directory. The Development key ring is filesystem protected but not at-rest encrypted; a reviewed production key-protection mechanism is a Production gate.

The local Web template is not installed and `RevoraWebDevelopment` does not exist. AMP's authenticated Manage Templates Refresh remains required. No live AMP path, ADS setting, instance, firewall or nginx configuration was changed for Web validation.
