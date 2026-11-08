# Ancient Devil owned trial engine

Follow-up: [Exorcist and class earning](ANCIENT_DEVIL_EARNING.md) now provide a
staged NPC coordinator, all-class earning and atomic amulet transfer validation.
Each private trial has an owned return guide. Production activation and the
overall boss quest remain open; the original engine-batch scope follows.


Current source status (reviewed 2026-10-01): the historical staging and
"still required" notes below describe this page's original batch. They are
superseded for NPC admission, summon/claim dispatch, startup/tick integration
and collection achievement 11020 by [lifecycle evidence](ANCIENT_DEVIL_LIFECYCLE.md)
and [collection evidence](ANCIENT_DEVIL_COLLECTION.md). Do not reimplement those
connected components. Composed-process collection and saved-victory reward
recovery now pass in the linked lifecycle record. Mid-fight interruption,
genuine-client acceptance and deployment remain unclosed gates.
Staged source, 2026-09-20. Admission NPCs and the overall Ancient Devil quest are
not enabled. The engine is compiled and its family-specific damage/death,
teleport and loot guards are connected; there is no startup initialization,
periodic scheduling or ordinary NPC route yet. It is exercised by isolated tests.

## Encounter and recovery behavior

The caller starts a trial at its installed class guard with the character item
lock held. The store checks class eligibility, saved state and range. The engine
allocates a private copy of reviewed terrain 1082 without catalog monsters,
NPCs or portals, records its encounter and life GUID, and persists entry at
(216,208). The class devil starts at (220,208). Runtime map IDs use the existing
11000-59999 allocator, separate from housing identities.

Revora trial policy is one owned class devil per certificate, a twenty-minute
trial and at most sixteen simultaneous private scenes. These unpublished values
are restoration choices, not verified original CO requirements. The chosen
guard selects the devil; the player's profession selects the certificate.

Only the current owner or their registered companion may fight that exact
registered monster object. Foreign sessions, counterfeit objects, expired scenes,
changed professions, trade and released lives lose combat authority. Direct
damage takes a nonblocking owner lock before HP changes; a busy owner defers the
hit. Outsiders cannot teleport into the scene, and same-scene teleport corrections
are restricted to short valid movement. PvP and unrelated actors in the private
scene are rejected. There is no generic loot from these trial families.

The normal damage pipeline reaches the owned death callback. Its transaction
marks the exact life defeated and records a pending certificate before applying
the death. Failure fences the player and leaves the encounter unpaid/alive.
Victory cleanup returns the player to Ancient Island and preserves their claim.
Death, departure, timeout, class change, disconnect or session replacement
abandons an unfinished hunt. Cleanup saves the return location before disposing
the private map, including when the original session is gone or fenced. A replacement session
already relocated by the existing login fallback keeps its public location.

Startup recovery, before player admission, abandons unfinished persisted trials
and returns saved private locations to Ancient Island. Won certificates remain
claimable. It refuses to operate on currently admitted affected players. Lost
unpublished scenes/uncertain commits cannot silently rebind an old life: a later
admission abandons that orphan and records a new attempt. A failed cleanup write
retains the scene for retry. Runtime startup/shutdown/login ordering still needs
integration and real-process acceptance; tests simulate process-memory loss.

## Installed profile evidence and explicit balance policy

A read-only query of `monsterinfos` found the five class families below, all
mesh 205 and database level 70. Several rows reverse minimum/maximum attack;
the Archer, Fire and Water rows have defence 65,000. Those combat values are not
copied into the restored trials.

| Family | Name | Loaded HP |
| --- | --- | --- |
| 7001 | WarriorDevil | 23,588 |
| 7002 | TrojanDevil | 9,436 |
| 7003 | ArcherDevil | 33,964 |
| 7004 | FireDevil | 38,550 |
| 7005 | WaterDevil | 30,000 |

WaterDevil's database life is 38,550, but the active `database/Monsters.txt`
overrides it to 30,000. That file's SHA-256 is
`99d5f7d6b3ece68d9d17cb31d3170ab77c8f2501d80af15075cad467696cac9b`.
The engine checks the loaded names, models and HP values. It preserves those
values and uses level 70, physical attack 350-550, defence 100, range two and
1.5-second attack intervals. These are explicit Revora balance choices; class
time-to-kill and genuine-client presentation remain unvalidated. Trial family
identities are 9916501-9916505; the public catalog families are unchanged.

## Validation scope

The isolated build and `-AncientWorldOnly` fixture pass 42 checks. It runs a
non-GM player through actual `Handle.ReceiveAttack` partial and lethal damage,
pending claim, certificate exchange and production item reload. It also covers
foreign direct damage/death, counterfeit monsters, busy owner locks, trade,
late callbacks, all cleanup causes, failed encounter registration/travel,
failed victory/cleanup writes and simulated restart with unfinished/won trials.
The existing `-AncientStoreOnly` fixture still passes 41 checks. The Warrior epic
regression passes 453 checks and the Abyss encounter regression passes 1,502
checks after the shared damage/teleport guards.

Fixtures supply character state, guard placement, start the internal coordinator
and provide damage values. They do not prove native NPC admission or native
attacks, ordinary class balance, Exorcist/return dialogue, all-class earning,
amulet trading, boss summoning, daily completion or achievement 11020. No live
schema, server restart, client file or release was changed.

The next implementation is brief Exorcist/guard dialogue, full lifecycle wiring,
all-class ordinary earning/transfer tests, and the remaining boss contract.
