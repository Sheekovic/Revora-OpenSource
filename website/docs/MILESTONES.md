# Revora Milestones

## Windows launcher research milestone — completed with release gates (2026-08-28)

The injection-free first-party launcher, signed-profile/hash/update core, reproducible Windows developer artifact, quarantine forensics, and Windows VM plan are complete. No client is approved, no launcher/client download is enabled, and no Windows or genuine-5165 compatibility is claimed. See `docs/LAUNCHER_MILESTONE.md`.

## M0 — Safe foundation

Acceptance requires:

- Required policy, architecture, security, research, milestone, and notice documents exist and agree.
- Approved repositories are isolated, pinned, license-reviewed, hashed/inventoried, and never executed.
- The clean .NET 10 solution has only reviewed project references and no third-party packages, imported targets, scripts, or native dependencies.
- Nullable analysis and warnings-as-errors are enabled centrally.
- Generic networking tests cover fragmented headers/bodies, coalesced frames, malformed/oversized lengths, cancellation, bounded backpressure, concurrent cleanup, and resource release.
- Revora builds and all tests pass on the supported SDK. If the SDK is unavailable, that is an explicit open gate.

## M0A — AMP integration foundation

This is additive and does not change M1–M4 interoperability scope.

Acceptance requires:

- Installed AMP version, instance/user model, ports, firewall posture, and supported local-template method are documented from read-only inspection.
- `Revora.Host` coordinates real persistence, Account, and Game runtime components and remains free of domain/protocol/gameplay logic.
- Lifecycle tests pass for ordered startup, failed database startup, occupied ports, SIGTERM/SIGINT, stdin stop, timeout, component failure, exit codes, premature READY prevention, secret redaction, spaces in paths, invalid configuration, two isolated loopback instances, and malformed console input.
- AMP template JSON/KVP validates against installed AMP 2.8.0.4 and launches a prebuilt self-contained executable without scripts or restore/build actions.
- A local `linux-x64` artifact passes clean start/ready/console/save/stop/restart/crash tests outside AMP.
- Only then may the private template be installed and one non-public `Revora Development` instance created, with an exact pre-change backup and verification that ADS/other instances are unaffected.

Current state: the explicitly selected loopback-only in-memory harness supplies volatile persistence and protocol-neutral Account/Game listeners while the default remains fail-closed. It can honestly reach READY and is covered by lifecycle, port-conflict, failure-propagation, isolation, bounded-shutdown, console, marker-count, and redaction tests. Durable adapters, verified 5165 behavior, official AMP schema validation, live AMP lifecycle testing, template installation, and Development instance creation remain gated.

Installation-readiness state: the deployment publish is a self-contained single-file `linux-x64` executable in a deterministic one-entry archive. Repository validation checks archive paths/types/modes, template references and safety defaults, and temporary permission behavior. It extracts and runs as `amp` from a path containing spaces. Live template parsing, non-disruptive ADS refresh behavior, resolved Development datastore paths, and AMP lifecycle behavior remain intentionally unverified until an approved controlled installation.

Controlled-installation attempt: the reviewed template was staged byte-identically, but ADS requires an authenticated panel session to invoke its supported Refresh action. Automation had no safe session and did not use credentials from process data or restart ADS. The template was narrowly archived and removed from the discovery path; no instance was created. See `AMP_LIVE_VALIDATION.md`.

## M1 — Verified account login and server selection

Entry gate: authentication packet layouts, framing, encryption transition, and handoff response are Confirmed in `PROTOCOL_RESEARCH.md`.

Current preparation: protocol-independent authentication throttling and an in-memory handoff-token store now provide bounded key tracking plus cryptographically random, hashed-at-rest lookup, expiring, realm-bound, single-use tokens. These components do not imply that the 5165 login request, client password transformation, response layout, or encryption transition is known. The M1 entry gate remains closed pending authentic-client evidence.

Durable validation update (2026-08-28): PostgreSQL migration 3, adaptive password hashes, atomic digest-only handoff consumption, character persistence, authoritative movement, session cleanup, and two-client visibility are implemented behind `PostgreSqlSyntheticTest`. Its independent JSON test wire format is not proof of 5165 compatibility and is never selected by default or in Production.

Acceptance requires deterministic known-good fixtures; malformed/truncated/oversized/replayed fixture rejection; authentication rate-limit tests; no credential logging; modern password-hash storage; single-use expiring handoff-token tests; loopback-only defaults; and successful interaction with an untouched isolated 5165 client in an approved test environment.

## M2 — Character creation and entry

Entry gate: connect, creation, response, and entry packet facts are Confirmed.

Acceptance requires validation tests for name/appearance/class ranges; duplicate-name concurrency tests; parameterized persistence integration tests; atomic handoff/session claim tests; reconnect and duplicate-login policy tests; and authentic-client creation plus entry without privileged shortcuts.

## M3 — Map loading and movement

Entry gate: map/action/movement structures and coordinate rules are Confirmed.

Acceptance requires deterministic map-entry fixtures; coordinate/bounds and illegal-transition tests; fragmented/coalesced encrypted-stream tests at the correct state; cancellation during map transition; authentic-client map display; and movement visible to the moving client without desynchronization.

## M4 — Nearby-player visibility (first playable milestone)

Entry gate: spawn/update/removal structures and visibility transitions are Confirmed.

Acceptance requires two-client tests for enter range, move in range, leave range, disconnect, map change, and simultaneous movement; no duplicate/ghost sessions under concurrency; bounded broadcast backpressure; cleanup after abrupt disconnect; and two untouched 5165 clients observing correct movement and removal.

Combat, inventory, NPCs, guilds, trade, events, payments, client modification, and launcher work remain out of scope until M4 is accepted.

## W0–W5 — Web platform foundation

- W0: PostgreSQL selected for future durability; boundaries, migration policy, identity threat model, patch design, SEO and rollback documented. No database installed.
- W1: original responsive brand, guardian art and complete bilingual server-rendered public pages implemented with Arabic RTL, English LTR, truthful availability and progressive enhancement.
- W2: disabled-by-default registration, cookie/CSRF/rate-limit policy, non-enumerating recovery/login foundation, safe redirects, session revocation and protected dashboard implemented. No usable public identity exists.
- W3: policy-protected Admin UI and redacted audit contract implemented. All mutations remain disabled.
- W4: honest download center plus declarative signed-manifest validation implemented. No client, patch endpoint or private signing key exists.
- W5: the self-contained website artifact and loopback lifecycle foundation are implemented. The reviewed AMP template remains an unused reference because the approved public deployment uses a dedicated hardened systemd service instead.

W6 public-only deployment completed on 2026-08-28. Revora.Web runs as `revora-web` on `127.0.0.1:5080`, nginx serves the bilingual application at the canonical HTTPS origin, www redirects permanently, legitimate public pages are indexable, private routes remain noindex, and both AMP hostnames passed regression checks. Registration, identity, database, client downloads, patch delivery, and Admin mutations remain unavailable.

## S1 — Durable server and protected operations

PostgreSQL 17 is local-only with least-privilege roles, migrations 1–3, and a verified dump/restore. Revora.Admin is live at `https://admin.playrevora.com` with separate identities, forced bootstrap password replacement, mandatory TOTP enrollment, CSRF, throttling, revocable sessions, noindex, and append-only audit events.

AMP validation is blocked safely: its rootless wrapper forced management/SFTP onto `0.0.0.0:8081`/`:2224` despite loopback metadata. Both starts were stopped and ports 2224, 8081, 9958, and 9959 were released. Authenticated ADS reconfiguration or a reviewed stopped-ADS conversion is required.

## C1 — Client Doctor owner test 1

- [x] Read-only bounded donor inventory and unconditional active-file rejection.
- [x] Separate deterministic constructor core and signed-profile policy.
- [x] Separate testing/production ECDSA identities and protected VPS repository.
- [x] Reproducible inventory-only Windows artifact.
- [ ] Owner Defender/Malwarebytes and Windows 10/11 inventory report.
- [x] Deterministic 5156 plus 5157–5165 static reconstruction and complete passive testing profile.
- [ ] Authoritative malware-free active components and Windows security-product approval.
- [ ] Private Flash/native-login compatibility proof.
- [ ] Genuine 5165 interoperability and public-download approval.

## C2 — Owner Test 4 automatic Account bootstrap

- [x] Exact 5165 `CMD_GAMESTART` to internal Win32 `0x464`/command `0x480` and Account-builder call graph recorded with confidence levels.
- [x] Deterministic patch accepts one reviewed input, asserts all original bytes, and produces SHA-256 `6d03cd53edde97c4354eea42b07e7fa79f7132d65f4710ba45f7c6b5da59e11d`.
- [x] Launcher-only HTTPS authentication and 60-second digest-stored ticket contract.
- [x] PID-bound one-connection bridge on `127.0.0.1:9958`, evidenced cipher, strict 276/1086 validation, atomic consumption/revocation and controlled 1055 response.
- [x] PostgreSQL V004 validated on a clean temporary database and applied after a checksum-verified backup.
- [x] Private authenticated Owner Test 4 package prepared; public Download remains disabled.
- [x] Test 4 and 4b classified as genuine partial failures; neither produced an Account connection.
- [x] Test 4c framework-dependent package is 3,044,523 bytes, excludes the runtime, and uses a one-time verified owner workspace.
- [x] Empty-buffer acceptance claim withdrawn; fixed non-secret compatibility state and exact internal dispatcher are now explicit.
- [x] Freeze the fixed-address automatic UI-bootstrap path after reproducible Test 4j wrapper rejection and client closure.
- [x] Resolve the endpoint constraint through the explicitly approved Reviewed RSA-testing configuration-only client, limited to the 64 Server.dat modulus immediates.
- [x] Prepare private Manual Playable Baseline M1 with complete donor verification, transactional workspace reuse, PID-bound loopback probe and controlled encrypted 1055 response.
- [x] Genuine manual-client observation of encrypted 276/1086 followed by the client-visible controlled 1055 rejection (`Failed to login: Invalid account or password!`).
- [ ] M2 exact Game DH/Blowfish transition evidence before publishing successful 1055 and Game handoff owner testing.
- [x] Owner Test 4d R1 evidence classified without treating an all-false, uncontrolled trace as proof of a skipped hook.
- [x] Owner Test 4e exact-hash control trace prepared with independent startup/window controls and precise root classification.
- [x] Sole genuine Test 4e run preserved: stages 1–8 present, ambiguous global ordering, no PID-bearing records, timeout.
- [x] Test 4f structured PID/thread/sequence/timestamp trace and corrected `0x44b8b4` ABI transition prepared.
- [ ] One genuine Test 4f Windows run identifies the first valid missing transition stage.
- [ ] Genuine successful Account response and Game handoff.

This milestone does not claim playable login. The preferred final public launcher target is .NET Framework 4.8.1 WPF with a package target below 10 MB; porting waits until authentication, bridge, update and launch contracts stabilize.
