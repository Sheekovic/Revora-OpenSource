# Windows Launcher Milestone Record

Date: 2026-08-28 UTC.

## Delivered

- First-party `Revora.Launcher` WPF application targeting self-contained Windows x64.
- Platform-neutral `Revora.Launcher.Core` with fixed endpoint policy, signed client profiles, critical-file SHA-256 verification, safe-path/link rejection, HTTPS-only staging, bounded files, atomic activation/rollback, and exact-process launch policy.
- `asInvoker` application manifest, no UAC/elevation, shell, service, scheduled task, persistence, injection, dynamically selected executable, or editable endpoint.
- Eleven deterministic launcher security tests.
- Reproducible developer executable at ignored path `artifacts/launcher-build-e/Revora.Launcher.exe`, 139,737,811 bytes, SHA-256 `5c804ff06c7bf482012e60042ff8c2df952888f3a7aabac889f1f0385b330dd7`.

Two independent clean publishes produced the same SHA-256 and compared byte-for-byte equal. The artifact is unsigned; Authenticode requires a legitimate publisher certificate. It contains no Conquer files, Flash runtime, loader, research material, secrets, or production signing key.

## Fail-closed release state

The UI is intentionally non-launching: no approved signed client profile or production manifest public key exists. This lets the owner inspect the launcher separately without weakening the client gate. Public launcher/full-client downloads remain disabled.

The quarantined 5165 archive is rejected after three malware detections. The ModDB/GameWatcher 5118 installer is an unapproved preservation candidate. Flash compatibility remains blocked: Ruffle is not an ActiveX drop-in, native login/handoff feasibility needs clean-client Windows evidence, and no compatibility module was selected.

## Validation

The complete solution built with zero warnings/errors. Test results were Unit 9, Integration 1, Protocol 0 fixtures by design, Host 11, Web 32, Admin 6, Playable synthetic 11, and Launcher 11. Deployment/template validation passed. The Playable test used a temporary isolated PostgreSQL database/OS role and exposed an existing migration-fixture requirement for `revora_game` to read active bans; the grant was applied only to the temporary database, which and its temporary role/account were removed afterward.

No Windows executable was run. Windows 10/11 visual, Defender, SmartScreen, update/rollback, Flash/native-login, and genuine-client interoperability tests remain mandatory.
