# AMP Installation Readiness

Status: readiness plan; no Revora template or instance is currently installed in AMP.

Live-attempt note: on 2026-08-28 the reviewed template was staged, but the supported Refresh required an authenticated ADS UI session unavailable to automation. The staging was rolled back completely before discovery or instance creation. `AMP_LIVE_VALIDATION.md` records the non-sensitive evidence. The checklist remains the approved continuation procedure for an authenticated operator.

## Audited release artifact

`/root/Revora/artifacts/Revora.Host-linux-x64.tar.gz` was regenerated because the earlier archive included unnecessary PDBs and separate Account/Game apphosts. The final SHA-256 is:

`261e02fb279af44e327bc68a65132f0e8adc4efe0a2620a69a07f2c1a22e8205`

It is a deterministic POSIX gzip/tar containing exactly one relative regular-file entry, `Revora.Host`, mode `0755`, owner metadata `0:0`, and epoch timestamp. It has no directory, source, test, research, Git, configuration, secret, database, log, key, link, device, traversal, absolute-path, setuid/setgid, or secondary executable entry. The Release publish suppresses PDBs and bundles the reviewed .NET runtime and Revora assemblies into the single x86-64 ELF. It was extracted as `amp`, changed to the intended runtime mode `0550`, and run successfully on Debian 13 from a path containing spaces. Its dynamic dependencies are only Debian base runtime libraries resolved on this host; no system .NET installation is required.

## Confirmed live facts

Read-only inspection on 2026-08-28 confirmed AMP 2.8.0.4 and the sole instance `ADS01` at `/home/amp/.ampdata/instances/ADS01`, running as `amp:amp`. Its Generic Module template parent is:

`/home/amp/.ampdata/instances/ADS01/Plugins/ADSModule/DeploymentTemplates`

That parent is `amp:amp` mode `0755`. The vendor cache below it is `CubeCoders-AMPTemplates-main`, `amp:amp` mode `0775`. The narrow prospective local-template path is:

`/home/amp/.ampdata/instances/ADS01/Plugins/ADSModule/DeploymentTemplates/LOCALRevora-main`

It does not currently exist. No existing path therefore needs backup today. Immediately before installation, repeat the absence check. If it exists, stop and archive only that directory before proceeding.

ADS listens at `127.0.0.1:8080`; existing nginx, AMP SFTP, and metrics declarations are unchanged. TCP 9958 and 9959 were unused in the read-only socket snapshot, but this must be repeated immediately before instance creation.

## Proposed Development instance

- AMP instance identifier: `RevoraDevelopment`
- friendly name: `Revora Development`
- application root from template: `./revora/`
- application base directory: `./revora/app/`
- executable: `app/Revora.Host`
- working directory: `app`
- arguments: `--config config/revora.json`
- `RuntimeAdapter`: `InMemory` (explicit selection)
- `Environment`: `Development`
- Account: `127.0.0.1:9958/TCP`
- Game: `127.0.0.1:9959/TCP`
- maintenance mode: enabled
- registration: disabled
- automatic migrations: disabled
- automatic updates: disabled
- automatic restart: disabled (`Limits.AutoRetryCount=0`)
- shutdown timeout: 30 seconds
- logs: AMP-captured stdout/stderr plus future instance-relative `logs/`; 14-day target

The expected instance datastore is `/home/amp/.ampdata/instances/RevoraDevelopment`, following the installed Instance Manager layout. It is prospective, not confirmed: ADS must create the stopped instance first, and the resolved path must be checked before artifact extraction. Do not substitute a different path silently.

## Offline compatibility evidence

Automated validation proves JSON parsing, unique/required KVP values, manifest/include cross-references, direct executable and working-directory values, literal stdin `stop`, readable/writable STDIO, anchored `^\[Revora\] READY$`, regex readiness mode, loopback defaults, empty updates, zero retries, fail-closed adapter default, empty environment map/admin password, and absence of shell/build/update command material. It also proves the release archive contains exactly one safe regular executable entry.

Comparison with the installed AMP 2.8.0.4 vendor templates verifies that Revora uses the same observed KVP key conventions for manifest names, `App.RootDir`, `App.BaseDirectory`, Linux executable, working directory, console flags, `String` exit behavior, included ports/updates, readiness mode/regex, compatibility, and retry limits. The installed ADSModule UI contains a Manage Templates Refresh action, and the documented local repository name convention accepts `LOCALRevora-main`.

Still unverified until a controlled live load: AMP's semantic acceptance of every Revora field, whether Refresh discovers the new template without restarting ADS, AutoMap's resolved configuration path, the actual generated Generic instance datastore/application paths, artifact provisioning behavior when updates are empty, UI rendering of all settings, port reservation behavior, and end-to-end Start/Console/Stop state detection. No claim of live compatibility is made yet.

## Ownership and permissions

| Scope | Owner | Mode | Notes |
|---|---|---:|---|
| release staging directory | `root:amp` | `0750` | trusted archives readable but not replaceable by runtime |
| release archive | `root:amp` | `0640` | checksum before extraction |
| instance/application/config/data/log directories | `amp:amp` | `0750` | private to AMP runtime group |
| extracted `Revora.Host` | `amp:amp` | `0550` | executable, not normally writable |
| normal configuration | `amp:amp` | `0640` | contains no credentials |
| `config/revora.secrets.json` | `amp:amp` | `0600` | never in template, arguments, or logs |
| normal data/log files | `amp:amp` | `0640` | tighter where supported |
| backup directory | `root:amp` | `0750` | runtime-readable only when required |
| backup archives | `root:amp` | `0640` | no routine secret inclusion |
| update extraction directory | `amp:amp` | `0750` | version-specific, same filesystem for atomic rename |
| rollback directory/archive | `root:amp` | `0750`/`0640` | previous verified release, protected from runtime writes |

These modes were exercised only below `/tmp/revora-readiness-milestone`. Revora ran successfully there as `amp`; no live AMP ownership or mode was changed.

## Guarded installation checklist

The commands below are a future operator plan. They were not executed. Commands that inspect AMP instances must run as `amp`; artifact custody commands require the existing privileged operator but never run Revora as root.

1. Capture the pre-installation state without sensitive process arguments:

```sh
runuser -u amp -- ampinstmgr --ShowInstancesTable
runuser -u amp -- ampinstmgr --ShowAllInstancePorts
stat -c '%n|%U:%G|%a|%F' /home/amp/.ampdata/instances/ADS01/Plugins/ADSModule/DeploymentTemplates
find /home/amp/.ampdata/instances/ADS01/Plugins/ADSModule/DeploymentTemplates -maxdepth 1 -type d -name LOCALRevora-main -print
ss -H -ltn '( sport = :9958 or sport = :9959 )'
```

2. If `LOCALRevora-main` is absent, record that fact; there is no pre-existing target to back up. If it exists, stop and investigate ownership/provenance. With explicit approval to replace it, back up only that directory:

```sh
install -d -o root -g amp -m 0750 /home/amp/revora-install-backups
tar --format=posix --owner=0 --group=0 --numeric-owner -C /home/amp/.ampdata/instances/ADS01/Plugins/ADSModule/DeploymentTemplates -czf /home/amp/revora-install-backups/LOCALRevora-main.preinstall.tar.gz LOCALRevora-main
tar -tzf /home/amp/revora-install-backups/LOCALRevora-main.preinstall.tar.gz
sha256sum /home/amp/revora-install-backups/LOCALRevora-main.preinstall.tar.gz
```

3. Stage only the reviewed template using explicit paths:

```sh
install -d -o amp -g amp -m 0750 /home/amp/.ampdata/instances/ADS01/Plugins/ADSModule/DeploymentTemplates/LOCALRevora-main
install -d -o amp -g amp -m 0750 /home/amp/.ampdata/instances/ADS01/Plugins/ADSModule/DeploymentTemplates/LOCALRevora-main/config
install -o amp -g amp -m 0640 /root/Revora/deploy/amp/revora.kvp /home/amp/.ampdata/instances/ADS01/Plugins/ADSModule/DeploymentTemplates/LOCALRevora-main/revora.kvp
install -o amp -g amp -m 0640 /root/Revora/deploy/amp/revoraconfig.json /home/amp/.ampdata/instances/ADS01/Plugins/ADSModule/DeploymentTemplates/LOCALRevora-main/revoraconfig.json
install -o amp -g amp -m 0640 /root/Revora/deploy/amp/revorametaconfig.json /home/amp/.ampdata/instances/ADS01/Plugins/ADSModule/DeploymentTemplates/LOCALRevora-main/revorametaconfig.json
install -o amp -g amp -m 0640 /root/Revora/deploy/amp/revoraports.json /home/amp/.ampdata/instances/ADS01/Plugins/ADSModule/DeploymentTemplates/LOCALRevora-main/revoraports.json
install -o amp -g amp -m 0640 /root/Revora/deploy/amp/revoraupdates.json /home/amp/.ampdata/instances/ADS01/Plugins/ADSModule/DeploymentTemplates/LOCALRevora-main/revoraupdates.json
install -o amp -g amp -m 0640 /root/Revora/deploy/amp/config/revora.json /home/amp/.ampdata/instances/ADS01/Plugins/ADSModule/DeploymentTemplates/LOCALRevora-main/config/revora.json
install -o amp -g amp -m 0640 /root/Revora/deploy/amp/config/revora.secrets.example.json /home/amp/.ampdata/instances/ADS01/Plugins/ADSModule/DeploymentTemplates/LOCALRevora-main/config/revora.secrets.example.json
```

4. Use ADS's installed Manage Templates **Refresh** action. Its presence is confirmed in the installed ADSModule UI; whether 2.8.0.4 accepts Revora without an ADS restart remains a live-load test. Do not use `ampinstmgr --UpgradeAll`, restart ADS, or refresh by an undocumented API. If the UI requests a controller restart, stop for approval.

5. Verify Revora appears once, vendor-template counts/checksums are unchanged, and ADS01 remains running. Create only the stopped Generic instance `RevoraDevelopment` through ADS with the proposed values. Do not enable start-on-boot or firewall synchronization. Confirm the resolved datastore before continuing:

```sh
runuser -u amp -- ampinstmgr --ShowInstanceInfo RevoraDevelopment
stat -c '%n|%U:%G|%a|%F' /home/amp/.ampdata/instances/RevoraDevelopment
ss -H -ltn '( sport = :9958 or sport = :9959 )'
```

6. Stage and verify the reviewed release, then extract only after the resolved application path agrees with the template:

```sh
install -d -o root -g amp -m 0750 /home/amp/revora-releases
install -o root -g amp -m 0640 /root/Revora/artifacts/Revora.Host-linux-x64.tar.gz /home/amp/revora-releases/Revora.Host-linux-x64-0.1.0-readiness.tar.gz
sha256sum /home/amp/revora-releases/Revora.Host-linux-x64-0.1.0-readiness.tar.gz
tar -tzf /home/amp/revora-releases/Revora.Host-linux-x64-0.1.0-readiness.tar.gz
runuser -u amp -- tar -xzf /home/amp/revora-releases/Revora.Host-linux-x64-0.1.0-readiness.tar.gz -C /home/amp/.ampdata/instances/RevoraDevelopment/revora/app
chmod 0550 /home/amp/.ampdata/instances/RevoraDevelopment/revora/app/Revora.Host
```

Provision `config/revora.json` at mode `0640` and the out-of-band non-placeholder `config/revora.secrets.json` at mode `0600`, without placing secret data in a command line. Confirm both listeners remain loopback.

7. Start once from ADS. Require one `[Revora] READY`. Test `status`, `health`, `players`, `save`, and `help`; reject malformed console input. Stop via AMP and require one `[Revora] STOPPED` plus exit 0. Restart once and expect volatile in-memory state to reset; only configuration persists. Confirm no socket on 9958/9959 is bound to a non-loopback address and ADS01/other services are unchanged.

## Rollback

Rollback triggers include template rejection, duplicate template registration, unexpected ADS restart request, wrong resolved paths/ownership, non-loopback binding, missing/duplicate markers, nonzero clean-stop exit, unrelated instance changes, or checksum mismatch.

Keep RevoraDevelopment stopped. Preserve its safe logs. Move the new local template aside rather than deleting it:

```sh
mv /home/amp/.ampdata/instances/ADS01/Plugins/ADSModule/DeploymentTemplates/LOCALRevora-main /home/amp/.ampdata/instances/ADS01/Plugins/ADSModule/DeploymentTemplates/LOCALRevora-main.failed
```

If a pre-install backup existed, restore only it:

```sh
tar -xzf /home/amp/revora-install-backups/LOCALRevora-main.preinstall.tar.gz -C /home/amp/.ampdata/instances/ADS01/Plugins/ADSModule/DeploymentTemplates
```

Use Manage Templates Refresh again. If refresh requires restarting ADS, stop for approval. Verify ADS01, template inventory, instance inventory, ports, nginx listeners, and firewall state match the pre-install capture. Deleting the failed Development instance is destructive and requires a separately reviewed ADS action; it is not part of automatic rollback.
