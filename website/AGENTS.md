# Revora Project Instructions

These instructions apply to the entire repository.

## Owner authority and instruction precedence

- The repository owner authorizes agents to complete Revora goals through any
  technically justified, lawful, evidence-backed implementation within the
  Revora ecosystem. This includes coordinated work across Revora server
  repositories, Revora Web, PlayRevora Launcher, Revora-owned native Bridges,
  approved Patch-7630 client integration, deployment tooling, diagnostics,
  branding, and operator applications.
- The owner-authorized Patch-7630 interoperability section below is a specific
  exception that takes precedence over older or more general wording in this
  file concerning client modification, injection, native dependencies,
  runtime loading, hooks, dynamic analysis, derivative builds, or Owner-Test
  packaging.
- Milestone lists are sequencing guidance, not hard scope prohibitions. Agents
  may implement prerequisites and cross-component changes required to reach an
  explicitly requested Revora outcome without requesting separate approval for
  every implementation technique.
- When evidence disproves a planned seam, architecture, or integration method,
  continue to the next evidence-backed Revora approach. Do not stop solely
  because a hook is inactive, a native component is required, or work crosses
  repository boundaries.
- Explicit owner requests in the active task may authorize deployment,
  publication, public binding, or production changes for their stated scope.
  Preserve backups, least privilege, validation, and rollback rather than
  treating older blanket wording as an automatic blocker.

## Scope

- Project: Revora, an independently written Conquer Online server emulator.
- Required client target: patch 7630.
- Platform: C# on .NET 10 LTS; Windows is the primary development environment.
- Current priority is account login, server selection, character creation and
  entry, map loading, movement, and nearby-player visibility. This priority does
  not prohibit launcher, Bridge, approved-client, diagnostics, deployment, or
  other prerequisite work needed to complete it.
- Defer unrelated gameplay expansion unless the active owner request asks for
  it. Client integration and display/launcher work are permitted when required
  by the requested milestone or separately requested by the owner.

## Legacy and licensing boundaries

- `conquer-online/comet` may be used according to its repository license, with attribution recorded in `THIRD_PARTY_NOTICES.md`.
- `DuneGameStudio/HeroesCoServer` is untrusted research evidence only. Do not copy its code, assets, database, or expressions into Revora.
- Do not acquire or inspect `Mohamed-Source5165` without explicit user authorization.
- Keep all research checkouts under ignored `research/isolated/`; never add them to Git.
- Never build, restore, run, load, or dynamically inspect unapproved legacy
  server repositories or their EXEs, DLLs, scripts, archives, hooks,
  generators, or MSBuild targets. This restriction does not apply to the exact
  approved Patch-7630 client hashes or Revora-owned synthetic targets under the
  interoperability authorization below.
- Inspect legacy material as text/data only. Record provenance, commit SHA, and confidence for every protocol claim.
- Never describe an inferred packet layout as verified.
- Do not copy any legacy source into Revora without explicit user approval and a documented compatible license.

## Security rules

- Never hardcode secrets or store plaintext account passwords. Redact credentials and tokens from logs.
- Load secrets from environment variables or protected, ignored local-development settings.
- Use parameterized database operations only; never concatenate SQL.
- Validate packet lengths strictly and enforce a configured maximum packet size.
- Implement TCP stream framing across fragmented/coalesced receives; one receive is not one packet.
- Every connection owns cancellation and bounded queues. Avoid globally mutable game state; ownership must be explicit.
- Rate-limit authentication attempts and malformed traffic.
- Keep administrative commands separate from chat and require explicit authorization for every admin action.
- Use structured logging, do not swallow exceptions broadly, and default listeners to loopback.
- Revora-owned native dependencies and approved-client integration techniques
  are pre-authorized under the interoperability section below when technically
  justified. Never recommend antivirus exclusions or weakening host security.

## Owner-authorized Patch-7630 client interoperability

The repository owner explicitly authorizes Revora client integration needed to
make the approved, lawfully possessed Patch-7630 client interoperate reliably
with Revora. This authorization applies to current and future login,
world-entry, launcher, rendering, compatibility, diagnostics, branding, and
operator-tooling milestones.

Agents may, without requesting separate approval for each technique:

- Perform static and dynamic analysis of the approved DX8/DX9 executables and
  their runtime-loaded modules.
- Perform bounded user-mode, hash-gated debugging of the approved DX8/DX9
  builds. Targeted software/hardware breakpoints, call stacks, thread IDs,
  register/control-flow metadata, API-boundary tracing, and bounded execution
  traces are permitted for interoperability.
- Execute the approved client hashes in bounded Owner Tests on the owner's
  authorized Windows machine after appropriate synthetic validation.
- Create Revora-owned x86 DLLs, native helpers, loaders, shims, proxies,
  compatibility layers, and narrowly scoped native dependencies.
- Load Revora-owned modules into an approved `Conquer.exe` launched through
  PlayRevora Launcher.
- Use hash-bound hooks, trampolines, call-site redirection, import/export or
  socket wrapping, bounded in-memory patches, protocol adapters, and signed
  derivative builds at proven UI, login, networking, bootstrap, rendering, or
  compatibility seams.
- Modify approved client configuration and Revora-owned compatibility files
  transactionally, and create clean derivative client artifacts when runtime
  integration alone is insufficient.
- Capture credentials transiently at a proven native Login-submit boundary,
  place them only in bounded temporary memory, authenticate over validated
  HTTPS, clear them on every terminal path, and exchange them for short-lived,
  single-use server credentials.
- Build separate DX8/DX9 paths where hashes, offsets, ABI, or behavior differ.
- Change seams and implementation architecture whenever evidence invalidates a
  prior approach, without pausing for renewed owner approval.
- Create private functional Owner-Test packages, transactional installers,
  rollback tooling, secret-free diagnostics, synthetic targets, and signed
  manifests.
- Coordinate required changes across Identity, Account, Game, Web/Auth,
  Launcher, Bridge, client compatibility, and deployment repositories.

Approved client executables:

- DX8 `Env_DX8\Conquer.exe` SHA-256
  `F417A25AB8B037D37619E304618BE615F7645CAF39F760EFF98D988796EE4E1E`.
- DX9 `Env_DX9\Conquer.exe` SHA-256
  `4889B6DAB71068D687A9757F64FB1033E9CE1C560714DF3B62634229776A65CE`.

Mandatory safeguards:

- Bind loaders, hooks, patches, and derivatives to normalized approved paths,
  architecture, profile, and cryptographic hashes; reject unknown builds and
  processes.
- Do not target, inject into, inspect, or modify unrelated applications or
  machines. Do not create a general-purpose injector, persistence mechanism,
  kernel component, stealth feature, or privilege-escalation path.
- Debugging is limited to the approved client process and Revora-owned
  synthetic targets. Credential contents, secret packet payloads, broad memory
  dumps, security bypasses, and unbounded instruction/event tracing remain
  prohibited.
- Do not disable or evade operating-system security, antivirus, code signing,
  licensing, anti-cheat, anti-tamper, firewall, or application-control policy.
- Never persist or log plaintext passwords, credential buffers, secret packet
  bodies, grants, tokens, nonces, private keys, password material, or database
  credentials.
- Keep credential access restricted to the explicit Login attempt with bounded
  buffers, validated TLS, strict timeouts, exception containment, deterministic
  UI recovery, and best-effort zeroization.
- Preserve Argon2id, replay protection, rate limits, atomic single-use grants,
  and separation of account, character, and wire identities.
- Prefer clean-room interoperability. Do not copy proprietary implementation
  code or redistribute proprietary executables without a separately documented
  lawful distribution decision.
- Preserve originals, document on-disk derivative changes, verify exact
  preimages/postimages, and provide transactional rollback.
- Use reproducible builds where practical, exact hashes, signed manifests,
  synthetic tests, redacted diagnostics, and private Owner Tests before stable
  publication.

An agent must not stop merely because an earlier address, ordinal, dispatcher,
hook, or protocol assumption is inactive. Continue through another proven UI,
request-construction, socket, protocol, server, or derivative-build boundary.
A genuine stop is appropriate only when the remaining action would exceed the
safeguards above, require unavailable owner credentials/infrastructure, violate
law or a higher-priority platform safety policy, or require an unresolved owner
product decision that materially changes the outcome.

## Engineering and validation

- Use SDK-style projects, `Revora.*` namespaces, nullable reference types, and warnings as errors.
- Dependency direction is enforced by `docs/ARCHITECTURE.md`; Domain remains dependency-free.
- Add deterministic fixtures and malformed-packet tests for confirmed protocol behavior.
- Test fragmentation, coalescing, invalid lengths, bounded backpressure, cancellation, cleanup, and concurrent session ownership.
- Build and run only Revora-owned clean code after confirming there are no imported targets, scripts, or unclear dependencies.
- Before a protocol change, update `docs/PROTOCOL_RESEARCH.md` with evidence and confidence.
- Do not bind publicly, publish, push, release, deploy, or expose services without explicit user approval.

## AMP and Linux hosting

- The supported panel track is Hostinger Game Panel backed by CubeCoders AMP Generic Module on Debian 13.
- `Revora.Host` is the single AMP-managed composition/lifecycle process. Do not move domain, protocol, persistence, or gameplay logic into it.
- Never emit `[Revora] READY` until persistence plus the Account and Game listeners are healthy and can serve their intended responsibilities.
- Graceful stdin `stop`, SIGTERM, and SIGINT use one bounded shutdown path. `[Revora] STOPPED` is printed only after successful cleanup and immediately before a zero exit.
- Operator input is an explicit allowlist. Never pass it to a shell, SQL engine, evaluator, or player-chat command path.
- AMP starts a prebuilt self-contained `linux-x64` `Revora.Host`; never configure Revora or its update actions to run scripts, restore, build, `dotnet run`, Git, Docker commands, Wine, or Proton.
- Hostinger's AMP deployment may use its existing AMP-managed rootless container mode. The host-side runtime must remain owned by the unprivileged `amp` user; privileged containers, host networking, broad host mounts, added capabilities, and Revora-controlled container commands are prohibited.
- Keep source-controlled templates under `deploy/amp/`. Do not edit AMP vendor templates in place.
- Secrets never appear in AMP manifests/KVP, command-line arguments, environment dumps, logs, or Git. Instance secret files are `0600`; instance/config/data/log directories are `0750` or stricter.
- Development remains loopback-bound. Opening ports, changing firewall rules, public binding, live template installation that affects ADS globally, controller restart, production instances, and automatic updates require explicit approval.
- Never modify or stop another AMP instance. Back up each exact AMP file before an approved live modification.

## Revora Web platform

- `Revora.Web` is a separate server-rendered ASP.NET Core composition root. It may depend on Application abstractions but must not couple website identity to unverified patch-7630 wire authentication.
- Development binds to an explicitly configured loopback address only. Production configuration is fail-closed and nginx remains the sole public HTTP/TLS listener.
- Public registration stays disabled until durable persistence, HTTPS, email verification/delivery, rate limiting, abuse controls, backups, and the game-account compatibility decision are all reviewed and operational.
- Authentication, dashboard, Admin, reset, verification, diagnostics, and maintenance routes remain `noindex`. Keep the whole website `noindex` until the documented indexing gates and visual review pass.
- Never commit proprietary client files, signing private keys, database dumps, generated instance data, website secrets, or release artifacts.
- Patch manifests are immutable and signed offline. They contain normalized relative destinations and HTTPS URLs only, never executable commands.
- Administrative routes require explicit policies. UI visibility is not authorization, and all future mutations require a redacted audit event.

## Windows launcher and client evidence

- Client archives, extracted trees, SWFs, packet captures, proprietary files, and malware samples remain outside Git and must never be executed on the VPS.
- `Revora.Launcher` runs without elevation, arbitrary shell execution,
  unrelated persistence, antivirus exclusions, bundled Flash, or editable
  server endpoints. Hash-bound loading of Revora-owned Bridge modules into the
  approved client is expressly permitted by the interoperability section.
- Client launch fails closed until a clean patch-7630 tree, signed full critical-file profile, lawful distribution decision, and Windows validation exist.
- The fixed endpoint is `playrevora.com`, Account TCP 9958 and Game TCP 9959. Client configuration is never a server security boundary.
- Do not enable a launcher download or claim Windows/7630 interoperability until all clean-client, native-login, real-Windows, and interoperability gates pass. An explicitly operator-approved, hash-published original patch-7630 baseline archive may be hosted as an unsupported baseline without claiming launcher or server compatibility.
- `Revora.ClientDoctor` treats a selected client only as an untrusted, read-only donor. It never executes or repairs source content and constructs only in a separate safe destination from signed profiles.
- Active client content is never donor-reused. Signing keys remain root-only outside Git and signing is an explicit offline/manual operation.
