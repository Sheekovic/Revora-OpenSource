# Spun Gold Armor implementation contract

Current status (2026-10-01): this chain is included in the completed private
[nineteen-chain earning/restart validation](EQUIPMENT_QUEST_AGGREGATE.md).
The approved Revora10931 evaluator is implemented and tested; older statements
below that it is disabled or that no process restart was tested are historical.
Native/authenticated-client acceptance and live deployment remain pending.

## Evidence and scope (2026-09-26)

The installed hash-bound journal contains four linked missions, 1723-1726,
minimum level 30 and legacy upper limit 50. Routes begin at Chang, use Veteran
Hong at 1011(194,260), and finish at Chang. Stage 1724 requires Invitation 729971;
1725 records disguise, Cloud defeat and SpunGoldArmor 729974. Pack 728237 is the
final catalog reward. The audit checks links, eligibility, route fields,
objective quantities, reward and five item identities. Numeric metadata and
hashes: [SPUN_GOLD_CONTRACT.json](research/SPUN_GOLD_CONTRACT.json).

The official [Spun Gold Armor guide](https://co.99.com/guide/quests/goldarmer.shtml)
adds the intermediate actions omitted from the journal counters: obtain an
Offering from Hong, visit Zhao Jian for a disguise, use the Offering near the
altar, defeat Cloud, open his Wooden Box and return the armor. It lists armor,
silver and experience, with repeat eligibility after rebirth. Original invitation
odds and disguise duration are not published. The installed quest/item content
supports Offering 729972 and WoodenBox 729973. No original odds or timing are
claimed verified.

Read-only server catalog: Bandit family 7 (level 32), BanditL33 family 66,
CloudtheLustful family 2141 (level 32, 1600 HP). A **legacy Cloud spawn row already
exists at 1011(99,430)**, with zero extents. It must not grant quest progress or
bypass an earned Offering/disguise. Hong 4464, Zhao 4465 and old Chang 8796 are
absent from the inspected NPC rows. Use the existing Revora Chang and owned NPC
aliases after validating terrain/placement. Spawn rows alone are not proof of
working runtime actors. See [catalog metadata](research/SPUN_GOLD_SERVER_CATALOG.json).

## Usable reward pack

FixedOutputPacks now handles 728237 transactionally, with exact item debit,
capacity, saved reward receipts, source binding and class snapshot protection.
It yields one Unique (+0) class armor. Unknown classes keep their unopened pack.
Catalog mapping:

| Class family | Item | Required level |
| --- | --- | --- |
| Trojan | 130027 | 32 |
| Warrior | 131027 | 32 |
| Archer/Assassin | 133017 | 27 |
| Ninja | 135027 | 32 |
| Monk | 136027 | 32 |
| Pirate | 139027 | 32 |
| Dragon Warrior | 138027 | 32 |
| Taoist | 134027 | 32 |
| Wind Walker | 101027 | 32 |

The installed pack says armor around level 32. Archer's loaded coat progression
has level 27 rather than a level-32 item, so this Revora mapping uses the nearest
lower catalog Unique coat instead of inventing an item. The class-neutral source
pack retains its binding on the output. No extra EXP Ball or quest issuer is
added by this consumer. The future once-per-rebirth quest reward will also include
one bound P7 soul under the owner's valuable one-time quest policy.

Build and **435 checks** pass: 406 shared prerequisites and 29 armor-pack checks.
They cover bound/unbound concurrent stack use, exact output/receipt reload,
full-bag singleton replacement, stacked capacity refusal, forced insert rollback,
reconnect retry, ten supported class cases (including both Taoist branches),
ordinary item dispatch and unsupported-class preservation. Catalog contract has
three passing parser tests. Logs: `obj/spun-gold-pack-build.log`,
`obj/spun-gold-pack-tests.log`. No source quest/earning completion is claimed.

## Next implementation batch

1. Verify walkable Hong/Zhao/altar positions and a client-supported beauty model.
   Existing DisguiseAmulet transformation code is only a source seam: do not copy
   its consumable behavior or claim quest disguise acceptance from it.
2. Implement saved per-rebirth stages and exact Invitation/Offering/Box/Armor
   ownership. Preserve minimum 30 without upper cutoff, consistent with Revora's
   recoverable equipment edition. Document any original-unpublished odds/timing
   as explicit Revora rules.
3. Summon an owned encounter only from a valid disguise and an earned Offering
   at the altar. Existing static Cloud actors cannot count. Guard attacks,
   lifetime, disconnect/retry and one-time box delivery, then require actual box
   opening and armor return before final rewards.
4. Connect ordinary NPC/item routes, recover lost items, validate earning/reload
   and atomic silver/EXP/P7/armor-pack delivery. Project the four saved missions
   and compose the next private client journal overlay.
5. Keep aggregate equipment achievement 10931 disabled until the entire edition
   is established. Native/Host acceptance and deployment remain separate gates.

No live database/schema change, Host restart, active client mutation or public
release was made in this batch. The contract/catalog artifacts contain derived
metadata only; proprietary files remain outside Git.


## Cosmetic disguise lease and shared terrain routes (2026-09-26)

SpunGoldAppearance supplies an owner/session/token-bound cosmetic lease. The
Revora appearance uses **2562005**, the female human mesh already used by the
local CityGuardSystem. This is an explicit Revora choice; the original quest's
exact beauty model was not established. It projects only the existing mesh field
in Entity's spawn/update buffer. The character's Body, Face, class, HP and combat
transformation state remain unchanged. Normal Body/Face writes retain the active
projection; a real transformation cancels it and owns the mesh thereafter.

The lease lasts five UTC minutes (Revora timing, not an original-rule claim),
does not extend on repeated application, and requires a locked eligible ordinary
owner on map 1011 plus a rechecked coordinator context. Invalid map, death, expiry,
replacement session or combat transformation invalidates the token. Cleanup only
restores a mesh still owned by this cosmetic appearance, using the latest actual
Body/Face/transformation values. A stale token cannot clear a newer lease. The
registry has a 64-lease bound. No disguise is persisted as character identity.

Build and **418 checks** pass (406 prerequisites plus twelve appearance checks):
shared spawn/update mesh, unchanged identity/HP, no renewal, stale-token refusal,
face updates, expiry, transformation precedence, map departure, death without
healing, save/reload session replacement and invalid-context refusal. Logs:
`obj/spun-gold-appearance-build.log`, `obj/spun-gold-appearance-tests.log`.
The future quest coordinator must own stage persistence, application, capacity
responses and scheduled cleanup; **no production quest giver calls this helper
and its Tick is not scheduled yet**. Existing Entity projections remain inactive
without a registered lease. Native appearance/clothing rendering is unverified.

The hash-bound terrain audit verifies both map-1011 bindings, dimensions and
reachable Hong/Zhao/altar/Cloud landmarks using only cells walkable in **both**
inputs. Client woods.DMap and server 1011.map are **not identical**: 3,534 cells
differ (3,433 client-blocked/server-open; 101 client-open/server-blocked). The first
equality check failed; the final report explicitly records the mismatch and
proves shared routes rather than asserting equality. No terrain was changed.
[SPUN_GOLD_TERRAIN.json](research/SPUN_GOLD_TERRAIN.json) records the exact hashes,
coordinates and cardinal walking distances. Live actor occupancy, native routing
and the cause of those pre-existing terrain differences remain unverified.

Next: connect owned invitation/Offering progression and the altar encounter,
then box opening/armor return/rewards, recovery and journal. The existing static
Cloud cannot be used as proof of an owned summon. Native/Host acceptance and live
deployment remain pending; aggregate achievement 10931 remains disabled.

## Saved progression and reward checkpoint (2026-09-26)

SpunGoldStore now owns eight durable stages per character/rebirth: acceptance,
Hong referral and invitation hunt, Offering, reserved summon, earned box, armor,
returned armor and final completion. Bandit families 7/66 use an explicit Revora
50% invitation chance with actual killer/death ownership and duplicate-death
protection. Each summon has a UUID, owner/life, body UID and five-minute deadline;
winning requires the exact saved attempt and registered private-family actor.
The legacy Cloud family 2141 cannot satisfy this store. Runtime registration and
combat ownership remain the next implementation step.

Item exchanges, silver, experience and final reward insertion share a transaction
with stage advancement. The final reward is one bound class armor pack 728237 and
one bound P7 selected at acceptance. This is a one-time quest per rebirth, not a
daily P7 source. Total silver is 1,200; armor return grants ten minutes of EXP.
Recovery cannot copy an item still present in any storage/owner row. Lost
invitations require another death; earned Offering/box/armor can be replaced.
Abandoned attempts return to Offering recovery; a full-bag victory saves the win
and defers its box until space becomes available.

Build and **445 checks pass**: 406 prerequisite checks and 39 focused checks for
ordinary characters at rebirth 0/1/2, real Bandit deaths, stage ordering,
duplicate invitations, warehouse refusal, disposal/recovery, abandoned attempts,
full-bag box and final rewards, exact box use, save/reload, silver/EXP,
transaction rollback on failed P7 insertion, and concurrent final claims.
Logs: `obj/spun-gold-store-build.log`, `obj/spun-gold-store-tests.log`.
The store fixture binds a test actor to a reserved attempt directly in the
isolated database; it does **not** prove live summon/combat integration.

**Not enabled or complete:** startup schema initialization, NPC/item/death routes,
owned Cloud combat and cleanup/reconnect recovery, journal, native client and
Host acceptance. No production schema, running server or active client changed.
Next roadmap action: connect those routes, validate ordinary-player earning,
and then prepare the private journal overlay. Achievement 10931 stays disabled.

## Owned Cloud encounter checkpoint (2026-09-26)

SpunGoldCombat now reserves the saved attempt, consumes the tracked Offering and
registers a separate family-9921723 Cloud actor. It copies the checked level-32,
1,600-HP family-2141 profile without enabling its normal loot or respawn. One
active encounter per character is allowed, with a 64-entry registry bound.
A bounded search uses the 25 audited shared-walkable cells x=97..101/y=428..432;
current monsters, companions, NPCs, players and pending summons exclude cells.
This avoids depending on the occupied legacy Cloud tile. Busy placement returns
without consuming the Offering. The terrain report now records those 25 cells.

The normal attack-validation, receive-damage and death paths now guard this
private family. Only the current owner or their registered companion can fight
it; companions credit the owner. Unregistered bodies fail closed. Owner-lock
leases serialize damage with quest mutations. Actual owner death credit calls
the saved victory transaction, including full-bag deferral. Completed corpses
are removed after one second. Expiry/departure removes the actor and restores
retryable progression for healthy sessions; replacement sessions wait until the
old runtime owner is cleaned up before abandoning their persisted old attempt.
These cleanup/reconnect helpers still require the upcoming coordinator calls.

Build and **420 checks pass** (406 prerequisite + 14 encounter checks): real
summon and Offering consumption, duplicate refusal, separate owners/cells,
foreign-player receive-damage rejection, attack ownership, unregistered-family
rejection, ordinary lethal attack earning a box, duplicate death, corpse cleanup,
expiry/recovery, map departure, and replacement-session cleanup/recovery.
`obj/spun-gold-combat-build.log` and `obj/spun-gold-combat-tests.log` record results.
This replaces the store-only synthetic actor binding as evidence for the tested
combat route. Companion behavior is implemented but not accepted by this suite.

No production coordinator invokes Summon, Tick or RecoverAttempt yet. NPC/item
routes, scheduled cleanup, login recovery, journal and native appearance/AI/Host
acceptance remain incomplete. No live deployment occurred. The global guards
are inert for existing legacy family-2141 Cloud actors.

## Ordinary earning route and journal (2026-09-26)

SpunGoldQuest is now wired into normal startup, NPC/item dispatch, Bandit death,
revive credit reset, periodic encounter/disguise cleanup and login recovery.
Hong (4464, 1011:194,260, mesh 6830) and Zhao (4465, 1011:200,325, mesh 6840)
are available to eligible ordinary players. Chang offers the quest through both
his level-30..39 Connector menu and level-40+ menu. Existing conflicting NPC
placements fail initialization instead of silently reusing the wrong actor.

Only a saved Offering-stage player receives Zhao's five-minute disguise; the
Offering requires that live owned token and altar proximity. The normal item
route opens only the exact earned Wooden Box. Recovery options are available
at Chang/Hong/Zhao. Login abandons orphaned saved attempts; when a previous
session still owns an actor, scheduled cleanup must retire it first, after
which NPC recovery resumes the task. Disguise capacity responds without charging
or fencing a player. NPC text stays brief and stage-specific.

The server projects missions 1723..1726 from saved progression and protects them
from forged client Begin and NPC abandon packets. The journal deliberately uses
saved stage status rather than asserting original native subtask counters.
Build and **484 checks pass**, including 406 prerequisites and 78 full-route
checks across rebirth 0/1/2: remote NPC rejection, forged Begin, ordered NPCs,
real Bandit invitation, disguise, altar/item rules, real owned Cloud death,
exact box opening, reload/armor return, duplicate final claims, bound P7 and
class-pack opening. The first fixture run omitted FixedOutputPacks.Initialize;
production Thread startup already calls it. Adding the matching fixture setup
produced the passing run. Logs: `obj/spun-gold-quest-build.log` and
`obj/spun-gold-quest-tests.log`.

`prepare_spun_gold_journal.py` composes over the Connector private overlay and
rewrites only these four missions. Three tests pass: routes/rewards/idempotence,
missing/duplicate missions, and ambiguous/missing fields. The private result is
`obj/chi-refill-validation/spun-gold-journal/`, SHA-256
`ea389442bc611fa747d68084dac807716d4aa23c60182c0f9d1458e8a1ad9e4f`.
Installed input, source overlay and approved DX9 executable hashes are checked;
unrelated journal bytes remain identical. It is unsigned, uninstalled and
unpublished. Native NPC/appearance/AI/journal, companion earning, concurrent
storage UI and real Host interruption acceptance remain pending. No production
schema or live process was changed. Achievement 10931 remains disabled.

## Authenticated earning and restart verification (2026-10-01)

The private `spun-wire` and `spun-wire-reload` lifecycle phases pass against
game assembly SHA-256
`37E6849089FBA067414F5D9E44A4BE332038904521DC5A822B7F924274652C6F`.
Both phases start the full server and authenticate an ordinary, non-GM,
non-reborn character through SRP/DH and normal world entry. The first completes
Chang/Hong/Zhao interactions, Bandit combat, Offering use, the owned Cloud
encounter, Wooden Box use and the final claim through socket packets. Combat
uses loaded monsters and ordinary melee; no forced death or damage override is
used. Both final reward UIDs and their bound flags are observed on the wire.

After graceful shutdown and a PostgreSQL restart, a fresh server process and
authenticated login preserve the exact quest row and saved item fingerprint
(UID, type, binding, enhancement, sockets and storage location). Repeating the
final claim leaves that fingerprint unchanged in both phases. The fixture
checks different game process IDs and PostgreSQL start times. Evidence:
`obj/spun-wire-host-1.log`, `obj/spun-wire-host-2.log`; reusable harness:
`tools/CheckCandidateSpunWire.cs` and the two lifecycle phase names above.

Scope: the harness supplies proximity by teleporting the player and controls
quest RNG. It reuses a previously prepared level-100 character and does not
prove fresh-character levelling, native navigation/rendering, companion combat,
random drop rates or crash recovery during an unfinished encounter. Those
checks and live deployment remain separate. No production gameplay code,
client files or live service changed in this batch. The earlier disabled-10931
note is historical; its later activation and aggregate evidence are documented
in `EQUIPMENT_QUEST_AGGREGATE.md`.

## Companion ownership and saved credit (2026-10-01)

The combat fixture now covers registered companion credit. Unregistered,
foreign-owned, replaced and wrong-map companions leave the private Cloud's
health and quest stage unchanged. The current registered companion passes the
attack guard and its lethal receive-attack callback credits the owner, produces
one Wooden Box, refuses repeated death credit and preserves the exact box UID
and stage through normal save/load. Nine added assertions pass within the
430-check prerequisite/combat run (`obj/spun-companion-tests.log`), against the
same assembly hash recorded above.

The fixture supplies the companion, position, RNG and lethal damage amount;
this proves authorization and durable owner credit, not companion AI,
calculated damage balance, native rendering or a separate process restart.
The private runner needed the existing assembly binding redirects and current
LocalTeamPkLedger/ProgressionEvidence initialization before the focused branch.
Initial fixture setup failures did not reach these assertions. No production
gameplay change was required, and no live service changed.

## Retained Spun Gold and Azure Pill batch (2026-10-02)

The retained level140 Water Taoist completes Spun Gold in life2: Chang's ordinary
option61, Hong, calculated family7 combat, Zhao's disguise, bait729972 at100,430,
calculated combat against the owned9921723 encounter, normal cleanup, thread729973
use, Hong's return and Chang's final claim. It receives bound728237 and a bound
P7 soul. The fixture selects successful Spun Gold material rolls but never assigns
quest stages, materials, damage or completion receipts directly.

The same batch then follows Azure Pill through Scholar Wu, Mu Mu, four calculated
family67 kills producing one eye and three meat items, and the herbalist's two
herbs. Returning to Mu Mu consumes the ingredients and starts the saved brew.
An immediate retry does not finish it. Full game/database restart preserves the
completed Spun Gold rewards and pending brew. The fixture advances the quest
clock just beyond the saved ready_at, receives and delivers pill721698, then
claims bound728238 plus another bound P7 soul from Chang. Both chains together
increase silver by exactly3,100.

Another full game/database restart preserves the exact nested career and all
four reward UIDs. Repeated final-claim requests for both chains are unchanged.
`career-retained-early-{brew,claim,reload}` pass on candidate SHA-256
`34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`.
After replacing fixture SQL table-name concatenation with two fixed parameterized
queries, the cleaned reload passes again. Probe `revora_retained_early_probe`
finishes at step3. A fresh read-only aggregate audit finds3/19 final receipts:
Connector, Spun Gold and Azure Pill. Achievement10931 remains unearned.

Random drops, proximity, attack cooldowns and the brewing clock are controlled;
this does not establish natural timing, combat survivability or native UI.
Prior funding/qualification provenance is unchanged. The existing private house
box provides capacity without deleting earlier items. No further production
code, binary, live deployment or client release changed in this batch.
