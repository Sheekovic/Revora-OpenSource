# Exploration destination repairs

2026-09-15. M6.45b restores existing ordinary-player travel to Orchid Garden and
Frozen Grotto's first two floors. Source and isolated validation are complete;
native-client acceptance and deployment remain pending.

## Problem and resulting behavior

The installed map index and terrain include 1806 and 1927, but `public.maps`
omits both. The loader can construct their worlds without metadata, while map
status emission and same-map teleports consult that missing metadata. The Frozen
Grotto Envoy's two choices also explicitly teleported to Twin City instead.

`ExplorationWorld.PrepareMaps` supplies runtime metadata before world loading.
It verifies the existing terrain hashes, validates both identities before adding
either, preserves compatible owner metadata, and rejects ownership/base-map
collisions. Defaults are Revora policies: status 2 for Orchid Garden and 8200 for
Frozen Grotto 2, matching the existing first-floor policy for the latter. No new
claim about the original status-bit meanings is made. Runtime defaults are
reconstructed at startup; this change does not write production map-table rows.

Ordinary player routes:

| NPC | Entry or exit |
| --- | --- |
| Daily Quest Envoy, Twin City (319,307) | Existing garden choice goes to map 1806, configured landing (107,109), from configured minimum level 80 |
| Magnolia keeper, Orchid Garden (102,105) | Existing return choice goes to Twin City (319,310) |
| Frozen Grotto Envoy, Desert City (472,645) | First-floor choice goes to 1762 (334,354); second-floor choice goes to 1927 (388,628) |

Routes require a living, loaded, canonical player beside the NPC, outside trade
and booth use. Missing or conflicting destination metadata blocks movement.
The garden retains its configured level and visibility settings. Repeated
requests after departure fail the origin/proximity check. Travel has no charge
and grants no seed, quest prize or flowering completion.

The existing first-to-second-floor and return portals are unchanged and tested
through the normal portal dispatcher. Correct destination metadata also prevents
same-map teleports from falling back to Twin City. Visits 10808, 10801 and 10802 persist
through the previously integrated travel service.

## Evidence and scope

The official [Frozen Grotto guide](https://co.99.com/guide/quests/frozengrotto.shtml)
identifies the Desert City General, the Envoy's first-floor admission and travel
between floors. The installed Envoy already offers the first two floors; this
repair preserves those options. It does not claim to implement the separate
level-120–129 quest.

The official [Beauty and Beast event](https://co.99.com/guide/event/2017_beauty_beast.shtml?campaignid=1)
uses Orchid Garden as a distinct destination. Revora's installed daily envoy and
Magnolia keeper supply the route repaired here. The [iPad Orchid quest](https://co.99.com/ipad/quests/orchid.shtml)
has a different seed/watering contract and is not silently substituted for this
PC server's daily quest.

Read-only database inspection confirmed the listed NPC identities, positions and
the two missing map rows. It also found 21 existing first-floor spawn records and
23 second-floor records. No monster populations, keys, boss prizes or paid seed
recipes are changed.

Reviewed installed server terrain SHA-256:

| Map | SHA-256 |
| --- | --- |
| 1762 | `9EE2B27D737769C53575A1862B821E950AF855B6FC680D03B041F56F15F04873` |
| 1806 | `0A1A8B62B23DA2060858FDB7F7270163C4B6CC03C8D6328B7012919AB05FB5E3` |
| 1927 | `5F4B3F51A8A022C217CF553909D96583D05D489953128A4FBF599B088611CBCE` |

## Validation and remaining work

The final staged build passed 105 travel, 129 region and 3 producer-audit checks.
Before the final first-floor normalization, it also passed 9 quest-packet,
35 Release Souls, 65 Summer, 34 Sky Pass, 25 Snake Islands, 43 Labyrinth and
179 cosmetic ownership checks: 627 checks total. Travel tests include the real NPC dispatcher,
configured level boundaries, ineligible sessions, missing metadata, metadata
collisions, changed terrain hashes, both Grotto entry options, portal returns,
same-map teleports and a fresh ordinary character load. They use the disposable
validation schema and read-only installed terrain. The complete host startup and
native map display have not been exercised by these fixtures.

Staged server SHA-256:
`F298D76F06865F0C061A9D7725D16A81AD50624FE98E553B77DE43CDFD632B0B`.

Forty-five of the 46 exploration regions now have supported terrain/setup. The
remaining destination gap is 1363, which lacks installed terrain/index content.
The full admission audit for every other destination remains open.

First-floor equivalence: approved client and server `GameMap.dat` select
`icecrypt-lev1` for terrain 1762; approved `GameMapEx.ini` labels logical 1926 as
Frozen Grotto 1, and region.ini associates 1926 with 10801. The server now maps
accepted visits in the canonical 1762/1762 world to that named-floor requirement.
This is an explicit Revora equivalence derived from those independent local
facts, not a change to native wire identity. The receipt retains actual server
map 1762. Arbitrary copies using terrain 1762 are rejected. Existing first-floor
monsters and portals stay in their owned world. The final suite exercises both
real Envoy admission and rejection of an unrelated instance.

Frozen Grotto's fifth-floor key/VIP admission and floor-four return are now
repaired by [M6.45d](GROTTO_PASSAGE.md), with ordinary key earning added by
[M6.45e](GROTTO_KEY_EARNING.md). Dragon Slayer objectives, the Magnolia
seed/flowering reward transaction and their original edition rules still need
separate implementation work.
No live restart, deployment, client modification, public patch or website update
was performed.

2026-09-16 follow-up: the existing two-seed Magnolia route now has durable
seed issuance, atomic planting rewards and walked/reloaded ordinary-player
validation in [M6.56b2a](MAGNOLIA_TRANSACTIONS.md). Official five-grade parity
and genuine client visual acceptance remain open.
