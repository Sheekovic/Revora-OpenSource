# Evil Abyss: second-rebirth quest restoration

2026-09-16. M6.55c3a: installed-world audit and pure stage/material planner;
M6.55c3b1: durable run, material, encounter and reward transaction participants.
M6.55c3b2: native quest journal overlay and saved keeper introductions.
M6.55c3b3: registered combat encounters, wild-life credit and final completion proof.
M6.55c3b4: ordinary NPC admission, offering confirmations, keeper reports and reentry.
The full quest milestone M6.55c3 remains open. The complete NPC/material/combat
journey is compiled and exercised only in isolated validation. Production startup
does not initialize it, and the Sereth entry stays hidden until initialization.
Quest-funded rebirth is implemented under M6.55c3b5, and scoped PvP/death rules
under M6.55c3b6. M6.55c3b7 stages and verifies native boss-profile loading.
Rendered client acceptance and combined client deployment remain open before
production activation.

## Authoritative contract

The official [second-rebirth quest](https://co.99.com/guide/quests/2reborn.shtml)
describes three Pure Vigors, 40,000 meters of grievance, nine ordered rounds of
eight spirits, then three consecutive Satan forms. Its token shortcut is a
separate route, already implemented under [M6.55c2](SECOND_REBIRTH_RESEARCH.md).
The installed genuine-6711 `ini/Questinfo.ini` independently confirms the
material quantities, exact grievance weights, ordered spirits and six native
mission identities below. No new client mission is needed for this original
quest; the existing custom Cup reuse of those identities must be separated.

| Native mission | Owner NPC | Prerequisite |
| --- | --- | --- |
| 2414: first trial | Arthur 3601, map 1700 (607,637) | One rebirth, level 120 |
| 2415: meet Bryan | Bryan 3602 (609,633) | 2414 |
| 2416: grievance | Bryan 3602 | 2414 |
| 2417: meet Angela | Angela 3603 (611,638) | 2416 |
| 2418: binding curse | Angela 3603 | 2417 |
| 2419: final trial | Angela 3603 / Satan Seal 3608 (331,340) | 2418 |

Highest promotion is required by the existing final rite policy; the published
quest admission itself is one rebirth and level 120. Water's level-110 first
rebirth exception does not lower this threshold.

## Material and encounter identities

The installed server catalog and matching native quest targets establish:

| Action | Exact payment | Result / family |
| --- | --- | --- |
| Earth Seal 3605 (444,878) | Moss 722723 + Dream Grass 722724 + Soul Aroma 722725, one each | Hill Spirit 3632 -> Ghost Horn 722722 |
| Arthur | Three Ghost Horns | Evil Tooth 722721 |
| Water Seal 3606 (883,447) | The three base materials + one Evil Tooth | Swift Demon 3633 -> Feather Stone 722726 |
| Arthur | Three Feather Stones | Immortal Stone 722728 |
| Fire Seal 3607 (920,929) | The three base materials + one Immortal Stone | Banshee 3634 -> Vigor Fragment 722729 |
| Arthur | Three Vigor Fragments | Impure Vigor 722730 |
| Cleansing Stove 3609 (715,744) | One Impure Vigor | Cleansing Devil 3635 -> Pure Vigor 722731 |
| Arthur | Three Pure Vigors | Grievance stage |
| Satan Seal 3608 | One Squama Bead 722727 | First Satan form; later forms belong to that same encounter |

The recursively expanded three-vigor recipe requires exactly **117 of each base
material**, 81 Hill Spirits, 27 Swift Demons, nine Banshees and three Cleansing
Devils. The public guide suggests collecting 119 of each; that is not an
additional two-item debit supported by any recipe. The implementation must
consume the exact individual recipe payments, not an arbitrary bulk entrance fee.

The native grievance description lists eight groups of four families. Installed
families 3600..3631 match those named groups, with legacy casing/spelling defects
such as `CryingSerpant`. Each group earns **1, 6, 20, 500** meters respectively.
Summoned material bosses and unrelated monsters do not earn grievance under
this mapping. Cap progress at 40,000; require Bryan's explicit turn-in before
the spirit stage.

Spirit order is families **3636..3643**: Andrew, Peter, Philip, Timothy, Daphne,
Victoria, Wayne, Theodore. Repeat the complete order nine times, for 72 verified
kills. Ignore out-of-order/foreign encounters; do not let duplicate death calls
or an arbitrary total-kill counter bypass the order. Angela authorizes the final
trial after the ninth round. Only families **3603, 3607, 3611, 3619** qualify as
Squama Bead sources (Demon Lord, Serpent Spirit, Fiend Lord, Hades Lord). The
installed client resolves the public page's ambiguous ?Hardest Lord? wording.
Drop probabilities are not established by either source and need a documented
Revora economy choice plus bounded ownership/loot fixtures.

## Installed world findings that change the implementation

A read-only PostgreSQL transaction inspected only map/NPC/monster metadata:

- Map 1700 exists, base map 1700, status 8195. Its 1246-by-1246 server terrain
  has 398,529 tiles in the eight-direction component reached from (601,641).
  All 30 installed NPC positions in this map are in that component. This proves
  terrain connectivity, not native pathfinding or complete server movement.
- The map has 101 spawn records, including the 32 grievance families, material
  bosses, an orphan 3646 spawn and additional unrelated families. Ordered-spirit
  NPCs exist; those NPC identities are distinct from similarly numbered monster
  families. Do not confuse VictoriaCurse NPC 3632 with Hill Spirit family 3632.
- The existing Water Seal summons family **3170**, whose catalog name is `3170`,
  level 83, life 23,346, attack 656..726. It is not Swift Demon 3633.
- Swift Demon 3633 currently has 30 million HP and an inverted attack interval
  (minimum 5,000, maximum 1,000). CryingSerpant 3606 has level 500 and 50,000
  attack. These are edited server values, not established original quest balance.
  Review and define map-scoped Revora encounter profiles before activating them.
- Families 3644 Satan and 3645 BeastSatan exist. **3646 is missing**, although a
  map spawn references it. 3647 is a separately named RulzanSatan, so silently
  using it as verified Fury Satan would be unjustified. The pure planner records
  the referenced third-phase identity; it cannot make that missing family usable.
- Arthur and Bryan are tied to `TareaCopa` custom Cup progression and require
  50 Pure Vigors / 400,000 meters. The monster hooks use the same native quest
  IDs but increment different legacy counters, including 2/4 instead of 6/20,
  and a non-ordered spirit count. Native text, NPCs and combat thus disagree.
- No ordinary Sereth-to-Abyss admission was found in the current NPC flow.
  Alex 3600 exists in Ape City, but does not provide the new trusted quest route.

These are specific repair prerequisites, not reasons to stop implementation.
Existing Cup progress must be preserved explicitly, never interpreted as an
Abyss completion receipt or silently overwritten through shared native IDs.

## Runtime contract and remaining acceptance

1. Add durable per-player run, selected-route/projection and owned encounter
   records, with canonical ordinary-player eligibility. Add short Sereth entry
   and return dialogue, valid landing and reentry after reconnect.
2. Separate native missions 2414..2419 from the custom Cup state before projecting
   them. Preserve existing Cup counters/quest data and isolate its legacy drop
   hooks. A client Begin/Finish/quit packet must never author authoritative stages.
3. Implement ordinary hunted/purchased materials and checked three-for-one
   exchanges. Validate exact owned item identities, stacks, binding and capacity;
   commit debits, output, stage and receipts together. Do not assume extra
   inventory space or erase a summoned entitlement after a failed projection.
4. Repair the reviewed world definitions and implement paid, owned summons with
   one live encounter per entitlement, exact death identity and recoverable
   pending rewards. Reconnect/restart must not consume a second offering or
   duplicate a reward. Verify all seals and spirit locations through real movement.
5. Apply the grievance/ordered-spirit/Satan rules only after canonical combat and
   stage checks; keep receipt writes and advancement atomic. Preserve documented
   Abyss PvP and quest-item death behavior after reviewing existing death hooks.
6. The final third-form completion commits **11031** once. Sereth then consumes
   that saved quest entitlement as an alternative to a token in the existing
   second-rebirth transaction, which separately earns **10316**. Neither raw
   client bits, Cup counters nor one loosely named Satan kill suffice.
7. Validate the full non-GM journey, faults, duplicate clicks/deaths, full bags,
   abandoned summons, reconnect/restart, ordinary combat and genuine-client
   quest presentation before deployment. No blanket claim of original balance
   or source-exact drop rates is made.

## Durable participants (M6.55c3b1)

`AbyssTrialStore` owns one first-life-bound run per character. It records stages,
versioned confirmations, selected Abyss/Cup route, exact paid operations and item
preimages, one outstanding encounter, wild-monster life receipts and unclaimed
material rewards. It checks the canonical ordinary session, first-rebirth/level
eligibility, saved character identity/location, owner lock and nearby NPC for
admission and offerings. Mutations require a caller-owned database transaction.
No GM name, rank or character allowlist is used.

`AbyssTrialMaterials` selects the exact recipe across owned permanent bag items,
including legacy zero-stack singletons. It validates cached and saved ownership,
binding, quantity, lock, suspicious/wardrobe and expiry fields, records each debit,
and checks every required write. A three-for-one exchange can use a full bag only
when its debit frees the output slot. Bound ingredients bind the crafted output;
bound Pure Vigors or the final bead bind the saved completion entitlement.

Resuming a paid encounter returns the same durable identity without another
charge. The returned `Created` flag distinguishes a new payment from a resume;
only a newly committed payment may update the inventory cache. Kill receipts and
stage advancement commit together. A material kill records its pending reward
even with a full bag. Claiming inserts the item and marks its receipt in the same
transaction. Failed writes roll back the whole operation; a failed post-commit
projection must fence the session, then reload the saved state. Concurrent or
stale confirmations cannot spend the offering twice.

The store applies the reviewed logical family order but does **not** prove a
monster was actually killed. The world participant described below establishes
actor/owner/life identity and writes 11031 in the final transaction. A terminal
store-only fixture grants neither 11031 nor 10316 and creates no trusted world
completion receipt. The second-rebirth rite does not consume this entitlement yet.

Wild material policy is explicitly Revora balance, not an inferred retail rate:
50% total base drops (16% Moss, 17% Grass, 17% Aroma; 50% none) during the first
trial, and 25% Squama Bead from the four reviewed lords before Satan's first form.
One server-generated monster life can be recorded only once across owners. Loot
is retained as a pending owned claim, rather than discarded on a full bag.
NPC/world delivery and PvP death protection remain integration work.

No Cup data is migrated or rewritten. The fixture preserves `TareaCopa` and the
serialized native quest entry through real character reload. The legacy
`SecoundQuestPoint` and `SecoundQuestDone` properties are memory-only in existing
`EntityTable`; the fixture proves Abyss leaves them unchanged in memory, not that
the old server persists them. Neither value qualifies as Abyss completion.

Remaining integration must load/refresh the journal on actual admission and login,
add short NPC dialogue and reentry, protect quest materials on Abyss PvP deaths,
and consume the earned entitlement in the existing second-rebirth transaction.
The full ordinary-player quest and genuine-client acceptance stay unchecked.

## Native journal overlay (M6.55c3b2)

`AbyssTrialJournal` overlays missions 2414..2419 in the existing paged 1134 list
without changing `p.Quests`. Saved route selection restores the original Cup list
when Abyss is deselected. The store now saves Bryan's and Angela's introductions:
grievance kills require Bryan's acknowledgement, and spirit summons require
Angela's. Each stage projects one actionable native mission, then the appropriate
finished and future statuses. The first trial's item checks remain client-owned
inventory presentation; no fabricated item completion is sent.

The existing 1135 objective packet sends saved grievance as 2416 counter 1 and
ordered spirit progress as 2418 counters 1 (complete rounds) and 2 (current round).
At a just-completed round, counter 2 stays at eight until the next Andrew starts
the next round; final progress is nine/eight. This follows the installed
`chk_quest` indices and existing packet serializer, not a new native capture.

For a selected Abyss run, a client Begin or bounded 2032 quit request can refresh
the journal but cannot accept, finish, erase or rewrite either its authoritative
run or underlying Cup entry. Failed journal loads fence the session instead of
falling through to a state mutation. A replacement connection has its own cache;
the superseded session cannot use it. Runtime activation remains off until the
complete quest initialization is connected.

## Registered combat participant (M6.55c3b3)

`AbyssTrialWorld` allocates map-1700 actors with distinct server family identities
9923632..9923646, bound to the saved paid encounter and canonical player session.
Only the owner or their registered companion can attack the encounter, receive
its retaliation or author its death. Other players remain in the shared world;
the service does not turn the whole map into a private instance. The selected
Abyss route suppresses the old map-1700 quest-drop region, including the loose
Satan one-kill completion/teleport, without rewriting Cup quest data. Unselected
Cup players retain that legacy route.

At most 32 paid actors exist concurrently. Each live actor lasts 30 minutes;
expiry, departure or replaced/disconnected sessions release the actor, preserving
the paid entitlement. Reentry can recreate that same encounter without charging
again. No public client packet can supply an encounter or monster-life UUID.
Canonical wild deaths use one server-generated life identity; respawn explicitly
starts a new life. Repeated corpse notifications cannot add grievance or loot.

Paid encounter deaths save the defeat, exact stage change and pending material
reward before removing the actor. A failed save leaves the encounter undefeated
and fences the session for reload. Full bags retain their pending material; claim
and item insertion are atomic. On the final third-form death, a distinct
`revora_abyss_completions` receipt, terminal run state and 11031 commit together.
Failure of any required completion/evidence write rolls everything back. A token
rite, Cup counter, free-standing Satan or terminal store fixture cannot create
that receipt. This batch does not grant 10316.

The following are explicit **Revora balance choices**, not original CO values:

| Creature | HP | Attack range | Defense | Scope |
| --- | ---: | ---: | ---: | --- |
| CryingSerpant | 50,000 | 1,400..1,728 | 0 | Existing/new family 3606 actors in map 1700 only; level 120 |
| SwiftDemon | 60,000 | 2,000..4,000 | 200 | Paid second material encounter only |
| Satan | 100,000 | 4,500..6,500 | 2,000 | Paid first final form |
| BeastSatan | 150,000 | 5,500..7,500 | 2,250 | Paid second final form |
| FurySatan | 200,000 | 6,500..8,500 | 2,500 | Paid third final form |

Other paid profiles retain the loaded family HP/attack/defense, use level 120 and
the existing boss-state/full-HP projection. Family 3646 is a scoped Revora profile
using the catalog-backed mesh 103 from 3647 as a presentation template. That is
an explicit appearance choice, not proof that 3647 is the original Fury Satan.
The shared monster catalog and persisted spawn rows are unchanged.

Read-only decoding of installed `6711/ini/monster.dat` (SHA-256
`99FA2C8F14C3851D797A0B20FBD325053F9ACEE35C7DAB4C5E73EDC2F5792585`)
found `SwiftDevil` TypeID 3633 / MaxLife 55,000, but no exact `SwiftDemon` entry.
It contains `FurySatan` TypeID 3646; all three native Satan profiles have MaxLife
65,535. The reviewed material/spirit/final entries have `Boss=0`. Consequently,
the actor's server boss bit alone does not prove correct client HUD behavior:
exact-name/MaxLife/Boss compatibility profiles must be staged, preserving legacy
Cup presentation, then checked through the native reader and genuine client.
No active client file or download was modified in this batch.

## Ordinary keeper conversations (M6.55c3b4)

`AbyssTrialQuest` validates all 29 installed talker identities and coordinates
before enabling its dispatcher. Sereth offers entry to ordinary first-reborn
characters at level 120. Arthur handles the three exchanges and three-Pure-Vigor
offering; Bryan and Angela save introductions and accept only completed stages.
The four seals, eight spirit identities and Satan keeper offer only the player's
current challenge. Bruce returns the hunter beside Sereth. Short dialogue uses
the existing NPC packets and the saved journal provides detailed objectives.

Offers belong to the current session, NPC, run and version, expire after two
minutes, and snapshot exact item identities, stacks and binding. Cancellation,
movement, trade, malformed requests and changed materials do not charge. The
payment transaction rechecks location and expiry before committing. A paid
challenge survives disconnect or its actor's lease: the keeper resumes the same
saved encounter without another offering. Claim buttons recover saved full-bag
spoils. Sereth can restore the Cup route after outstanding encounters finish;
original Cup entries and counters are preserved.

The fixture uses actual 2031 NPC-open and sealed 2032 dialogue dispatch for the
whole chain, canonical monster death dispatch to hunt all 351 base ingredients,
and registered owned deaths for all 120 paid material encounters, 72 spirits and
three final forms. It kills 80 canonical lords for grievance and hunts a Squama
Bead. Separate actual server-movement paths reach 17 representative installed
keepers (including all spirit identities) and return through Bruce. Combat loops
use fixture positioning and explicit death calls; this does not claim native
attack timing, player input, rendering or full movement between every kill.

## Quest-funded second rebirth (M6.55c3b5)

Sereth prefers a completed owned Abyss trial over an Exemption Token, and says
which payment the confirmation will use. `AbyssRebirthPayment` requires the
saved run's exact first-life identity, version and binding plus the world
completion proof joined to its defeated third-form summon. Terminal counters,
legacy Cup progress and an achievement bit alone do not authorize the rite.
The chosen proof is consumed in the same transaction as the second-life profile,
equipment, skills, proficiencies, gift, receipt and achievement 10316. The token
stays untouched; the bag calculation does not count it as a freed slot. Bound
trial offerings produce a bound gift. Achievement 11031 remains the final
combat reward and is not manufactured by either rebirth payment path.

The receipt adds `quest_run` and `payment_kind`; existing receipts default to
`exemption-token`. A check ties token and quest payment fields together and a
unique index permits only one receipt per quest run. The existing second-life
recovery uses the same recorded class, level and EXP for both payment paths.
No legacy receipt is replaced or inferred from a reborn flag.

Outstanding paid encounters or unclaimed items block both payment paths until
resolved. An ordinary keeper resumes the paid fight, and Sereth/Bruce can return
saved spoils. Successful quest payment clears the selected overlay and consumes
the entitlement; a token bypass clears the overlay while preserving unfinished
history without marking it completed. Both restore the untouched Cup journal.
Runtime activation remains pending the remaining world/client work.

## Public Abyss PvP and death rules (M6.55c3b6)

The official guide, rechecked on 2026-09-16, explicitly permits PK in the Abyss
without PK points or an enemy record and protects quest materials from death
there. `AbyssTrialWorld` now identifies canonical ordinary sessions in the exact
public map 1700, independently of whether they selected the Abyss or Cup route.
Its attack hook suppresses the flashing-name crime flag after the normal Peace,
team, spell, attackability and other permission checks. Actual player death skips
PK points, kill/enemy records, guild PK donation and crime-related rewards for a
pair in that world. This does not make the map peaceful or confer attack rights.

The existing death-drop loop skips quest items 722721..722731 only while their
canonical owner is in the public Abyss. It covers player and monster kills and
all eleven materials, including unbound high-PK inventory. Other items keep the
existing drop behavior, and the same materials can drop outside the map. Existing
black-name jail behavior is retained; this change does not claim that policy as
verified retail behavior. No global PK-map list or shared item metadata is edited.
The rules remain gated by world initialization, which production still omits.

## Native boss profiles (M6.55c3b7)

All fifteen owned encounters now have distinct display names and reviewed HP
values in `AbyssTrialWorld.HudNames/HudHealth`. The source fails initialization
if a loaded template's effective HP disagrees with that contract. These names
leave shared Cup and loose-monster catalog names intact:

| Logical family | Display name | MaxLife |
| --- | --- | ---: |
| 3632 | Hill Spirit | 50,000 |
| 3633 | Swift Devil | 60,000 |
| 3634 | Vigor Banshee | 60,460 |
| 3635 | Cleansing Devil | 65,535 |
| 3636..3643 | Cursed Andrew/Peter/Philip/Timothy/Daphne/Victoria/Wayne/Theodore | 65,535 each |
| 3644 | Sealed Satan | 100,000 |
| 3645 | Beast Satan | 150,000 |
| 3646 | Fury Satan | 200,000 |

`tools/ClientPatches/prepare_abyss_boss_profiles.py` checks the approved DX9
executable and active catalog hashes, reads this server contract, and appends
fifteen matching profiles to a private copy. It encodes header spaces with `~`,
sets Boss=1/Level=120/MaxLife, preserves every original decoded catalog byte,
round-trips the encrypted file, and refuses to overwrite a differing staged
artifact. Originals, staged content and a manifest stay ignored under
`obj/chi-refill-validation/abyss-boss-hud/`. No patch version, public archive,
active-client installation or signed release is produced.

`tools/ClientPatches/Verify-BossProfiles.ps1 -Profile abyss-boss-hud` runs the
existing Revora-owned x86 reader against disposable, exact-hash copies of the
approved `GameData.dll` and its fixed dependency set. Its bounded loader/lookup
seams are documented in [BOSS_HUD_6711.md](BOSS_HUD_6711.md). Before staging,
all fifteen aliases fail lookup; afterward all resolve with the expected HP and
Boss=1. Both variants also verify the fifteen original names still return their
original HP and Boss=0: **60 native checks**. The original daily/Rush/Specter/
Terato before/after mode passes **22 regression checks**.

Catalog preimage: `99fa2c8f14c3851d797a0b20fbd325053f9acee35c7dab4c5e73edc2f5792585`.
Private postimage: `df6431be254159ce83a812532b6f24330df799bfec66e1cfc2ba6436b5d6970a`.
Abyss native probe: `F2EF13B30E9335A4CC90D94B7782271519F60BC933493C320C5D290CF273C120`.
Native logs: `obj/chi-refill-validation/abyss-native-hud.log` and
`obj/chi-refill-validation/boss-profile-abyss-regression.log`.

This proves native data loading, not the rendered bar, model or interaction in
a logged-in genuine client. The private aliases must be composed with other
pending client content and visually accepted before a separately approved
installation/publication and production quest initialization. The explicit
Fury Satan mesh choice remains as documented above.

## Validation and provenance

`AbyssTrialRules` implements only immutable progress planning, eligibility,
recipes, material-boss outputs, named grievance weights, ordered spirits, bead
source identities and chained Satan progression. It does not read/write game
state, charge players, spawn monsters, send packets or create achievements.
The constructor rejects impossible stage/counter combinations before mutation.

The isolated build and **223 checks** in
`tests/PotencyHunt/Verify-PotencyHunt.ps1 -AbyssRulesOnly` pass. Tests recursively
execute the full three-vigor dependency graph with exactly 117 base materials,
check all grievance groups and caps, run nine complete ordered spirit rounds,
reject foreign/wrong-order encounters and validate all three final phases.
These are planner fixtures, not proof that an ordinary player can finish the
quest in the current server. Three producer-audit checks also pass; coverage
remains 223/337 and no new trusted achievement producer is claimed.

| Inspected evidence | SHA-256 |
| --- | --- |
| Installed `ini/Questinfo.ini` | `F783C2A8EAD9461A469C79B6C7E7AAE00B2604066D4BC6E2E2C56D7E98FC5E14` |
| Official quest HTML, private archive | `8ABCA0F59F418D5C937B8FF9ACDFB1174F05BC3E9ADC783F07222A3B42667812` |
| Installed server `database/maps/1700.map` | `8026193EC8C8CB7F1934CCF7C2E713931A0C65269C8D7A0B67209019BED17D1B` |
| Read-only catalog observation | `AB42CCFFA3632D9F1B4E39EDD86713AB2CC78439D0CD5A381165654957FAF916` |
| Revora-owned x64 metadata probe | `097D46B9018C69FB318BCF733DB0A8B6B0BCEB21CFA56BC1C6B3602FB21766FE` |

The probe source, executable and output remain ignored under
`obj/chi-refill-validation/` (`AbyssProbe.cs`, `bin/AbyssProbe.exe`,
`abyss-catalog.txt`). It uses the existing protected connection configuration,
issues `SET TRANSACTION READ ONLY`, selects only map/NPC/monster metadata, and
rolls back. No credentials, character data, raw client catalogs or database dumps
are committed. Official HTML is under ignored
`research/isolated/second-rebirth-6711/2rebornquest.html`.

Rule-test log: `obj/chi-refill-validation/abyss-rules.log`.
Staged server SHA-256:
`92DC05A9E7891E441A1C1C637F1C14E8FB5E3595CC6340684740C4C861AFCB4D`.
Fixture executable SHA-256:
`3C5EFF7EBFBFE6ED233203593CE1B33F3BC49E23EDCEC649A10F1AE5C0DCF86F`.
The live server, database, client, patch downloads and website are unchanged.

The subsequent store batch builds in the same isolated output. Its
`-AbyssStoreOnly` fixture passes **1,062 checks**, including first-rebirth prerequisites and planner
checks, then executes all 120 material encounter payments/defeats, the complete
three-vigor graph, 40,000 grievance, 72 ordered spirits and all three final logical
phases against the disposable `revora_potency_validation` schema. Inputs and
logical death notifications are fixture-driven; this is transaction validation,
not a claim of packet, pathfinding or combat completion. It also exercises bound
recipes, Cup isolation, production reconnect, saved pending loot, rejected
metadata changes, suppressed writes, full bags, concurrent confirmations and a
lost post-commit projection. No new achievement producer is counted (223/337).

Store log: `obj/chi-refill-validation/abyss-store.log`.
Staged server SHA-256:
`2504B6EFB8FB38B5D423E88163AB92FF8DBFCA570F60AC6681B89B82DC0A4816`.
Fixture executable SHA-256:
`2D25C1FBE0BF52D51266DDDE7BC51C46742C85548CDA3D4E7854965F09C4609E`.

The subsequent isolated journal build and **1,101 checks** in `-AbyssJournalOnly`
pass, including the full store suite with required keeper introductions. It
checks real Begin/quit dispatch, malformed bounded requests, exact status and
objective packet fields, route switching, all round boundaries, production
reload and preservation of all six serialized Cup objective values. Three
producer-audit tests pass; trusted source coverage stays 223/337. Genuine-client
rendering, NPC admission and actual-world earning remain unvalidated.

Journal log: `obj/chi-refill-validation/abyss-journal.log`.
Staged server SHA-256:
`8D3DE6F0D124A085E6CF6B2F16BD3602C12242C124B8F116FFFB64519FFE877A`.
Fixture executable SHA-256:
`19B04FDB204BCB2C2C53A4A8C1E78C17632DF813985252DBA4B697A639D360CB`.

The subsequent world build and **1,339 checks** in `-AbyssWorldOnly` pass. This
includes store/journal prerequisites, all 120 registered paid material deaths,
the complete 72-spirit sequence and three final forms, real monster `DieCore`
material dispatch, canonical companion credit, foreign/copied actor rejection,
lease/reconnect recovery, full bags, session changes during payment, final proof/achievement fault rollback and
legacy Satan isolation. The 80-lord grievance loop uses canonical registered
corpse/respawn notifications; fixture positioning and the initial 117 base
ingredients are supplied explicitly. It is not a full NPC, movement or native
combat-input journey. The guarded timer call compiles in the final isolated build;
its underlying release/resume behavior is exercised directly by the fixture.

Three producer-audit tests pass. The source inventory is now **224/337**, with
113 IDs still lacking a trusted producer; this remains a source upper bound, not
a completed ordinary-player achievement route or activation claim. The full
quest and native/client/deployment milestones remain open.

World log: `obj/chi-refill-validation/abyss-world.log`.
Staged server SHA-256:
`E1A4B13D0E91E3C88D37B717F3FFF692C7809CC6085B3B2B4D1C88F4BE050689`.
Fixture executable SHA-256:
`61F1A7EA4F798F81EF2FCF6C8DA95F6B31B620403C816803DF273F50902FB808`.


The NPC batch builds and passes **1,595 checks** in `-AbyssQuestOnly`, including
all world/store/journal prerequisites. Rejection tests cover missing, cancelled,
expired, changed, duplicate and wrong-NPC offers; malformed frames; admission
during trade, at a distance or below the required level; and payment expiry
after transaction writes, followed by production reconnect. Three producer-audit
tests pass, with source coverage unchanged at 224/337. The final rebuild changes
only indentation after the tested build. No production initialization, deployment
or native-client acceptance is claimed.

NPC log: `obj/chi-refill-validation/abyss-quest.log`.
Final staged server SHA-256: `8365D3B9E378388B2AE952FF3CE6B1DA51889236F8AB87B9DAAF6434B5E37DD6`.
Fixture executable SHA-256: `67F4BCFDA0C5180D1EBB85F2B6A9D1470AD99AE52BDDF32DABB0E38DE46C9110`.


The earned-rite batch passes **1,621 checks** in the expanded `-AbyssQuestOnly`
suite and **1,826 checks** in the independent `-SecondRebirthOnly` regression
suite; their first-rebirth prerequisites overlap. New checks retain the actual
NPC-earned final proof through suppressed run/profile/item/skill/proficiency/
receipt/evidence writes; reject altered run versions, binding, history, proof
ownership, final-family identity and consumed state; and exercise cancellation,
expiry, full bags, concurrent confirmations and production reload. The independent
bound world journey proves rebirth with no token, a bound gift and recovery of
the EXP actually earned during its hunt. Outstanding paid encounters and saved
loot block a token bypass until resolved. Synthetic terminal counters without
world proof do not fund rebirth. The token suite rechecks all class transitions,
1,000 three-life skill histories, equipment, write faults and recovery.

Build and three producer-audit tests pass. Coverage remains 224/337.
Logs: `obj/chi-refill-validation/abyss-rebirth.log` and
`obj/chi-refill-validation/second-rebirth-abyss-regression.log`.
Staged server SHA-256: `D87DEC70D0DF5830352722DC8AAAB932CD124B6252DBDEF2045D1AA0648C5D25`.
Abyss fixture SHA-256: `D51069EABBCC5EBC3C8CD5A32526C647C266F00F07C94E5AEF3FBEEA81EDD6BC`.
Token regression fixture SHA-256: `CA0DBF7D0A94F5ED07537A9147BE32D8140CC923B43AB0555DD03AABE654FD06`.


The PvP batch builds and passes **1,648 checks** in `-AbyssQuestOnly`, including
all NPC, owned combat, earned rebirth, journal and transaction prerequisites. New
checks use actual `CanAttack` and player/monster death paths: ordinary unselected
players can fight without crime flags; Peace and attackability remain enforced;
PK points and durable enemy/kill records stay unchanged; all eleven quest items
are protected; high-PK deaths with guaranteed drop rolls preserve their durable
inventory; and reconnect keeps those items. Positive controls prove unrelated
items still drop in Abyss and quest materials still drop outside it. Altered map
identity and superseded sessions do not receive protection. Three producer-audit
tests pass; coverage remains 224/337.

Log: `obj/chi-refill-validation/abyss-pvp-quest.log`.
Staged server SHA-256: `E557DC0909D3E07AB19D63196E8BCA83D3BCFB743122326AE4B742F0D9E871C3`.
Fixture SHA-256: `C582896C480738B19E0BA7A13518DF9B4369780326C0DA0B74511932F7B302A6`.
No live server, database or client deployment occurred.


The boss-profile batch builds and passes **1,784 checks** in `-AbyssQuestOnly`.
It includes the full ordinary NPC/material/combat/rite/PvP journey, independent
exact-name/HP expectations for all fifteen profiles, serialized full/half/one/zero
HP spawn and damage updates, and rejection of catalog HP drift. Private staging
is repeatable and leaves the active catalog hash unchanged. **60 native Abyss
checks**, **22 existing-boss native regression checks**, and three producer-audit
tests pass. Source coverage remains 224/337.

Log: `obj/chi-refill-validation/abyss-hud-quest.log`.
Staged server SHA-256: `34988D560E8E90CCB5E63EEE8B66778CDA3C09D8AC792E3C87AB6DA551D3862A`.
Fixture SHA-256: `8D01C1FC2148AA4744E760014B78B30ACB371AB2722337EA83FB8745C60AD97D`.

## Final rite reward update (2026-09-21)

The owned final-form victory earns the existing Abyss entitlement; Sereth's
newly confirmed second-rebirth rite now also grants an unbound P7 soul. The soul
is committed with consumption of that entitlement and the character transition,
not through a separate combat callback. Unclaimed spoils still block rebirth.
See [transaction and receipt details](SECOND_REBIRTH_RESEARCH.md#valuable-final-rite-reward-2026-09-21).
