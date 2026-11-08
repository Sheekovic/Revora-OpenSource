# Way of Heroes: total equipment bonus

Implemented and privately validated 2026-10-02; not deployed live.

The installed task22 milestones now use the sum of valid equipped +levels:

| Stage | Total +levels | Bound reward |
| --- | ---: | --- |
| 602 | 15 | +2 Stone730002 |
| 706 | 50 | +3 Stone730003 |
| 802 | 70 | +3 Stone730003 |
| 902 | 100 | 2,000 Chi Points pack3005128 |
| 1007 | 120 | 3,000 Chi Points pack3005129 |

## Documented Revora definition

Following the owner's earlier instruction to use recommendations, the sum uses
the active equipment bank's combat slots1,2,3,4,5,6,8 plus shared fan10, tower11,
steed12, crop18 and wings19. This resolves the installed wording ?all equipped
gears?; it is a Revora interpretation, not a verified historical slot list.
Cosmetics, inventory, warehouse, inactive-bank, suspicious, temporary and wardrobe
items do not contribute. Each saved item must have a valid category/slot, match
the live equipped identity and +level, be a single permanent owned item and have
+level0-12. Duplicate occupied slots require reload.

The existing task23 single-item calculation shares the same validation and keeps
its original maximum-item semantics. Task22 sums those +levels instead. The normal
opening gates, bound output, stone grade metadata and atomic unique claims remain.

## Private process evidence

CheckCandidateTotalBonus.cs uses five ordinary non-GM characters. Each is supplied
with level130, second rebirth, Warrior stats/proficiency and a valid equipment set
whose total is exactly one below its threshold. These starting +levels are fixture
prerequisites, not claimed naturally earned composition history.

- [x] All five below-threshold claims refuse with unchanged snapshots.
- [x] Normal composition consumes a supplied bound material to earn the final
  +level: +1 boots become +2 for the first four, +11 boots become +12 for120.
- [x] Removing the qualifying boots to the bag refuses. Switching to the empty
  alternate combat bank refuses despite shared talismans/steed remaining equipped.
- [x] Eight concurrent requests per threshold award exactly one correct bound item.
- [x] The three stone rewards are normally consumed into supplied +0 boots, which
  become +2/+3/+3 with zero remaining progress. Composition replay changes nothing.
- [x] The two Chi packs are normally consumed into2,000 and3,000 saved Chi Points;
  item-use replay cannot grant twice.
- [x] Full private game/database restart preserves exact scoped gear, composition,
  reward, Chi-use and milestone snapshots for all five characters; repeated claims
  change none.

Game SHA256:
19509980643112A4A7F2C2D5F56A9696C3ABFBEE16582CBC86AA46580CDBCDBA.
The first compile caught a misplaced local accumulator declaration, corrected
before candidate execution. Build then completed with existing warnings using the
committed-source overlay excluding unrelated owner WIP. Both process phases passed
on the first gameplay execution. Shared fan/tower/steed are exercised; crop/wing
inclusion is implemented but not separately equipped in this fixture. Native-client
acceptance and live deployment remain pending.
