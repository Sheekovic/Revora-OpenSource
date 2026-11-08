# Revora Expeditions — implementation status and delivery path

## Status

**Not connected, deployed or client-confirmed.** The requested extraction event
is not playable. This document implements the planning requirement from
`Features/REVORA_EXPEDITIONS_VISION_AND_REQUIREMENTS.md`; it is not a completion claim.

Current bounded increment: protocol-independent fixed UTC cycle calculation and
single-cycle registration ownership, reusing `EventAggregate`. No Host worker,
NPC option, account restriction, migration, temporary-loot item or live event was
added. Synthetic tests are distinct from PostgreSQL and genuine-client tests.

## Fixed requirements and staged policy choices

| Rule | Decision |
|---|---|
| Calendar | 00:00,03:00,06:00,09:00,12:00,15:00,18:00,21:00 UTC, independent of restarts |
| Registration | Exactly [start−5 minutes,start); no late entry |
| Duration/extraction | Event ends at start+20 minutes; extraction opens at start+5 |
| Participation | Solo, one participation per character per cycle; no GM/owner requirement |
| Capacity | Staged pilot16, not a production balancing conclusion |
| Seat policy | First validated request serialized by the authoritative owner wins; ties use that serialization order |
| Minimum/no-shows | Staged minimum1 supports PvE; empty cycle cancels; reserved no-show seats are not reassigned after closing |
| Cancellation | Staged first version offers no pre-start seat cancellation; disclose before registration |
| Original property | Equipment ownership must remain protected; no hidden durability, potion or fee costs |

The registration primitive accepts owned ordinary characters individually; teams
are neither its authority nor its participant identity. Actual team-benefit
suppression belongs to the future instance/combat adapter. The primitive cannot
by itself guarantee database ownership across processes or restart persistence.

Proposed remaining pilot policies, **not implemented or enabled**:

- Standardized event-only combat-stat overlays and event supplies, never replace
  or overwrite original item records. No personal potion/durability loss or fee.
- Two or more reviewed exits;10-second stationary extraction, damage or movement
  cancels the countdown. Exact placement awaits a validated map review.
- Temporary loot only; on death half of eligible temporary stack units, rounded
  down per stack, may become participant loot and the remainder expires. Reward
  tables and economic values require balancing against inexpensive Master power.
- Resolve authoritative lethal damage before extraction at the same logical
  tick; deadline wins at exactly start+20. Only extraction completed strictly
  before the deadline may settle. One durable terminal outcome.
- Individual disconnect leaves the event actor vulnerable for30seconds; reconnect
  resumes that same participation, never a new life. A server-wide interrupted
  cycle cancels, restores original-world position/property and grants no free
  extraction. These policies require implementation and published UI text first.
- Per-character entry is enforced; shared IP is not proof of abuse. Additional
  multi-account/feeding controls must use documented behavior evidence.

## Existing building blocks, not an existing expedition

- `src/Revora.Gameplay/Services/AdvancedGameplayServices.cs`: `EventAggregate`
  already provides ownership, participant capacity, command replay and start.
  It is not thread-safe, has no five-minute lower bound, and exposes ranking
  semantics unsuitable for extraction settlement. The new wrapper supplies
  locked registration/window ownership without copying that aggregate.
- `db/migrations/V027__events_arena_and_rankings.sql`: event definitions,
  instances and participant primary key exist. No reviewed expedition definition,
  restart-safe admission implementation or loot-entitlement schema exists.
- `Legacy6652MapTransitionService` and current6711 adapters provide authoritative
  collision/map transfers and persistence. Expedition isolation, safe spawning,
  instance-only access and blocked external travel still need integration.
- `MonsterRuntime6711` and durable combat/reward services provide ordinary PvE.
  Solo opt-in PvP, event crime isolation, temporary reward origin and fair combat
  overlays are not completed expedition features.
- Atomic inventory, ground pickup/drop, shops, warehouses, trading and vending
  are ordinary-property flows. They must **not** receive temporary expedition
  items as normal owned inventory. Their existing transactions are reusable
  implementation patterns, not proof that loot-smuggling protection exists.
- Confirmed NPC text/options, private messages and item/status codecs can explain
  rules and results. No bespoke client window or unconfirmed packet is required
  merely to begin; exact timer/temporary-loot presentation remains to be tested.

## Phases and gates

1. **Calendar/registration foundation (this staged increment).** Deterministic
   cycle identity, exact UTC boundaries, bounded16 seats, synchronized final-seat
   allocation, duplicate-character handling and frozen roster. Integrate durable
   V027-backed admission under database locks before exposing registration.
2. **Durable lifecycle.** One cycle key/character participation, restart recovery,
   no-shows, start/finalize/death/extraction terminal receipts. Coordinate any new
   migration centrally; do not repurpose ordinary ranking score as loot ownership.
3. **Isolated playable map.** Select proven map/collision data, fair spawn/exit
   distribution, solo combat overlay and allowed skills. Block external portals,
   warehouse/trade/vending and cross-instance assistance while participating.
4. **Risk loot and settlement.** Dedicated temporary-origin ledger, atomic carrier
   transfer, deterministic death/drop resolution, idempotent extraction and
   durable pending delivery for full inventories. Ordinary gear never enters it.
5. **Complete pilot.** NPC registration→safe entry→PvE/PvP→temporary loot→death or
   extraction→ordinary-world return. Add private countdown/outcome messages, then
   run ordinary multi-client and genuine6711 presentation tests.
6. **Controlled release.** Backups, migration validation, startup failure policy,
   bounded maintenance and metrics. Enable only after the complete loop passes;
   registration alone must not be advertised as a live expedition.

## Acceptance evidence required

- Current synthetic tests: exact opening/closing/extraction/deadline endpoints,
  UTC-offset equivalence, eight deterministic daily identities, capacity race,
  duplicate and forged ownership, immutable roster, no late/second registration,
  empty cancellation, missed-cycle expiry and clock rollback protection.
- Future disposable PostgreSQL tests: concurrent final-seat winner across two
  owners, restart recovery, unique participation, settlement replay/conflict,
  carrier-change races, full-inventory entitlement retention and rollback.
- Future gameplay tests: original gear byte-for-byte/owner/version preservation,
  every temporary-loot escape route rejected, simultaneous death/deadline/extract
  ordering, event-specific crime rules without clearing prior Wanted state,
  disconnect/reconnect without extraction, no leaked next-cycle actors/items.
- Future genuine-client tests: clear rules/countdowns, correct map/AOI, distinct
  temporary loot, interrupted extraction, one successful settlement and one
  death result using ordinary non-GM accounts. Server tests do not satisfy this.

Diagnostics should contain cycle/participation/operation IDs, transition reason
and counts only; do not log chat bodies, credentials or secret packet contents.
Before production, publish chosen balancing policies in natural player-facing
language and document actual client results separately from this roadmap.
