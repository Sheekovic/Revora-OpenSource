# Newcomer achievement10930: reviewable Revora proposal

Status: owner approved the recommended edition with "use all recommendations".
Implementation and private ordinary-player validation now pass. Live deployment
and native acceptance remain pending; historical inspection below records the
pre-implementation state.

## Verified local numeric evidence

The hash-reviewed6711 Questinfo.ini contains one record each for3478 through3481.
Their listed predecessors are0,3478,3479,3480 respectively. Every record has
category1, level range1-140 and profession/sex filters0. Numeric NPC/map/position
fields are retained in the generated
[prerequisite audit](research/QUEST_PREREQUISITE_AUDIT.json); client prose/assets
are not copied into the repository. This is a catalog observation, not verified
native prerequisite semantics or a proof of live NPC availability.

| Mission | Begin NPC | Finish NPC | Listed predecessor |
| --- | --- | --- | --- |
|3478|5673|5673|0|
|3479|5673|8|3478|
|3480|8|3|3479|
|3481|3|2001|3480|

All listed NPC destinations are on map1002. This candidate avoids the absent
predecessors and incompatible class branches in the older mixed recruit records.
It does not establish that the original achievement requires these four missions.
No existing handler for these mission IDs was found in the reviewed feature/NPC
source. Database NPC availability and native shop/warehouse interaction must be
verified before implementation can claim ordinary access.

## Approved Revora edition

Require these four mission completions in order, once per character, and record
10930 only in the final successful completion transaction. Preserve progress
through rebirth and class changes; do not infer completion from level, current
map, a forged client quest status or merely opening the quest panel. Keep the
installed level1-140 admission range and all-class/all-sex access. Existing
characters may complete the introduction once under the same conditions.

Grant one bound4030101 rune at the end, using the existing owned quest-reward
transaction. This satisfies the owner's rune reward category without a daily P7
or a new currency. Final progress, achievement evidence, item and reward receipt
must commit together. A full bag must leave a claim pending; repeat confirmation,
reconnect and concurrent requests must never award again. Use short original
Revora dialogue matching the owner's game-world style.

The warehouse and pharmacist retain their normal services. Determine the actual
client interaction seam before adding the introduction; do not turn their shops
or warehouse into dialogue-only NPCs. Never modify starter-pack contents, initial
spawn, or the old starting map as a side effect of this proposal.

## Required implementation and acceptance sequence

1. Check the four intended NPC identities, maps and existing handlers; implement
   narrowly scoped quest hooks without breaking their services.
2. Persist monotonic owned completion receipts and project the installed four
   mission IDs through the existing quest journal. Finalize10930 and the rune
   atomically; reject skipped, remote, stale and forged progression.
3. Validate with an ordinary character: ordered route, full-bag retry, duplicate
   and concurrent confirmations, saved progress/claim after process restart,
   class/rebirth retention, and unchanged warehouse/shop access.
4. Update producer/reachability inventories only after the implemented path passes.
   Genuine6711 navigation, dialogue and reward presentation remain a separate
   native acceptance gate; publication does not authorize live activation.

The updated audit has tests for missing/duplicate missions and extra prerequisite
branches. Even a valid candidate always reports original_aggregate_verified=false.

## Loaded-service inspection (2026-10-01)

The private full-server newcomer-inspect phase ran on
58A61FDE21CA31128BDF7534DBCAA0F983787854379E6321CACB27AD8EE01D46.
It inspects loaded map objects and both service context and the ordinary shop
catalog; it does not seed quest progress or exercise a native client.

| NPC | Loaded1002 state | Required implementation consequence |
| --- | --- | --- |
|5673|Absent|Restore the intended introduction talker at432,397.|
|8|406,312; mesh80; type3|Keep the warehouse type and current position.|
|3|400,331; mesh30; Shop;20 Gold-priced catalog items|Keep the ordinary silver shop and current position.|
|2001|Absent|Restore the intended captain talker at439,464.|

The warehouse/pharmacist offsets from Questinfo coordinates are within the
existing six-tile quest proximity radius. Their current service identities must
not be replaced with talkers to make a synthetic dialogue test pass. An initial
EShop-only inspection omitted the ordinary ShopFile catalog; the corrected
inspection confirms pharmacist stock, so no stock repair is required.

Local npc.ini identifies model506 as KungfuMaster and174 as TCCaptain. These are
numeric local catalog observations; mesh/rendering must still be checked in the
approved native client. No proprietary catalog contents are committed.

Warehouse.Process handles validated1102 requests, but TryStorage alone checks
ownership/storage aliases and does not prove proximity. Any introduction hook
must independently verify map1002, actual NPC8 and range before credit. The
pharmacist uses ItemHandler's ordinary ShopFile path. No dedicated server-visible
shop-open request was established by this bounded review. Do not claim opening
the shop completes a quest merely because a server helper can call a dialogue.

Next implementation: restore the two missing talkers; establish explicit owned
service-interaction hooks for the middle steps; protect3478-3481 from generic
client quest mutations; add persistent journal projection and atomic final rune
claim. The exact pharmacist interaction remains an engineering decision to
resolve before calling the path usable. Original aggregate semantics, native
acceptance, quest earning and final reward remain unverified. Inspection helper
compiles with warnings as errors and the private database is stopped.

Local model-catalog evidence: ../../6711/ini/npc.ini SHA256
b06db1e2484992a1b58eabd7a3d884da07ac728a4a183d0fd18f2dccd5b20497.

## Implemented Revora introduction (2026-10-01)

NewcomerIntroduction restores talkers5673/2001 with the reviewed models and
positions. Warehouse8 and pharmacist3 retain their types, positions and stock.
All ordinary living characters level1-140 may complete the chain once:

1. Accept Kungfu Master's introduction (3478).
2. Open personal Twin City storage near warehouse8 (3479). Remote storage aliases
   or opening storage elsewhere do not advance it.
3. Buy one stocked supply from pharmacist3, within six tiles (3480). This is the
   documented Revora interaction: shop opening alone is not credited. The normal
   catalog silver price applies. The one-item purchase, debit and stage commit
   together; other purchases retain their existing shop route.
4. Speak to captain2001 and accept one permanent bound4030101 rune (3481).
   The reward item, final stage, owned reward UID and10930 commit together.

The character-keyed table has no rebirth/class reset key. Journal projection
comes from saved stages; generic quest acceptance/dialogue mutations for these
IDs are intercepted and cannot invent completion. NPC requests verify identity,
range and current ordinary session; state/payment changes hold the inventory
lock and database character lock. An uncertain failure requires reconnection
through the existing persistence-failure policy. A full bag preserves stage3.
No daily P7, starter-pack or initial-spawn change.

Private phases newcomer-start/fault/finish/reload pass on
1DF810722999A045468702D0CC0F6168C2AD1221EBFF6E479F375B7113CEC6A3.
They prove early captain refusal, repeated acceptance, remote-warehouse refusal,
normal warehouse request, stage2 cold recovery, priced pharmacy purchase,
full-bag deferral and final-write rollback without a stray rune. Another process
claims once; restart retains the same owned bound rune, completed journal and
10930 packet bit. Eight parallel completed-claim replays preserve one rune.

Fixture4294907301 uses a supplied ordinary level100 profile and guild-independent
entry. Supplied filler items exercise the full-bag case and are preserved in
storage. It is not the retained career and does not add to that career's count.
Class/rebirth independence is structural here; an actual reset during this quest
was not exercised. The private DB is stopped. Build has pre-existing warnings;
helper compiles with warnings as errors. Native rendering, navigation and UI
acceptance remain pending; these tests use server handlers and controlled travel.
