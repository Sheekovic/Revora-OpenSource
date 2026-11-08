# Chi Token cash redemption

Date: 2026-09-16. M6.60. Server implementation; not deployed.

Seven daily-quest Chi Tokens (729304) now redeem across carried items. The old
handler required a single stack of seven, reduced its cached quantity, paid
currencies separately, then removed the item through the legacy inventory path.
Quest services issue separate saved tokens, so an otherwise complete ordinary
quest reward chain could not use its seven gifts directly.

The existing Revora economy is preserved: `Config.ChiTokenCashCp` (default 1,500)
and `Config.ChiTokenCashRevoraPoints` (default 5) for seven tokens. This is cash,
not a claim of original CO Chi-point parity. Both bound and unbound tokens retain
the prior cash policy. A mixed bundle is allowed; its source bindings are saved
in the receipt. No new quest or achievement is awarded by redemption.

## Complete exchange

Ordinary item-use action 4 selects the clicked token first, then other usable
carried tokens in UID order until exactly seven units are available. A legacy
zero-stack singleton counts as one and is deleted with its exact saved zero
preimage. Partial stacks retain their surplus. Locked, suspicious, warehoused,
wardrobe, equipped and expired items cannot be consumed. The selected source
must be the actual item owned by the canonical healthy ordinary session.

The character's inventory lock and saved character row serialize the exchange.
The saved bag UID set, every selected source's ownership/metadata/quantity and
both expected currency balances must agree with the loaded owner. One
transaction debits all seven units, credits CP and RVP, and inserts a receipt
containing the seven source-unit UIDs, prior stack sizes and bindings. A missing
write rolls everything back and fences further mutation until reconnect.

The existing item-use frame is bounded before use (32..128 total bytes, exact
body/trailer, packet ID/action/seal). Direct item dispatch shares the same
service. Successful cache updates and currency notifications follow commit.
One-second session debounce protects duplicate concurrent requests; deleted
source UIDs cannot be replayed after reload. A remaining stack can be redeemed
again intentionally when another seven units remain.

## Validation

`tests/PotencyHunt/Verify-PotencyHunt.ps1 -ChiTokensOnly` exercises 601 checks,
including the 487 Release Souls/prerequisite checks. Ordinary fixtures cover
singletons, zero stacks, exact/partial/repeated stacks, mixed binding, full bags,
concurrent use with autosave, insufficient/ineligible sources, stale source/bag
and currency images, nullable balances, overflow and malformed frames.
Suppressed first/late deletion, partial-stack update, currency credit and receipt
writes preserve every token and both currencies, followed by full production
reload and successful retry.

The earned journey completes seven separate Desert Guardian daily routes using
ordinary walking, inspection, NPC continuation and production reload. Its seven
actual bound token gifts redeem for exactly one cash payout; the seven earned
EXP Balls remain. No synthetic token is inserted into that journey.

The related quest-gift suite passes another 490 checks through the shared item
packet/dispatch path: 1,091 checks across both suites (shared prerequisites are
included), plus three producer-audit tests. Ignored logs are
`obj/chi-refill-validation/chi-token-cash.log` and
`obj/chi-refill-validation/chi-cash-gift-regression.log`.
The isolated build passes. Staged server SHA-256:
`214E218C6F5D764B3B98326970FB5464FDE07F6F9F5865464FDD319215864E73`.
Final harness SHA-256:
`3CD2F8E79CB058E79301F6000BE4A45A9AA569C1A01943AB1A9521CD314DE0DF`.
The producer audit stays at 222/337; cash redemption adds no achievement.

## Activation and remaining gates

Initialization creates `revora_chi_token_cash` idempotently alongside the other
item services. No live migration, server restart, patch or website publication
is included. Preserve currency, remaining items and redemption receipts during
rollback; restoring the former handler reintroduces its unsafe exchange behavior.
Genuine 6711 item interaction, tooltip/economy presentation and immediate CP/RVP
visual acceptance remain pending. Original CO reward parity is separate work.
