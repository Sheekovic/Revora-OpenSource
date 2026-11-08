# ADR: Public registration and no-email recovery

## Status

Accepted for `feature/public-registration`.

## Decision

Successful public registration activates a Player account immediately. Email is neither required nor synthesized, because Revora has no reviewed delivery dependency and must not create fake identity data. The immutable UUID AccountId is authoritative across Web, Account, Game, recovery, and character ownership. Username and character name are mutable/presentational identifiers, never ownership keys.

Ten high-entropy one-use recovery codes are the primary recovery method. Optional slow-hashed questions are weaker supporting evidence and require a recognized device or familiar-network signal plus cooldown; uncertainty creates a sanitized Admin-reviewed request. IP or device evidence alone cannot prove ownership. Admin approval creates eligibility for a short-lived user-controlled reset and never discloses secrets or lets Admin choose the new password.

Patch 5165 restricts the current password policy to 8–15 printable ASCII characters so one password can be verified identically by Web and genuine packet 1086 without truncation. Launcher-only authentication must later provide a modern credential boundary, revocable launcher sessions, and an evidence-proven Account/Game bootstrap before this compatibility limit can be relaxed.

## Consequences

Durable sessions, security stamps, device/network digests, reset authorizations, recovery codes, and pending review state are additive tables around the existing account authority. Password replacement revokes dependent authentication artifacts but never changes AccountId, character UID, ownership, inventory, or world state.
