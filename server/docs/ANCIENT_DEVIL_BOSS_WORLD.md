# Ancient Devil guarded world

Current source status (reviewed 2026-10-01): the historical staging and
"still required" notes below describe this page's original batch. They are
superseded for NPC admission, summon/claim dispatch, startup/tick integration
and collection achievement 11020 by [lifecycle evidence](ANCIENT_DEVIL_LIFECYCLE.md)
and [collection evidence](ANCIENT_DEVIL_COLLECTION.md). Do not reimplement those
connected components. Composed-process collection and saved-victory reward
recovery now pass in the linked lifecycle record. Mid-fight interruption,
genuine-client acceptance and deployment remain unclosed gates.

Staged server implementation, 2026-09-20. This batch supplies actual guarded
combat and connects it to paid encounter persistence. It does not yet expose a
summon/claim dialogue or activate the full quest at production startup. No new
achievement producer, deployment or client patch is included.

## Admission and combat

The coordinator prepares an owned copy of island terrain 1082 with four Devil
Guards, the Ancient Devil and a return guide. Capacity is sixteen concurrent
scenes. A fresh nine-amulet reservation and the five actor bindings commit in
one transaction; failed binding rolls back payment. Unexpired paid entries resume
without another offering. Only undefeated actors are published on resume, and
the original thirty-minute deadline remains unchanged.

Only the canonical summoner and their registered companion can fight these
actors. Actual object identity, map registration, session ownership, the item
lock, living state and encounter deadline are checked before damage/death credit.
Foreign teleports, copied actor identities, stale generations, unrelated combat
inside the private map and damage during trade are refused. Boss combat stays
locked until all four guard deaths commit. Owned actor deaths suppress ordinary
legacy drops; the boss instead commits the durable victory recipe.

Normal return dialogue, disconnect, death, invalid ownership and deadline expiry
release scenes through the same cleanup path. An unexpired paid entry retains
its guard progress for another admission. Failed cleanup retains ownership for a
later tick. A committed victory returns the summoner to the public island and
retains its reward until claimed. Post-commit admission failure requires reload
and preserves the paid run for recovery. The class-trial and boss-world registries
remain separate, with both checked at combat and teleport boundaries.

## Profile evidence and explicit policy

A read-only query of the installed `monsterinfos` catalog confirms family 7000
is DevilGuard, mesh 205, level 130 and 6,590 HP. Its stored attack range is
reversed (5,150 minimum / 4,800 maximum). The owned guardians preserve that name,
mesh and HP, with explicit Revora combat policy: level 100, 700–1,000 physical
attack and 200 defense. Four guards and their fixed positions are Revora policy,
not recovered retail encounter scripting.

The boss uses the independently audited AncientDevil name, level 120 and 65,535
HP from the installed client/server presentation catalogs. Its mesh 205 is an
inference from `ini/npc.ini` section NpcType770, whose AncientDevil motion entries
start with 0205; this is not verified monster rendering. Explicit Revora attack
is 1,000–1,500 physical damage before normal combat calculations, with 300 defense.
Guard and boss attacks use range two and a 1,500 ms attack interval. No hard-coded
final player damage or claim of fair class balance is made.

Owned families are 9916601–9916605; no installed monster row is repurposed. Family
7006 remains Guild Beast and is not used as this boss. The native AncientDevil
profile currently says Boss=0. No long HP bar, DevilGuard presentation or genuine
client rendering is certified by this server test. These require native review
and, where justified, a separately validated client profile patch.

## Validation scope

The isolated build and 258 combined boss-world checks pass. Shared-hook
regressions pass: 42 Ancient class-world, 453 Warrior Epic and 1,502 Abyss world
checks. The final profile change was confined to the new boss scene and verified
by rerunning its complete fixture.

`Verify-PotencyHunt.ps1 -AncientBossWorldOnly` earns the first complete collection
through ten actual class trials and nine ordinary trades, resumes its paid run,
kills actual guards/boss through the normal server damage/death pipeline and
claims the saved reward through the internal claim service. It reloads the
character and proves duplicate claims do not pay again. A second offering is
earned through fresh trials and trades for admission rollback and recovery tests.

The fixture covers foreign and counterfeit actors, busy owner locks, guard
protection, player attackability, normal return dialogue, disconnect resume,
failed actor binding, failed death receipts, failed expiry and successful cleanup
retry. Test hits use controlled amounts; this is not combat balance or native
input testing. Summon admission and reward claim are still internal fixture
calls. Completion of the player-facing switch, startup lifecycle, achievement
evidence and genuine-6711 journey remains open.
