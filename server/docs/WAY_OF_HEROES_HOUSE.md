# Way of Heroes: house and Fine Furniture Pack

Implemented and privately validated 2026-10-02; not deployed live.

Installed stage704 (task35, second rebirth level15 opening) asks for a house and
awards FineFurniturePack3005131. Qualification requires a saved first construction
receipt and a currently owned level1-5 house. Buying a certificate alone does not
qualify. An unrecorded legacy house is not backfilled as purchase evidence.

The installed client itemtype.txt describes one random bound piece of furniture
but does not specify the pool or odds. Revora uses equal chances among the twelve
existing decorative furniture types721177-721188 (Lamp through End Table), worth
2,500 CPs each at the furniture merchant. This is a documented Revora pool, not
verified original odds. It excludes the storage box. The result is permanent and
bound. Existing decoration placement requires a level-two-or-better home; a
level-one owner can retain the reward until upgrading.

The existing item-use transaction consumes one owned pack and inserts one result
plus a revora_fine_furniture_pack_uses receipt atomically. Existing ownership,
capacity, malformed-frame, trade, binding and session checks remain in effect.
The milestone has its own unique claim receipt; no GM or account allowlist is used.

## Private process evidence

CheckCandidateHouseClaim.cs supplies level130, second rebirth, 100,000 CPs and
1,000,000 silver to one ordinary non-GM character. These are fixture prerequisites,
not naturally earned currency or progression.

- [x] No house and certificate-only states refuse704 without state changes.
- [x] The ordinary House Admin sells the first certificate, then consumes it and
  300,000 silver to construct the house. Eight concurrent milestone requests
  produce one bound pack and one claim receipt.
- [x] An out-of-range controlled random choice refuses without consuming the pack.
- [x] The character buys the normal level-two upgrade before decoration placement.
- [x] All twelve deterministic pool choices open through the normal item-use
  packet, with eight concurrent requests per pack and subsequent replay. One pack
  is earned from704; eleven additional packs are supplied to cover the whole pool.
- [x] All twelve bound results place through normal item-use/placement packets.
  The first eleven are removed through the ordinary owner removal packet to
  respect room capacity; the final End Table stays placed. Place/remove replay
  preserves exact scoped snapshots.
- [x] Full game/database restart preserves house/builds, milestone, items, all
  pack receipts and placement/removal records exactly. The remaining furniture
  actor reloads into the owned house; another704 request changes nothing.

Game SHA256:
5F843E4D0A3CEA8885828CC2C96790F5CCBD4BFA882859B8BA89AD9FABC7B534.
Build completed with existing warnings using the committed-source overlay that
excludes unrelated owner WIP. Initial harness compilation exposed source-list
argument formatting and a missing namespace; these were corrected before gameplay.
The first gameplay fixture wrongly tried decorating a level-one house. Existing
placement correctly refused. The revised fixture resumed the persisted house,
claim and furniture reward, bought the level-two upgrade and passed earning and
cold reload without resetting the character. No production capacity rule changed.
This batch does not claim a new rollback-fault test, native-client acceptance,
original random odds or live deployment.
