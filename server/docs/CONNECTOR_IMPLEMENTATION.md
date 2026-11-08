# The Connector implementation contract

Current status (2026-10-01): this chain is included in the completed private
[nineteen-chain earning/restart validation](EQUIPMENT_QUEST_AGGREGATE.md).
The approved Revora10931 evaluator is implemented and tested; older statements
below that it is disabled or that no process restart was tested are historical.
Native/authenticated-client acceptance and live deployment remain pending.

## Evidence checked 2026-09-26

The installed 6711 journal defines five missions, 1718-1722, beginning at level
22 with a client upper limit of 42. The hash-bound contract audit validates the
predecessors, numeric NPC routes, five Castle Bandit kills, one Connector Letter
729942 and reward pack 728236. Three loaded item names match the contract.
The derived metadata is in [CONNECTOR_CONTRACT.json](research/CONNECTOR_CONTRACT.json).
No proprietary journal or item catalog is committed.

The official [Connector guide](https://co.99.com/guide/quests/connector.shtml)
describes a trainer referral, a message between two thieves, five bandit kills,
a letter hand-in and a final reward. Its listed prize includes a level-27 Refined
necklace, silver, experience and an EXP Ball. It also describes repeat eligibility
after rebirth. The installed edition instead routes the final reward through
Chang; implementation must follow that local route.

Locations conflict: the walkthrough lists Wong at 347,228, the official
[NPC list](https://co.99.com/guide/npcs/bonusnpc.shtml) lists 474,332, and installed
6711 lists 440,339. Use the installed route with a dedicated Revora alias after
validating the server map and occupied NPC locations. Chen has four evidenced
hiding locations in Phoenix Castle; the first journal route uses 33,398. His
rotation schedule is unpublished and must not be invented as an original rule.

## Implementation batch

1. Read the loaded NPC, monster/spawn and item catalogs for Wong, Chen, Castle
   Bandits, the necklace pack and its concrete output. Avoid repurposing unrelated
   legacy NPC IDs. Verify item consumer support before enabling reward issuance.
2. Add a per-rebirth durable chain: Chang acceptance; Wong referral; Chen hunt
   acceptance and five genuine owned kills; letter issuance; Wong hand-in; Chang
   final reward. Distinguish hunt acceptance from completion so remote kills or
   repeated conversations cannot skip the objective.
3. Preserve exact earned-item ownership, replay-safe deaths, atomic silver/EXP
   and final rewards. Apply the owner's valuable one-time quest policy with one
   bound P7 soul in addition to the catalog pack. Preserve a minimum level of 22
   with no upper cutoff, matching Revora's existing recovery policy for this edition.
4. Connect ordinary NPC routes and saved journal, validate earning/reload,
   invalid proximity, duplicate claims, full inventory and rollback. Stage a
   private client overlay over the Power Book journal; keep live deployment and
   native acceptance pending.

Server NPC placement, bandit family ownership, pack contents, Chen's Revora
movement policy and detailed saved-stage transactions are still unverified.
No Connector service is enabled by this research. Aggregate equipment achievement
10931 remains disabled: this chain and Azure/Power Book do not prove the whole
edition. A bounded local scan found 150 TypeId-4 journal rows, including unnamed
roots; that row count is not a count of implemented quests or an achievement rule.

## Validation

The installed audit passes. Three synthetic tests cover valid metadata and reject
changed links, kill/item quantities, coordinates, levels, rewards, ambiguous or
missing missions and wrong catalog names. These are contract checks, not gameplay
tests. No game rebuild, live database write, client installation or deployment
was needed for this evidence batch.


## Loaded actors and usable pack (2026-09-26)

Read-only loaded-catalog inspection identifies CastleBandit family **2140**, level
22 and 545 HP. Ordinary Bandit family 7 is a different actor and must not count.
Five catalog spawn rows for 2140 exist on map 1011, including the journal area;
one has zero extents and needs runtime validation before treating that row as a
working spawn. This is catalog evidence, not proof of observed live population.
NPC 4437 exists at 1011(33,398), mesh 5820, with database name `None`. Wong 4436
and Chang 8796 are absent from this query; use the existing Revora Chang service
and an owned Wong alias. Before reusing Chen 4437, validate its loaded talker and
preserve unrelated roles. No native Chen rotation has been established.
Derived results: [CONNECTOR_SERVER_CATALOG.json](research/CONNECTOR_SERVER_CATALOG.json).

Pack 728236 advertises a Refined necklace in the installed catalog. The server
contains level-27, class-neutral ThreadNecklace **120046** (Refined quality); the
class-190 MaskBag family is distinct. FixedOutputPacks now opens one 728236 into
one 120046 with source binding and +0, using the existing atomic payment/output
receipt and inventory installation. This does not add an issuer or a daily P7
reward. Any separate quest EXP Ball reward must be implemented at the earning
route; it is not silently added to a pack whose catalog describes one necklace.

Build and **564 checks** pass: 406 prerequisite checks, 142 fixed-pack checks,
eight Azure-ring regressions and eight Connector-pack assertions. Shared,
parameterized single-equipment-pack fixtures test bound/unbound concurrent stack
use, exact UID/receipt reload, full-bag singleton replacement, stacked remainder
capacity, forced insert rollback and reconnect retry. Logs:
`obj/connector-pack-build.log`, `obj/connector-pack-tests.log`. The new test is
registered in the tracked purse review suite. No ordinary Connector earning path
or native pack presentation is claimed yet. No live catalog/client/schema change
or deployment was made.


## Ordinary earning route (2026-09-26)

ConnectorQuest is now connected to startup, normal NPC dispatch, actual monster
death and respawn notifications. Chang offers the quest from level 22, including
the 22-39 range that cannot use Azure Pill. Wong uses alias 900071718 at
1002(440,339), mesh 5810. Chen uses catalog actor 4437 at 1011(33,398), mesh 5820;
startup refuses a conflicting loaded Chen placement. This Revora edition keeps
Chen at that first verified location. No invented original rotation is claimed.
No GM status, owner identity or allowlist is required. Eligibility uses current
rebirth 0-2 and a minimum of 22 without the old upper cutoff.

Durable sequence: Chang issues a bound badge and freezes the P7 identity; Wong
refers the player to Chen; Chen authorizes the hunt; five genuine family-2140
wilderness deaths allow Chen to issue one bound letter; Wong consumes that exact
letter for silver and ten minutes of EXP; Chang consumes the exact badge for
three bound rewards (pack 728236, EXP Ball 723911, selected P7). Silver totals
1,300. Wrong family/map, unrelated killers, remote conversations, premature
hand-in and corpse replay cannot advance the chain. Respawn clears the per-life
weak death marker. Inventory and saved character are checked under owner locks;
NPC mutations hold the session-install lease and repeat proximity before commit.
Post-commit installation failure uses the existing reconnect fence.

Final rewards and their exact UIDs are stored with completion. Shared EXP code
preserves mentor, reincarnation and auto-hunt effects. Full reward capacity is
net two extra slots because the badge is consumed. The necklace pack's existing
atomic consumer produces one bound level-27 Refined Thread Necklace. No daily P7
source or aggregate equipment achievement is enabled.

Build and **440 checks** pass (406 prerequisites, 34 Connector route assertions).
Three ordinary characters cover rebirths 0, 1 and 2, with the unreborn character
starting at level 22. Actual NPC dispatch and lethal damage complete every stage;
the fourth kill cannot issue a letter, and a reload precedes the fifth kill.
Tests reject remote acceptance, skipped referral, ordinary Bandit credit and
replayed deaths/claims, verify hand-in EXP/silver, force a final item insert
failure, retry after reload, and open/reload the earned necklace through ordinary
item dispatch. No quest stages are seeded. Monsters are controlled fixture actors
using the loaded CastleBandit profile; this does not prove native spawn/AI behavior.
Logs: `obj/connector-quest-build.log`, `obj/connector-quest-tests.log`.

Remaining: saved journal/Begin protection and composed private directions,
lost-badge/letter recovery, focused full-inventory earning cases, native NPC/spawn
acceptance and live deployment. Startup is connected in source, not deployed;
no live schema/client/Host was changed. Aggregate achievement 10931 remains off.


## Journal, recovery and capacity (2026-09-26)

The five missions now project from the saved current-life stage through the
existing quest status serializer. Begin cannot author progress; NPC quest-abandon
requests refresh instead of deleting state. NPC actions refresh the GUI and
ordinary full journal loading reads persistence again. Journal status is staged
progress; the current kill count is reported by Chen rather than an invented
counter packet. The abandon branch has source review, not new native acceptance.

Chang, Wong and Chen offer lost-item recovery. The transaction locks saved owner,
inventory and quest rows; a surviving item row anywhere prevents copying it.
Missing badges may be replaced while unfinished. Missing letters may be replaced
only at earned-letter stage 4, so completed hand-ins do not manufacture letters.
Fresh bound singleton UIDs, tracking changes and unique old-to-new recovery
receipts commit together. Recovery preserves stage, kills, reward identity and
purse. Unaccepted/completed quests cannot replace consumed items. Full inventory
leaves the operation retryable. Recovery uses the same session lease, proximity
recheck and post-commit reconnect fence as normal quest actions.

Build and **493 server checks** pass, including all earlier earning cases plus
all journal stages/reload, forged Begin requests, actual ordinary badge/letter
disposal followed by recovery, recovery replay, stored-letter refusal, completed
recovery refusal, full-bag letter issuance retry and final-reward capacity refusal.
The full earning journeys remain unseeded. Logs: `obj/connector-journal-build.log`
and `obj/connector-journal-tests.log`. The suite includes 406 shared prerequisites;
it is not 493 independent Connector tests.

`tools/ClientPatches/prepare_connector_journal.py` stages the five corrected
entries over the private Power Book overlay. Wong uses the verified installed
location and Revora alias; Chen uses the fixed first hiding place. Directions,
level ceiling, bound necklace pack/EXP Ball/P7 rewards and recovery instructions
match the server edition. Only these five sections change; unrelated bytes remain
identical and the rewrite is idempotent. Three Python checks cover preservation,
correct routes, missing/duplicate missions and malformed fields.

Private artifact: `obj/chi-refill-validation/connector-journal/`.
SHA-256: `a4aaa9ac6da9453170fca6de42322e482ec7cc6f44d8ee584b310cc67b25b90e`.
It is unsigned, uninstalled and unpublished. No proprietary file is committed.
Native NPC/journal/disposal presentation, actual loaded spawn/AI behavior,
concurrent storage UI, full Host restart and live deployment remain pending.
Aggregate achievement 10931 remains disabled. No live schema, Host or client
was changed by this batch.


## Loaded hunt and partial progress through full restart (2026-10-01)

Three private phases pass: `career-connector-hunt`, `career-connector-claim`,
`career-connector-reload`. Ordinary actor4294908401 accepts Chang's badge,
visits installed Wong, uses the installed Phoenix portal and receives Chen's
hunt. Two loaded living CastleBandits (family2140) die through production
ReceiveAttack with controlled damage. Each earns exactly one saved kill.
Early hand-in refuses the letter. The actor saves and exits abruptly at2/5.

Cold game/database startup preserves the exact owned badge, quest stage, two
kills, profile and prior Azure/event rewards. Three further loaded CastleBandit
deaths reach5/5. Chen issues one bound letter; a duplicate request changes nothing.
The installed return portal brings the actor to Wong, who consumes that letter.
Chang consumes the original badge and grants exactly three new bound items:
necklace pack728236, EXP Ball723911, and the saved P7 soul. Total silver increases
by1,300. The hand-in EXP follows normal progression, with the profile included
in the saved snapshot. Completion replay and post-completion lost-item recovery
both preserve state. A further cold restart retains stage6,5/5 and all three
reward UIDs, and repeats both refusals without another item or payment.
Checkpoint: `revora_connector_process_probe`, done=true and exact snapshot.

The hunt uses actual composed-map entities from the installed spawn population;
it does not prove every spawn row, the zero-extent row, AI difficulty or native
combat. NPC approaches and lethal damage are controlled; no stages, badge,
letter, kills or prizes are fixture-seeded. This is one current rebirth; earlier
all-life and lost-item tests retain their own scope. The earned necklace pack
and EXP Ball remain owned/unopened here. No production code/build changed.
Candidate SHA256:
`05F7A80A0EC5F74ED699076019CE7FFD9F2B5B2646F7C69562FEC1A4E43BD9A6`.
The main retained career is unchanged. Native journal/client acceptance and live
deployment remain pending; no live server, schema or client was changed.

## Retained career Connector journey (2026-10-02)

The same retained twice-reborn Water Taoist now finishes the Connector chain in
life2. An ordinary trade with the existing spouse frees one inventory slot while
preserving that item's identity. Chang900071727 option51 issues the badge; Wong
900071718 and Chen4437 admit the messenger through normal dialogue, with the
ordinary Phoenix portal used for travel. Two calculated ranged CastleBandit2140
kills are saved before full game/database restart. Premature turn-in is unchanged.

After restart, three more calculated kills complete the five-kill requirement.
Chen issues the letter, Wong consumes it, and Chang consumes the badge and gives
three bound rewards:728236,723911 and a P7 soul. The journey checks the total
1,300-silver increase and all three exact reward UIDs. Another full restart retains
the nested career and reward identities. Repeated completion and recovery options
cannot duplicate rewards or replace consumed quest items.

`career-retained-connector-{hunt,claim,reload}` pass on candidate SHA-256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
Probe `revora_retained_connector_probe` ends at step2. Two preliminary attempts
stopped at an overly strict fixture capacity assertion, before quest admission;
the fixture was corrected to the actual three-slot requirement. No reward or
quest eligibility rule was relaxed. Movement/proximity and attack cooldowns are
controlled; damage is calculated by the normal attack path, not injected.
Starting qualifications/funding retain earlier provenance, and native acceptance
is pending. No production binary or live deployment changed.

Fresh read-only evaluation finds one of nineteen aggregate final receipts now
matches for the retained UID. Achievement10931 remains absent, as expected;
other characters' earlier completed chains are not counted toward this identity.
