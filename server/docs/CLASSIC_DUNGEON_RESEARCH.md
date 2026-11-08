# Classic dungeon restoration checkpoint

2026-09-15 UTC; source baseline `0fe8fea`. These findings distinguish the historical
editions. Sky Pass now has a [validated implementation](SKY_PASS.md); the other
dungeons remain research and implementation work.

Later checkpoint: [Dis City stage evidence](DIS_CITY_RESTORATION.md) reads the
previously omitted achievement icon identifiers and resolves their four ordered
stage associations. The table below preserves the earlier audit finding; it is
not the latest blocker state. Dungeon implementation and native acceptance remain
pending, while Snake Islands and classic Labyrinth now have validated code.

## Sources and version boundaries

The approved client's `Questinfo.ini` hash is
`f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14`;
`Achievement.dat` is
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.
Only bounded quest/item records were inspected; proprietary content remains outside Git.

The [current Sky Pass guide](https://co.99.com/guide/quests/skypass.shtml) and
[older official iPad guide](https://co.99.com/ipad/quests/skypass.shtml) describe
five stages, combat passes, a final reward and loss of position after disconnect
or revival. The current page limits rewards daily and has five reward tiers;
the older page offers a fixed four-Meteor reward or a lottery. Their level/reward
editions differ. Neither establishes a random-gate probability.

The [Snake Islands guide](https://co.99.com/guide/quests/snakeislands.shtml)
requires level 90 and traversal across islands to defeat Snake King near (87,67).
It does not establish a precise loot probability table or completion NPC claim.

The [current Dis City guide](https://co.99.com/guide/quests/monstercity.shtml)
describes ordered crystals, a summoned Pluto and a treasury. The
[older official iPad edition](https://co.99.com/ipad/quests/monstercity.shtml)
instead describes capped entrants, two flanks and a single horn winner. They
must not be combined silently into one supposed official edition.

The [current Labyrinth guide](https://co.99.com/guide/quests/labyrinth.shtml)
uses four treasure chests and crystals. The
[older official iPad guide](https://co.99.com/ipad/quests/labyrinth.shtml)
explicitly exchanges seventeen Cloud Diamonds for an Ancestor Box from Simon.
This resolves the historical reward name; it does not establish all box roll weights.

## Installed identities and implications

| Route | Client evidence | Installed server evidence | Remaining decision/evidence |
| --- | --- | --- | --- |
| Sky Pass | Mission 6350; Daniel 30055, Dreamland 1012 (155,165); God Cloud 30054, map 1040 (83,184); final item 721109 | All 25 challenge monster families 3000–3024 and their spawns; five gate groups and 25 stage guards | Code and 34 route checks pass; native/live acceptance pending |
| Snake Islands | Achievement 11018 says complete the quest | Green Snake 30115, Bird Island (678,966); exit 30125, map 1063 (87,44); monster families 3100–3103, including Snake King 3102 | Code and 25 island/revival checks pass; ferry restored to valid official (468,203); native/live acceptance pending |
| Dis City | Missions 6472–6475 name Hell Gate, Hell Hall, Cloister and Formation; achievement 11002–11005 labels remain only numbered | Solar Saint 3215 in Twin City (327,250); existing event class is a state stub | Four missions support a possible stage mapping, but do not prove the four achievement triggers; keep that inference unverified |
| Labyrinth | Mission 6467 is a newer weekly chest route; achievement 11027 explicitly requires the Ancestor Box from Simon; item 721540 is AncestorBox | Simon 1152 is parked on 5001; generals 1153–1156 and floor monsters exist on 1351–1354 | Code and 42 route/payment/loot checks pass for the explicitly named classic exchange; native/live acceptance pending |

Client itemtype hash remains
`d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a`.
Sky passes 721100–721103 correspond to the first four stages;
721104–721108 distinguish final-stage challenge tiers; 721109 is the prize pass.
All ten are present in the client. Server item validation remains an initialization gate.

Dis City's installed mission objectives also contain stale location links and
date fields. Matching text is evidence of content, not a verified native trigger
or permission to mark all four achievements on a single boss death.

## Terrain evidence

The active server map files are:

- Sky Pass 1040: 616 × 616, SHA-256
  `eecf4024f7e626f627f36d67b80be6bfda97f7f5722ada97b4055280d4fc514f`.
- Snake Islands 1063: 456 × 456, SHA-256
  `a87744ff679f61c2fb69c4a8bce6dfc07e7a735cc41ed79e4f03d92ec3dfc349`.

Walkable tiles form many separate islands. A four-neighbor walking search alone
cannot certify these routes. The current player jump handler permits a valid
landing within distance 20; a bounded route fixture must exercise that movement
rule, not assume continuous ground or use arbitrary teleports to prove traversal.
Snake spawn rectangle origins can themselves be blocked, so use valid landing
tiles inside the reviewed spawn/quest regions instead of copying a rectangle origin.

## Selected Sky Pass contract for implementation

Use the installed five-stage map and genuine passes, with a clearly documented
Revora gate rule: each of the first four stages seals one of five gate choices as
the open route. A wrong choice leads to its matching arena; defeat its actual
monster and return its pass to retry. The open choice stays fixed for that stage,
so five distinct choices suffice. The fifth stage always requires a real fight.
Never reroll a sealed stage because of a retry packet or reconnect.

All canonical ordinary characters may begin; the installed mission has no minimum
level. Gate dialogue identifies the selected monster's actual level. Difficult
combat provides its own practical limit. Existing contest ownership blocks entry.

God Cloud consumes the issued prize pass and grants four bound Meteors plus 11019
in one transaction, once per Pacific day. The four-Meteor choice is supported by
the older official guide; bounding and the sealed gate rule are explicit Revora
choices. Do not invent historical loot weights or substitute a reward-free counter.

Save gate choices, battle ownership, item UIDs and transitions. A full bag preserves
earned credit. Death/disconnect abandon unfinished stages as the guides describe;
an already-earned final prize entitlement remains claimable after recovery.
Test genuine NPC requests, every gate/monster/guard association, bounded movement,
duplicate/stale tokens, foreign kills, restart, daily claims and transaction rollback.
Native mission projection may use only the already-supported quest packet fields;
live tracker and visual acceptance remain separate.
