# Revora cinematic launcher and desired-state patching

## Trust and process boundary

`Revora.Launcher` is a framework-dependent .NET 10 WPF application that runs as the current Windows user. It does not request elevation, install a service, execute shell commands, inject code, load plugins, modify DNS/hosts, disable security products, or accept editable Account/Game endpoints. `Revora.Bootstrapper` is a separate fixed first-party executable used only to replace a stopped launcher from a signed launcher-release manifest.

The embedded production ECDSA P-256 public key verifies immutable envelopes. The corresponding private key remains root-controlled outside Git and outside every release. Client releases pin patch 5165, `playrevora.com`, Account `9958`, Game `9959`, the exact approved executable hash, release identity, minimum launcher version, and every managed relative path. Stable and test endpoints can expose the same signed release bytes; promotion does not rebuild client content.

The owner/test package opts into the non-default test endpoint with an exact package marker named
`Revora.TestChannel` whose complete content is `revora-launcher-test-channel-v1`. The existing
`--test-channel` switch remains available to release engineering. There is no user-facing channel
picker, and a package without either explicit test identity always uses stable. An invalid marker
fails closed. Both test manifests still require the production public-key signature; the marker
does not bypass trust validation. Test and stable cache files are separate so an unavailable test
release cannot silently fall back to stable state.

## State machine

The launcher uses these explicit states:

`Starting → ResolvingClient → Recovering → FetchingManifest → VerifyingManifest → Scanning`

Scanning reaches either `Ready` or `RepairRequired`. Repair follows:

`RepairRequired → Downloading ↔ Paused → VerifyingDownloads → Applying → Scanning`

Launch follows:

`Ready → Launching → GameRunning → Ready`

An explicit additional-account launch follows:

`GameRunning → Launching → GameRunning`

Maintenance, Offline and Error have bounded retry paths. Illegal transitions throw and are covered by deterministic tests. `Play Revora` is rendered only in `Ready`.

Version 1.0.7 adds a bounded first-install path. A portable launcher may manage its own writable,
safe directory when no client fingerprint exists yet; a blank directory selected elsewhere is
still rejected. The signed desired-state manifest then supplies the complete client through the
same staging, hash verification, atomic replacement, and final verification path used for repair.
Partial client fingerprints are classified as repairable so a missing critical file does not force
a full download. Unmanaged screenshots, settings, and logs remain outside the managed allowlist.

A normal initialization cycle scans, automatically repairs only signed managed-file failures, verifies
the resulting desired state, and claims one automatic launch. Clean clients launch once without a
repair. Version 1.0.8 keeps the launcher available while games run and exposes `Open Another Account`;
each deliberate click starts one independently tracked, already-verified genuine client. Closing one
client does not disturb the others. The state returns to `Ready` only after the final tracked client
exits, and no exit claims another automatic launch. Full Repair remains manual and is unavailable while
a tracked game runs. The existing process-safety guard still blocks every patch application whenever a
genuine game process is running; multi-account launch permission never grants file-mutation permission.

## Portable root and file policy

The default candidate is resolved from `Environment.ProcessPath`, never the shell working directory. An existing root is classified as complete or repairable from the reviewed patch-5165 fingerprint (`Conquer.exe`, `version.dat`, and the expected data directories). A root with no fingerprint is accepted for first installation only when it is the directory containing the running `Revora.Launcher.exe`; an arbitrary blank folder selected elsewhere remains rejected. Every root must be writable without elevation, contain no reparse-point escape, and not be a drive root, Windows root, Program Files root, profile root, Git repository, or launcher staging directory. `.revora-launcher/patch.lock` is held exclusively during mutation.

Only paths in the signed desired-state manifest are managed. Extra content is not mirror-deleted. Screenshots, logs, crash reports, exported diagnostics and user-local settings remain outside the managed allowlist. Known-incompatible-file quarantine requires a future explicit signed policy; the current launcher does not invent one.

## Verification cache

Critical executables, DLLs, `Server.dat`, `version.dat` and other security-sensitive entries are always hashed. Passive files may use the release-bound verified-state cache only when normalized path, size, last-write ticks and the prior verified SHA-256 all match. Missing entries, metadata changes, manifest changes, interrupted apply journals and malformed cache JSON force hashing. Full Repair hashes every managed file.

## Download, apply and recovery

Objects use `objects/sha256/<prefix>/<sha256>` under fixed HTTPS endpoints. The downloader has bounded concurrency and retries, exponential backoff with cryptographic jitter, CDN/origin fallback, valid HTTP range continuation, no-range restart, pause/resume, cancellation, disk-space preflight and optional bandwidth limiting. Redirects are disabled. Signed transfer/expanded sizes and final SHA-256 are enforced. Gzip expansion is bounded exactly to the signed size.

Objects remain in `.revora-launcher/staging`. A write-through replacement is materialized and rehashed before mutation. The apply journal is flushed before moving a live file. Existing files move into a unique release/operation rollback directory, then replacements move atomically into place. Recovery distinguishes a merely planned entry from a completed backup, preventing deletion of an untouched file after forced termination. A failed operation restores only files listed in its journal.

## Self-update

The launcher verifies the launcher-release manifest for its resolved package channel. Test packages
use `/launcher/v1/test/manifest.json`; ordinary packages use
`/launcher/v1/stable/manifest.json`. When a newer version is valid, it copies the fixed reviewed
framework-dependent bootstrapper package to a temporary directory, starts it without shell
execution, and exits. The bootstrapper waits boundedly for the old PID, downloads only signed
release entries, verifies every size/hash, swaps the stopped launcher directory, starts the new
entry point and waits for `.launcher-ready`. Missing readiness restores the old directory. A
framework-dependent bootstrapper avoids the opaque native single-file bundle that triggered a
ClamAV heuristic during validation; it uses the same installed .NET Desktop Runtime required by
the launcher.

No Authenticode claim is made until a protected certificate is available and the exact artifacts are signed and verified. ECDSA release signing authenticates Revora content independently of Authenticode/SmartScreen reputation.

## Authentication boundary

The production launcher is a signed desired-state patcher and verified process launcher, not an
authentication client. It never asks for, transmits, stores, or forwards game credentials or a
launcher ticket. After a normal scan or automatic signed repair reaches a valid state, it launches
the approved client once. The genuine client continues to perform Account/Game authentication.

`LauncherAuthenticationClient` remains dormant in `Revora.Launcher.Core` only because the isolated
historical Manual Playable H1 protocol probe still references it. `Revora.Launcher` does not
construct or invoke it, and no launcher session is present in the WPF lifecycle.

The approved production client hash `902954c9...93c2f` has a proven native command-line username
and password surface, but Windows exposes those arguments and it is therefore forbidden for a
password or bearer ticket. It does not contain a proven named-pipe, shared-memory or other
memory-only launcher-ticket consumer. Earlier fixed-address automatic bootstrap experiments were
frozen after genuine instability. Therefore a signed patch/repair release can be production-ready
independently, but **launcher-only automatic legacy Account authentication cannot be claimed or
promoted as complete** until an explicitly approved, genuine-client-proven ticket transport exists.
The complete evidence matrix is in
`research/launcher/APPROVED_5165_HANDOFF_EVIDENCE.md`. The launcher must not consume a ticket
speculatively or describe normal client launch as ticket handoff.

## Publisher

`Revora.PatchPublisher` accepts a reviewed canonical client root plus the existing full profile allowlist. It hashes every entry, refuses any mismatch, creates missing content-addressed objects, emits schema-v2 canonical JSON, signs it with an explicitly supplied protected PEM, and emits a release report. Original archives and client files remain outside Git. `verify-publication` validates the signed envelope and every published object reference with only the public key.

Launcher releases are published separately with `publish-launcher`; only `.exe`, `.dll`, `.json`, `.png` and `.ico` files are eligible, PDBs are excluded, and both launcher and bootstrapper must be present.

## Diagnostics and privacy

Diagnostics contain bounded timestamps, state/category, signed release/build identity, normalized managed paths and sanitized result categories. They exclude passwords, tickets, authorization headers, environment variables and absolute user/client paths. ZIP timestamps are deterministic and valid on Windows.
