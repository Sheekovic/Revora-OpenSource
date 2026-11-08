# Career starting boundary

Reviewed2026-10-03 against the current workspace. This is a prerequisite audit,
not another earning test or a completion claim.

The retained character4294907001 was created by the offline `Player` helper in
`tools/CheckCandidateArenaLifecycle.cs`. Its initial in-memory level is140 and
class15; the helper inserts a database profile at level100/class15 directly.
`CareerFirstRite` in `tools/CheckCandidateCareerRites.cs` reloads it, raises it
to120, saves it and invokes `EliminationHit` on a daily boss to obtain the first
funding. Those actions do not exercise character creation, initial class
promotion, levelling to120 or calculated combat for that original purse.

Later ordinary earning and cold-reload proofs remain useful and must be
preserved. Replacing one later controlled battle with calculated combat cannot
retroactively turn the initial qualification/funding into a fresh-player route.
Neither322 stored receipts nor321 candidate identities certify that claim.

Production `Database/CharacterCreation.cs` inserts level1 and saves the account/
character mapping and creation receipt transactionally. The workspace's
`docs/REVORA_STARTER_PACK.md` already records owner-defined rewards: opening
class gear,50,000 bound CPs and1M silver, original-life level milestones,
first-rebirth gear/garment, and the final second-rebirth bundle. It is incorrect
to treat missing starter-product instructions as the present blocker.
The custom pack/source, supporting consumers and documentation are owner WIP;
the recent private roadmap build intentionally uses a committed-source overlay.
Do not silently claim that binary validates every staged starter change.

## Next foundation check

1. Freeze and identify the exact creation/starter source inputs being tested;
   preserve owner edits and keep this separate from the existing retained UID.
2. Create an ordinary private account and submit creation through the production
   handler/transaction, starting at its real level/class with no direct profile
   insert or added purse. Authentication may remain a separately disclosed gate.
3. Verify1002(327,303), one starter entitlement, the opening reward's exact
   binding/currency/requirements, duplicate requests and cold recovery.
4. Use ordinary earning for initial promotion/levelling and first funding before
   connecting a new career to later tested routes. Keep clocks/proximity and
   any other fixture controls explicit. Do not reuse the old character's receipts
   as new-character credit, reset them, or call this foundation alone a320 proof.

The current retained checkpoint remains `revora_retained_lab_box_probe`.
Native6711 acceptance and live deployment stay pending. No owner WIP, player
state, build, service, database or public release changed in this audit.

## Fresh production creation and starter opening (2026-10-03)

- [x] Private ordinary account creation through `AccountTable.Insert`, using an
  ephemeral verifier credential; no plaintext credential persisted or logged.
- [x] Production `EntityTable.CreateEntity` transaction, ordinary account, Trojan
  selection2/body1003; generated UID1096322, level1/class10, map1002(327,303).
- [x] Creation entitlement delivers one bound727990; repeated delivery changes nothing.
- [x] Cold process opens the delivered pack through `ItemHandler.UseItem`:
  seven bound Super items118009/130009/120009/150009/160019/410009/420009,
  each +12/-7/two Super Dragon gems; pack remains for later milestones.
- [x] Opening adds exactly50000 bound CPs and1000000 silver. Actual initial
  candidate balances are10000 CPs, zero bound CPs and100000 silver, leaving
  10000 CPs/50000 bound CPs/1100000 silver. No fixture purse or gear was added.
- [x] Another cold process restores the exact saved profile, items, creation,
  delivery and claim rows. Repeated delivery/opening does not add rewards.
- [ ] Native creation/login delivery, equipment presentation and client acceptance.
- [ ] Unsupplied levelling/promotion and subsequent career earning.

`tools/CheckCandidateFreshStarter.cs` adds three private runner phases:
`career-fresh-starter-create`, `career-fresh-starter-open`, and
`career-fresh-starter-reload`. The first invocation successfully created the
character but stopped on a wrong zero-silver expectation; inspection established
100000 starting silver. The fixture resumed the same saved creation rather than
resetting or recreating it. All three phases subsequently passed. The new
checkpoint is `revora_fresh_starter_probe`, separate from the retained career.

Scope: the fixture uses a disconnected transport and timer-free screen. It
submits a decoded creation request directly to production `CreateEntity`, loads
production entity/items, and explicitly invokes starter `OnLogin`. It does not
exercise SRP/DH, the wire creation dispatcher, `GeneralData.DoLogin`, a native
client or every class/rebirth milestone. Gear is not auto-equipped; catalog
requirements are unchanged. No new320-achievement count is claimed.

The test uses a private frozen overlay of the staged starter work, not a claim
that uncommitted owner changes are published. HEAD creation/Thread/ItemHandler
receive the minimal starter initialization/dispatch hooks; the three staged pack
sources are frozen without changes. Exact source, executable, catalog, fixture
and successful log hashes are recorded in
[the provenance record](research/FRESH_STARTER_VALIDATION.json).
Only the private candidate executable and catalog were refreshed; hash-named
preimages and manifest backups were preserved. No live deployment occurred.

## Fresh level1 to first promotion (2026-10-03)

- [x] Continue UID1096322 from the exact saved starter checkpoint, without
  raising level/class, inserting items, supplying currency, calling the EXP
  grant API directly or changing monster HP/damage.
- [x] Equip the level1 starter ring through the ordinary equipment handler;
  use the starter blade/necklace/boots as level eligibility is reached.
- [x] Defeat136 installed family1 Pheasants (40 HP each),136 calculated melee
  attacks, reaching level15 with568 remaining EXP. Each successful attack is
  followed by a real monster death and production EXP calculation.
- [x] Use Trojan trainer10022's ordinary offer/confirmation: class10 to11 and
  achievement10819 with source `class-promotion-v1`. Equip the starter coronet
  and armor after promotion, without changing their normal requirements.
- [x] Separate process restores exact saved items, profile/EXP, starter receipts
  and achievement evidence. Checkpoint: `revora_fresh_training_probe`.
- [ ] Later promotions, ordinary qualification/funding and compatible320 career.

`tools/CheckCandidateFreshTraining.cs` phases `career-fresh-training-earn` and
`career-fresh-training-reload` both pass on the same frozen starter executable.
This test controls player proximity, attack cooldown timestamps and school
transport/NPC proximity. It does not test native walking, portal navigation,
monster retaliation, real-time combat duration or client equipment display.
The initial boots request supplies slot6; the equipment handler normalizes
non-weapon destinations to the item slot. Later successful use supplies slot8.
No monster spawn is added, no kill RNG is forced, and no floor loot is picked up.
The prior retained-career checkpoint remains untouched. Logs and source hashes
are in [the training evidence record](research/FRESH_TRAINING_VALIDATION.json).

The stronger cold audit additionally verifies all six equipped IDs, unchanged
50000 bound CPs/1100000 silver, and10926 CPs. The926 earned CPs came from the
installed `MonsterTable` universal kill-CP path (unforced random bonuses); this
is the new character's first combat-earned funding. The final balance lies
within the configured aggregate minimum/maximum for136 kills. An initial
additional assertion incorrectly expected no CP change; the profile was not
edited to satisfy it. Replaying trainer confirmation without a fresh offer
leaves the full snapshot unchanged. The earn phase ran before those extra cold
assertions were added; provenance records both fixture versions.

## Fresh level40 promotion and first starter milestone (2026-10-03)

- [x] Continue the exact level15/rank11 checkpoint with its earned belongings.
  Calculated melee defeats438 more installed ordinary monsters on maps1002/1011;
  no HP/damage/EXP grants or spawn edits. All438 kills take one attack with the
  already earned/eligible starter equipment. Level40 ends with10512 EXP.
- [x] Ordinary trainer10022 offer/confirmation advances rank11 to12 and records
  achievement10820 exactly once. The next material promotion is not claimed.
- [x] Open the retained starter item at the earned level40: one bound +12
  steed300000 with an unforced permitted color, no gem sockets and saved bless0.
  The opening leaves currencies unchanged and retains the pack for level70.
- [x] Separate process restores exact items, profile/EXP, starter claim and
  achievement evidence. Repeated starter use and trainer confirmation change
  nothing. Final CP balance14704 includes3778 newly earned kill CPs in this leg.
- [ ] Continue level70+ milestones and later promotion/material routes.

`tools/CheckCandidateFreshMilestone.cs` phases `career-fresh-milestone-earn` and
`career-fresh-milestone-reload` pass. The current fresh-character checkpoint is
`revora_fresh_milestone_probe`; earlier fresh snapshots are historical preimages.
It journals progress after each kill for bounded recovery rather than resetting
player progress. Eligible targets are installed normal monsters within five
levels above the character, selected from an explicit family-name list.
Transport between hunting maps/school, proximity and attack timestamps are
controlled. Retaliation, native navigation, actual duration and live/client
acceptance remain unproven; no native or production deployment occurred.
[Exact build/fixture/log evidence](research/FRESH_MILESTONE_VALIDATION.json).

## Fresh level70 and material promotion (2026-10-03)

- [x] Continue the exact level40/rank12 checkpoint.1261 additional installed
  ordinary monster kills through2215 calculated melee attacks earn level70,
  with11175 remaining EXP. No supplied levels, damage, HP, currency or loot.
- [x] Buy one unbound Emerald1080001 through the installed CP mall's sealed1009
  request, charging its catalog price. Trainer10022 consumes that exact UID
  for rank13 and achievement10821, with no extra CP charge.
- [x] The retained starter item grants one bound +12 crop203009 for earned
  level70, with no blessing or sockets. No currency is granted or charged.
- [x] Separate process restores exact profile/EXP, items, promotion evidence
  and starter receipts; repeated starter and trainer requests pay nothing.
  Final CP balance25414 includes this leg's kill income minus the Emerald.
- [ ] Continue level100+ rewards and remaining material promotions.

The current fresh checkpoint is `revora_fresh_seventy_probe`. Its recorded
Emerald UID/price supplement the saved character/item/receipt snapshot.
`tools/CheckCandidateFreshSeventy.cs` adds `career-fresh-seventy-earn/reload`.
The selector uses installed non-boss/non-guard family IDs1..30 on the five
ordinary hunting maps, levels30 through at most five above the character,
without injecting spawns or forcing random rewards. As in prior legs, transport,
proximity and attack timestamps are controlled; native navigation, retaliation
and real-time pacing are not certified. Private starter-overlay and native/live
release boundaries remain unchanged.
[Exact source/build/log evidence](research/FRESH_SEVENTY_VALIDATION.json).

## Fresh level100, Meteor promotion and three-item milestone (2026-10-03)

- [x] Continue the saved level70/rank13 character. Buy/use ten ordinary EXP
  Balls723700 at27 CPs each through the sealed mall request and production
  consumer. No clock override or allowance reset; these reach level74.
  Reusing consumed item objects does not add EXP. The daily receipt survives.
- [x] Equip the already owned second starter weapon through the ordinary
  handler, then3520 installed monster kills/11830 calculated melee attacks
  reach level100 with132448 EXP. No supplied damage, level or currency.
- [x] Buy one Meteor1088001 at the installed3-CP price; trainer10022 consumes
  its exact UID for rank14 and achievement10822 without another CP charge.
- [x] Starter milestone100 pays exactly three bound +12 items201009/202009/
  204009. The fan has two Super Thunder gems and bless1; tower has two Super
  Glory gems and bless1; wing has one of each and bless0. Currency is unchanged.
- [x] Separate process restores exact profile/EXP, items, starter/promotion
  evidence and the saved EXP Ball day/count. Starter/trainer replay pays nothing.
  Final balance52081 CPs includes26940 kill CPs less270 for balls and3 for Meteor.
- [ ] Earn level110/120, Moon Box/final promotion and later career requirements.

Current fresh checkpoint: `revora_fresh_hundred_probe`. Earlier fresh checkpoints
are preimages, not reset targets. `tools/CheckCandidateFreshHundred.cs` runs
`career-fresh-hundred-earn/reload` on the unchanged frozen starter executable.
Monster target, movement and timing controls match the preceding combat leg;
no monster HP, damage, reward RNG or respawn is supplied. The live/calendar
EXP Ball limit remains intact. Native/client, retaliation and real-time pacing
limits remain open, and no production deployment occurred.
[Exact source/build/log evidence](research/FRESH_HUNDRED_VALIDATION.json).

## Fresh Moon Box quest with unforced tactics (2026-10-03)

- [x] Continue the exact saved level100/rank14 progression checkpoint. Equip
  its earned fan, tower, wing, steed and crop through ordinary equipment handlers.
- [x] Fortune Teller/Maggie route uses production randomness unchanged:60 tactic
  visits,114 ToughHorn-family kills/226 calculated melee attacks, and8 surrendered
  runs before collecting all six tokens in one successful run. Early Death
  surrender removes the run's tokens; the character starts again normally.
- [x] Trade the actual six tokens for Soul Jade; visit the installed Life ghosts
  until the selected ghost accepts it. No DB-selected ghost shortcut or supplied
  token/jade is used. No drop, monster HP or damage is overridden.
- [x] Earn exactly one unbound Moon Box721090 and one bound P7 soul, matching
  the two saved reward UIDs; record11022 with `moon-box-v1` once. This repeatable
  quest has no daily schedule; it is not classified as a daily P7 reward.
- [x] Cold process restores exact profile, inventory/equipment, quest run/results
  and achievement evidence. The box stays unconsumed for final promotion.
- [ ] Reach the final promotion's level requirement and consume the earned box.

`tools/CheckCandidateFreshMoon.cs` runs `career-fresh-moon-earn/reload`.
Current fresh checkpoint: `revora_fresh_moon_probe`. The old fresh snapshots
remain preimages. Each quest action/kill is journalled without erasing past runs.
Controlled player/NPC proximity and attack timestamps remain; navigation,
retaliation, real-time duration and native/live acceptance are not certified.
This supplements earlier Moon Box tests that supplied RNG or lethal damage;
it does not rewrite their provenance or certify the whole320 career.
[Exact build/fixture/log evidence](research/FRESH_MOON_VALIDATION.json).

## Fresh first-life foundation through level 120 (2026-10-03)

- [x] Purchase one DragonBall at the installed 215-CP price and consume that
  exact UID through the ordinary daily EXP NPC 3825. The actual daily receipt
  is preserved; neither the clock nor the daily allowance is reset. This reaches
  level 105. Repeated confirmation adds nothing.
- [x] Continue calculated combat with the earned equipment. This leg records
  11,413 installed monster kills and 12,295 melee attacks, reaching level 120
  with 414,753 EXP. Batches stop after 2,500 kills or 150 seconds and resume from
  the exact cold snapshot. Normal respawn timing and reward RNG remain intact.
- [x] Consume the Moon Box earned by the preceding quest through trainer 10022.
  The character becomes Trojan rank 15 and records achievement 10823 once;
  promotion adds no CP charge. Final promotion was completed at level 114.
- [x] The retained starter item pays its level-120 bound P7 choice pack 3006233.
  The pack remains unopened: selection/consumption is a separate check. The
  main starter item remains for rebirth milestones.
- [x] Final earning/reload phases restore exact profile/EXP, items, promotion,
  Moon Box results, starter claims, EXP Ball allowance and DragonBall receipt.
  Repeated starter/trainer requests add nothing. Final CP balance: 132,528.
- [ ] First/second rebirth, their starter rewards and later compatible career.
- [ ] Native login, display, combat pacing/retaliation and coordinated release.

The fresh first-life foundation now has a production level-1 origin, calculated
combat/legitimate purchases, all five Trojan promotions and starter milestones
through 120. It contains no directly supplied level, class, purse, equipment,
quest material, monster HP or lethal damage. Movement/proximity and attack
cooldowns remain explicitly controlled; it is not native or live acceptance,
nor proof that all 320 achievements are jointly earned.

Current fresh checkpoint: `revora_fresh_advanced_probe`. The fixture is
`tools/CheckCandidateFreshAdvanced.cs` (`career-fresh-advanced-earn/reload`).
An earning marker with `stage=training` certifies a resumable checkpoint only;
completion requires `stage=claimed` plus the final cold reload. Four archived
batch markers precede the final level-120 run.

Two initial target-selection stops exposed fixture assumptions: ordinary Bird
Island variants include IDs above 30, and live target availability requires the
lower-level families too. The selector now covers non-boss/non-guard IDs 1..100
at level 80+ on ordinary hunting maps, up to five levels above the player.
Already saved kills were retained. Final promotion sequencing was moved ahead
of level 120 as soon as eligible. A later replay probe addressed the trainer
from Bird Island after the reward was committed; it was corrected to travel to
school before replay. No character or receipt was reset to make tests pass.
[Build, fixture and batch evidence](research/FRESH_ADVANCED_VALIDATION.json).

## Earned P7 selection and item-menu routing repair (2026-10-03)

- [x] Open earned pack61696829, cancel/navigate safely, and reject a lost offer
  after restart. Reopen and select page2/option2 to obtain bound P7 soul800618.
- [x] Exact pack consumption, one receipt, unchanged currency, stale/repeated
  requests and cold reward ownership pass on the fresh character.
- [x] Diagnose and repair the world-NPC fallback blocking item-menu selections.
  A separate supplied-item ordinary regression character reproduces the missing
  route, then passes full packet-dispatch checks and cold persistence after fix.
- [ ] First/second rebirth and their starter milestones; native client acceptance.

Current fresh checkpoint is `revora_fresh_choice_probe`; `revora_choice_wire_probe`
belongs only to the separate regression fixture and is never earning credit.
The private candidate adds the staged item-use/dialogue seams for the initial
fresh test and then the corrected network route. The source batch publishes the
minimal reviewed P7 consumer, registration and wire routes; the broader owner
starter work retains its separate staged status. Native/live gates remain open.
[Routing change and exact validation scope](P7_CHOICE_PACK.md).

## Fresh first rebirth and steed compatibility repair (2026-10-03)

The same production-created ordinary character UID1096322 now completes first
rebirth from its earned level120 Trojan rank15. It buys the catalog-listed
Celestial Stone721259 for30 earned CPs, takes Sereth's Trojan/slender/random-gem
route, and returns at level15/class11/reborn1. No level, class, material, purse,
rebirth reward or random outcome was supplied for this transition. Controlled
NPC proximity and portal positioning remain fixture limitations.

This exposed an actual blocker: `ConquerItem.Bless` and `NextRed` share wire
byte31. The equipped starter steed had red150 but stored blessing0. First rebirth
tried to compare/update blessing for every equipped item and rolled back with
`Rebirth equipment changed.` The fix writes only lowered equipment, returned
off-hands and the selected blessed piece. The existing locked whole-row
preimage still protects all belongings, including unchanged mounts/talismans.
The successful retry preserved the steed's complete saved row.

The failed transaction left the stone owned and rebirth uncommitted. Recovery
compared the prior checkpoint, allowing only the observed30-CP purchase,
controlled travel and its new10612 travel receipt. No character reset, refund,
replacement material or second purchase occurred. Rebirth then consumed the
stone once and issued its permanent unbound Armor of Holy Light187405, random
Super gem and class weapon. The separate first-rebirth starter claim issued
seven bound +12/-7/two-Super-Dragon-gem pieces and bound Garment Pack723981.
Opening that pack with unchanged production RNG yielded permanent bound
five-star garment189135, blessing0. These starter consumers remain the reviewed
frozen private owner overlay; this batch does not publish all staged starter work.

Three separate game/database processes verify the rite, starter claim and cold
reload. Saved profile, possessions, skills, proficiencies and receipt snapshots
match after restart. Repeating the rite, starter use and consumed garment-pack
use gives no duplicate reward. Balances remain132498 CPs,50000 bound CPs and
1100000 silver after the30-CP stone purchase. The current fresh checkpoint is
`revora_fresh_rebirth_probe`; older checkpoints remain historical preimages.

The new harness is `tools/CheckCandidateFreshRebirth.cs`; reproducible phase
names and exact artifact hashes are in
[the validation record](research/FRESH_REBIRTH_VALIDATION.json). This proves a
purchased-stone route, not fresh mining/Clean Water crafting. NPC requests and
starter/garment consumers are dispatched inside the private server; this is not
native login, packet presentation, live deployment or full-career completion.
Second-life advancement and second rebirth remain open.

## Fresh second-life promotions and repeated Moon Box (2026-10-03)

UID1096322 continues from the saved first-rebirth checkpoint through level110
and the four remaining Trojan promotions, reaching class15. Three bounded
combat processes total5148 ordinary monster kills and6159 calculated melee
hits. The promotion route purchases/consumes an Emerald and a Meteor at their
loaded prices, then earns a new Moon Box:41 tactic visits,102 calculated-combat
kills/204 hits and4 surrendered attempts with production RNG unchanged.
The new quest result has its own run and owned reward identities; the previous
first-life Moon Box remains consumed. The new bound P7 quest reward survives
reload, and the new Moon Box is consumed for the second-life final promotion.

The character used no supplied levels, EXP, materials, combat damage or purse.
Its prior level120 rebirth does not meet the first-life level130 recovery rule.
Daily EXP Ball/DragonBall limits were not reset, clocks were not advanced and
no additional daily EXP purchase was used. Controlled target proximity,
teleports and attack-cooldown timestamps remain explicit harness controls;
these tests do not establish natural travel time, retaliation or native pacing.

Separate game/database processes verify level110/class14, repeated quest
rewards, then the final class15 promotion and its exact saved state. Replayed
promotion/starter requests preserve possessions and purse. The existing
promotion achievement stays single, and the first-rebirth starter claim count
remains one: the first-life40/70/100/120 milestones are not paid again.
The current fresh checkpoint is `revora_fresh_second_promotion_probe`, with
173959 CPs. Earlier fresh checkpoints are historical preimages. The older retained
career is unchanged. Evidence hashes and phase names are in
[the validation record](research/FRESH_SECOND_LIFE_VALIDATION.json).

This batch adds earning/persistence evidence only; game SHA256 remains
B90E35E9F3281B416DA579BA2ACC568BE1997BDD17A9648D6071CC0B5F1C8826 with the
previously documented frozen starter overlay. Level120, second rebirth,
remaining compatible-career work and native/live gates remain open.

## Fresh second rebirth and final starter closure (2026-10-03)

The same ordinary UID1096322 reaches second-life level120 through8617 further
monster kills/9434 calculated melee hits. After7500 of those kills, it buys the
listed one-hour EXP Potion723017 for27 earned CPs and consumes it through a
sealed1009 packet. Its saved3600-second grant and unique receipt survive the
next process; replay does not issue another bonus. The daily EXP purchase limits
and all clocks remain unchanged. This does not verify native countdown pacing.

An888-CP Exemption Token purchase creates the owned payment receipt. Sereth's
Warrior route then consumes that token and records first-life15/120,
second-life15/120, new class21/level15/reborn2. An offer lost across restart
cannot complete until renewed. The rite grants its bound P7 soul and10316;
it does not falsely grant the still-gated Abyss quest achievement11031.

The final starter claim is exercised through sealed item packets, pays exactly
100000 bound CPs and5000000 silver, and issues seven bound Warrior pieces
(+12/-7/two Super Dragon gems) plus two bound P7 choice packs. Both choices are
consumed through sealed1009/2032 dispatch, remain bound, and reject replay.
The retained starter item is deleted and `revora_starter_pending` closes.
Re-login delivery and replay requests do not recreate the item or pay again.
A separate cold process preserves235126 CPs,150000 bound CPs and6100000 silver.

The extra equipment test initially assumed all seven standard pieces could be
worn at15. That assumption was wrong: shield900009 is a standard level40 item,
consistent with the owner's explicit policy to retain standard requirements.
Helmet, necklace, armor and sword had already equipped when that assertion
stopped the fixture. A full-row audit proved the only saved changes were the
final payout/closure, two P7 choices and those four ordinary swaps. Recovery is
hash-gated to that exact preimage/state; no character state or rewards were
reset/reissued. Ring and boots then equipped, and the level15 shield refusal
was retained. A further157 calculated kills/hits earn level40; the shield now
equips normally and all seven owned final pieces survive a separate restart.
No production requirement or catalog row was changed to make the test pass.

Current fresh checkpoint: `revora_fresh_third_shield_probe`, class21/level40,
reborn2,235993 CPs,150000 bound CPs and6100000 silver. Older checkpoints are
historical preimages. Exact hashes, phase logs and the recovery audit fingerprints
are in [the validation record](research/FRESH_SECOND_REBIRTH_VALIDATION.json).
Game SHA256 remains B90E35E9F3281B416DA579BA2ACC568BE1997BDD17A9648D6071CC0B5F1C8826.
The starter/garment consumers still come from the documented frozen owner overlay;
this evidence batch does not publish unrelated pending owner implementation.
Controlled proximity, attack cooldown and timer-free sessions remain disclosed.
The rite uses decoded NPC dispatch; sealed item dispatch is not a native-client
acceptance test. Further compatible-career earning and native/live gates remain
open. The older retained career is untouched.

## Fresh Warrior promotions and shield instruction (2026-10-03)

The same production-created ordinary character UID1096322 continues from the
level40/class21/reborn2 shield checkpoint. Calculated ordinary monster attacks
produce526 kills/548 hits, reaching level70/class23. The two trainer promotions
use Warrior God10001 in map1004; the second purchases and consumes Emerald1080001
at its catalog CP price. Repeated promotion requests leave the saved image intact.
No levels, materials, gear, purse or achievements are supplied for this phase.

At class21, the two rank-gated shield lessons refuse without granting skills.
At earned class23/level70, ordinary NPC options9/10/11 grant Shield Block10470,
Defensive Stance11160 and Magic Defender11200. Exactly three lesson receipts are
saved. A separate game/database process loads all three skills and repeats those
options without changing possessions, profile, purse, skills or receipts.
The final CP balance is239243; the closed starter entitlement stays closed and
its second-rebirth claim remains single. This proves earning/instruction and
persistence, not native casting or complete skill-level training.

Current authoritative fresh checkpoint: `revora_fresh_warrior_probe`.
Older third-shield/second-rebirth checkpoints are historical and must not be
restored over this continued character. `career-fresh-warrior-train` and
`career-fresh-warrior-reload` pass on executable
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
Fixture compilation uses x86 and warnings as errors. No production change,
live restart, deployment or native acceptance is claimed by this batch.

As in the prior journey, target proximity, travel and attack cooldown are
controlled; the timer-free process retains the previously purchased EXP-potion
balance. This does not prove natural elapsed-time leveling, retaliation or travel.
An initial refusal assertion incorrectly compared snapshots before/after moving
to the trainer. Recovery accepted only map/x/y differences in that exact starting
profile, with every other saved field/possession unchanged; it did not reset
progression or reissue rewards. Comparisons now begin after reaching the NPC.

## Fresh Warrior level110 and Meteor promotion (2026-10-03)

The same saved Warrior advances from level70/class23 to level110/class24 using
4107 additional calculated kills and4587 hits across two bounded processes.
Cumulative Warrior-stage totals are4633 kills/5135 hits. At level100, ordinary
catalog purchase supplies Meteor1088001; Warrior God's promotion consumes its
exact UID and charges the catalog CP cost. Repeated promotion does not change
the saved profile, possessions or purse. The existing three shield lessons remain
single after repeated instruction. The final saved CP balance is270552.

`career-fresh-warrior-advance` saves each kill/promotion and can continue a partial
batch. `career-fresh-warrior-advanced-reload` requires level110/class24, exact
saved snapshot and all three previously learned shield skills, then verifies
lesson replay leaves that snapshot unchanged. Both advancement processes and the
separate reload use the unchanged private executable
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
The current checkpoint remains `revora_fresh_warrior_probe`; the level70 image and
its older reload phase are historical. Final class25 still needs a newly earned
Moon Box; reaching110 alone does not grant that promotion or its achievement.
The prior controlled travel/cooldown, retained paid EXP buff and timer-free
limitations remain. No native acceptance or live deployment is claimed.

## Fresh final Warrior promotion through Moon Box (2026-10-03)

The same level110/class24/reborn2 Warrior earns another Moon Box through the
ordinary tactics quest:37 visits,98 calculated kills,195 hits and two genuine
surrender/retry cycles. Tactic selection and drops are unforced; no tokens,
completed stages or reward items are inserted. Normal NPC exchanges produce
Moon Box721090 and the existing bound P7 reward. The third quest-result receipt
contains their exact UIDs; achievement11022 remains a single original receipt.

A separate process reload preserves the earned rewards before spending them.
Warrior God consumes the exact earned Moon Box for class25. CPs, bound CPs and
silver remain unchanged by that promotion; achievement10817 is recorded once.
Another game/database restart and repeated promotion confirm the final saved
profile, possessions, currencies, lesson receipts and starter closure remain
unchanged. Final profile: level110/class25/reborn2,271570 CPs.

The four phases `career-fresh-warrior-moon-earn`, `-reload`, `-promote` and
`-promote-reload` pass on private executable
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
The current fresh checkpoint is now `revora_fresh_warrior_promotion_probe`;
`revora_fresh_warrior_probe` and the Moon Box pre-spend checkpoint are historical.
The fixture retains controlled travel/proximity/cooldowns and timer-free runtime;
it proves this earned server route, not native navigation or natural difficulty.
No live deployment, source balance change or new catalog identity is claimed.

## Fresh Soul Trial and public warehouse (2026-10-03)

UID1096322 continues from the earned final Warrior promotion. Its32 existing
bag items are deposited through sealed1102 requests at the ordinary Twin City
warehouse; no possessions are deleted or substituted. The fixture now constructs
that standard warehouse before loading items, matching this character's new saved
storage state, and loads the saved variable vault before quest interactions.

Caelan accepts the current-life Soul Trial and supplies the three ordinary travel
options. Calculated attacks against registered startup wilderness monsters earn
50 Birdmen,50 Macaques and50 Sand Monsters:150 kills/150 hits. Counters are saved
by the production death path and the completed, unclaimed hunt survives a separate
game/database restart before turn-in. No quest counters, deaths or rewards are
supplied. Proximity/travel/cooldown and the timer-free runtime remain controlled.

The ordinary claim pays once for life2:500 RVPs,50000 silver,15 unbound EXP Potions
and one bound class-oriented P7 soul. CPs, bound CPs and level are unchanged by
claiming. This is the existing once-per-life reward, not a daily quest payout.
The character now holds16 reward items in its bag and the32 prior items remain
stored; silver is6150000 and RVPs500. Same-session and cold repeated acceptance/
turn-in leave the entire saved snapshot unchanged, including currencies, items,
training receipts, variables and the unique life2 payout receipt.

`career-fresh-soul-hunt`, `-claim` and `-reload` pass on private executable
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
A final cold check also compares the loaded warehouse item UIDs with every saved
Twin City warehouse row. Current fresh checkpoint: `revora_fresh_soul_probe`.
The previous Warrior promotion checkpoint is historical. This extends the same
production-created career without changing the retained mixed-provenance career,
and does not certify native UI, full independent320 earning or live deployment.

## Fresh Courier quest and earned level120 reward (2026-10-03)

The same saved ordinary Warrior accepts Cerys's Courier Loop, reports to the
City Commander, uses Cerys's Phoenix Castle travel option and earns20 Bandit
kills through20 calculated attacks on registered monsters. PurseClerk authorizes
return; a separate game/database process reloads this completed, unclaimed stage.
No quest stages, kills, currencies, items or levels are supplied for this route.

Cerys's existing once-per-life reward raises the character from110 to120 and
pays8000 CPs,80000 silver,200 RVPs and a bound class-oriented P7 soul. Its prior
VIP4 becomes VIP6 for seven days. Bound CPs are unchanged. Claiming is the
production quest transaction, not a fixture level setter. Same-session and cold
accept/turn-in retries preserve the complete saved snapshot, including VIP
level/days, skills, prior Soul Trial claim, all items and the unique life2 Courier
receipt. The final reload reports level120/class25/reborn2,281860 CPs,700 RVPs,
6230000 silver and VIP6/seven days.

`career-fresh-courier-hunt`, `-claim` and `-reload` pass on unchanged private
executable `121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
Current fresh checkpoint: `revora_fresh_courier_probe`; older Soul Trial and
Warrior checkpoints are historical. Controlled travel/proximity/cooldown and
timer-free runtime remain limitations; natural VIP expiry and genuine-client
presentation are not newly verified. No live deployment or reward-policy change.

## Fresh daily Price Hunt with unforced drops (2026-10-03)

The same ordinary UID1096322 stores its17 Courier/Soul reward items through
public warehouse packets, preserving49 stored items in total. The Envoy assigns
route1 normally:20 materials729096 from BanditL97/BanditL98 on map1015. No quest
clock, assignment or drop RNG is overridden. Calculated combat earns54 kills in
54 hits at the configured40% material drop chance; ordinary sealed floor pickup
requests yield20 materials and20 durable receipts. Repeated pickup packets do
not duplicate items. The completed, unclaimed hunt survives a separate process.

After reconnecting, a stale completion request correctly pays nothing until the
player reviews the hunt again. Reopening the Envoy menu and claiming pays2500
CPs,15 RVPs,25 Study Points, one Chi token and one bound Tortoise Breaker rune.
The completed receipt has no skip fee or soul UID, and no new P7 item is granted.
Same-session and cold free/paid completion retries preserve the full snapshot.
Final currencies:284599 CPs,715 RVPs and25 Study Points.

`career-fresh-price-hunt`, `-claim` and `-reload` pass on unchanged private game
SHA-256 `121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
The initial claim fixture omitted the required post-reconnect menu review; its
refusal was correct. The corrected fixture explicitly verifies that refusal
before completing the earned claim, without resetting character or quest state.
Current fresh checkpoint: `revora_fresh_price_probe`, stage paid. Courier and
older checkpoints are historical and must not be restored as current state.

This adds fresh route1 evidence only. Travel, proximity, attack cooldowns and
timer-free runtime remain controlled, and the previously purchased EXP buff is
retained. Natural pacing, retaliation, other assignments and native presentation
are not newly certified. No game source, balance or live deployment changed.

## Fresh Release Souls with real prayer deadlines (2026-10-03)

Ordinary UID1096322 continues from the paid daily Price Hunt checkpoint. The
Envoy's normal travel option reaches the Desert Guardian; acceptance and three
separate Soldier Remains prayers use ordinary NPC handlers. No clock override,
quest progress, reward or currency is supplied. Each prayer remains pending on
an immediate tick and completes after an actual3.1-second wait. Repeating each
already-released soldier grants no progress. Three saved distinct souls survive
an unclaimed game/database process restart.

The Guardian then pays2500 CPs,15 RVPs,50 Study Points, one EXP Ball722136,
one Chi token and one bound Tortoise Breaker4030101. Exactly those three new
items are verified; no P7 is added. The natural completion stores achievement
11012 and a complete run with three distinct souls and three reward identities.
Same-session and cold acceptance/free-claim/stale-paid-claim retries preserve
all nested prior possessions, currencies and receipts. Final currencies are
287099 CPs,730 RVPs and75 Study Points.

`career-fresh-release-earn`, `-claim` and `-reload` pass on private executable
SHA-256 `121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
Current fresh checkpoint: `revora_fresh_release_probe`, stage paid; the Price
checkpoint is now historical. NPC proximity remains controlled and the candidate
uses its timer-free runtime with explicit production quest ticks. This verifies
real prayer deadlines and durable ordinary rewards, not native navigation,
client presentation, full independent320 completion or live deployment.
No production source or reward-policy change was required.

## Fresh Ancestor Worship and combat-filled Soul Ball (2026-10-03)

The same ordinary UID1096322 continues from the paid Release Souls checkpoint.
The Envoy admits the character to the Ancestral Hall; a separate process reloads
that unclaimed entry before statue98130 grants2500 CPs,15 RVPs, one unbound Chi
token and one bound Tortoise Breaker rune. Silver and Study Points stay unchanged.
The receipt records no P7. Same-session and cold statue retries preserve the
exact nested career. Final Worship balances:289599 CPs,745 RVPs,75 Study Points.
The existing production EXP reward is included in saved progression; this batch
does not separately assert its numeric amount.

The Envoy then sells a Super Spirit Bead for the configured800 CP fee. Using the
empty bead pays nothing. Registered Birdman/Bandit wilderness monsters on1015
die through250 calculated melee attacks; each of250 kills contributes exactly
two spirits, reaching500. No spirit counter, death, gear, currency or clock is
supplied. The purchased full bead and prior career survive an unclaimed process
restart. Ordinary item use consumes the exact bead and grants2500 CPs,15 RVPs,
80 Study Points, one Chi token, one bound Tortoise Breaker rune and172800 seconds
of blessing. No P7 is issued; the saved contract retains800 paid issue CPs,
zero completion fee and `paid_finish=false`. Reusing the consumed object is a
no-op. Another cold process retains exact items, receipts, purse and blessing.
Final balances:293654 CPs,760 RVPs,155 Study Points,172800 blessing seconds.
Hunting currency changes are retained, not replaced with an assumed net total.

All six phases `career-fresh-worship-enter`/`-claim`/`-reload` and
`career-fresh-bead-hunt`/`-claim`/`-reload` pass on unchanged private executable
SHA-256 `121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
Current fresh checkpoint: `revora_fresh_bead_probe`, stage paid. Worship and
Release Souls checkpoints are historical. This extends ordinary earning with
existing rewards; no production source or live deployment changed. Proximity,
travel, attack cooldowns and timer-free runtime remain controlled. Natural
combat difficulty, blessing expiry and genuine-client presentation remain
unverified. This is not full independently earned320 completion.

## Fresh first-grade Exorcism and unpaid victory recovery (2026-10-03)

Ordinary UID1096322 continues from the paid Super bead checkpoint. The Envoy
sends it to Market; Heavenly Master accepts grade1 and admits the character to
its private hunt. Earth Master releases the10-million-HP Spook. Existing earned
Warrior equipment and263 calculated melee attacks defeat it; no health, damage,
kill, clock, profile or currency is supplied. The production victory records
achievement10529. Earth Master's ordinary return leaves the completed daily
unclaimed, and a separate game/database process preserves the entire snapshot.

Heavenly Master pays2500 CPs,15 RVPs,25 Study Points, one Chi token, one Exorcism
Token and one bound Tortoise Breaker rune. Exactly these three new items appear;
all prior bag items remain. The claimed receipt has no skip fee and `paid=false`.
Same-session and cold acceptance/claim retries pay nothing and preserve the exact
nested career, including prior bead blessing, possessions and quest receipts.
Final balances:296154 CPs,775 RVPs and180 Study Points.

`career-fresh-spook-hunt`, `-claim` and `-reload` pass on unchanged private game
SHA-256 `121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
Current fresh checkpoint: `revora_fresh_spook_probe`, stage paid; the bead and
older checkpoints are historical. NPC/attack proximity and cooldowns are
controlled, and the timer-free candidate does not establish Spook retaliation,
natural difficulty, native presentation or live readiness. No production source
or reward-policy change was needed.

The fresh career now has five of seven Envoy daily earning routes verified:
Price Hunt, Release Souls, Worship, Soul Ball and Exorcism. Rare Materials and
Magnolia remain for this independently created character; this is not a claim
of five-sevenths completion for the overall roadmap or full320 career.

## Fresh Super Magnolia with unforced bonus draw (2026-10-03)

Ordinary UID1096322 continues from the paid Exorcism checkpoint. The Envoy's
garden travel and Flowered Orchid's reviewed100-CP purchase issue one Super
seed. A duplicate purchase request neither charges nor grants another seed.
The exact unplanted seed and purse survive a separate game/database process.

Ordinary item use beside the flower bed consumes the issued seed and grants
2500 CPs,15 RVPs,300 Study Points, one Chi token and one bound Tortoise Breaker
rune. The normal bonus RNG draws31, correctly producing no bonus item under the
20% rule; the exact roll/no-bonus receipt persists. No clock, bonus draw, seed,
profile or currency is supplied. Reusing the consumed seed pays nothing, and
same-session/cold seed requests neither charge nor grant again. The planting
reward is immediate: the seed's300-minute value scales EXP, not a growth timer.
The existing EXP reward is covered by saved progression, not a new numeric
amount assertion. Final balances:298554 CPs,790 RVPs and480 Study Points.

`career-fresh-magnolia-seed`, `-plant` and `-reload` pass on unchanged private game
SHA-256 `121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
Current fresh checkpoint: `revora_fresh_magnolia_probe`, stage paid; the Spook
checkpoint is historical. Travel/proximity and timer-free runtime remain
controlled; native presentation and live release are unverified. This one draw
verifies the observed outcome and receipt, not a statistical20% distribution
or fresh bonus-pack opening. No production source or balance changed.

Six of the seven Envoy daily routes now have fresh-career earning evidence.
Rare Materials remains. Its four normal-gem IDs700001/700011/700021/700031 are
absent from the private loaded shop source files; mining is the next reviewed
material source. This is a bounded daily subset, not full320 completion.

## Fresh mined Rare Materials and seven daily routes (2026-10-03)

Ordinary UID1096322 continues from the paid Super Magnolia checkpoint. The
Envoy's unforced assignment requests three normal Phoenix Gems700001. The
character buys a1000-silver Pickaxe from Storekeeper, temporarily unequips its
original weapon/shield and enters Plain Mine through the100-silver Conductress
passage. No quest assignment, mining roll, material or currency is supplied.

Mining uses the normal configured1% gem branch and ordinary loot RNG. The
fixture advances only MiningRewards.Clock by three seconds per attempt;2144
attempts produce the three required gems. When bag space runs low it returns
to Storekeeper, sells only newly mined non-gem items through sealed sale packets,
checks each exact GoldWorth/3 payment, and repays the ordinary mine passage.
534 items are sold. All prior bag items and all mined gems are protected from
sale. The original weapon and shield UIDs are re-equipped before turn-in; the
purchased pickaxe and remaining mined items are retained. This demonstrates
material acquisition and inventory turnover, not natural elapsed mining time
or a statistically verified drop frequency.

The offering survives an unclaimed game/database process restart. Ordinary
Envoy completion consumes exactly three gems and pays2500 CPs,15 RVPs,15 Study
Points, one Chi token and one bound Tortoise Breaker rune. No P7 or skip fee is
recorded. Same-session and cold repeat free/paid claims preserve the exact nested
career, including earlier daily receipts, storage, equipment and blessing.
Final balances:301054 CPs,805 RVPs,495 Study Points.

`career-fresh-rare-mine`, `-claim` and `-reload` pass on unchanged private game
SHA-256 `121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
Current fresh checkpoint: `revora_fresh_rare_probe`, stage paid; Magnolia and
older checkpoints are historical. Proximity/travel and mining time are
controlled, with timer-free runtime. No production source or live deployment
changed. Native UI, natural economy pacing and the full320 career remain open.

All seven Envoy daily routes now have earning and cold-persistence evidence on
this same production-created character: Price Hunt, Release Souls, Worship,
Soul Ball, Exorcism, Magnolia and Rare Materials. This closes the fresh daily
route subset only; it does not claim a Way of Heroes aggregate reward, full
roadmap completion, other random assignments or native/live acceptance.

## Fresh seven-daily Way of Heroes reward (2026-10-03)

UID1096322 continues from its earned Rare Materials checkpoint. Production
Way of Heroes loading reads its seven completed daily services; no daily row,
qualification or Way of Heroes completion is supplied. A sealed2830 request
for stage409 grants exactly one bound Praying Stone1200001 and one nonlegacy
claim. All previous bag items remain, with CPs/RVPs/silver/Study unchanged.
Duplicate claim packets do not issue another stone.

A separate game/database process reloads the unconsumed stone. Ordinary sealed
item-use packets consume its exact UID once and add604800 blessing seconds.
The previous172800 seconds become777600. Repeated use grants nothing; the saved
use receipt records exactly604800 added seconds for that bound item. Another
cold process preserves the exact full career, seven daily ledgers, saved Way
of Heroes state, unique claim, item-use receipt and resulting blessing. Further
stage409 requests remain no-ops.

`career-fresh-daily-claim`, `-use` and `-reload` pass on unchanged private game
SHA-256 `121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
Current fresh checkpoint: `revora_fresh_daily_claim_probe`, stage used. The Rare
Materials checkpoint is historical. This closes the earned daily-checklist
substage and reward use for the fresh career, not a main-stage aggregate or
full320 achievement career. Earlier earning timing/proximity limits still apply;
blessing countdown, native presentation and live deployment remain unverified.
No production source change, new balance policy or live restart was required.

## Fresh achievement compatibility audit (2026-10-03)

A read-only career export after stage409 records level122/class25/reborn2 and46
stored identities:9 class promotions,11 travel-region receipts,8 observed-state
receipts and18 other progression/equipment/event receipts. The337-ID catalog
therefore has291 absent identities on this character. These counts are receipt
presence, not independently earned completion or a percentage. In particular,
controlled approach/travel coordinates explain region receipts; ordinary
navigation still needs its own evidence.

The original unpromoted Trojan creation and earned first promotion prove10819,
which was missing from the older retained career. Applying the same four default
activation/catalog exclusions and the remaining eleven unproven early-promotion
routes leaves322 conservative candidates,276 currently absent. The two-identity
margin over320 is planning arithmetic, not proof the rest can coexist. Neither
the retained career's321 candidates nor its mixed-provenance receipts should be
substituted for this fresh character's46 stored identities.

The export is `docs/research/FRESH_CAREER_RECEIPTS.json`; the reproducible report
is `docs/research/FRESH_COMPATIBILITY_PLAN.json`, generated and checked with
`tools/audit_fresh_compatibility.py`. The source catalog and input hashes are
recorded, and the export binds to the current checkpoint snapshot hash. The
private `career-fresh-achievement-audit` phase only reads career rows and exports
numeric IDs/source labels; it does not grant achievements or reset the snapshot.
Current checkpoint remains `revora_fresh_daily_claim_probe`, stage used.

Next quest earning target: Rise of the Warrior, while the fresh character still
satisfies its ordinary Warrior20-25/level40+ admission. Verify its actual quest
route and saved max-weapon reward before moving to another class. Its level140
reward equipment requirement remains separate from receipt ownership at122.
Later class/progression/travel/event routes remain in the compatibility report;
full320, natural combat pacing, native acceptance and live release remain open.

## Fresh Rise of the Warrior and max-weapon recovery (2026-10-03)

The same level122/class25/reborn2 character enters Rise of the Warrior through
Warrior God using its existing qualification and equipment. Ordinary dialogue
issues one bound jade slip; repeat acceptance creates no duplicate. Sealed item
use consumes it and enters the memory. The chest route leads to the registered
Sealing Stele, defeated by one calculated melee attack. The following chest
releases Tiny Mind of Evil, defeated by two calculated attacks. No damage,
health, kill, quest stage, qualification or currency is supplied.

Both victory receipts and stage6 survive a separate process after the ordinary
return to Warrior God. Resume opens the final cave, where Miracle's Spirit
awards exactly two unbound EvilSlayer624439 fists, each +12, -7 and two empty
sockets. CPs, RVPs and silver are unchanged; all earlier bag items remain. A
repeated final claim creates nothing. Ordinary return and another cold process
preserve exact reward UIDs/attributes, seven stage receipts, two victories and
two reward rows plus the complete nested prior career. Trainer acceptance,
resume and jade-recovery replays after completion are no-ops.

`career-fresh-rise-earn`, `-claim` and `-reload` pass on unchanged private game
SHA-256 `121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
Current fresh checkpoint: `revora_fresh_warrior_epic_probe`, stage paid; the
daily-checklist checkpoint and its46-receipt audit are historical snapshots.
The character remains122 and has not equipped these level140 weapons. Proximity,
cooldowns and timer-free runtime are controlled; the one/two-hit victories do
not establish encounter difficulty or retaliation. Native journal/reward display,
level140 equipment use and live release remain pending. No production source,
balance policy or live deployment changed.

## Fresh Connector and Spun Gold equipment chains (2026-10-03)

UID1096322 continues from the paid Warrior epic checkpoint. Ordinary sealed
Twin City warehouse deposits retain existing items and make room for the quest
materials. Chang, Wong and Chen's Connector route earns its five Castle Bandit
kills through calculated attacks and returns the issued letter. Connector stops
at stage5 with no final reward. Spun Gold then follows Hong/Zhao, obtains the
invitation through unforced Bandit loot, uses the issued offering/disguise,
defeats the registered encounter and returns the armor. Spun Gold stops at
stage7 with no final reward. Together these routes use8 kills/8 calculated hits.
No stage, material, qualification, loot draw or damage is supplied.

Both unfinished reward claims and all prior possessions survive a separate
game/database process. Chang's two ordinary final options grant five bound
items: Connector necklace pack728236, EXP item723911 and one P7; Spun Gold armor
pack728237 and one P7. The three fixed IDs and two P7 profiles are checked against
five distinct reward UIDs in the saved life2 contracts. Another cold process
preserves those same inventory items, final stages6/8, reward arrays and the
complete nested earlier career. Repeated final claims do not grant again.
These are rebirth-scoped equipment quests, not prohibited daily P7 payouts.

`career-fresh-classic-earn`, `-claim` and `-reload` pass on unchanged private game
SHA-256 `121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
An additional cold reload checks the exact three pack/item IDs against the saved
five reward UIDs. Current fresh checkpoint: `revora_fresh_classic_probe`, stage
paid. Warrior epic and the earlier46-receipt audit are historical checkpoints.

This advances2 of the19 equipment chains on the fresh character; it does not
repeat or replace the earlier supplied-profile19-chain evidence. Quest pack
opening, the remaining17 fresh chains and aggregate10931 remain separate work.
Travel/proximity/cooldowns and timer-free runtime are controlled; normal elapsed
pacing, retaliation, disguise rendering and native/live gates remain unverified.
No production source, balance policy or live deployment changed.

## Fresh Azure Pill and Artisan Feather with preserved interruption (2026-10-03)

UID1096322 continues from the paid Connector/Spun Gold checkpoint. Azure Pill
uses ordinary Chang/Wu/Mu/Bian dialogues and calculated Fire Rat kills, with
unforced50% material drops. The issued eye, three meat items and two herb packs
are consumed by the normal brewing transaction. An immediate request correctly
refuses the unfinished pill. Only AzurePillQuest.Clock is advanced past its saved
two-minute deadline; the normal pill pickup/delivery reaches unpaid stage6.

Artisan's ordinary family dialogues and calculated bird kills earn its100-feather
counter; unforced Vulture drops supply the final feather. Both quests reach their
unpaid final stages before a separate game/database restart. Ordinary Chang
claims grant four distinct bound rewards: ring pack728238, weapon pack728239 and
two P7 souls. Exact reward UIDs, final life2 stages7/6, all prior possessions and
nested career survive another cold process. Repeated final options pay nothing.
This completes4/19 equipment chains for this fresh character, not aggregate10931.

### Interrupted fixture and narrowly checked continuation

The initial earning process stopped at a helper assertion requiring the first
attack to reduce HP. Saved evidence showed Azure stage6 with no reward, Artisan
stage3 with79 feathers and no reward, retained original items with two pieces'
normal durability wear, earned EXP/CPs,2100 quest silver and approach coordinates.
No other snapshot fields changed. The exact initial/current UTF-8 snapshot hashes
were reviewed and pinned in the continuation branch:

- Initial: `c212946f2edbadc489a776756dc4c13cc768ca01965bbe8a3ddcbbf049d994c6`.
- Inspected: `d752cb03f10536bea654bcc193ea1c1fd63577982e534334dbc0e2d9f725b270`.

Only the private fixture checkpoint was advanced to this inspected earned state;
no quest, character, item or reward rows were reset or supplied. The helper now
allows a non-damaging first swing, still requiring a calculated defeat within a
bounded attack count and rejecting player death/persistence fencing. The first
swing's precise cause was not established; no production combat fix is claimed.
The resumed23 kills/23 hits cover21 remaining birds and two Vultures only. The
interrupted process's unsaved combat counters cannot support an exact whole-run
total and are not reconstructed as if measured.

`career-fresh-azure-artisan-earn`, `-claim` and `-reload` pass after continuation
on unchanged private executable SHA-256
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
Current fresh checkpoint: `revora_fresh_azure_artisan_probe`, stage paid; the
classic pair and earlier receipt audit are historical. Timing/proximity/cooldowns
remain controlled. Pack opening, other15 chains, aggregate10931, natural pacing,
retaliation, native presentation and live deployment remain separate work.

## Fresh Mystic Ring and Frantic Monkeys (2026-10-03)

Ordinary non-GM UID1096322 continues from the paid Azure/Artisan checkpoint at
level122, class25, rebirth2. Five separate private game/database processes pass:
`career-fresh-mystic-frantic-prepare`, `-mystic`, `-frantic`, `-claim` and `-reload`.
The executable remains SHA-256
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.

The preparation phase deposits24 existing items through sealed1102 requests at
the Phoenix Castle warehouse, leaving10 in the bag. Every prior item remains
owned. The fixture now optionally loads that ordinary warehouse before loading
saved items; all later processes restore its24 items without supplying belongings
or expanding storage limits.

Mystic Ring uses Chang, the Minstrel, Dealer Shen and Shen Junior in their normal
order. The character buys12 Stanchers and12 Agrypnotics from a loaded silver shop;
exact silver debits and24 saved medicine-consumption units pass. Twenty registered
Ratlings are defeated through normal calculated melee attacks. The ordered scroll
and ring exchanges end with earned ring use, the mouse appearance and unpaid
stage9. Leaving Phoenix Castle and the normal appearance tick clear the disguise.
No medicine, kill credit or quest material is supplied by the fixture.

Frantic Monkeys earns its Decayed Heart from the first Macaque under the normal
50% drop roll. Bao and Doctor Li exchange the owned heart for poison. Normal
poison admission creates the private trial and monkey appearance; a tick before
the one-minute deadline produces no trial monkeys. Only the scene clock advances
63 seconds, then in three-second steps for population/cleanup. Five calculated
trial defeats and three calculated ape/leader defeats produce five and three
saved encounter receipts. The earned vial and Devilish Sword follow the normal
use/hand-in path to unpaid stage9. The clock is restored afterward.

The batch records29 defeats and30 attacks:20 Ratlings, one Macaque, five trial
monkeys and three apes. One Ratling's first swing caused no HP loss; the bounded
normal attack loop then defeated it. No damage formula, monster HP or drop roll
is overridden. This does not establish the cause of the non-damaging swing.

A separate process reloads both unpaid quests before Chang pays exactly four
bound items: headgear pack728240, weapon pack728243 and two P7 souls. Both life2
rows reach stage10 with two distinct reward UIDs each. Every prior bag item is
retained; exact reward identities, nested career, medicine/encounter receipts,
14 bag items and24 Phoenix warehouse items survive the final cold reload.
Repeating both final dialogue options in the claim and reload processes is a
snapshot-preserving no-op.

Current fresh checkpoint: `revora_fresh_mystic_frantic_probe`, stage paid. The
Azure/Artisan checkpoint and earlier receipt audit are historical. Fresh equipment
coverage is6/19; thirteen chains, their aggregate10931 and reward-pack opening
remain. Controlled proximity, travel, attack cooldowns and the timer-free runtime
are fixture limitations. Natural pacing, retaliation, native presentation and
live deployment remain unverified. This batch changes fixtures/documentation only.

## Fresh Soul of the General and The Mad Man (2026-10-03)

Ordinary non-GM UID1096322 continues from the paid Mystic/Frantic checkpoint.
Four separate private game/database processes pass:
`career-fresh-general-mad-general`, `-mad`, `-claim` and `-reload`, on unchanged
executable SHA-256
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
The character remains level122/class25/rebirth2.

Soul of the General follows Chang, Amber, Zhao, the painter and Guard Li through
their ordinary dialogues. The earned silk pack records one Snakeman, ten Phoenix
RockMonsterL42 and ten GiantApeL53 defeats. The existing20-rock population is
loaded once; its ensure routine adds zero duplicates. A calculated phantom defeat
creates one saved kill receipt. After a real1.1-second wait, the normal quest tick
delivers the Paper Figure. Zhao consumes it and leaves the quest at unpaid
stage11. No quest clock or combat/damage override is used.

The Mad Man earns a Venomous Fang from its first Snakeman under the normal50%
roll. Judd and Dong process that owned fang; ordinary Blue-then-Green Toxin use
admits the character to the five-minute collection scene. All seven stones issue
one powder each. Repeating each stone interaction preserves the full snapshot.
Using an owned powder consumes all seven and creates the Rainbow Powder, then
returns the character to Ape City. Du San consumes it and leaves unpaid stage8.
The completed trial retains mask127 and seven powder receipts. No clock or drop
roll is overridden. The unused Red Toxin remains owned and is not counted as a
reward.

The two earning processes record23 defeats from23 calculated melee attacks:
21 scent targets, the phantom and one fang-carrying Snakeman. Both quests survive
an unpaid restart before Chang grants exactly five distinct bound items: boots
pack728244, headgear pack728245, EXP item723911 and two P7 souls. Final life2 stages
are12/9, with two/three exact reward UIDs respectively. All prior bag items remain.
Another cold process preserves the nested career, trial/material/kill receipts,
20 bag items and24 Phoenix warehouse items. Repeating both final options in the
claim and reload processes changes neither saved snapshot nor rewards.

Current fresh checkpoint: `revora_fresh_general_mad_probe`, stage paid. Earlier
checkpoints and the46-receipt audit are historical. Fresh equipment coverage is
8/19; eleven chains, aggregate10931 and reward-pack opening remain. Travel,
proximity and attack cooldowns are controlled; the timer-free harness does not
establish natural travel pacing, retaliation or native presentation. Unrelated
private vote-pulse PostgresException logging persists; these focused passes do
not certify whole-runtime health. No production source or live deployment changes
are part of this fixture/documentation batch.

## Fresh Ghost City and Tomboy (2026-10-03)

Ordinary non-GM UID1096322 continues from the paid General/Mad checkpoint.
`career-fresh-ghost-tomboy-ghost`, `-tomboy`, `-claim` and `-reload` pass on the
unchanged private executable SHA-256
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`, after the
specific fixture recovery documented below. Ghost City's rescue hand-in earns
level123; class25 and rebirth2 remain unchanged.

Ghost City follows Aunt Zhang, Wang and Shine, uses the firework at all three
ordered sites, earns the charm and visits MoMo/the pillar for powder. Its dark
visit defeats100 KidCatchers/KidRippers and then KidHunter/KidSiezer through
normal calculated melee. The saved102 distinct encounter receipts have kinds
60/40/1/1. The earned Bezoar, five distinct child rescues, MoMo hand-in and Aunt
report reach unpaid stage10. Repeating child rescue leaves the snapshot intact.
Only the scene clock advances in three-second population/cleanup steps; no HP,
damage or kill-credit override supplies progress.

Tomboy earns ten Marbles and raw stones from ordinary loaded Rock Monsters with
unmodified50% drop chance and random stone selection. It performs four unforced
50% fusion attempts: three successes and one failure. Every attempt consumes its
recorded two owned materials and preserves all other bag items; successful
attempts alone create their tracked jade. Lynn consumes the ten Marbles and three
jades. Carolyn's wrong answer changes nothing; the correct answer reaches unpaid
stage5. This phase records28 defeats/28 attacks with no RNG or clock override.

A separate process reloads both unpaid quests before Chang grants six distinct
bound rewards: armor pack728246, necklace pack728241, two EXP items723911 and two
P7 souls. Final life2 stages11/6 each retain three exact reward UIDs. All prior
bag items remain. The final cold process preserves the complete nested career,
material/fusion/encounter/rescue receipts,26 bag items and24 Phoenix warehouse
items. Repeating both final options in the claim/reload processes pays nothing.

### Interrupted visit snapshot and preserved continuation

The first Ghost process stopped after all102 defeats and the first child rescue.
Its snapshot query incorrectly selected a single row from the multi-visit history;
PostgreSQL returned21000 once two visits existed. The query now aggregates visits
in stable visit order. This was a fixture-query failure, not a production quest
failure. A separate inspect process captured the before/current snapshots:

- Initial UTF-8 SHA-256: `a8687c9467d05771284b08c21f516a06d03966c243a9ebc031d5599df612a69f`.
- Inspected UTF-8 SHA-256: `57d42c3ffee8583bfa1bf72862dce5e9c78fde14d6d8cce8f33bd8ff004f93b2`.

The reviewed differences are Ghost stage8, three sparks,102 kill receipts, child0,
two ended visits, the earned charm/Bezoar,500 quest silver, earned EXP, recovery
coordinates and two prior equipment durability decreases. No prior item was
removed; no other snapshot fields changed. Only the private probe advances to
this exact inspected state. The continuation uses the earned charm to enter a
new dark visit, completes the remaining rescues and hands in normally. No quest,
character, item or reward rows are reset or manufactured. The original process's
local attack count was not saved; later counters cover Tomboy's28 defeats/attacks
only. Ghost's102 saved kills remain separate evidence, not a fabricated hit count.

Current fresh checkpoint: `revora_fresh_ghost_tomboy_probe`, stage paid. Earlier
checkpoints/receipt audit are historical. Fresh coverage is10/19; nine chains,
aggregate10931 and pack opening remain. Controlled travel/proximity/cooldowns and
timer-free runtime do not prove natural pacing, retaliation or native rendering.
Live deployment/client acceptance remain separate. This batch changes private
fixtures and evidence documents only.

## Fresh Desert Blood and Hero's Wings (2026-10-03)

Ordinary non-GM UID1096322 continues from the paid Ghost/Tomboy checkpoint.
`career-fresh-desert-hero-start`, `-blood`, `-hero`, `-claim` and `-reload` pass,
after the preserved hunt interruption below, on unchanged private executable
SHA-256 `121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
The character remains level123/class25/rebirth2.

Desert Blood earns and reads its Stone Spell from normal BladeGhostL83 drops,
enters the Crypt, and accepts the warrior's request. Its saved hunt reaches100,
then a separate process reloads that exact count before earning the remaining100.
The final counter is200. Normal map respawns replenish exhausted wilderness
populations; a bounded45-second selection wait now accommodates them without
changing death timestamps, respawn settings, HP, spawn counts or quest credit.
Five fangs drop through the unmodified50% roll. Four ordinary Crypt visits,
Harvey's letter, Peach Bag and note-reading/report complete unpaid stage10.

Hero's Wings buys one Meteor through ordinary remote mall2888 for the catalog's
3 CPs from the existing earned balance. No boss bounty or supplied currency is
needed. The Meteor is consumed once; a repeated hand-in changes nothing. Bird
Blood and Gallbladder use unmodified50% rolls, the map's randomly assigned site
is read from saved state, and ordinary item use gathers the root there. Ten
HawkL93 defeats form five pairs within the real one-minute deadlines. No quest
clock or random roll is overridden. The pill/report route reaches unpaid stage11.
This phase adds27 measured defeats/27 attacks. The fixture can preserve an unpaid
HawKing hunt across a process if its loaded population gives no Gallbladder;
that branch was not needed or exercised in this run.

An initial mall fixture call wrongly looked for2888 as a physical NPC and stopped
before any purchase or new quest mutation. It was replaced with the documented
ordinary remote-mall buy path; this was a fixture correction, not a game-source
change.

A separate process reloads both unpaid quests before Chang pays six exact bound
items: ring pack728242, headgear pack728247, two EXP items723911 and two P7 souls.
Final life2 stages11/12 each record three distinct reward UIDs. All prior bag
items remain. The final cold process preserves the complete nested career,
materials, four ended Crypt visits,32 bag items and the paid quest rows. Repeating
both final options in claim/reload is a snapshot-preserving no-op.

### Exhausted-population fixture and preserved continuation

The initial hunt stopped because a fixture `First` query assumed a live target
was always present. Inspection found stage3 with69 saved hunt kills and the read
Stone Spell. The exact reviewed UTF-8 snapshots are:

- Initial SHA-256: `17e94a90635b0f7a0b9b55981366db765cf58a2cbf63c89ee966dc243db21e14`.
- Inspected SHA-256: `2594951e035f9b689dfdeeec20d084c01fa6c9802afe9b0dc3ca41a349863c81`.

Other changes are one ended Crypt visit, its tracked spell,200 quest silver,
760 normal monster CPs, earned EXP, hunting coordinates/map and two equipment
wear decreases. No prior item was removed; no other snapshot fields changed.
Only the private probe advances to that exact reviewed state. No quest, player,
item or reward rows are reset. The resumed first phase earns31 remaining kills
to100, and the next earns100 plus five fang kills. Their136 measured defeats/
attacks exclude the interrupted process. With Hero's Wings, the continuation
counters total163/163; they are not a whole-journey attack count.

Current fresh checkpoint: `revora_fresh_desert_hero_probe`, stage paid. Earlier
checkpoints/receipt audit are historical. Fresh coverage is12/19; seven chains,
aggregate10931 and pack opening remain. Controlled travel/proximity/cooldowns do
not prove natural pacing or native presentation. The fixture omits player
pet/screen timers; normal map respawn timers do run, as these waits demonstrate.
Earlier shorthand saying 'timer-free' must not be read as every runtime timer
being disabled. Retaliation, live deployment and native acceptance remain separate.
No production-source change or live deployment is included in this batch.

## Fresh Family Feud and Desert Treasure (2026-10-03)

Ordinary non-GM UID1096322 continues from the paid Desert/Hero checkpoint.
Five separate private game/database phases pass without interrupted recovery:
`career-fresh-family-treasure-prepare`, `-family`, `-treasure`, `-claim` and
`-reload`. Executable SHA-256 remains
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
The character remains level123/class25/rebirth2.

Preparation deposits17 prior items through sealed1102 requests at Phoenix's
ordinary warehouse. No belonging is lost; cold phases retain41 warehouse items
and begin with15 in the bag. Family Feud earns50 BanditL97 and ten Banditti counts,
its leader/courier encounters, ten deliverers, and unforced Indigo/Scarlet token
drops. Normal map respawn waits replenish exhausted wilderness populations. The
Golden Token/disguise admits the player to Kerry's chamber; five accepted jump
handlers earn all five puzzle steps before normal exit/report reaches unpaid
stage9. Family clock advances control encounter ticks and puzzle deadlines;
no combat damage, health, token or random-drop override supplies credit.
This phase records74 defeats/75 attacks, including one initially non-damaging
Bandit swing handled by the existing bounded attack loop.

Desert Treasure earns vault keys from loaded bats with unforced50% drops. Each
vault attempt uses the earned Reveal Vial, accepted jump onto91,283 and ordinary
vault dialogue. Every attempted key is consumed; the vault's normal50% roll is
unchanged. The resulting Scripture Box is opened and handed in. The player takes
Shark's advertised certain portrait option:99,999 silver, with the normal100
silver quest payment checked separately in the resulting balance. A repeated
purchase preserves the full snapshot and the single saved bid receipt.
Five unforced Bat Essences reveal the portrait beside Louis. A calculated Jinx
victory followed by the owned Pure Bottle earns one capture receipt and relic.
The final report and three-answer quiz complete; repeating the quiz gives no
second experience reward. Treasure clock control covers spawn, reveal and
three-second capture deadlines; natural player timing is not established.
Treasure adds15 defeats/20 attacks, making the measured batch total89/95.

A separate process reloads both unpaid quests before Chang grants six distinct
bound rewards: necklace/bag pack728248, boots pack728249, two EXP items723911 and
two P7 souls. Final life2 stages10/8 each save three exact reward UIDs. Prior bag
items, the reusable Reveal Vial and all earlier career state remain intact.
The final cold process retains22 bag items,41 Phoenix warehouse items, material/
encounter/puzzle/payment/capture/quiz receipts and exact rewards. Repeating both
final claim options preserves the full snapshot.

Current fresh checkpoint: `revora_fresh_family_treasure_probe`, stage paid.
Earlier checkpoints and receipt audit are historical. Fresh coverage is14/19;
five chains, aggregate10931 and reward-pack opening remain. Travel/proximity,
attack cooldowns and the named quest clocks are controlled; normal map respawns
run. Retaliation, natural pacing, native presentation and live deployment remain
separate gates. This batch changes fixtures/documentation only.


## Fresh Desert Expedition (2026-10-03)

`CheckCandidateFreshExpedition.cs` continues ordinary non-GM UID1096322 from
`revora_fresh_family_treasure_probe` without supplying levels, equipment, quest
materials or quest progress. All three private phases (earn, claim, reload) pass
on candidate game SHA256
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
The harness compiles with warnings as errors; no game-source rebuild or deployment
is part of this batch.

Chang and Master Mo Mo grant the earned Fervent Pill. Normal Bull Monster deaths
use unforced 50% drops to earn ten Burnt Bullhorns. Mo Mo refuses early hand-in
without consuming them. Advancing the controlled quest clock to the deadline
settles all six health debits, verified in saved payment receipts; health remains
positive and below its initial value. The earned mirror then places three traps.
Each registered Mad Bull moves through the ordinary owned-encounter movement
handler into its trap before calculated melee kills it. All three ghost items
are earned, followed by a calculated Bull Master victory and heart report.
There are 34 defeats and 68 calculated attack attempts across the whole route.

An unpaid restart preserves stage5 and four successful encounter receipts. Chang
then grants exactly three distinct bound rewards: pack728250, EXP item723911 and
one P7 soul. Prior bag items remain owned. A further restart preserves stage6,
exact reward UIDs, material/trap/health receipts, earlier career state and all
storage. Repeating the final claim preserves the full saved snapshot both before
and after restart. The character remains level123/class25/reborn2 with25 bag items.

Current fresh checkpoint: `revora_fresh_expedition_probe`, stage paid. Fresh
coverage is15/19; Power Book, Defense Talisman, Attack Talisman and Tempest Wings,
aggregate10931 and reward-pack opening remain. Quest time, travel/proximity,
attack cooldowns and monster movement cooldowns are controlled. Damage and drop
RNG are not replaced; normal map respawn timers run. Retaliation, natural pacing,
native presentation and live deployment remain separate gates. The unrelated
private vote-pulse PostgresException is still present; these passes do not prove
whole-runtime health. Two initial harness compilation errors were corrected
before any quest execution; no interrupted quest recovery was required.


## Fresh Power Book (2026-10-03)

`CheckCandidateFreshPower.cs` continues ordinary non-GM UID1096322 from the paid
fresh Expedition checkpoint. Earn, claim and reload pass on candidate game SHA256
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
No supplied levels, quest materials, direct quest-row progress or lethal-damage
shortcut is used. The harness compiles with warnings as errors after correcting
one missing namespace import before execution; no game-source rebuild is needed.

The remote CP mall sells one Meteor for exactly3 earned CPs. Chang's badge and
the trainer's book purchase consume that Meteor once; repeated purchase preserves
the full snapshot. Four owned floors earn their normal combat stages, including
ten monsters moved by the ordinary monster movement handler into the herd area.
Calculated melee targets the actual lured body. The timed pairs use the controlled
quest clock. Material drops and consumption/cooking retain their ordinary50%
rolls, with bounded retries until success. A naturally dropped Flame Plume burns
ten individually saved final corpses; every burn increments the durable receipt
count exactly once. The exit book returns the character to Twin City. Repeating
the trainer's final report preserves the full snapshot.

Combat totals107 defeats/240 attack attempts; the character earns level124.
A separate process reloads unpaid stage13, then Chang consumes the earned badge
and book and grants exactly two distinct bound rewards: armor pack728251 and one
P7 soul. The Flame Plume is already consumed by the tenth burn. All earlier bag
items remain owned. A further restart preserves stage14, exact reward UIDs,
purchase/life/burn/visit receipts, earlier career and warehouse state, with27 bag
items. Repeating Chang's final claim preserves the complete snapshot before and
after restart. Pack opening is still a separate pending check.

Current fresh checkpoint: `revora_fresh_power_probe`, stage paid, level124/class25/
reborn2. Fresh coverage is16/19; Defense Talisman, Attack Talisman and Tempest Wings,
aggregate10931 and reward-pack opening remain. Travel/proximity, attack and
movement cooldowns, population pulses and quest time are controlled. Normal
damage and random material outcomes are retained. Retaliation, natural deadlines,
native presentation and live deployment remain separate gates. The unrelated
private vote-pulse PostgresException remains visible in the earning log; this
batch does not establish whole-runtime health or change production gameplay.


## Fresh Defense Talisman (2026-10-03)

`CheckCandidateFreshTower.cs` continues ordinary non-GM UID1096322 from the paid
fresh Power Book checkpoint, with its earned level124/class25/reborn2 and gear.
Earn, claim and reload pass on candidate game SHA256
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
No levels, equipment, materials or quest progress are supplied. The first attempt
stopped before quest acceptance because the new phase was missing from the
runner's existing terrain-staging predicate. The corrected runner stages reviewed
1782..1784 house maps only in the private candidate, verifies each against SHA256
`9619A90984E1FD2F959CE89F918075353A5BC5839CBDD6D4EEF2A1B05C9A87C4`,
and removes only files it created during cleanup. The unchanged starting snapshot
passes on retry; no quest reset or recovery override is used. Missing terrain now
fails an explicit Enabled assertion before character loading.

The character clears five house bandits, buys three fishing nets for exactly
30000 silver each and earns three fish through calculated encounter victories.
Repeating each net purchase before consuming it preserves the entire snapshot.
The fish exchange and three doors earn access to Ruan Brother. The controlled
quest clock reaches Shawn's scheduled minute, and calculated victories earn his
spear, the two desert encounters, five house rats, four dungeon generals and
Yang Feng. Four smelting operations reconstruct the tower through ordinary NPC
handlers. All combat uses calculated melee:21 defeats/34 attack attempts.

An unpaid restart preserves stage9, the reconstructed tower and quest receipts.
Sugar Tang consumes that tower and grants exactly two distinct bound rewards:
Elite Star Tower202008 and one P7 soul. Earlier possessions remain owned. A final
restart verifies stage10, exact reward UIDs, three30000-silver purchase receipts,
fishing/house/desert/general/Feng/visit/material records and all earlier career
state. Repeating the reward claim preserves the complete snapshot both before
and after restart. The final bag contains29 items.

Current fresh checkpoint: `revora_fresh_tower_probe`, stage paid. Fresh coverage
is17/19; Attack Talisman, Tempest Wings, aggregate10931 and reward-pack opening
remain. Travel/proximity, attack cooldowns and quest/schedule time are controlled;
damage is not replaced. Retaliation, natural pacing, native scene acceptance and
live deployment remain separate gates. The quest's private success depends on
the reviewed staged terrain; it is not proof of default production availability.
This batch changes fixtures, their staging selection and documentation only.


## Fresh Attack Talisman (2026-10-03)

`CheckCandidateFreshFan.cs` continues ordinary non-GM UID1096322 from the paid
fresh Defense Talisman checkpoint. Earn, claim and reload pass on candidate game
SHA256 `121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
The runner includes these phases in existing hash-checked private house-terrain
staging because Attack Talisman initialization depends on Defense Talisman.
Missing activation fails before character loading. No character profile, quest
material, quest progress, lethal damage or random success is supplied.

The character buys Bamboo Wine for10000 earned silver, with a repeat purchase
preserving the full snapshot, and gathers five Snake Meats through unforced
ordinary drops. The cave permit costs another10000 silver. Calculated victories
earn the cave materials, five Crimson Claw receipts, the lake encounter and the
randomly assigned Courtesan encounter. The player uses the earned alchemy items,
incense and pillar offerings through ordinary item/NPC handlers. The garden boxes
are opened in order without reading the hidden winning-box field; any intervening
ghost is fought through calculated melee. The fan is exchanged for the Poem Book.
The full route uses16 defeats/25 attacks and ends with the exact silver balance
`opening - 20000 + 900`, including ordinary stage rewards.

An unpaid restart preserves stage10/book_given/cave_paid, the Poem Book and all
quest receipts. Sugar Tang consumes the book and grants exactly two distinct bound
rewards: Super Heaven Fan201009 and one P7 soul. Earlier bag and warehouse property,
including the prior Star Tower, remains owned. Another restart preserves stage11,
exact reward UIDs, purchase/material/encounter/visit records and the whole earlier
career. Repeating the final claim preserves the full snapshot before and after
restart. The final character is level124/class25/reborn2 with31 bag items.

Current fresh checkpoint: `revora_fresh_fan_probe`, stage paid. Fresh coverage is
18/19; Tempest Wings, aggregate10931 and reward-pack opening remain. Travel,
proximity, attack cooldowns and quest time are controlled. Damage, meat drops,
Courtesan assignment and garden random selection are not replaced. This run does
not claim every ghost outcome. Retaliation, natural pacing, native scene acceptance
and live deployment remain separate gates. The private staging dependency is not
proof of default production availability; the unrelated private vote-pulse error
also remains outside this batch. Changes are fixtures/staging selection/docs only.


## Fresh Tempest Wings and nineteen-chain achievement (2026-10-03)

`CheckCandidateFreshTempest.cs` continues ordinary non-GM UID1096322 from the paid
fresh Attack Talisman checkpoint. Earn, claim and reload pass on candidate game
SHA256 `121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
Eleven prior items move through the ordinary warehouse packet handler into Phoenix
storage, leaving20 bag items and52 stored there without deleting owned property.
The character buys50 Meteors for exactly150 earned CPs and exchanges them through
the ordinary NPC in five batches of ten, receiving five Meteor Scrolls. Mr. Wonder
consumes those five scrolls, with the durable permit/payment records checked.
The owned Roc falls to four calculated melee attacks. Its feather is earned and
the scene exit returns the character to Phoenix Castle. No supplied combat stats,
lethal damage, quest materials or direct quest progression are used.

An unpaid restart preserves stage3, the feather and permit/kill/visit records.
Achievement10931 is absent before the final claim. Mr. Wonder consumes the feather
and grants exactly two distinct bound rewards: Elite Tempest Wing204008 and one
P7 soul. The same transaction creates the single equipment-quests-revora-v1 /
nineteen-chains-cross-life / rule_version1 evidence row for10931. TitleStorage's
normal login path exposes its achievement bit. A further restart verifies the
same rewards and evidence, the bit, earlier career and warehouse property, and
unchanged snapshots after repeat claims. Final bag count22, level124/class25/reborn2.

Current fresh checkpoint: `revora_fresh_tempest_probe`, stage paid. All19 fresh
quest chains and aggregate10931 are now earned and cold-verified within the stated
fixture limits. Earlier starting-profile and interrupted-run disclosures remain
part of that evidence; this does not make the entire320-achievement career proven.
Reward-pack opening remains pending. Travel/proximity and attack cooldowns are
controlled; this Roc encounter uses the ordinary clock and calculated damage.
Retaliation, native scene acceptance and live deployment remain separate gates.
The runner temporarily stages hash-checked private house terrain and Tempest map
SHA256 `AE99B05F276CCACAFF95C3772211A5C9B1F0CC5FE4EA88C2CD9DA8C97E346BD6`;
it cleans up only its additions. This is not default production availability.
The batch changes fixtures, staging selection and documentation only.


## Fresh earned equipment-pack opening (2026-10-03)

`CheckCandidateFreshEquipmentPacks.cs` continues ordinary non-GM UID1096322 from
the paid Tempest checkpoint. Open and reload pass on candidate game SHA256
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
The initial snapshot proves all sixteen distinct bound single-copy packs728236
through728251 are still owned with no prior use receipts. Stored packs are
withdrawn through sealed ordinary warehouse packets from their actual banks;
no replacement packs, equipment, currency or class change are supplied.

The Warrior class25 outputs are independently asserted in pack-number order:
120046,131027,151077,510097,111057,120158,150178,510108,160118,111068,131078,
111089,120189,160199,150219,131109. Power Book's final pack also gives723911.
These are the documented original-style equipment outputs, bound with +0/-0;
the valuable bound P7 rewards were separately earned at quest hand-in and remain
owned. This batch does not claim these packs contain maxed weapons.

Each sealed ordinary item-use dispatch consumes its exact pack once and creates
the expected output(s). The committed use receipt matches the exact new item UIDs.
A repeated dispatch for the consumed UID preserves the complete snapshot. All
non-pack possessions remain owned. A separate process reloads sixteen use receipts
and all seventeen exact outputs, then replays all sixteen consumed UIDs without
changing state. Aggregate10931's saved evidence remains intact after consumption.
The final bag contains37 items; the character remains level124/class25/reborn2.

Current fresh checkpoint: `revora_fresh_equipment_packs_probe`, stage opened.
All nineteen chain earning paths, aggregate10931 and these sixteen earned pack
uses now pass with cold persistence within the documented fixture limits. Class
selection in this fresh run covers Warrior25 only; other classes are not newly
claimed here. Native item presentation and live deployment remain separate gates.
The runner keeps the same hash-checked private terrain staging for consistent
quest initialization. No game-source rebuild, live restart or client patch occurs.


## Fresh post-equipment compatibility audit (2026-10-03)

`career-fresh-equipment-audit` verifies the current loaded character against the
entire saved `revora_fresh_equipment_packs_probe` snapshot before exporting only
profile and achievement-source identities. No character progress is changed.
The regenerated `FRESH_CAREER_RECEIPTS.json` / `FRESH_COMPATIBILITY_PLAN.json`
now describe level124/class25/reborn2:50 stored identities,287 absent catalog
identities and272 absent conservative candidates. This supersedes the earlier
46-receipt daily checkpoint for planning, without rewriting its historical proof.

The four added rows are three controlled travel-region receipts (10617,10619,
10703) and equipment aggregate10931. Nineteen quests do not mean nineteen new
achievement identities. The planning pool remains322, only two above the320
target, with four unavailable identities and eleven early-promotion routes still
unproven. These are conservative planning exclusions, not global impossibility
claims. Existing controlled travel/combat and native limitations still apply.

The next fresh route is the documented Twin City introduction3478-3481 and its
Way of Heroes104 claim:10930 is absent, the ordinary handler accepts levels1..140
without a rebirth restriction, and current level124 eligibility is established by
source. Its earlier supplied-profile test does not replace earning on this fresh
career. Keep the same saved character and earned purse; do not replay the closed
nineteen-chain batch or report50/320 as an overall completion percentage.


## Fresh Twin City introduction and104 reward (2026-10-03)

`CheckCandidateFreshIntro.cs` continues ordinary non-GM UID1096322 from the opened
fresh equipment-pack checkpoint. The level124/class25/reborn2 character earns
introduction3478-3481 through KungfuMaster, a sealed warehouse-open packet, one
real pharmacist purchase at catalog silver price and the captain's bound4030101
rune. An out-of-order captain request and104 claims at stages0..3 do not pay.
The final introduction saves10930 from newcomer-intro-v1. No starting profile,
quest progress or currency is supplied. Stage4/rune ownership survives restart
before the Way of Heroes104 potion claim.

The claim commits one bound723017. The first test then stops because its existing
SoulClaimUse helper asserts consumption, whereas the ordinary potion handler
correctly refuses while the earlier earned EXP bonus is active. Read-only
inspection finds only the104 claim, its potion UID61882366 and saved Way of Heroes
state added; all prior state is unchanged. Reviewed snapshot SHA256 values are
`9bdbed85deb89a32ea1e1c3c754bc538ddf3415bda525bc2b4228925141c48e5`
(before) and `9ebad43352bf815bddc11888595270d52ba99a25c09dbd5a51df98c809ac7481`
(after). The fixture pins both, advances only its private checkpoint to claimed
and resumes using the existing potion. It does not replay or manufacture payment.

The corrected refusal check confirms no consumption or mutation while the old
bonus runs. The ordinary TickDoubleExperience handler receives controlled future
ticks until that bonus expires; no direct zero assignment is used. The earned
potion then consumes once and saves3600 seconds, with one bound use receipt.
A separate cold process preserves the rune, paid introduction,104 claim, potion
use, timer and earlier career. Repeating the captain,104 and consumed potion UID
leaves the exact snapshot unchanged. Final inventory39; native pacing is not
established by the controlled countdown.

Earn, corrected claim and reload pass on game SHA256
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
Current fresh checkpoint: `revora_fresh_intro_probe`, stage used. The post-equipment
50-receipt audit is now historical; no new whole-career total is asserted here.
Travel/proximity and countdown timing are controlled; native/live gates remain
pending. No production code, live process or client patch changes in this batch.


## Fresh post-Ancient compatibility audit (2026-10-03)

A read-only private process loads the current character and verifies its entire
`FreshAncientBossSnapshot` against the claimed boss checkpoint before exporting
receipts. Main UID1096322 is now level128, Warrior25, rebirth2. The export records
56 identities (50 at the post-equipment checkpoint);281 catalog identities are
absent, and266 of the322 conservative candidates are absent. No overall roadmap
percentage or compatible320 completion follows from these counts.

The six additions are10930 introduction,11018 Snake Islands,11019 Sky Pass,
11020 Ancient collection,11027 Labyrinth and observed Virtue threshold10317.
The latter follows the earned team-training balance, not a separately completed
quest. Early Trojan10819 remains the only recovered early-promotion exclusion;
eleven others and the four unavailable identities retain their documented gates.

`career-fresh-ancient-audit` passes on the unchanged private candidate.
`docs/research/FRESH_CAREER_RECEIPTS.json` and the reproducible
`FRESH_COMPATIBILITY_PLAN.json` now reference snapshot SHA-256
`017ca240db577f62ed1f5a68dcdecbe07ddb88f68effa28bfce02f2439891747`.
The compatibility auditor regenerates and passes `--check`. This is the
post-Ancient/pre-affiliation boundary: the subsequent clan-founder10304 receipt
is documented in its own earning batch, not silently included in this export.
Next shared prerequisites are ordinary clan/guild founding and their events.

## Fresh post-Flame compatibility audit (2026-10-03)

`career-fresh-flame-audit` reloads the full paid Flame checkpoint and verifies
its exact nested career before exporting the profile and achievement sources.
The character is level131/class25/second rebirth. The export contains79 saved
identities,258 absent catalog identities and243 absent conservative candidates
out of322. These are evidence counts, not an overall completion percentage.

Compared with the post-Beast export, the ten additions are siege-bomb10231,
Dis City11002-11005, Power Arena11013/11014, Flame Lit11016 and observed-state
10310/11132. The observed-state rows retain that provenance; they are not
silently relabeled independently earned quests. The four unavailable identities
and eleven remaining early-promotion assumptions stay unchanged.

The snapshot SHA-256 is
`5980b428706ad07295c1fb02bef2641cb114fbb37935ac6cb9cf32419a5cca4b`.
The reproducible compatibility report references this sanitized export; source
presence, stored receipts and jointly earned320 remain separate claims.
This is the post-Flame/pre-Demon-Exterminators boundary. No live restart,
production rebuild or deployment occurred.


## Fresh post-Snow compatibility audit (2026-10-03)

`career-fresh-snow-audit` reloads the ordinary character with all three warehouses
and requires exact equality with completed `revora_fresh_snow_probe` stage3
before exporting receipts. The profile is level132, class25 Warrior, twice reborn.
Snapshot SHA-256:
`5bad0236e45f7a7562593dc5dbc7f68924af3e1bc9801dc421415bdb2427a5fc`.

There are88 stored identities,249 absent catalog identities and234 absent from
the322 conservative candidates. Since the dated post-Flame79-receipt audit,
Demon Exterminators11001, Terato10526/Dragon Slayer10932 and Snow Banshee10527
have independently documented earning evidence. The other five additions are
travel-region10802-10806 from controlled positioning; they do not prove an
independently navigated travel itinerary. Source producers337, eleven remaining
early-promotion planning exclusions and the four unavailable gates are unchanged.

The exported `research/FRESH_CAREER_RECEIPTS.json` and generated
`research/FRESH_COMPATIBILITY_PLAN.json` reflect this exact pre-housing checkpoint.
This is a work-queue refresh, not compatible320 completion or native acceptance.
Private log: `arena-process-career-fresh-snow-audit.log`.

## Fresh post-Purple compatibility audit (2026-10-03)

`career-fresh-purple-audit` verifies the complete stage4 Purple combat checkpoint
before exporting receipts. The same level132/class25/twice-reborn character now
has98 stored achievement identities, up from88 at the post-Snow checkpoint.
The ten additions are furniture10101; housing10102?10105 and10122; Arena Honor
10320, participation11032 and class victory11101; and Sword Master10528.
Purple Banshee itself is validated by its victory/reward rows, not an invented
achievement identity.

The337-entry catalog still yields322 conservative candidates with224 absent.
The eleven early-promotion exclusions remain planning assumptions; unavailable
10726/11010/11031/11232 and native gates remain explicit. This is not a completion
percentage or proof that all remaining routes can be earned on one character.
Controlled travel and observed-state provenance keep their prior limitations.

The audit snapshot SHA-256 is `ca45f7610a2fb43a89eb05bffc3ee201a2c06adb0ccc345be28b4e9b2c8d50dc`.
`docs/research/FRESH_CAREER_RECEIPTS.json` and `FRESH_COMPATIBILITY_PLAN.json`
record this dated checkpoint; `audit_fresh_compatibility.py --check` passes.
Later earning advances the career and must not rerun this historical baseline.

## Fresh post-tournament compatibility audit (2026-10-03)

`career-fresh-tournaments-audit` reloads the ordinary level133/class25/twice-reborn
main and the same four earned peers, including all four used town warehouses,
and requires the complete `revora_fresh_team_twenty_probe` stage2/wins20 snapshot.
The exported snapshot SHA256 is
`6b1b0dd8840759a837bb2d221b7c80272620a7509e9dd42cba15ad3303b339a2`.
The audit is read-only with respect to career progress. Initial missing fixture
imports and a local-name collision were fixed before execution.

There are107 distinct saved identities, up from the historical post-Purple98,
with none removed. The nine additions are Quiz10120/11128, Steed Race11113/11122,
Weekly11116/11118, Monthly11117/11119 and Elite champion11123. Duel Pit, LMS and
Team Arena reward records remain separate accomplishments rather than invented
additional achievement identities.

The337-entry catalog has230 absent identities; the conservative322-candidate
plan has215 absent. This is a receipt inventory, not a completion percentage or
proof that320 identities can be jointly earned. The11 early-promotion exclusions
remain reviewed planning assumptions; unavailable10726/11010/11031/11232 and
native-client gates remain unresolved. The exporter and regenerated Python
compatibility report pass their checks.

Largest remaining producer groups include promotions, observed profile states,
travel, weapon skills, subclasses, runes and collections. Remaining event routes
include additional solo-Arena class opponents/rank progress, Class PK, Couple PK,
Battle Power War and flower deliveries. Team PK/Skill Team PK also require a
qualifying eight-team field even though their reward progress is not represented
by a new receipt in this count. Continue those actual earning paths; do not infer
completion from source coverage or replay completed daily/event fixtures.

## Fresh post-Relic compatibility audit (2026-10-03)

The `career-fresh-relic-audit` phase reloads the same ordinary level133,
class25, twice-reborn character and four earned peers, initializes all five town
warehouses, loads JiangHu, and requires the completed Relic Rush checkpoint.
Snapshot SHA-256:
`7f0d0ff788a0aff3741521924deb6543bcd5f1db974679c1cb1060f5e1230105`.
The audit does not award progress or change the checkpoint. Its initial missing
Server namespace qualification was corrected before execution.

The saved inventory is now128 distinct identities,21 more than the historical
post-tournament107, with none lost. Additions are nobility10318/11120, fourteen
subclass P1/P9 receipts, rune11310/11311/11313 and CTF10230/10301. Chi/JiangHu,
Wave Defense and Relic Rush have their separately checked reward ledgers; their
completion is not counted as invented achievement identities.

The catalog still contains337 identities.209 are absent;194 of the322
conservative candidates are absent. The regenerated JSON input hashes and
`audit_fresh_compatibility.py --check` pass. These counts do not certify a
compatible320 career, public release, or an overall completion percentage.

Next work remains the actual unearned routes: Class PK, Couple PK, Battle Power
War, remaining solo-Arena opponent/rank milestones, flower delivery and the
qualifying Team/Skill Team PK fields. General progression gaps remain41
promotion identities,32 observed-state identities,26 travel identities,
16 weapon-skill identities,10 collection identities and9 class-rune identities.
Those figures include the catalog's existing planning exclusions and must not
be read as all independently achievable on this exact profile. Native acceptance,
four unavailable identities and documented earning prerequisites remain open.

This checkpoint supersedes earlier counts for current planning, without changing
the dated scope or validity of earlier route evidence. Existing event fixtures
are not rerun merely to increase totals. The private candidate remains
`D48A6C00F6BF84889581ADFEABEB924C4C7448C5DC3C6D17F8674129C43665B6`.

## Fresh post-Couple PK compatibility audit (2026-10-03)

The audit reloads the six-character roster and requires the completed
`revora_fresh_couple_probe` step 4 checkpoint, including its nested prior career.
Snapshot SHA-256:
`5fb516baaf42b0437a3a74b767243f78449f1acdca8529b6507e40b777d2093a`.
The main character remains level 133, Warrior 25, twice reborn. No progress is
awarded by this audit, and no prior earning phase is replayed.

There are 134 saved achievement identities, six additions since the post-Relic
128 and none lost: 10127 flower delivery, 10129 mutual marriage, 11114 Battle
Power War, 11131 Class PK, and 11115/11121 Couple PK participation/victory.
Each addition has its separate ordinary earning and cold-recovery evidence.

Of 337 catalog identities, 203 are absent; 188 of 322 conservative candidates
remain absent. The eleven early-promotion planning exclusions and four
unavailable identities remain unchanged. These numbers are a receipt inventory,
not an overall completion percentage or proof of a jointly earnable 320 career.

The next solo-Arena work can reuse the earned Warrior, Archer and Monk peers
for missing class-victory paths, then address the other eligible opponents and
first-nine/rank milestones. Team/Skill Team PK still need qualifying eight-team
fields. General progression gaps include 41 promotion identities, 32 observed
states, 26 travel identities, 16 weapon skills, 10 collection identities and 9
class-rune identities, including existing planning exclusions. Four flower-queen
identities remain separate from completed gift delivery: existing competitors
were preserved, and no ranking victory was fabricated.

`career-fresh-couple-audit` and regenerated `audit_fresh_compatibility.py --check`
pass against candidate D48A6C00F6BF84889581ADFEABEB924C4C7448C5DC3C6D17F8674129C43665B6.
This supersedes the historical post-Relic counts and its then-pending event list.
Native acceptance and live deployment remain pending; no new release is claimed.
