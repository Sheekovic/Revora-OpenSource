# Pirate Epic restoration

2026-09-27, reviewed against source `9e4cd192`. Research prerequisites only;
the quest is not implemented or enabled. Both pirate mastery suppliers remain
unproven. This document does not authorize a live deployment.

## Original quest contract

The [official quest guide](https://co.99.com/guide/quest/pirate_epic_quest.shtml)
describes level70+, second-reborn Pirates. Pirate Lord admits them to Sea of
Rage. A daily offering of three flowers of each color yields a bottle with a
star1-3 map; alternatively, a star1 map costs27 CPs. Map stages1-4 have map-reward
chances30%,25%,20%,20%, respectively, with output ranges1-4,2-5,3-5,4-5.
Stage5 gives a Heart. Failed searches give other prizes. Luffy offers a direct
search service, and Mermaid exchanges Spring Water. A Heart converts one
equipped weapon; an ordinary right-hand rapier takes priority over a pistol.
The page includes a pre-release caveat. It does not establish secondary draw
weights, every consolation quantity or exchange prices.

## Installed identities

The installed `ItemtypeSub.txt` independently identifies:

| Item | Identity |
| --- | --- |
| Star1 through star5 maps | 3307443 through3307447 |
| Drift Bottle | 3307448 |
| Heart of the Sea | 3307449 |
| Longevity Spring Water | 3307450 |
| Oracle of the Sea | 3307451 |

`Questinfo.ini` entry792 supplies mission6868, type6, professions70-75,
minimum70 and Pirate Lord9391 at1004,58,34. These client coordinates differ
from the guide; actual loaded server placement must be checked before routing.
The journal names Heart3307449 and Water3307450 as rewards. It does not identify
Oracle3307451 as the quest's conversion material.

The Oracle handler in `Network/GamePackets/ItemHandler.cs` grants670439 and
671439 in separate writes then removes the pack. At39 occupied slots it can
lose the second output. Do not repurpose this distinct pack as a Heart or
introduce its issuance before its own atomic consumer is repaired.

## Terrain evidence

`tools/audit_pirate_epic_terrain.py` checks the exact client index, five archive
and extracted-content hashes, dimensions, access flags and observed object
record layout. [Numeric report](research/PIRATE_EPIC_TERRAIN.json).

The index maps10266-10270 to pirate02_new through pirate06_new, each100x100.
All five server `.map` files are absent in the private candidate. The native
files use version1006 with a different post-grid layout from the legacy
`Game/Map.cs` reader. The audit deliberately does not certify object collision
or install a floor from grid flags alone. Sea of Rage's hub identity remains
unverified; published hub coordinates cannot fit these100x100 areas.

Reproduce without modifying either source tree:

```powershell
python tools/audit_pirate_epic_terrain.py --client C:\Users\Administrator\Desktop\Revora\6711 --database C:\Revora\DisposablePostgres\moon-box-candidate\game\database --output docs/research/PIRATE_EPIC_TERRAIN.json
```

Input provenance: locally possessed approved6711 tree and private candidate
catalog. ItemtypeSub SHA256:
`6e318b1e53b0fb28036bd547b5cb78914e3e66b1272d8054259b6b0bcaa66aea`.
Questinfo SHA256:
`f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14`.
Client index and individual map hashes are embedded in the audit; proprietary
bytes and extracted maps remain outside Git.

## Staged conversion rules

`Game/Features/PirateEpicRules.cs` now defines the confirmed item identities,
eligibility, map-reward rates and one-weapon selection. It is compiled by the
standalone checks and the server project, but not yet connected to normal
gameplay dispatch. A normal right-hand rapier has priority; only a catalog-valid epic
rapier permits conversion of the left-hand pistol. Missing mappings cannot
silently consume the Heart or fall through to a different weapon.

`tools/CheckPirateEpicCatalog.cs` uses the actual merged item loaders without a
database or listeners. It verifies all686 ordinary weapon identities:309 of343
per family map to catalog-valid epic items with matching class, level and quality.
The34 unsupported identities per family end in0 (suffixes000 through330 in tens).
[Exact numeric result](research/PIRATE_EPIC_WEAPON_MAPPINGS.csv). These are
catalog checks, not native equip/render or ownership/transaction acceptance.
The base catalog SHA256 is
`1ed7a23b7a34b5e3706277f97e421e3cdf0b269eeb5be7367a4ebc0e4f22b3ef`.

`tests/PirateEpic/Verify-Rules.ps1` passes790 checks covering class/rebirth/level
boundaries, priority, missing target catalogs, swapped slots, unrelated items,
map identity bounds and invalid stage input. Both checks compile x86-compatible
framework code with warnings as errors. No game rebuild or deployment was needed.
The staged store now validates owned equipment snapshots and atomically consumes
a singleton Heart while preserving the selected weapon UID and enhancements.

## Private conversion transaction and restart

`PirateEpicStore` is compiled but has no startup initialization or gameplay
caller. `Prepare` requires an eligible ordinary player, primary equipment bank,
owned permanent singleton Heart, correct weapon priority, durable/cache item
agreement and a two-minute offer. `Commit` takes the owner lock and revalidates
the session and complete owned-item preimage. One database transaction consumes
the Heart, updates only weapon type/binding, inserts a bound P7 soul and records
the conversion. Operation and weapon-preimage uniqueness protect replay. The
selected weapon keeps its UID and remaining saved columns; binding is inherited
from either material or weapon. Post-commit projection updates inventory and
recalculates equipment stats.

The isolated build passed. `tools/Verify-ArenaProcess.ps1 -PirateStoreOnly` passed
`pirate-store` and `pirate-reload` with a PostgreSQL restart between processes,
against executable SHA256
`80274ECB940923706651503E7F150D8B1DA0C772FD971F5F708B917A7724A05D`.
The fixture uses an ordinary non-GM profile with supplied qualifications, Hearts,
weapons and enhancements. It proves:

- Forced receipt-insert failure rolls back material, weapon and soul writes.
- The same offer can succeed after a confirmed rollback; replay cannot award again.
- The first Heart converts611005 to671005, leaving the pistol unchanged; the
  second converts612005 to670005.
- Exact weapon UIDs, +12, -7, enchant255, two Super Tortoise gems and inherited
  binding persist through reload, together with two bound P7s and two receipts.

This is not a hunted/paid quest route or native UI test. The loaded Heart catalog has stack capacity0 (a nonstackable singleton);
oversized stacks must reject, rather than introducing unsupported stack behavior.
Ambiguous-commit recovery and ordinary acquisition remain to be verified before
any dispatch activation. Production schema and files are untouched.

## Conversion boundary checks

The expanded `-PirateStoreOnly` suite passes both game/database phases against
SHA256 `CC50D3120EA9B2146869FF330145E15CAD53CC29DE3B476F56F91B8CAD38A8C6`.
It rejects oversized Heart stacks, expired offers, false caller context and
changed saved equipment. At40 occupied inventory slots, consuming the singleton
Heart makes exactly one slot for the bound soul. Eight concurrent commits of one
offer result in one success; rollback/retry and both weapon/reward restart checks
continue to pass.

The review exposed a saved-rental preimage gap: cached permanent equipment could
have a different `days` value in persistence. `GearImage` now checks saved permanent,
non-warehouse, non-wardrobe and non-suspicious status before making an offer. The
fixture explicitly changes the saved rapier to a rental without changing its
cache and verifies rejection. The first expanded fixture run had an instance-field
reflection mistake; correcting its binding flags produced the passing runs above.
No production activation or new earning-source count is claimed.

## Private base-grid preparation

The terrain auditor now accepts `--stage <new-private-directory>`. It derives
all five100x100 base-access grids from exact hash-gated client archives, verifies
the version1006 record structure, writes five10008-byte server-format files and
read-backs them before publishing the staging directory. Its manifest retains
all native/object/admission gates as false. Existing destinations and paths
inside or enclosing the client/database trees are rejected.

The local run staged to ignored `obj/pirate-base-floors-v1`. All five resulting
hashes, dimensions and walkable-cell totals match the numeric report. No runtime
floor, client file or map index changed, and proprietary derivatives are not in
Git. These are base grids only: they must not be installed or called complete
collision maps before the remaining collision validation.

Hub research checked the installed10271 beach grid: the published NPC points
116,191;128,187;107,178 are inside its214x214 dimensions and have access flag0.
This is compatibility evidence, not proof of original Sea of Rage identity.
Its native file contains two portal records,56 kind4 objects and an additional
opaque trailer. Neither the portals nor the trailer is silently discarded for
runtime use. Obtaining the guide screenshot failed; original hub identification
and complete collision-aware staging remain open.

## Private paid map purchase

`PirateEpicStore.Purchase.cs` implements the confirmed27-CP one-star map offer.
A two-minute confirmation snapshots the owner, balance and owned items. Under
owner/item locks, one transaction debits CPs, inserts3307443 and saves an
operation receipt. Full bags and changed balances/items/context reject before
commit; the committed balance and item are then projected immediately. Purchased
maps are unbound, an explicit Revora paid-material choice. Alternate equipment
blocks weapon conversion only, not buying a chart. No NPC dispatch is enabled.

The isolated build and expanded `-PirateStoreOnly` two-process suite pass against
SHA256 `D618BF733EEA5E5D69C4675CDD8FCB488029AF6B968073B5E0976E4BD7C73FB2`.
Starting with supplied100 CPs, forced purchase-receipt failure leaves100 CPs and
no map. Eight competing confirmations then produce one map, one receipt and73
CPs. A40-slot bag rejects another offer. Exact map inventory and73 CPs persist
after the game/database restart, together with the earlier rapier/pistol
conversion and bound-P7 checks. This proves the payment core, not original quest
admission, searching, NPC proximity or naturally earned purchase funds.

## Private daily flower offering

`PirateEpicStore.Flowers.cs` adds the three-red/three-white offering for Bottle
3307448. It selects owned unlocked permanent flowers within catalog stack limits,
snapshots material and inventory state, and commits daily eligibility, material
debits and the bottle in one transaction. Bound input makes the bottle bound.
There is no P7 or currency reward for this daily offering. Revora's explicit reset
boundary is00:00 UTC. Offers expire after two minutes and cannot cross that boundary.
The unique owner/day receipt prevents a second offering, including after reconnect.

The isolated build and expanded `-PirateStoreOnly` suite pass both game/database
phases against SHA256
`450377FDEAD114A747A5C855839F9623112A736F6479DE829ABD23E1F1F909BB`.
With supplied flowers, the probe verifies rollback after flower debit when the
bottle insert fails; eight concurrent confirmations yield one bottle and one daily
receipt. A second offering rejects despite enough remaining flowers; moving the
controlled clock to the following UTC day permits a fresh offer. A confirmation
crossing midnight rejects even within the two-minute lifetime. Bottle identity,
binding, remaining flowers and the daily lock survive actual restart, alongside
chart payment and weapon-conversion regressions.

The coordinator, hunted flower admission for Pirates and bottle/search outcomes
remain disconnected. This is transaction acceptance, not a completed ordinary
quest or native interaction. Source producer/career totals do not increase.

## Private bottle and search outcomes

`PirateEpicStore.Search.cs` now atomically consumes one owned bottle/map unit and
inserts the resulting item(s), with an operation receipt and before/after session
checks. It validates catalog stack bounds, ownership, permanence, binding and
capacity before offering a search. Final-stage offers reserve space for both
Heart and Water. Maps support partial stack debit; bottles remain singleton.
No NPC, travel or item-use dispatch is enabled yet.

Revora defaults adopted for unpublished details: a bottle selects uniformly
among stars1-3; a successful map draw selects uniformly within that stage's
published range. Failure gives one bound Water; stage5 gives one Heart plus one
bound Water. Map/Heart outputs inherit input binding. These are explicit local
rules, not verified original secondary odds or a claimed owner reply. The main
30/25/20/20 percent map-reward probabilities remain unchanged. Random draws use
bounded rejection sampling, avoiding modulo bias.

The isolated build and2094 rule checks pass. Expanded `-PirateStoreOnly` passes
both game/database phases against SHA256
`B76DC9552553111D6DCAAF24F78CC9C8E086EEB5A692F4A0A88D9D06AC68B2C3`.
With controlled draws, the purchased star1 map becomes star4, then star5, then
Heart+Water. The daily-offering bottle becomes star3 and its failed search yields
Water. A forced search-receipt failure rolls back the consumed map and output;
retry succeeds, and replay rejects at all five steps. One of the two converted
weapons now uses this paid-map-earned Heart; the other Heart remains supplied.
The unbound earned Heart leaves its weapon unbound, while the supplied bound
Heart binds the other. Both P7s remain bound. Exact remaining items, enhancements,
CPs and five search receipts survive restart. The original fixture expectation
that both weapons bind was corrected to test actual source-binding inheritance.

The later checkpoint below verifies stack-debit and final-stage capacity runtime
boundaries. Real search/travel interaction, journal projection, Water exchanges
and lost-COMMIT recovery remain
open. This transaction chain is not a native or ordinary NPC earning-route claim.

## Search capacity and partial-stack recovery

The expanded `-PirateStoreOnly` suite passes both processes against the same
`B76DC9552553111D6DCAAF24F78CC9C8E086EEB5A692F4A0A88D9D06AC68B2C3` build;
this batch changes acceptance coverage, not production code.

- A singleton star5 map rejects at40 occupied slots without consuming anything.
  At39, it grants both Heart and Water and finishes with exactly40 occupied slots.
- A separately supplied two-unit star1 map stack rejects at40 because one consumed
  unit would leave the input slot occupied. At39, a controlled failed search
  leaves one map unit and inserts one bound Water, returning to40 slots.
- Exact remaining map unit, three Water rewards, six search receipts, converted
  weapon metadata, CP balance and daily offering state survive database/game
  restart. Earlier rollback, replay and concurrent-payment checks still pass.

The paid-map-to-Heart chain remains distinct from the separately supplied stack
fixture. No additional earning-source count or native acceptance is claimed.
The next exchange prerequisites are catalog-confirmed scroll identities3307432,
3307433 and3307434 (Sea Burial, Bloom of Death, Immortal Force); their learning
requirements and transaction consumers still need verification.

## Private Water exchanges and scroll learning

The installed catalog and existing Pirate Lord options agree on these mappings:

| Scroll | Skill | Existing mentor option |
| --- | --- | --- |
| 3307432 Sea Burial | 14680 | 23 |
| 3307433 Bloom of Death | 14720 | 25 |
| 3307434 Immortal Force | 14710 | 24 |

`PirateEpicStore.Training.cs` implements a ten-Water exchange per scroll, an
explicit Revora price. Owned permanent unlocked materials are selected and
snapshotted; a transaction debits Water, grants a scroll with inherited binding
and records the exchange. Learning consumes one owned singleton scroll together
with saving the skill and its receipt. Already-known cached or saved skills
consume nothing. Learning retains the legacy level40 minimum and requires Pirate
class70-75; the quest's second-rebirth/level70 gate applies to earning/exchanging,
not learning a traded scroll. These are documented local compatibility choices,
not newly verified original learning requirements.

The isolated build and expanded `-PirateStoreOnly` suite pass both game/database
phases against SHA256
`E9F06E2D440FBAC25CE1035E28BB88A55A56BD3949E1DE8D1067CFBEB58C0E7A`.
The fixture supplements its three earned Water with37 supplied Water and buys
four bound scrolls: the three distinct skills plus a duplicate Sea Burial scroll.
A forced learning-receipt failure rolls back skill and scroll debit. Trading
rejects learning; eight competing requests yield one learned skill per scroll.
The duplicate remains intact. All three skills load through the production skill
loader after database/game restart, alongside the exact leftover scroll and
three learning receipts. The initial reload fixture omitted that loader; the
corrected fixture passes without changing production persistence behavior.

All dispatch remains disconnected. The legacy free-learning options23-25 still
need coordinated interception when the quest is enabled. Native skill use,
learning level/class boundaries, exchange rollback/full-bag/concurrency boundaries,
journal and ordinary NPC/travel access remain open. No new daily P7 was added.

## Exchange and learning receipt enforcement

Both exchange and training now require exactly one inserted receipt before
commit. Previously they called the insert without checking affected rows; a
trigger suppressing the insert could leave a reward committed without its audit
record. The owned private fixture uses a `RETURN NULL` trigger on each receipt
table and verifies that material/skill/reward changes roll back. After removal,
eight competing confirmations of each exchange grant exactly one scroll and debit
Water once; concurrent learning and duplicate-scroll preservation still pass.

The isolated build and both `-PirateStoreOnly` game/database phases pass against
SHA256 `21AAD3A136446E09DB4A75070470605FED14B4E0A8D267E780125D1C0DB9749D`.
The production skill loader retains all three skills on restart. No dispatcher,
live schema, quest-completion count or native gate changed in this correction.

## Receipt-derived mission6868 journal

`PirateEpicJournal` projects the installed mission6868 from committed receipts.
An eligible Pirate initially sees Available; a purchase, flower offering or search
changes it to Accepted. A final-star search receipt containing Heart3307449 makes
it Finished permanently, independent of later item consumption. Possession of a
traded Heart, a weapon conversion alone or legacy quest kills cannot invent this
completion. Ineligible players with actual history retain their journal record.

The private store initializer registers the journal; normal startup still does
not initialize the Pirate feature. Quest-list filling suppresses legacy6868 rows,
and existing1134 Begin/2032 quit handlers protect the receipt-derived state.
The installed mission has IntentAmount=0, so no speculative1135 progress counters
are added. [Protocol evidence](PROTOCOL_RESEARCH.md#pirate-mission6868-receipt-projection-2026-09-27).

The isolated build and expanded `-PirateStoreOnly` suite pass both game/database
phases against SHA256
`D3F5D7C17B6264C0FF31A9C33290A6F62DEEEF42E68D9E05906B1719271C7C6E`.
A supplied Heart plus legacy kills99 remains Available despite a forged Finished
Begin and quit request. Paid acquisition becomes Accepted; the earned final-map
Heart becomes Finished. After both Hearts are consumed and the database/game
restart, the quest remains Finished, forged changes remain ineffective and the
legacy kills99 stays untouched. Existing rewards, skill-loader and payment
recovery checks pass. Native journal presentation and ordinary NPC access remain
unverified; no full quest milestone or supplier count increases.

## Private Revora hub and return (2026-09-27)

The private hub uses map10271 as a documented Revora choice, not a verified
original Sea of Rage identity. Its version1004 base-floor derivative is hash
checked before map registration. The read-only `tools/check_pirate_hub.py` audit
verifies the installed archive/native map, exact214x214 floor and connected
approaches to Jack Sparrow, Mermaid and Luffy while avoiding their footprints.
It finds9207 reachable cells. The56 kind4 records and572-byte opaque trailer do
not establish native collision semantics; genuine-client acceptance remains open.
Only the disposable candidate received this derived floor. The five treasure
base grids remain separate staging artifacts and production is unchanged.

`PirateEpicWorld` checks map/NPC identity conflicts, prepares the three hub actors,
and validates entry and return. The actual loaded Pirate trainer is NPC4272 in
map1004, consistent with class promotion. The installed mission6868 navigation
still targets9391: a coordinated client navigation correction is required before
release. Entry currently requires eligibility and proximity to4272.

The isolated build and both `-PirateStoreOnly` phases pass against SHA256
`4B89B06F9F4F82D454860BEF49D1E1363647AAE9B788FB1AB2A3091D506BB266`.
The fixture initializes the hub twice, enters at114,188, verifies all three NPCs
and returns beside the trainer. It also retains the transaction/restart checks
above. Entry/return methods are called directly with controlled proximity;
normal startup, NPC opening/reply dispatch, client navigation and native travel
are not accepted or enabled by this batch. No supplier or completion count rises.

## Private NPC trade dispatch (2026-09-27)

The normal `Npcs.GetDialog` dispatch now reaches the private-enabled Pirate hub:
trainer4272 offers passage; Jack Sparrow offers a27-CP chart or the daily three
Red/three White Flower offering; Mermaid offers the three ten-Water scroll
exchanges. Each trade requires a separate confirmation, a stored typed offer,
the same nearby NPC and the transaction store's ownership/preimage checks.
Offers are consumed before commit and cleared on leaving range or closing.
The feature remains disabled in normal startup. Luffy search, Heart awakening,
scroll item use and old free-training interception remain unfinished.

Build plus both private game/database phases pass with executable SHA256
`F8351E2022C009C9E0EF0988B5DA060583D4CACA9C4CC343879BE21074679ED4`.
The ordinary-profile fixture uses the normal NPC dispatcher for trainer passage,
chart offers/confirmation and return. Confirmation without an offer and after
moving out of range leaves the balance100 and no chart; returning cannot revive
the invalidated offer. Eight confirmations of a fresh offer produce one chart,
one receipt and balance73. Reload retains that chart/balance along with existing
reward/skill checks. Funds and qualifications are supplied. Flower and Mermaid
transactions retain their store tests, but their new dialogue branches have not
yet received equivalent end-to-end dispatcher coverage. This is not native-client
acceptance or a completed ordinary Pirate earning route.

## Confirmed awakening and trade dialogue coverage (2026-09-27)

Jack Sparrow now offers Heart awakening through the normal NPC dispatcher while
private hub activation is enabled. The offer names the rapier or pistol chosen
by the existing rapier-first rule, states the binding outcome, and requires a
separate confirmation before consuming the Heart. Selection considers bound
Hearts first, then UID; invalid selected material is rejected by the store.
Closing, invalid proximity and confirmation consume/clear the pending offer as
for chart trades. Exactly one bound P7 accompanies a successful awakening.

The ordinary-profile fixture now uses NPC dialogue for the pistol conversion,
flower offering and all four Water exchanges, as well as chart purchase and
passage. Offer review alone must leave materials intact; eight confirmations
must produce one conversion/flower reward/exchange. The rapier conversion retains
its direct store rollback/concurrency checks. Skills are still learned directly
through the store test: item-use dispatch remains unfinished. Controlled inputs,
clock and search draws remain explicit; this is not the full earned quest route.

The build and expanded store/reload game-process phases pass against SHA256
`EC554C0611FFCB2F24F172A6AAA040266246EFEBD5F697889D51D628C00FAF09`.
Exact weapon UIDs, enhancements, binding, P7 rewards, daily eligibility, scrolls
and learned skills persist across the game/database restart. No live activation.

## Private skill-scroll dispatch and trainer interception (2026-09-27)

While the hub feature is enabled, item-use dispatch recognizes the three Pirate
scrolls and calls the durable learning transaction under the inventory owner
lock. The direct item fallback requires the exact owned item reference. Existing
1009 envelope bounds, declared length, family, action and seal are checked before
learning. Trainer4272 options23/24/25 are intercepted and explain the scroll route,
so the legacy free `AddSpell` branches cannot bypass the requirement. Existing
skills are retained and duplicate scrolls are not consumed. Normal startup still
does not activate this feature.

The build and private store/reload phases use executable SHA256
`B4CCF4108B125F1ABE8D88C2EA23DA6825E910174D68F2C35150D6E36E55ED7D`.
The fixture invokes all three trainer shortcuts with no scroll and verifies no
skill appears. Each earned exchange scroll is then learned through
`ItemUsage.Process`, with eight concurrent uses yielding one receipt and one
consumption. A fourth duplicate Sea Burial scroll remains. Direct handler tests
reject changed declared length, packet family and seal before mutation. Existing
store rollback and trade rejection checks remain. Reload verifies all three
skills and the retained duplicate scroll. These synthetic packet checks do not
prove native-client interaction or the remaining class/level/length boundaries.

## Private Luffy search service (2026-09-27)

Luffy now offers separate choices for each chart grade and for bottle opening.
The selected owned input is captured in a typed, expiring offer; a second reply
confirms consumption. Dialogue discloses the chart-success chance, failed-search
Water, guaranteed five-star Heart/Water outcome and bound-input inheritance.
The existing transaction preserves ownership, capacity, replay and receipt
protections. The direct NPC service follows the previously researched Luffy
service; it does not replace the unfinished treasure-area travel route.

The private fixture now searches its paid chart through NPC dialogue to a
five-star chart and Heart, and opens its flower bottle through the same service.
Controlled draws cover successful chart upgrades and failed-search Water. Eight
confirmations at each stage must consume one input and exhaust only the intended
draws; old store offers cannot replay the consumed input. The final search still
checks full-bag rejection and two-output capacity. Qualification, CPs, weapons,
flowers and extra Water remain supplied where earlier sections state; no full
hunted/earned quest claim or supplier-count increase follows from this test.

Build and both game/database phases pass against SHA256
`2176D4C5193FFE4B762EA82F028554E4F821D700F9B6A73AB0E49797BD61C488`.
Restart retains exact rewards, weapon metadata, skills and completed journal
state. The feature remains private and absent from normal startup.

## Private hunted flower route (2026-09-27)

Eligible second-reborn level70 Pirates can enter the existing Abyss flower hunt
through trainer4272 option151 while the Pirate feature is enabled. They share
the existing owned monster/drop/pickup flow and its Revora one-in24 chance per
flower color. NPC3604 returns them beside the actual Pirate trainer; passage
from that trainer then reaches Jack Sparrow. Existing Ninja/Monk/Taoist branches
remain in place. The private gate prevents activation in normal startup.

Build plus store/reload game-process phases pass against SHA256
`BB641778B3D16901F004B045BE4BC502694262168C06195C9D130822131EF03E`.
The ordinary Pirate fixture now obtains six of each flower from twelve actual
loaded hunt monsters via the encounter-death and floor-pickup handlers. Repeated
pickup packets yield twelve items and twelve pickup receipts. NPC admission,
return and hub passage succeed. The collected flowers feed the existing daily
offering/bottle/search/exchange/awakening test, followed by reward recovery after
restart. The fixture deliberately marks collected Red Flowers bound afterward
to retain the binding-propagation regression; normal hunt drops remain unbound.

Kills and drop rolls are controlled, not native combat acceptance. Qualification,
CPs, weapons, one Heart and supplemental Water still come from fixture setup.
Remaining flowers and exact later rewards survive restart. Treasure-area travel,
ordinary weapon acquisition, native-client testing and normal-startup activation
remain unfinished; no supplier-count or overall completion increase is claimed.

## Travel parser gap and candidate coordinates (2026-09-27)

Reinspection of `Game/Map.cs` confirms that its native-file path skips one portal
count and then reads an object count. For these exact version1006 files, the
observed tail is `(0,0,count)`: the legacy reader would see zero objects and miss
all13/4/7/8/8 records. It also skips416 payload bytes for kind4, whereas these
files have424-byte records including their kind. This is evidence against using
the native reader unchanged; it does not prove that the ignored fields cannot
affect client collision. The hash-gated audit retains the opaque fields.

The audit now derives proposed Revora entry/search positions from the largest
connected base-access component, maximizing distance from its boundary. A
separate3x3 NPC footprint fits four cells from each landing. These are candidate
coordinates, not verified original coordinates or completed travel behavior:

| Map | Landing | Search NPC | Base-grid boundary distance |
| --- | --- | --- | --- |
|10266|29,56|33,56|14|
|10267|56,78|60,78|14|
|10268|25,42|29,42|19|
|10269|46,56|50,56|15|
|10270|26,45|30,45|22|

The read-only audit passes against all five exact installed archives and native
hashes; its numeric report includes the legacy count mismatch and candidates.
No terrain was installed and no game build/deployment was needed for this audit.
Map10267 has two disconnected base components, so arbitrary midpoint admission
would be especially unreliable. Original travel coordinates are not published
in the [official guide](https://co.99.com/guide/quest/pirate_epic_quest.shtml);
its linked travel screenshots could not be fetched during this check. Native
collision/acceptance, recoverable admission and actual travel remain pending.

## Direct bottle and Heart item use (2026-09-27)

The existing guarded1009 action4 route now handles bottles and Hearts as well as
skill scrolls. Bottle use opens the exact owned bottle through the atomic search
transaction; Heart use awakens the exact selected material's rapier-first target
through the existing conversion transaction. Both paths use the owner lock,
durable preimages and existing eligibility/capacity rules. Direct fallback calls
must supply the inventory's actual object, not a fabricated copy of its UID.
NPC confirmation routes remain available. Chart item-use travel is not enabled
by this change.

The build and private store/reload fixture use SHA256
`8317A18525A4DFA1FCFF860895DE7354C2D97B09383A8134B2384B39A270C5D9`.
The fixture opens the hunted-flower bottle and awakens the rapier through
`ItemUsage.Process`; eight repeated requests consume one item and grant one
result. Fabricated bottle/Heart objects leave the inventory unchanged. Pistol
awakening remains covered through NPC dialogue. Exact weapon metadata, binding,
P7 rewards, skills and journal completion are checked after database/game restart.
Native-client right-click acceptance remains pending; the production gate stays
closed and earlier fixture-supplied inputs remain explicitly documented.

## Private journal navigation correction (2026-09-27)

The actual private world probe now emits the nonsecret trainer identity and
position: `4272,1004,72,35`. This is the loaded catalog position, not the original
guide's coordinate. `prepare_pirate_journal.py` stages a narrowly scoped correction
of mission6868's BeginNpcId and FinishNpcId from9391,1004,58,34 to4272,1004,72,35.
It requires the approved DX9 executable hash and exact installed Questinfo hash,
preserves original bytes for rollback, rejects differing pre-existing artifacts
and verifies that the active file stays unchanged. All other mission bytes,
including IntentAmount and reward text, are preserved.

Private files are under ignored `obj/chi-refill-validation/pirate-journal`.
The source SHA256 is
`f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14`;
the staged output is
`1c72505f7b226d7b177b013ff818400b5c43bc62dfe634a7171b4f615f69651b`.
Checks confirmed exactly two corrected fields, unchanged intent counts, rejection
of a missing mission, duplicate mission and altered navigation preimage. The
private game/database suite also passed with the unchanged previous executable.

This is a standalone review overlay based on the installed catalog; it must be
composed with the other pending quest overlays before release. It is unsigned,
not installed or published, and native navigation acceptance remains pending.
No proprietary catalog bytes were added to Git.

## Shop-purchased weapon route (2026-09-27)

The loaded weaponsmith shop5 already sells compatible rapier611004 and
pistol612004 for their catalog silver prices. The private ordinary-player fixture
now purchases both through `ItemUsage.Process` and equips them through the normal
item handler before hunting flowers. It verifies the exact aggregate silver
debit and two durable shop receipts; no new shop stock or economy price changes
were needed. Fixture enhancement SQL no longer assigns equipment positions.

Both weapons subsequently awaken to671004/670004 through the existing item/NPC
routes. The fixture links their retained UIDs back to the corresponding purchase
receipt arrays, including after database/game restart. Plus12, -7, enchant255 and
two Super gems remain controlled enhancement inputs for preservation testing;
this does not prove an ordinary enhancement-earning route. One Heart and the
extra Water, silver balance, CPs and qualification still come from fixture setup.
The other Heart comes through the paid-chart search path.

Both game/database phases pass against the unchanged executable SHA256
`8317A18525A4DFA1FCFF860895DE7354C2D97B09383A8134B2384B39A270C5D9`.
Only the fixture/documentation changed, so no full game rebuild was performed.
Supplier classifications and full quest completion remain unchanged pending the
remaining earning, travel, activation and native acceptance gates.

## Both Hearts earned from quest inputs (2026-09-27)

The ordinary private fixture no longer supplies either awakening Heart. Its
purchased one-star chart follows the controlled1 -> 4 -> 5 -> Heart route. The
hunted-flower bottle follows bottle -> 3 -> 5 -> Heart. Both use the existing
NPC/item handlers and durable transactions. The temporary supplied Heart used
solely to test forged journal completion is deleted before quest earning begins.

A bounded recursive receipt query follows each purchased-chart or flower-bottle
UID through search reward UIDs to the consumed Heart UID in an awakening receipt.
It requires both distinct origins after the conversions and again after the
game/database restart. The fixture also retains purchase-receipt ownership of
both weapons, precise enhancements/binding, two bound P7s and completed journal.
There are now seven search receipts including the separate partial-stack test.

The executable remains
`8317A18525A4DFA1FCFF860895DE7354C2D97B09383A8134B2384B39A270C5D9`;
this is a fixture change, not a new game build or deployment. CPs, silver,
qualification, enhancement values and supplemental Water remain controlled
inputs; drop/search outcomes are deterministic. Red Flowers are marked bound
for the binding regression. These limitations and private activation still
prevent a claim of full live quest completion or a supplier-count increase.

Both private store/reload game-process phases pass with these earned-Heart and
purchase-ownership assertions. Travel, native acceptance and normal-startup
activation remain open.

## Private travel prototype and location recovery (2026-09-27)

`InitializeTravel` is an explicit private initializer, absent from normal startup.
It hash-checks all five audited base-access derivatives, validates map identities
and reserved NPC identities/footprints, then registers a Treasure Guide per map
at the proposed Revora coordinates above. The disposable candidate now contains
these five small floors and their runtime-manifest hashes. This supersedes the
earlier blanket staging-only restriction solely for an isolated server-flow
prototype: native object collision remains uncertified, and production installation
is still prohibited until that gate passes. No installed client was changed.

Chart right-click is recognized only after this private initialization. Revora
entry requires being in Sea of Rage and passing ownership/eligibility/capacity
checks. Entry keeps the chart. The destination guide offers a separate search
confirmation using the existing atomic receipt transaction, then returns to the
hub; leaving without searching retains the chart. A newly opened dialogue after
restart prepares a fresh offer from the owned chart rather than restoring a stale
in-memory offer. These are documented Revora interaction choices, not claims
about original entry coordinates or exact original consumption timing.

The first recovery run found the login loader's general `MapID >= 7008` reset.
It moved this character to Twin City despite a saved valid location. The loader
now exempts only enabled Pirate hub/travel maps with matching loaded identity
and a walkable saved coordinate. Housing and other high-map handling are unchanged.
A disabled/unloaded Pirate map does not gain an exception.

Build and both game/database phases pass against SHA256
`5D5E08B2EE38E25D7C98A71F47C29CA8E762ADFF98FE16B12A48CD489294EFB2`.
The fixture initializes all five destinations twice, enters10266 from the paid
chart through item use, retains it until guide confirmation, searches and returns
to the hub. Later it saves an unconsumed chart inside10266, restarts game/database,
verifies10266 at29,56 with the same remaining chart and returns without a charge.
Existing earned-Heart, purchased-weapon and reward/skill checks still pass.
Other four entry/search journeys, abnormal-disconnect timing, malformed travel
requests and native collision/rendering need further validation. No activation,
public patch, deployment or supplier-count increase follows from this prototype.

## All five private travel destinations (2026-09-27)

The controlled paid-chart chain now visits grades1 -> 2 -> 4 -> 5, while the
hunted-flower bottle chain visits3 -> 5. Together they exercise item-use entry
and guide searches on all five registered maps using the audited candidate
coordinates. Before each chart entry, changed length/family/seal packets must
leave the player in the hub with the original chart intact. These malformed
checks call the guarded handler directly; valid entry uses ItemUsage.Process.

At each destination the fixture prepares a search, returns without consuming,
re-enters and tries the old confirmation. Nothing is consumed until a fresh
search offer is prepared. Eight confirmation requests then produce one search
and one return, with the intended controlled random draws used exactly once.
Five-star capacity, bought weapon UID ancestry and both earned Heart origins
remain checked. There are eight search receipts including the separate stacked
chart regression. Saved first-map location and unconsumed chart recovery remain
part of the restart phase; this does not test restarting inside every other map.

The runtime is unchanged from the previous private prototype:
`5D5E08B2EE38E25D7C98A71F47C29CA8E762ADFF98FE16B12A48CD489294EFB2`.
No new game build, live installation or native-client claim is made. These checks
validate server transitions against base grids; object collision, actual player
walking and rendering still need genuine6711 acceptance.

Both expanded private store/reload phases pass with the all-five-destination
checks above. Native and activation gates remain open.

## Water acquired through chart searches (2026-09-27)

The fixture no longer inserts a37-unit Water stack. Before each Mermaid exchange,
it buys and searches charts through Jack Sparrow/Luffy until it owns ten Water,
then trades and learns through the existing handlers. Interleaving the exchanges
keeps this ordinary40-slot inventory within capacity. The38 total chart purchases
cost1026 CPs from a controlled starting balance1100, leaving74 after restart.
The additional37 searches use fixed failure outcomes, yielding37 bound Water.

The receipt audit counts40 Water rewards across45 search operations. Two Water
come from the earned five-star Hearts; one still comes from the separate supplied
two-unit chart-stack regression. That supplied chart input remains explicit:
this batch removes supplied Water, not every controlled test material. Four
exchanges consume40 Water, three scrolls learn distinct skills and one duplicate
Sea Burial scroll remains. Exchange and training rollback/repeated-request checks
are retained, along with exact saved balance, skills and duplicate-scroll recovery.

Both private game/database phases pass against unchanged executable SHA256
`5D5E08B2EE38E25D7C98A71F47C29CA8E762ADFF98FE16B12A48CD489294EFB2`.
No game rebuild was necessary. Native interaction, collision and normal startup
remain pending; the broader roadmap is not complete and nothing was deployed.

## Skill-use rejection boundaries (2026-09-27)

The private fixture now invokes normal item-use dispatch with profession69,
profession76, level39, a locked scroll and an active trade. Each attempt must
retain the exact inventory UID/type/binding/stack fingerprint and create neither
a skill in memory nor a durable skill/training row. Original qualification and
item state are restored between cases. The guarded handler separately rejects
a24-byte truncated packet and a129-byte packet whose declared length/seal would
otherwise be internally consistent. Existing changed-family/length/seal checks
remain. Accepted learning still runs through eight repeated item-use requests.

These cases use the unchanged private executable
`5D5E08B2EE38E25D7C98A71F47C29CA8E762ADFF98FE16B12A48CD489294EFB2`.
They verify negative eligibility and packet bounds; positive learning at exactly
level40 and every eligible profession is not covered by this batch. Native and
normal-startup gates remain unchanged.

Both private store/reload phases pass, including unchanged materials after the
rejected requests and the three saved skills after valid learning.

## Usable material selection (2026-09-27)

NPC automatic selection previously sorted Hearts/charts before checking whether
they were usable. A bound locked chart could therefore hide a usable unbound
chart and prevent an otherwise valid trade. Hub and travel-guide chart selection
now filter through the same SearchInput predicate as PrepareSearch; Heart
selection reuses the conversion store's Heart predicate. Durable ownership and
saved-preimage checks still run during preparation and commit. Direct item use
continues to use the exact item the player selected.

The regression fixture adds a bound locked chart ahead of Luffy's preferred
selection, then verifies that the ordinary unbound chart search earns Water while
the locked chart remains untouched. The decoy is removed only by fixture cleanup.
This covers the concrete chart failure; locked-Heart behavior shares its existing
predicate but is not separately exercised in this batch. Build and private
store/reload checks use SHA256
`C51EAB70EF888E1945A56DDA344D50AE6BE307D2AAD84D55B762FFAC04E8C554`.

Both private game/database phases pass. No deployment or native acceptance is
claimed. The checklist below separates connected private paths from their
remaining acceptance gates.

## Journal composed over the current quest bundle (2026-09-27)

`compose_pirate_journal.py` now resolves the Pirate navigation correction over
`tempest-wings-composed-v2`, which already inherits the reviewed Fan/Tower and
other quest changes. It pins that bundle manifest to
`4758432c1cf0627bdc501d7791224e3c82dba8cde606d593b1351a8e3f746100`
and validates all ten base outputs. The new journal hash is
`4e54218fe0e58b75406cda15c71f4d4f633698db7eca17681b9a50618aac224b`.
Only mission6868's two navigation lines differ from the composed base; every
other journal byte is retained.

The ignored `pirate-journal-composed-v1` directory contains only the changed
journal and a resolved ten-file manifest. Nine unchanged files remain referenced
by their existing relative source paths and hashes, avoiding another full copy.
This is deliberately not a standalone release directory: preserve the pinned
Tempest v2 source alongside it. Preparation validates the approved DX9 executable
and installed journal, checks read-back hashes and leaves active files untouched.
A second run verifies identical artifacts rather than replacing them.

Composition, repeat-run and exact two-line-difference checks pass. No game build
or game-process rerun was needed for this client-data preparation. The bundle is
unsigned and unpublished; transactional installation, native navigation/rendering
and Pirate native collision remain open. No proprietary bytes were added to Git.

## Atomic purchased-chart merge (2026-09-27)

The legacy stack merge persisted its enlarged destination before separately
removing the source item. A failure between those operations could duplicate
material. Enabled Pirate charts and Water now use a guarded1009 action48 path:
validate owned usable matching-type/binding items, check stack capacity, lock
owner and durable item preimages, then update/delete within one transaction.
Inventory projection occurs after commit. Other material families retain their
existing behavior; split handling is not changed by this batch.

The fixture now buys both chart units through Jack Sparrow instead of inserting
a two-unit stack. A forced source-delete failure rolls back the destination
increment and preserves both UIDs/counts. Eight merge requests then yield one
two-unit stack and remove the source once. The existing partial-stack search
consumes one unit and saves the remaining unit through game/database restart.
The chart case is directly covered; Water uses the same handler but is not
separately merged by this fixture.

Forty chart purchases now cost1080 CPs from the controlled1100 starting balance,
leaving20. Forty Water rewards still come from45 searches; all chart inputs in
those earning searches are purchased or obtained from the hunted-flower bottle.
There is no supplied awakening Heart, supplemental Water or partial-stack chart
in this path. Temporary forgery/locked-decoy materials are deleted independently;
qualification, currency, enhancements and deterministic outcomes remain controlled.

Build and private store/reload phases use SHA256
`19CD8A4770DE76289E9B6785029FBDE97B29C6C5F2B64EE970E32B1EF23B5EAB`.
Native merge, collision/interaction acceptance and production activation remain
pending. No deployment or overall roadmap completion is claimed.

Both private phases pass, including rollback, repeated merge, partial consumption
and exact saved rewards/balances.

## Atomic Pirate material split (2026-09-27)

Enabled chart/Water splitting now shares the guarded1009 stack dispatcher.
It validates the actual owned unlocked permanent stack, requested quantity,
catalog limit, ordinary session and one free bag slot. The original-stack debit
and new-item insert commit together under the owner/item locks; client inventory
updates follow commit. Binding, durability, maximum durability, plus, blessing
and color are retained for the separated material. Other item families continue
through their existing handlers.

The private chart test forces an insert failure after the debit and verifies
that the original two-unit stack and durable inventory fingerprint are unchanged.
Eight requests to split one unit from that two-unit stack yield two one-unit
items, which rejoin into the original UID. At40 bag slots, split leaves all rows
and quantities unchanged. This is quantity-conservation coverage for a two-unit
stack, not a promise that repeated valid requests against a larger stack are
interpreted as one user action. Native stack UI and separate Water cases remain
unverified.

The build and private game/database phases use SHA256
`9B72D1C220424D314A9C2E9879B98EED6C063C294C202F642CA2E1AAB1800B33`.
The existing earned-item, travel, reward and restart checks remain in the suite.
Normal startup and live deployment are unchanged.

Both private store/reload phases pass with split rollback, quantity conservation
and full-bag refusal included.

## Earned Water stack acceptance (2026-09-27)

The private ordinary-player journey now exercises the three bound Water units
earned by its preceding searches. Malformed length, packet family and seal are
rejected by the stack dispatcher. A locked input or mismatched binding leaves
the saved inventory and cached quantities unchanged. Eight concurrent merge
requests consume the second UID once, retaining binding and all three units.
Zero, whole-stack and uint-maximum split quantities are refused. Eight one-unit
split requests against the two-unit stack produce exactly two bound singletons;
ordinary merges then consolidate the three earned units into one saved stack.

That stack is consumed by the existing ten-Water skill-scroll exchange journey.
Both private game-process phases pass, including game/database restart, exactly
three learned skills, the retained duplicate scroll, two bound P7 rewards, forty
chart purchases and the final20-CP balance. No materials were supplied for these
new cases. Qualification, starting funds and search draws retain the fixture's
previously documented controls. Log: `obj/pirate-water-process.log` (ignored).
The game binary is unchanged from the atomic-split build above; only the private
fixture was extended. This supersedes the separate-Water-case gap above, not
native stack UI, authenticated gameplay, crash recovery or live activation.

## Awakening successful-COMMIT reply loss (2026-09-27)

The ordinary NPC conversion of the paid-chain Heart now runs through a bounded
loopback PostgreSQL relay. The fixture verifies the disposable process database,
runtime user and fixed backend127.0.0.1:25439 before redirecting connections.
It forwards background/preparation work, arms only when the awakening receipt
insert is sent, then observes the backend's successful COMMIT completion and
closes that connection before forwarding the response. Frames are size-bounded,
stream operations time out and relay tasks stop with the scope. No payload or
credential logging is added. This reuses the established gem-embedding fault
technique, with transaction selection for the actual running game process.

The NPC handler receives an NpgsqlException with EndOfStreamException and marks
the character as requiring persistence reload. Its stale inventory still shows
the consumed Heart and original pistol, while durable queries find the converted
same-UID pistol, deleted Heart and exactly one bound soul joined to the awakening
receipt. Eight more confirmations on that fenced session change no saved item.
Production character/item/skill loading recovers the committed result, and eight
replays of the consumed Heart's item-use request leave it unchanged.

Both private process phases pass, including the later game/database restart,
exact weapon metadata and binding, two total awakening receipts and two bound P7
souls. Log: `obj/pirate-commit-loss-process.log` (ignored). The game binary is
unchanged from the atomic-split build above. This proves one real successful-
commit response-loss boundary, not every Pirate transaction's failure behavior,
authenticated client disconnects, native acceptance or live activation.

## Paid chart and daily-offering commit recovery (2026-09-27)

The same disposable relay now has an explicit three-table receipt allowlist.
The first earned chart purchase and the hunted-flower daily offering each lose
their successful COMMIT response separately; the awakening fault remains in the
same journey. Preparation and unrelated transactions pass through normally.

For the chart purchase, the fenced cache retains1100 CPs and no chart, while the
database has1073 CPs and one chart joined to its purchase receipt. Eight stale
confirmations leave that state unchanged. Production reload recovers the chart
and1073 CPs; another eight confirmations without a new offer do not charge again.
A deliberately prepared new purchase remains allowed and is exercised later.

For the offering, the fenced cache retains six flowers of each color and no
bottle. The database has three of each remaining plus one bound bottle joined
to its daily receipt. Repeated confirmations on the fenced session do nothing.
After production reload, eight fresh offering/confirmation attempts cannot
repeat that day's claim. The existing next-day eligibility check still passes;
no P7 soul is added to the daily offering.

Both private process phases pass, including the full earned search, training and
awakening journey and subsequent game/database restart with the same expected
balances, skills and rewards. Log: `obj/pirate-entry-commit-loss-process.log`
(ignored). The game binary is unchanged. Search, exchange, learning and stack
transactions have not yet received this specific transport-fault test. Native
acceptance, authenticated disconnects and live activation remain pending.

## Scroll exchange and learning commit recovery (2026-09-27)

The targeted relay allowlist additionally covers scroll-exchange and training
receipts. On the first earned ten-Water exchange, the relay drops the successful
COMMIT reply. The NPC handler fences the session, whose cache still holds ten
Water and no scroll. Durable queries find no remaining Water and one bound Sea
Burial scroll joined to its ten-Water receipt. Eight stale confirmations change
nothing. Production reload recovers that scroll and zero Water, and replaying
the old confirmation cannot create another exchange.

The subsequent ordinary scroll-use packet loses its successful COMMIT response
separately. The fenced cache retains the scroll and lacks skill14680, while the
database has deleted that scroll and saved the skill and matching training
receipt together. Eight requests on the fenced session leave that result alone.
Production character/item/skill reload recovers the skill and consumed-scroll
state; eight further item-use replays leave exactly one training receipt.

The original invalid-scroll, malformed-frame, wrong-class/level, trade, rollback,
duplicate-skill and three-skill earning checks remain in the same journey.
Exchange/learning now have this specific transport-fault coverage; search and
stack operations do not yet. Native acknowledgment, authenticated disconnects,
all possible failure timings and live activation are not established by it.

Both private process phases pass, including the later game/database restart
with exactly three learned skills, four exchanges, the retained duplicate
scroll and the same final20-CP balance. Log:
`obj/pirate-training-commit-loss-process.log` (ignored). The game binary remains
the atomic-split build; this batch changes its private acceptance fixture only.

## Chart search successful-COMMIT reply loss (2026-09-27)

The private journey now drops the committed search response at two travel-guide
boundaries: the paid one-star chart's controlled two-star result, and the paid
five-star chart's Heart plus Water result. The relay explicitly allows the search
receipt table. Both handlers fence the stale session before inventory projection
or the normal return teleport. The database has consumed the chart and retained
exactly one search receipt with the expected output types.

Eight stale confirmations change no saved item. Production reload keeps the
character on the treasure map, removes the spent chart and restores the exact
reward UIDs recorded by the receipt. The upgraded chart and paid-chain Heart
remain unbound; Water is bound. Eight fresh review/confirmation pairs cannot
search again or reroll the consumed input. The controlled random queue stays
empty and the restored session stays usable. The guide's explicit return still
works. The final search continues to satisfy the39-to40-slot reward-capacity case.

Both private game/database phases pass with all prior fault cases included:
purchase, daily offering, two searches, scroll exchange, skill learning and
awakening. Exact acquired weapons, final rewards, skills and balances survive
the later process restart. Log: `obj/pirate-search-commit-loss-process.log`
(ignored). The game binary is unchanged. These tests cover selected successful-
commit reply losses; bottle opening, partial-stack searches and merge/split have
other acceptance coverage but not this specific fault. Native client acceptance,
authenticated disconnects, other fault timings and activation remain pending.

## Remaining implementation

- [x] Separate the confirmed Heart material from the Oracle pack.
- [x] Audit installed treasure-map identities and native grid prerequisites.
- [x] Establish the private hub and stage all five base-grid travel destinations.
- [ ] Complete native object-collision validation for the staged terrain.
- [x] Document Revora choices for unpublished draws, consolation and exchanges.
- [x] Implement the private27-CP chart purchase with rollback, concurrency,
  full-bag rejection and saved map/balance recovery.
- [x] Implement private atomic flower offerings and durable daily eligibility.
- [x] Implement private atomic bottle/search outcomes and validate the paid-map
  chain to an owned Heart across restart.
- [x] Verify partial-stack search and complete final-reward capacity across restart.
- [x] Verify all five private travel routes, first-map restart recovery and ordinary
  access without GM/name gates.
- [ ] Verify remaining abnormal-disconnect and ambiguous-commit session boundaries.
- [x] Verify NPC awakening after lost successful-COMMIT response: fenced stale
  session, exact durable reward, production reload, replay and process restart.
- [x] Verify paid chart and daily offering after lost successful-COMMIT responses:
  exact debit/reward, stale-confirmation refusal, restored daily eligibility and
  ordinary reload followed by the complete earned journey and process restart.
- [x] Verify earned Water exchange and scroll learning after lost successful-
  COMMIT responses: atomic consumption/reward, fenced replay and production reload.
- [x] Verify chart upgrade and final Heart/Water search after lost successful-
  COMMIT responses: exact reward UIDs/binding, no reroll, map recovery and restart.
- [x] Implement and validate catalog mappings and rapier-first slot selection.
- [x] Privately convert owned weapons one Heart at a time, preserving enhancements,
  binding and UID, with rollback/replay and actual restart checks.
- [x] Connect conversion to ordinary purchased weapons, both earned Heart routes
  and NPC/item dispatch, with saved receipt ancestry.
- [ ] Finish remaining conversion acceptance and activation gates.
- [x] Implement private durable Water exchanges and scroll learning; verify saved
  skills, rollback and duplicate-scroll preservation across restart.
- [x] Project mission6868 from receipts, reject forged progress and retain
  completion after material consumption and restart.
- [x] Connect exchange/training NPC/item dispatch and intercept free legacy
  trainer options; verify documented rejection and restart cases.
- [ ] Finish remaining exchange/training acceptance boundaries.
- [ ] Verify genuinely acquired inputs, failure/replay/rollback, full bags and
  exact rewards across game/database restart. Respect the bound-P7 one-time
  quest policy without adding P7 to the daily flower offering.
- [ ] Genuine6711 acceptance and coordinated deployment.


## Normal startup and earned supplier (2026-09-27)

- [x] Server startup prepares the hash-checked hub identity before map loading,
  then initializes its store/NPCs and all five treasure routes after the other
  quest services and before opening Account/Game listeners.
- [x] The server GameMap.dat lacks10271. Pirate initialization now constructs that
  hub explicitly from the same approved grid previously created by the helper.
  Existing terrain hash, map identity, saved-NPC, footprint and walkability checks
  remain mandatory; a conflict aborts startup rather than serving a partial route.
- [x] The ordinary non-GM process fixture no longer initializes the store, maps,
  hub or travel routes itself. Normal startup must expose them. Purchased inputs,
  both earned Heart ancestries, same-UID rapier/pistol awakening, bound P7 awards,
  Water exchanges, learned scrolls, journal and five-map travel checks all pass.
- [x] A second normal game startup after database restart recovers the exact
  weapons, materials, skills, receipts and saved treasure-map location. Existing
  rollback and selected lost-COMMIT reply cases remain in the passing fixture.

Build: `obj/pirate-startup-build.log` (existing legacy warnings). Process evidence:
`tools/Verify-ArenaProcess.ps1 -PirateStoreOnly -ResetOwnedFixture`,
`obj/pirate-normal-startup-process.log`. Gameplay SHA256:
`35D38E91EF3CBD5BE3737FED1C77FC7494EA249D5B81E71DF7726355652E598D`.
The first startup test exposed the absent GameMap.dat row; explicit hub creation
resolved that prerequisite. The private executable was refreshed with its prior
version retained; the owned database clone was removed. No live deployment.

The fixture still supplies starting eligibility/currency, clocks, proximity and
selected draws/damage. This proves an ordinary startup-accessible earned weapon
supplier, not proficiency/mastery training, full authenticated TCP completion,
native object collision or progression to eligibility. Those gates remain open.


## Earned-weapon combat mastery and persistence (2026-09-27)

- [x] Train the quest-earned rapier671 and pistol670 through production melee
  combat to proficiency12, earning11322 and11323, then reload exact XP/levels
  and verify repeated achievement observation leaves one evidence row each.

`CheckCandidatePirateLifecycle.PirateCombatTraining` loads the ordinary saved
character and the same two awakened weapon UIDs from the earned quest fixture.
It calls `Attack.HandleAttack` against startup-loaded non-boss monsters. It does
not assign proficiency levels, proficiency XP, damage, monster HP or XP rates.
The fixture supplies proximity and advances attack cooldowns; targets that take
no damage for32 attempts are skipped. Combat is bounded to90 seconds/50,000
attempts. Ordinary equipment/CP/money drops are disabled in this test. The
preceding conversion fixture supplies eligibility, funds, controlled quest RNG
and +12/-7/two-gem enhancements to test their preservation. This is server-process
combat evidence, not an unassisted live-player timing or difficulty benchmark.

The fresh earned quest and reload passed in `obj/pirate-combat-complete.log`;
its first20-second training window saved both families at level10. The same
owned character resumed without resetting earned progress. In
`obj/pirate-combat-persistence.log`, training reached level12 with4144 XP in both
families (16,779 additional attempts, five additional kills), followed by a
PostgreSQL/game restart. The reload matched saved levels/XP and found exactly
one evidence row for each achievement before and after repeated observation.
Both final phases passed on gameplay SHA256
`35D38E91EF3CBD5BE3737FED1C77FC7494EA249D5B81E71DF7726355652E598D`.
The isolated fixture database was cleaned up after success.

The initial50,000-attempt probe stalled on a non-damaging target; bounded target
skipping fixed the fixture, without changing gameplay combat rules. Authenticated
end-to-end Pirate quest interaction, native DX9 acceptance and deployment remain
pending. These two earned identities do not prove a compatible320-achievement
career or change the producer/wardrobe inventory counts.


## Authenticated chart purchase and travel (2026-09-27)

- [x] Ordinary SRP/DH game-socket admission through PirateLord4272, chart
  purchase through199590, immediate inventory/CP updates, stale-confirmation
  rejection and use of that earned chart to enter treasure map10266.
- [x] PostgreSQL/game restart, fresh authenticated login, persisted chart and
  73-CP balance, stale confirmation without another charge, and chart travel.
- [ ] Complete authenticated search, flower, awakening and scroll journeys.
- [ ] Native DX9 presentation/collision and deployment.

`tools/Verify-ArenaProcess.ps1 -PirateWireOnly` passed both phases in
`obj/pirate-wire-process.log` on gameplay SHA256
`35D38E91EF3CBD5BE3737FED1C77FC7494EA249D5B81E71DF7726355652E598D`.
The fixture supplies a second-reborn Pirate,100 CPs and proximity to the trainer;
it does not seed a chart or directly invoke the NPC/item handlers. The ordinary
account sends actual NPC option and1009 item-use packets through SRP/DH game
login. Socket decoding checks100->73 CPs, exactly one3307443 chart, hub10271 and
treasure map10266. Server checks confirm one durable purchase with100/73 balances.
The second login opens the trader menu and sends option20 without a new offer;
neither inventory nor CPs changes. This covers stale replay after reauthentication,
not resurrection of an uncommitted pending offer. All changes remain private;
the owned disposable database was removed after successful validation.


## Authenticated treasure search and exact reward reload (2026-09-27)

- [x] Search with the purchased chart through NPC199600 over the authenticated
  game socket; observe chart removal, one allowed reward and return to10271.
- [x] Reload the exact reward UID/type after PostgreSQL/game restart, retaining
  binding for Water of the Sea and the unchanged73-CP balance.
- [x] Reject stale confirmation both after the search and on a fresh login.

`obj/pirate-wire-search.log` records all three `-PirateWireOnly` phases passing
on the same gameplay hash recorded above. The search receipt references the
chart UID from the paid purchase receipt, and its reward UID/type matches the
character's sole saved inventory item after restart. The search RNG is not
replaced: a run covers its actual sampled outcome, not every possible branch.
Allowed one-star outcomes are a one-to-four-star chart or bound Water of the Sea.
No search input or reward is seeded. Existing fixture controls remain eligibility,
starting CPs and trainer proximity; the third phase supplies proximity to the
original treasure guide to test a stale confirmation after reauthentication.
It does not move or create an NPC. No second search receipt or reward appears.

The first duplicate arrives after automatic return to Sea of Rage; the fresh
login duplicate is sent beside the original guide after opening its menu but
without preparing another search. Together they cover both out-of-location and
valid-location stale requests. The prior two-phase chart-only acceptance above
is historical; the current suite adds search to phase two and exact reward reload
as phase three. Flower exchange, full chart progression to Heart, awakening and
scroll learning still need authenticated journey acceptance. Native DX9 and
live deployment remain pending.


## Authenticated paid-chart awakening and all scrolls (2026-09-27)

- [x] Buy30 charts for810 CPs through actual authenticated NPC requests, and
  perform34 searches to earn two Hearts and30 Water.
- [x] Use the two earned Hearts through1009 packets, converting purchased
  rapier611004 to671004 and pistol612004 to670004 with unchanged item UIDs;
  observe both bound P7 rewards immediately.
- [x] Exchange30 earned Water for three bound scrolls through Mermaid199591,
  use each scroll through1009 and observe skills14680,14720 and14710 immediately.
- [x] Repeat each consumed Heart/scroll use without another award or learning.
- [x] Restart PostgreSQL/game, authenticate again, and match exact item
  UID/type/binding/quantity fingerprint,190 CPs, both equipped weapons and all
  three skills in server state and socket updates.

`tools/Verify-ArenaProcess.ps1 -PirateFullWireOnly` passed both phases in
`obj/pirate-full-wire.log` on gameplay SHA256
`35D38E91EF3CBD5BE3737FED1C77FC7494EA249D5B81E71DF7726355652E598D`.
Receipt counts remain30 purchases,34 searches,2 awakenings,3 scroll exchanges
and3 training records after restart. The owned disposable database is removed
only after both phases pass. Helper compilation treats warnings as errors.

Fixture controls: ordinary Player account, second-reborn Pirate eligibility,
1000 starting CPs, existing fixture silver, and proximity to NPCs. The base
weapons are bought/equipped through production handlers before socket login,
not supplied items; their two shop receipts are checked. No enhancement is
seeded in this fixture. NPC proximity is supplied between trades; walking is
not claimed. Search outcomes are controlled: two1->4->5->Heart progressions,
then28 consolation searches. The34 searches produce the two Hearts and30 Water;
no chart, Heart, Water, scroll, P7 reward or skill is injected. This is deterministic
path coverage, not a measurement of live drop rates or grind duration.

Every chart purchase/search, awakening, Water exchange and scroll use in this
batch enters via authenticated TCP; private helper calls only supply setup,
proximity/RNG and assertions. The client-side probe checks all item identities,
types/binding, CPs and1103 skill updates. Raw game input never receives authority
to select an outcome. Existing process tests cover other RNG branches and
transaction failure/recovery cases.

Authenticated flower hunting/offering remains open. Full native6711 rendering,
movement/collision and deployment remain separate pending gates.


## Open authenticated flower-hunt regression (2026-09-27)

- [ ] Complete flower combat/pickup/offering and daily-limit restart acceptance.
- [ ] Fix medicine rejection after ordinary unsaved combat HP changes.

The new opt-in `tools/Verify-ArenaProcess.ps1 -PirateFlowerWireOnly` is an
**open failing regression**, not acceptance evidence. On private gameplay hash
`35D38E91EF3CBD5BE3737FED1C77FC7494EA249D5B81E71DF7726355652E598D`,
`obj/pirate-flower-wire.log` fails during its first combat phase; the owned
fixture remains stopped for diagnosis. The planned reload phase has not run.

The ordinary Pirate enters1700 through PirateLord over authenticated TCP and
attacks a real family3609 monster with50,000 HP. At normal550-ms request spacing,
production combat reduces both monster and player HP. Immediately before a
1002050 potion request, the probe records live HP8352 and saved HP10000. A
bounded first-chance observer recognizes only the exact existing diagnostic
`Durable HP/MP changed; reload required.` and emits a fixed metadata marker.
The redacted game log confirms that marker and `source=healing-medicine` failure.
The following socket barrier times out. No successful flower hunt is claimed.

Source evidence: `Entity.SetHitpoints` updates live HP and packets, without an
immediate database update. `HealingMedicine.TryUseOwned` nevertheless requires
saved HP/MP to equal current live HP/MP before consuming medicine. A normal
combat delta therefore trips its stale-writer check. Removing that check alone
would weaken concurrency protection; the next fix must distinguish the last
durable health image from pending live combat changes, preserve ownership and
atomic medicine consumption, and serialize concurrent health mutations.

Earlier accelerated attacks also starved the probe's response barrier; the
fixture now uses550-ms spacing, preserving production packet filtering and
attack cooldowns. Setup currently supplies +12/-7/two-gem enhancements for the
two purchased weapons and15 healing items, plus eligibility/currency/proximity.
Damage and monster HP are not overridden. Drop color and daily clock are
controlled for the eventual12-flower/two-offering test. No flower/drop/pickup,
medicine success, daily claim or reload result from this fixture is marked proven.


## Authenticated flower route and healing correction (2026-09-27)

- [x] Enter1700 through PirateLord, kill flower-bearing monsters using actual
  TCP melee attacks, collect12 drops with duplicate pickup packets, and return
  through the exit NPC.
- [x] Offer three earned flowers of each color, use the earned bottle for its
  chart, and reject another offering while three flowers of each color remain.
- [x] Restart PostgreSQL/game, reauthenticate, match the exact item fingerprint
  and unchanged1000 CPs, and refuse the second daily offering again.

Both `-PirateFlowerWireOnly` phases pass in
`obj/pirate-flower-healing-fixed.log` on gameplay SHA256
`02480BEB0990A0D88D95855927D68E2F2A28A87A5D8F27896FA27960E7CDB583`.
The final receipt checks find12 flower pickups, one offering and one search
whose input is the earned bottle UID. No flowers, bottle or resulting chart were
seeded. This supersedes the open regression above for medicine/flower acceptance.

The fixture uses live normal monsters from families3609/3613/3629, allowing
ordinary respawns, and550-ms attack spacing. It does not supply damage, reset
monster HP, force deaths or bypass packet limits. Ordinary HP potions are used
through1009 during the hunt. Setup supplies eligibility, currency, two weapons
purchased through production handlers, their enhancement fields,30 healing items,
and proximity. Drop color and the daily clock are controlled; unrelated ordinary
money/CP/equipment drops are disabled in the private fixture. This proves the
quest path, not a combat difficulty or natural drop-rate benchmark.

[The healing fix and final-build checks](HEALING_MEDICINE.md#unsaved-combat-health-correction-2026-09-27)
record the subsequent atomic snapshot-holder improvement separately. The flower
run above is not mislabeled as running on that later executable. The final build
retains the same transactional health behavior. Native DX9 presentation,
movement/collision acceptance and live deployment remain open.
