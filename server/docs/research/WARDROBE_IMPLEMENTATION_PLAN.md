# Wardrobe quest and event implementation plan

Status: proposed work, not deployed. Baseline: `23c520b`, 14 September 2026.

Read the [research report](WARDROBE_QUEST_EVENT_RESEARCH.md) for evidence, historical rule conflicts and sources. The [reward matrix](WARDROBE_REWARD_MATRIX.csv) covers all 72 entries. Phase IDs below are the same IDs used in that matrix.

**Recommended first release:** authoritative completion/award foundations, working local CTF and Elite PK, and the first verified classic quest routes. A second content release should introduce the five summer wings through a documented crafting season. Do not wait for cross-server infrastructure to improve local progression.

## 1. Sequencing and measurable outcomes

| Phase | Deliverable | Dependencies | Reward effect | Completion gate |
| --- | --- | --- | --- | --- |
| P0 | Achievement authority, durable award grants, migration and catalog validation | Existing source/client evidence | Makes existing and future eligibility trustworthy; no speculative unlocks | Forged claims rejected; ordinary-player grants survive relog/restart; old evidence preserved |
| P1 | Restore local CTF and Elite PK | P0; client-compatible event packet audit | Activates earning routes for two already-implemented checks: Victor and Infernal Beast wings | Full fresh tournaments completed by ordinary players; results and expiry persist |
| P2 | Classic quests and achievement completion bridges | P0; each quest's rule contract | Expands legitimate Rising Star progress and available activities | Verified terminal transactions; published per-character reachability, not just catalog count |
| P3 | Horn of Summer material quests and wing crafting | P0; fragment/item mapping; seasonal tuning | Five currently locked summer wings | Recipes, binding, delivery, duplicate handling and caps pass real-client checks |
| P4 | Seasonal Guild War and Holy Grail | P0/P1; Cup/Grail item and edition research | Two currently locked rewards | Legitimate season-to-cup-to-Grail path; atomic crafting and both grants |
| P5 | Homecoming and historical social campaigns | P0; exact campaign contracts; P6 for genuine all-server stages | Up to seventeen locked entries across five families; conditional on evidence | Return eligibility, voting/contributions, ranks, category mapping and durations verified |
| P6 | Cross-server CTF, Elite PK and Team PK | P0/P1; distinct origin realms; coordinator | Ten currently locked competition rewards | Independent origins compete; authenticated finalized results return exactly once |
| P7 | Poker, Fashion, Planet and Honored Hero follow-up | P0; unresolved rules researched | Nine remaining entries, some still unknown | Source-specific rules established before a grant is enabled |

P5's seventeen entries are Homecoming (4), 2016 Mr./Ms. (7), 2017 Mr./Ms. (4), Romance (1), and Heart (1). P7's nine are Poker (4), Fashion (3), Planet (1), and Honored Hero (1). These counts account for all 43 locked entries without double-counting the two P1 rewards. They are planning coverage, not a promise to grant every historical reward under invented rules.

P2 and P3 can follow P0 independently of P4–P7. Dis City remains a conditional P2 target until the four achievement meanings are resolved. If that research takes longer, ship the confirmed quest routes first.

## 2. P0: make ownership and completion durable

### Catalog and source contracts

Create a server-owned catalog of factual achievement IDs and predicates from the installed 6711 version. Each entry needs a completion source, class/rebirth prerequisites, repeatability, and reachability classification. Do not copy client presentation text into server source. Undefined bits and unknown achievement IDs never contribute to the total.

Classify wardrobe rewards explicitly:

| Ownership kind | Examples | Required behavior |
| --- | --- | --- |
| Current condition | Prestige, Chi, Jiang Hu, collections, Emperor | Re-evaluate current authoritative state; remove when requirement is lost. |
| Current result holder | Victor | Read last finalized event result and current eligible leader. |
| Temporary award | Elite PK champion; dated tournament titles | Read source grant and absolute expiry; enforce expiry even while offline. |
| Permanent earned unlock | Crafted summer wing, qualifying Grail grant if confirmed | Read durable completion/award evidence; relog cannot erase it. |

The final classification for a historical reward must follow its selected edition. Do not make every event reward permanent because the existing wardrobe blob has no expiry column.

### Proposed additive storage

Use the current PostgreSQL persistence layer; these are conceptual records, not a migration script:

| Record | Required fields and constraints |
| --- | --- |
| Event definition | Stable ID, frozen rules version, source edition, eligibility and reward contract |
| Event run | Run ID, origin/realm, definition version, opening/closing UTC instants, lifecycle state; one finalization |
| Participant | Run ID + character identity, eligible roster snapshot, admission state, join/leave result |
| Quest progress | Character + quest + daily/season scope; accepted state, objective counters, completion transaction |
| Achievement completion | Character + known achievement ID, verified source and completion time; unique completion |
| Event result | Run + placement/recipient; immutable finalized outcome and tie-break evidence |
| Award grant | Run/source transaction + recipient + reward slot as unique identity; reward key, expiry, source version and delivery state |
| Delivery receipt | Unique grant identity; committed reward payload and pending client notification |

Freeze rules for a running event. A later rules version must not allow the same reward slot to be claimed twice. Require event finalization before ranking grants become eligible. Keep returnee eligibility evidence separate from ordinary last-login updates.

Define whether rank rewards are exclusive or cumulative for the selected edition. A first-place result must not automatically grant every lower-rank title merely because it also satisfies a broad Top 8 predicate.

The existing title blob remains a compatible projection for client presentation and equipment state. `TitleRequirements.Eligible` must combine current-condition rules with legitimate persisted grants. `Reconcile` must derive the same answer after restart. It must never treat an old arbitrary blob entry as sufficient new evidence.

For material/paid claims, the source consumption, charge, grant and receipt must share one database transaction and concurrency boundary. Install committed in-memory state or reload it after a failure; stale inventory caches must not authorize a second claim. Send inventory/currency/wardrobe updates after commit. Retrying a lost response must return the existing result rather than repeat the charge or random roll. Durable delivery handles a full inventory or disconnect without losing the reward.

### Achievement migration and trust

1. Preserve the original achievement words and existing title backups before migration. Use additive schema and a reversible migration record.
2. Restrict counting to the known catalog; classify legacy bits as backed by durable evidence, reconstructable, or unsupported.
3. Backfill only facts the server can establish. Current equipment can prove a current-state condition, but cannot prove an old tournament win or completion of a quest-specific item exchange.
4. Produce an operator report of expected eligibility changes using character identifiers only. Define treatment of unsupported old progress before activation; do not quietly invent completion to reach 320.
5. Move trusted game completion into one service with idempotent source events. Client requests remain bounded UI operations or validation hints, never proof of a win or quest completion.
6. Reconcile once on login and after a committed change, with periodic expiry checks. Preserve the ordinary player's immediate update path.

Before changing an achievement/event packet, extend `docs/PROTOCOL_RESEARCH.md` with exact evidence and confidence. Do not call inferred layouts verified. Test the existing packet's length, identity, bounds and malformed cases as well as the new authority rules.

## 3. P1: restore local tournaments as complete services

**CTF work package:** trace the existing packet family, guild signup, event map 2057 and scoring code. Extract the necessary lifecycle from parked scheduling into an explicitly owned active system. Validate objective interactions against run membership, map, distance and state. Finalize a deterministic winner, persist it once, and let current guild leadership determine Victor eligibility.

**Elite PK work package:** identify the active tournament classes and avoid starting duplicate legacy coordinators. Audit existing signup, match timers and claim NPCs. Freeze the 6711-compatible bracket contract, persist match progression/results, and issue the existing seven-day highest-bracket champion entitlement through the new grant service.

Both work packages must handle insufficient turnout, legal byes, disconnected players, late entry, draws, cancelled runs, server restart and repeated claims. A fake adventurer must not enter a competitive bracket or generate title-winning evidence. Participation limits and invalidation rules must be public and enforced equally for non-GM players.

Do not simply remove the unconditional return from `WorldTournaments`: that would also reactivate unrelated legacy event blocks. Wire only the audited event services and remove or leave disabled their duplicate old scheduling paths.

Acceptance: two ordinary guilds finish a fresh CTF; ordinary players finish a fresh Elite PK bracket; losers cannot claim winner rewards; leader changes, expiry, logout and a restart produce correct wardrobe ownership and visuals.

## 4. P2: first classic quest batch

### P2A: confirmed routes and existing completion bridges

| Work package | Actual player loop | Required integration | Acceptance evidence |
| --- | --- | --- | --- |
| Demon Exterminators | Accept hunt, complete each stage, return for final claim | Quest progress, legitimate kill credit, daily scope, NPC/item mapping, achievement 11001 | Stage order and counts persist; guard kills do not advance; one final claim |
| Moon Box | Navigate tactics, collect required distinct tokens, exchange and finish | Map transitions, token ownership, atomic exchange, achievement 11022 | Purchasing/trading a Moon Box never substitutes for the terminal quest transaction |
| Power Arena restoration | Enter, defeat guardians, advance through seven stages, earn/exchange badges | Existing stage NPC audit, reachable schedule, defeat/re-entry rules, achievements 11013/11014 | Cannot skip floors or replay badge claims; genuine badge route works |
| Existing Daily Envoy routes | Complete an already-working hunt/soul/Spirit Bead route | Trace current completion and reward transaction; map only matching achievements | Completion updates tracker, persistence and achievement once |
| Quiz and local tournament hooks | Correct answers, delivered flags, genuine participation/wins | Existing quiz/tournament result sources | UI reports cannot replace answer/objective/result records |

Historical counts and mechanics are in the research report. Any reduced hunt counts, changed daily caps or reward substitutions require a named Revora edition. Progress values shown to players must match the actual completion rule.

### P2B: larger restoration after version checks

Dis City needs a proper dungeon lifecycle and clarified achievement semantics, not just four grants on a boss death. Sky Pass needs five navigable floors and a safe terminal claim. Snake Islands needs map validation and a traversable jumping route. Labyrinth needs Simon's accessible entry/reward options and the precise Ancestor Box exchange. Each should use existing lawful client assets when the item/map identity is established.

Audit newbie/equipment bonus and rebirth quest prerequisites alongside these systems. Already-promoted or reborn characters need an explicit, evidence-backed way to complete eligible quest history; a paid promotion shortcut must not silently stand in for a named quest.

### Rising Star reachability artifact

Before claiming Rising Star is normally attainable, produce a factual 337-row server catalog audit with:

- ID, verified predicate and authoritative completion producer;
- required class, rebirth, gender, social role and event availability;
- permanently repeatable, historical-only, mutually exclusive or currently unavailable status;
- a concrete route or supported class-change sequence reaching at least 320 for each intended character profile;
- unresolved entries and the maximum proven reachable count for that profile.

If the proven maximum is below 320, present the missing prerequisites and possible content changes. Do not lower the threshold or count arbitrary custom daily tasks as old achievements without an explicit product decision. Repeating one daily quest remains one distinct achievement.

## 5. P3: summer wing season

Use the documented recipes in the research report as the baseline. Before coding, map fragment, wing, dye and binding item IDs against both client and server catalogs; a display name match alone is insufficient. Confirm whether existing item-use and wardrobe paths can carry the required state. Stage a client data patch only for content that actually needs it.

Proposed earning design: one short daily material chain plus progress from a rotating existing activity. PvE should provide a complete path; competitive activity can be an optional alternative. Choose explicit caps, season dates and a guaranteed minimum earning rate after measuring the current economy. Do not automatically attach summer rewards to Revora's farmable Outstanding Exploit Packs.

The artisan shows **Materials**, **Craft wings**, and **Rules**. Preview the exact recipe, binding and random-color outcome before consumption. A repeated request after a disconnect must return the same recorded result. A full inventory, concurrent craft, database failure or existing duplicate design must have a defined, visible outcome.

Potential completion effect: five additional eligibility sources, taking implemented expressions/sources from 29 to 34 if no other new reward family is included. That arithmetic does not assert that all earlier collection supplies or Rising Star are already complete.

## 6. P4–P7: larger or evidence-dependent features

| Family | Concrete prerequisite | Deliverable once resolved |
| --- | --- | --- |
| Holy Grail | 6711 item identities, cup source, season and leader-transfer rules | Guild War season ledger, legitimate cup exchange, Grail craft with both wardrobe grants |
| Homecoming | Absence threshold, campaign dates, reward duration and VIP evaluation moment | Persistent first-return eligibility and one campaign claim per entitled identity |
| Mr./Ms. 2016 | Category/rank mapping, Beauty/Wise conditions and duration | Election with transactional votes, immutable standings and category-specific claims |
| Mr./Ms. 2017 and Romance | Local/all-server stages, category mapping, source-specific expiry; portrait format decision | Staged election and correctly timed awards; all-server results depend on multi-realm scope |
| Couples vs. Singles | Support thresholds, eligibility and expiry | Faction contribution campaign and Heart-wing claim; separate from Couple PK |
| Cross-server competitions | Origin realms, exact brackets/edition, admission/state ownership | Coordinator, authenticated result return, roster entitlement and replay-proof claims |
| Poker | Target edition, bracket/elimination rules, tournament wallet and reward semantics | Full coordinator and audited standings; table-level play alone is insufficient |
| Fashion / Planet / Honored Hero | Original authoritative grant rules | Rule contract first; implementation remains unplanned where identity is unknown |

Cross-server transport needs its own bounded interoperability work: provenance-qualified character snapshots, unique origin identities, exclusive session ownership during participation, disconnect/return recovery and authenticated immutable results. Reuse Revora's supported process/composition architecture; do not introduce a second uncontrolled lifecycle or imply a new native protocol is already verified. Single-realm alternatives must be presented as changed Revora editions if selected.

## 7. Scheduling, UI and economy contract

Normalize scheduling before adding weekly events. Most current events use host-local time while Extraction uses UTC. Store run instants in UTC and render a clearly named server timezone; test daylight-saving gaps and repeated hours. Persist scheduled run IDs so a restart cannot launch or reward the same occurrence twice.

Inventory current registration/fight windows and daily boss lifetimes, then reserve a major weekly event slot with fewer competing invitations. Maintain one active cosmetic season initially. A quest board should show the next opening and eligibility reason; it should not advertise a parked event as available.

Keep NPC introductions to one or two short in-world sentences. Put longer mechanics on an optional rules page and exact objective progress in the quest tracker. Client-facing descriptions must say whether the activity is a classic reproduction or a Revora seasonal variant. Avoid adding technical migration or database details to player dialogue.

Use bound materials/cosmetic progress as the first reward layer. Budget tradeable items, XP, CPs and RVPs separately. Decide kill attribution for real players, owned companions, teams and fake adventurers explicitly: companions may assist only according to the selected quest rule; ambient fake actors must never create accounts, receive rank prizes or fabricate achievement progress. Test these rules rather than relying on the NPC name or appearance.

## 8. Validation and release gates

These are future acceptance checks, not tests claimed to have run during this research.

| Area | Necessary evidence |
| --- | --- |
| Authority | Unknown/undefined achievement IDs, forged progress, wrong character identity, unauthorized map/objective actions and forged equips rejected |
| Transactions | Double-click, parallel request, reconnect replay, insufficient materials/funds, full inventory, failed write and lost response cannot double-charge or duplicate a grant |
| Persistence | Progress and final results survive relog/restart; committed awards are recoverable; original backups remain retained |
| Lifetime | Permanent grants persist; temporary grants expire online and offline; leadership changes update role-based awards |
| Event lifecycle | Ties, no-shows, cancellation, byes, duplicate timers, crash before/after finalization and late claims produce deterministic outcomes |
| Eligibility | Ordinary non-GM characters can complete the feature; fake actors and privileged commands cannot establish competitive achievements |
| Actual client | Quest acceptance/progress/completion, material counts, currency changes, title points, equipped wings and nearby-player visuals verified on the approved 6711 client |
| Compatibility | Existing blob and confirmed packet formats preserved; any protocol change has updated evidence and malformed/framing checks |
| Scheduling | One run per scheduled occurrence, explicit timezone, bounded overlap, daylight-saving and restart recovery |
| Economy | Measured legal earning routes and daily caps; no unbounded conversion from existing high-volume drops |

Build and test each coherent implementation batch once its changes are complete. Use disposable test persistence and real non-GM client checks. Stage required client content transactionally with exact preimages/postimages and rollback; keep proprietary client files and release artifacts out of Git. Update the wardrobe matrix to distinguish **implemented**, **validated playable**, and **live** as those states become true.

## 9. What needs a decision later

No decision is required to finish this research. The next implementation proposal can begin with P0 and the confirmed P1/P2A contracts. Before activating affected features, settle only the decisions that change their outcome:

1. Treatment of unsupported historical achievement bits after preserving and measuring them.
2. Classic versus explicitly adapted quest counts, season lengths, material rates and tradability.
3. Whether Revora will operate distinct origin realms for genuine cross-server rewards.
4. In-world portraits versus the historical real-photo format for a social competition.
5. Any proposed replacement rule for rewards whose original requirements cannot be recovered.

Keep unresolved content out of that first release. Finishing a small complete earning path is a measurable milestone: an ordinary player accepts it, completes the real objective, claims once, sees the reward immediately, and still owns the correct reward after restarting.
