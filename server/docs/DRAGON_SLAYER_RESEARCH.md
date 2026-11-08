# Dragon Slayer: 6711 contract and staged crafting

M6.46 completes the pill-crafting prerequisite and removes two incorrect legacy
summoning paths. M6.47 now supplies [grass, minion and ghoul earning](DRAGON_SLAYER_HUNTS.md).
M6.48b now completes the [Terato server route and rewards](TERATO_DRAGON.md).
The complete Dragon Slayer acceptance remains open in the genuine client.
Validation below records the M6.46 baseline;
the linked M6.47 evidence supersedes its staged build.

## Confirmed identities and version differences

The official [Dragon Slayer guide](https://co.99.com/guide/quests/10expansion/terato_dragon.shtml)
describes seven grass/minion branches, nine matching kills per pellet, a ghoul
pill from each branch, and seven distinct pills combined into a Dragon Pill.
Its final encounter awards 50 Study Points to each team member and random
refinery/accessory/Dragon Soul rewards. It does not specify a complete loot
distribution or the exact server combat profiles needed here.

The installed 6711 item descriptions place the summoning sites differently from
the older web guide and the legacy NPC dialogue. For this client target, the
installed numeric identities and tooltip coordinates are the intended contract:

| Branch | Grass | Minion client TypeID | Pellet | 6711 site | Ghoul client TypeID | Pill |
| --- | --- | --- | --- | --- | --- | --- |
| Ice | 711189 | 3073 | 720591 | 511,570 | 3066 | 711181 |
| Stone | 711190 | 3074 | 720592 | 599,439 | 3067 | 711182 |
| Point | 711191 | 3075 | 720593 | 515,329 | 3068 | 711183 |
| Ray | 711192 | 3076 | 720594 | 441,317 | 3069 | 711184 |
| Grey | 711193 | 3077 | 720595 | 183,375 | 3070 | 711185 |
| Blood | 711194 | 3078 | 720596 | 109,335 | 3071 | 711186 |
| Bloom | 711195 | 3079 | 720597 | 125,279 | 3072 | 711187 |

The Dragon Pill is 720598; its 6711 tooltip identifies floor-six site (532,394)
and the Royal Guardian at Wind Plain (517,742). Fragment 728120's tooltip
specifies eight fragments per Dragon Pill. The installed floor-six Guardian is
NPC 7767 at (361,645), unlike the old guide's (477,759). Client TypeIDs identify
client profiles; they are not assumed to be wire body/mesh values.

All fourteen named minion/ghoul profiles exist in the client catalog. They declare
level 128, MaxLife 65,535 and Boss=0. Those values are client presentation
evidence, not verified original server attack/defense or maximum-health rules.
The exact-name TeratoDragon profile declares TypeID 4152, level 138,
MaxLife 50,000,000 and Boss=1. A future server encounter must agree with that
presentation or provide separately reviewed matching client content.

Read-only database inspection finds **no named minion, ghoul or TeratoDragon
server templates**. Crucially, template 4152 already belongs to **DragonKing**,
with body 950, 100,000,000 HP and level 200. It must not be overwritten or assumed
to be the named Terato encounter. Floor six currently contains seven spawn
records for FrostBladeDevil (2419) and four for MagmaDemon (4151). Its only NPC
row is the Guardian; the seven ghoul statues are absent.

Reviewed installed inputs (SHA-256):

| Input | Hash |
| --- | --- |
| 6711 itemtype.dat | `D49F0658BF01387B99C284DDEC81713838F2EAE3ADB8E63666E5C906F63CF87A` |
| 6711 monster.dat | `99FA2C8F14C3851D797A0B20FBD325053F9ACEE35C7DAB4C5E73EDC2F5792585` |
| Server maps/2056.map | `60E67D7058814B81E2FB602A9480F919FD247F177922EB308BD121E30CD73766` |

The server terrain is 740×740. A bounded four-neighbor traversal from the
Guardian finds 99,330 connected walkable cells; all eight 6711 summoning sites
above are themselves walkable and in that component. This checks server terrain
access, not native rendering or exact spawn occupancy during play.

## Implemented crafting prerequisite

Ordinary, nearby, living players can use Guardian 7767 to combine either one of
each seven ghoul pills or eight Dragon Pill Fragments into one Dragon Pill.
The fragment service's placement at this Guardian is a Revora integration choice;
the client description establishes the recipe, not that particular NPC route.
The brief dialogue replaces the former nonfunctional grass/pellet answers and
retains the existing Desert City return near (482,633).

Each recipe takes a snapshot of eligible owned materials, then requires a fresh
confirmation within two minutes. Stack changes, expired offers, cancel replies
(including native NPC-ID-zero cancellation) and confirmation replay cannot
consume a new recipe. A later deliberate recipe selection may craft again.

The transaction locks the character, verifies location and inventory count,
debits exact item preimages, inserts the result and records both the craft and
each material in `revora_dragon_pill_crafts` / `revora_dragon_pill_materials`.
One bound input makes the result bound; bound stacks are selected first. A full
inventory is accepted only if consumed inputs free a result slot. No currency
is charged. Projection follows commit; a database failure rolls everything back
and fences the session for reload.

This is an owned-material service. M6.47 separately proves fresh acquisition of
the seven ghoul pills through server fixtures. Fragment acquisition and the final
dragon remain outside those completed checks.

## Incorrect legacy summoning removed

The old `case 720598` had its entire body commented out, so Dragon Pill use
continued into Sword Master's house-dialog branch. The old Royal Guardian
(180810) separately consumed a Dragon Pill and five Meteors before calling
template 4152, which is the unrelated DragonKing described above. Both paths
now preserve the player's offering with a brief unavailable-seal message.
M6.48b replaces those unavailable replies through an owned encounter dispatcher;
both ordinary routes now work in server fixtures, with no change to DragonKing's
existing template. The paragraphs above record the M6.46 baseline repair.

## Validation and next implementation

The final staged build passes 113 Grotto/crafting checks, including both recipes,
stack leftovers, binding, concurrent confirmations, expiry/cancellation, invalid
sessions/materials, capacity, exact database preimages, suppressed item/receipt
writes, fresh ordinary-player persistence and the two legacy misrouting guards.
Related suites pass 47 monster equipment, 105 travel, 179 cosmetic ownership,
9 quest-packet, 43 Labyrinth, 35 Release Souls, 65 Summer, 34 Sky Pass,
25 Snake Islands and 3 producer-audit checks: 658 total.

Staged server SHA-256:
`0E926DCAF20D0753EBC5B2377C56D6AB05133FA6FD3C998854EA26D8409D6544`.

Remaining implementation sequence:

1. Completed in M6.47: owned grass claims and seven nine-minion objectives;
   reject duplicate death credit and retain progress through reconnects.
2. Completed in M6.47: seven 6711 ghoul sites, pellet consumption, distinct pill rewards and
   recoverable summoned encounters. Establish body/mesh and attack/defense rules
   from further evidence or explicitly documented Revora balance choices.
3. Completed in M6.48b: independent 50-million-HP Terato, paid spawn recovery,
   simultaneous summons and temporary-world reconstruction. Full Host restart
   acceptance remains pending.
4. Completed in M6.48b: 50 Study Points per eligible team member plus a reviewed loot pool,
   with atomic, replay-safe claims. Integrate Royal Guardian's five-Meteor route
   only with that same encounter ownership model.
5. The full ordinary server earning route passes in M6.48b. Validate genuine
   6711 presentation before closing Dragon Slayer or deploying it.

No live restart, deployment, client modification, public patch or website
publication was performed. Producer coverage remains 156/337.
