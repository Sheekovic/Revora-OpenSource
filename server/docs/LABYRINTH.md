# Classic Labyrinth restoration

Implementation and 42 isolated checks pass, 2026-09-15, baseline `361c18f`.
Live client acceptance and deployment remain pending.

Achievement 11027 explicitly requires receiving Ancestor Box 721540 from Simon.
The [official classic guide](https://co.99.com/ipad/quests/labyrinth.shtml)
confirms the exchange of seventeen Cloud Diamonds and a 2,000 Virtue entry fee.
Its heading also mentions a permit, while its entry walkthrough describes only
the fee. This Revora classic edition selects the walkthrough's fee-only entry;
it does not claim to implement the later permit/chest quest 6467.

Simon 1152 is currently parked on map 5001 with no visible entry option. Restore
him to valid terrain near the installed 6711 location in Twin City (209,374).
Generals 1153–1156 remain on floors 1351–1354. The first three consume genuine
Sky/Earth/Soul Tokens to advance. Entry, paid visits, floor transitions and
recoverable delivery must be persisted; duplicate requests must not charge again.
Death or a voluntary exit ends the visit. A reconnect inside an active visit
restores that floor without charging again.

Installed diamond identities are 721533–721536; tokens are 721537–721539.
Existing Slinger/Bladeling/Minotaur drop rates are 10/10/5 percent; GoldGhost,
AgileRat and FiendBat token rates are 20/15/10 percent. AzureBird 3147 has been
repurposed as a level-140, five-million-HP monster and has no Star Diamond drop.
Keep its installed combat profile; restore a map-specific ten-percent Star
Diamond chance. Do not change the city summon sharing that family identity.

The selected Revora loot delivery is a saved personal satchel from actual owned
monster deaths during a paid visit. Materials are bound and use their existing
stack identities. Full bags preserve rolled loot for collection from Simon or a
general. Only map-matching families count. Existing general loot remains separate;
disable duplicate legacy quest-item drops on these four maps.

Simon exchanges seventeen Sun/Moon/Star/Cloud Diamonds for two bound Meteors,
four bound Meteors, a bound normal gem, or a bound Ancestor Box respectively.
The Cloud exchange and 11027 commit together. Box opening consumes its genuine
recorded UID and saves its result atomically. Revora box probabilities are
explicitly 60% two Meteors, 30% one normal gem, 10% one DragonBall; these are
economy choices, not recovered historical weights. Native tooltip corrections,
visual acceptance and deployment remain separate work.

Validation uses an ordinary non-GM character, actual owned monster deaths,
production jumps to every general, physical stack consumption and inventory
reload. It covers each floor and all box prize branches, wrong maps and entity
ownership, replayed confirmations, concurrent death/entry/opening, full bags,
failed entry/loot/step/exchange/opening transactions, death recovery and old-visit
callbacks. A committed fee followed by a failed teleport remains resumable after
reload without another charge. Virtue training increments and the existing other
Virtue spend share a dedicated Virtue lock with the new entry fee. Item ownership
locks precede that lock, and training does not acquire a teammate inventory lock.
Travel happens after releasing the Virtue lock.
The existing training message now reports the actual 3.83-per-level multiplier
used by its award instead of the unrelated 7.7 multiplier it previously displayed.

All 671 preceding regression checks pass. After the final Virtue-lock adjustment,
the affected Demon/Summer suites were repeated: 44 and 62 checks pass. The final
build also passes all 42 Labyrinth checks, including simultaneous training credits
and admission. No live gameplay test is implied by these isolated fixtures.

The private overlay changes nine description fields only. It builds on the
reviewed Summer overlay and preserves its sixteen prior item changes.
Three Labyrinth overlay checks and four Summer overlay checks pass. The combined
unsigned artifact is staged under ignored `obj/chi-refill-validation/labyrinth-overlay`.
No installed client or public download was modified.

- Input SHA-256: `c488ace14dffae62a6cebc95fcd48a9947a4accba5d129800d3e25c01cd6c210`.
- Output SHA-256: `d2a2014fb2acea3af4e15388eb2bec8ee32299a667478006306c94dc8037422a`.
- Isolated server SHA-256: `B879E86F9BCC2ED899DAAE132D423931D40B9C4CD1E90D998719CD4B2053D5E6`.

Live acceptance must verify Simon/general interaction, the satchel message and
stack display, monster difficulty, floor navigation, box use and achievement
presentation on the genuine client. Mission 6467 remains the separate later
edition; this classic exchange does not complete its unrelated chest objectives.

## Valuable final exchange (2026-09-21)

Every newly confirmed seventeen-Cloud-Diamond exchange now gives the bound
Ancestor Box plus one unbound catalog P7 soul. This is the final exchange that
awards Labyrinth achievement 11027. Sun/Moon/Star material exchanges and floor
passages retain their existing intermediate rewards. The new soul is uniform
across the loaded P7 catalog.

This service has no previously saved future reward contract: Simon reviews an
exchange and consumes diamonds on confirmation. New exchange receipts record
`reward_version=1`; old receipts default zero and are never reopened. Already
issued Ancestor Boxes retain their existing opening recipe. Box opening does
not mint another P7 soul. Each further exchange requires another seventeen
owned bound Cloud Diamonds and a fresh reviewed confirmation.

Both item UIDs, consumption and achievement commit together. Capacity accounts
for actual exhausted stacks, and a missing exchange receipt aborts reward
insertion. Simon's displayed offer includes the soul. Isolated build and 46
Labyrinth checks pass, including earning across all four floors, insufficient
space with remaining diamonds, final-exchange rollback without a saved soul,
replayed confirmation, all three box outcomes and item reload retaining three
souls for three paid exchanges. No native/live acceptance is claimed.
Log: `obj/chi-refill-validation/labyrinth-p7.log` (ignored).

## Retained-career entry prerequisite (2026-10-01)

- [x] Earn enough Virtue through ordinary team training and retain it across
  game/database restart: 2,106 points, trainee level 37.
- [x] Complete the retained career's four-floor route and final Cloud Diamond
  exchange, then verify its rewards and 11027 across restart (evidence below).

[Training controls and unresolved startup fault](CAREER_REBIRTH_JOURNEY.md#virtue-training-prerequisite-2026-10-01).
The prerequisite check does not supersede native acceptance gates.

## Retained career four-floor earning and restart (2026-10-01)

The retained ordinary Archer completes Simon's paid entry, all three general
passages, and the seventeen-Cloud-Diamond final exchange. The fee consumes
2,000 earned Virtue Points, leaving 106. A normal two-player trade transfers
owned unbound Refined Rainbow Gem UID 60170128 to the existing peer, making one
bag slot available without deleting possessions or changing their binding.

506 normal ranged monster kills earn the three passage tokens and seventeen
Cloud Diamonds. Loaded families are GoldGhost 3142, AgileRat 3145, FiendBat 3148
and Minotaur 3155. Production drop rolls and rates remain unchanged. The fixture
controls approach coordinates and attack cooldown; it does not supply damage,
monster death, quest material or Virtue. This is server-handler evidence, not
native movement/combat acceptance or a statistical drop-rate study.

After the fourth-floor satchel is collected, character save and abrupt process
exit leave seventeen bound diamonds and all three saved passage receipts. A
separate game/database restart preserves the exact nested career and Labyrinth
snapshot. Normal login recovery restores the fourth-floor admission; the North
General returns the player to Simon. Simon's reviewed final exchange consumes
the diamond stack and awards exactly one bound Ancestor Box and one bound P7
soul, recording achievement 11027 and one exchange receipt. Every unrelated bag
UID is retained. The box remains unopened; opening behavior is outside this batch.

Repeated confirmation, including a new offer with no diamonds, grants nothing.
A further cold restart preserves reward UIDs, prior career state, exchange and
achievement. All three phases pass without fixture recovery:
`career-lab-hunt`, `career-lab-claim`, `career-lab-reload`.
Checkpoint: `revora_career_lab_probe` done=true / `CareerLabSnapshot`.
Candidate SHA-256:
`B558BF74A339C92158604D48BD6ED9AB8F079055CF44545DCE13137CF8F9DF70`.
No production code, live restart, deployment or client release changed.


## Retained Ancestor Box opening (2026-10-03)

The retained character opens its actual Simon-issued box60495399 through
`ItemHandler.UseItem`, with provenance checked against the original kind3
exchange. An unforced roll25 produces two bound Meteors1088001. The consumed
box disappears, and both actual reward UIDs match `revora_lab_boxes.rewards`.
Repeating the same consumed object and a reconstructed stale UID yields nothing.

Exact nested prior career, belongings, balances, exchange and opening records
survive a separate game/database restart. Both
`career-retained-lab-box-open` and `career-retained-lab-box-reload` pass;
checkpoint `revora_retained_lab_box_probe` wraps the retained unarmed checkpoint.
Source: `tools/CheckCandidateRetainedLabBox.cs`. Private executable remains
`71F2DA5388C13BBA67FFA936ECBDCB141AE716D21EB2CA78DC151B4AFF380A77`.
No production change was needed. The earlier earned bound P7 remains accounted
for; opening this box does not grant another P7 or achievement. This closes the
previously excluded retained opening check for the observed Meteor branch;
earlier component tests retain gem/DragonBall/full-bag/fault scopes. Natural
reward probabilities and native opening presentation are not established by
one roll. No live deployment occurred.


## Fresh career four-floor earning and box opening (2026-10-03)

The existing ordinary Warrior UID1096322 continues from the verified team-training
checkpoint with2,005 earned Virtue. Seven owned items are deposited through the
Phoenix warehouse packet handler, preserving ownership and leaving32 bag slots
occupied. Simon charges exactly2,000 Virtue for admission; five remain throughout
all later phases. No token, diamond, gear, currency or kill credit is supplied.

Normal melee calculation and unforced loot rolls earn all three passage tokens
and17 Cloud Diamonds. Four floors require212 kills and972 attacks; cumulative
floor totals are2/4,7/19,23/107 and212/972 kills/attacks. Each general collects the
saved satchel and consumes its actual token to advance. All three transition
receipts and the unexchanged17-diamond stack survive game/database restart.
Normal login recovery restores the paid fourth floor without charging again.

The final general returns the character to Simon. His ordinary confirmed
exchange consumes the diamond stack and gives one bound Ancestor Box plus one
bound P7 soul, with exactly one11027 classic-labyrinth-v1 receipt. The box and
soul survive another restart. Sealed ordinary item use opens the earned box
with an unforced reward draw; its saved result is checked against the existing
Meteor/gem/DragonBall pool, exact result count, bound ownership and consumed box
UID. A final restart preserves the entire nested career, including the trainee's
saved possessions/profile, five remaining Virtue, reward/exchange/box ledgers
and all prior earning. Exchange and consumed-box replay issue no extra reward.

`tools/CheckCandidateFreshLab.cs` phases `career-fresh-lab-earn`,
`career-fresh-lab-claim`, `career-fresh-lab-open` and `career-fresh-lab-reload`
all pass on their first run. Current checkpoint `revora_fresh_lab_probe` is
`opened`. Snapshot helpers now accept a character UID so the inactive trainee
can be compared directly from saved data without installing another session.
Game SHA-256 remains
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.

Scope: NPC/monster approach and attack cooldowns are controlled. When a target
family has no living member, the existing map respawn callback is invoked with
advanced time; loot rolls remain unforced. This does not prove natural duration,
retaliation, combat balance or native navigation. Player pet/screen timers are
omitted and prerequisite terrain is staged privately as in the prior batch.
No game-source change, live restart, deployment or native-client acceptance.
