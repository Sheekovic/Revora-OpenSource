# Revora Security

## Live protected services

Revora.Admin is a distinct service identity and cookie/key ring on loopback 5081. Administrator identities are separate from game accounts. Bootstrap instructions are root-only; first login requires password replacement and TOTP enrollment. Mutations require authentication, CSRF, recent authentication for high-risk actions, and append-only audit events. The host is permanently noindex and exposes no public health endpoint.

The synthetic game adapter is restricted to Development/Test and loopback and is not a production fallback. PostgreSQL credentials are absent from AMP; the game runtime uses peer authentication through an instance-local Unix-socket path.

## Threat model

Revora assumes hostile unauthenticated Internet traffic, malformed and replayed client packets, credential stuffing, malicious player input, compromised legacy source archives, accidental secret disclosure, abusive administrators, database compromise, and concurrency races that duplicate or corrupt player state.

Initial services bind to loopback only. Public exposure requires a separate deployment review covering TLS/proxy boundaries, firewalling, rate limits, monitoring, backups, and incident response.

## Core controls

- Reject frames smaller than the protocol header or larger than the configured maximum before allocation or parsing.
- Parse untrusted lengths with checked arithmetic and preserve partial frames across receives.
- Bound all queues and connection counts; cancellation and cleanup must be deterministic.
- Apply per-source and per-account authentication limits plus escalating limits for malformed traffic.
- Treat names, chat, and all packet fields as hostile. Validate ranges and normalization at trust boundaries.
- Use structured event IDs and redact passwords, tokens, connection secrets, and personal data.
- Catch exceptions only where a recovery or translation policy exists; retain causality in logs.

## Secrets and authentication

- Production secrets come from environment variables or a platform secret store. Local secret files are ignored and must use restrictive filesystem permissions.
- Account passwords are stored only as salted, memory-hard password hashes using a reviewed platform/library implementation. The concrete algorithm and parameters require an ADR before persistence work.
- Any legacy client-side password transformation is transport compatibility, not password storage security.
- Realm handoff tokens are random, short-lived, single-use, scoped, compared in constant time where applicable, and never logged.
- Authentication responses avoid account-enumeration differences where protocol constraints permit.

## Administration

- Administrative commands use a separate authenticated control surface, not player chat parsing.
- Authorization is deny-by-default and checked per action against explicit roles/capabilities.
- High-impact actions produce tamper-evident audit events containing actor, target, action, result, and correlation ID—but no secrets.
- No source-code usernames, implicit developer privileges, hidden commands, or remote shutdown/evaluation hooks are permitted.

## Database safety

- Persistence is accessed through Application ports and parameterized operations only.
- Database accounts receive minimum permissions. Schema migration credentials are separate from runtime credentials.
- Transactions protect multi-record invariants; optimistic concurrency or explicit locking is documented per aggregate.
- Backups, restore tests, retention, and encryption are required before production use.

## Legacy-source isolation

- Approved research repositories live only under ignored `research/isolated/` at pinned commits.
- Clone operations disable hooks; submodules are not initialized. No restore, build, script, archive extraction, binary loading, or dynamic execution is allowed.
- Static inventory covers executable/binary formats, archives, Git attributes/hooks, package sources, MSBuild imports/targets/tasks, pre/post-build events, source generators, native interop, network callbacks, credentials, unsafe admin commands, SQL construction, and suspicious process/file/network activity.
- Unlicensed evidence may inform independently derived facts such as numeric IDs and observed behavior, but its expression is never copied.
- Hashes and provenance are retained for audit reproducibility.

## AMP deployment boundary

- AMP/Hostinger is production infrastructure. Inspect before changing and never guess paths, users, units, ports, template schema, or controller behavior.
- Revora runs as the existing AMP instance user, never root. It receives only its instance directory, two approved loopback TCP endpoints, its protected configuration, and minimum database permissions.
- Non-secret settings live in `config/revora.json`. A separate `config/revora.secrets.json` is `0600`, excluded from Git/backups where secrets are managed separately, and never surfaced in AMP settings.
- AMP console input reaches only the allowlisted operator parser. It cannot invoke a shell, arbitrary code, SQL, player chat, or GM commands.
- The stable READY/STOPPED markers are lifecycle assertions, not cosmetic text. Fatal startup returns non-zero and redacts exception details that could contain credentials.
- Automatic updates remain disabled until a trusted versioned release, SHA-256 verification, atomic replacement, preserved state, readiness verification, and rollback are implemented.
- Development binds to `127.0.0.1`; AMP port reservation does not authorize firewall exposure.
- The host's existing AMP account membership and firewall posture are inherited infrastructure facts, not privileges Revora may use or expand.
- Hostinger requires AMP-managed containers for new instances. Revora permits only AMP's existing rootless container mode: the host-side process remains owned by `amp`, the container is unprivileged, host networking and broad host mounts are forbidden, and no extra Linux capabilities may be added. Revora never invokes Podman or Docker itself.
- Release custody is separate from runtime ownership: reviewed archives are `root:amp` mode `0640` in a `0750` staging directory, while Revora still executes only as `amp`. Extracted application files are non-writable during normal operation; instance config/data/log directories remain narrowly writable by `amp`.
- Deployment archives are rejected unless every path is relative and traversal-free, every entry is an expected regular file, links and setuid/setgid are absent, and forbidden development/secret material is absent. Extraction is tested only into a newly created isolated directory before any live use.
