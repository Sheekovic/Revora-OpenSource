# Exploration achievement regions: 6711 evidence

2026-09-15, baseline `3a09c46`. This checkpoint establishes numeric region facts
and a tested matcher. It does not enable travel completion producers.

## Source and confidence

Approved executable: `6711/Env_DX9/Conquer.exe`, SHA-256
`3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60`.
The top-level `6711/Conquer.exe` is a different 41-byte artifact and was rejected
by the hash gate; it was not executed or used as native evidence. All examination
here is static. The existing read-only Ghidra program is
`research/isolated/ghidra-6711-account312/Account6711/Conquer.6711.original.exe`.

Installed `6711/ini/region.ini` SHA-256:
`939c68eb8a7d00b6a21adbf494ab421140b8f7b7f139f5eb7a2c9645ef404618`.
Achievement catalog SHA-256:
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.

The region file contains 93 entries of types 27, 28 and 29. Cross-checking their
numeric achievement IDs against the actual 337-entry catalog leaves **76 regions
for 46 active achievements**. Seventeen referenced IDs are absent from the
installed catalog: 10615, 10616, 10623, 10624, 10627, 10628, 10630, 10704, 10705,
10706, 10707, 10722, 10729, 10730, 10731, 10732 and 10811. They are not revived
from stale region data. All three type-29 entries refer to these obsolete IDs.

Only numeric interoperability facts are retained in
[TRAVEL_REGION_FACTS.csv](research/TRAVEL_REGION_FACTS.csv). Proprietary region
labels, executable contents and decompiled implementation stay outside Git.

## Native evidence

| Address | Statically confirmed behavior |
| --- | --- |
| 009a1738 | Reads `ini/region.ini`; parses map, type, rectangle, text and auxiliary numeric fields into a region record |
| 009a0ee9 | Routes region types 27 through 29 to the dedicated achievement-aware region query |
| 009a023a | Tests `x >= left && x < left + width`, likewise y; skips a matching region whose achievement bit is already set |
| 007b9fbb | Looks up an achievement bit from the numeric ID using its decimal group/bit components |
| 009b34ee | On changed map coordinates, examines region types 1 through 35 and invokes the entry callback when a type changes to present |
| 007a40d3 | For types 27 through 29, queries the matching region and dispatches the achievement ID stored at region offset 0x18 |
| 007bc99c / 007ba94e | Dispatches through the achievement manager; buffers before readiness or forwards an uncompleted ID |

Private logs are `obj/chi-refill-validation/travel-region-*.log`. These facts
verify the spatial predicate and local entry detection. The final outbound
request builder has not been retraced here. Revora already rejects client-authored
1136 action-2 completions; that restriction must remain in place.

Types 27 and 28 are not complements. They use identical rectangle tests.
For example, (381,209) on map 1002 lies in both the Twin City and Wind Plain
achievement regions in this installed edition. General town-name or protection
regions use other types and different coordinates; substituting those regions
would change achievement requirements.

## Completed prerequisite

`TravelRegions.At(map,x,y)` uses the 76 reviewed numeric rectangles, preserves
file order and deduplicates IDs where several rectangles describe one place.
It accepts no player or packet and performs no persistence. It therefore returns
candidate IDs only, not proof that a player visited them.

The isolated build and independent C# fixture executable pass **129 checks**:
lower/upper edges, city/countryside overlap, altar unions, village overlap,
all active IDs, each of the ten Moon Platform map variants, whole-map edge 999,
unknown maps, ushort extremes and excluded obsolete-map entries. The fixtures
use explicit expected destinations and boundaries rather than loading expected
answers from the runtime table. No database or client process is started.

## Next implementation and acceptance

- [x] Recover installed numeric IDs and rectangles; reject obsolete catalog references.
- [x] Verify the native rectangle and entry detection behavior statically.
- [x] Implement and test the pure candidate matcher.
- [x] Record candidates only from accepted server movement, teleport or world-entry observations with canonical ownership and valid terrain.
- [x] Preserve brief crossings; do not rely exclusively on a thirty-second achievement refresh.
- [x] Atomically persist visits and completion evidence; reject missing writes and replayed/client-authored completions.
- [ ] Audit ordinary-player portal/NPC/quest access to each destination. A matching map ID does not prove its earning route is available.
- [ ] Test valid walking, jumping and travel, invalid movement, concurrency, rollback, reconnect and immediate achievement packets with non-GM characters.
- [ ] Confirm region notices and achievement display in genuine 6711, then deploy a coherent runtime batch.

The region IDs must match actual world identity. Do not award a static-map
achievement merely because an unrelated dynamic instance has a similar name or
base floor. Where a requested destination is unreachable, restore its actual
travel/quest route or leave it explicitly incomplete.

The geometry prerequisite left the source inventory at 110/337. The runtime
integration below adds 46 producer calls, making the source upper bound 156/337;
it does not prove 46 accessible destinations. No live deployment, public patch,
website publication or client modification occurred.


## M6.45: accepted travel visits

`TravelProgress` runs after accepted walking, two-step movement, bounded jumps,
teleports and authenticated world entry. It requires a loaded, living, unfenced,
canonical ordinary player and the canonical map with walkable terrain. Two reviewed fixed world aliases retain
their logical identity independently of terrain ID. Monster
movement and unrelated dynamic copies cannot grant player visits. Teleports
retain their resolved destination map before sending updates.

The first visit and all overlapping achievement completions commit together in
`revora_travel_visits` and `revora_achievement_evidence`. The transaction locks the
character row and checks that both writes exist before committing. The session
cache advances only after commit and achievement refresh; reconnect reconstructs
client state from durable evidence. A brief crossing remains earned after leaving.
There is no reward payment, inventory mutation or client-authored completion.

Automated coverage includes accepted walking/sliding/jumping/teleports, the real
Desert-to-Mystic-Castle portal, forged remote portal coordinates, ineligible
sessions, wrong map identity, concurrent replay, suppressed writes, ordinary
character save/reload, and immediate achievement state. The world-entry hook is
exercised as an accepted server observation; full network login and native client
display remain separate acceptance gates.

### Destination gaps

Read-only comparison with the installed `public.maps` table and terrain files:

| Achievement | Intended map | Installed gap |
| --- | --- | --- |
| 10718 | 1219 | Resolved in M6.45a: approved logical alias of terrain 1212 |
| 10719 | 1217 | Resolved in M6.45a: approved logical alias of terrain 1075; Potency Hunt is unchanged |
| 10726 | 1363 | Map table entry exists, but the terrain file is absent |
| 10801 | 1926 | No map table entry or terrain file |
| 10802 | 1927 | Terrain exists, but no map table entry |
| 10808 | 1806 | Terrain exists, but no map table entry |

Forty static regions and two reviewed fixed aliases have an installed walkable
point. This terrain test is not an admission
test. The installed portal graph links Twin City to 1000, 1001, 1011, 1013, 1014,
1015, 1016, 1020 and 1036; the Desert-to-Mystic-Castle and 1214-to-Joint-Canyon hops have additional
ordinary-player dispatcher tests. Other quest/NPC access, map
restoration and all native presentation checks remain unfinished. Player houses
use other base maps and are not substitutes for Moon Platform maps 1100-1109.

Next: restore evidence-backed missing map content and admissions without changing
Potency Hunt, then test each ordinary-player route and the genuine 6711 display.


Validation: 72 travel integration checks and 129 region matcher checks pass on
staged server SHA-256
`7579FF639F8E474886A30D65C37CBEBE9AF6780B6117990088B0BEE3BEB026B6`.
Before the final player-only movement guard, the same batch passed 74 progression,
91 title, 34 Sky Pass, 25 Snake Islands, 43 Labyrinth, 164 Dis City and 179 cosmetic
ownership checks. The final travel suite explicitly verifies the added guard.
Three producer-audit tests and the 337-row inventory check pass: 814 checks total.
Fixtures use only the disposable validation schema; installed map metadata and
terrain are read-only inputs. No production character or map data was changed.


## M6.45a: fixed logical map aliases

The first runtime pass conservatively required logical map ID to equal terrain
ID. Further approved-client analysis disproved that requirement for fixed world
aliases. Native 1110 field 1 selects region IDs, while field 2 follows a separate
terrain-ID setter; see [the protocol evidence](PROTOCOL_RESEARCH.md#logical-travel-identity-and-approved-static-aliases-2026-09-15).
The service now accepts only the explicit pairs 1217/1075 and 1219/1212 in addition
to equal-ID static maps. Wrong floors and arbitrary dynamic copies are rejected.
The original 46 producer identities are unchanged; this resolves two runtime
identity gates without remapping the world or changing Potency Hunt.

The 6711 GameMap.dat does not independently list 1217 or 1219; it lists terrain
1075 and 1212. GameMapEx.ini separately identifies the logical destinations.
Hashes: client GameMap.dat
`86a54928a9682617371542cdb5a72707c799b92a5c820e3581183adaa94f32b0`;
server GameMap.dat
`d1f35d6dc3dc0d1958a2aa612c4cdc988ed693636d89ad93078388e81cd23c19`;
client GameMapEx.ini
`64ce044b2eb7ad15e980d91a5834f19d5ccc5dee2d3022cacb72e7a99833af7f`.
Only numeric facts and provenance are retained in Git.

Final alias validation: 80 travel integration checks, 129 region checks and
3 audit tests; staged server SHA-256
`53F891CF0D608F4E33B8C6D56463323D6938E64D4CBD09BCE31C8197C972DC8A`.
The four unresolved setup/terrain identities are 1363, 1926, 1927 and 1806.
DMaps.Load can construct 1927 and 1806 from GameMap.dat even without map-table
metadata, but this does not establish healthy travel: status emission and
same-map teleport fallback consult that missing metadata. Their admission/status
routes must still be repaired and tested. No production deployment occurred.


## M6.45b: garden and Grotto admission repairs

Runtime metadata and ordinary NPC/portal routes now restore Orchid Garden and
Frozen Grotto 2. Both immediate visits and garden reconnect persistence pass.
See [destination repairs](EXPLORATION_DESTINATIONS.md) for scope, hashes, 627
validation checks and remaining work. Forty-five exploration regions have
supported terrain/setup, including a reviewed named-floor equivalence from
server world 1762 to the catalog's first-floor requirement. Destination 1363
remains unresolved.
The source producer upper bound stays 156/337. No deployment occurred.


## M6.45c: remaining terrain candidate

Map 1363 has a private, reproducible terrain/index repair derived from its
reviewed existing archive. All 313,600 cells agree with Revora's runtime floor
behavior. See [stage evidence](GLOOMY_JAIL_STAGE.md) for 20 passing tests, exact
hashes, preserved duplicate index entries and the unchanged admission/native
acceptance gates. The stage is not installed; current runtime support remains
45 regions and source coverage remains 156/337.
