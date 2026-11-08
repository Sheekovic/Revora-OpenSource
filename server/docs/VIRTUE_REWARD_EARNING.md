# Virtue rewards and saved balance ownership

2026-09-15; baseline `a129880`. Ordinary-player source implementation and isolated
validation are complete. Native acceptance and production deployment remain open.

## Evidence and retained Revora policy

The official [Virtue Points guide](https://co.99.com/guide/guides/virtuepoints.shtml)
locates Shelby in Market at (240,249) and gives the exchange rates: 5,000 points
for one Meteor, or 15,000 for one EXP Ball's worth of experience. A read-only
query of the installed server confirms NPC 30000, Shelby, map 1036, (240,249),
mesh 7624. No new NPC identity or client patch is needed.

The installed achievement catalog identifies 10110 as exchanging Virtue for a
Meteor. Its SHA-256 is
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.
The installed item catalog, SHA-256
`d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a`,
identifies 720757 as a ten-EXP-Ball pack. The previous hidden 15,000-point branch
gave that pack, while the advertised Meteor exchange had no implementation.
The replacement delivers the documented single-ball experience directly through
the existing transactional experience engine, with its progression, level-up,
mentor and reincarnation handling. This change does not independently reverify
the complete historical EXP curve or Virtue earning formula.

Shelby's existing Revora gold-wallet service remains available: 200,500,000,000
silver purchases item 723723, whose existing server use returns 200,000,000,000.
The 500,000,000 fee is retained. These are explicitly custom Revora values:
the installed item's description instead names a 500,000,000-silver reward.
This milestone preserves the existing economy and does not change the wallet
item-use handler or claim its native description is reconciled. Future wallet
redemption/description work must consider outstanding player-owned bags.

## Completed behavior

Shelby's short dialogue exposes the two documented Virtue choices, a balance
check and the existing wallet service. Every purchase requires a fresh matching
one-minute offer. Confirmations cannot substitute another recipe, carry across
sessions or be replayed for another charge. Another deliberate quote allows
another exchange. All services require an ordinary canonical, living player
beside Shelby, outside a booth or active trade.

Meteor payment, creation of the real unbound item, receipt and achievement 10110
commit together. Experience payment, the calculated reward and its associated
progression writes share the same transaction and produce no item pack or
Meteor achievement. Full bags block item rewards before payment; direct EXP
still works with a full bag. Level-capped or cursed characters retain their points.
The preserved wallet purchase now commits its silver debit, item and receipt
atomically. Inventory, experience and achievement updates follow the commit.
A missing reward insert, suppressed receipt, suppressed achievement or stale
balance rolls back the transaction and fences the session for reload.

`VirtueBalance` records the strict nullable uint preimage loaded from the
character row, separately from legitimate earned points that have not yet been
saved. Spending can commit those pending gains and the debit together. Full
character saves compare the prior durable Virtue value and cannot overwrite an
external credit or refund an exchange. Negative/out-of-range stored values are
rejected instead of silently wrapping. New-character bootstrap retains its
existing initial-save path.

Character saves take the existing item lock and then the existing Virtue lock;
training credits still need only the Virtue lock, avoiding an added lock on
another character's inventory. Labyrinth's 2,000-point admission uses the same
saved balance transaction. Resuming an existing paid expedition does not advance
the saved Virtue preimage or charge again. The existing Labyrinth fixture now
loads a real balance preimage before simulating unflushed training gains.

## Validation and remaining gates

The isolated Debug build SHA-256 is
`957d721d47726478182ba73a3a48e49a6a38c2c9ef2724bfd4b334da0ca8879d`.
It passes **1,631 checks**: Shelby 34, experience 452, cosmetics/dyes 179,
progression 74, titles 91, Labyrinth 43, mentor admission 55, mentor departure 34
and combat 669. Three achievement-audit fixture tests pass. Database fixtures run
sequentially in `revora_potency_validation`, with no production player writes.

Shelby fixtures call the actual NPC handler with non-GM characters and cover all
three recipes, exact/insufficient fees, duplicate and mismatched confirmations,
expiry, full bags, pending gains, failed reward/receipt/achievement inserts,
stale/invalid/nullable balances, concurrent earning/spending/full saves and
fresh-session reload through the production character/item loaders. The
post-login progression refresh restores the earned achievement packet. External
website grant providers are not invoked by those isolated reload fixtures.

- [x] Restore the official Meteor and single-ball experience exchanges.
- [x] Atomically earn and restore 10110 after an actual Virtue-to-Meteor exchange.
- [x] Preserve and transactionally protect the existing wallet purchase.
- [x] Protect loaded Virtue balances across saves and Labyrinth admission.
- [x] Verify ordinary-player payment, persistence, replay and rollback behavior.
- [ ] Validate Shelby dialogue, immediate rewards and achievement display in native 6711.
- [ ] Deploy the coherent server build and initialize its receipt table in production.
- [ ] Reconcile the separate custom wallet redemption/description mismatch with outstanding bags preserved.

Producer inventory is **110/337**; **227** entries still have no trusted producer.
This does not establish a per-character route to 320 achievements. No live
restart, deployment, public patch, website publication or client modification
occurred. Preserve the new receipts and balance evidence during any future rollback.

## Retained mentorship, team training and Shelby exchange (2026-10-02)

The retained ordinary character4294907001 continues from its Supervisor checkpoint.
Existing trainee4294908501 retains its earlier saved training progress. Normal2065
request/accept packets establish mutual mentorship, giving10124 to the main mentor
and10125 to the trainee. Immediate acceptance replay is unchanged. Full game and
database restart preserves the relationship before further hunting.

Ordinary team creation/invitation/acceptance makes the mentor leader. Calculated
bow attacks kill existing monsters with the trainee nearby; actual team EXP raises
the trainee's level, and each increase is checked against the production Virtue
formula. The first bounded batch of1,000 family18 kills reaches level45 and1,343
Virtue but does not meet the goal. That progress is retained, not reset. Both
players then enter through PotencyGuide900000018. A second1,000-kill batch against
PotencySerpents reaches level59 and4,098 Virtue; another526 kills reaches level63
and5,022 Virtue. These are persisted partial batches, not three fresh careers.
The guide's ordinary return option brings the team home.

A full restart precedes the reward. Stone City storage frees one bag slot. Shelby
30000 in Market1036, options9/10, consumes exactly5,000 Virtue and delivers one
unbound Meteor with a saved receipt. The remaining22 points, exact Meteor UID,
mentor relationship, trainee progress/contributions and previous nested career
survive another actual restart. Replayed confirmation and acceptance change
nothing. Main achievements10110,10124 and10317 remain saved;10125 is on the trainee,
not yet earned by the main character as an apprentice.

The training checkpoint initially omitted achievement refresh. On reload the
normal observable-state producer added10317 for reaching5,000 Virtue. A complete
snapshot comparison found that single receipt was the only change; a one-time
exact-hash recovery retained it without inserting evidence. The helper now calls
the normal refresh before checkpointing. The recovery branch was removed and
the cleaned cold-reload phase passes.

Phases `career-retained-mentor-{admit,train,claim,reload}` pass on unchanged private
candidate SHA-256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
Final probe `revora_retained_mentor_probe` is step3. The two earlier training caps
are recorded as incomplete attempts with preserved progress, not passing phases.
NPC/monster approach, cooldowns and respawn timing are controlled. Earlier trainee
starting qualification and career funding retain their documented provenance;
this does not establish natural hunt duration, survivability, native UX or live
acceptance. No production code/build/deployment changed.


## Fresh career earned Labyrinth fee (2026-10-03)

The fresh level124 Warrior UID1096322 continues from the complete Snake Islands
checkpoint with zero Virtue Points. A separate ordinary account and character,
FreshPupil1 / UID1096323, are created through AccountTable and CreateEntity;
the trainee starts at level1, zero rebirths and receives the normal bound starter
pack through its login handler. No level, money, equipment or Virtue is assigned
by the fixture. The trainee's starter pack remains unopened.

Sealed ordinary team create/invite/accept packets put the career character in
charge.452 existing Birdman kills through normal melee damage,452 attacks,
raise the nearby trainee to level33. Each level transition checks the exact
production Virtue calculation; the leader earns2,005 points. Ordinary team exit
packets dissolve the team. Both saves and a fresh game/database restart preserve
those levels, exact Virtue, both characters' possessions, creation/starter records
and the leader's complete nested career. The2,000-point Labyrinth admission is
now funded through earning; this batch does not claim Labyrinth completion or
Shelby's reward exchange.

`tools/CheckCandidateFreshVirtue.cs` phases `career-fresh-virtue-earn` and
`career-fresh-virtue-reload` pass on their first run. Current checkpoint is
`revora_fresh_virtue_probe`, stage `earned`. Neither prior checkpoint is reset.
Game SHA-256 remains
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.

This uses offline ordinary-player adapters, controlled map/monster approach,
nearby trainee positioning and attack cooldowns. Normal map respawn timers run;
player pet/screen timers are omitted. One-hit kills do not prove combat balance,
natural training duration, retaliation or native client experience. Private
prerequisite terrain staging remains unchanged. No live deployment or client
patch; authenticated/native creation is not claimed.
