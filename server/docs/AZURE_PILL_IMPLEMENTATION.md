# Azure Pill restoration contract

Current status (2026-10-01): this chain is included in the completed private
[nineteen-chain earning/restart validation](EQUIPMENT_QUEST_AGGREGATE.md).
The approved Revora10931 evaluator is implemented and tested; older statements
below that it is disabled or that no process restart was tested are historical.
Native/authenticated-client acceptance and live deployment remain pending.

## Evidence resolved 2026-09-26

The installed 6711 journal has a complete linear chain, missions 1727-1732,
with no missing predecessor. `tools/audit_azure_pill.py` validates the hash-bound
journal, all six stage links, NPC IDs, material quantities, eligibility, timer
and reward pack, then writes only derived numeric metadata and hashes to
[AZURE_PILL_CONTRACT.json](research/AZURE_PILL_CONTRACT.json).

The official [PC Azure Pill guide](https://co.99.com/guide/quests/pill.shtml)
confirms the route through Chang, Scholar Wu, Mu Mu and Bian Qing, its material
recipe, ring reward and reset after rebirth. The installed journal supplies
six discrete stages and a two-minute preparation timer. These are separate
sources: journal declarations are not proof of installed NPC placement or
native execution.

## Concrete implementation sequence

1. Accept at Chang; visit Scholar Wu, then Mu Mu.
2. Earn one Ratling Eye (721695) and three Ratling Meat (721696) from the owned
   quest hunt; collect two Herb Packs (721697) from Bian Qing.
3. Consume those exact owned materials atomically at Mu Mu; save a UTC deadline
   two minutes ahead. After the deadline, issue Azure Pill (721698) once.
4. Deliver the owned pill to Scholar Wu; claim at Chang with a once-per-rebirth
   completion receipt. The journal identifies final ring pack 728238.
5. Add a valuable owner-policy reward to completion, with its final category
   and exact item contract explicitly documented and tested before activation.

Use an owned quest service with durable per-rebirth stage/material/claim records,
ordinary NPC proximity, replay-safe deaths and atomic item exchanges. Unknown
original drop odds must be documented as Revora tuning, not called verified.

## Routing conflicts and unfinished evidence

Source audit finds existing `case 4482` (Phoenix return service) and `case 4484`
(cross-server travel dialogue). Neither may be globally replaced. The installed
journal declares those IDs as Scholar Wu and Bian Qing. Before routing, inspect
the server NPC rows and their maps, then use exact NPC/map ownership or dedicated
Revora aliases plus a staged journal overlay if the identities conflict. Chang's
journal row labels Job Center while declaring map 1002; resolve the installed
world location instead of guessing from that label.

The client's range is 40-60; the official guide only gives a minimum level.
Owner preference for higher-level recovery was requested. No level-policy
change is activated here. Server item catalog, Fire Rat family/spawn ownership,
ring-pack opening and resulting equipment identity still need verification.

The aggregate achievement 10931 remains disabled. Azure Pill is only one chain,
not proof of the entire equipment edition. No quest NPC, drop or earning path
has been activated by this evidence batch.

## Validation

The installed contract audit passes; four unit tests reject changed links,
quantities, timer, level range, reward ID, duplicate/missing missions and changed
material names, and preserve the incomplete/aggregate-not-proven flags. No game
build was needed for this Python evidence tool. No client files, decoded text,
live schema or server deployment were published.


## Server catalog and usable ring-pack prerequisite

Read-only inspection of the installed runtime resolves these identities:
FireRatL38 is family 67, level 38, with spawn row 59 on map 1011. All four
materials and pack 728238 are loaded. Their first six identity/eligibility fields,
and those of ring 151077, match the encrypted client catalog. See the derived
[server catalog evidence](research/AZURE_PILL_SERVER_CATALOG.json).

NPCs 4482, 4483 and 8796 are absent from the installed NPC table. NPC 4484 is
actually the cross-server agent on map 5001 (78,66), mesh 30940. Restoration must
provide quest actors without moving or replacing that service; source-level
4482 still has a separate legacy return dialogue. Exact quest NPC placement and
client journal aliases remain implementation work.

Pack 728238 previously had no opening route. It now uses the existing owned
fixed-output service to produce one IvoryHeavyRing 151077, level 43, Unique
quality 7. This is Revora's explicit mapping to the installed level-43 Unique
ring, rather than a claim that an original pack-opening algorithm was recovered.
It preserves source binding, initializes durability from the catalog, consumes
one unit and saves the output UID receipt atomically. A singleton replaces itself
in a full bag; a stack remainder requires an additional space. No quest issuer
was added in this prerequisite batch.

The staged build and 556 combined checks pass (406 prerequisites, 142 existing
fixed-pack checks, eight new Azure ring assertions). New checks cover actual
item-use dispatch, both binding states, stacked/concurrent consumption, exact
reload, capacity and rollback/reconnect. The existing Meteor source-text check
was replaced with actual item-use verification, and the hardcoded three-ID SQL
text assertion was removed in favor of the existing receipt migration/use checks.
The old-schema migration tests run before the new pack cases.

Ignored evidence: `obj/azure-server-catalog.log`, `obj/azure-ring-build.log`,
`obj/azure-ring-tests.log`. Tracked `VerifyAzureRingPack` also runs in the purse
review suite. Ordinary use of an already-owned pack is tested; Azure Pill's
quest earning path, NPC/journal integration, valuable final bonus, native
acceptance and deployment are still unfinished. No live changes were made.


## Six-stage server implementation (2026-09-26)

The server earning path now has dedicated quest actors (900071727-900071730),
startup registration, normal NPC dispatch, owned Fire Rat deaths and revive
cleanup. Existing travel actors remain intact. Chang is at map 1002 (473,300),
Wu at 1011 (214,231), Mu Mu at 1011 (182,241), and Bian Qing at 1002 (425,331).

Revora recovery policy uses level 40 minimum without an upper cutoff, once per
rebirth (0, 1 or 2). This is the stated recovery assumption, not a verified
original maximum-level rule. Each eligible owned FireRatL38 death has a 50%
Revora-tuned material chance, awarding the missing eye first, then three meat.
Corpse replay does not create extra rolls or materials. Bian issues two herbs
once. Exact saved material UIDs, brewing deadline, stage and completion survive
reconnection. Inventory changes and stage transitions share a transaction.

Delivery awards the journal's silver (1,900 total across stages) and one-third
of an EXP Ball, capped by normal progression rules. Final completion grants a
bound ring pack 728238 plus one bound P7 soul selected and saved at acceptance.
This is a one-time quest per rebirth, not a daily P7 source. A failed final item
insert rolls back both rewards and completion; reconnect permits a safe retry.

Validation uses ordinary characters across all three rebirth states, real NPC
dispatch, controlled owned monster deaths, saved item reload, early brewing,
repeated herb/completion requests, failure injection and normal ring-pack use.
The staged build and 440 checks pass (406 prerequisite checks and 34 Azure
assertions). Logs: `obj/azure-quest-build.log`, `obj/azure-quest-tests.log`.
The targeted suite is also registered in the tracked purse-review suite.
Client journal alias routing, native world/NPC checks and live deployment remain
pending. The old journal still names legacy NPC IDs; do not publish this batch
as a completed native quest. Achievement 10931 remains disabled.


## Saved journal projection and private overlay

The full quest GUI now derives missions 1727-1732 from `revora_azure_pill` for
the current character/rebirth. Stages 1-6 show completed predecessors and one
accepted mission; stage 7 shows all six finished. Legacy quest entries cannot
replace these projections, and client Begin requests cannot accept or finish
this chain. NPC actions refresh the GUI; reconnect reads the durable stage.

The staged build and 482 checks pass (406 prerequisites, 76 Azure assertions).
These include all eight journal states and six client Begin attempts across
all three rebirth states. Logs: `obj/azure-journal-build.log` and
`obj/azure-journal-tests.log`. Three Python patch tests also pass, checking
alias/reward rewriting, idempotence, unrelated bytes and malformed inputs.

`tools/ClientPatches/prepare_azure_journal.py` stages over the reviewed Warrior
max-reward journal, preserving the prior quest bundle and Monk navigation.
It verifies the installed journal, composed source and approved DX9 hashes
before staging and again afterward. Only six Azure sections change: aliases,
Chang's map label, the level-255 display ceiling, reward text and ingredient
instructions. The server still enforces its level-40 minimum and per-rebirth
completion. The maximum display value covers the byte-sized character level.

Private artifact: `obj/chi-refill-validation/azure-pill-journal/`.
Postimage SHA-256:
`cd9a8666d2bb7d6bd862d7d1f85147856e6407d5f552b2d99f85a91e4c11733d`.
The manifest includes the source and original hashes, preserved original,
unsigned/uninstalled state and future transactional rollback requirement.
Client data stays ignored. This is not a signed release: real DX9 navigation,
NPC placement/rendering, live progress display and deployment remain pending.


## Ordinary route and brewing through full restart (2026-10-01)

Three private process phases pass: `career-azure-brew`, `career-azure-claim`,
`career-azure-reload`. Existing ordinary character4294908401 accepts at installed
Chang, follows the installed Twin City/Phoenix portals, and visits Wu and Mu Mu.
Seven owned deaths of loaded FireRatL38 monsters produce exactly one eye and
three meat through the unchanged50% material rolls. Damage is controlled through
the production ReceiveAttack path; materials, ownership, stage and final rewards
are not fixture-seeded. Bian gives two herbs once; a repeated request preserves
the exact snapshot. Mu consumes all six inputs and commits the brewing deadline.
An immediate repeat cannot issue the pill.

The actor is saved while brewing, then the process exits abruptly. After a full
game/database restart, the saved quest/profile/items and prior event rewards
match exactly. A controlled clock just after the persisted deadline allows Mu
to issue the owned pill. Wu consumes it; Chang completes stage7 and awards the
saved bound P7 type plus bound ring pack728238. Exactly two new item UIDs appear,
and the full route's silver increment is1,900. The configured EXP reward follows
the actor's normal progression rules and its saved profile is in the checkpoint.
Repeated completion pays nothing. Another cold restart retains stage7, both
recorded reward UIDs, profile and exact prior state, with another replay refused.
Checkpoint: `revora_azure_process_probe`, done=true and AzureProcessSnapshot.

No production code/build changed. Candidate SHA256:
`05F7A80A0EC5F74ED699076019CE7FFD9F2B5B2646F7C69562FEC1A4E43BD9A6`.
The pack remains owned/unopened in this process batch; existing pack-use tests
retain their separate scope. NPC approaches, combat damage and completion clock
are controlled, so this does not certify native combat, natural two-minute UI
observation, the private journal overlay or live deployment. It is one actor's
current rebirth, not new process coverage of all three lives. The retained main
career and its achievement count are unchanged. No live process was restarted.

## Retained brewing and reward follow-up (2026-10-02)

The same retained career now earns Azure Pill through calculated material kills,
ordinary NPC delivery, a saved brewing period across full process restart, and
final bound rewards. Exact item identities survive another restart and repeated
claim is unchanged. See [the combined retained batch](SPUN_GOLD_IMPLEMENTATION.md#retained-spun-gold-and-azure-pill-batch-2026-10-02)
for controlled randomness/time, qualification provenance and native limitations.
