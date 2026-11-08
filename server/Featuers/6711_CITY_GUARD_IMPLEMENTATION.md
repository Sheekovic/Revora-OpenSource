# City Guard: realistic 6711 implementation status

Updated 2026-09-08 UTC. Requirement: [City Guard vision](../../Features/REVORA_CITY_GUARD_VISION_AND_REQUIREMENTS.md).
Audience is every eligible ordinary player. No owner-name or GM exceptions.

## What exists, and what does not

| Boundary | Current evidence-backed state |
|---|---|
| Rules | `CityGuardRules` implements PK bands, criminal-kill exclusions, heat decay, bounty eligibility, tribute calculation and target filtering. These are foundation rules, not a running pursuit. |
| Persistence | V041/V046 and `PostgreSqlCityGuardStore` provide tables, serializable bounty settlement and status redemption. Persistent crime ingestion, pursuit coordination and Arena recovery are not connected to the 6711 Host. |
| Representation | Existing 6711 player/NPC/monster codecs are reusable evidence, but there is no integrated equipped human-guard actor or allocated Commander wire UID. Never create a human-operated account to stand in for the guard. |
| Commander | V041 contains disabled UUID `51650041-0000-4000-8000-000000000002` at map1002 `(455,378)`. This is the existing definition to reuse. Historical collision validation is not current 6711 occupancy confirmation. |
| Live feature | No current Host call sites connect `CityGuardRules` or `PostgreSqlCityGuardStore`. V041 seeds `guards.*` disabled; current deployed gate values were not queried in this review. No pursuit, Arena, Honor earning or redemption is claimed live or client-confirmed. |

Existing live monster movement, world ownership locks, confirmed actor effects,
atomic combat rewards, collision-backed transitions and NPC dialogs are useful
dependencies. They do not themselves fulfill guard behavior or authorize PvP.

## Small prerequisite completed in this increment

Bounty evaluation previously trusted `SameAccount=false` even when the two
actual account IDs were equal. It now rejects identical account identities
independently of that hint. Empty cycle/account IDs, negative PK/daily totals,
and damage shares outside `[0,1]` fail closed with zero reward. Valid distinct
accounts retain existing thresholds, caps and contribution rules. This changes
only the staged domain boundary; it does not enable payouts or replace durable
ownership/eligibility validation at settlement.

`BountyIdentityTests` adds 23 deterministic assertions, registered alongside the
12 existing guard groups. Consolidated Release build passed with no warnings or
errors; targeted CityGuard suite passed13 groups, including23 new assertions.
No new database, worktree or deployment was needed for this staged prerequisite.

## Policy and conflicts to resolve before connection

Retain existing Revora PK bands: white0–29, red30–99, black100+. Blue is an
independent state; blue alone gives one five-second warning. Red/black or
qualifying PK overrides warning. An actual attack on an attackable guard goes
directly to one Arena incident. The nonattackable Commander remains a service.

Use a shared incident deadline of five minutes, not a timer per guard. Initial
Revora policy: at most two guards per offender, maximum acquisition within the
existing18-tile view, 60 seconds hidden to escape and 30 seconds of reacquisition
grace after escape. A distinct authenticated new crime may end grace; replay
cannot. These are proposed implementation defaults, not active runtime behavior.

Keep crime history, PK, bounty entitlement and pursuit lifetime separate.
Existing heat decay must not erase persistent crime/black eligibility. Existing
`IsLegalGuardTarget` does not implement blue-warning precedence. Existing
`FallbackRelease` grants exit after three defeats/ten minutes, conflicting with
the current victory-or-ransom requirement: do not wire that legacy fallback.
Failure recovery must restore an encounter or a safe recoverable checkpoint,
not invent a third player-selected escape or clear Wanted status.

Initial economy can retain the staged1–5 Honor bounty bands, daily10 cap,
20% player contribution and24-hour same-account-pair cooldown. Shared IP alone
never disqualifies a player. Keep rewards disabled until cycle eligibility,
actual contributors and anti-arranged-kill evidence are durable. Retain the
existing disclosed10,000–1,000,000 silver ransom calculation provisionally;
quote and confirm before an atomic debit-and-release. No hidden fines/items lost.

## Delivery sequence and acceptance

1. **Crime and pursuit ownership.** Add immutable crime receipts from authorized
   damage attribution, excluding defense and sanctioned combat. Own pursuit by
   incident across guards/maps, bounded navigation and return-to-duty. Test
   outside-city crime, blue/red/black precedence, new versus replayed crimes,
   five-minute guard handoff, hiding, dead/disconnected targets and isolation.
2. **Visible guard and Commander.** Reuse the existing Commander definition after
   current collision/NPC/portal occupancy checks. Select reviewed equipment and
   prove an independently owned human-like actor identity with two observers.
   Add ordinary explanations and status before enabling attacks. Validate
   spawn/removal, appearance, walls/range, return path and failed-recipient cleanup.
3. **Atomic Arena encounter.** One active incident/character, persistent instance
   and safe return anchor, validated guard damage and no loot/Honor farming.
   Victory and quoted ransom resolve exactly once. Disposable tests must cover
   simultaneous guard hits, stale confirmations, insufficient silver, rollback,
   death, disconnect, restart and failure after commit but before client updates.
4. **Honor and redemption.** Derive one winner from durable damage/cycle records,
   enforce account ownership and caps inside settlement, then publish updates.
   Reuse Commander confirmation menus and reviewed rewards. Test duplicate claims,
   cooperating accounts, same-IP unrelated players, full inventory, concurrent
   redemptions and reconnect persistence before any earning/redemption gate opens.

Ordinary-account final exercise: peaceful passage; blue warning; red detection;
black detection after travel; lawful defense; bounded escape; guard assault;
Arena victory and ransom; reconnect/restart; contested bounty; duplicate reward
confirmation. Require server tests and genuine-client two-observer confirmation
separately. A visible guard or an unconnected service is not feature completion.

Diagnostics should record correlation/incident IDs, transition reason, map and
result only, at state changes with bounded rates. No secret bodies or per-tick
spam; telemetry failures must not affect gameplay. Deployment remains a separate
coordinated backup/build/test/drain/restart milestone. Current classification:
**FOUNDATION_STAGED — not connected, not demonstrated live on 6711.**
