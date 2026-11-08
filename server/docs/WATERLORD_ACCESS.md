# Adventure Islands and the complete rebirth-material journey

M6.54a-c, 2026-09-16 UTC; baseline `b60365f`. Completes the automated ordinary
player route from a purchased Pickaxe through seven mined gems, island travel,
Waterlord Clean Water, return travel and Heavenly Stone crafting. Final rebirth
and genuine-client acceptance remain open.

## Evidence

The official [Adventure Areas NPC guide](https://co.99.com/guide/npcs/adareasnpc.shtml)
identifies Old Explorer, Grandpa and Boatman, with the latter two providing
island travel and departure. The official [first-rebirth quest](https://co.99.com/guide/quests/q_reborn.shtml)
places the Clean Water source in the Adventure Islands and requires seven normal
gems. The server's existing handlers, installed NPCs, map profiles and portals
establish the concrete Revora route. Existing server fares and placements are
preserved; this does not claim that every historical official edition used them.

Read-only database inspection:

| Actor | Installed identity and location | Existing service |
| --- | --- | --- |
| Guild Controller | NPC 380, map 1002 (319,325) | Option 1 enters public guild-map transport area |
| Adventure transporter | SOB NPC 9884, map 1038 (348,320), type 2 | Option 1 charges 1,000 silver and enters 1216 |
| Old Explorer | NPC 300655, map 1216 (3,481) | Option 1 enters 1210 for free |
| Grandpa | NPC 300653, map 1212 (381,32) | Option 1 crosses to logical map 1219 |
| Boatman | NPC 300654, map 1219 (381,32) | Option 1 returns to Twin City for free |
| Waterlord | Family 8500, map 1212, existing spawn center (517,732) | Existing reviewed Clean Water source |

The transporter is in `sobnpcs`, not `npcs`; `Map.LoadNpcs` loads both into the
ordinary NPC dictionary. Its omission from the first NPC-only query was not
evidence that the route was absent. No GM, guild membership or winning-guild
ownership is required by this existing transport route.

Ignored inspector `obj/chi-refill-validation/WaterlordAccessInspect.cs` is a
reviewed x64 Revora/Npgsql read-only helper. Executed SHA-256:
`42112104822191A621877C09A1FBB6025B39B6018B0F1BF67B271E9AAD6C9CAE`.
It reads protected configuration, opens a read-only transaction, and prints only
relevant public NPC/map definitions. It does not change live data or print secrets.

Terrain SHA-256 evidence:

| Physical terrain | SHA-256 |
| --- | --- |
| 1210 | `EEA1BD2B9982A4E3E34BE7C3EEB9B466233E72274D7627DD7577D6A78DAEACC9` |
| 1211 | `BE082832754C344496A917D3427E06B2A9A9864CED4B068C8756AAD4511DF55F` |
| 1212, also used by logical 1219 | `4754EBF90CE69974FCE5984710FFA14B2D2ADD62C7CD12C2285E8852BC86C1BC` |

Installed `Portals.ini` SHA-256 remains
`4A9A681103915CA232BE7C98DEFD69CF08AE5639205822967EE678985EA0AAA9`.

## Fixes

`AdventureTravel` protects the transporter's existing 1,000-silver payment with
the loaded purse preimage, owner lock and persisted source-map check. The debit
and resumable passage commit together. A successful transition to 1216 consumes
the passage through a database trigger in the map update transaction. Rejected
map writes and silently suppressed passage-consumption writes leave one pending
paid trip; reconnecting players may return to the transporter and resume without
another charge. Later intentional trips pay normally. Cumulative trip count and
latest payment occupy one `revora_adventure_passages` row per character.

All four restored NPC handlers enforce their actual map/profile, proximity,
canonical loaded living session and absence of trade, vending or poker. Target
maps must exist with the expected physical profile and valid nearby terrain.
The existing free services remain free. Sereth and the island NPCs use brief
world-facing directions rather than implementation details.

Two installed destinations were blocked:

- The 1211-to-1212 portal specified **(1017,1294)**. The accepted nearby landing
  is **(1016,1293)** on the current terrain.
- Grandpa's map-1219 landing specified **(448,272)**. The accepted nearby landing
  is **(450,269)**, preserving logical 1219 and physical terrain 1212.

The service chooses the nearest valid player/monster-free tile within the
existing bounded landing search. Island portal actions originating on physical
1210, 1211 or 1212 validate destination identity and landing before teleporting.
No client binary, installed portal file, map terrain or NPC database row changes.

## Validation

Isolated legacy Debug/x86 build passed. Server SHA-256:
`FC899193945895D8088EBC2F18D43F144E5D5512CD2A926BA5B55150ED2F381E`.

`Verify-PotencyHunt.ps1 -AdventureOnly` passes **169 checks**. It extends the
mining suite with one ordinary, unguilded character carrying 2,200 starting
silver. The player buys a 1,000-silver Pickaxe, pays the 100-silver mine fare,
equips the tool, mines all seven gems, pays the existing 1,000-silver adventure
fare, hunts and picks up Clean Water, returns through Grandpa and Boatman,
and crafts the stone. The remaining 100 silver, equipped Pickaxe, Heavenly
Stone, mining totals, craft record, paid passage and four island visit
achievements survive production character/item/title loading.

Movement paths are computed against the installed floor and live NPC collision
cells, then traversed one accepted step at a time through `Entity.Move`. The
fixture excludes diagonal corner cutting. It invokes the real NPC handlers and
family-10010/action-85 portal boundary, checking that the intended portal is the
first eligible one under the server's distance calculation. From the Storekeeper
onward, the successful journey does not place the character next to remote NPCs
or directly teleport them into the hunt. Normal services perform all map changes.

After mining, the route returns through Ape City and Phoenix Castle to Twin City,
then Guild Controller, transporter, Old Explorer, 1210 and 1211. The map-1212
outbound portal sequence is **19,15,12,0,2**; return to Grandpa uses
**22,24,5,6**. All segments are reachable through ordinary accepted walking.
Logical 1219 reaches Boatman without reusing a portal into physical 1212.

The four existing visits **10715/10716/10717/10718** are earned by actual accepted
movement/transitions and persist after reconnect. No new producer identity is
invented. First-rebirth quest/growth achievements remain unearned by crafting.

Failure checks cover insufficient/stale purses, changed source map, missing or
substituted terrain, remote/mismatched NPC requests, trade/vending/death/fake or
unloaded sessions, concurrent admission, suppressed receipt/purse writes, failed
map transition, suppressed arrival consumption, reconnect and repeat travel.

The same build passes mining **94**, first-rebirth materials **74**, travel **105**,
guild season **344**, Grotto **262**, promotions **266**, progression **74** and
default hunt/boss rewards **216**: **1,604 gameplay checks** and **3 audit tests**.
Database suites ran sequentially in the disposable validation schema. Logs are
ignored `obj/chi-refill-validation/adventure.log` and `adventure-*.log`.
Producer coverage remains **217/337**, with unchanged wardrobe classifications.

## Limits and next work

The fixture starts with an eligible level-135 character and a small purse; it
does not prove a level-one leveling or silver-earning journey. Mining/drop rolls
and time are deterministic. Waterlord's death is deliberately triggered through
the real death handler with reduced fixture HP; live combat balance, elapsed
hunt time and native movement/animation are separate acceptance work.

An unrelated direct Ape City/Twin City portal pair still specifies Twin City
coordinates outside the installed 824-by-824 terrain: (350,845) and (531,842).
The verified journey uses the valid Phoenix Castle connection. This batch does
not claim that every historical world portal is correct.

The [atomic first-rebirth rite](FIRST_REBIRTH_COMPLETION.md) now completes this
walked journey, including production reload and both first-rebirth achievements.
The dedicated Adventure suite now has 171 checks. Post-rebirth recovery/catalog
follow-ups, native 6711 acceptance, real-time pacing and deployment remain open.
No live server was replaced/restarted and no public client patch was published.
