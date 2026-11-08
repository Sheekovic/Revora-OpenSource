# Exorcism research and reward transactions

Research baseline: `4114157`, 2026-09-16. Token exchanges and the two pack
openings now have atomic, ordinary-player reward paths. The complete hunt,
daily completion, team instance lifecycle and Soul Ball bonuses remain open.

## Evidence and edition differences

The [official Exorcism guide](https://co.99.com/guide/quest/2012/Exorcism.shtml)
describes a level-90 daily hunt, three difficulty tiers, three tokens for a
standard pack and nine for a Golden Pack. Standard soul rewards are P3–P4;
Golden soul rewards are P3–P6. Golden packs include 50 bound CPs and a 20%
chance of an additional Chi Token. Its third-tier completion gives three
tokens and 100 study points. The original guide's 27-CP direct completion
price differs from Revora's existing 5,000-CP configuration.

The [older Thrilling Spook guide](https://co.99.com/guide/monsters/thrilling_spook.shtml)
describes a level-80, once-daily hunt with a one-hour limit and team admission.
It requires signing up with Heavenly Master before Earth Master summons the
boss and describes removal from the burrow after leaving the team. Its
single-tier reward is 50 study points and one token. Its named attacks include
an area stun and stronger area damage. These older numbers are not silently
combined with the later three-tier edition.

The [Spirit Bead guide](https://co.99.com/guide/quest/spirit-bead.shtml)
lists Thrilling Spook bonuses of 600, 800 and 1,000 spirits. They require a
trusted encounter victory before they can be added to the daily Soul Ball
ledger. A matching monster name is insufficient evidence.

The owner's installed `6711/ini/itemtype.txt` independently identifies
`711211`, `720948` and `729594` as the token, standard pack and Golden Pack.
Its Golden Pack description agrees on 50 bound CPs and P3–P6 souls. Its NPC
directions point to Twin City, whereas the installed server places Heavenly
Master in the Market. This batch preserves the installed server's real NPC:
`12333`, map `1036`, `(243,151)`. No proprietary client files are committed.
The inspected item catalog SHA-256 is
`52889C537DDD00A6F4D349FA9243268DFEDC81AB3899523F1E434218735387CB`.

## What was broken

- The old exchange removed tokens before attempting to insert a pack, with
  no shared transaction or single-use confirmation.
- Golden packs gave one bound CP, had no extra Chi draw, and included five
  P1/P2 soul slots outside their advertised range.
- The standard pool contained only accessories, despite its P3/P4 promise.
- Exclusive random bounds made the final standard and Golden slots
  unreachable. Creating a new time-seeded random generator for each opening
  also made repeated draws unnecessarily correlated.
- Pack deletion, reward insertion and currency updates were independent.
  A full bag or failed write could lose the source or partially pay a reward.

## Implemented reward behavior

Heavenly Master's existing exchange dialogue uses the configured three/nine
token prices. It creates a two-minute, single-use offer. Confirmation requires
the current ordinary player, the real nearby NPC, the same terms, and no
trade or booth. A cancellation, expired offer, repeated continuation or
changed price cannot charge. There is no GM or account allowlist.

Exact token units are selected bound-first, including legacy zero-stack
singletons. Partial stacks retain their remaining quantity. An output pack is
bound if any consumed token was bound. Only permanent, unlocked, unsuspected
inventory items may be spent. A consumed stack can supply the output slot.

Opening one standard pack gives one random accessory or P3/P4 soul. Opening
one Golden Pack gives one random accessory or P3–P6 soul, 50 bound CPs, and
an independent 20% extra Chi Token draw. Binding carries into every item.
Catalog entries marked for seven days retain that lifetime and a persisted
start timestamp; permanent variants remain permanent.

The explicitly **Revora-specific** pools use uniform slots:

- Standard: 78 existing accessory slots plus eight P3/P4 soul entries, 86
  slots total. Each slot has probability `1/86`.
- Golden: 45 retained slots after removing the five low-grade soul slots.
  The existing duplicated Radiant Sword entry remains, so it has `2/45`
  probability; other unique entries have `1/45`.
- Extra Chi: an independent draw from 0 through 99, awarded for 0 through 19.

The official guides do not establish exact item weights. The current pools
award individual souls directly, not an invented claim of exact original
soul-pack/refinery/Justice Scroll parity. Native reward UI acceptance remains
pending. Random draws use an unbiased cryptographic index rather than a new
time-seeded generator per opening.

The owner inventory lock and durable owner-row lock protect each operation.
The complete bag identity and every input preimage are checked. Token/pack
debits, every gift, Golden bound CPs and the corresponding receipt commit
together. All writes require the expected affected row count. Capacity is
checked before randomness, reserving space for the possible extra Chi Token;
an insufficient bag cannot reroll away that prize. Overflow preserves the pack.
After commit, the existing inventory and bound-CP update packets refresh the
player's cache/client. Persistence failures fence the session for reconnect.

`revora_exorcism_exchanges` records selected token UIDs/counts and the issued
pack. `revora_exorcism_openings` records exact source UID/stack, reward UIDs and
types, bound-CP amount and time. Spent item identity prevents durable replay;
stack openings also have a one-second per-session debounce. A reconnect can
open remaining owned units but cannot restore spent units.

## Remaining encounter work

Read-only catalog and terrain inspection found material ownership problems:

- Existing entry builds dynamic copies of map `1013`. The installed client
  calls it **Tiger Cave**, not Black Burrow. Its inherited portals lead to
  Phoenix Castle/Dragon Pool unless an owned encounter clears them. The
  cached map is 104×104; `(36,24)`, `(25,30)` and `(52,49)` are walkable.
- NPC `8798`, imported by the old burrow path, is actually Refurbisher on map
  `1012` at `(70,70)`. Sending another mesh at another coordinate does not
  create a correctly owned Earth Master in the map dictionary.
- The installed Spook Shop is NPC `8308`, map `1019`, `(25,30)`. A private
  instance requires its own correctly placed NPC object.
- Installed monster family `4212` is Thrilling Spook, level 136, mesh 952.
  The old Earth Master instead constructs three RAM-only monsters without
  durable run, participant or victory identity.
- The old team entry loops through unrelated online players and assigns
  their team map; solo/team cleanup and jump-time fake NPC presentation also
  lack a single instance owner.
- Kill handling sets both legacy completion flags, while the claim guard
  rejects the resulting state. Direct completion still uses split legacy
  writes and the old reward amount. Those paths are not marked repaired here.

Next implementation sequence:

1. Durable per-player acceptance/victory/claim and per-run party, grade,
   deadline and actor identity; conservative migration of legacy flags.
2. One owned instance lifecycle with correctly cloned NPCs, controlled
   portals, admission, team-leave rules, expiry and restart/reconnect recovery.
3. Whole daily reward transaction and source-authenticated Soul Ball credit
   of 600/800/1,000 on eligible registered victories.
4. Validate boss combat/HUD and original terrain/edition differences with the
   genuine 6711 client; retain explicit custom-terrain wording if needed.

Legacy packet ID `2400` dispatch alone is not proof of a native layout or
client entry flow. This batch disables that unconfirmed entry dispatch globally. The encounter
rewrite must use the existing NPC service path until native entry evidence is
established. The old private-hunt entry is unavailable in this isolated build;
pack exchange/opening remain ordinary services. No owner-only exception exists.

Private map evidence: installed client `GameMap.dat` SHA-256
`86A54928A9682617371542CDB5A72707C799B92A5C820E3581183ADAA94F32B0`;
cached server map `1013` SHA-256
`E689BAA83C58952C16352A10E4C5DF2D2E8BC86FFC3BE728C4F74574183B4868`.
Map identity is supported by local client metadata; it is not a claim that
this terrain matches the original Black Burrow. Catalog inspection used the
local, read-only x64 helper `obj/chi-refill-validation/bin/InspectExorcism.exe`,
SHA-256 `058EB528C4236FA2A797ED2EC16D8BD889CE0B7833DA5CB9EA2156CE225D5E32`,
with protected local settings and read-only SQL. No credentials were emitted.

## Validation and publication

Validation results are recorded with the final isolated build below. Tests
use ordinary non-GM fixtures and production item/dialogue/persistence handlers.
They supply tokens and packs directly; they do not prove that the still-open
Exorcism hunt can earn them correctly. No host restart, live migration, client
patch, production deployment or native UI acceptance is included.

The isolated build passed, followed by these suites (shared prerequisites are
included in each suite's count):

| Suite | Checks |
| --- | ---: |
| Exorcism rewards, including shared rebirth prerequisites | 752 |
| Soul Ball regression | 559 |
| Elite prize pack regression | 498 |
| Price Hunt and dialogue regression | 511 |
| Quest packets | 9 |
| Gameplay total | 2,329 |
| Producer audit tests | 3 |

The final Exorcism suite covers all 86 standard and 45 Golden slots through
production reload, both binding states across the pools, four Chi probability
boundaries, real NPC exchange continuations, cancellation and malformed frames.
Fault injection suppresses debit, stack, first/second gift, wallet and receipt
writes, then reloads and retries. Additional cases cover full bags, stale
purses, overflow, zero stacks, exact inventory preimages, concurrency and the
globally disabled 2400 path for both player and administrator fixtures.

Soul Ball and Elite prize regressions ran before the final scoped packet guard;
the 752 Exorcism, 511 Price Hunt and nine quest-packet checks then passed on the
final build. Final harness SHA-256:
`E8AF722CD246175FAB94085B0C958A6F23670EDBE487460DA2122766EB9CA89A`.
Ignored logs: `obj/chi-refill-validation/exorcism-*.log`.
The achievement audit remains 219 producers present and 118 absent; this reward
repair does not fabricate an achievement producer or complete the hunt.

## Follow-up: owned hunt

The [owned daily hunt](EXORCISM_HUNT_TRANSACTIONS.md) now implements the server
entry, team lifecycle, victory/claim and Spook spirit steps that were pending in
this batch. The validation above remains the historical exchange/pack baseline;
original combat/terrain parity and genuine-client acceptance remain open.
