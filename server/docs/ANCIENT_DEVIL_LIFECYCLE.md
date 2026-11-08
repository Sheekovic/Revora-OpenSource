# Ancient Devil source lifecycle and display preparation

2026-09-20. The source now initializes AncientDevilQuest from
`Thread.CreateTournaments`, after maps, item/monster catalogs, persistence and
wardrobe storage load and before Account/Game listeners open. Initialization
validates NPC identities and terrain, prepares the stores, recovers stranded
characters and publishes the public NPCs. The existing one-second world event
callback calls `AncientDevilQuest.Tick`, which services both class-trial and paid
boss registries. The quest initializer keeps its dispatcher disabled until its
initialization sequence succeeds.

A bounded read-only query against the configured server catalog found zero NPC
or SOB-NPC rows occupying the added identities 300010, 300016 and 300017. Existing
guard metadata is validated by the earning fixture. No production rows were changed.

The complete ordinary-player server route is connected: travel, class trials,
certificate exchanges, cross-class trades, collection achievement, reviewed
offering, guarded battle, return/resume and once-only reward claim. The isolated
build and 196 combined seal checks pass. The boss-world suite passes 278 checks,
including full 65,535 server HP and the existing boss HUD flag path. The paid recovery suite also passes 326 checks. These tests
invoke the quest lifecycle directly; no live Host was restarted or deployed.

## Offline native evidence

The existing hash-gated x86 GameData probe now has Ancient-specific before/after
modes. It runs copied native dependencies only in the ignored disposable fixture,
reusing that dependency directory. The wrapper validates every dependency hash,
compiles an x86 probe, captures its hash and bounds each process to twenty seconds.
No running Conquer process is inspected or modified.

The original catalog's native lookup returns AncientDevil at MaxLife 65,535,
Boss 0, and no DevilGuard profile. The private staged catalog loads AncientDevil
at MaxLife 65,535, Boss 1, and DevilGuard at MaxLife 6,590, Boss 0. All four native
before/after checks pass. The probe SHA-256 for this run is
`5ece11473c46954cf89a5af1aa6ed29d059f8fd2b68046734609d4cce1885276`.

`prepare_ancient_profiles.py` changes only AncientDevil's Boss flag in the original
catalog and appends DevilGuard, using the existing WarriorDevil display settings
with the guarded scene's name, type, HP and level. Unrelated catalog bytes remain
intact. Three synthetic transformation tests cover preserved data, changed or
ambiguous inputs and duplicate application. No proprietary data is added to Git.

- Approved DX9 executable SHA-256:
  `3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60`.
- Active catalog preimage SHA-256:
  `99fa2c8f14c3851d797a0b20fbd325053f9acee35c7dab4c5e73edc2f5792585`.
- Private staged postimage SHA-256:
  `fe70117a12f17b9b2b27be9c0fb3149302372acf7f213a0a2a1b800090cb2c92`.

Reproduce preparation and the bounded native load test with:

```powershell
python tools/ClientPatches/prepare_ancient_profiles.py
& tools/ClientPatches/Verify-BossProfiles.ps1 -Profile ancient-boss-hud
```

Original and staged data plus an unsigned private manifest remain under ignored
`obj/chi-refill-validation/ancient-boss-hud`. The active client is unchanged.
This catalog is based on the active preimage, so it must be merged with other
staged profile additions before any coordinated release. It is not a signed,
installable or published patch.

## Remaining acceptance

Native profile lookup does not prove the rendered model, HP bar or animations.
The genuine 6711 client still needs the entire ordinary-player journey, including
all NPCs, movement, guard/boss rendering, HP changes, achievement notification,
inventory/currency updates, reconnect and reward retry. A real Host startup and
restart against the intended release configuration also remain untested. Combat
balance and unpublished quest rules retain their documented Revora policies.
These gates remain open under M6.66c; this source milestone does not close them.

## Source reconciliation and next acceptance batch (2026-10-01)

A current source review confirms `Thread.CreateTournaments` calls
`AncientDevilQuest.Initialize`, and the world callback calls its `Tick`.
Initialization connects trial, collection and boss-world stores before publishing
NPCs and enabling dispatch. `AncientDevilSeal` offers and commits normal NPC
summon/resume/claim actions. `AncientDevilCollection` writes 11020 from the earned
nine-class collection. Earlier staging pages now point here instead of implying
these components remain unimplemented. Historical suite counts remain historical;
no component suite was rerun for this documentation reconciliation.

Next composed-process acceptance must earn the class certificates and amulets,
trade nine distinct classes through normal handlers, verify collection evidence,
pay the reviewed offering, complete the guarded fight, interrupt before reward
claim, then restore and claim once across game/database restarts. Preserve
ordinary-player access, exact offering consumption, spare amulet ownership and
bound P7 payout. Test progress belongs to that concrete route, not another source
implementation checkmark. Native rendering/combat and release remain pending.

## Ordinary collection and unclaimed victory across processes (2026-10-01)

The composed private server now completes the acceptance route specified above.
Ten separate ordinary level-100 fixtures (UIDs4294908601-4294908610) cover all ten
classes. Initial levels/classes and starting profiles are controlled inputs;
this is not native creation or natural class leveling, and does not add credit
to the separate retained 320-achievement career.

Each uses the installed Bird Island portal, Exorcist dialogue, its permitted
guard, private trial victory, certificate claim and class-amulet exchange.
Nine normal two-party trades assemble ten distinct earned amulets on the first
hunter and write achievement11020 plus the collection's provenance receipts.
Certificates and amulets are earned through handlers, never fixture-supplied.
All ten profiles, exact item rows, trials, collection and achievement evidence
are saved before abrupt process exit and checked after game/database restart.

The Ancient Seal reviews and consumes nine amulets, retaining the spare. The
collector defeats all four guards and then the boss in their owned encounter.
The normal quest tick returns the collector to the island. A saved, unpaid
victory and the spare amulet survive another abrupt process exit and restart.
The ordinary Seal claim then delivers exactly the saved DragonBall-or-six-Meteor
recipe, unbound as configured, plus one bound catalog P7 soul. The spare remains
owned. A further cold reload preserves the entire snapshot and reward UIDs;
replayed claim and summon options grant nothing and consume nothing.

All four runtime phases pass: `career-ancient-earn`, `career-ancient-win`,
`career-ancient-claim`, `career-ancient-reload`. One compile-only setup failure
(missing generic-list namespace) was fixed before any character creation.
Latest checkpoint: `revora_ancient_process_probe` step3 / `AncientProcessSnapshot`.
Earlier event rewards are included in the nested snapshot and remain unchanged.
Candidate SHA-256:
`F5683ADB48B1D7015F0B998AA48F569431955CAA298FD506FE0BA78F35F93B96`.

NPC/mob approach coordinates and lethal damage amounts are controlled. Damage
uses production ReceiveAttack and owned death handling; this does not prove
attack decoding, damage calculation, combat balance or native movement. This
batch interrupts after collection and after earned victory, not mid-fight or
during COMMIT. Genuine-client acceptance, natural timing, production release
configuration and deployment remain open. No live restart or release occurred.


## Fresh Warrior amulet through calculated combat (2026-10-03)

The actual fresh career UID1096322 continues from its completed Labyrinth
checkpoint as a level124 Warrior with existing earned equipment and possessions.
Exorcist300010 admits it through the ordinary island dialogue. WarriorGuard300011
refuses an unearned certificate and starts the class-owned private encounter.
One normal calculated melee attack defeats the Warrior Devil; no HP/damage value
or death callback is supplied. The quest tick returns the character to1052.

Across four separate private processes, the unpaid victory is preserved, the
ordinary guard issues bound certificate710016, that exact certificate survives
restart and is exchanged for unbound Warrior amulet710011, then the amulet and
all prior career/trainee state survive final reload. Replayed claim/exchange
options grant nothing.11020 remains absent: one amulet does not meet the
nine-distinct-class collection requirement. Final bag count is36.

`tools/CheckCandidateFreshAncient.cs` phases `career-fresh-ancient-earn`,
`career-fresh-ancient-certificate`, `career-fresh-ancient-exchange` and
`career-fresh-ancient-reload` pass after a fixture family-ID correction. The first
attempt searched for source family7001, whereas the private encounter uses
FamilyOffset+7001 (9916501), and stopped before any attack. Normal startup
recovery retained that undefeated life, marked its trial abandoned and returned
the character to1052 at216,208. A recursive comparison found only those trial,
life and position changes; no prior property, purse or earning changed.

The private probe alone adopted the reviewed recovery after exact hashes:

- Before: `c8a8d744fa388b34c3509d85eca8a37d9960ee7c702c50332db28df319f85092`.
- Recovered: `8e956605b2695c2d19b59e039aa5f8f08f6d7cca59450202f6646a561992d3de`.

The abandoned trial remains recorded. A newly admitted ordinary trial earns the
certificate; nothing is reset or manufactured. Final checkpoint is
`revora_fresh_ancient_probe`, stage `exchanged`. Game SHA-256 remains
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
NPC/monster approach and attack cooldown are controlled; normal map timers run,
player pet/screen timers are omitted. The one-hit result is not balance or
retaliation acceptance. No live deployment or native-client validation.

Next fresh collection work: use the existing ordinary Trojan trainee and newly
created ordinary Archer, Ninja, Monk, Pirate, Dragon Warrior, Windwalker and
Water Taoist contributors. Earn their level/class qualification and amulets,
trade the eight distinct amulets to this Warrior, verify11020, then validate the
nine-amulet offering and calculated boss fight. The earlier ten supplied-profile
fixture is supporting coverage, not a substitute for this earning chain.


## Fresh collection contributors created and trained (2026-10-03)

Seven new ordinary accounts/characters are created through AccountTable and
CreateEntity: FreshArc1, FreshNin1, FreshMonk1, FreshPir1, FreshDrag1, FreshWind1
and FreshWater1. Their installed creation choices yield base classes40,50,60,
70,80,160 and100, each at level1 and zero rebirths. The existing ordinary Trojan
FreshPupil1 joins them at its previously earned level33. Every contributor opens
the normal bound RevoraStarterPack through its item handler, with one saved
opening,50,000 bound CPs,1,100,000 silver and owned class gear. No profile level,
item, purse or amulet is directly supplied. The label FreshWater1 does not itself
qualify the base Taoist as Water; the trainer branch remains a next step.

The saved Warrior leads two ordinary five-member teams using sealed create,
invite, accept and exit packets. First group:2,920 calculated one-hit Birdman
kills raise the Trojan to58 and Archer/Ninja/Monk to50. After a cold process load,
the second group earns the remaining levels through2,929 kills. Final levels
are58,50,50,50,50,50,50,50, with5,849 total kills/attacks. Every trainee level
transition checks the exact production Virtue award; the leader's final balance
is36,971 (starting at five after Labyrinth). Both teams dissolve normally.

A final game/database restart checks the exact eight contributor profiles,
possessions, creation/starter records, achievement receipts and all nested prior
Warrior/Trojan career state. This is a prerequisite, not proof of the eight
remaining trials or collection11020. It preserves the already earned Warrior
amulet; no new Ancient amulet is issued in this batch.

`tools/CheckCandidateFreshContributors.cs` phases
`career-fresh-contributors-create`, `career-fresh-contributors-train-a`,
`career-fresh-contributors-train-b` and `career-fresh-contributors-reload`
all pass on their first run. Checkpoint `revora_fresh_contributors_probe` is
`trained`; `revora_fresh_contributors` records exact roster ownership.
Game SHA-256 remains
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.

Controls: NPC/map/monster approach, nearby teammate positions and attack cooldowns
are controlled. An exhausted Birdman population uses the existing map callback
with advanced time. XP and Virtue are earned by actual calculated kills, but
this does not establish natural duration, survivability or native party UX.
Player pet/screen timers are omitted. No live restart, deployment or client patch.
Next: ordinary trainer qualification (including the Water branch), starter-gear
equipment, each contributor's own trial, and eight provenance-preserving trades.


## Fresh nine-class collection and calculated boss payout (2026-10-03)

The eight normally created/trained contributors now qualify through their real
trainers, including base Taoist100 to101 then Water132. Final classes are11,41,
51,61,71,81,161,132. Each equips owned starter gear through the equipment handler,
with at least six equipped pieces and a main weapon. The existing Warrior's
class25 amulet is preserved throughout.

Each contributor enters its permitted Ancient trial, defeats the private devil
using normal calculated attacks, claims its bound certificate and exchanges that
exact UID for its class's unbound amulet. There are eight kills and124 attacks
(6,8,15,14,15,14,14,38); Archer uses ranged attacks, the others melee. No damage
value, class, certificate, amulet or death callback is supplied. The four phases
`career-fresh-amulets-{qualify,earn,trade,reload}` pass on their first run.

After cold amulet recovery, six prior Warrior items move through the ordinary
Phoenix warehouse handler. Eight two-party trades transfer only the earned
amulets to the Warrior.11020 is absent through the seventh trade and appears
exactly once on the eighth, when nine distinct provenanced amulets coexist.
The final reload preserves all nine exact UIDs, collection provenance, donor
ownership changes, class/starter profiles and the entire prior career. Bag38
has room for every transfer. This covers nine required schools; it does not
claim a freshly leveled Fire Taoist contributor.

The Ancient Seal then consumes exactly those nine amulets, preserving the other
29 bag items. Four guards and the boss die through nine calculated melee attacks;
five owned death receipts and the unpaid victory are saved. Cold NPC payout
issues the saved unforced result: six unbound Meteors and one bound catalog P7.
A final restart preserves exact result ownership,36 bag items, the offering,
victory, collection achievement and all prior contributor/career state. Repeat
claim/summon options consume and award nothing.

`tools/CheckCandidateFreshAncientBoss.cs` phases
`career-fresh-ancient-boss-{win,claim,reload}` pass after correcting an overly
strict snapshot assertion. The first cold claim stopped before claiming because
startup intentionally rotates the battle generation to revoke old actors.
The revised check requires one battle, defeated mask31, a changed generation,
and equality of every other field in the complete nested snapshot before updating
only the private checkpoint. The subsequent claim log confirms generation-only
recovery. No victory, item, offering or earning receipt was reset or fabricated.
Current checkpoint: `revora_fresh_ancient_boss_probe`, stage `claimed`.

All seven successful phases use the unchanged game binary SHA-256
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
NPC/monster approach and attack cooldown are controlled; player pet/screen timers
remain omitted. These checks establish ordinary handler earning and persistence,
not natural difficulty, retaliation, authenticated/native movement or rendering.
No live restart, game-source change, deployment or client patch is included.
