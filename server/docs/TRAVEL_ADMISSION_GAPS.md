# Travel admission review (2026-10-01)

## Frozen Grotto follow-up

Floor3 now passes ordinary admission and cold recovery. Floor4 remains
admission-unproven: the catalog hash below has no PortalExit destination2054.
Section1999 exit0 reaches7007, whose exit returns to1999. It also misspells
PortalExit6 as PortalExt6; Map.LoadPortals only reads PortalExit. No catalog
change is made here. Guardian tests start on floor4 and do not prove its entrance.
The private installed NPC table contains only Guardian7764 on2054 and no NPC
on1999 (read-only query on2026-10-01). Client GameMapEx.ini identifies1999/2054
as FrozenGrotto3/4, but does not supply their connecting route.

The official [Grotto guide](https://co.99.com/guide/quests/frozengrotto.shtml)
confirms floor3-to-floor4 progression but its retrieved text does not establish
exit coordinates. The [Missing Key guide](https://co.99.com/guide/quests/10expansion/missing_key.shtml)
confirms floor3/4 keys and Guardian admission to floor5. Both were checked
2026-10-01. Resolve the missing route from client/installed NPC evidence before
claiming full admission; a bounded numeric review does not rule out computed routes.

Current: the approved [Forgotten Realms expedition](FORGOTTEN_REALMS_EXPEDITION.md)
now passes ordinary admission, travel receipts, hunt/reward and fresh-process
recovery. The four admission-only exclusions below are closed. Map1363 was tested
with its staged floor; its coordinated index/floor release remains open.

## Historical pre-implementation review

Four supported terrains are withheld from the achievement career planning pool
until ordinary admission is demonstrated:

| Achievement | Map | Catalog destination |
| --- | --- | --- |
| 10723 | 1360 | Deserted Wild |
| 10724 | 1361 | Flame Plain |
| 10725 | 1362 | Death Cave |
| 10727 | 1364 | Corpse Forest |

The private runtime's `database/Portals.ini` has SHA-256
`4a9a681103915ca232be7c98defd69cf08ae5639205822967ee678985ea0aaa9`.
It has no sections or entry/exit map references for any map 1360 through 1364.
`Game/Map.cs:LoadPortals` selects portal sections by BaseID. The existing
TravelProgress checks require these worlds' actual map and terrain identities;
arbitrary dynamic instances cannot substitute for them.

A numeric-reference review at source `0cc2efda` across Game, Network, Database,
Server.cs and Npcs.cs finds the travel predicates, unrelated NightDevil spell
1360 and NPC mesh 1360, but no direct admission to these maps. This bounded
review does not prove that no computed destination, item or other data-driven
route could exist. Hence the classification is **admission-unproven**, not
permanently unavailable. Terrain availability alone does not prove access.

Map 1363 / achievement 10726 remains separately unavailable under the existing
staged-terrain and unresolved admission classification. It is not subtracted
again. The four new gaps reduce the unproven planning pool from 328 to 324,
leaving four above Rising Star's 320 requirement. Source coverage stays 334/337
and the ceiling after known unavailability stays 331. No compatible career,
new earning path, native acceptance or deployment is claimed.

To close each gap, identify an evidence-backed ordinary entrance and return,
check its eligibility/cost and loaded population, then earn the travel receipt
through that route and verify persistence. A GM teleport or a freely invented
entrance cannot establish restoration of the original quest. A documented
Revora replacement needs an owner rule decision.
