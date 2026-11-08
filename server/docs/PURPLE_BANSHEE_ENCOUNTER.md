# Purple Banshee house encounter

## Fresh earned summon purchase and cold ownership (2026-10-03)

Fresh ordinary UID1096322 continues from the completed Arena/Sword Master path.
`career-fresh-purple-buy` spends35,000 of its earned992,000 Honor through the
Honor shop, leaving957,000 in both solo/team mirrors and one item3004259.
The repeated Buy request does not duplicate the item or debit. The saved
purchase records the exact item UID and before/after balances. No Honor, item,
level or equipment is supplied by the fixture.

`career-fresh-purple-purchase-reload` compares the entire nested prior career,
purchase, inventory and balances after a separate game/database restart.
The bag contains33 items; its optional ordinary warehouse branch was not needed
and is not claimed as newly exercised. There are no Purple Banshee run rows yet.
The new checkpoint is `revora_fresh_purple_probe`, stage1. Do not repeat Buy or
compare the advanced character against the historical Sword Master checkpoint.

Both purchase and final cold recovery pass against unchanged private game hash
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
The first cold attempt passed the ownership comparison but failed in diagnostic
printing on an empty equipment slot. The fixture now skips null slots; only the
read-only cold phase was rerun. The failed redacted log remains alongside the
successful logs under `C:\Revora\DisposablePostgres\moon-box-candidate`, named
`arena-process-career-fresh-purple-purchase-reload-diagnostic-failure.log`.
Source: `tools/CheckCandidateFreshPurple.cs` and the registered lifecycle phases.

## Fresh maintained combat and cold rewards (2026-10-03)

The same ordinary level132 Warrior now completes `career-fresh-purple-maintain`,
`career-fresh-purple-win`, `career-fresh-purple-loot` and
`career-fresh-purple-reload`. All four phases pass on their first execution
against the unchanged private game hash above. The purchase checkpoint is now
historical: authoritative `revora_fresh_purple_probe` is stage4 and its snapshot
uses `FreshPurpleCombatSnapshot`, including all repair receipts and the nested
prior career.

Before summoning, ordinary unequip/repair/equip requests restore the shield,
blade, ring and fan for5,438 silver. The next process verifies cold maintenance,
enters the earned level-five house through its NPC, consumes the purchased
Spirit and submits sealed2032 confirmation twice. Exactly one54M-HP boss appears.
No character level, weapon requirement, damage amount, HP, result or loot roll
is supplied. The existing starter equipment remains equipped.

10,749 calculated melee attacks earn victory. Seven further ordinary repairs
cost148 silver in total; repeated repair requests do not charge again. Maintenance
uses existing silver-paid inventory repair while staying in the house and checks
that each operation creates exactly one receipt. The character remains alive.
Victory pays200 Study Points once and saves three uncollected loot entitlements.
After a process/database restart, real floor-item pickup requests collect all
three; duplicate requests do nothing. Exactly one drop is a bound P7 soul.

Final cold recovery compares the complete snapshot, verifies all three saved
item UIDs in the loaded bag and one paid200-point Study entitlement. Replayed
confirmation/reward polling creates no boss, floor loot or second reward.
The four redacted logs are `arena-process-career-fresh-purple-{phase}.log` in the
private directory named above. No live process was restarted or deployed.

Scope: travel/proximity, attack stamps and encounter time are controlled. The
synthetic sessions omit native player/screen timers, and the fixed encounter
clock does not establish natural attack cadence, retaliation, freezes, survival
or balance. Genuine6711 summon/house/HP/skill visuals and release remain pending.
The successful server earning path does not close those gates.

## Retained Arena-funded house hunt (2026-10-01)

The ordinary retained career completes `career-purple-buy`, `career-purple-win`,
`career-purple-loot` and `career-purple-reload` across four game/database
processes. All phases passed on their first execution. The journey continues
from [earned Arena Honor and Sword Master](SWORD_MASTER_ENCOUNTER.md#retained-earned-honor-journey-2026-10-01), without supplying Honor or a summon item.

The owner leaves its house through the installed portal and trades three existing
unbound reward items to its retained peer through ordinary mutual trade. The
exact item identities remain owned; no possessions are deleted to make room.
The Honor shop spends 35,000 of the remaining earned 992,000 Honor, leaving
957,000 in both saved mirrors and exactly one Banshee Spirit. Repeating Buy
charges nothing more. The exact item and purchase receipt survive abrupt exit.

After cold loading, the owner enters its earned level-five house through the
ordinary NPC, uses that Spirit and submits sealed 2032 confirmation. Duplicate
confirmation creates no extra actor or charge. Normal ranged combat takes 345
hits to defeat the 54,000,000-HP boss; the fixture supplies neither damage amounts,
a result nor a loot draw. Victory pays the owner's 200 Study Points and saves
three uncollected drops before abrupt exit. The next process restores the house
and loot, and sealed floor pickups, each replayed, collect exactly three items
including one bound P7. A final cold load compares the complete saved snapshot;
stale confirmation and reward polling grant nothing further.

The bag ends at 40 items. Clocks, approach positions and attack cooldown are
controlled. This proves the retained solo earning/reward route, not natural
combat difficulty, seven-skill timing/visuals, a new party journey or native
client acceptance. Earlier party/skill checks remain separate evidence. Active
fight interruption intentionally forfeits admission; this journey interrupts
after purchase and committed victory/pickup, without changing that rule.

No production change or rebuild was needed. Private executable SHA-256 remains
`1109CC262F8FBAA65A50451A60BE104FC2FB4A1FD885A72B11E444FE0C19E91E`.
Catalog installation, genuine 6711 acceptance and live restart/rollback remain
pending; no live deployment, restart or client release occurred.

The [matching private catalog candidate](PURPLE_BANSHEE_CLIENT_STAGE.md) now
repairs the server separator and describes the implemented contract. Four tests
and a whole-file audit pass; installation and native/live acceptance remain open.

The source candidate now connects Banshee Spirit purchase/use, confirmed house
admission, owned combat, atomic victory, Study credit and persisted floor pickup.
It supersedes earlier documents describing the admission/reward participants as
inactive. No live Host, production player data or installed client was changed.

## Ordinary player route

The loaded Honor shop sells Banshee Spirit 3004259 for 35,000 Honor. An eligible
ordinary authenticated owner brings the exact item into their own canonical
house and confirms a two-minute server offer. The offer freezes item identity,
stack, binding and house. Stale, malformed, changed-item and expired requests
cannot spend the item. Admission takes one unit and reserves one Pacific-day
attempt atomically, with no GM/name allowlist or inherited level-100 gate.

The admitted party contains the owner and up to four current teammates present
in the house. Their continued team membership is required. Only that party and
its registered nearby companions can fight or claim victory. Sword Master and
Purple cannot coexist in the same house; both real confirmation paths consult
the shared durable-owner admission lock. Each retains its own daily quota.

Revora policy: the encounter lasts thirty minutes. Owner departure, disconnect,
no remaining living fighter, timeout or interrupted process forfeits the attempt.
Startup fails closed if active records cannot be recovered. Same-process repeated
initialization does not retire a live actor. A bounded orphan sweep covers an
admission that committed but failed to publish its actor.

## Combat and reward boundaries

Owned family 9916806 derives identity/mesh from loaded family 7565 PurpleBanshee:
mesh 951, level 138, 54 million HP, raw melee attack 10,000 and defense 40,000.
The full HP serializer receives MaxHitpoints before presentation. Magic attack
7,500 and magic defense 200 are explicit Revora tuning. These are raw stats, not
a promise of identical final damage against every class or equipment set.

Original Purple skills are not verified. This house variant deliberately uses
Revora's reviewed frost implementation: Ice Bolt, Frost Claw, rotating Arrow
Hail, Ice Burst, Ice Shock, Avalanche and Metaplasia healing. It remains stationary
and casts from its controller, avoiding duplicate per-player monster-AI ticks.
Warnings last two seconds, recovery four; hail emits six one-second pulses and
turns ten degrees each pulse. Burst/Shock freeze for five/three seconds; heal is
capped at two percent of maximum HP. Geometry and timings are custom tuning.

Combat scans only its frozen party, rather than every connected player for every
house boss. Owner/item and encounter locks serialize healing, damage, victory
and cleanup. Player effects prevent ordinary movement, jumping and attacks;
cleanup removes only this encounter's state and preserves foreign Freeze flags.
Genuine 6711 skill visuals, freeze presentation and long HP acceptance remain open.

Victory commits the exact run and eligible reward-party snapshot with all loot
and Study entitlements. The summoner receives 200 Study Points, eligible teammates
50, including when a teammate lands the final blow. Two custom weighted P5/P6 or
refinery drops and an extra bound P7 follow the documented
[reward policy](PURPLE_BANSHEE_REWARDS.md). Generic monster loot is bypassed.
Purple receives no aliased SnowBansheeSoul/Snow Banshee Spirit Ball bonus; the
fixture verifies the player's accepted bead remains unchanged after victory.

## Validation scope and remaining acceptance

The isolated build and **241 checks** pass: 87 Purple Banshee, 79 Sword Master,
60 Snow Banshee and 15 quest-packet checks. Ignored logs under
`obj/chi-refill-validation/`: `purple-banshee-encounter-tests.log`,
`purple-encounter-sword-regression.log`, `purple-encounter-snow-regression.log`,
and `purple-encounter-packet-regression.log`. Validated build SHA-256:
`ae9f4c568671dc93bc60f3304c76289391f67a7e2b470f8504e8783e713f315a`.

The ordinary fixture buys from the real Honor-shop handler, builds/enters a house,
uses item confirmation, deals nonlethal/lethal damage, picks up the three rewards
through the sealed floor request and reloads the character through production
loaders. That earlier fixture seeds Honor; the retained journey above now separately
proves spending Honor earned through normal Arena combat.
A guest reaches the house through the real friend invitation/acceptance route,
joins the frozen team and defeats the boss for the distinct 200/50 Study split.

Other checks cover all seven combat phases, actual freeze movement/jump/attack
rejection and expiry, duplicate confirmation/death, foreign actors, companion
registration/range, reward-write failure rollback, reconnect, timeout, orphan
recovery, startup failure, house upgrades and both cross-boss native requests.
No native client playthrough is claimed by these server fixtures.

Remaining work: matching server/client catalog installation,
genuine summon/house/combat/HP/loot acceptance, and coordinated live installation
with backups and rollback. Earlier inactive-stage checkmarks remain historical;
the overall Banshee/native/Host milestone is still incomplete.
