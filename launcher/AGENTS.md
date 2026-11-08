# PlayRevora Launcher instructions

These instructions apply to the entire repository.

## Canonical owner workflow

Read and follow `docs/OWNER_WORKSTYLE.md`. It defines the default execution,
owner-interaction, and reporting model for this project. Resolve older workflow
wording in favor of that document while preserving the safeguards below.

## Repository boundaries

- Preserve unrelated working-tree changes and commit only task-related files.
- Make permanent fixes in source and validate the packaged/runtime path; do not
  patch an installed client as the source of truth.
- Keep the launcher focused on updating, integrity verification and repair,
  client diagnostics, graphics/profile selection, and starting the game.
- Treat private client artifacts, signing keys, credentials, diagnostics, and
  research evidence according to their existing licensing and security rules.
- Never weaken manifest signatures, TLS, authentication, hash gates, or client
  integrity checks to obtain a passing result.
- Do not publish private packages or proprietary binaries, perform destructive
  operations, use owner credentials, or broaden a private test into a public
  deployment without explicit authorization.
