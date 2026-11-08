# Guard summon achievement

Reviewed 2026-09-25. Achievement 10119 identifies a guard summon in the installed
hash-bound Achievement.dat catalog. Spell 4000 is the guard skill; other companion
spells are not treated as equivalent achievement identities.

## Material and instruction

Correction to the earlier prerequisite audit: item 1072031 is Euxenite Ore, not
a guard book. Both server and installed client catalogs identify it that way.
The inspected client itemtype.dat SHA-256 is
`d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a`.
The previous transaction used this same item ID; only its descriptions were wrong.
Existing mining rewards already produce this ore. No new drop or price was added.

Instructor 10000 option 88 preserves the existing class-independent reborn rule.
An ordinary player must be near the instructor on map 1004, reborn in live and
saved state, and outside trade or booth activity. One owned Euxenite Ore and the
initial skill 4000 are exchanged in one owner-locked database transaction.
Already-known or refused learning preserves the ore. Exact saved ownership,
stack, binding, lock, bag/warehouse/wardrobe position and rental preimages guard
consumption. Failed writes roll back; failed postcommit projection fences the
session for production login recovery.

## Summon evidence

The shared companion cast block resolves a positive-health monster definition
before spending casting resources or creating an actor. Successful spell 4000
casts then record achievement 10119 only for a living companion owned by the
canonical ordinary player, registered in that player's current map, with a
matching guard monster family and a durably learned skill. The evidence transaction
locks the character and records one idempotent receipt, then refreshes achievements.
The ephemeral world actor is not part of that database transaction.

## Validation and remaining gates

The isolated Debug/x86 build and 629 collection acquisition checks pass. The earned
route reuses the original boss-funded purse and purchased Pickaxe, pays the mine
fare, mines Euxenite Ore through the actual mining tick, teaches the guard through
the NPC handler, reloads the character, casts the skill and reloads its achievement.
No ore or replacement money is supplied for this route. Character qualification,
boss combat, mining roll/time and instructor placement are controlled fixtures.

Separate instruction fixtures cover stacked materials, concurrent requests,
already-known skills, live/saved rebirth, proximity, trade, locks, stale saved
location, suppressed writes and retry after login. Missing and zero-health guard
definitions award no achievement and spend no casting resources. Healthy summons
and concurrent receipt retries leave exactly one completion. All loaded guard
levels are covered because the existing cast path can change the learned level.

This does not certify guard training XP/cooldowns, other companion families,
full rebirth acquisition, native UI or live deployment. No deployment, restart or
client patch was performed. Unrelated owner edits were present in the isolated
build. Native acceptance remains pending.

## Retained mined-material instruction (2026-10-01)

The ordinary retained, twice-reborn Windwalker buys a Pickaxe from Twin City's
existing shop for exactly 1,000 silver and equips it through normal item handlers.
Existing weapons are preserved. It pays the mine fare of 100 silver through
NPC 10080 option 5, begins mining with the sealed action packet and receives
Euxenite Ore through the production mining tick. The saved mining row identifies
the exact ore UID. The ore roll and tick interval are controlled; no ore or
replacement purse is inserted.

NPC 10000 option 88 on map 1004 consumes that ore and durably teaches spell 4000.
Original weapons are re-equipped. After a process restart the learned skill
remains, and a valid decoded cast through the production attack handler creates
a positive-health companion owned by this character in its current map. The
owned-guard-summon receipt records achievement 10119. Another restart preserves
the learned skill, consumed-ore absence and full prior-career snapshot.

Repeating instruction while already trained changes no saved state. The first
learning attempt completed its checkpoint, then the fixture's replay attempted
to address the trainer from Twin City and failed with KeyNotFoundException.
Recovery first checked the exact saved checkpoint, approached the actual trainer
map and repeated only the already-known instruction. No mining, purchase or
ore consumption was repeated. This is a fixture correction, not a gameplay fix.

Stored rewards free the required bag slots through ordinary Stone City deposits.
NPC travel/proximity, mining outcome/time and attack timing are controlled. The
cast uses the decoded handler rather than native input. The temporary companion
is not expected to survive process restart; the skill and achievement must.
Training speed, guard combat balance, native UI and live deployment remain open.

The initial summon checks correctly failed at zero stamina: the fixture disables
Screen timers, including normal stamina regeneration. The passing cast explicitly
supplies the ordinary full-rest precondition of 100 stamina, checks the loaded
guard family and casting eligibility, and verifies all 100 stamina is spent.
No persistent currency, material, skill, achievement or companion is supplied.
This does not validate the regeneration timer; earlier failed casts are not
reported as successful summons.

Validation: recovered `career-retained-guard-learn`, then
`career-retained-guard-summon` and `career-retained-guard-reload` passed on
candidate SHA256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
No production code, live deployment or client patch changed.
