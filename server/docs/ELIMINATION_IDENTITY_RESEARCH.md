# Elimination identity checkpoint

Reviewed 2026-09-20. Achievement 11010 is the remaining missing producer in the
installed 11000 quest category. Its expansion guide has now been recovered, as
recorded below; runtime target/den details still require work. Similar
event names do not establish a link; this checkpoint enables no player service.

## Installed evidence

`python tools/audit_elimination_identity.py --client ../../6711` passes. The
read-only audit checks hashes, the achievement codec round trip and unique records:

| Record | Identity | NPC / location | Minimum level |
| --- | --- | --- | --- |
| Achievement | 60210 / 11010, score 10 | Internal key `Achievement_PassElimTraitTask` | Unspecified |
| Elimination | Mission 993 | 3713, Twin City (436,449) | 40 |
| Monster Elimination | Mission 1962 | 9043, Twin City (443,363) | 70 |

Neither quest candidate explicitly references achievement 11010. Mission 993
records an April 2010 event period and scrap rewards; mission 1962 records
July/August 2011. Other later Monster Elimination variants also exist. Do not
combine their dates, limits or rewards by name. The achievement's internal key
suggests a different task, but that remains an inference.

Questinfo SHA-256:
`f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14`.
Achievement SHA-256:
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.

## Primary-source findings

The official [subclass guide](https://co.99.com/ipad/classes/sub_class.shtml)
lists Elimination as a ten-Study-Point source and links to
`https://co.99.com/guide/quests/10expansion/elimination.shtml`. This identifies a
research lead, not the quest objectives or completion boundary.

The browser could not retrieve that expansion URL. A separate validated-HTTPS
read returned HTTP 200 containing the site's Page Not Found page, not a guide:
5,700 bytes, SHA-256
`68cdbcc5787d29f197641afe10170775cd9af23db184ca8b0999ce4ea5fc9e6c`.
HTTP success therefore does not establish recovered source content.

The [Monster Elimination guide](https://co.99.com/guide/quests/monsterelimination.shtml)
describes Master Zeng, level 70 admission and 500-kill rounds. It does not establish
achievement 11010. The [April weekend guide](https://co.99.com/guide/quests/2010april_weekend.shtml)
associates Marilynn with contribution and scrap exchanges, likewise without
proving that achievement's earning condition.

A Python archive-index request failed certificate validation. A follow-up using
Windows Invoke-WebRequest with normal certificate validation succeeded. TLS
verification was never disabled. No third-party emulator implementation was
acquired or copied.

## Recovered expansion guide

The [archived official guide](https://web.archive.org/web/20150906203216/http://co.99.com:80/guide/quests/10expansion/elimination.shtml)
was retrieved from the raw 20150906203216 capture. Its SHA-256 is
`cec1e82791dff1a0b9d9f2b2acf1b245be74a882410a47858e28b796d2cde097`.
The HTML remains in ignored `obj/chi-refill-validation`; it is not redistributed.

The guide describes a daily rescue for reborn characters of level 70 or higher.
Georgia stands in Maple Forest (423,55). Twelve Beauty Herbs yield Fancy Spice,
used at (311,256) to summon Rock Behemoth. Defeating it rescues Landy. Ten Fire
Pills from Fire Spirits L43 then cure her poisoning. Georgia subsequently sends
the player into the Rock Monster Den, where defeating lackeys reveals Rock
Leader. The final return pays one EXP-ball unit and 50 Study Points, once daily.

The specific guide's 50 Study Points conflicts with the generic subclass list's
10. Use the specific quest reward as the staged contract, documenting that
discrepancy. The guide does not publish drop odds, herb locations, den terrain ID,
lackey count or monster combat stats. Those require further installed evidence or
explicitly documented Revora adaptations, not assertions of retail parity.

## Installed cross-check and initial rules

The extended read-only audit validates all three matching client/loaded-server
items and their instructions: Fire Pill 711041, Beauty Herb 711042 and Fancy
Spice 723019. The installed NPC profiles include Georgia (468) and Beauty Herb
(470). Server display profiles contain RockBehemoth and RockLeader, both level
42 with 3,656 display HP and TypeIDs 2440/2441. These display IDs are not evidence
of a server monster family, model selection or a playable boss.

A read-only query found no runtime catalog NPC named Georgia, Landy or BeautyHerb,
nor a matching 468/470 lookface. A targeted monsterinfos query found FireSpiritL43
family 68 (mesh 202, level 43, life 3,658), but no RockBehemoth/RockLeader family.
The ordinary RockMonster row is level 77 and must not silently become this boss.

`EliminationRules` stages only admission, material counts, item identities,
documented reward units and the spice site. The six-cell interaction reach is
Revora policy. Build, 37 boundary checks and the installed catalog audit pass.
No NPC, item handler, kill producer, daily receipt or achievement grant is enabled.

## Next implementation gate

### Herb placement and gathering contract

The staged rules now reserve Georgia 300020, herbs 300021–300032, and future Landy
300033. A read-only query of both `npcs` and `sobnpcs` found no rows in this range.
Runtime initialization must still reject collisions: this query is a snapshot,
not a permanent reservation. Georgia and herb native profiles remain 468/470.

Herb coordinates are defined once in `EliminationRules.cs`. Twelve distinct
plants, one gathering per plant per owned run, are an explicit Revora adaptation;
the official guide publishes neither locations nor regeneration behavior. The
pure mask transition rejects wrong maps, invalid NPCs, corrupt masks, repeated
plants, excessive range and overflowing coordinates. It does not authenticate
players or create inventory; the future transaction must validate the canonical
player and lock the saved run before applying the transition.

`python tools/audit_elimination_world.py` reads those source sites and verifies
their centers and reachable interaction approaches in the installed 944×944
Maple Forest terrain, SHA-256
`7e8f7a8262f8ae93a314699af6d276f0324fff6c32bcc6061bffc2f336f1e817`.
All routes originate at Georgia's southern approach (423,57), reserve 3×3
footprints around Georgia and all herbs, and prohibit diagonal corner cutting.
All twelve plants and the spice site are in the reachable component. The spice
approach requires 198 minimum walking steps under these assumptions. Dynamic
NPCs, monsters, portals and genuine-client travel are not covered by this audit.

The standalone rule suite now passes 115 checks; six synthetic terrain tests
cover connected and disconnected sites, blocked actors/origins, diagonal corner
cutting and malformed maps. No player service or reward has been enabled.

Connect the staged NPC identities and herb approaches to owned transactions.
The initial daily herb, spice, lost-material recovery and expired-item cleanup
transactions, NPC dialogue, saved rescue admission and private actor bindings now
and staged private travel/combat/Landy recovery exist with 123 passing store/interaction/lifecycle checks
([scope and remaining work](ELIMINATION_STORE.md)). The dispatcher is connected,
but normal startup does not initialize this incomplete service.

A follow-up native `monster.dat` decode/encode round trip found unique
RockBehemoth/RockLeader records matching the server display values above.
This still does not establish their spawn-body mesh. No `Landy` name occurs in
the installed `npc.ini`, `npcex.ini` or `NpcX.ini`. Nearby profile 469 is named
Cherry, not Landy; numeric adjacency to Georgia is not identity evidence. A
documented Revora appearance adaptation or further evidence is required for the
rescued NPC. The staged Landy actor now explicitly reuses Cherry's mesh 4690
with server name Landy as that Revora adaptation; native appearance acceptance
remains open. No client profile has been changed.

Establish canonical FireSpiritL43 kill credit, owned Behemoth/Landy state and a
verified den terrain/profile. Add daily
admission, exact material consumption, private encounter recovery and atomic final
EXP/Study rewards. Bind 11010 to the completed rescue-and-den contract only after
ordinary-player rollback, replay and reload checks. Keep the similarly named
historical events separate and leave client/Host acceptance open.


## Rock Monster Den mapping investigation (2026-09-20)

Installed `6711/ini/GameMapEx.ini` explicitly names section 1951
`RockMonsterDen`; SHA-256 is
`64ce044b2eb7ad15e980d91a5834f19d5ccc5dee2d3022cacb72e7a99833af7f`.
The server has `bin/Debug/database/maps/1951.map`, an 800 by 800 terrain grid,
SHA-256 `06a630a835ece881500e93587aee51136dfd15cd0a1a91debb992c9efbd3c1ea`.
This is strong installed naming evidence, but does not establish a loadable
native scene or the correct underlying DMap asset.

A complete bounded parse of both installed map indexes found zero 1951 records:

- Client `ini/GameMap.dat`, 12,015 bytes, SHA-256
  `86a54928a9682617371542cdb5a72707c799b92a5c820e3581183adaa94f32b0`.
- Runtime `database/GameMap.dat`, 11,513 bytes, SHA-256
  `d1f35d6dc3dc0d1958a2aa612c4cdc988ed693636d89ad93078388e81cd23c19`.

Read-only `SELECT id,mapdoc,type FROM maps WHERE id=1951` returned no rows.
A bounded header check of loose DMap files immediately under the approved
client's `map/map` directory found no 800 by 800 file. This does not exclude
compressed assets, other directories, or map aliases. A same-size exact-content
comparison found no duplicate of 1951 among runtime `.map` files.

Next evidence work must resolve the underlying client terrain/alias and prove
server collision agreement before the private den can be enabled. Do not treat
the display-name section as a map-index entry, silently substitute an unrelated
map, or claim native acceptance. Saved post-cure admission can be implemented
and tested independently; lackey count and combat tuning still require explicit
Revora adaptation because the recovered guide gives no values.


## Explicit Revora underground den adaptation

A bounded follow-up inspected all 129 local map archives without a loose DMap
counterpart. Each archive's listed uncompressed total was limited to 32 MiB;
extraction was read into memory with a ten-second timeout and no client writes.
None had the 800 by 800 dimensions of server terrain 1951. This narrows the local
search but does not prove the original retail den asset never existed.

The staged Revora encounter will instead use a private copy of the existing
underground mine terrain, base 1021. This is an explicit layout adaptation, not
retail-map parity or a newly verified 1951 alias. Both current map indexes bind
1021 to `map/map/mine.7z` with flag 256. The extracted archive DMap is byte-identical
to the loose `mine.DMap`; its row walkability data exactly reproduces the complete
server `1021.map` grid, including dimensions. Reviewed hashes:

- Archive: `4de18909269b55d24bc86720d4395f71dfa5e4a95ed96a52e501d86e3f062346`.
- DMap: `561be32ea449aa5310ced4c92f70aacc066dc79223be95abbd4c5b09caa52d46`.
- Server terrain: `608dc4f689d0eff861bcaf5e48a0585714e9bd254c03209a04dd92872e3f19d2`.

`EliminationRules` stages landing (32,40), exit (30,40), six lackeys at (30,30),
(40,40), (50,50), (60,60), (70,70), (70,50), and leader (60,75). Six distinct
lackeys is a Revora pacing choice because the guide gives no count. The leader
rule requires all six saved slot bits; duplicate kills and corrupt masks cannot
advance the rule. World code must authenticate each canonical actor, generation,
owner and kill before calling it; these pure rules do not provide that authority.

`python tools/audit_elimination_den.py --client ../../6711` validates the hashes,
index bindings, complete walkability agreement, distinct sites and connected
two-cell approaches. It conservatively reserves every actor's 3 by 3 footprint
and forbids diagonal corner cutting. Existing Maple Forest audit behavior is
preserved. Build, 141 Elimination rule checks and six synthetic terrain checks
pass. No private den scene, kill persistence, NPC travel, client render or reward
is enabled by this layout milestone. Terrain agreement is not visual acceptance;
that remains required for the eventual real-client private-map test.


## Composed den index mismatch (2026-09-27)

Verified local evidence: the private candidate's `database/GameMap.dat` SHA256
`D1F35D6DC3DC0D1958A2AA612C4CDC988ED693636D89AD93078388E81CD23C19`
contains map1021 -> `map/map/mine.7z`. `DMaps.Load` converts that archive suffix
into `.dmap` and retains the resulting index path. `Map` prefers its base-ID
`maps/1021.map` cache, whose reviewed SHA256 remains
`608DC4F689D0EFF861BCAF5E48A0585714E9BD254C03209A04DD92872E3F19D2`.

EliminationDenWorld incorrectly required only `maps/1021.map` in the index and
therefore failed actual composed startup despite the correct cached terrain.
It now accepts exactly the reviewed cache spelling or the loader's native
`map/map/mine.dmap` spelling (Windows case/separator normalization). Wrong maps,
parent traversal, absolute paths and the unconverted archive spelling remain
rejected. The exact cache hash is still mandatory before initialization, and
private den construction continues using the explicit reviewed cache filename.
The process fixture covers accepted/rejected names and actual full startup;
this is server index/cache compatibility evidence, not native den rendering.
