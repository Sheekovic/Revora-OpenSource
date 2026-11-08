# Revora Living World: 6711 implementation plan

Status: implementation design, not an enabled feature. This plan translates the complete [initial concept](../../Features/REVORA_LIVING_WORLD_INITIAL_CONCEPT.md) into the current server architecture. Existing NPCs, monsters and teams are reusable foundations; none constitutes a persistent adventurer or companion implementation.

## Current evidence and gaps

| Area | Existing source | What still has to be built |
|---|---|---|
| Human visibility and movement | `Legacy6652AoiRegistry`, isolated `Legacy6711WalkPackets`, Host authoritative movement callbacks | Separate non-account actor ownership; independently confirmed adventurer presentation and removal |
| Collision and map identity | `Legacy6711DocumentCollisionProfile`, map publication profile, `IOwnerMonsterCollisionMap` | Reviewed routine paths, portal/service clearance, stuck recovery and continuity across maps |
| AI lifecycle | `OwnerMonsterRuntime` and `MonsterRuntime6711` | Adventurer intent, rest/travel routines, population budgets; do not treat a monster template as a complete adventurer |
| Actor retirement | `ActorUidLeasePool6711`, bounded observer publication/retirement | Extend one coordinated identity authority with a proven adventurer namespace; booth IDs are not automatically suitable |
| Player interactions | NPC 2031/2032 menus, one-shot confirmations, transaction receipts | Authored greetings, bounded relationship facts and encounter ownership |
| Combat | Monster PvE pipeline, selected confirmed spell/ranged adapters, durable reward paths | Companion attribution, assistance policy, resource ownership and anti-farming controls |
| Guard integration | `CityGuardRules`, `PostgreSqlCityGuardStore` | Actual guard orchestration remains separate; do not grant adventurers police powers |

These are source-level dependencies, not a declaration that every related feature is deployed or genuine-client confirmed. Inspect the release manifest and enabled gates when implementing each increment. The recovered NPC visibility work also does not implement their quests or services.

## Chosen architecture

Use a dedicated server-owned `Adventurer` aggregate, not fabricated authentication sessions or human accounts. Store a stable internal identity, presentation profile, home region, routine checkpoint, bounded relationship facts and version. Wire identity is leased from a reviewed namespace through the shared actor ownership boundary. Keep actor counts separate from authenticated human counts in all operator and player-facing metrics.

Implement a dependency-free decision policy and a gameplay-owned, cancellation-aware scheduler. A scheduler step produces semantic intentions such as travel, wait, greet or retire; persistence commits important transitions before the Host adapter publishes them. Protocol encoding stays inside `Revora.Protocol6711`. The Host only composes services and translates committed effects into bounded session deliveries.

The first representation to investigate is an explicitly labeled, player-shaped world actor using existing appearance evidence. This is a candidate, not a proven protocol contract: confirm all actor fields, update/removal handling, targeting behavior and label presentation before enabling it. Do not reuse a human identity to make the client accept it. If this representation fails, a clearly labeled NPC representation may support a narrower ambient milestone, but moving player-like adventurers must not be claimed complete.

Use authored dialogue and deterministic behavior. No model service, private-message collection or bot login client is needed. Persist relationship facts only for a defined purpose, with limits and expiry. Economic receipts and reward eligibility remain separate from relationship memory.

## Phases and delivery gates

### 1. Identity and harmless city routines

Start in published map 1002, with a reviewed route outside service, portal and stall footprints. Add a hunting-region route only after its exact path and resource-clearance tests pass. Do not select an arbitrary hunting map merely because its ID is supported.

Proposed initial operator policy: at most two ambient adventurers in the trial region; one deterministic decision per actor per second; no rewards, loot pickup, player trade, market orders or attacks. These are Revora operating proposals, not client or reference facts. Measure them in the disposable/server-client test before choosing production limits.

Deliver persistent identity, clear labeling, a travel/rest/greet routine, document-grid collision, occupancy checks, bounded path search, explicit stuck/retire state, per-region disablement and secret-free transition diagnostics. A nearby player must be able to observe the same actor complete and repeat its routine after restart. Failed delivery quarantines its wire identity until safe retirement.

### 2. Density and optional encounters

Add human-priority regional density with hysteresis and gradual arrivals/departures. Never despawn an actor mid-interaction solely because a threshold changes. First encounters should be non-economic: a traveler asks for assistance, completes or expires deterministically, and remembers the encounter without granting currency or items.

Before reward-bearing encounters, specify budgets, unique encounter IDs, eligibility, repeat limits, cooperating-account abuse handling, disconnect settlement and atomic receipts. Verify zero reward on failure and single settlement under concurrent requests. Do not imply such rewards already exist.

### 3. Limited PvE companionship

Require explicit owner commands and define inactivity, map transfer, death, logout and retirement behavior first. Follow/Wait/Retreat can precede combat. Protect/Heal require the existing authoritative targeting, collision, cooldown and resource paths; do not introduce an independent damage formula.

Before enabling damage, decide and test kill credit, assist attribution, EXP, proficiency, loot rights and reward ceilings. No autonomous offline progression, player-market access, resource transfer or rare-boss competition. Recruitment price and duration remain open product choices; no placeholder fee is authorized by this plan.

### 4. Guard reactions and practice

Civilian reactions may subscribe to actual danger events without changing Wanted state. Enforcement still belongs to the guard subsystem. Practice opponents stay explicitly labeled, outside human rankings and reward-bearing competitive events. Expeditions remains human-only, including its companion exclusion.

## Acceptance and diagnostics

For each phase record designed, implemented, integrated, deployed and genuine-client-observed status separately. A moving synthetic actor does not satisfy the full feature.

Required deterministic tests cover identity collisions, disconnected observers, ordered spawn/move/remove, no duplicate actor after restart, blocked paths, disappearing routes, bounded retries, density oscillation, human priority and feature disablement. Ordinary players must have the same access regardless of account name or GM role. Simulated activity must never alter human online counts or player balances.

Use disposable PostgreSQL tests for version conflicts and restart recovery. Later reward tests must include duplicate completion, cancellation after commit, rollback, concurrent participants and offline-owner rejection. Test server-client appearance and behavior separately from domain/serialization assertions.

Measure CPU time per scheduler step, path-search work, active/retiring actors, queue depth, observer bytes and failed deliveries. Log state changes and bounded failure reasons with actor/encounter correlation; no per-tick flood or private conversation bodies. Before expansion, publish measured limits and a rollback procedure. Shutdown stops admission, settles interactions, persists checkpoints, retires visible actors and releases identities only after delivery/transport retirement.

## Next concrete implementation

First prove the labeled actor presentation and reserve its identity ownership boundary; in parallel build the no-reward routine state machine and route-clearance fixtures. Neither needs companion rewards or a complete City Guard runtime. Existing NPC transport completion can continue independently and improve the navigable world, but it is not Living World actor progress.

### Separate next-batch travel dependency: Conductress 9439

The approved reference `References/Slayer3D-6711/Mr_Rayzo/Npcs.cs:18819` defines two choices for the recovered NPC on map 3055 at `(24,90)`: option 1 travels to map 1000 `(493,648)`; option 2 travels to Market 1036 `(291,236)`. Both deduct 100 silver but require a balance strictly greater than 100, meaning at least 101. Preserve that distinction rather than silently changing the compatibility rule.

This route is **not implemented by this document**. Prerequisites are an explicit minimum-silver-balance field and transactional validation, the same narrow source/hash-bound recovered-NPC validation used by visibility (the current transport transaction still demands historical `closure_state='valid'`), destination document-grid checks, and ordinary-player payment/replay/reconnect tests. Market entry must retain the existing durable return-anchor policy. Do not change legacy import rows or substitute another destination. No new database, build, client test or route activation was performed for this planning batch.
