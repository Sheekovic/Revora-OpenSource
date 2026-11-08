# Revora Operations

Revora.Web runs on loopback 5080, Revora.Admin on loopback 5081, and PostgreSQL is local-only. `RevoraDevelopment01` exists but must remain stopped until AMP's rootless container management and SFTP listeners are proven loopback-only. Prefer AMP UI/API operations; do not run Revora as root.

Hostinger requires AMP-managed container instances. Select the normal AMP container target; do not enable privileged mode, host networking, broad host mounts, or extra capabilities. Before starting Revora, verify the host-side container process belongs to `amp` and that only the instance datastore is writable.

Observed gate: AMP 2.8.0.4 forced `0.0.0.0:8081` and `0.0.0.0:2224` during two container starts. Both were stopped immediately. Do not start the instance again until authenticated ADS reconfiguration or a reviewed stopped-ADS conversion corrects this. Admin bootstrap instructions are root-only at `/root/revora-admin-bootstrap/INSTRUCTIONS.txt` and must never be copied to Git or logs.

Installation is not an operations shortcut. Follow the guarded plan in `AMP_INSTALLATION_READINESS.md`; stop if ADS requires a restart or resolves a different instance/application path.

The first controlled attempt stopped safely because the supported template Refresh requires an authenticated panel session unavailable to automation. An operator must refresh through an existing authenticated Hostinger Game Panel session; never substitute a management credential scraped from a process or an undocumented API call.

## Start

Confirm loopback bindings, protected secret file mode `0600`, database availability, and free assigned ports. Start once. Success requires one `[Revora] READY`; process existence alone is not readiness.

For the lifecycle-only development harness, set `RuntimeAdapter` explicitly to `InMemory`; it is rejected outside Development/Test and on non-loopback addresses. `Disabled` is the safe default. In-memory state is disposable and must never be represented as a durable or protocol-compatible server.

## Stop and restart

Use AMP Stop, which writes `stop` to stdin. Wait for `[Revora] STOPPED` and exit code 0. Restart is Stop followed by Start. Kill is only for an exceeded shutdown timeout after capturing safe logs and confirming no persistence operation is active.

## Status, health, players, and save

Send the exact console commands `status`, `health`, `players`, or `save`. `save` produces an audit event followed by `PersistenceFlushComplete`. Never paste shell/SQL commands into the console.

## Logs

Review AMP-captured stdout/stderr for structured timestamp, severity, component, event name, and safe message. Never publish raw authentication logs or environment/process dumps. READY and STOPPED must appear once per successful lifecycle.

## Failed startup

Do not create a restart loop. Record exit code and safe event names. Check configuration syntax, directory ownership/modes, secret-file presence/mode, database reachability, and port ownership. Correct only the affected instance, then start once.

## Port conflict

Keep the instance stopped. Identify the owning process read-only. Do not stop unrelated services. Assign two unused loopback TCP ports through AMP and ensure AutoMap updates both nested listener values before retrying.

## Database outage

Enter/retain maintenance mode, stop new login/session acceptance, and allow bounded retries only. If persistence health cannot recover, perform graceful stop. Never expose the database or weaken authentication as a workaround.

## Safe maintenance

Enable maintenance mode, run `save`, take a consistent backup, stop cleanly, perform the approved scoped change, start once, verify layered health and READY, then disable maintenance only after validation.

## Rollback

Stop cleanly. Preserve current logs and checksums. Restore the previous verified application artifact atomically without overwriting configuration/data/secrets. Start once and verify READY. Database downgrade/restore is a separate approved procedure.

## Recommended AMP schedules

No schedules are currently installed. Proposed Development schedules:

- `save`: every 15 minutes, with overlap disabled.
- health check: every 5 minutes; alert on non-ready, do not restart automatically.
- backup: daily after `save` and a transactionally consistent DB dump; retain 7 daily and 4 weekly sets.
- controlled restart: weekly maintenance window only when required.
- log maintenance: daily, bounded by application/AMP retention.
- update check: daily metadata/checksum check after releases exist; never auto-install.
- maintenance transitions: explicit operator-controlled tasks around approved work.

Crash policy: no immediate automatic retries in the initial template. A future policy may permit at most two retries with increasing delay and a cooldown, contingent on verified AMP task semantics.

## Revora Web Development

The Web instance candidate is `RevoraWebDevelopment`, template repository `LOCALRevoraWeb-main`, executable `Revora.Web`, working directory `app`, and loopback endpoint `127.0.0.1:5080`. Recheck the port immediately before creation. Keep registration, indexing, updates and automatic restart disabled; keep maintenance enabled.

Supported stdin commands are `help`, `status`, `health`, `version`, `database`, `migration`, `backup`, `cache-clear`, `maintenance-on`, `maintenance-off`, and `stop`. Unknown input is rejected as a single command and never reaches a shell. Database/migration/backup report unavailable while durable storage is absent. `stop` and SIGTERM close the listener and emit one STOPPED marker.

The application directory is immutable to routine operations (`0750` directories, `0440` content, `0550` executable). Instance `config` is `0750` with normal files `0640`; secrets, when introduced, are `0600`. `data`, `data/keys`, logs and backup staging are separate instance-owned `0750` trees (the key directory is tightened to `0700`). Never copy the key ring independently of its protected backup scope because doing so invalidates cookies and recovery tokens.

## Public Revora.Web service

The production website is independent from AMP. Operate only `revora-web.service`; never use the unused `LOCALRevoraWeb-main` candidate or create `RevoraWebDevelopment` for the public site.

- Status: `systemctl status revora-web.service`
- Readiness from the host: `curl --fail http://127.0.0.1:5080/health/ready`
- Graceful restart: `systemctl restart revora-web.service`
- Safe logs: `journalctl -u revora-web.service --since today`

Every start must emit exactly one `[Revora.Web] READY`. A graceful SIGTERM must emit exactly one `[Revora.Web] STOPPED`. Port 5080 must remain bound only to `127.0.0.1`; public health paths are intentionally rejected by nginx. Do not put credentials into command arguments or inspect unrestricted environments.

For deployment, extract a checksum-verified artifact into a new root-owned staging directory, apply the documented immutable modes, carry forward only the reviewed non-secret configuration, stop the service gracefully, atomically exchange the app directory, and start once. Keep the prior app under the root-only rollback directory until post-deployment validation completes. See `WEB_PRODUCTION_DEPLOYMENT.md` for the current artifact, live evidence, and nginx rollback.
