# Protocol and gameplay integration evidence

## Inner Power progression transaction, 2026-09-26

Source evidence: existing `Network/PacketHandler.cs` case 2610 reads action at
byte 4, gong identity as uint32 at byte 5 and the second parameter at byte 9;
action 4 unlocks and action 3 trains. This is legacy-source interpretation, not
verified native capture. The request now requires a 13..64-byte body and exact
body-plus-eight-byte-seal length before those reads. No wire field was added.

`Game/Features/InnerPowerProgress.cs` commits book consumption with unlock and
commits each training level with cumulative potency spending. Inventory ownership,
binding, stack, warehouse, award checkpoints and progression revision are checked
before commit. Client updates follow commit; projection failure fences the session.
A repeated unlock is free of side effects. Training has no verified request nonce:
separate accepted requests train successive levels and pay separately until the
cap; this change does not claim network replay deduplication for training.

The installed `bin/Debug/database/NeiGongInfo.ini` supplies Universal Concept A's
second-rebirth/level-15 admission and five 1,500-potency levels. Test eligibility,
books and initial currency are supplied; pack opening, NPC purchase, packet
mutation and recovery use ordinary code. End-to-end book earning and native
acceptance remain pending. See `INNER_POWER_PROGRESS.md` for validation scope.


## Enchant persistence transaction, 2026-09-25

The action-28 field interpretation and packet serialization remain unchanged.
The handler's selected target/material identities and existing calculated outcome
now feed an owner-locked transaction; material consumption, target HP and receipt
commit together. [Validation and limits](ENCHANTING_TRANSACTION_AUDIT.md) distinguish
supplied-material server tests from native acceptance and patch-specific amounts.
The one-second receipt window suppresses immediate identical retries; it does
not invent a wire operation identifier or claim indefinite replay detection.

## Enchant request semantic guards, 2026-09-25

The existing ItemUsage action 28 dispatches target UID and material UID to
`ItemHandler.EnchantItem`. No wire layout changes. Server source previously
classified material by its narrowed suffix, allowing unrelated item identities.
Require the full existing gem-family range and quality suffix before enum
validation. The installed StrRes.ini and ChatTips.ini independently support cap
refusal and preserving a better prior enchantment; hashes and limits are recorded
in [the enchanting audit](ENCHANTING_TRANSACTION_AUDIT.md). Neither static strings
nor server fixtures prove the native interface, numerical increments or atomic
material consumption. Those gates remain open.

## Accessory equipment slot validation, 2026-09-25

Existing server-source evidence: `ItemHandler.EquipItem(ItemUsage, GameState)`
allows `IsAccessory` items to bypass ordinary item-category slot correction.
The caller's `dwParam` consequently reached the saved position and equipment
array without restricting it to hand-accessory slots. `ConquerItem` and
`Equipment` identify and project those slots as 15 and 16. Reject accessory
requests for any other slot before inventory mutation. No packet layout,
offset, serialization or new protocol family is introduced or claimed verified.
Native client acceptance remains pending; this is server-side validation of the
existing semantic field. The earned Terato fixture exercises invalid positions,
both valid hands and production item reload.

## Team command authority, 2026-09-21

The existing `Network/GamePackets/Team.cs` handler reads action and target UID
at offsets 4 and 8 of packet 1023. Its Kick, Dismiss and shared settings branches
previously accepted any team member. This is direct server-source evidence, not
a newly verified native-client layout. Keep the existing serialization; reject
buffers shorter than the fields read and require a current authenticated session.
For actions 6 through 13 require the actual leader object and its exact member
entry. A target UID cannot authorize the sender. Packet-handler fixtures cover
ordinary leader/member commands, spoofed targets and a replaced session.
This does not establish native UI behavior or serialize every team mutation.

## Trojan Epic journal projection, 2026-09-21

The installed 6711 Questinfo.ini has twelve ordered missions recorded in
`research/TROJAN_EPIC_BASELINE.json`, with its source hash. Its explicit counter
links are `[chk_quest 3272,1,1,1]` and `[chk_quest 3274,1,1,1]`; borrowing uses
`[chk_item 3003340,1]`. Project the two kill flags from durable traitor/final
victories using the existing 1135 objective-one field, and list states through
existing 1134 serialization. No new packet layout or energy field is inferred.
Client Begin and quit must refresh server-owned progress rather than mutate it.
Confidence: direct local journal metadata and existing server serializer behavior;
synthetic wire assertions are required. Native display and navigation acceptance
remain unverified. Production initialization waits for the complete world route.

## House storage prerequisites, 2026-09-20

Connected-box follow-up: paid ItemBox selection uses the existing action-5 preview
with regular-NPC type 4 and look 8200-8207. Placement remains the audited three
field 2030 request. Bounded action-0 clicks now select an owned nearby box and
request the existing warehouse window; sealed 1102 kind-20 requests reach the
owned container. Action 6 removes only an empty owned box. All box list/transfer
requests validate durable placement and access; no new packet layout is claimed.
The server route and source/acceptance distinction are documented in
[the item-box service](HOUSE_ITEM_BOXES.md). Native rendering, window selection
and password-window recovery are still acceptance gates.

Identity follow-up: the previously audited uint key at warehouse offset 4 is now
retained by the item model, storage enum, loader and Guild rollover snapshot.
Exact registered keys take precedence; low-bit compatibility lookup is limited
to the account identity or an owned sash identity. There is no wire-layout change.
The existing signed-integer database column accepts the reserved furniture range;
transfers reject keys above its range rather than wrapping them. See
[persistence and legacy-collision limits](HOUSE_STORAGE_PROTOCOL.md).

Box interaction follow-up: spawn parser 0x96E44D/0x96E456 maps protobuf field 7
to +0x40, then receiver 0x96E7B4/0x96E7BA stores that type in actor +0x3CC.
The ordinary click switch maps both types 3 and 4 to 0x724B34; 0x8D5931 sends
CMsgNpc action 0, selected actor UID, zero, zero through builder 0x92AA7B.
`FurnitureRequest.TryInteraction` decodes that exact bounded contract, retaining
all 32 actor bits. This decoder alone is not dispatched and grants no access.
Type 4 takes the ordinary NPC renderer and preview path, and selects storage
kind 20. Installed NPC model 820 is named ItemBox. These are static facts;
choosing type 4 for Revora boxes remains subject to native acceptance.

Follow-up: full-width key/kind storage lookup is traced through constructor
0x898DE8, getters 0x872D59/0x904B53 and receiver 0x97BC19. The UI branch at
0x4697A6 derives kind 10/20, but its house-box meaning is not yet established.
Reply reuse resets the item cursor and rejects writes beyond Count; no new wire
layout is introduced. Isolated build, 193 protocol fixtures and 40 hash-bound
static assertions pass. See [storage evidence and open gates](HOUSE_STORAGE_PROTOCOL.md).

Transaction follow-up: actions 1/2 now use checked, atomic item-location updates
before runtime projection and replies. No packet layout changed. Magnolia and
Guild fixtures previously constructed a 92-byte outbound packet as a request;
they now use the audited 172-byte sealed request. Build, 393 housing/storage,
838 Magnolia, 344 Guild season and 193 protocol checks pass. Box activation,
full-width ownership mapping and genuine-client acceptance remain open.

The approved client builds 1102 requests with declared length 164 (172 including
the existing seal), action byte 12, kind byte 13 and item UID at 18. Builder and
receiver evidence exposed the server reply-list setter writing action at 20.
It now preserves byte 12, returns capacity for empty lists, validates incoming
frames before field access and checks dictionary lookups. Build, 188 synthetic
protocol checks and 16 native assertions pass
([scope and unfinished ownership/storage work](HOUSE_STORAGE_PROTOCOL.md)).
House item boxes are not connected or deployed by this prerequisite fix.

## Furniture preview boundary, 2026-09-20

Approved-DX9 static inspection distinguishes two packet-2030 builders. The local
preview confirmation emits protobuf fields 4/5/6 and no actor UID; inbound
packet-2031 action 5 activates the preview with field 4. Fifty reproducible
instruction assertions and confidence limits are recorded in
[the furniture investigation](FURNITURE_NATIVE_RESEARCH.md). No inbound handler
was enabled by that research checkpoint. The subsequent
[furniture service](HOUSE_FURNITURE.md) now dispatches the bounded requests into
owned house/item transactions. Item use sends action-5 control (type 26 and the
selected terrain look); action-6 removal is answered with action-3 removal control.
The separate inbound request type avoids trusting a spawn UID or client-provided
NPC type. An isolated build and 44 synthetic checks validate the bounded decoder;
genuine-client presentation and complete native acceptance remain unresolved.

## Owned travel-scroll shop purchases, 2026-09-20

The housing earning fixture reproduced a saved-purse mismatch in the existing
silver shop branch: two 200-silver gates left runtime silver 998600 but saved
silver 999000, while both purchased items were durable. The repair retains the
existing 1009 purchase interpretation used by WeaponBookShop (shop UID, item
type, quantity and actions 1/55), validates that frame before use, and commits
the approved gate stock, silver and receipt together. No new packet layout or
native wire verification is claimed; real-client purchase acceptance remains a
separate gate.

## Housing NPC and private-map integration, 2026-09-20

The existing 2031 NPC dispatcher and byte option field carry House Admin's
session offers and option-40 confirmation; no new packet layout is introduced.
Existing dynamic teleport and portal paths carry the durable logical house ID
and the installed base terrain. Entry is checked against server-owned housing
metadata and saved arrival. Construction achievements come from committed
payments and levels, never client achievement assertions. Server dispatch,
walking and reload checks pass; native dialogue, interiors and HUD acceptance
remain unverified. See [scope and evidence](HOUSING_SERVICE.md).

The mined-material fixture additionally exercises existing mining-start data,
shop purchase, item-use, portal and jump handlers without changing their layouts.
Its Market route includes the server-accepted jump from 155,107 to 141,111;
walking alone cannot connect that entrance to the housing craftsmen. This is
server integration evidence, not a captured or rendered native-client journey.
See [fixture scope](HOUSING_MATERIAL_ROUTES.md#full-mined-material-acceptance-fixture).

## Guild transporter completed-journey evidence, 2026-09-20

The existing 2031 NPC option-one dispatch now routes all four installed guild
conductors through owned payment/arrival recovery. No packet layout changes.
Achievement 10117 comes from the completed server passage, never a client
completion packet or supplied destination. Existing map updates and title-login
projection are reused; interrupted achievement delivery is retried after the
normal authenticated login state is established. See
[route evidence and acceptance limits](GUILD_TRANSPORTER_EARNING.md).

## Classic dance achievement admission, 2026-09-20

Baseline `de95de0`: existing protobuf family 10010, action 81 reaches
`GeneralData.ChangeAction`; the server names emote 1 as Dance. Installed
`3dmotion.ini` provides actions 1..8 for all four ordinary body profiles,
including unarmed and weapon variants (SHA-256
`9951727aa1ed461fdbb083cc49fb35299fbd15a146a2289f842a6c5df33533f1`).
The installed spell catalog names 1380..1410 in steps of five as dances 2..8.
Achievement 10121's reviewed catalog condition is dancing in Twin City.

These are source/catalog associations, not a newly captured native packet or
verified animation. Existing framing/serialization is reused. Only canonical
accepted classic dance actions may produce evidence: no client achievement
claims, packet coordinates, high-word aliases or foreign UID establish it.
The basic dance needs no learned spell; the seven additional dances require
their available learned skill. Performer transformations and combat arts are
separate and remain unverified for this producer. Native 6711 UI/animation
acceptance is still required before deployment.

## Lottery pending result ownership, 2026-09-20

Baseline `4f070bb`. Existing packet 1314 reads a ushort action at byte 4 and
serializes a 34-byte response (26-byte body). Retain that serializer and admit
only 14..128-byte frames with matching declared length, ID and client seal,
and actions 0/1/2. This is source-derived framing, not a genuine-client capture.
Fourteen bytes cover the six consumed request bytes plus the existing eight-byte
seal. Do not infer the incoming request size from the 34-byte outgoing response.
NPCs 925..929 on map 700 start or restore a draw; subsequent actions require
the same nearby registered box and a result presented in the current session.
Persist exact pending prize, binding and two-reroll limit before presentation.
No native request nonce is verified: repeated initial requests restore the
pending result, claims are durable once-only, and mutating draw/reroll requests
also use a short session debounce. Delayed Continue requests after a claimed
result cannot be distinguished from a new intentional draw by this layout.

The existing 1314 property getters locate sockets at bytes 7/8, plus at 9,
color at 10 and rerolls at 11. Their setters incorrectly write two bytes for
the one-byte sockets/plus/color fields, so setting socket two can clear plus.
Write the individual bytes without changing offsets or total length. Validate
distinct field values in the serializer fixture; native presentation stays open.
Anniversary boxes also emit 1314 and share the old volatile prize fields, but
`TwinCityPlaza.IsClosed` explicitly closes every such NPC, and none is installed
on maps 1002/701. Preserve that campaign closure. New 1314 admission only serves
normal boxes 925..929; reopening the separate campaign requires its own repair.

## Lottery ticket exchange, 2026-09-20

Baseline `84b5535`. Reuse existing NPC 923 option 2 and inventory projections;
no new packet layout. Installed client/server catalogs identify 710212 as one
old ticket exchangeable for three 711504 small tickets. The dated 2016 official
Vegas guide confirms Lady Luck's exchange role. Exact evidence and remaining
draw/claim gaps are in [the restoration note](LOTTERY_TICKET_RESTORATION.md).
Only the canonical ordinary player beside the existing NPC in Twin City or
Market may exchange. Direct item-use cannot redeem the NPC-only item. Native
dialogue and lottery rendering remain unverified.

## Penitence Amulet use boundary, 2026-09-20

Baseline `fe2af78`. Reuse existing item-use packet 1009/action 4 and ordinary
PK detail/name-flag projections. Both amulet IDs are intercepted before legacy
item dispatch. Validate the existing 32..128-byte use envelope, declared size,
packet ID, full action and client seal before consuming an owned source.
This is source/fixture-confirmed behavior, not a new genuine-client capture.
Installed `region.ini` SHA-256
`939c68eb8a7d00b6a21adbf494ab421140b8f7b7f139f5eb7a2c9645ef404618`
identifies Deityland regions as map 10250; the server has `maps/10250.map`.
Use the map's BaseID for the location-specific cooldown. Catalog wording is
interpreted as VIP3+ overriding both location cooldowns; historical exception
precedence and native display remain unverified. No new packet layout.

## Warrior native monster-profile evidence, 2026-09-20

Baseline source commit `9b3270a`; extended the existing hash-gated x86 offline
GameData harness with four Warrior names. Native RoleDataQuery, loader RVA
0x173b9 and lookup vtable offset 0x2c resolve SealingStele and all three
MindofEvil variants in both reviewed catalogs. MaxLife at profile offset 0x10
is 30000/50000/100000/200000; Boss at 0x10c is 0/1/1/1 respectively.
Eight checks passed, plus 60 Abyss and 22 existing-boss regressions.
Dependency/catalog/probe hashes and provenance are in
[Warrior Epic evidence](WARRIOR_EPIC_QUEST.md#native-monster-profile-validation-2026-09-20).
Confidence: directly observed native data-loader behavior for these exact
artifacts. This does not establish rendered HUD behavior, model identity,
private-map navigation or any new packet layout. No protocol changes made.

## Warrior epic journal projection, 2026-09-20

Combat representation follow-up: the installed binary model catalogs bind
armor key 916000000 to the same mesh/texture 9997700 as NPC object 770
(Sealing Stele). Existing entity spawn/death and boss-health paths are reused;
no new packet layout is claimed. Revora selects existing monster body 917 for
TinyMindofEvil pending native review; adjacency to 916 is not proof of retail
identity. `tools/audit_warrior_models.py` verifies selected resources and hashes.
Installed exact-name profiles supply 30,000/50,000 HP and levels 72/70. Other
combat values are Revora balance choices, not verified retail statistics.

Installed 6711 `Questinfo.ini` (SHA-256
`f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14`)
orders missions 3678, 3679, 3695, 3680, 3681, 3682, 3683. Project their
Available/Accepted/Finished states through the existing 1134 status path from
the owned PostgreSQL route stage. No new packet layout or objective counter is
inferred. Client Begin and NPC Quit requests must not write these stages or
legacy quest records once the route is enabled. Saved receipts remain the only
progress authority. Native visual acceptance is pending; catalog ordering and
server projection tests are not proof of native rendering. The journal now
initializes with the server quest coordinator. NPC and owned jade handlers reuse
the existing bounded request paths; full route integration tests exercise their
frames without claiming genuine-client visual acceptance or deployment.

## Wall of Prestige, 2026-09-13

Owner wall.png shows an empty showcase and currency-derived personal prestige.
Local code sends ConquerPoints in the login 1151/2 row, ranks by ConquerPoints,
lists RevoraPoint, and ignores 3257. The 3253 table-based category scores are the
authoritative existing prestige calculation for this correction.

Read-only static analysis of the approved local `6711/Env_DX9/Conquer.exe`, SHA256
`3728BC3FD4E584D85301EDD2B1887A176267CDAA6356038F9951EF23E4FDED60`, confirms:

- CMsgRank receiver 008D2740 routes prestige types 80000000..80000010 to
  008D2401. Modes 1 (list) and 2 (self) use 80-byte rows at packet offset 24:
  rank at 24, score at 32, UID at 40, name at 48, level at 80,
  profession at 84, mesh at 88. Count is at 16, registered count ushort at 12.
- Mode 6 with ranking type 0 enters 008D27E6 and populates the wall. Its row
  differs: category at 40, character UID at 44; other fields as above.
  Category 80000000 forces the overall winner into profession group 0;
  other rows use profession / 10 (008902D4), including Windwalker group 16.
  Category-to-profession mapping is confirmed at 0088F47A and the category
  constants at 0059AC80..0059ACBF (Trojan/Warrior/Archer/Ninja/Monk/Pirate/
  Dragon Warrior/Water/Fire/Windwalker).
- CMsgRankMemberShow vtable C0F6F0 / receiver 00953372 handles 3257 action 0
  through 009531D3. The protobuf parser at 00952FC8 reads action field 1 and
  repeated member messages field 2. Members with field 1 == 1 are accepted.
  Member fields 3/4/5/6/7 are UID/name/guild/mesh/hair; 8..19 hold appearance
  (hat, garment, left weapon/accessory, right weapon/accessory, mount armor,
  armor, wing, wing plus, title, flag). Field 2 is the request parameter.
  The receiver copies these into its central-character display and refreshes
  GUI 0xF27. No client binary modifications are needed.
  Action 1 selects STR_DLG_PRO_RANK_USER_OFF_ONLINE, used when an inspected
  winner is offline rather than leaving the request without a reply.

Inspection harness and bounded disassembly are ignored under
`research/isolated/prestige-6711` in the parent worktree. Confidence: confirmed
exact-client static receiver evidence; live rendering remains to be tested.
Use one calculation for status, self, pages and winners, deterministic ties,
real row counts, and saved character data for offline ranking. Do not reuse
currency fields or load offline players through login/mutation routines.
The shared scorer retains existing category formulas except the erroneous
hardcoded two rebirths, now read from the character. Offline profiles rebuild
inside one read-only, repeatable-read PostgreSQL transaction. Read-only validation
loaded both existing characters and produced identical ordering after reload.

## Prestige cosmetic wing selection, 2026-09-25

Owner reports the wall displays the default equipment wing despite a selected
wardrobe wing. Further read-only inspection of the same approved DX9 SHA-256
above confirms that showcase member field 16 supports both identities:
009532F7 copies member+0x64 to display+0x2c; 0059CE7B..0059CE83 passes that
value and field-17 plus to 0059B54C. That function calls the item classifier
00878E37: item IDs in the 204xxx wing family classify as slot 22 and use the
ordinary wing path. Other positive values use the default base wing with the
same plus, then pass the supplied cosmetic identity to 0077A37A, storing it at
display actor+0xF08. Zero clears that cosmetic selection. No extra protobuf
field or client patch is required. Field 19 is not the cosmetic wing field.

Correction: carry the authoritative selected wardrobe wing into online and
saved prestige profiles; prefer that identity in existing member field 16 and
preserve the actual equipped wing's plus. With no selected cosmetic, retain
the original item ID (or zero when absent). Confidence: exact-client static
consumer evidence; successful native rendering after this correction remains
to be observed. Prestige scoring and underlying equipment are unchanged.

## Status/CharacterInfo correction, 2026-09-13

Owner screenshot `C:/Users/Administrator/Desktop/diag/status-debug.png` shows
`Domino: Code display error.`. Modern Revora already established the exact
6711 CharacterInfo contract in commit `d300806bc47518e649b2e70e9f4e6b5ef64296cd`
and display identity in `e87ed45` (modern worktree HEAD
`e90b24ca6bdb216accd3561e6616dde5c594dab9`). Evidence is in the parent
`docs/PROTOCOL_RESEARCH.md`, section "Exact 6711 CharacterInfo correction", and
`docs/6711_CHARACTER_DISPLAY_IDENTITY.md` (SHA256
`406F29AD37AE62699167CEB921794688F40848F0C383F04098A2E8D7A9C321D6`).

That recorded static analysis targets approved DX9 SHA256
`3728BC3FD4E584D85301EDD2B1887A176267CDAA6356038F9951EF23E4FDED60`:
receiver 009833EA reads hair UInt32 at 18, CP UInt32 at 30, EXP UInt64 at 34,
HP UInt32 at 72 and mana UInt16 at 76. Base attributes are already correctly
placed at 62/64/66/68 and unspent points at 70 in this legacy server.
Receiver 009834DA and the status display chain read a nonzero signed-positive
UInt64 identity at 140..147; Domino currency at 132..139 is separate.

The current legacy writer incorrectly writes CP as UInt64 overlapping EXP,
mana at 74 inside the HP field, and truncated HP at 76 inside mana. It omits
display identity entirely. Apply the modern confirmed field widths/positions,
using this server's durable positive character UID as its Revora display code.
Do not alter saved attributes, currencies, prestige formulas, or packet strings.
Confidence: confirmed modern static receiver evidence and local source mismatch;
no new native analysis or corrected-client capture is claimed for this port.

The screenshot's right-hand Level 3500 and Rebirth Times 2000 entries are
prestige contributions in the existing CMsgUserAbilityScore path (140*25 and
2*1000), not wrongly decoded character level/rebirth fields. The modern family
score response is a limited projection and is not evidence to replace this
server's prestige calculation wholesale.

## Auto-hunt gold collection, 2026-09-13

Local source inspection found no automatic pickup call: AutoHunt.Process only
toggles state and the manual ItemHandler.PickupItem requires the exact tile.
The existing one-second Screen.itemsCallback now attempts nearby gold pickup.
It reuses the existing FloorItem animation/removal, Money update, and GetMoney
writers without introducing fields or interpreting unknown auto-hunt options.
Three-tile terrain-visible collection is a Revora server behavior, not a claim
of official-client protocol parity. VIP 1 eligibility, ownership/team sharing,
15-second protection and 20-second expiry remain as configured in this source.
Confidence: high for inspected server behavior; genuine-client delivery remains
to be checked after approved activation. Gold uses Entity.Money and the existing
heartbeat/logout character save, not a new persistence format.

## Companion AI, 2026-09-13

Follow-up: restored companion jumping by reusing WorldActorMotion's existing
Data.Jump writer unchanged (old position in wParam1/wParam2, destination in
Xt1/Yt1, facing and timestamp). New six-tile jumps validate every interpolated
tile and diagonal corner before changing position; blocked paths retain walking
navigation. Existing client jump rendering is source-supported, while this
updated AI choice still needs in-game observation. No new packet layout inferred.

Source baseline: local Revora-6711 commit `e66abcaee9e089f0c88a8bb43a0d57843f9748f2`.
Existing `Chat` whisper dispatch, `GroundMovement`, `Attack`, `SpellUse`, and
`Data.RemoveEntity` writers are reused without new fields or packet families.
Local `magictype.txt` and `Handle` identify 1005 as Cure, 1000 as Thunder,
and 1002 as Tornado. Cure uses capped spell.Power healing; Thunder uses
Calculate.Magico times 0.5 and Tornado uses Calculate.Magico. Melee and bow
attacks reuse Calculate.Melee and Calculate.Ranged respectively. Confidence
is high for these existing server paths; client rendering and movement remain
unverified for this change until an ordinary-player Patch-6711 session.

Companion decisions are server-side. No Windwalker skills, packet layouts,
or official AI parity are inferred from client assets.

## Cultivation pricing evidence

Recorded 2026-09-13 against repository baseline `e66abca` and the local
cultivation-pricing change. Target remains genuine patch-6711 DX9. No packet
IDs, field offsets or packet layouts are changed here.

## Published prices

- [Official Chi guide](https://co.99.com/guide/guides/chi_system.shtml):
  1 CP purchases 4 Chi points. A zero-to-4,000 refill therefore costs
  1,000 CPs. High confidence in the published rate. Whole-CP rounding upward
  for an incomplete group of four is a Revora implementation choice; the
  guide does not specify rounding.
- [Official Jiang Hu pricing tips, 2013-07-25](https://co.99.com/news/2013-07-25/age_of_jiang_hu_tip_ii_how_you_assign_suitable_attributes.shtml):
  extra-course pricing increases by 10 CPs, capped at 500 CPs.
  [Official training event table, 2014](https://co.99.com/guide/event/2014/jianghu_training_spree.shtml)
  corroborates the 10-CP normal-training base. High confidence in those
  published prices; this is the classic-system schedule, not temporary sales.
- [Official Jiang Hu guide](https://co.99.com/guide/quest/2013/the_age_of_jiang_hu.shtml)
  states the free-training cost of 10 Study Points, one Talent and one Free
  Course. Its [restore screenshot](https://hw.99.com/uploads/co/images/guides/quests/2013/jianghu/v1/019.jpg)
  visibly shows a restore cost of 20. High confidence in the documented
  costs. Screenshots were inspected directly.

## Existing implementation boundaries

`Network/PacketHandler.cs` routes Chi buying, and its existing low-Chi study
path, to `GeneralData.BuyStrengthChi`. Pricing changes are server-side.
`Network/GamePackets/JiangHu/JiangHuCalculate.cs` interprets training action
1 as paid training, action 0 as free training, and Jiang Hu action 10 as
restore. These meanings are supported by existing source, but have not been
independently verified with a genuine-client capture during this change.

The existing `JiangHuUpdate` writer is reused to return restored attributes.
The old attribute must be copied before mutation to permit restoration, and
its pending snapshot is consumed once. No new packet family is introduced.

Premium-quality selections, pill/bound-CP payment paths, daily-reset behavior
and exact attribute probabilities are outside this pricing verification.
Do not describe this change as complete Jiang Hu parity. Isolated handler
tests use stubbed persistence, network and stat dependencies; a Windows client
session and durable failure/restart tests remain necessary for live validation.

## 6711 native NPC Buy investigation, 2026-09-13

See [SHOP_BUY_6711.md](SHOP_BUY_6711.md) for exact approved-binary addresses,
hash, confidence and the data-only 1.0.9 workaround. Static evidence shows
that native NPC Buy validates the silver price even for MoneyType=1. No
packet format or server payment behavior changed; live clicks remain an
owner validation step.

## Potency Hunt, 2026-09-13

[POTENCY_HUNT.md](POTENCY_HUNT.md) records the owner-selected difficulty,
probabilities, NPC route, persistence extension and validation. Existing NPC,
monster, floor-item and Inner Power update packet writers are reused. No new
wire packet family or inferred packet layout is enabled. The new applied-award
marker extends the local InnerPower.txt checkpoint format only.

## HorrifyingSpecter boss HUD, 2026-09-13

Owner follow-up: visibility is confirmed, but the native display reads
`10000/5000000` at full health. The server currently divides both spawn HP
and HP attribute updates by MaxHP/10000. Approved DX9 static evidence at
0x00448D49 formats current actor HP and 0x00779C7A(1) separately; 0x00778CB9
also reads current actor HP directly. This supports sending actual HP in
the existing protobuf spawn field 23 and HP attribute value, retaining
the exact-name client profile's actual MaxLife. No new packet layout.
Confidence is high for the normalization error; corrected native display
still requires an in-game check. Daily bosses and the five existing Boss
Rush profiles use the same correction (client patch 1.0.12).

See [BOSS_HUD_6711.md](BOSS_HUD_6711.md). Approved DX9 static evidence proves the boss-bar eligibility path rejects an absent exact-name monster profile. The server sends Boss=1 and normalized full HP, but client monster.dat knows family 2719 as CreepySpook rather than HorrifyingSpecter. A client-data alias is staged; no packet layout changes. Patch 1.0.11 was applied locally and publicly verified after owner retry authorization; native visual confirmation after client restart remains pending.

## Extraction and event update, 2026-09-13

The larger Expedition uses the existing dynamic-map teleport path with client base map 1015. NPC interaction, HP attributes, PK-mode updates and corner HUD messages reuse existing packet writers. Bird Island terrain comes from the installed server cache; existing AlienSerpent, SilverHawk and HorrifyingSpecter client profiles match encounter names and HP. No new packet family or inferred wire layout is enabled. Server fixtures cover the terrain and interaction gates; expanded native-client playtesting remains pending.

## 2026-09-14: daily boss names and native HUD eligibility

Patch 1.0.12 was insufficient: literal spaces in monster.dat section headers
are truncated by the approved DX9 GameData.dll parser. Static evidence at
RVA 0x173B9 and native runtime calls to that loader plus exact-name lookup
(vtable +0x2C / RVA 0x10CC5) reproduce all nine failures. Encoding header
spaces as `~` makes all four daily and five Boss Rush profiles resolve with
Boss=1 and the right MaxLife; displayed server names remain unchanged.
The approved dependency hash, provenance, test procedure and patch 1.0.13
hashes are recorded in BOSS_HUD_6711.md. Confidence: native runtime verified
for profile lookup and values; rendered HUD remains to be visually confirmed.
No packet layout or health serialization change is introduced by this fix.

## 2026-09-14: rune composition and prestige

Approved DX9 executable SHA256 remains
3728BC3FD4E584D85301EDD2B1887A176267CDAA6356038F9951EF23E4FDED60.
Static evidence: 0x00899037 builds message 1009, action UInt16 at 20,
target UID at 8, material count UInt32 at 26, material UIDs at 91,
declared length 91 + 4*count (excluding the eight-byte footer).
0x008F1CC3 calls that builder with action 63 for composition.
0x008F2460 computes the next-level cost as the difference between adjacent
cumulative rune_levexp rows. 0x008F17A1 and 0x0087A91F establish +1..+8
material EXP as 10,40,120,360,1080,3240,9720,29160. The client failure
path at 0x005EA9A7 compares the updated rune with its predicted result.
Server +Stone handling was commented out and returned an unchanged rune.

0x0041DE4E sums ability categories 21,22,23 for the displayed rune prestige
line. Installed ability_score.dat contains red/blue/yellow level scores in
those categories; the server only implemented categories 1..20 and its
offline item query excluded all rune slots. No new message family is needed.
Confidence: approved-client static evidence and installed data; ordinary
player persistence, replay and malformed-request fixtures cover the server
implementation. Native rendered UI confirmation is still separate.
Research output stays ignored under research/isolated/ghidra_scripts/rune-*.txt.

0x00898F95/0x00771D20 zero the native item message before the builder fills it;
offset 4 is not a unique composition request ID. Identical stack requests
therefore use a one-second duplicate suppression window, not permanent
packet-hash rejection. Single-use material UIDs and transactional row checks
protect already-consumed items after that window. The protocol cannot
distinguish a delayed duplicate from a deliberate later use of the same stack.

## 6711 wardrobe eligibility and title values (2026-09-15)

Installed client `ini/Storage.ini` SHA-256 `00490ada2256a196dbd0ffb645fd0ce7b9be51069aad57d1346aec9a44792db9` lists 72 unlocks. Decrypted `ini/title_type.dat` SHA-256 `178db69d4d5f1563622f4f6b8d517485b5fcff507892d0bd55366f5acd6155bb` has 71 type/ID/point tuples; decryption round-trips to the original. These are local primary data evidence, high confidence. Type 2039 appears only in Storage.ini, so no unsupported point value is invented. Wings mostly contribute zero title points. The old runtime catalog omitted rune and romance collection unlocks.

Existing packet 3301 writer emits a ushort length/ushort ID header followed by protobuf and an 8-byte transport footer. Reader is bounded to the payload after that header; the header must not be interpreted as a protobuf Fixed32 length prefix. Existing actions 0/3/4/5 and fields are retained. This framing is server-code evidence, not a fresh native-client capture; equip behavior still needs an in-game visual check. Incoming equip is authorized against live eligibility and saved ownership.

Jiang Hu score is promoted from ushort to uint to represent the documented 81,000 threshold. Existing packet 2703 rank serializer already writes uint scores; no wire width changes. Achievement requirements count distinct completed bits from the existing achievement subsystem; this change does not replace its legacy client-reported achievement ingestion.

## Live RVP inventory refresh, 2026-09-15

Read-only Ghidra analysis of approved DX9 SHA-256 `3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60` confirms CharacterInfo 1006 bytes132..139 call the 64-bit currency setter `007edb54`. The 10017 receiver `0093f76e` dispatches attribute `0x5b` (91) through `00943c7f..00943c98` to that same setter, with both 32-bit halves and refresh flag1. That flag calls inventory/UI refresh `0077d25f`. Attribute47 updates an unrelated racing field (`0078af51`, hero+0x42f0); attribute30 updates hero+0x11d4 and is also unrelated despite the old enum name Domino. Confidence: native static receiver and instruction evidence; no fresh in-game confirmation claimed.

Use existing protobuf 10017 attribute91 with the final unsigned RVP balance, including zero, for login synchronization and every increase/deduction. Keep currency persistence and the separate character identity at140 unchanged. Private raw research logs remain under ignored `obj/chi-refill-validation/titles-release/rvp-native-*`.

## Achievement authority foundation (2026-09-15)

Installed `Achievement.dat` SHA-256
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`
contains 337 unique positive achievement IDs mapping to 337 distinct positions
in thirteen 32-bit words. The existing Revora client-data transform round-trips
the decoded evidence exactly. The factual numeric catalog is locally verified;
the complete gameplay predicates are not yet implemented.

The retained server 1136 writer uses ushort length/ID at 0/2, uint action at 4,
character UID at 8, and word count or achievement ID at 12. Its full state is
68 declared bytes plus an eight-byte transport footer; its completion notice is
20 plus eight. This is existing server-code evidence, not a fresh client capture.
Read requests are bounded and validated against the declared frame; action 2
from a client no longer establishes achievement completion. The state and notice
writers retain their existing layout. Only committed server evidence may change
the displayed/completed achievement set. Legacy words are archived separately.

No new packet family is enabled. Malformed frame and identity checks, catalog
mapping, concurrency and persistence require fixtures; genuine 6711 rendering
remains a separate roadmap gate.

## Local CTF restoration — 2026-09-15

The existing 6711 NPC 2031/2032 dialogue, movement and SobNpc spawn/combat
paths provide the local event interaction. The installed Achievement.dat (SHA-256
df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc)
maps 10230 to planting a CTF flag and 10301 to winning a CTF round. Numeric
catalog evidence is confirmed; a new live client earning session is pending.

Legacy CTF packet 2224 and native ranking requests have no captured 6711
validation in this task. Keep their dispatcher disabled and remove their runtime
sends. Use existing NPC dialogue, flag NPC mesh 8910 and normal chat for event
instructions/results. No new packet layout is asserted as verified. Carrying flag
status 118 is an existing source mapping; its visual presentation still needs a
genuine client test. The server owns possession independently of that status.
## Local Elite PK restoration — 2026-09-15

The compiled legacy level groups are <100, 100–119, 120–129 and 130+ in
Game/ElitePKTournament.cs. NPC 448582 enters through the existing NPC dialogue
dispatcher. Dynamic map 700 teleport and standard combat are established server
paths, reused without changing their layouts. The newer official guide also
describes rebirth/BP editions; those are not evidence for changing this 6711
level edition. Native Elite PK ranking/bracket/wager packet handlers have no
genuine-client validation in this work and remain disabled in the restored
edition. NPC dialogue and normal chat supply registration, watching and results.

Champion wing key 60010020 and legacy title value 16 are numeric existing source
and installed catalog facts. The seven-day award is measured from the committed
result, not a claim request. Champion title ownership is reconstructed from that
award. No new wire layout is inferred or claimed verified. Live 6711 appearance,
watching and equipment checks remain separate acceptance gates.

## Demon Exterminators restoration — 2026-09-15

At `513ba7c`, installed server data has NPC 2000 with mesh 1640 and jar item
750000. Client `NpcType164` names CloudSaint. The old jar handler uses attack
subtype 36 and ResponseDamage for the count; jar maximum durability carries a
monster identity in the legacy loader. These are source/data observations, not
captured packet verification. Reuse the existing count response with server-owned
identity and progress; do not accept incoming counters as quest evidence.

The installed client mission 6012 at NPC 2000 is a different weekly quest
(Crisis of Evil), not Demon Exterminators. Do not reuse that mission or claim
native quest-tracker support. Brief NPC progress and the existing jar request
provide this edition's UI; genuine 6711 jar rendering remains unverified.

## Moon Box restoration — 2026-09-15

Installed source/database and read-only client data agree on tokens 721010–721015,
Soul Jade 721072, Moon Box 721090, Fortune Teller 600050, Maggie 600003, tactic
Ghosts 600005–600017 (odd IDs), and Life ghosts 600018–600042. Client GameMapEx
names 1043–1048 as the six token tactics, 1049 Death, 1050 Life; 1042 is the portal.
Installed ToughHorn families 6000–6006 populate the seven hostile maps.

Client mission 2378 describes this chain, but its historical activity dates are
expired. Do not claim native tracker visibility from that ID alone. The restored
route uses existing NPC, item, map and achievement packets without a new layout.
Server-owned run/token identities authorize exchange and final completion 11022.
Live visuals and any tracker content patch remain separate validation work.

Follow-up achievement catalog inspection establishes that 11022 specifically
requires receiving the Moon Box from the quest NPC. The alternate Meteor ending
must not grant it. The box-only predicate replaces the broader initial quest-end
interpretation before deployment; no live achievement migration is required.

## Power Arena restoration — 2026-09-15

Read-only installed catalogs identify manager 500010, Agates 3956–3962,
maps 1730–1736, ArenaSentinel families 2133–2139 and Refinery Badge 720148.
Achievement.dat (SHA-256
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`)
defines 11013 as passing floor seven and 11014 as receiving twelve Arena badges.
Neither possession of purchased badges nor a client completion packet proves
these conditions. Existing item/NPC/achievement packets suffice; no new layout
is inferred or described as captured. Native mission tracker and client visuals
remain unverified. The official guide supplies the two daily hours, seven floors,
defeat/re-entry behavior and 2/6/12 badge pack prices; unspecified counts and
per-session reward limits will be explicit Revora rules.

## Quest-list buffer correction — 2026-09-15

The existing 1134 writer and indexer both use an 8-byte header and 12-byte quest
records (ID, status, time), followed by the server's 8-byte transport suffix.
Its allocation incorrectly used `20 + 8 * count`, which matches one record but
underallocates multi-record lists. Allocate `16 + 12 * count`, bounded to the
existing 80-record page size. Guard append/index access before writing. Validate
inbound framing and the Begin record before indexing; unknown client-authored
quest IDs cannot enter the catalog. A List request carries no required record
body, so its count is not treated as proof of attached records. This is a
source-proven memory-bound correction, not a captured 6711 packet claim.

Validation: the isolated build and `Verify-PotencyHunt.ps1 -QuestPacketsOnly`
pass nine checks, including every 0–80 record count, trailer preservation,
over-capacity writes, malformed Begin requests and more-than-80-entry GUI paging.
Known Begin requests produce server-authored Accepted status; the original
client buffer is not echoed back as a second status update.

## Release the Souls earning bridge — 2026-09-15

The installed Questinfo.ini mission 6049 is active daily content: minimum level
70, three soldier remains, Desert Guardian reference 301 at map 1000 (536,643),
and Chi Token 729304 / 50 study points in the reward description. This agrees
with the newer three-soul guide, while older official editions required eight
or sixteen. Installed server NPC 15140 occupies that exact guardian location;
301 is unused in the installed server NPC table. Remains 65822–65840 are nineteen
installed NPCs on map 1000. Use a runtime 301 alias for the guardian to match the
client's mission target, retaining its installed mesh 8180. Remains use mesh 8170.
The native mission's count is a projection of saved distinct NPC visits, never
an incoming 1134/1135 counter. Reuse existing 1134/1135 and inspection animation
writers; real-client animation, pathfinding and tracker acceptance remain pending.

## Quiz Show transport decision — 2026-09-15

The parked 2068 handler reads bytes 4 and 8 before adequate validation, lacks a
single-answer guard and ignores any question sequence. Source offsets alone do
not verify the genuine 6711 answer layout. Disable inbound 2068 gameplay globally;
do not restore it from guessed offsets. The Revora quiz edition uses the existing
NPC request/dialogue path instead: a server-issued prompt identifies the session
and question; four distinct per-question option IDs select one answer. Saved
answers, deadlines and results are authoritative. No new packet layout is claimed.
The NPC route needs live-client presentation acceptance. Restoring the native
quiz panel remains a separate evidence and compatibility task.

## Trade settlement prerequisite — 2026-09-15

The existing 1056 implementation decodes its action at offset 12, a 32-bit
request/item/CP value at offset 4, and an unsigned 64-bit gold value at offset 4.
Its own writer allocates 20 bytes with declared length 12; this is source evidence,
not a new captured-client layout claim. Preserve those fields while bounding
incoming buffers before decoding. Do not infer a different layout from the
transaction work. Genuine-client request/accept display acceptance remains pending.

Before this change, settlement transferred inventory items independently of payment
and took player locks in caller-dependent order. Summer's unbound wing items expose
that path, so atomic settlement is a prerequisite: stable pair lock
order, reciprocal live-session ownership, exact offered items, confirmed balances,
one durable exchange receipt and post-commit inventory/currency installation.
Offer changes must revoke prior acceptance. These are server transaction semantics,
not a new packet family or an assertion that all historical trade behavior is verified.

## Sky Pass mission projection — 2026-09-15

The reviewed 6711 `Questinfo.ini` names mission 6350, Daniel 30055, God Cloud
30054 and prize item 721109. Its terminal condition references objective 6.
The existing 1135 writer already supports that uint objective slot inside its
84-byte buffer. Reuse the existing writer and quest status family; saved server
progress remains authoritative, including native Begin requests. This is client
content plus source-layout evidence, not a new packet capture or live UI claim.

## PK halo identity correction - 2026-09-15

Installed client `ini/Title.ini` SHA-256
`62DE06F8145472CB53F15608F06268C4A062F8C6AA23599BF34DBBDA137FD46E`
defines title-list IDs 20/21 as cross-server Elite PK first/second place.
They are not weekly/monthly PK honors. The legacy source's `IsWarTop` range
incorrectly mixes compact database status keys with this native title-list family.
The first new local-PK batch inherited that collision; correct it before deployment.

Installed `ini/statuseffect.ini` SHA-256
`05FB159E8BD4AA30DD88A9F41D774A80D05B1DE79893D969ABFF0CAD756A3BDF`
maps status indices 36/37 to the monthly/weekly halo effects. It also contains
the established class status effects at 38-43, Monk at 114, Pirate at 122,
Dragon Warrior at 154 and Wind Walker at 203.
This is strong client-content/source interoperability evidence, not a new
captured packet or a claim of live visual acceptance. Reuse `Entity.AddFlag` and
`RemoveFlag` with the existing effect-vector writer; do not add or reinterpret
packet fields. Persistent result expiry governs the projected halo. Do not put
compact legacy status keys into `TitlePacket` or issue cross-server title IDs
from a local event. Archive old status rows and ignore their former projection.

The same reviewed status-effect catalog maps index 51 to `mate_pk_01`, the Couple
PK halo. The restored NPC edition projects that flag from an unexpired committed
pair result; legacy database key 8 is archived and excluded from title lists.
Existing interaction actions 46-50 do not establish trustworthy paired consent
or a verified 6711 holding-hands/hug pose. No new interaction layout is enabled.
Both spouses instead confirm through the existing NPC request/dialogue family,
with an expiring, session-bound server-issued option code. Native pose and halo
visual acceptance remain unverified.

## Subclass progression request bounds - 2026-09-15

The existing 2320 dispatcher reads the action at offset 8 and subclass at offset
10. `SubClassShowFull` writes a ushort action at 8 after the timestamp, whereas
the older `GameSubClass` helper uses a different action location. Preserve the
active dispatcher/ShowFull family, require a complete bounded declared frame,
and validate the full ushort action. This is source-layout evidence, not a new
client capture. No client-provided level, phase or study balance authorizes a
mutation; each request advances at most one server-owned step. Duplicate learning
is idempotent; repeated valid upgrade commands request successive paid levels.
Do not invent a nonce or expected-level field in this unverified request layout.

## 2026-09-15: rune loadout transaction boundary

Source baseline `0a8b247`. Existing active `RunesSystem.HandlePacket1009` and
`ItemUsage` identify family 1009, UID at 8, destination at 12, action at 20,
and a separate timestamp at 22. Actions 57/58/61 select equip, unequip and
inventory/rune-bag transfer. Preserve the two-byte action boundary, rather than
accepting arbitrary high action bits or treating timestamp bytes as the action.
This is source-supported behavior; no new native capture or nonce is claimed.
Loadout requests retain the existing minimum 38-byte frame and declared length
including the eight-byte trailer convention, with a bounded 128-byte maximum.

The installed achievement records 20227/20228 identify 11310 (equip one rune)
and 11311 (equip five runes), SHA-256 of Achievement.dat:
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.
Use real persisted valid rune slots, with five within one loadout; do not combine
partial primary and alternate sets. Class-specific full-level predicates remain
unconfirmed and disabled. The new persistence boundary passes 36 direct checks;
these two producers are enabled in the staged source. Native loadout UI acceptance remains pending.

Rune skill removal reuses the existing `GameState.RemoveSpell` projection:
`Data` action 109, character UID and spell ID in dwParam. It is sent only for
removed derived rune skills after commit, without a separate spell-table write.
The next login rebuilds rune skills from saved equipment. This is existing writer
evidence, not a new native capture.

## 2026-09-15: refinery application and stabilization boundary

Source baseline `05668be`: active `Purification` uses family 2076, mode byte 4,
owned target UID at 8 and material UID at 12. `ItemAddingStabilization` uses mode
byte 4, target UID at 8, count at 12 and four-byte material UIDs starting at 16.
Keep these existing source layouts and bound lengths/counts before reading them;
no captured native layout or replay nonce is newly claimed. The existing 2077
`ItemAdding` writer projects effect identity, quality, percent and remaining time.

The [official refinery guide](https://co.99.com/guide/guides/refinery.shtml)
describes a seven-day equipment effect, VIP extensions, six stabilization costs
10/30/70/150/270/430, retained credit from earlier stabilization, and restoration
of the permanent effect after a temporary replacement expires. The installed
server had used the artifact cost function for refinery and had no sixth tier
in its refinery table. Apply each material to a matching equipment family;
bound materials require bound equipment, consistent with the
[official bound-item rule](https://co.99.com/guide/items/bound_cps.shtml).

Implement owned transactions for both transitions, preserving a permanent
`itemadding` row below the current temporary row. Existing stabilizationpoints
can retain earned credit. An insufficient selected stone batch consumes nothing;
one selected UID consumes one stack unit. Paid stabilization grants only the
required cumulative credit, not extra credit for overpayment. Legacy permanent
effects retain the minimum credit associated with their existing level; this is
a compatibility migration rule, not proof of the amount historically paid.

When an expired refinery has no permanent fallback, send the existing
`ItemSoul.Expired` projection before the updated equipment. A permanent fallback
uses the existing refinery update instead. Both follow the storage commit.
Stabilization family is the existing `PacketID.ItemAdding` (1038), distinct from
the outgoing 2077 effect writer. Native appearance remains pending validation.

## 2026-09-15: mailbox claim restoration

Baseline `d4886a5`: active family 1045 reads action at offset 4 and mail ID at
offset 6. The old four-byte action read overlaps the mail ID, making most
nonzero IDs change the action. A two-byte action is the source-consistent
interpretation; preserve the existing ID offset and bound the frame before
reading. Confidence: inferred from the active source's overlapping fields, not
verified by a new native capture. The existing 1046 list, 1047 notification and
1048 text writers remain the presentation boundary. Native acceptance is pending.

Claims must compare a loaded mailbox revision with the durable `prizes` blob,
consume an attachment and persist its receipt state in the same transaction.
An item attachment transfers its original owned escrow UID; it must not recreate
only the currently visible refinement under a new UID. This preserves all item
columns, both refinery rows, cumulative credit and other UID-keyed metadata.
Deletion of a mail with unclaimed rewards is refused. Gold and CP claims must
check purse preimages and update client balances only after commit.

The existing 1009 returned-item action 50 delegates to the same durable mail
entitlement; its client-provided UID alone no longer grants ownership. Its native
presentation and any non-mail return origins remain unverified. The 1046 list
request retains the source's page offset 8 with bounded framing and pagination.
Repeated list construction resets its write offset, and out-of-range unsigned
pages return an empty list without signed-index overflow. Legacy stored letter ID
zero remains valid; a read-only audit found it in 10,168 existing mailboxes.

## 2026-09-15: Dis City instance integration

Baseline `818b618`: the existing four-argument teleport sends the supplied native
base map but reads status flags from shared base metadata. Dis City's 2021-2024
terrain files coexist with house aliases in that metadata. An owned instance
must retain its explicit dungeon base and use its own map-status record through
the existing `SendCurrentMapStatus` projection. No packet fields are added.
Source confidence: confirmed code path; native terrain/status acceptance pending.
Missions 6472-6475 and achievements 11002-11005 retain the installed stage
association documented in `DIS_CITY_RESTORATION.md`. Their existing task packets
are projections only; a client quest-begin request cannot grant stage completion.

Tracker correction at baseline `bd8f6e6`: each installed Dis City intention uses
`chk_quest mission,1,1,1`, so sending a raw kill count in counter 1 implies readiness
after the first kill. Project an objective-ready boolean in counter 1, retain raw
progress in counter 2 and the frozen target in counter 3. This reuses the existing
1135 writer; native checkbox rendering remains unverified. Blank `FinishNpcId`
fields exist in the installed catalog. The private overlay uses that established
content form because a fixed public-map link cannot target an owned dynamic map;
the issued mirror provides the actual return to the stage guide.

## 2026-09-15: Dis City treasury content profile

At `d34599b`, installed `npc.ini` SHA-256
`b06db1e2484992a1b58eabd7a3d884da07ac728a4a183d0fd18f2dccd5b20497`
contains HellCrystal profile 3748 and HellTreasury profile 3749. The existing
NPC mesh convention selects these with meshes 37480/37490. This replaces the
generic relic profile used for the first crystal implementation; no NPC packet
fields change. Appearance remains subject to native acceptance.

Neither reviewed map catalog has map 2025. Installed `GameMap.dat` SHA-256
`86a54928a9682617371542cdb5a72707c799b92a5c820e3581183adaa94f32b0`
has map 3954 at `map/map/treasure.7z`. Its server cached floor is 120 by 120,
with 2,360 walkable cells; (46,48) and (49,45) are walkable. Floor SHA-256:
`efc6436eb9274f9fb6e0fd088817d5cd850c3773ca7c848b7db0056fc9b7a22e`.
Revora selects this existing treasure room for owned treasury visits. This is
a documented local map selection, not proof of the original Dis City map identity.

## 2026-09-15: attribute allocation ownership and numeric bounds

Baseline `11b86d4`: `PacketID.Atributes` is 1024; the existing dispatcher reads
four UInt32 deltas at offsets 12, 16, 20 and 24. Its transport contract uses a
UInt16 payload length at 0 and an eight-byte `TQClient` suffix. These are confirmed
source conventions, not a new native capture. Preserve the existing offsets and
echo; reject frames whose complete declared payload does not contain all four
fields. The allocation helper validates framing, type and suffix independently;
the outer dispatcher must reject buffers shorter than its header/suffix minimum
before reading them. No new packet fields or client identities are introduced.

The old UInt32 sum can wrap (for example, maximum UInt32 plus one), and each
target stat is only UInt16. Widen both the sum and destination checks before any
mutation. Use the authenticated canonical player and the character's save lock
for the entire allocation, including derived-stat refresh. EXP read/modify/write
uses the same owner lock. Native UI acceptance remains pending. There is no
verified request nonce in this packet: repeated valid deltas may spend remaining
points, but cannot create points or exceed the available balance. This change
does not claim a durable per-request receipt or replace the legacy heartbeat save.

## 2026-09-15: reincarnation request bounds and recovery ownership

Baseline `7e82b6b`: `PacketID.Reincarnation` is 1066. The existing handler reads
the class byte at offset 8 and a body-selection byte at offset 12. Require at
least 13 declared payload bytes plus the existing eight-byte `TQClient` suffix,
consistent declared/actual length, matching packet type and suffix, before any
restoration, body change, item consumption or CP charge. These are verified
source accesses and existing transport conventions, not a new native capture or
proof of all fields in the genuine client packet. Additional payload bytes retain
their previous treatment. Require the canonical eligible player and the owner
lock; preserve the currently configured service thresholds and prices. Native
panel acceptance and the full paid-reset transaction remain open.

## 2026-09-15: mentor EXP claims and relationship framing

Baseline `b436353`: mentor reward packet 2067 reads UInt32 action at 4 and writes
the server-owned UID at 8, EXP at 24 and UInt16 blessing/stone fields at 32/34.
Its existing constructor emits a 40-byte payload plus eight-byte suffix. Require
at least 36 payload bytes before using the legacy response fields, matching type,
declared length and the existing `TQClient` suffix. Actions 1 and 2 retain their
existing shared claim behavior. Client-supplied balances and mentor UID cannot
authorize or size a claim; owned database contribution rows determine its amount.

Relationship packet 2065 has a 24-byte fixed payload prefix with type/UID/parameter
at 4/8/12 and online/name-length fields at 20/21. Require that complete prefix,
declared length, type and suffix before relationship changes. Response name
replacement copies only the fixed prefix, so a shorter server-owned name cannot
overflow its new buffer when replacing a longer incoming packet. No new native
fields are inferred. These are existing source accesses and transport conventions;
full genuine-client claim/social UI acceptance remains unverified.

## 2026-09-15: weapon-book purchase request boundary

Baseline `a7e26b5`. The existing 1009 `ItemUsage` reader and shop dispatcher
read shop UID at 8, item type at 12 and quantity at 26. The reviewed two-byte
action at 20 is distinct from the timestamp at 22. The existing dispatcher
routes actions 1 and 55 to NPC buying; preserve those source-supported aliases.
For the sixteen reviewed weapon books, require at least 30 payload bytes plus
the existing eight-byte `TQClient` suffix, at most 128 total bytes, matching
type and declared/actual length. These are source bounds, not a new native
capture. Quantity zero retains the existing one-unit meaning; purchases are
bounded to forty nonstacking books and available inventory slots.
The outer `ItemUsage.Process` now requires the full 22-byte action prefix
before its existing action accessor runs, so short input cannot throw before
the book service's stricter complete-frame check. This is not a claim that
every other action's variable fields have been reviewed.
Reviewed book buys dispatch from the two-byte action at 20 before the legacy
action accessor's equipment fallback at byte 16. An opaque byte at 16 must not
turn a valid book buy into an equipment-set switch.

No request field has been verified as a unique purchase nonce. Suppress a
repeated owner/shop/item/normalized-quantity purchase within one second using
committed database receipts. This is a durable debounce across reconnects,
not exactly-once identification of arbitrarily delayed replay. Later matching
requests are new purchases. Native buy/multibuy acceptance remains pending.

## 2026-09-15: auction boundary and listing identities

Baseline `944a012`. The existing `Auction` reader uses family 1320, action byte
at 8 (list/cancel/bid/buyout = 0/1/2/3), item reference at 12, currency byte at
16, reserve/offer at 20, buyout at 24, duration byte at 28 and confirmation at
32. Its constructor has a 38-byte payload plus the eight-byte transport suffix.
The old bidding response also emits a 36-byte payload (44 total), covering
all accessed fields including confirmation at 32. Accept at least that complete
fixed prefix, up to 128 total bytes, with declared length, family and suffix.
These are source-supported bounds, not a native request capture.

The 1321 source defines page count/total/page fields at 46/48/50, each UInt16,
and a record beginning at 52. A full record ends at 120, so its complete frame
needs 128 bytes including the suffix. The separate old `MsgAuctionItem` builder
allocated only 120 and overlapped the last eight bidder-name bytes with the
transport suffix. The existing `AuctionQuery` builder's 128-byte size fits.
Source confidence is high for these accessed bounds; native paging semantics
remain unverified. The 1322 builder has a 20-byte prefix, one 68-byte record,
and an eight-byte suffix. Preserve those existing response fields.

No input timestamp or field has been verified as a unique auction request
nonce. Reusing the physical item UID for successive listings permits an old
request to name a different sale. The integration will assign a durable,
non-reused display UID to each listing UUID and remap only its preview packets.
The original physical item and adding rows retain their real UID. The client
is inferred to treat the listing reference as opaque; this mapping requires
genuine 6711 list, detail, bid, cancellation and delivery acceptance before
deployment. It must not be described as native-verified based on fixtures.

New listing requests still identify the inventory item before a listing exists.
No unique creation nonce is verified. A durable one-second owner/item debounce
suppresses rapid cancel/relist repeats; a later creation request is treated as a
new listing. This does not claim exactly-once recognition of arbitrarily delayed
creation replay. Buy, bid and cancel requests instead resolve a permanent listing
reference, including after relisting or reconnect.

## 2026-09-15: friend consent and achievement boundary

Baseline `6187b86`. Existing family 1019 accesses a peer UID at 4, action byte
at 8, online flag at 9, rank data at 12, sex at 16 and a sixteen-byte name at
20. The constructor emits 72 payload bytes plus the suffix. Friend commands
10/11/14 are apply/accept/remove; enemy removal is 18. Require the complete
36-byte accessed prefix plus suffix, declared length and family before parsing,
with a 128-byte maximum. These are inherited source bounds, not a new capture.

The supplementary 2071 request-information builder has distinct requester and
receiver UIDs at 4 and 8. The old friend request filled both with the receiver.
The replacement uses the actual sender and recipient. No overlapping optional
flag fields are newly interpreted. Outgoing 1019 names remain within their
sixteen-byte slots; incoming names, rank and online claims authorize nothing.

Only a live pending request bound to both canonical sessions can authorize an
accept. No unique acceptance nonce has been verified in 1019. The server owns
the short-lived invitation and committed consent receipt; native request,
acceptance, list refresh and achievement presentation remain acceptance gates.

## 2026-09-15: trade partner consent and probation

Baseline `53f19a1`. Family 2046 uses UID at 4, action at 8, online at 9,
a signed minute countdown at 10 and sixteen name bytes at 16. Its existing
constructor emits 32 payload bytes plus the eight-byte suffix. Actions 0/1/4/5
are request-or-reciprocal-accept, reject, break and server add. The old
HoursLeft setter writes hours multiplied by sixty while its getter reads raw
minutes. Use bounded complete frames (40 through 128 bytes), family/length/seal
checks and explicit minute serialization; do not infer a new accept action.

The same native action names a player on request and acceptance. A server-owned
pending request bound to both current sessions authorizes the reciprocal action.
There is no verified nonce; arbitrarily delayed identical packets cannot be
identified across a later new request. Native request/accept/reject/break,
countdown and locked-item trading remain genuine 6711 validation gates.

The official [Trade Partners guide](https://co.99.com/guide/guides/tradepartners.shtml)
confirms consent, twenty partners and a three-day probation. The installed
achievement catalog maps 10126 to adding a trade partner. This establishes the
server earning rule, not packet capture verification or full Forbidden Sale
System equivalence. Existing Revora locked-item trading remains the scoped
consumer and must validate the saved mutual relationship at settlement.

## 2026-09-15: mentor admission consent

Baseline `ba2c13e`. Existing family 2065 uses action uint at 4, UID at 8,
dwParam at 12, response byte at 16, online at 20, one-byte name length at 21
and name bytes at 22. Requests 1/2 select the peer through dwParam. The server
sends invitations 8/9 with requester UID at 8 and receiver at 12; the existing
accept handlers select the requester through UID and require response byte 1.
Preserve these meanings; incoming names and unused response fields have no
authority. Bound complete frames to 32–128 bytes and names to sixteen encoded
bytes, with the suffix outside the accessed name. The old Name setter allocated
by character count; replacement encoding is byte-bounded. This is inherited
source evidence, not a verified native capture.

Accepted admission requires an unexpired owned invitation with matching direction
and both canonical sessions. No unique native acceptance nonce is established.
Native invitation/response, list display and achievements remain pending.

Related 2066 output keeps both inherited record forms. MentorInformation's 158-byte
record uses name length/data at 77/78, spouse at 79 plus encoded mentor length,
and apprentice at 81 plus both preceding encoded lengths; its one-byte gaps are
preserved without claiming their meaning. Names are assembled at serialization
so setter order cannot overlap them. ApprenticeInformation uses the existing
three-name list at 76. Three sixteen-byte names need 128 payload bytes plus the
suffix, so its old 112-byte allocation grows to 136 only when required. Null
spouse names become empty; byte encoding, individual limits and suffix space are
enforced in both forms. These corrections preserve source layouts; genuine
6711 display acceptance is still required.

## 2026-09-15: mentor departure and retained EXP

Baseline `11e32e1`. Existing 2065 action 3 leaves the acting player's mentor;
action 4 selects an apprentice through dwParam at 12. Existing server notices
use 18/19 (dump apprentice/mentor) and the 2066 apprentice information record
with zero level/class for removal. Preserve these action families and canonical
actor/target identities, with the same bounded 2065 frame validation. A packet
cannot choose another player's mentor departure or forward a forged notice UID.
No new countdown field or nonce is claimed. Native removal and reward refresh
remain acceptance gates.

The [official FAQ](https://co.99.com/guide/faqs.shtml), section 10i, resolves the
guide's reset wording: a departing apprentice pays the mentor 50,000 silver and
cannot apply to become an apprentice for three days. Thus membership ends now
and the child has a saved admission cooldown. The guide's general reset interval
is applied to expulsion too; this extension remains an explicit Revora rule
pending more precise original expulsion evidence. Existing 2067 EXP actions 1/2
continue to claim an owned amount; retained EXP joins that same transaction.

## 2026-09-15: mentor +1 claim boundary

Baseline `8f154f9`. Existing 2067 action 4 claims +1 Stones, with the displayed
ushort contribution value at 34 in the 48-byte record. Keep the existing response
shape and source-authoritative balance; an incoming balance/UID cannot authorize
delivery. The official mentor guide establishes 100 contribution points per +1
Stone. Existing Revora delivery uses item 730001 with Plus=1 and unbound state.
Preserve those item facts while committing debit and item ownership together.
No new protocol family or wire completion nonce is inferred. Native reward-panel
and item presentation remain acceptance gates.

## 2026-09-15: mentor blessing units and unchanged claim frame

Baseline `8f154f9`; evidence is local `MentorPrize`, `GameState.AddBless`,
`Entity.HeavenBlessing` and the one-second online countdown. Existing 2067 action
3 claims blessing. Its ushort view at offset 32 represents stored contribution
hours: the earning code divides source seconds by 10, 60 and 60. The old claim
passed that hour count directly into a seconds-based grant. Confidence is high
for the local unit mismatch; genuine 6711 presentation remains unverified.
Claims now convert whole hours to seconds and retain every undeliverable hour.
The 2067 layout, client seal, action values and outgoing status updates are
unchanged. Supplied reward totals and mentor IDs never authorize a claim.

The [official mentor guide](https://co.99.com/guide/guides/mentors.shtml), reviewed
2026-09-15, confirms Praying Stones as the blessing earning source and dropped
+1 equipment as the composing source. It does not establish the exact blessing
ratio. This batch preserves Revora's existing one-whole-hour-per-36,000-source-
seconds rule for existing AddBless callers; it does not claim those callers are
all official earning sources. Dedicated item-consumption/source transactions and
native acceptance remain open. Claim delivery bypasses AddBless so it cannot
recursively create upstream contribution credit. The existing online-only
countdown policy is preserved, with normal saves now retaining countdown ticks.

## 2026-09-15: Praying Stone use boundary

Baseline `53d053c`. Existing 1009 EquipItem action 4 dispatches inventory consumables
through ItemHandler.UseItem. Existing item IDs 1200000/1200001/1200002 grant 3/7/30
days respectively. The [official Heaven Blessing guide](https://co.99.com/guide/guides/heavenblessing.shtml)
confirms those three durations and right-click use. The existing mentor guide
limits the documented earning source to Praying Stones; generic event blessings
must not recursively earn mentor hours. Keep Revora's existing whole-hour ratio
until its original exact ratio is evidenced.

No frame extension is inferred. Saved selected-item ownership, exact stack debit,
duration grant and mentor credit will be one transaction. The inherited use path
has no proven durable per-click nonce. A bounded one-second selected-UID debounce
can suppress rapid repeated stack use, but cannot establish indefinite wire replay
identity. Genuine 6711 right-click, duration/mentor panel and stack presentation
remain acceptance gates.

Implementation validates sealed 32?128 byte Praying Stone use frames, accepting
the existing 112-byte shape with a nonzero timestamp at offset 22. The action
check reads only the ushort at 20; those timestamp bytes are not an extension to
the action. Confidence is high for fixtures and the local dispatcher; genuine
6711 presentation remains pending.

## 2026-09-15: ordinary monster +1 equipment earning gap

Baseline `4d304a6`. A bounded audit finds no +1 assignment in the compiled
MonsterInformation.Drop routine; its only generic equipment-capable path is
OwnItemID. Read-only public catalog inspection finds 615 monster definitions,
zero equipment-valued ownitem entries and 591 zero ownitem entries. Inspector
SHA-256: `1F08BC4839DDD60A3CBE6C5B4A290F6475AA69FD35A36B25E07706E86C0BB062`.
Thus a pickup-only contribution hook would have no ordinary earning source.
The official mentor guide requires an actual monster-dropped +1 item, not a
purchase or trade. Restoring a bounded ordinary wilderness equipment drop path
is a necessary Revora gameplay prerequisite, not an inferred official drop rate.

Existing floor packet 1101 uses UID at 8, item ID at 12, X/Y at 16/18 and type at
22. Pickup authority must come from the current map floor object and private
server-origin evidence, never from supplied item details or the mutable MobDropped
flag. No new packet family is needed. Existing 117-byte floor spawn and existing
item/status updates remain unchanged; native pickup presentation remains pending.

The implemented equipment route validates sealed 32?128 byte 1101 pickup frames,
including the existing 117-byte shape. Supplied item type and coordinates are
ignored for authority; fixtures intentionally forge them while the saved map
object and collector position determine delivery. The 1% five-map common-gear
roll is an explicit configurable Revora policy. Native acceptance is pending.

## 2026-09-15: shared BP calculation and BP PK identity

See [BP PK research](BP_PK_RESEARCH.md) for official 2015 identity evidence and
calculation prerequisites. Existing server fields expose mentor and clan shares
through spawn metadata and 10017 updates. The clan code redundantly sends two
single-value updates with the same ClanBattlepower type, first a source UID and
then a byte-truncated share; the Entity.ClanSharedBp setter already sends the
full-width numeric share. Do not infer a new clan packet layout from those two
writes. Retain the existing scalar share update and separately track its source
server-side. Mentor status uses the inherited ExtraBattlePower update and mentor
information shape. Native acceptance remains pending; arithmetic fixtures are
not wire-verification evidence.

The old mentor setter also calls the three-argument Entity.Update overload, which
appends two entries of the same ExtraBattlePower type (amount, then mentor total).
It does not encode a verified two-value status record. Keep one scalar combined
extra-BP update plus the inherited MentorInformation share field. All login,
mentor-login and admission calls to BattlePowerFrom now enforce the same canonical
relationship and duplicate-row exclusions as combat. Managed tests establish
consistent values; genuine 6711 presentation is still unverified for this change.

## 2026-09-15: installed mentor/clan overlap evidence

The follow-up read-only client audit found StatusTips.ini section 44 explicitly
selects the higher mentor/clan share. Its reviewed SHA-256 is
`6DEC8FE19FEF9EC06AA7D32BF0F16D861439421C6B1822342348C05884A5CD35`.
Correct the initial additive total from `7e93cca` before tournament division
assignment or BP observation. This establishes a gameplay rule from installed
client text; it does not verify status packet layout or rendering. The tooltip
also displays two mentor-related numbers, but text placeholders alone cannot
establish the wire field carrying mentor total. Preserve that native acceptance
question rather than inferring a new packet member from UI text.

## 2026-09-15: BP PK admission and reward integration

The [BP PK contract](BP_PK_IMPLEMENTATION.md) selects an explicitly documented
monthly Revora season using the confirmed original event identity and rewards.
Server NPC 91050 uses installed manager presentation 37270 near 1002 (295,141).
The raw server map uses zero for a walkable cell; its inverse-named Floor setter
must not be interpreted as one meaning walkable. Native map access remains an
acceptance gate. The code reuses NPC dialogue and existing local-PK combat leases;
no client-reported BP, participation, winner or token origin is authoritative.

Achievement 11114 is written only by the committed contested BP field producer.
CPs, full-width Chi (existing 2534 Points at offset 10), bound token items and
receipts commit together. Exchanges consume exact saved item UIDs backed by
champion-origin rows and update the existing Chi message. The installed original
halo identity is unresolved; a saved seven-day result is not projected as an
unrelated class or weekly effect. Native halo and reward presentation are pending.


## 2026-09-15: native hair/face storage and dye edition mismatch

Approved DX9 SHA-256 `3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60`.
Read-only Ghidra inspection of the existing Account6711 project identifies
`CMsgHairFaceStorage` receiver `008cb7d3`, sender `008ca4c3`, and protobuf
serializer `008ca719`. These are native static facts, not a captured session or
proof of rendering. Numeric schema and remaining gates are detailed in
[the cosmetics restoration checkpoint](COSMETICS_RESTORATION.md).

Packet family 3510 (`0xdb6`) has a four-byte length/type header followed by
protobuf and the transport suffix. Top-level field 1 is the action scalar;
fields 2, 3 and 4 are separate optional singular submessages; field 5 is repeated
owned-entry information. The current shared five-scalar DTO does not describe all
of those different messages correctly. In particular the entry's field 3 is a
boolean, field 4 a scalar, and field 5 repeated scalar values. The later
consumer trace places the colour mask in the second field-5 value, not field 4.

The colour dialog at `004f2b35` constructs action 7 to acquire a colour then
action 6 to select it, using field 3 containing style and colour scalars. An
already-owned colour sends only action 6. The ownership predicate `008cb59b`
checks bit `1 << colour` in the stored style's colour mask. Receiver action 6
updates that style's selected colour through `008cb149`; action 5 updates
ownership through `008cb0f7`. Action 7 is not a colour-selection acknowledgement.

The installed old dye items 1060030-1060090 retain their historic names but their
current descriptions specify 200 silver redemption. The installed colour table
contains 270 style/colour rows at a numeric cost of 1,000, with colour IDs 1-6.
Do not implement the older seven-consumable dye route as if it were the native
6711 cosmetic service. No cosmetic achievement is enabled at this checkpoint.


### 3510 response and appearance follow-up (2026-09-15)

Same approved DX9 hash and read-only project as above; original implementation
baseline `e4128a6`. Consumer `008cb5f2` returns the map value after its key.
`008cb798` stores active state, selected colour from field-5 element zero, owned
colour mask from field-5 element one, then the independent field-4 scalar.
The earlier field-4 mask label is corrected. Emit both field-5 values, unpacked;
permanent entries use field 4 zero. Timed expiry units are not established.

Native preview `00699803` and actor setter `00765fbd` establish colour times 1000
plus style. The 10017 attribute-26 branch at `00942aeb` uses the low four decimal
digits for that actor setter. `004f2b35` directly selects default colour zero
with action 6; action 7 purchases only installed colours 1-6. These facts support
the strict parser correction and independent response encoder fixtures.

Hair rules `008c9d90` accept all five scalars; `008cb2e1` compares rule type 5
against the fifth scalar. Avatar receiver action 8 passes only the first four
values to `008cbe82`. Complete avatar eligibility mapping is still pending.
The new response encoder and transaction service are disconnected from runtime
handling until that mapping, alternate writers and handler integration are ready.
Confidence: high for these traced numeric shapes, pending for native acceptance
and full product behavior. No inferred layout is labelled a captured exchange.


### Avatar VIP and profession follow-up (baseline b959964)

Read-only native function `008cd7c3` evaluates rule 5 by calling `00779b41`,
which reads local player field `+0x5b4`. Setter `00779b48` writes that field and
is called from the 10017 receiver's case `0x27` (39), at `00940a84`. This resolves
the missing fifth action-8 rule scalar: avatar VIP eligibility reads the local
player VIP value. Revora already identifies update field 39 as VIPLevel.

Avatar profession predicate `008cc43e` uses `0077a0a7`, whose numeric calculation
is `(profession % 1000) / 10`. Type comparison in `008cd7c3` permits catalog type
zero or equality with player field `+0x1ec`; full body normalization for that
field has not been traced. No claim of a native session or completed handler
integration follows from these static facts. Private logs remain outside Git.


### Runtime integration plan (baseline c120166)

The replacement 3510 handler will use the strict request parser and bounded
response encoder already checked against native numeric shapes. Acquisition
acknowledges action 1; selection action 3; colour acquisition action 5; colour
selection action 6. Query/open responses project only durable ownership. No
client-provided ownership, restriction values or payment amount is accepted.
Normal VIP update 39 supplies the avatar window's local VIP value. Native window
and world acceptance remain pending; this is not a newly inferred packet layout.


Runtime integration of that plan now connects the strict parser, transaction
service and encoder at the existing 3510 dispatch. The code-level ordinary-player
fixtures cover committed action sequences, malformed input, replay, failure
fencing and a fresh game-state loader session. The public login wrapper retains
external website grant delivery after successful state loading; isolated tests
exercise the state-loading body without touching the identity schema. Native
window/rendering acceptance is still pending. See COSMETICS_RESTORATION.md for
current scope, validation counts and remaining gaps.


## Exploration region evidence (2026-09-15)

The approved 6711 DX9 static analysis confirms region types 27-29 select
uncompleted achievement IDs by half-open rectangles. The map-coordinate entry
callback dispatches the region's stored ID through the achievement manager.
See [the travel checkpoint](TRAVEL_ACHIEVEMENT_RESEARCH.md) for artifact hashes,
addresses, active/obsolete ID filtering and remaining runtime work. The final
outbound request builder is not newly verified here. Existing rejection of
client-authored 1136 action-2 completions remains mandatory; server acceptance
must be derived from validated world movement rather than those claims.

The server now records visits from accepted movement/teleport/world-entry hooks.
This adds no inferred wire format and keeps action-2 completion rejection intact.
Only canonical static map identity and valid server terrain can supply visit
candidates. Six installed destination gaps remain explicit in the travel
checkpoint; numeric producer coverage is not proof of admission or native display.


### Logical travel identity and approved static aliases (2026-09-15)

Further static analysis of the same approved DX9 SHA-256 establishes that native
1110 parsing at `009739f1` puts protobuf field 1 (tag 8) at object offset `0x28`,
field 2 (tag 16) at `0x2c`, and field 3 (tag 24, 64-bit) at `0x30`. The serializer
at `009735f6` confirms those field numbers. The handler at `00973b35` passes
field 1 to `009a49b7`, which stores logical map identity at map offset `0xec`.
`0099ff49` selects region records whose map field equals that logical identity.
Field 2 takes the separate `0099d5f0` path. Confidence: statically confirmed for
the approved executable; no live rendering claim. Native field 4 is not consumed
by this parser; existing weather behavior is outside this travel change.

Thus a reviewed fixed world alias may earn its logical map's regions without
requiring logical ID to equal terrain ID. Revora's installed pairs 1217/1075 and
1219/1212 can be accepted explicitly, preserving their map identities, existing
terrain and NPC/portal destinations. Arbitrary instances sharing either floor
remain excluded. Private native logs: `travel-region-logical-map.log`,
`travel-region-map-set.log`, `travel-region-status-identity.log`, and
`travel-mapinfo-wire.log` under `obj/chi-refill-validation`.


M6.45b adds a server-side named-area equivalence for Frozen Grotto 1: the
canonical world 1762/1762 uses approved `icecrypt-lev1` terrain, while the
installed achievement region is numbered 1926. Accepted visits there satisfy
10801 and retain server map 1762 in their durable receipt. This equivalence is
an inference from the approved map index, map labels and region catalog. It
does not rewrite native 1110 identity, change any packet layout, or accept
arbitrary instances sharing the floor. See EXPLORATION_DESTINATIONS.md for the
terrain hash gate and ordinary-player Envoy/portal checks.

## Grotto Key pickup integration (2026-09-16)

The Missing Key earning path reuses the existing 1101 floor-item spawn/pickup
contract and its managed-loot framing guard. No packet fields or client map
identities are added. Approved-client display remains unverified for this new
item source; server dispatcher fixtures establish only accepted/rejected frames,
canonical floor ownership and durable inventory behavior. Item 721347 comes from
the installed 6711 catalog; location evidence is the official Missing Key guide
linked in `GROTTO_PASSAGE.md`. The drop percentage is Revora policy, not a
verified native or historical constant.

## Dragon Pill recipe dispatch (2026-09-16)

The Guardian crafting prerequisite reuses existing NPC dialog/option packets;
new server options 17 and 60 select fragments and confirm an owned recipe.
Cancellation with NPC ID zero must reach the offer owner before the legacy
global close-dialog return. Fixtures verify both that cancellation and ordinary
replies. No packet layout, native model field or client family is newly enabled.
The item 720598 switch previously shared Sword Master's executable branch
because its intended code was commented out. Its new explicit break preserves
the item until Terato's encounter lifecycle is restored. See
DRAGON_SLAYER_RESEARCH.md for exact installed identities and remaining gates.

## Dragon Slayer actor presentation (2026-09-16)

M6.47 reuses existing NPC, item-use and monster spawn/death packets; no new packet
layout is inferred. Static evidence distinguishes Monster.dat TypeID from
BodyType and the actor's base body. The original minion/ghoul body mapping remains
unverified, so the server explicitly reuses reviewed floor-six bodies 154/163
as a Revora presentation choice. See DRAGON_SLAYER_HUNTS.md for binary hashes,
parser/consumer addresses and confidence limits. Server fixtures verify dispatch,
ownership and persistence; native rendering remains unverified.

## Guild season battlefield integration (M7.1b)

Server source baseline `0c443ec` uses SobNpc packet family 1109 for the existing
map-1038 pole 810 and gates 516074/516075. The new coordinator reuses that serializer,
the existing gate mesh transitions and existing AutoInvite resource 10515. It
changes combat ownership and durable results, not their packet layouts. The
ordinary invite response still uses the existing map entrance route.

Evidence: the focused fixture loads the installed structure identities/health
and executes the real SobNpc ReceiveAttack and CanAttack dispatchers. It tests
gate opening and capture HP restoration in server state. This is server-path
evidence, not fresh native rendering or invitation-response verification. The
full genuine 6711 match remains pending. See [battle validation](GUILD_SEASON_BATTLES.md).

## Guild leadership and seasonal claim integration (M7.2a)

Baseline `8e0d5b1` defines guild command family 1107, promotion actions 28/29/34,
requested rank at offset 8, string count at 24, byte-length at 25 and member name
at 26. This is existing server reader/writer evidence, not a newly captured
native request. Leadership promotion validates bounded framing, the shared
client seal and the name extent before using that established dispatch path.
Existing constructor padding may remain zero-filled before the seal.

Seasonal claims reuse NPC 10003's existing 2031/2032 dialogue path and add options
80/81. M7.2b adds prize-selection options 82/83/84 and single-use confirmation
85 through the same established dialogue dispatcher; item/CP updates reuse existing
serializers. No new packet layout is introduced. The installed database identifies GuildDirector at map 1002 (317,321),
mesh 150; installed `npc.ini` NpcType15 agrees. Inventory delivery reuses the
existing item packet and duration fields. Real client promotion and claim display
remain acceptance tasks; no new wire family is introduced.

## Terato Dragon profile and summon routes (2026-09-16)

The disposable x86 GameData fixture now also queries exact name `TeratoDragon`.
Both reviewed monster.dat variants resolve it with Boss=1 and MaxLife=50,000,000
through the real native loader. The existing approved dependency hashes and
isolated fixture path remain enforced. This is native profile-resolution evidence,
not rendered combat acceptance. The server reuses its existing boss spawn and
actual-HP updates plus floor-item 1101 framing; no new field layout is inferred.

Installed item 720598 points to F6 (532,394) and Royal Guardian at Wind Plain
(517,742). The legacy server NPC 180810 is instead RealmGuard on map 5000
(90,743), mesh 8630. Restoring the client-described ordinary route requires a
reviewed runtime relocation to canonical map 1002, preserving database rows.
DragonKing template 4152 remains unchanged. Reusing its known dragon body 950
for the separate Terato encounter is an explicit Revora presentation choice;
the profile TypeID alone does not prove that original mesh mapping.

The installed npc.ini separately identifies NpcType2993 as TeratoDragon and
references motion family 950000, supporting the chosen existing dragon artwork.
This metadata is supporting evidence, not proof of a live monster render.
Installed magictype.dat SHA-256
`618431607E8DB81F5E8C6093D801329B73BD9A5AF4AA57FC8A38348A832173C2`
identifies TailSweep 7011, FlameShower 7013, DragonBreath 7014, FearRoar 7015 and
MistyFlame 7017. Terato's owned combat path uses those existing spell identities
and the existing spell-use packet. Timing, geometric ranges and damage tuning
are explicit Revora rules; historical server values are not claimed.

Installed Achievement.dat SHA-256
`DF78DD498353B0B089F9B4228CADA5D79F4547AAE7B705349816671B2C29EBFC`
defines 10932 by receiving a Dragon Soul from Dragon Slayer. Its new trusted
producer belongs to the durable quest-soul pickup transaction, rather than the
kill or Study Point payment. Existing 1136 projection carries that completion.

## Seasonal Gold Prize rollover (M7.2c)

Rollover preserves the owned item UID and existing bottle slot or warehouse.
Inventory/equipment projection reuses the existing item-update serializer;
equipped statistics reuse LoadItemStats, WindowStats and prestige projection.
Stored items use their ordinary next warehouse listing. Existing warehouse family
1102 dispatch now shares the inventory owner lock with rollover; its frame layout
is unchanged. No new wire layout is
inferred. Real-client equipment and warehouse presentation remains unverified.

## Holy Grail crafting integration (M7.3)

GuildDirector 10003 options 86/87 select and confirm the earned twelve-trophy
recipe through the existing NPC dialogue dispatcher. Item delivery and both
wardrobe updates reuse established serializers and the current title-storage
reconciliation path. The installed Storage.ini keys are 20342034 (Invincible)
and 60116011 (Supreme); their requirements name twelve-cup Grail crafting.
No new packet family or inferred layout is introduced. Native crafting, title
and wing presentation remains an acceptance task.

## Recoverable regular Guild War purses (M7.2e)

Purse collection adds GuildDirector option 88 through the existing dialogue
dispatcher. Currency and timed-item delivery reuse current CP/RVP/silver and
inventory updates. Existing guild leader/deputy flags retain their current
identities and legacy stored status keys 9/10; their duration is measured from
the battle result. No packet layout is inferred or changed. Native automatic
delivery, purse dialogue and honor display remain acceptance tasks.

## Class-specific full-level rune receipts (M6.46b)

Installed Achievement.dat identities and category headers are recorded in
[FULL_LEVEL_RUNE_ACHIEVEMENTS](FULL_LEVEL_RUNE_ACHIEVEMENTS.md). Ten class-specific
receipts use the existing 1136 achievement update and 1009 actions 57/63 for
equip/composition. No frame layout or new native request is inferred. The two
loadouts remain separate, using the already-reviewed rune slot and level rules.
Native display remains unverified. Server transaction and regression tests pass
as recorded in the linked implementation checkpoint.

## Rune admission and active effects (M6.50b)

Rune admission reuses existing 1009 equip/unequip/composition requests. Installed
RuneSys and catalog evidence are in RUNE_ADMISSION_RESEARCH.md. No new packet
layout is inferred. Recovery retains old item positions until an ordinary
unequip; invalid equipment is excluded from runtime effects and earning.
Native primary/alternate presentation remains an acceptance gate.

Server fixtures verify the existing equipment switch updates the selected rune
effects. This is not native evidence that every rune-panel control emits the
same switch request; that UI correspondence remains unverified. No packet or
stored position migration was introduced for old 106-108 equipment.

## First-rebirth materials (M6.52)

The Clean Water restoration reuses the reviewed floor-item family 1101: existing
UID lookup, 32-128 byte frame bounds, declared length, family and client suffix
checks. It adds an owned Clean Water item source and routes both ordinary and
auto-hunt pickup through the same transaction; it introduces no new layout.
Sereth's existing NPC dialogue uses options 30/31/38 and a new server-selected
confirmation 39 through the existing NPC request/reply encoding. Native 6711
pickup/tooltips/dialogue remain acceptance gates. Numeric catalog and source
claims are recorded in FIRST_REBIRTH_MATERIALS.md; none is represented as a
new captured native packet.

## Mining action and reward ownership (M6.53)

The existing 6711 action service uses protobuf family 10010 and action 99
(`Data.SwingPickaxe`). `Data.CMsgAction`, `Ext.ToConquerPacket` and
`Ext.ConvertToProto` establish the current server's four-byte header, protobuf
body and eight-byte transport suffix; no new client layout is inferred.
The mining request remains an intent to start. It cannot supply a reward, roll,
position or yield timestamp. Current server-owned equipment/location and a
minimum two-second cadence control rewards. The existing 1009 shop request
also covers catalogued Pickaxe purchases using the reviewed buy frame.
Native client action/tool acquisition acceptance remains unverified. Source
and transaction fixtures are recorded separately in MINING_GEM_EARNING.md.

The family-10010 dispatcher now enforces its existing declared frame length,
family and client suffix before protobuf parsing; malformed payload exceptions
are contained before action dispatch. The mining suite also exercises the
ordinary action-85 portal boundary against installed portal definitions.
Conductress option 5 uses the existing NPC encoding and now reaches the mine
through a resumable paid server-side passage. None of these fixtures is a
new native capture or proof of client animation/display acceptance.

## Adventure Islands access (M6.54)

The restored journey reuses existing NPC request/reply encodings and action-85
portal requests in protobuf family 10010. Guild transporter 9884, Old Explorer
300655, Grandpa 300653 and Boatman 300654 retain their existing option 1.
The installed portal definitions and terrain show a blocked entry tile on 1212;
server-selected nearby valid landings preserve logical map identity. No packet
layout or new client action is introduced. Native client travel/display acceptance
remains pending; source and transaction fixtures are recorded in
WATERLORD_ACCESS.md separately from captured-client evidence.


## M6.52d first-rebirth transaction review

The existing 2031 NPC dialog dispatch, 1008 item, 10010 skill removal, spell and proficiency update families are reused without a layout change. Sereth first-rebirth options 200-214 replace arithmetic class options that collided with second rebirth. [Rule evidence and acceptance](FIRST_REBIRTH_COMPLETION.md). Official gameplay pages are not wire evidence; genuine 6711 UI acceptance remains open.


## M6.55a first-life level recovery

The existing shared experience transaction and level/stat update packets carry first-life recovery. No new packet layout is asserted. Original level/EXP comes only from the committed first-rebirth receipt; its consumed timestamp commits with progression and source payments. The published level-130 epiphany and local evidence are tracked in FIRST_REBIRTH_RECOVERY.md. Native acceptance remains pending.

### M6.56a EXP consumables (2026-09-16)
The existing item-use dispatch and item removal/stack, level, EXP and attribute
updates are reused. No new packet layout is inferred. At baseline `8091bfd`,
`ItemHandler` recognizes standard IDs 723700/723911/722136 (ten daily, below
137) and power IDs 723744/722057 (ten percent of the current level curve,
below configured cap). Those are local server rules, not a claim of exact
official 6711 numerical parity. The transaction work preserves these rates
and binding-based mentor rules while coupling item debit, daily allowance,
progression, mentor effects and recorded recovery. Native acceptance is open.

### M6.56b1 owned Elite PK prize bundles (2026-09-16)
Baseline `0919408` has sixteen `ItemHandler` prize identities, 720714?720729.
The twelve lower IDs grant EXP plus items; the four upper IDs grant CPs plus
items. They remove their pack before completing their rewards. These are active
owned-item handlers, but current `ElitePkResults.Finish` grants only saved
honors, not packs. Atomic consumption must not be described as a completed
event earning route. Reuse existing item, CP, EXP and level update packets;
no new wire layout is proposed. Native acceptance remains pending.

### Magnolia transaction continuation (2026-09-16)
At `3cf820a`, NPC 44100 issues configured seeds on choices 3/4; the garden
return is already guarded by ExplorationWorld. Item IDs 729307/729310 plant
near (98,111) or (106,98) on physical map 1806. Existing item, EXP, CP/RVP,
study-point and string-effect packet builders suffice; no new layout is
inferred. The 2012 official Magnolia guide confirms level 80, daily completion,
and garden planting, but its five-grade upgrade/reward system differs from
Revora's two-grade custom economy. Transaction work must retain that distinction.

### Daily Envoy hunting-material earning (2026-09-16)
Baseline `abb36bc` uses name-based `TryDropDailyPriceItem` calls and generic
floor pickup for four materials. The replacement reuses existing 1101
sealed floor-item frames, direct auto-hunt dispatch and 1008 item updates.
No packet layout changes or new client claims are made. Installed monster
family IDs, names, physical hunting maps and item catalog entries are the
server source evidence; actual catalog/spawn matches are checked in the
disposable fixture. The official [Everything Has A Price guide](https://co.99.com/guide/quests/2012/Everything_Has_A_Price.shtml)
confirms that materials may be hunted before acceptance and are tradable.
The 40-percent drop rate remains Revora configuration, not an official rate.
Genuine 6711 loot/rendering acceptance and complete reward transactions remain open.

### Daily Price Hunt transaction and list travel (2026-09-16)
At baseline `3db3c01`, Daily Envoy options 2/11-15 accept, turn in, reroll,
skip, reissue and describe the hunt. Owned list 729087 opens an existing NPC
dialogue whose choice 1 reaches the corresponding installed hunting map.
The replacement keeps those packet families and options; it binds the
continuation to the exact owned item and current daily assignment. Existing
1008 item, CP/RVP, study, EXP/stat and quest-list packet builders carry
committed state. No new layout is claimed; genuine 6711 acceptance remains
open. Server rules and the custom economy are recorded in
DAILY_PRICE_TRANSACTIONS.md; official guide evidence is gameplay evidence.

For the Daily Envoy/list dialogue only, 2032 frames must contain the existing
20-byte minimum body, no more than 120 body bytes, an exact transport suffix
and an input count within the body. The existing sealed 28-byte empty-input
request passes. Four malformed variants are tested before a valid continuation.
This is a bounded check of the existing local request layout, not new native
client capture evidence.

### Rare Materials durable commission (2026-09-16)
Baseline `e33f438` implements Daily Envoy choices 19, 28-30 and 32-34 for
normal-gem commissions, RVP changes/star purchases and CP completion. It saves
no `MaterialsStar` value. Existing item, RVP/CP, study and progression packet
builders are reused; there is no new mission ID or inferred packet layout.
The scoped Daily Envoy dialogue frame bounds already apply. The installed
normal-gem catalog and existing Plain/Forest Mine sources supply all four
requirements. [Official Rare Materials](https://co.99.com/guide/quests/rare_materials.shtml)
confirms the daily level-75 service and three reward stars, but its prices and
rewards differ from Revora's custom economy. Genuine 6711 acceptance remains
open; gameplay documentation is not wire evidence.

### Exorcism reward services and unconfirmed entry (2026-09-16)
Baseline `4114157` dispatches packet 2400 to a RAM-only Thrilling Spook entry
callback without a demonstrated native payload layout. Official gameplay
guides and the installed Tiger Cave map/NPC metadata do not verify this wire
family. Disable 2400 globally pending native evidence and the owned encounter
replacement. No owner-only exception or replacement layout is introduced.

Exorcism exchange options 6/8/9 on installed Heavenly Master 12333 reuse the
existing 2032 dialogue. Apply the existing 20–120-byte body, exact seal suffix
and bounded input-length checks before decoding this NPC's continuations.
Route them to the owned service before legacy NPC-ID rewriting/dismissal so
a wrong NPC identity cannot be hidden and choice 255 cancels its offer.
Existing item and BoundConquerPoints update builders carry committed rewards;
no new packet layout is asserted. Synthetic framed tests establish server
behavior only. Genuine 6711 acceptance remains open. Evidence and gameplay
scope: [Exorcism research](EXORCISM_RESEARCH_AND_REWARDS.md).

### Owned Exorcism hunt continuation (2026-09-16)
The replacement uses existing bounded NPC dialogue: Heavenly Master 12333
accepts difficulty, offers entry and pays rewards; an instance-owned Earth
Master 900000132 summons the registered boss or returns participants to the
Market. Use the installed mesh 7610 for Earth Master and family 4212/mesh 952
for Spook presentation. These are local presentation facts, not newly verified
native entry or boss-skill protocols. Packet 2400 remains disabled. Dynamic
map travel, monster spawn/boss HUD and committed inventory/progression updates
reuse existing builders. Map 1013 is explicitly the reviewed Tiger Cave terrain
used for Revora's private hunt, not a verified original Black Burrow map.

### Anniversary isolation continuation (2026-09-16)
Legacy NPC 900028 has no row in the installed public NPC catalog. Its old code
is a custom Revora service, not proof of an original 6711 event. The installed server catalog
identifies 3001172 as VirtuePack(A), although the legacy server treats it as an
Anniversary bead. Keep that presentation mismatch and absent admission NPC
explicit; do not publish a client patch or invent an original campaign calendar.
The transaction replacement retains scoped NPC 2032 and item-use handling,
with existing inventory, silver, RVP, blessing and Study Point updates. No new
wire layout is claimed. Existing owned beads need safe redemption independently
of whether fresh admission is deployed.

### Owned Spook named attacks (2026-09-16)
Official gameplay identifies Soaring Leap (ranged/stun), Bloody Battle (ranged)
and Surging Shake (heavy damage). Installed server magictype and approved-client
MagicEffect metadata agree on names at 11321, 11310 and 11309. Older 1036x
client labels differ; do not assert grade-specific historical mapping. Reuse
existing SpellUse and timed character-update builders. Spell identities/effect
metadata are local catalog evidence, not native capture or rendering acceptance.
Server warning timing, geometry, stun enforcement and damage scaling are explicit
Revora choices. The old name-based fixed-damage branch is still reached by the
previous owned encounter; replace it instead of assuming normal defense math.

### Spirit hunting world coverage (2026-09-16)
The existing region and travel evidence establishes public Adventure worlds,
fixed logical map 1217 / terrain 1075, and Grotto worlds 1762, 1927, 1999,
2054, 2055 and 2056. Read-only installed monster spawns confirm ordinary
hunting populations. Expand the server spirit eligibility list using these
identities; do not treat arbitrary terrain copies or Potency map 1075 as
Adventure Zone. No packet layout or native rendering claim changes.
The official Spirit Bead guide supplies generic wilderness level rewards, but
does not enumerate every map ID. This map policy is an evidence-backed Revora
integration choice, not a captured original-client quest predicate.

### Nested Elite prize item contracts (2026-09-16)
Owned items 720049, 720731 and 729626 use the existing item-use dispatch and
inventory updates. The legacy recipe and official prize guide identify a +6 random-color steed;
the loaded server/client descriptions identify a weapon accessory and a
class-based Super weapon refinery. The client steed label has encoding damage.
The old refinery name filter also includes Superior packs and non-weapon
materials, contradicting the last description. Reviewed refinery metadata
identifies level-5 weapon positions 4, 5, 500 and 421. Accessory catalog lifetimes
include permanent, 5-, 7- and 30-day entries; persist the selected item's actual
lifetime. No new wire layout or native client acceptance is claimed.

### Elite placement prerequisites (2026-09-16)
The official prize guide distinguishes champion, runner-up, third and fourth
through eighth rewards, with claims before the following weekly start. The
current NPC-based tournament records only elimination bouts. Add a server-owned
bronze bout and validate the saved advancement graph before making placement
entitlements. Native bracket/wager protocol families remain disabled; this
change uses existing admitted combat, travel and NPC builders only. No new
client packet layout or historical bronze timing is asserted.

## Elite PK Envoy prize claim continuation (2026-09-16)

Before enabling the new prize payout, the existing 2032 NPC continuation is bounded for Envoy 448582: body length 20..120, exact eight-byte trailer and bounded input length at offset 17. UID at offset 8 and option at 14 follow the existing Revora NpcRequest layout (server source evidence, not a newly verified client capture). Options 6/7 review and claim saved placement rewards. The ordinary session, physical NPC proximity and optional echoed UID are checked again at the service boundary; tournament/native bracket packets remain unchanged and unverified. The 575-check claim suite validates bounded packet dispatch, atomic failure recovery and four complete ordinary earned brackets; see [Elite prize claims](ELITE_PRIZE_CLAIMS.md). Genuine 6711 visual acceptance remains pending.

## Magnolia grade continuation (2026-09-16)

The existing Garden Keeper 44100 NPC continuation uses bounded 2032 frames (20..120 byte body, exact eight-byte trailer, input length within body). Options 5/6/7 review a carried seed, change it for CPs or replace it with Super. No native packet layout is added; confidence is source/fixture-level until genuine 6711 acceptance. The 2016 official Study Point removal supersedes the old 2012 guide for patch 6711 (2018-05-03). Grade amounts and CP prices come from the official Magnolia guide; the official Price Hunt guide equates 60 minutes with one EXP Ball. Exact lottery odds and extra gift outcomes require separate evidence or an explicitly documented Revora policy.

The 552-check Magnolia suite validates these continuations, all five ordinary grade changes and complete planting/reload transactions. See [Magnolia grade evidence](MAGNOLIA_GRADES.md). Revora lottery odds are explicitly disclosed, not claimed as published 6711 probabilities.

## Magnolia gift-use prerequisite (2026-09-16)

Meteor Scroll 720027 and Class2MoneyBag 723714 use the existing 1009 item-use action 4, owned item UID at offset 8. The atomic handlers validate the existing 32..128 byte frame, declared body plus eight-byte trailer, packet ID, action and seal. This is server-source evidence, not a new captured 6711 layout. The loaded catalog defines ten Meteors and 800,000 silver respectively. The 490-check gift suite validates ordinary use/reload, malformed frames and transactional failure recovery ([scope](MAGNOLIA_GIFT_USE.md)). Magnolia bonus issuance remains separately tracked.

## Ordinary EXP Potion transaction (2026-09-16)

The existing 1009 action-4 owned-item path is reused for catalog-backed 722700 (600 seconds) and 723017 (3,600 seconds). Validate total frame 32..128, exact body/trailer, action/ID/seal before effects. The existing FlagsUpdate DoubleExpTimer=18 projection remains at normal multiplier 200; source/fixture evidence only, not a new 6711 capture. Countdown, autosave and immediate assignment must share owner locking plus a durable timer preimage before enabling atomic item consumption. Catalog 720793/720981 are tournament EXP packs and 728201 advertises 5x kills; these are not ordinary-potion aliases and remain separate work.

## Magnolia seven-day blessing identity (2026-09-16)

Read-only active 6711 and loaded server catalogs give 720578 and 1200001 the same PrayingStone(M) seven-day use description. Extend the existing bounded 1009 action-4 Praying Stones service to this exact legacy identity; no new packet family or native capture is asserted. The existing binding, blessing and mentor-credit transaction applies to both IDs. Receipt schema migration must preserve existing records and enforce the legacy identity's seven-day duration; legacy zero-stack singleton sources require an exact checked debit.

## Desert Guardian continuation hardening (2026-09-16)

Guardian 301 (legacy alias 15140), remains 65822..65840 and the existing Daily Envoy options reuse the existing 2032 continuation. Before dispatch, bound body length to 20..120, exact eight-byte trailer and input length within the body. The service accepts zero or the active NPC as echoed UID, and checks the canonical ordinary session and physical NPC proximity. Route farewell to the service to cancel paid offers. These are server-source/fixture claims, not a newly captured or visually verified 6711 layout.

## Chi Token cash redemption (2026-09-16)

Token 729304 reuses existing 1009 action 4 with selected owned UID at offset 8. The new handler must bound total length 32..128, exact body plus eight-byte trailer, action/ID/seal before consumption. No new layout or client capture is asserted. The previous handler required seven units in one stack and wrote currencies separately. Preserve the Revora-configured seven-token cash exchange (default 1,500 CP and 5 RVP); select the clicked token first, then other usable carried tokens by UID, saving all debits and cash in one transaction. This is Revora economy behavior, not an original CO Chi reward parity claim.

## Second-rebirth rite continuation (2026-09-16)

Before replacing Sereth's second-life path, use the existing 2032 body (20..120 bytes, exact eight-byte trailer and bounded input), echoed active NPC 59558 or zero, and canonical nearby ownership. Options 2/9 start class selection, 170..179 review a destination, 180 confirms; farewell cancels the session offer. No new native packet family or capture is claimed. Complete token/profile/gear/skill/proficiency/receipt/achievement changes together and send existing projections after commit. Quest bypass and second-life evidence are distinct.

Second-rebirth token purchase reuses the existing 1009 actions 1/55: shop at offset 8, item type at 12, action at 20, and existing quantity/currency fields from `ItemUsage`. Require complete body+8 seal, 38..128 bytes. Loaded EShop stock 2888 is the existing remote mall; other stock IDs require their canonical nearby NPC. Currency selector 0/1 spends CP, 2 spends bound CP and creates a bound token. No new native packet capture is claimed. Catalog-loaded price/stock and owned balance, bag, receipt, and duplicate-click checks remain authoritative.

## Abyss quest journal overlay (2026-09-16)

Reuse existing 1134 List/status and 1135 objective projections for installed missions 2414..2419. The genuine 6711 Questinfo.ini identifies 2416 objective 1 as grievance (40,000), and 2418 objective 1 as completed rounds (9), objective 2 as the current ordered round (8). The first trial uses client item checks. Server source supplies the existing packet offsets; no new native capture or visual confirmation is claimed. Project the selected durable Abyss route without altering serialized Slayer Cup entries with the same IDs. Client Begin and 2032 quit requests may refresh this overlay but cannot author, erase or complete its state. Bound quit parsing to an existing 20..120-byte body and exact eight-byte trailer before reading its quest identity. The overlay remains globally inactive until full quest initialization.

## Abyss keeper conversations (2026-09-16)

Reuse the existing protobuf 2031 NPC-open request and bounded 2032 continuation, not a new packet family. The latter requires a 20..120-byte body, exact eight-byte trailer and bounded input before dispatch; echoed NPC must be zero or the actual nearby active keeper. Sereth uses 181 (review), 182 (enter), 183 (Cup route), 184 (saved spoils). Abyss keepers use ordinary per-NPC options and 20 for an expiring, exact-material confirmation; farewell clears that offer. Actual ownership, current saved stage, canonical map and distance are checked server-side. Existing serialized Cup quests remain separate. The source/fixture evidence does not assert a new genuine-client capture or visual acceptance.


## Abyss-funded second rebirth (2026-09-16)

Reuses the already bounded Sereth 2032 class-selection and confirmation options (2/9, 170..179, 180). No packet layout changes. The server chooses verified, unconsumed Abyss final-combat proof before offering a token debit, explicitly identifies the payment in dialogue, and consumes that exact proof in the character transition transaction. No new genuine-client capture or native visual acceptance is claimed.


## Abyss exact-name boss HUD profiles (2026-09-16)

The owned Abyss encounters now use fifteen distinct display names with the existing spawn and HP-update writers. No packet layout changes. Private monster.dat aliases encode spaces with `~`, as proved by the approved GameData loader in BOSS_HUD_6711.md. Hash-gated x86 native before/after lookup verifies every alias and intended Boss=1/MaxLife, while the original fifteen profiles retain Boss=0 and original health. Server fixtures check the matching names and full/half/one/zero HP projections. Native profile loading is verified; rendered gameplay is not. Files remain privately staged, without active-client installation or publication.

## Paid reincarnation ownership (2026-09-16)

Retain the existing source-derived 1066 framing (declared body plus eight-byte TQClient seal, minimum body 13), class byte 8 and body-choice byte 12. These offsets and body selectors are existing server evidence at cbdebb1, not a verified native capture. The existing Sereth 2032 options 3/5 open the native reincarnation window; 6/7 review/confirm the existing 2,000 CP dew purchase. Require an expiring nearby ordinary-player offer before accepting 1066. Do not introduce a packet layout or claim native preview/body rendering acceptance. Persist the selected profile, payment and recovery receipt together before projecting existing updates.

## Archer Master Assassin training (2026-09-16)

Reuse NPC 400's existing 2031 open and 2032 continuation options: 49 lists
Assassin arts, 12..18 select the seven installed spells. Add NPC 400 to the
existing 20..120-byte body, exact eight-byte trailer and input-length guard
before deserialization. Echoed NPC must be zero or 400; ownership, public Job
Center 1004, proximity, promoted Archer class and learning level are server
checks. Source-derived framing at 5df7f9c and ordinary dispatch fixtures are
implementation evidence, not a native capture. No layout changes. Correct
option 14 to logical spell 11620. See REBIRTH_SKILL_AUDIT.md for catalog hashes,
primary learning evidence and unresolved combat semantics.

## Wind Sage skill and scroll exchange (2026-09-16)

Reuse NPC 4720's source-derived 2031/2032 conversation and existing options
60, 61, 69, 80..83. Add its continuation to the existing bounded 20..120-byte
body plus eight-byte trailer/input guard before deserialization. Require echoed
NPC zero/4720 and the canonical nearby ordinary owner in public Job Center.
No packet layouts change and no new native capture is claimed. Scroll ownership,
conditional payment, learned skill and receipt must commit together before
existing inventory/skill projections. Evidence baseline is source 56c8fa7,
installed spell/item catalogs and the official Ninja skill/epic quest guides;
see NINJA_SKILL_TRAINING.md for exact identities and source conflicts.

## Ninja Epic owned NPC and item routes (2026-09-16)

Reuse the existing 2031/2032 framing, source-derived at 538d6dd. Trial NPCs
199444..199446, 199464..199466 and 199544..199546 use existing menus plus option
20 for an expiring owned confirmation. Wind Sage 4720 retains option 86 for
entry. Require a bounded 20..120-byte body, exact eight-byte trailer/input bounds,
canonical active NPC, map and proximity. Life's Eye uses the existing owned item
1009 use path; no new packet family or client layout is introduced. Item use
must validate the selected eye UID and active katana, commit the same conversion
as NPC confirmation, and suppress immediate replay across reconnect. Native
rendering remains a separate acceptance gate.

## Ninja flower hunting and pickup (2026-09-16)

Evidence baseline: `80525d0` source, installed map-1700 NPC/spawn metadata and
existing bounded 1101 ground-item fixtures. Wind Sage 4720 adds option 87 for
free hunting passage; Bruce 3604 adds option 90 for free Job Center return.
Both use the existing source-derived 2031/2032 exchange, active NPC/map/proximity
and the 20..120-byte continuation guard. Registered flower origins use the same
32..128-byte 1101 frame and seal validation as other restored material drops.
Manual and auto-hunt pickups share one owned transaction. No layout changes,
new packet family or native capture are claimed. Genuine 6711 presentation
still requires acceptance.

## Ninja rebirth and active clone ownership (2026-09-16)

Baseline `b8413cc` uses 2812 with target UID at offset 4, clone count byte at 8,
and clone UIDs from offset 9 in four-byte strides. This is source-derived,
not verified by a new native capture. The existing summon path creates two
clones. Bound the existing reads to one or two UIDs, a 9..64-byte body and the
exact existing eight-byte sealed trailer; do not invent new fields. Process
under the same character owner lock as the rites, require the learned Shadow
Clone skill and canonical owned map actors, and reject stale/replayed clone IDs
after a successful reset. Reuse existing RemoveEntity and CounterKillSwitch-off
projections after commit. First/second/paid request layouts remain unchanged.

## Head Abbot epic learning ownership (2026-09-16)

Baseline `65a1099`: reuse source-derived 2031/2032 NPC exchange; no new packet
layout or native capture. Head Abbot 4271 options 23/24/25 must dispatch before
legacy continuation rewrites the echoed NPC ID. Extend the existing 20..120-byte
body, exact eight-byte trailer and bounded input-length guard to this actor.
A nonzero echoed actor must match 4271; canonical owner, public Job Center,
proximity, class, level and the owned scroll remain server-authoritative.
Existing skill/item projections occur after the durable exchange. Native UI
acceptance remains open.

## Monk Epic Quest runtime plan (2026-09-16)

Baseline `e3fe890`: map index 3983 and installed NPC meshes are static catalog
evidence. Source-owned actors 199483..199487 reuse ordinary 2031/2032 dialogue,
20..120-byte continuation guards, exact trailer and actor echo checks. Head
Abbot 4271 adds options 86/87 for trials/hunting; Bruce 3604 retains option 90
for return. Item 3007564 uses the existing owned UseItem path. No new packet
fields are inferred. The native 3526 navigation mismatch is not silently
represented as working; ordinary NPC navigation is independent of that UI.

## Monk rite cleanup and delayed effect ownership (2026-09-16)

Baseline `81e5eec`: Infernal Echo creates existing 1101 floor effects using item
identity 1397; existing screen code also routes 1380/1390 through the same
pulse. No new layout or native capture. Reuse RemoveEffect projection when a
committed rite retires owned registered effects. Serialize creation/pulses with
the character lock used by rites, verify canonical session/map/effect ownership,
and reject stale objects or already consumed pulses. This is lifecycle and
ownership validation, not a claim of full 6711 effect timing/damage parity.

## Monk native journal projection (2026-09-16)

Baseline `f64a76a`: installed Questinfo.ini mission 3526 lists three intentions
in Fortune/Altar/Victory order and type 6. Reuse the existing source-derived
1134 status and 1135 indexed-objective projections; map those intentions to
indexes 1/2/3 as an interpretation requiring genuine-client acceptance, not a
new verified capture. Earned flags come only from committed successful trial
receipts, remain historical across retries/rebirth, and grant no extra reward.
An owned/traded Heart and legacy quest blob cannot complete this overlay.
Protect both 1134 Begin and bounded 2032 interaction-102 quit requests.
The installed NPC link 8314/1004/34/33 is stale; privately stage only its two
mission-3526 links to actual Head Abbot 4271/1004/80/45, preserving original
bytes and exact hashes. No active client installation or publication.

## Sutta introduction (2026-09-16)

Baseline `5f261d5`: item 3007561 and the official Monk quest guide confirm
the introductory book and 30-minute EXP reward. Reuse Head Abbot's bounded
2032 option 88 and the existing owned 1009 UseItem route (20..120 NPC body;
24..120 item body and exact eight-byte sealed trailer). No new layout or native
capture. Revora policy supplies one bound book and one reward per character,
read beside Head Abbot; it supplies no remote teleport or journal success.
The retail acquisition method and remote-use behavior are not verified.

## Warrior instructor ownership (2026-09-16)

Baseline `6c86f60`: reuse existing 2031/2032 dialogue at Warrior God 10001.
Extend the established 20..120-byte NPC continuation guard and nonzero actor
echo validation to that instructor before legacy routing. Existing option 6
becomes the accurately named installed Pounce; option 12 is a fist-art menu,
not a level-140 multi-grant. Individual options 20..24 teach the five installed
fist arts. Hidden option 10 must not teach a stance with no active cast path.
No new layout or genuine-client capture is claimed. Skill projections follow
one profile/skill/receipt transaction, including all newly eligible XP arts
when using existing option 5.

## Warrior life and self-effect ownership (2026-09-16)

Baseline `24b117f`: reuse existing 1105 skill and indexed status projections for
Maniac Dance/Backfire and the existing 113/128 shield detail reset. No new
packet fields or native capture. Both self-casts must derive state from the
canonical caster under its character lock, require the learned art and paired
equipped fists, and never write a supplied target's timers. The scheduled
Dance pulse shares that lock with all three rites and requires live learned
ownership, equipped fists and the active fifteen-second effect. After a
committed rite, clear only that character's modes; preserve failed-rite state
and other characters' buffs. Backfire's damage algorithm and the disabled
Defensive Stance remain separate unresolved combat work.

## Backfire counter ownership (2026-09-16)

Baseline `2a95268`. The installed skill 12680 levels 0..6 provide powers
55/60/65/70/75/80/90, ten-second duration and 15,000 ms cooldown. The official
Warrior skill/class guides describe one HP-based counter capped at 50,000 plus
30 stamina. Reuse existing `Attack.BackFire`, status removal and stamina update
projections; no new packet layout or native capture is established. The SRP
username fix in the preceding commit does not change this combat protocol.
The initial event combat leases and blocking checks precede the counter already.
The new one-use claim must not acquire a victim's inventory lock while handling
another player's attack; cast/rite cleanup and counter claims share a short
per-session mode lock, released before damage, event scoring or death callbacks.
An accepted counter is a separate damage effect and does not rewrite the landed
incoming strike. Native animation and exact retail hit-order acceptance remain
pending. Sources: https://co.99.com/guide/skills/m_warrior.shtml and
https://co.99.com/guide/classes/invinciblewarrior/index.shtml .

## Backfire world-actor and summon ownership (2026-09-16)

Baseline `56287e7`; no packet layout changes. Guard and adventurer strikes have
separate server damage paths and must invoke the same one-use counter before
applying the incoming hit. Fake player bodies are admitted only through their
exact live session and guard/adventurer registry identity, never a display-name
prefix. A guard death can move the defender to or from Darius's arena; an old-map
strike must stop after that transition. These are Revora encounter semantics,
not a claim about retail guard behavior.

Companion ownership has three existing representations: the single companion
and shadow-clone slots plus map registration, and the pet collection's own
registry. The latter does not register its bodies with the map. Map registration
can use either entity dictionary depending on UID. The counter must validate the
exact body against its owning canonical session and the corresponding registry.
Companion death through `Die(Entity)` removes only that body's owning slot.
Existing BackFire, status, stamina and summon-removal projections are reused;
genuine-6711 animation and retail hit ordering remain unverified.

## Magic Defender caster ownership (2026-09-16)

Baseline `ba51e71`. Reuse existing decoded skill 11200, skill-use projection and
indexed status/detail 128; no packet layout or native capture is established.
The installed server rows provide durations 11/12/13/14/15 seconds, range 4,
30 stamina and 20,000 ms cooldown. The official Arabic skill table agrees with
those durations, while the present English table doubles them. Preserve the
installed 6711 duration profile; do not reinterpret every duration column.
Both official guides describe a shield requirement, nearby team protection,
movement interruption and loss of immunity when the attacker is 20 BP above
the caster. The existing calculation instead compares the recipient and uses
conflicting three-BP checks. Source authority must be the exact live caster
session, learned skill and shield, with recipients only projecting its remaining
duration. Multiple overlapping sources must remain independent. Boundary choice:
an advantage of 20 or more defeats a source; 19 does not. Exact native timing
and presentation remain an acceptance gate.

Sources: https://co.99.com/guide/skills/m_warrior.shtml and
https://qahr.99.com/guide/skills/m_warrior.shtml .

## Defensive Stance restoration (2026-09-16)

Baseline `98a72cd`. Reuse the existing Warrior God 2031/2032 dialogue option 10,
decoded spell 11160, spell-use projection, indexed stance flag/detail 126 and
existing FlashStep position correction. No new packet layout or verified native
capture. The installed server's two rows contain power 30200/30300 and duration
120/240; its existing loader decodes the percentage component as 200%/300%.
The English Warrior guide agrees; the Arabic guide instead lists 400% at the
second level. Preserve the installed 200%/300% profile. Treat it as total physical
defense, not another subtraction of the entire defense from final damage.
Shield equipment, learned ownership, live session and expiry govern the bonus.
The documented movement contract permits walking but forbids run, jump and
mounting. A second cast cancels the effect. Genuine-client icon, movement
correction and the percentage-detail interpretation remain acceptance gates.
# Flower request admission follow-up (2026-09-20)

Ranking follow-up to `5b11f6d`: the existing `FlowersPacket` dispatcher routes
both female and male gift categories to `GeneralData.ShowGenericRanking2`, but
that page builder only selected female arrays. Male categories therefore reached
a null list. Reuse the existing `GenericRanking.Kiss/Love/Tins/Jade` identifiers
and serializer for their corresponding persisted lists; reject unknown categories
without a page. Top 100 includes position 100 in page nine and self queries.
This is source-based repair of existing routing, not a newly verified native
packet layout. UID tie-breaking is explicit Revora policy.

The existing serializer starts records at byte 24 with an 80-byte stride, but
its fixed 752-byte allocation fits only nine records. Flower pages of ten need
832 bytes including the existing eight-byte trailer. Use a bounded flower-page
factory retaining the legacy minimum size for shorter pages; other ranking
families retain their existing allocation. Record offsets remain source-inferred,
and genuine 6711 rendering of the larger page still requires acceptance.

Durable-delivery follow-up to `1a4402d`: the same admitted 1150 request and
existing effect serializer are retained while source/recipient ownership moves
into PostgreSQL transactions. No new wire family or verified native capture is
claimed. Preserve the legacy free male rose's effect value 4 (despite its rose
counter/category zero) until client evidence resolves that presentation mismatch;
free female kiss retains effect 1. Inventory gifts keep category+1 effects and
the existing female-sender category offset of four. Query-day refresh uses the
existing ranking query boundary and does not add a new packet layout.

Baseline `808522c`. Source evidence only: `PacketID.SendFlower` routes 1150 to
`Flowers.SendFlower`; existing reads use action at byte 4, target UID at 8 and
owned item UID at 12. The server's `SendFlower` serializer writes action as a
uint and emits a 60-byte body plus eight-byte trailer. This does not verify the
exact native client request size. Admission bounds requests to 24..128 total
bytes, requires declared body plus trailer to match, validates ID/client seal,
and reads the whole action field (only existing send actions 0/1).

Nonzero item UIDs must resolve to an owned, catalog-present gift in families
751..758 with a positive encoded quantity. Unknown IDs previously fell through
`GetFlowerTyp` to rose type zero, allowing unrelated item consumption. This
prerequisite does not certify native recipient/binding semantics or atomic
delivery. The existing memory-only counters and disabled flower persistence
remain unfinished; genuine-client acceptance stays open.
# Gem-composition boundary, 2026-09-20

Installment follow-up: existing House Admin dialog option 11 visits showroom
3024. Manager 91017 reuses the existing NPC dialog family: options 1-5 prepare
material deliveries, 6 prepares construction, 40 confirms, 7 returns to Twin City,
and 255 cancels. No new packet layout is claimed. Source startup publishes the
hash-checked server terrain and existing client-template mesh 12970; the mesh
mapping remains static evidence pending rendering. Build and 290 server housing
checks pass. See [implementation and acceptance limits](HOUSING_INSTALLMENTS.md).

Housing integration follow-up: House Admin's existing NPC request/dialog family
adds option 10 to quote the next level's published gem payment, then reuses the
existing option-40 confirmation. No new packet layout, client binary change or
native wire claim is introduced. Build and 217 server housing checks pass;
native menu presentation and acceptance remain open ([scope](GEM_HOUSING_ROUTES.md)).

Before replacing the unsafe legacy action-39/action-51 handlers, static analysis
of the exact approved DX9 hash establishes their common 1009 builder, ordinary
classification argument, seven selected Tortoise UIDs and response event
dispatches. See [the address/evidence ledger](GEM_COMPOSITION_NATIVE.md) and
`tools/audit_gem_composition_native.py`. Confidence: verified static instruction
boundaries only; selection/response UI interpretation and live acceptance remain
open. Ordinary declared length is 95; the seven-UID request is 123, excluding the
eight-byte seal. Never infer selected materials from hardcoded gem types.

Implementation follow-up: static preview construction at `004DFCD9` closes the
ordinary input-type-minus-one ambiguity, and 45 static checks now pass. The new
ordinary handler admits the exact 103-byte request, action UInt16 39, count zero,
binding 0/1 and a published recipe whitelist. It retains existing success value
one and the existing response serializer; consuming UI behavior remains pending
native acceptance. Action 51 is unavailable globally until its outcome policy
and response are resolved; the legacy unchecked grant was removed. Build and
115 ordinary-player fixtures pass; no native/live acceptance or deployment.
## House-visit dialogue routing (2026-09-20)

House Admin visit options reuse the existing 2032 NPC-dialogue layout: declared
length at 0, echoed NPC identity at 8, option byte at 14 and input length at 17.
This is source evidence from the existing bounded quest dispatchers, not a new
native capture. Visit dispatch must run before the legacy NPC dispatcher replaces
the echoed identity with `ActiveNpc`; otherwise a wrong-actor reply loses its
original identity before the service can reject it. Apply the existing 20–120
declared-byte/input-bound checks and the existing client seal to House Admin,
then dispatch visit options
before that rewrite. No new packet family or field is introduced. Native menu,
arrival and achievement presentation remain acceptance gates.

## Mount composition research (2026-09-20, baseline 4ace95c)

The existing Compose reader interprets 2036 as mode byte at 4, count byte at 5
and uint32 item UIDs beginning at 8. Modes 2/3 are labelled CurrentSteed/NewSteed.
This is source-derived evidence only; no native packet capture confirms padding,
count semantics or color/lineage mode behavior. The dispatcher currently delegates
without composition-specific bounds. No wire handler is changed in this research
batch. See [the restoration contract](STEED_BREEDING_RESEARCH.md) for exact source
hashes, ownership/write defects and the required earning and malformed-input tests.

Mount follow-up: approved image 3728bc3f...4fded60 was inspected read-only at
constructor VA 0x8BFE45 and its original/new-breed caller around 0x54F060.
Static evidence supports modes 2/3, total UID count at byte 5, main at 8, minor
UIDs from 12 and declared length 8 + 4 * count. This supersedes the prior lack of
binary construction evidence; it remains static evidence, not a captured native
exchange. `SteedBreedingRules.TryRequest` implements only this bounded shape and
existing transport seal validation. It is not connected to player dispatch yet.
Exact hash, numeric/preview boundaries and 108 checks are recorded in the
[breeding contract](STEED_BREEDING_RESEARCH.md#approved-client-static-evidence-and-rules-foundation).

The owned breeding service will intercept modes 2/3 before legacy composition,
using the approved-image constructor shape above. Any other composition mode
selecting a mount is rejected rather than delegated to equipment calculations.
This is a server ownership/transaction repair; it does not introduce new wire
fields. The installed +12 preview exposes a 2,000,000 progress scale; using that
as a retained-progress cap is explicit Revora policy pending native acceptance,
not independently verified retail server behavior.

Breeding prerequisite: the approved 6711 item-1008 parser (VA 0x980707 onward)
reads lineage progress as uint32 at 42 (0x980887, setter 0x879135), the separate
boolean word at 38 (0x9808D9, setter 0x8794E0), and the next green channel as
uint32 at 34 (0x980868). Revora incorrectly aliases its Suspicious property to
byte 44 inside lineage progress and places NextGreen at byte 37, the high byte
of the native channel. Correct these properties to word 38 and uint32 34;
keep progress at 42. This evidence is exact-image static parsing, not a live
packet capture. Test field independence, production reload and color round trips.


## Sword Master candidate combat (2026-09-21)

Local loaded `magictype.txt` SHA-256
`97ecb413566f5fea541090d1a1c1498ed2acf0874efa6056984583c65c187a71`
contains level-zero Slash 10369, SwordBeam 10504, Immolation 10500,
RevengePunch 10505, BladeVortex 10506, BladeStorm 10503 and Heal 10374.
Later variants also exist; selecting this set is Revora policy, not evidence
of the original monster-to-spell mapping. The official Sword Master guide
(https://co.99.com/guide/monsters/sword_master.shtml) describes single-target,
target-area, nearby-area and healing behavior without numerical formulas.

A Revora-owned x64 read-only catalog inspector, hash
`113A87EF226490E17E01C6AAAC908796F61F44F73AF60E872D056A5D87AA8D27`,
read only monster record 4170: SwordMaster, mesh 953, level 200, life 15,000,000,
raw attack 1,600-2,328, defense 100. Source/binary are ignored under
`obj/chi-refill-validation`. No player data was inspected or changed.

The candidate reuses existing 2032 confirmation, SpellUse, monster spawn/health
and sealed 1101 pickup serialization. No new packet layout is inferred.
Catalog identities are verified locally; animations, long HP bar and genuine
6711 presentation remain unverified until native acceptance. Damage, radii,
timing, level override and healing are explicit Revora tuning.


## Snow Banshee candidate combat integration (2026-09-21)

The public encounter reuses existing monster spawn, full boss HP, SpellUse and
GameCharacterUpdates Freeze=59 serializers. No new packet layout or family is
inferred. Loaded level-zero spell identities are 30010 IceBolt, 30014 FrostClaw,
30013 ArrowHail, 10373 IceBurst, 30011 IceShock, 30012 Avalanche and 10372 Metaplasia.
The official Snow Banshee guide supplies the seven names, ten-degree clockwise
hail rotation each second and five-/three-second freezes; exact variant mapping,
visuals and numerical formulas are not verified original behavior. Radius, cone
width, six-pulse hail, windup/recovery, heal amount and attack tuning are explicit
Revora policies. Existing Freeze messages are presentation only: encounter-owned
server state gates movement and attacks and clears without removing foreign
Freeze flags. Genuine 6711 visuals, freeze recovery and long HP acceptance remain
open. Reference: https://co.99.com/guide/monsters/snow_banshee.shtml


## Purple Banshee reward participant (2026-09-21)

The inactive house reward participant reuses the existing sealed 1101 floor
request parser, floor spawn/removal, inventory item update and SubClassShowFull
Study Point serializers. It adds no packet family or inferred layout. Owned
floor provenance dispatch also covers the existing alternate pickup handler.
Fixture evidence covers malformed requests, repeated native-format pickup and
production reload; it does not establish genuine 6711 visual/client acceptance.
The item summon remains dormant pending combat/lifecycle integration. See
[PURPLE_BANSHEE_REWARDS.md](PURPLE_BANSHEE_REWARDS.md).


## Purple Banshee encounter candidate (2026-09-21)

Use the existing strict 2032 confirmation, full monster HP, SpellUse and
Freeze=59 serializers; no new wire layout. Local family 7565 names PurpleBanshee
and uses mesh 951. Its original skill set is not verified. Revora explicitly
chooses the reviewed seven frost spell identities already used by Snow Banshee
for this house variant, with the same warning/shape/freeze implementation.
This is custom combat, not evidence of original Purple skill or visual parity.
Native spell appearance, full HP and cleanup acceptance remain open.

Server integration now passes 87 encounter checks; Sword Master (79), Snow
Banshee (60) and quest-packet (15) regressions pass. See
[PURPLE_BANSHEE_ENCOUNTER.md](PURPLE_BANSHEE_ENCOUNTER.md) for fixture scope.
# Stored 6711 SRP verifier prerequisite (2026-09-21)

Source evidence: `Network/AuthPackets/Srp6711.cs` currently derives a verifier
from a fresh salt and password, then uses only that verifier for the exchange.
Separating credential derivation from exchange creation can preserve the current
1213/1214 layout while allowing a stored salt/verifier. This is a source-backed
inference, not a new genuine-client validation result. No captured secrets are
used. The planned fixture uses an independent synthetic client calculation.

[RFC 5054 section 2.4](https://www.rfc-editor.org/rfc/rfc5054.html#section-2.4)
describes verifier-based SRP generally; it does not verify Conquer's custom
256-bit group, little-endian arithmetic or password derivation. Existing 6711
parameters remain unchanged for compatibility and are not claimed to provide
modern password-storage strength or replace Argon2id in other Revora services.
Database storage, authentication dispatch and native login remain separate gates.

Implementation evidence: `CreateVerifier` emits salt plus fixed-width verifier;
`BeginWithVerifier` validates exact lengths and `1 < verifier < N`, clones the
salt and generates a new server ephemeral value. Mutable password-derived byte
buffers are cleared on the derivation path; managed strings/BigInteger storage
are not claimed to be securely zeroized. No packet field or group parameter was
changed. Existing plaintext-based `Begin` delegates through the same exchange.
`tests/AccountLogin/Verify-SrpVerifier.ps1` passes 46 focused checks against an
independent synthetic client calculation with no database or native client.
Account-schema integration and persisted ordinary-player authentication remain
unfinished. No authentication material from players was captured or printed.

Account integration follow-up: `AccountTable` now recognizes a persisted format
marker and loads a validated salt/verifier record; `Server` starts that exchange
and still requires the existing 1214 proof before forwarding. It clears any old
pending exchange when a new 312-byte request begins. No new packet layout is
introduced. Server account creation and explicit password changes persist the
marker/verifier atomically; existing accounts are not bulk-migrated. The focused
database suite passes 33 checks and the independent client suite passes 46.
Actual native login, website credential compatibility and production migration
remain unverified. Legacy weak-group SRP is not presented as modern password
storage or as a substitute for existing Argon2id-based services.


### REVIEW-326: Way of Heroes request validation (source evidence)

Existing packet 2830 reads mode at byte 4 and stage/page at byte 5. The
supervisor correction applies the existing declared-length plus 8-byte seal
contract to views and claims, validates the authenticated current session,
and bounds read-only pages to 1?10. Claims still route through the existing
packet handler. Disposable fixtures exercise malformed lengths/seals and page
floods with no progress mutation. This is source/harness evidence, not a new
native 6711 wire verification. Native acceptance remains pending.

### REVIEW-343: existing guild-command session guards (source evidence)

The existing packet 1107 dispatcher reaches `GuildCommand.Process`. It now
checks the current authenticated session and reload fence under the character
lock before dispatch. Existing join/invite actions 1/2 recheck guild recruitment
requirements at reciprocal acceptance; quit action 3 rejects replaced/fenced
callers. Disposable ordinary-player fixtures exercise those existing packets
and production reload. No packet layout was added or newly verified on a
native client. Full recruitment authorization and atomic lifecycle remain open.

## Marriage request/accept server boundary (2026-09-25)

Existing Attack constants identify request 8 and accept 9; Handle's active 3D
branch treats Attacked as the peer. This is existing-server evidence, not a new
native capture or proof of all 6711 UI variants. The old branch accepted without
recorded proposal and projected two spouse names without an owned database
transaction. The replacement will require the canonical sender UID, a nearby
opposite-sex character (existing Flowers body classifications), an expiring
session/entity-bound proposal, ordered owner locks and atomic spouse/achievement
persistence. It preserves the existing packet family and adds no inferred native
layout. Native proposal/accept display remains an explicit acceptance gate.

## Direct equipment upgrade transaction adapter (2026-09-26)

Source evidence: ItemUsage.Process actions 19/20 delegate to ItemHandler.UpgradeItem;
the existing handler reads target UID and material UID from ItemUsage.UID/dwParam.
Material 1088000 selects quality and 1088001 selects level; 1088002 is the separate
legacy level-reduction path. The adapter retains this dispatch and existing item
update/removal serialization. It adds no packet field or result flag. Confidence:
verified source behavior, not a fresh native-client capture. Compose modes 6/7
still have conflicting source count interpretations and are not certified by
this direct-path work. Genuine 6711 upgrade presentation remains pending.

## Compose upgrade constructor evidence (2026-09-26)

Read-only inspection of approved DX9 image SHA-256
3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60
establishes the mode-6/mode-7 request construction at 0x8BFE45. It stores mode
at byte 4, material-array count plus two at byte 5, target UID at 8, a separate
uint32 argument at 12, and the selected UID array from byte 16. Declared length
is 8 + 4 * byte[5] (16 + 4 * material count); packet ID is 2036. Callers at
0x4AA586 (mode 6) and 0x5AC206 (mode 7) supply selected-array length and summed
material value as separate arguments. Helper 0x6A98EE values 720027/720028 at ten,
723268 at five, and ordinary material items at one. Installed catalog names
identify MeteorScroll, DBScroll and MeteorBox respectively. Only matching owned
material families may contribute; the client-supplied value is not payment proof.

This explains why Countx (the low byte at 12) cannot be used as the UID count.
The original Count-2 interpretation is correct for upgrade arrays. Mount callers
use the shared constructor differently: their argument at 12 is the first minor
UID and the copied array holds remaining minors, preserving the previously
researched mount layout. Static construction evidence is verified; a native
exchange and resulting UI acceptance remain pending. No proprietary bytes or
implementation code are added to Git.

## Ordinary repair persistence adapter (2026-09-26)

ItemUsage action 14 continues to select the bag item through UID at offset 8.
The handler now delegates payment and durability to an owned repair transaction.
No wire fields or actions changed; existing silver/item updates are retained.
Action 15 VIP repair is outside this change. Evidence is existing server dispatch
plus isolated packet-handler tests, not a new native 6711 capture.

## Dedicated socket opening, action 43 (2026-09-26)

Approved DX9 hash 3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60:
static sender 0x49996D selects action 43, calls 0x8990F2 at 0x499988, appends each
selected material UID through 0x898FF5 at 0x4999B8, and sends at 0x4999E8. The
shared builder declares 95 bytes, stores target at 8, classification at 12 and
action at 20. The appender increments uint32 count at 26, writes UIDs from 91,
and extends length four bytes per entry. Socket requests therefore have declared
length 95+4*N and transport length 103+4*N, with the existing eight-byte seal.
The target getter called at 0x49997C reads its object's UID at +12. Static
construction is verified; native UI acceptance is pending. Recipe sizes are
1/5/7/12, and payment must use precisely the selected owned material UIDs.
The earlier server-generated 112-byte singleton fixture is not this native shape.

### Clan membership consent boundary (2026-09-25)

Legacy source Clan.Process consumes 1312 actions 9/11 (recruit/application) and
10/12 (their accepts), peer UID at byte 8 and acceptance byte at 16. This is
source-inferred, not native-client verified. Hardening retains the existing
route while requiring bounded framing and session-bound mutual consent; no new
packet family is enabled. See CLAN_MEMBERSHIP_EARNING.md for current limitations.

### Existing clan quit/kick boundary (2026-09-25)

Clan.Process currently handles 1312 action 23 as self-quit and action 22 as a
kick using the length byte at 17 and target name beginning at 18. This is
source-inferred, not native-client verified. The repair retains these actions
and checks bounded framing, current membership and durable leader authority;
it does not add a new client command or infer consent from a target UID alone.

### Existing clan leadership transfer (2026-09-25)

The legacy 1312 action 21 selects an online clan member by the length byte at 17
and name beginning at 18. This source-inferred boundary is not native-client
verified. The repair keeps that existing leader-issued action and online-target
requirement; it adds saved-authority checks and one transaction for clan identity
and member ranks. No new recipient-consent packet is invented.

### Clan War territory result boundary (2026-09-25)

The existing 1313 constructor uses 148 bytes including the eight-byte footer;
actions 6 and 8 query an envoy and request entry. NPC UID is at offset 12.
These offsets are source-inferred, not confirmed by a native 6711 capture. The
owned route will retain that family, require bounded framing and a nearby actual
envoy, and serialize server-owned admission/result records. Client achievement
catalog hash df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc maps
10305/10306/10307/10308/10309 to Twin/Desert/Bird/Ape/Phoenix territory occupation.
Legacy arena indices 0/3/4/2/1 respectively identify those city maps; the other
five wilderness arenas have no separate achievement identity in this catalog.


### Guild donation dispatch hardening (2026-09-25)

Evidence: existing `GuildCommand` serializer/parser uses packet 1107, action
at offset 4 and uint amount at offset 8; actions 11 and 20 are silver and CP
donations. No new packet layout is introduced. The donation branch now requires
36-128 total bytes, declared body length equal to total minus eight, packet
1107 and the existing TQClient seal before invoking payment. Source confidence
is confirmed for this parser/serializer; genuine client capture acceptance is
still pending. The isolated guild suite exercises the existing command path,
invalid seal, payment rollback and ordinary reload. This does not verify the
shape of every live client variant. No credential packet content is logged.


### Guild profile and arsenal request bounds (2026-09-25)

Evidence: existing GuildProfilePacket fields run from offset 8 through offset
59; PacketID.TopGuild is 1058. The profile path now requires 68-128 total bytes,
a matching declared body length and packet ID, and the existing eight-byte
client seal. It displays counters without changing treasury or offices.
The existing arsenal parser identifies packet 2203, action byte 4, slot byte 8,
item UID at 12 and enhancement at 16. Dispatch now requires 28-128 total bytes,
matching declared body and ID, client seal and a slot in 0..8 (0 aliases head).
No new offsets are introduced. These are source-derived safety bounds, not
claimed verified native client captures. Ordinary packet-dispatch fixtures cover
profile response fields and arsenal unlock payment/reload; actual 6711 UI
acceptance remains pending. Enhancement semantics are still legacy work.


### Arsenal enhancement ownership and unavailable advertisements (2026-09-25)

The existing 2203 action 3 still takes its enhancement value at offset 16.
The parser now passes that uint directly to the owned operation; the shared
static packet buffer is removed. The level-1/2 cases and 50,000-times-level fee
are source-derived legacy rules, not newly verified official pricing. Frame
bounds remain those documented above. Real client acceptance is pending.

Active packet 2225 previously changed a bulletin and subtracted cached silver
without publishing an advertisement. The alternate Guild_AdvertiseRecruit
adapter has overlapping uint requirement fields at 272/274/276/278/280 and a
commented-out dispatcher. Publishing/charging is now unavailable globally and
neither adapter changes funds. This follows the repository's rule for
unconfirmed protocol families. Do not mark advertisements implemented until
verified layout, publishing, eligibility and atomic payment are present.


### Owned guild inscription requests (2026-09-26)

Evidence: existing ArsenalPacket and Arsenal.FastInscribeItem source at base
commit 2165bdd. The 2203 actions 1/2 reuse the existing item UID offset 12 and
slot offset 8; framing remains unchanged. Both now resolve owned bag/equipped
items and use a transaction for inscription/removal and achievement 10226.

The existing 2204 loop reads count byte 4 and four-byte UIDs starting at 5.
The hardened parser requires 1..40 entries, exactly 13+4*count total bytes,
matching body length/packet ID and the existing eight-byte client seal.
Duplicate UIDs are processed once. This exact compact frame is a source-derived
interpretation tested with synthetic dispatch; native 6711 padding and UI
acceptance remain pending. No packet capture or new field verification is
claimed. Bulk operations commit per owned item, not as one batch.


### Durable guild office appointments (2026-09-26)

The existing 1107 actions 28, 29 and 34 use the same source-derived role uint16
at offset 8 and one length-prefixed name at offsets 24..26 as leader transfer.
Discharge action 30 uses that target name and maps to Member. All these mutation
routes now share the bounded 35..128-byte parser: declared length, family,
client seal, exactly one nonempty name up to 16 bytes and zero trailing padding.
The action numbers and offsets are local source evidence, not native captures.

The appointment-list response retains its existing string layout but now draws
role availability, capacities and personal CP fees from the same policy as the
mutation path. The UI no longer advertises ten deputy slots while the server
allows six. Manager/supervisor are excluded from manual appointment. Synthetic
request tests cover ordinary actors, malformed name length, offline recipients,
replay and discharge; native UI handshake/acknowledgment acceptance is pending.


### Interaction cancellation admission (2026-09-26)

Source base `4466e7f`: `Attack` declares interaction request/accept/refuse/effect/stop
as 46/47/48/49/50. This is existing source-derived behavior, not a newly verified
6711 wire layout. `Handle.InteractionStopEffect` and `InteractionRefuse` accepted
a target UID without reciprocal pairing, allowing unrelated interaction state
to be cleared (stop also teleported that target to its current location).
The admission repair requires distinct healthy canonical sessions, the exact
actor entity, the same map instance, proximity, and reciprocal partner UIDs,
under nonblocking session leases and ordered owner locks. Existing response
serialization is unchanged. Native cancellation acceptance, request consent,
paired movement and achievement 10130 remain separate incomplete work.


### Recipient-owned interaction consent (2026-09-26)

Base `804fb55`. Legacy request/accept types remain 46/47; this change does not
claim native layout verification. Request state previously wrote mutual partner
UIDs, and accept trusted those fields, so the inviter could accept on behalf of
the recipient. The repair keeps one two-minute invitation per recipient, bound
to the exact sender/recipient sessions and entities. Only the invited recipient
can consume it, under ordered nonblocking ownership locks. A separate accepted
pair record gates effect activation; cancellation clears that proof. No new
achievement is emitted. Movement and genuine 6711 acceptance remain pending.

The effect handler continues resolving its peer from the server-owned partner
field, not a packet target. Review found `Attack.ResponseDamage` aliases
`Buffer.Damage` rather than the separately declared protobuf response field. No
accessor or packet-layout change is made without native evidence; interaction
mode identity and achievement 10130 therefore remain unverified.


### Paired session termination (2026-09-26)

Base `c978433`. Disconnect closes the shared invitation/accepted-pair proof by
exact session object, without taking the other player's locks. Before subsequent
requests, each participant discards expired or closed local state under its owner
lock. Legacy walk/jump follower branches additionally require a live accepted
pair, matching map identity and bounded proximity. No wire layout changes or
achievement producer are introduced. These are ownership/cleanup prerequisites;
native mode identity and complete paired movement acceptance remain unverified.


### Owned follower movement (2026-09-26)

Base `a03c522`. Existing follower walk/jump writes are moved behind the same
nonblocking session leases and owner locks as consent. The follower must still
stand at the leader's accepted starting tile, while the leader must be at the
expected destination. Both need live accepted effect state on the same map.
Terrain, range, follower movement restrictions and exact session references are
rechecked before follower coordinates change. Existing outgoing packet families
are retained; no claim of native hand-holding mode identity or 10130 is made.


### Separated-pair reinvitation (2026-09-26)

Base `6076256`. A new invitation initiated by a participant may close that
participant's separated accepted pair under its owner/session locks. This does
not move either character or accept the replacement invitation. Merely receiving
a third party's request cannot dissolve an active separated pair. The peer only
clears already-closed state through ordinary refresh. Existing packets and native
mode uncertainty are unchanged.


### Guild contribution score projection (2026-09-26)

Base `45ebf7f`. Existing GuildDoantion.cs packet 2101 serializers contain silver,
CP, Guide, PK, arsenal and total donation fields; Guild.SendGuild writes the
member total at offset 8 in its existing buffer. This is source evidence for
existing fields, not newly verified native behavior. No packet family, size,
field order or action is added. The planned projection uses contribution points
from the official donation guide (CP x20; silver 10,000 per point through 300,000
points, then 100,000), excludes flowers from total contribution and saturates
32-bit displayed values instead of wrapping. Saved paid currency remains raw.
Native labels, totals and numeric-bound presentation remain pending.


### Guild ranking response ownership and pages (2026-09-26)

Base `fda77aa`. Existing packet 2101 source reads category/page at byte offsets
4/5 and emits a 12-byte header followed by 68-byte rows and the eight-byte
TQServer seal. Categories 0..9 map silver, CP, Guide, PK, arsenal, rose, orchid,
lily, tulip, total; non-total lists expose 20 entries and total exposes 100.
These are source-derived layouts, not a new native capture. Preserve them while
replacing per-page arithmetic with bounded ten-row slices of one owned snapshot.
Input admission checks minimum header/seal space, declared size, family, seal
and supported category/page before accessing guild state. Native navigation and
empty/last-page presentation remain unverified.

### 2026-09-26 Azure Pill journal projection

Base 9007c9f. Reuse the existing 1134 quest status serializer and strict Begin
handler without changing packet layout. Missions 1727-1732 come from the
hash-bound installed journal (see AZURE_PILL_IMPLEMENTATION.md). Saved stages
1-6 project one accepted mission and completed predecessors; stage 7 projects
all finished. Client Begin never authors these saved stages. This is source
and database-fixture evidence, not a native packet capture or acceptance claim.


### 2026-09-26 Power Book saved journal projection

Base 7b7cdf24. Missions 1840-1852 are evidenced by the hash-bound installed
Questinfo.ini audit in POWER_BOOK_CONTRACT.json. Reuse existing 1134 status
serialization without changing the packet layout. Saved stages 1-13 project the
matching accepted mission and finished predecessors; stage 14 finishes all.
Client Begin and NPC quest-abandon requests must not author/reset durable progress.
This is source/local-client-file evidence, not a native packet capture. Private
scene navigation remains owned-book travel; static journal coordinates cannot
identify a dynamically allocated scene. Do not label dynamic native routing verified.


### 2026-09-26 Connector saved journal

Base f0a14080. Installed hash-bound missions 1718-1722 are documented in
CONNECTOR_CONTRACT.json. Reuse existing 1134 status serialization: saved stages
1-5 project accepted mission and completed predecessors; stage 6 finishes all.
Begin and NPC quest-abandon requests refresh authoritative progress. No packet
layout changes or native capture claims. Counts remain available in Chen's
conversation; no unverified objective-counter packet layout is introduced.


### 2026-09-26 Spun Gold cosmetic appearance seam

Base 658f78a2. Entity already emits mesh through its 10014 spawn buffer and
existing mesh-update path. CityGuardSystem uses human mesh 2562005 for Officer
Lana in this 6711 source. Reusing that female appearance for the Revora quest is
a local representation choice, not proof of the original quest's beauty model.
Keep Body/Face, transformation/HP state and saved identity unchanged; project only
the mesh field. No packet layout changes. Native quest-disguise rendering remains
unverified and must stay behind the ordinary-client acceptance gate.

## 2026-09-26 ? Spun Gold saved journal projection

Missions 1723..1726 are grounded in SPUN_GOLD_CONTRACT.json and the hash-checked
installed Questinfo.ini. The existing MsgTaskStatus projection and Begin/abandon
protection paths are reused; no new packet layout is inferred or enabled.
Saved stages map to the four mission statuses, with no claim that original
native disguise/kill subtask counters are implemented. Private overlay rewrite
checks preserve all other mission bytes. Full-route tests verify saved statuses
and rejection of forged Begin; native rendering/interaction acceptance remains
pending. See SPUN_GOLD_IMPLEMENTATION.md for hashes, test scope and gates.

## 2026-09-26 ? Artisan Feather journal projection

The installed five-stage contract (ARTISAN_FEATHER_CONTRACT.json) defines missions
1733..1737. Existing MsgTaskStatus status projection and Begin/abandon protection
are reused; no new packet layout or original subtask-counter behavior is claimed.
The NPC reads the persisted 100-feather count. Private overlay directions match
the loaded Vulture spawn regions and preserve unrelated bytes. Full-route tests
prove server status progression and forged Begin rejection, not native display
or navigation. ARTISAN_FEATHER_TERRAIN.json records a failed conservative Twin
City route audit. Native/Host acceptance and deployment remain pending.

## 2026-09-26 ? Shared quest cosmetic projection

QuestAppearance reuses the existing Entity mesh spawn/update field for owned
Spun Gold and Mystic Ring leases. No new packet layout is asserted. Mystic
candidate 30040 is derived from npc.ini BlueMouse type 689 and 3DSimpleObj.ini
part/texture links recorded in MYSTIC_RING_SERVER_CATALOG.json. Native player
rendering of that NPC/simple-object candidate remains unverified. Server tests
prove projection, identity preservation, conflict ownership and cleanup only.
No native acceptance or blue visual parity is claimed.


## Chi request admission and pursuit costs (2026-09-26)

Source evidence: `Network/GamePackets/ChiSystem/Chi.cs` declares a 16-byte body
plus 8-byte client seal, type at 8, power at 10 and UInt32 lock mask at 11.
The existing reader accesses through byte14. Dispatcher2533 now rejects bodies
shorter than16 before deserialization. This is source-layout and synthetic-frame
evidence; native6711 capture acceptance remains pending. No field offsets or
outbound layouts change.

Mutation admission accepts power1..4 and existing owned powers for pursuit.
Masks0..14 select unlocked attributes; unknown bits and all-four-held mask15
are rejected before automatic CP refill. Actual existing cost is50 plus50 per
held attribute. The dispatcher now compares against that cost, replacing its
fixed200 threshold. Existing explicit refill and insufficient-Chi refill policy
remain. Ordinary sealed packet tests cover all masks, malformed modes, short
bodies, paid unlock, pursuit and production Chi reload. No protocol-family
expansion or native acceptance is claimed.


### JiangHu creation bounds and transactional dispatch (2026-09-26)

Evidence is local source plus synthetic handler fixtures, not a native-client
capture. `Game.JiangHu.JiangHus` retains message 2700 action at byte4, creation
name length at byte6 and name at byte7. Creation now requires 1..16 characters,
a declared body covering the name, and the existing eight-byte trailer before
reading the name. Synthetic sealed frames pass through `PacketHandler` into
ordinary atomic creation. `ExtraStudy` retains action/star/stage at bytes9/10/11
and delegates free/paid training to the transaction service. No new wire layout
or native interoperability is asserted. See `CULTIVATION_COMBAT.md` for recovery
limits and isolated database evidence.


### Perfection tempering request evidence (2026-09-26)

Local `ItemRefineOperationProto` defines operation/target/string/material UIDs as
protobuf fields1/2/3/4; existing dispatcher3251 reaches `CMsgItemRefineOpt`.
An isolated x86 probe serialized this source model, placed it after the four-byte
game header with the normal eight-byte seal, and the existing reader returned
Temper, target1234 and material5678 correctly. Therefore the hypothesis that the
reader always fails on a game header is disproven. This is synthetic source
compatibility evidence, not native capture. The active Temper branch instead
reinterprets all raw protobuf integers as material UIDs and deletes inventory
before saving progress. The next implementation must use the decoded field4,
validate ownership and binding, cap progression and commit consumption with the
target. Existing source values are10/100/1000/10000 for star stones3009000..3,
and a54-level cap. No new wire field or official economy claim is established.


## Perfection owner field clearing (2026-09-26)

Source evidence: `ConquerItem.PerfectionOwner` reads the existing 16-byte field at
buffer offset72. Its setter previously wrote only the new string, leaving trailing
bytes when replacing a name with a shorter name or an empty value. The exchange
fixture reproduced runtime/stored preimage disagreement after clearing ownership
and exchanging again without a reload. Clear this existing field before writing;
no packet identifier, length or layout is changed. Confidence: source/isolated
handler evidence; genuine6711 display acceptance remains pending.

## Ordinary +composition persistence boundary (2026-09-26)

Baseline d22b49ed: Compose.cs and ItemHandler.ComposePlusLegacy identify existing
2036 mode 0, total UID count at byte 5 and target/material UIDs from byte 8.
Bounds use declared length 8 + 4*count plus the eight-byte client seal; count is
2..41 and all UIDs must be nonzero/distinct. This mode-0 interpretation is source
inference; mount constructor evidence does not independently verify every mode.
No new wire family is enabled. Native mode-0 acceptance remains pending.
The planned owned replacement retains installed enhancement thresholds and
credits, rejects invalid ownership/materials before mutation, and commits result
and consumption together. Mode 5 chance enhancement is outside this change.

## Paired interaction constructor evidence (2026-09-26)

Approved DX9 constructor0x8BB1F7 separates arguments6/7 into native object offsets
0x60/0x84 with presence masks0x4000/0x800000. Request46, accept47 and refuse48
call sites and eight request UI-effect mappings are recorded in
[paired movement evidence](PAIRED_MOVEMENT_EARNING.md#approved-client-interaction-constructor-2026-09-26).
33 hash-bound instruction checks pass. Serializer tag mapping and hand-holding
identity are still inferred/unresolved; field12 is not established as the missing
interaction value. No packet handler or earning producer is changed.

## Paired serializer field confirmation (2026-09-27)

The approved DX9 serializer writes constructor offsets0x60/0x84 as fields15/24;
its tag helper and parser independently confirm wire-type-zero tags0x78/0xC0.
Field12 uses a different native offset0x54. Selector43 reaches the double-dance
cursor resource; it does not establish hand-holding. The reproducible audit passes
61 instruction checks plus exact localization checks. See
[provenance and boundaries](PAIRED_MOVEMENT_EARNING.md#verified-serializer-fields-and-dance-selector-2026-09-27).
This supersedes only the previous serializer uncertainty. Hand-holding identity,
server earning and native acceptance remain open; no wire behavior is changed.

## Hand-holding identity, approved DX9 (2026-09-27)

Static approved-client evidence links dialog 360 control 2436 to effect 100004
(UI selector 28). Constructor 0x4FF208 passes dialog 360 at 0x4FF224;
control binding 0x4FF47A uses 2436. The WM_COMMAND map at 0xB9AE38 binds
2436 to callback 0x4FF343, which sends 100004 through 0x76225F with command
3065. The independent effect-indexed icon branch for 100004 reaches 0x4FF148
and `Mutual_HoldHandPic`. Installed GUI.ini section 360-2436 labels hand-holding.
GUI SHA-256: `8ef1d6cd6109fe4227bf28d9e877d30428e7680abd98093337d23119e252128b`.
Executable provenance/hash and field 15 mapping are recorded above. This is
static UI/action identity evidence, not a native movement/animation acceptance.
No new field layout is inferred and no wire accessor is changed.


## Boost static price and request contract (2026-09-27)

Read-only audit `python tools/audit_perfection_boost_native.py` passes **59
instruction checks** against the approved DX9 SHA-256
`3728BC3FD4E584D85301EDD2B1887A176267CDAA6356038F9951EF23E4FDED60`.
It also gates `ini/info.ini` by SHA-256
`F698ABCFC6595A6ABA333B5B1C87545DD3B4891FFFA284A2A6372139B5997337`.
No client executable is executed or copied into Git.

Static findings supersede the earlier resource-only uncertainty:
- `[EquipRefine] Emoney=8, RefineExp=10`; native reads at 0x49FD8D and
  0x49FDFF feed whole-bundle division/multiplication at 0x49FEC2..0x49FECF.
  The installed client therefore budgets 8 CPs per 10 progress points.
- Confirmation handler 0x49C5EF passes operation 3, selected-item accessor
  result, count 1 and the address of confirmed spending (`this+0x20C`) to
  builder 0x8C0788. The builder selects packet 3251.
- Protobuf writer 0x8C01F5 writes operation as field 1, item as field 2,
  and the integer array as repeated varint field 4. The builder does not
  set string field 3. Native parser 0x8C089A accepts field 4 packed and
  unpacked. These are static constructor/writer/parser observations, not
  captured traffic or successful native server acceptance.
- An item flag selects either one wallet getter or the sum of two getters
  (0x49FE43..0x49FE78). Exact binding identity, wallet mapping and payment
  priority are still unconfirmed. Do not infer a bound-CP spending policy
  solely from this branch.

Boost is **not fixed or completed**. Next: resolve the wallet/binding contract,
then implement bounded atomic payment/progression, reject excess spending at
cap, and test ordinary-player ownership, malformed/native-shaped requests,
duplicate delivery, rollback and fresh reload. Existing broken RVP behavior
is unchanged by this evidence checkpoint. No production deployment or schema
change; native acceptance remains pending by owner choice.


## Revora atomic Boost implementation contract (2026-09-27)

Packet3251 operation3 carries one confirmed CP amount in repeated field4;
string field3 is absent from the native builder. Keep packet decoding compatible
with that shape. The static bound-item flag getter0x8792CE is also consumed at
0x428985: value1 appends STR_MONOPOLYPRE (Cn_Res.ini:2678, `(B)`); the Boost
predicate0x8794F4 admits flags1..3. Wallet ordering is not encoded on this request.
Revora policy: bound gear spends bound CPs first, then regular CPs; unbound gear
spends only regular CPs. This ordering is a Revora rule, not original-server proof.
Use8 CPs per10 progress, require one positive multiple-of8 amount, and reject
amounts above the smallest whole bundle reaching the current Revora level54 cap.
Persist both wallet debits, owned item progress and a receipt in one transaction.
A one-second per-player/item durable cooldown absorbs immediate duplicate clicks,
including across reload. The wire has no request nonce or expected item state:
arbitrarily delayed identical requests cannot be distinguished from new purchases.
Do not claim exactly-once delivery beyond that bounded duplicate window.


## Private Account listener recovery prerequisite (2026-09-27)

A bounded loopback probe targets only the existing candidate on127.0.0.1:19558,
using its DPAPI-held ordinary account. Source evidence at c4245809:
`Server.AuthServer_OnClientReceive`, `AuthClient.Send`, `Authentication.Deserialize`,
`Srp6711Exchange.CreateChallenge/TryReadProof`, and `Forward` define the current
Revora exchange:8-byte1059 seed,312-byte1636 request (32-byte username at4; empty
password field at73),653-byte1213 challenge,262-byte1214 proof and224-byte1637
forward. Account lengths include the full frame; there is no game packet seal.
The client-side test cipher is the inverse of the server's documented byte
transform, with independent directional counters. Existing independent SRP
fixture math supplies the proof without accessing server private state.

Confidence before execution: verified source layout, not yet verified live socket
behavior for this probe. A successful run will prove the running Revora listener's
behavior with this synthetic client, not a genuine6711 login, DH/game entry,
character earning or reward recovery. Credentials, proof bodies and forwarding
grants must remain in temporary process memory and never enter logs or Git.


Probe execution passed on gameplay89a9d09e in two actual private Host/database
cycles (ignored `obj/account-wire-host.log`). The actual Account listener accepted
the ordinary account proof and returned a loopback game handoff; a repeated proof
coalesced with the first request and a separately corrupted proof were rejected.
The login was written in two pieces; TCP may coalesce those writes, so this does
not prove a particular receive segmentation. No grant value, credential or proof
body was persisted. The game/DH, character-entry and earned-reward recovery gates
remain open. Native-client interoperability is not inferred from a test client.


## Private game handoff probe (2026-09-27, before execution)

Source6dc422d2 defines an encrypted Game hello with an11-byte prefix and a length
at offset11. `DHKeyExchange.ServerKeyExchange.GeneratePacket` writes bounded junk,
two8-byte IVs and length-prefixed hexadecimal P, G and server public key, followed
by TQServer. Client reply parsing expects7 prefix bytes, a4-byte length,10?49 junk
bytes and a1?128-character public key plus8 trailing bytes. The existing receive
path still assumes that initial reply arrives in one receive; the probe must not
claim fragmentation correctness for this path.

The proposed test client uses the installed CO2_CORE_DLL DiffieHellman/CAST5 API,
with the server's MD5/hex derivation, then a sealed1052 handoff. Dependencies are
existing Revora runtime artifacts, not newly acquired code. CO2_CORE_DLL SHA-256:
`71EA5FA0CDB40E577F222CBDE5FB2BC750CA3332412F58CEF2C353C75E0DCC2B`.
The existing characterless private account should receive the exact NEW_ROLE
control message. Confidence: source-derived pending live probe; no native-client,
character creation, fully loaded session or earned reward is established yet.


The private socket probe reached exact NEW_ROLE through the actual Game DH and
sealed1052 path in both Host cycles. A follow-up negative probe demonstrated that
the same grant also obtained NEW_ROLE on a second connection: the characterless
branch returned before removing AwaitingPool. The redacted startup log contained
two NEW_ROLE responses for the one authenticated private account.

Correction: consume the matched grant under LoginSyncRoot before either new-role
or existing-character handling; ignore repeat1052 on an already-associated socket;
explicitly disconnect an absent grant. The probe requires EOF/reset on game-grant
replay (a timeout is not acceptance evidence). This changes server authorization
state ownership, not the source-derived packet layouts. Ordinary character
creation and earned-reward recovery still require subsequent validation.


Post-fix evidence: the isolated assembly built successfully and the real Host
probe passed in both database/start/stop cycles. Each fresh Account grant reached
NEW_ROLE once; reuse on a second Game connection was closed, not merely timed out.
The same negative check failed against the previous candidate. All 28 runtime
schema tables were retained and both shutdowns were graceful with exit code 0.
Logs: ignored obj/game-handoff-replay-before.log and
obj/game-handoff-replay-after.log. This verifies this server's synthetic client
handoff, not native DX9 acceptance or existing-character/reward recovery.


## Private ordinary character persistence probe (2026-09-27, source-derived plan)

The explicit CheckCharacterEntry mode uses the existing ordinary private account.
After NEW_ROLE, it sends source-derived packet 1001 (declared 76, sealed length84),
name at24, body1006 at72 and creation-class3 at74 (server maps to Trojan10).
It requires the server's 1006 character-info frame with UID at8, class at81,
rebirth at85 and bounded name at149/150. On the second real Host/database cycle,
it reauthenticates with a fresh grant and requires the same persisted UID/name.
Database assertions separately require ordinary account state2, the sole private
character, account ownership, creation receipt and spawn1002(327,303). This leaves
the private character in place for later earning/recovery tests; it does not seed
rewards, elevate the account, or claim world loading/native client acceptance.
Catalog preparation stays separate because its empty-player guard is retained.


Execution evidence: packet1001 created RoadmapProbe, UID1096321, with the normal
creation receipt. Login then failed in LoadItemStatsCore via BloodshedSeaBalance
(restored before items/spells/subclasses/Chi); an isolated stack-only diagnostic
confirmed that path. The diagnostic was removed from the final assembly.
Boost state now loads without rebuilding stats during EntityTable.LoadEntity;
GameState.LoadData rebuilds after the saved dependencies. Two subsequent real
Host/database cycles returned character-info for the same UID and retained the
ordinary state2 account, receipt and configured spawn. No world-entry, earned
reward or crash-recovery acceptance is inferred. Logs: ignored
obj/character-wire-first.log, obj/character-wire-diagnostic.log and
obj/character-wire-after.log, plus the redacted private Host log.


## Private world/Courier probe (2026-09-27, source-derived plan)

Extend explicit character mode using the existing Data protobuf serializer for
10010 actions74,76,77,78,97,114,251. Require matching UID/action replies; source
SetLocation must return map1002 and327,303. Jump137 uses Coords_X/Coords_Y. Move
four tiles to323,303, within Q.Near's six-tile radius of Cerys900000017 at319,303.
2031 CMsgNpc requests option0 then1; bounded2032 reply frames expose option IDs
at14, interaction type15 and Finish100. Require option3 for accepted StageAccept,
repeat acceptance without leaving that stage, then return to spawn. The second
Host cycle must show option3 before acceptance, proving saved stage restoration
through an ordinary connection. This is source-derived server interoperability,
not native-client validation or completion/earning of the Courier reward.


Execution: both real Host/database cycles passed. First connection reported
restored=False, accepted through the2031 ordinary NPC-init handler, and received
StageAccept option3. After graceful Host/database restart, the second connection
was required to report restored=True before any acceptance request; it did.
Both rounds completed action acknowledgements, authoritative SetLocation after
each jump, repeat acceptance and return to327,303. Source-derived use of2031's
option field is not evidence for the native client's2032 dialog-selection UI.
That separate path and native rendering remain unverified here.
Log: ignored obj/world-courier-wire.log. No character level, quest stage or reward
was written directly by the probe; the server performed normal persistence.


## Private Courier commander and hunt travel (2026-09-27, source plan)

Switch positive NPC choices to the normal fixed2032 selection frame: declared20,
sealed28, NPC ID at8, option14, interaction type2 at15, empty input.2031 protobuf
remains only the initial NPC click. After Cerys option3, require1002(442,339),
Commander91001's option10, then return via existing public Data.SendTC177 and
short jumps to Cerys. Greeting must expose hunt option4. Require repeat accept1
to retain hunt, travel via4 to1011(130,250), and return by public action177.
Second Host cycle must expose hunt option4 before advancing any stage. No direct
quest progress/level/monster/reward writes; combat and turn-in remain pending.


Actual private Host evidence: both cycles passed the2032 option path. The first
advanced through Commander91001 to hunt and reported hunt-restored=False. After
graceful Host/database restart, the second required hunt-restored=True
before any advancement and passed. Both verified quest teleport coordinates,
public return action177, short movement back to spawn, preserved hunt on repeated
acceptance, ordinary SQL identity/spawn and game-grant replay rejection.
Log: ignored obj/courier-hunt-wire.log. Actual native dialog UI, combat, turn-in
and abrupt recovery remain pending; no private rewards or levels were seeded.


## Private Courier combat preparation (2026-09-27, source plan)

Read-only private catalog inspection found regular Bandit32/545HP/81-89attack,
BanditL33/1635HP, plus stronger Messenger/Aide variants. The ordinary saved
RoadmapProbe was level1 with342HP, a130006 armor and410009 weapon,10000CPs.
The installed itemtype-new catalog prices723700 EXP Ball at27CP; emoneyshop.ini
contains it under2888. Prepare the character through normal1009 BuyFromNPC1 then
EquipItem4 consumption, never direct level writes. Limit preparation to two
balls per probe and stop once level32 is reached. Require immediate1008 item UID,
10017 CP decrement27,1009 removal and10017 level increase. This tests the current
server catalog/service; it does not certify the unrelated legacy shop's atomicity.


Combat probe plan: source-derived10014 spawn fields identify only regular Bandit
or BanditL33. Move in bounded fifteen-tile steps near the installed hunt region,
select a visible target within17 cells, jump adjacent, send1022 melee2 with no
client damage value, then require server kill14 attributed to this UID. Stop on
player death, missing target or bounded timeout. On return, Cerys must report
exactly one more kill. This first transport combat check is deliberately bounded
to one real death per Host cycle before scaling to the complete20-kill contract.


The first real combat attempt stopped before combat:130,250 and130,265 are
blocked in the actual944x9441011.map. SHA256
7E8F7A8262F8AE93A314699AF6D276F0324FFF6C32BCC6061BFFC2F336F1E817.
Read-only terrain analysis found112,272 is the nearest five-by-five clear landing
to130,250 under Chebyshev then Manhattan distance; it connects by ordinary ground
to130,330 in the Bandit spawn region. Correct the quest's public constants to
112,272. The probe gates this terrain hash, checks the landing footprint and uses
walkable short-jump endpoints, rather than bypassing terrain or editing progress.
An interrupted private hunt resumes through existing public return action177.


Two post-fix Host cycles confirmed actual melee deaths and persisted Courier
progress0->1 then1->2; no synthetic monster death or counter writes were used.
The preparation had purchased/consumed two27CP balls, yielding levels30 then35
and9946CPs; the following restart retained level35. Scale the socket probe to
three actual kills per bounded connection, then ordinary clerk/turn-in when20
is reached. Require exact immediate purse deltas, level120, one new bound soul
and duplicate-claim refusal; native rendering and abrupt recovery stay separate.


Final execution: the socket player completed all20 installed-monster kills,
clerk approval and normal2032 turn-in. Immediate updates showed8000CP,80000silver,
200RVP, level120 and bound soul UID59752588/type823058. The active catalog identifies
823058 as CraneRing, purification level7. Repeated turn-in was refused. Two more
real Host/database cycles preserved the exact earned receipt fingerprint and
its bound, permanent inventory item; completed-dialog replay paid nothing.
Logs: obj/courier-preparation-wire.log, obj/courier-combat-after.log,
obj/courier-full-wire-{1,2,3}.log and obj/courier-earned-restart.log (ignored).
All were normal shutdowns; abrupt interruption and native acceptance remain open.

## Equipment blessing hardening (2026-09-27)

Baseline46b102a3 ItemUsage.Mode.Bless is existing family1009 action40. The server
reads target UID at8, count at26 and material UIDs from91. This is source-inferred;
no action40 native sender has been verified. The hash-bound DX9 shared builder
0x8990F2/appender0x898FF5 is proven for action43, not proof for action40. Harden
this existing mutation with bounded exact framing: count1/3/5, declared length
91+4*N or95+4*N (minimum UID payload or shared-builder four-byte trailer), eight
seal bytes, matching family/action and distinct nonzero material/target identities.
Do not describe this as native acceptance or enable another packet family. See
EQUIPMENT_BLESSING.md for recipe evidence and transactional acceptance.


## Existing gem embedding boundary (2026-09-27)

The installed server routes packet1027 to EmbedSocket.Process. Its existing
reader uses target UID at12, gem UID at16, ushort socket index at20 and ushort
operation at22 (0 insert,1 remove). These offsets and meanings are source-inferred
from the existing handler, not newly verified against a native6711 sender.
The bounded adapter accepts32..64 bytes, the established payload-length header
plus eight-byte client seal, indices1/2 and operations0/1; bounded extension
bytes remain uninterpreted. Native acceptance remains pending. No new packet
family, credential field or client artifact is introduced.

The previous implementation persisted socket state before consuming the gem,
allowed replacement of socket2 without checking for an empty hole, and could
clear socket1 when removing socket2. EquipmentSockets.Embed now validates owned
items, existing holes and family compatibility, and commits insertion payment,
result, deduplication receipt and equipment evidence together. Removal destroys
only the selected filled gem, retaining the hole and the other socket, as a
correction of the existing destructive removal operation; it grants no gem.
A request has no verified unique operation nonce. A persistent receipt prevents
reusing one gem UID in the same target/socket, including after removal; use a
new gem item UID for a deliberate replacement. Delayed removal packets cannot
be distinguished from a new removal of the currently selected gem.
See GEM_EMBEDDING.md for validation and remaining transport/native gates.


## Taoist Epic NPC integration, 2026-09-27

Source evidence: the existing NPC reply dispatcher deserializes the same
NpcDialog family and option/echo fields used by Ninja and Monk Epic. The Taoist
coordinator joins both Npcs.GetDialog opening and PacketHandler reply dispatch.
Its actors and Taoist Moon10000 use the existing20..120-byte declared-body check,
body-plus-eight-byte framing and byte17 text-length bound. No new packet family,
field layout or client patch is introduced. This is existing-source integration
evidence, not a verified6711 capture. Native rendering and socket acceptance
remain pending. Private handler-driven tests cover Moon entry, Bruce return,
three oven offerings and interrupted Sun King confirmation; ordinary production
initialization remains disabled. See TAOIST_EPIC_RESTORATION.md for scope.


## Taoist journal3418 integration, 2026-09-27

Installed Questinfo identifies mission3418/type6 with Divine Panacea as its
prize. TaoistEpicJournal projects saved trial receipts into the existing1134
list and1135 detail builders; it does not add a packet family or change framing.
Historical Leaf/Crystal/Panacea flags use existing counters at offsets12/16/20.
That objective mapping is a Revora interpretation pending native acceptance,
not a verified original packet capture. Existing Begin and2032 interaction102
quit dispatch now protect3418 by refreshing authoritative receipt-derived state.
Private handler tests prove forged requests cannot complete/reset trial history
and that consumption plus game/database restart preserves completion. They do
not establish native presentation or production activation.


## Taoist Panacea item framing, 2026-09-27

The existing family1009 action4 dispatcher now routes owned3006016 to the staged
Taoist coordinator before generic item use. It requires32..128 total bytes,
declared body plus eight-byte seal, family1009, action4 and the existing client
seal. No wire field or packet family was added. This is source-contract reuse,
not new native capture evidence. Private direct-dispatch checks reject truncated,
length/family/seal-mismatched and oversized frames without consuming Panacea,
then verify one valid conversion and restart persistence. Direct UseItem also
requires the actual owned object, not a copied UID. Native6711 acceptance and
normal-startup activation remain pending.


## Pirate mission6868 receipt projection (2026-09-27)

The approved client Questinfo.ini entry792 identifies mission6868, type6,
minimum70, Pirate professions70-75 and IntentAmount=0. SHA256:
`f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14`.
Use the existing1134 quest-status family only; do not invent1135 counters for
this zero-intention mission. Status is a Revora projection of committed purchase,
flower and search receipts; completion requires a saved final-map Heart reward.
The existing Begin and2032 quit handlers must not accept client-authored progress.
Confidence: installed numeric identity verified; status semantics are explicit
Revora rules; native presentation remains unverified. No new wire layout claimed.

## Pirate skill-scroll item-use dispatch (2026-09-27)

Reuses the existing server1009 action4 item-use envelope used by Taoist Panacea:
owned item UID at8, action at20, declared length plus8 equals buffer length,
32..128-byte bounded buffer and final ClientSeal. No new wire layout is inferred.
Installed scroll3307432 maps to14680,3307433 to14720,3307434 to14710; mappings
are catalog-validated in the Pirate research record. Private packet fixtures
validate server dispatch only; genuine6711 scroll-use capture remains pending.

## Pirate bottle and Heart item-use dispatch (2026-09-27)

Extends the existing guarded1009 action4 route to3307448 bottle and3307449 Heart;
no additional packet fields or layout are introduced. Exact owned item references
are required by the direct fallback. The bottle runs the existing search
transaction; Heart runs the existing rapier-first conversion transaction.
Native right-click acceptance remains pending; synthetic dispatch is tested.

## Private Pirate chart travel (2026-09-27)

The existing bounded1009 action4 handler recognizes chart3307443..3307447 only
after explicit private InitializeTravel. No new wire layout or packet family
is introduced. Normal startup does not enable travel. Synthetic first-map entry,
NPC search, persisted location and return are tested; native chart interaction
and collision are not verified. Entry retains the item; confirmed search uses
the existing atomic search receipt transaction.

## Private Pirate material merge (2026-09-27)

Reuses existing1009 action48 and UID/dwParam item identities from ItemUsage;
no new packet family is inferred. Bounded envelope and seal checks precede a
transactional chart/Water merge while the Pirate feature is enabled. Synthetic
server dispatch is checked separately from pending native merge acceptance.

## Private Pirate material split (2026-09-27)

Extends the existing guarded1009 stack route to action49: UID selects the owned
stack and dwParam is the requested split quantity, matching the legacy handler.
No new fields are introduced. Debit and inserted split item share one transaction;
native split interaction is still pending.

## Team response framing, 2026-09-27

The two-account loopback SRP/Game-DH probe reaches ordinary invitation acceptance,
rejects unsolicited/replayed acceptance and unauthorized kicks, then fails on the
leader's authorized kick: packet1023 declares24 bytes but its expected eight-byte
server seal does not follow that payload. Evidence: private gameplay SHA256
D57AE461F6A7BB17C76AAFA75ACD0713C78B13CDD9AD80E19CD5C8F26F9DAC8A,
redacted arena-process-team-wire.log, stage game-frame-seal-kind-1023-length-24.

Source confirms Team's constructor allocates36 bytes while declaring24;
GameState.Send writes the seal at buffer.Length-8 and sends the whole buffer.
Team.RemoveOwned constructs this response. Thus its seal starts at28 instead of24,
leaving four extra bytes in the stream. The received player request uses32 bytes
and is echoed successfully during Create, so that path concealed the defect.
Preserve the observed24-byte payload and allocate32 bytes for constructor-built
responses. Confidence: verified Revora source and authenticated synthetic TCP
failure, not newly verified native6711 packet semantics. No proprietary bytes or
credential payloads were recorded. The positive rerun is documented separately.

## Unsolicited inspection cancellation on attack, 2026-09-27

The authenticated Skill PK fixture receives a51-byte packet10010 with legacy
operation164 at offset24 after sending an ordinary attack. It cannot be decoded
as the existing Data.CMsgAction protobuf. Attack.Proc directly calls
DialyQuestsEffect.CheckCancelGuard after clearing both inspection flags, even
when neither was active; CheckCancelGuard constructs exactly this fixed-layout
packet. Record the prior activity before clearing the flags and send cancellation
only for an active inspection. This removes an unrelated legacy notification from
normal attacks, without inventing a new native164 layout. Active inspection UI
compatibility remains unverified. Evidence is source plus private synthetic TCP,
not a native-client acceptance claim; packet bodies were not recorded.


## Sign-in issuance request bounds, 2026-09-30

The existing Revora `MsgSignIn` constructor allocates 20 bytes with declared
length 12, identity 3200 and action at offset 4. The request handler now checks
a minimum 20-byte frame, maximum 64 bytes, declared length plus eight, identity
3200 and the shared client seal before reading the action. Its private fixture
uses that existing serializer and supplies the client seal. This is source and
synthetic-handler evidence, not verified native 6711 request framing. No new
protocol family is enabled; native display/request acceptance remains pending.


## Union creation framing and committed projection (2026-09-30)

Provenance: existing `Sistemas/Union/Union.cs` MsgLeagueOpt reader and
`Sistemas/Union/UnionPacket.cs` overhead serializer at `8fdf7f1f`, plus the current
private `tools/CheckUnionFormation.cs` handler probe. Confidence: source-derived
layout and private server-handler behavior; no new native6711 capture is claimed.
The existing action7 reader uses action at4, string length at19 and text at20.
The frame boundary is header length plus the existing eight-byte client seal.
Union dispatch now rejects truncated/oversized frames, inconsistent header length,
wrong packet ID or seal and non-current players. Creation bounds its text length
inside the payload and refuses blank, edge-whitespace and control-character names.
Other Union actions remain legacy handlers; this is not a complete office-family
security or interoperability acceptance claim.

Formation sends the existing overhead message fields using the existing protobuf
length-prefix/header encoding. Its separate committed projection restores runtime
and spawn-buffer fields and sends the packet without persistent setters. The
14 private checks cover malformed creation frames, ordinary access, replay,
transaction rollback, login and injected postcommit projection failure. Actual
client receipt/display and native UI submission remain pending.

## Union succession reuses the existing action14 fields (2026-09-30)

Source provenance: `Sistemas/Union/Union.cs` at `1ec2c496` reads the action as a
ushort at4 and successor UID as uint at10. The current implementation retains
those fields and existing response packet IDs/layouts. The private
`tools/CheckUnionSuccession.cs` supplies those existing fields with header length
and client seal, joins via action8, appoints core/guard offices via actions28/24,
and exercises transfer/replay/reload with ordinary players. Confidence is source
and isolated handler evidence, not verified native6711 UI submission/display.
No new packet family or client artifact is introduced. Atomic transfer replaces
only persistence/ownership behavior and uses committed office/spawn projection.

## Competitive Kingdom presentation (2026-09-30)

Source evidence: existing MsgLeagueOpt KingdomIcon uses its uint dwParam3; existing
spawn-buffer UnionType/OfficialRank and overhead protobuf fields carry membership
presentation. Competitive authority now drives those same fields after committed
results or expiry. Title/wings use the existing TitleStorage request/reply and
wardrobe persistence paths. No new packet ID, field layout or client artifact is
introduced.40 private handler checks cover earning, equip, successful old-ruler
visual revocation, reconnect and expiry;568 title regressions also pass. Confidence
is source and private server-handler evidence. Actual native packet receipt and
rendered6711 title/wing/badge acceptance remain pending.

## Newcomer integration: existing packet families (2026-10-01)

No packet layout is introduced or reclassified. Existing validated1102 Entire
requests can advance warehouse introduction only after owned-storage lookup and
a separate map1002/NPC8/range check. The existing ItemUsage shop route handles
a single pharmacy3 purchase atomically with tutorial progress. Generic1134 and
2032 quest-action acceptance is guarded for3478-3481; saved state projects the
journal. Four private server-handler phases pass, including restart/replay and
rollback. Native client behavior remains pending. See NEWCOMER_ACHIEVEMENT_PROPOSAL.md.


## Senior Training Pill server route (2026-10-02)

Item 3003126 now uses the existing sealed 1009 action-4 inventory request and
existing 2702 free/paid Jiang training path. No new packet layout, pill toggle,
or guessed Jiang action was added. The composed private harness dispatched
ordinary non-GM claim, item-use, training and restoration frames and checked
transaction rollback and game/PostgreSQL cold recovery. This is server-handler
evidence only: native 6711 activation of this particular pill is unverified.
The catalog describes an Ultra-rate increase without publishing numeric odds.
The independent 10% rescue of non-highest rolls is a documented Revora rule,
not an original protocol/odds claim. See WAY_OF_HEROES_SENIOR_PILLS.md for the
exact executable hash, earned reward counts, five test phases and open gates.


## Enlightenment1127 admission evidence (2026-10-02)

The existing Enlight serializer creates40 bytes, declares32, writes donor and
recipient UIDs at12/16 and uses the standard trailing seal. Admission now
validates that managed layout before accessing those fields. The private
production-handler fixture rejects malformed lengths, identity/seal spoofing
and invalid sessions without changing XP or points. This is managed evidence,
not a verified native capture. Same-map18-tile interaction distance is a Revora
policy. Official level90/20-level-gap evidence and remaining atomic/delayed
payout work are recorded in ENLIGHTENMENT_PROGRESS.md. Stage606 remains off.


## Enlightenment durable interaction (2026-10-02)

The existing managed1127 frame remains40 bytes (declared32 plus seal), donor
UID at12, recipient UID at16. Bytes4-7 are used as a same-day request dedup value;
no native nonce semantics or native compatibility is claimed. Managed ordinary
handler fixtures validate donor debit/XP/deferred entitlement transactions,
concurrent replay, five daily receipts, queued timer/reconnect preservation,
midnight reset, rollback, interrupted installation and cold recovery. The XP
amount is a documented Revora choice: immediate and deferred awards each use
one tenth of the EXP Ball captured at acceptance. The first queued award starts
at acceptance; login display restoration no longer rewrites saved minutes.
See ENLIGHTENMENT_PROGRESS.md for exact builds, scope and remaining gates.
Milestone606 remains disabled. No live/client release is part of this batch.


## Enlightenment EXP item credit and stage606 (2026-10-02)

EXP Ball and EXP potion consumption now saves10 internal enlightenment units
with the consumed item and XP/buff transaction. The official guide specifies
0.1 point for each such use; one accepted enlightenment spends100 internal units.
Stage606 follows the installed task31/one donation/open1101/three bound Meteors
definition and requires a non-legacy committed donor receipt. Private ordinary
fixtures exercise item use, donation, concurrent claims, normal reward upgrades,
rollback/interrupted installation and exact cold XP/receipt snapshots on build
465F0683E57CABBF5B9E5FF22A49D2B3AA60CC8941590553082EFB9D4E0C7FE7. No new wire
shape is introduced or native-client acceptance claimed. UnknownMan3825's
DragonBall-for-XP payment/XP/point transaction remains separate pending work.
See ENLIGHTENMENT_PROGRESS.md for scope and the official reference.


## UnknownMan exchange persistence (2026-10-02)

The installed NPC3825 at Market1036 (212,204) now routes its existing dialog
options through DragonBallExperience. Option0 quotes the level-based exchange;
option1 requires a current same-session quote and near-NPC eligibility. Exact
owned item payment, XP/progression, per-calendar-day receipt and50 enlightenment
units per consumed DragonBall commit together. Existing Revora1/2/10-ball prices
and XP bands are retained; their authenticity is not inferred from this work.
Private normal-dialog fixtures passed all three prices, concurrent confirmation,
refusals, rollback/interrupted installation, partial-stack use and cold restart
on7C702031D84FEC8BC56D46CE689DBDD028A55294E1317499EC02B9DC3F675867. No new
wire layout is introduced or native acceptance/live deployment claimed. See
DRAGONBALL_EXPERIENCE.md for references and remaining boundaries.


## Champion restoration evidence and stat prerequisite (2026-10-02)

The official2013 Champion guide describes weekday, level70+ matches; the official
2015 Kingdom War notice cancels the event. Revora's installed task505 is not
proof of later native event availability. A restored edition requires explicit
Revora scheduling/admission/result and interaction evidence. The existing2604
serializer remains managed evidence only; no new packet family is enabled here.
The one-based grade lookup into a seven-row zero-based cap table is corrected,
with restrictive invalid-grade fallback and the documented Apothecary exception.
A synthetic stat fixture checks actual equipped HP/Battle Power/subclass effects,
not match admission or milestone earning. Stage505 remains disabled. See
CHAMPION_ARENA_RESTORATION.md for source links and the implementation sequence.
Enlightenment's separate new fixtures prove allowance/overflow/day-boundary
rollback and cold recovery; see ENLIGHTENMENT_PROGRESS.md. No live/client release.


## Champion ordinary NPC controls (2026-10-02)

The existing2600-2604 input layouts still lack native acceptance evidence.
Their request dispatcher is now inert, including direct2604 action calls;
client-provided bytes cannot register, accept, cancel or query those legacy
handlers. Existing output serializers are not claimed newly verified.

The Revora restoration uses established NPC2031/2032 handling: a town registrar
703001 and owned-arena steward703002, with current-session, NPC identity,
distance and invitation/match checks. Registrar2032 requests have bounded
length/input and client-seal validation. The ordinary message-box path remains
optional under AutoInvite.Enabled; its reply captures opponent, round and day
and revalidates the current invitation. Registrar acceptance works with that
optional popup disabled. Managed packet/harness evidence and native acceptance
are distinct; the latter remains pending. No new packet layout is inferred.


### Way of Heroes main-stage claim restoration (2026-10-03)

Existing2830 source dispatch reads mode at byte4 and a four-byte page/stage at
byte5; modes1/2 distinguish substage/main-stage claims. This restoration uses
that existing layout and validates mode2 pages1-10 before arithmetic; mode1
cannot address a main-stage ID. Progress is read from saved server state, never
from the claim packet. Existing length/seal/session validation remains in force.
The response2831 already carries StageInfo.Done at byte8 and is restored from
main-stage payout receipts. This is source/private-harness evidence, not new
native6711 packet verification. Native acceptance remains pending. The hardcoded
2832 summary is not evidence of actual completion and requires a separate audit.

## P7 choice item-menu routing (2026-10-03)

Source-confirmed: `PacketID.NpcDialog` is 2032; `NpcRequest` reads NPC ID at8,
option at14 and input length at17. Ordinary fallback dispatch requires a world
NPC, which cannot represent item3006233. `P7SoulChoicePack` already establishes
an owned, session-bound offer but requires an early dialogue route before that
fallback. Route only the active P7 menu, validate declared length20..120, exact
frame size including the8-byte seal, input extent and seal before deserializing.
No new wire layout is inferred. Native6711 menu rendering remains unverified;
synthetic dispatcher and cold persistence evidence is recorded separately.


## 2026-10-04: JiangHu training comparison ordering on production

Owner reported that clicking Train no longer retained the previous skill in the
comparison. JiangTraining sent the full newly rolled star list (JiangHuStatus)
before the existing2702 training delta. The historical train handler sent the
delta without first replacing the full list. Training now retains that sequence;
restoration still sends full accepted status. No packet layout or opcode changed.
The server continues to snapshot MyOldStar before the roll and uses its existing
restoration transaction. This does not add cross-login persistence for MyOldStar.

Evidence: Game/Features/JiangTraining.cs and
Network/GamePackets/JiangHu/JiangHuCalculate.cs (PrepareTrainingRoll, SendStatus,
historical CreateRoll). Confidence: source-verified overwrite ordering; native
6711 comparison behavior still requires the owner's retry. Deployed to the main
production server; no separate database or client patch used. See
research/PRODUCTION_PERFECTION_JIANG_HISTORY_20261004.json.


## Wardrobe 3300 Wear selector ? production 2026-10-04

Native 6711 owner retry against production captured decoded metadata only:
action 5, owned item UID 59662147, field 3 = 9, server catalog ID 194395;
action 5, owned item UID 59658253, field 3 = 17, server catalog ID 200613.
Both requests passed player readiness and wardrobe lookup. Field 3 on these
Wear requests is the equipment slot, not the catalog item ID. Confidence: live
verified for action 5 garment and mount appearance. Existing item-ID/zero forms
remain accepted for compatibility. Ownership and actual type/slot are resolved
from the owned server item, never supplied by this selector. Other actions were
not captured and retain their existing selector validation. No raw packets or
credentials were recorded. Initial SQL-only checks did not cover this client
request mismatch; native switching acceptance remains pending the corrected build.


## Wardrobe displaced cosmetic inventory display ? 2026-10-04

Owner reports bag icons after native Wear swaps. Server source shows
UnequipCommittedCosmetic sends item-use action 6, also used by ordinary
unequip-to-bag. It does not clear the resulting bag icon when InWardrobe is
true. Main DB confirms the displaced mount remains inwardrobe=1, position=0.
Use the existing action-6 then action-3 inventory-removal sequence (also used
by Equipment.DestroyArrow) for wardrobe-owned displaced cosmetics only.
Withdraw subsequently sends the item's normal inventory record after committing
inwardrobe=0. Confidence: server-state/source verified; UI behavior of the new
cleanup sequence awaits native retry. No packet layout changes.


## Revora Relic acquisition ? 2026-10-05

No new packet family. Existing 6711 ConquerItem fields at offsets 127, 131,
135, 139 and 143 contain the five Relic attributes; ConquerItemTable already
persists them. Existing Relics.CreatRelicAttr and GameState.HandleRelic agree
on decimal value*1000 + quality*100 + attribute type. Source confidence only;
newly earned Relic native tooltip/stat acceptance is pending. The client
STR_HOW_TO_GET_XUAN_BAO says ancient Runes but neighboring XUAN_BAO labels refer
to Relics. DeityLand 10250's donor GameMap entry resolves to the installed
map/map/island-snail01.7z. The server GameMap catalog lacks this entry; it is appended there. Released
client 1.0.20 already has the route and 1.0.21 preserves it. See docs/DEITYLAND_RELICS.md.


## Native pickup frame size correction (2026-10-05)

Live approved-client pickup attempts from MrKaelos on map 1015 reached FloorItem.Process as packet 1101, total length 137 bytes, action byte 3, with server/drop coordinates matching and inventory count 1. PacketHandler had already verified declared framing and client seal. Repeated equipment requests were rejected by the managed-drop 128-byte maximum; ordinary gold uses a separate path. Support the observed 137-byte frame in addition to existing 32-128-byte frames, retaining exact declared length, packet ID and seal validation. No extra field meanings are inferred. Confidence: observed frame size; native successful collection still pending.


## Arena bot follow-up, 2026-10-05

Owner live 6711 test: bot inspection shows unknown class and Arena overlay remains after a completed win/return. Code evidence: Entity.ToReviewed6711PlayerSpawn omits profession fields, while existing CMsgPlayer maps first/second/current classes to protobuf fields 70/71/72. Bot-only spawn will include these existing schema fields. CleanupOwned resets server statistics but sends no existing ArenaSignup Match/MatchOff (2205, action 6, option 3). Add that existing message when clearing an owned fighter match. Confidence: source-confirmed omissions and existing schema/constants; native correction acceptance still pending. Do not describe the client response as verified until retested.


## Arena terminal scoreboard message, 2026-10-05

Owner confirmed action 6/option 3 alone does not close the in-match names/damage panel after returning to the city. The retained legacy Qualifier.Win terminal path sends ArenaSignup.Dialog2 (2205 action 10), winner option 1 and loser option 0, before refreshed statistics. Current durable cleanup had omitted that final message while retaining action 9 result notification. Restore action 10 during owned fighter cleanup after teleport, alongside MatchOff. Evidence is the local legacy terminal sequence plus owner negative acceptance; native confirmation of this correction remains pending.
