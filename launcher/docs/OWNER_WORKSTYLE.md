# Sheeko owner workflow

This document is the canonical owner-working-style policy for this repository.
Apply it together with the root `AGENTS.md`; preserve stricter security,
licensing, integrity, and publication safeguards.

## Default execution model

1. Work end-to-end: diagnose, fix source, build, test, package, install or
   deploy when that is already within the requested task, and verify.
2. A request to implement or fix a feature authorizes all safe, routine,
   reversible work required to complete it. It does not authorize public
   release, destructive data loss, credential use, or unrelated
   infrastructure changes.
3. Do not stop for routine intermediate approvals, including ordinary source
   edits, builds, tests, packaging, copying reviewed artifacts, restarting a
   directly affected test service, routine rollback, or fixing follow-on
   implementation defects.
4. When a routine blocker appears, diagnose it, make a permanent source fix,
   rerun validation, and continue toward the original goal. Do not rely on
   one-off edits to installed or deployed artifacts.
5. Preserve unrelated working-tree changes and commit only task-related files.
6. Prefer one complete correction and one meaningful owner canary over a chain
   of partial canaries or approval checkpoints. Never ask the owner to repeat
   a failed action until its exact defect is identified and corrected.
7. Reuse verified evidence. Revalidate affected files and critical boundaries;
   avoid repeated full-tree scans unless risk or changed evidence requires one.
8. Prioritize a playable end-to-end result. Do not spend repeated cycles
   improving diagnostics when a simpler secure source-level correction can
   achieve the requested outcome.
9. Temporary owner canaries may be account-restricted, but final functionality
   must support all legitimate accounts and must not remain restricted to
   `SHEEKOVIC` or another test identity.

## Launcher product boundary

The launcher is an updater, client doctor and integrity-repair tool,
graphics/profile selector, and game starter. Do not add unnecessary credential
entry, authentication steps, or complicated user workflows.

## Owner interaction and reporting

- Ask for owner input only when Codex genuinely cannot perform the action, such
  as entering private credentials, confirming an in-game visual result,
  choosing between meaningful product/UX alternatives, authorizing destructive
  or irreversible work, authorizing public/security-sensitive scope expansion,
  or providing unavailable authority or infrastructure.
- When an owner test is finally required, give one short instruction, wait for
  the result, and then continue fixing autonomously.
- Keep reports concise: root cause, permanent fix, test/result, and the exact
  owner action only when genuinely required.
- Do not repeatedly list unchanged hashes, scans, backups, service states, or
  test matrices. Report them when they changed, failed, or materially affect a
  decision.
- Use a blocker response only when progress truly requires owner-only
  information, unavailable authority, or a meaningful product decision.

## Safeguards that always remain in force

- Never expose, log, or persist credentials, secrets, reusable tokens, private
  keys, or credential-equivalent material.
- Never weaken signature, TLS, authentication, authorization, replay, or
  integrity checks merely to pass a test.
- Never publish private research or proprietary binaries.
- Never perform destructive or irreversible actions without explicit owner
  authorization.
- Never deploy publicly when only a private owner test was requested.

## Persistent preference text

`Sheeko owner workflow`: Sheeko prefers autonomous, source-first, end-to-end
execution with concise reporting. Avoid approval chains, repeated scans,
repeated canaries, and stopping at routine defects. Fix follow-on issues
autonomously and request user action only for credentials, visual confirmation,
meaningful product decisions, destructive/public actions, or unavailable
authority.
