# Fierce XP: Way of Heroes103

Status: milestone103 enabled; ordinary earning, claim, consumption and cold persistence validated server-side. Native acceptance and deployment remain pending.
Inspected source baseline: bc167c68 (2026-10-02). This is local code/catalog
evidence, not verification of historical client behavior.

The installed stagegoal entry1-3 requires killing20 monsters while an XP skill
is in effect: task4, require20, progress1, one bound EXP Ball723700.
The claim remains disabled until the earning path below is implemented.

## Findings

- `Game/Features/MonsterKoCounter.cs`, AcceptedDeath, validates the canonical
  ordinary player, exact map-owned monster and accepted killer, then prevents
  repeat credit for that monster life. Its achievements are10/100/300; it does
  not currently persist20 or model every XP skill.
- `Game/Entity.cs`, HasLiveKoMode, checks only Cyclone, Superman, SuperCyclone,
  DragonCyclone, Oblivion, ManiacDance and Omnipotence with live timestamps.
  This cannot alone satisfy the cross-class requirement.
- `Game/Attacking/Handle.cs`, the inoffensive XP switch, also activates Fatal
  Strike and defensive buffs. CanUseSpell checks NeedXP; PrepareSpell removes
  XPList. XPList therefore indicates readiness, not proof of an active XP effect.
- ReceiveAttack carries SpellInformation into ReceiveEntityAttack, but terminal
  calls pass only the killer to Entity.Die. Direct XP spell identity must be
  carried to the accepted death without trusting a client spell ID or allowing
  a previous cast to credit an unrelated kill.
- Entity.Die routes many event encounters through custom death handlers before
  the generic DieCore path. A generic death hook alone is not proof of complete
  coverage. Inspect each relevant accepted-death seam and retain encounter
  ownership protections.
- WildMonsterVictory lives in `Game/Features/EquipmentAchievements.cs`; its
  ordinary transaction path requires level120+ monsters. It cannot be reused
  unchanged as the general novice XP-kill producer.

## Next implementation contract

1. Establish valid sustained-effect and direct-cast attribution from production
   spell admission, including transformed/delayed paths where applicable. An
   installed NeedXP row alone does not prove a playable or damage-dealing skill.
2. Bind credit to the canonical player, accepted monster life and actual active
   effect/cast. Exclude guards, revivers, companions and duplicate death callbacks.
   Do not alter existing KO achievement semantics to approximate this task.
3. Persist the earned20-kill evidence, then use the existing transactional claim
   route for progress, one bound ball and the unique claim receipt. Explicitly
   establish whether the20 must share an activation from available evidence;
   do not silently equate all lifetime monster kills with XP kills.
4. Test actual ordinary activation and kills for sustained and direct families,
   19/20 boundary, expired/readiness-only states, refused casts, unrelated damage,
   duplicate callbacks, concurrent claim, consumption and separate cold reload.
5. Keep103 unchecked until these earning and reward checks pass. Native client
   acceptance and live deployment remain separate gates.

## Local catalog inventory

These are candidate XP rows for handler inspection, not confirmed playable paths.
Catalog SHA256: `97ECB413566F5FEA541090D1A1C1498ED2ACF0874EFA6056984583C65C187A71`.
The loader reads NeedXP from column21 and spell ID from column1.

| ID | Catalog name |
| --- | --- |
| 1010 | Lightning |
| 1015 | Accuracy |
| 1020 | Shield |
| 1025 | Superman |
| 1040 | Roar |
| 1050 | Revive |
| 1105 | Restore |
| 1110 | Cyclone |
| 1125 | Volcano |
| 1270 | Golem |
| 1280 | WaterElf |
| 1320 | FlyingMoon |
| 1360 | NightDevil |
| 4010 | SummonBat |
| 4020 | SummonBatBoss |
| 5001 | SpeedLightning |
| 5100 | IronShirt |
| 6010 | ShurikenVortex |
| 6011 | FatalStrike |
| 8002 | Fly |
| 8030 | ArrowRain |
| 10309 | ChainBolt |
| 10390 | Oblivion |
| 11050 | CannonBarrage |
| 11060 | Blackbeard`sRage |
| 11610 | BladeFlurry |
| 11970 | SuperCyclone |
| 12290 | DragonCyclone |
| 12700 | ManiacDance |
| 12860 | Omnipotence |
| 13300 | Blackbeard`sRage |
| 13361 | Volcano |


## Direct-cast ownership prerequisite (2026-10-02)

The [official Way of Hero guide](https://co.99.com/guide/guides/hero_system.shtml)
requires20 XP-skill monster kills and one bound EXP Ball. It does not explicitly
require a single activation. Revora will accumulate qualifying XP kills across
activations; ordinary non-XP kills will not count. This is a documented reading
of the guide, not a claim of recovered original internal counter semantics.

`XpCastAttribution` now owns a synchronous scope around each Handle invocation.
PrepareSpell records proof before consuming XPList only when the canonical
ordinary player owns that scope and the supplied spell is the installed, learned,
available XP spell at the learned level. Direct-hit lookup requires that exact
spell object, player body and unchanged map-transition version. Nested casts
start without their parent's proof; disposal restores the parent scope. Proof
cannot flow to timer threads and is cleared on return or exception unwind.

Private `career-xp-attribution` passed using candidate SHA256
`D08D0734A17AA75E988AC4AFF83AC17C464AD9CC26DB0918E4891C29EDDDC7F2`.
It checks readiness consumption, no proof outside the scope, unprepared scope,
forged spell object, another thread, nested call, away-and-back map transition,
exception cleanup, missing learned skill and a later ordinary spell preparation.
The fixture supplies the learned skill and exercises real preparation; it does
not prove skill acquisition or an actual XP monster kill. An initial fixture
failure exposed an unavailable supplied skill; it was corrected to Available=true.

- [x] Build and validate direct-cast ownership isolation.
- [ ] Connect this proof to accepted monster death and durable qualifying kills.
- [ ] Implement sustained, transformed and delayed XP-effect lifetime ownership.
- [ ] Enable103 only after20-kill earning, claim, consumption and cold persistence.

No reward or achievement is enabled by this prerequisite. No deployment occurred.

The existing `career-champion-blade` real-handler combat regression also passed
with the same executable, covering normal Blade Tempest and Twilight callbacks.


## Direct XP kill receipts (2026-10-02)

ReceiveAttack now scopes proof to the exact target and prepared XP spell after
combat admission. Nested damage starts its own target scope. AcceptedDeath uses
that proof only after validating the canonical player, accepted killer and exact
map-owned, dead monster. Guards, revivers and companions are excluded. Each
monster life has a fresh receipt identity; duplicate death callbacks cannot add
another receipt. Ordinary damage carrying an XP spell object without an accepted
cast cannot qualify.

`revora_xp_kills` stores accepted life, player, map, monster family and spell.
An entity-row lock serializes the player's receipt count. At20 the writer stops;
a weak per-body completion cache avoids repeated database work afterward. Inserts
are read back in the transaction before commit. Database errors fence the session
through the existing quest failure path. Recovery of a death whose receipt could
not commit is **not yet implemented or accepted**;103 remains disabled while this
and other effect families are unfinished.

Private `career-xp-kills-earn` and `career-xp-kills-reload` passed on candidate
SHA256 `6F543F4B92D7752673B726C1191D4A7AABFB70E28F43F1DFEE3C233721E33329`.
An ordinary account with a supplied learned Lightning skill uses real Handle
casts against supplied ordinary monsters. Twenty actual kills save twenty
receipts; unowned direct damage and duplicate death notifications save none.
A separate process verifies all20 retained receipts and no103 claim. No skill
acquisition, sustained XP effect, reward claim or native acceptance is implied.
Initial checks found an unnamed fixture monster and an unsupported ushort database
map parameter; the fixture now supplies a name and the writer uses an int.

- [x] Connect synchronous direct XP damage to accepted monster life receipts.
- [x] Validate real Lightning kills, duplicate refusal and cold receipt retention.
- [ ] Complete sustained/transformed/delayed effects and persistence-failure recovery.
- [ ] Validate20-kill claim, reward consumption and cold claim persistence before103.

A retained-fixture rerun also killed another monster with a real XP cast after20
and verified the receipt count stayed20. The ownership-isolation phase passed
again with the same executable. Existing receipts were preserved throughout.


## Transactional death decision and write recovery (2026-10-02)

This supersedes the earlier post-death receipt writer. The generic monster death
path now owns the monster-life lock through its death decision. After cancellation
branches and the existing unarmed-victory decision, XpKillLedger commits the XP
receipt before installing death. It acquires the character lock without waiting
and locks the persisted owner row in the transaction. A refused insert or database
error leaves the monster alive. The same life identity resolves uncertain commits:
an existing receipt allows only its recorded killer to finish the death, without
requiring another XP activation or inserting another receipt. A rolled-back XP
attempt followed by an ordinary hit does not earn XP credit. Respawn starts a new
identity; already completed20-receipt owners retain the bounded cache behavior.

Private `career-xp-recovery-earn` and `career-xp-recovery-reload` passed against
SHA256 `3826861801043078A2657D30FB927889093160ACB6A5EE3F86379D7B064026A8`.
The fixture uses ordinary accounts and real Lightning handlers. A PostgreSQL
trigger suppresses receipt insertion: HP remains10, no receipt is saved and retry
succeeds after removing the trigger. An injected exception after commit leaves a
retained receipt and a living monster; another player cannot take it, and the
recorded killer completes it with an ordinary hit without duplicate credit.
Another suppressed XP attempt followed by an ordinary finishing hit earns nothing.
A separate process compares the exact saved receipt JSON and confirms the other
player has no receipt. Supplied skill/monster prerequisites remain explicit.

An initial check exposed the missing character lock in the new writer; that was
fixed with bounded lock acquisition. One subsequent candidate run stalled outside
PostgreSQL (no blocked database queries). Its exact candidate executable was
terminated, its wrapper completed cleanup, and the next instrumented run passed.
The transient stall's cause is not established; this is not a full concurrency or
live reliability acceptance claim. Temporary step-file tracing was removed after
localizing the successful run. The recovery phase now has a90-second bound.

- [x] Persist the direct-XP death decision before irreversible generic death state.
- [x] Validate insert refusal/retry, uncertain-commit ownership, ordinary-hit
  recovery, no invented XP on rollback and cold receipt persistence.
- [ ] Finish sustained/transformed/delayed XP lifetimes and103 reward validation.
- [ ] Native client acceptance, wider concurrency acceptance and deployment.

The same executable also passed retained XP earn/reload and cap checks, cast
ownership isolation and the existing Champion Blade Tempest/Twilight regression.


## Owned timed XP effects (2026-10-02)

XP credit now also observes status effects applied during an owned, prepared XP
cast. The record binds the ordinary recipient body/session, installed XP spell
and exact status-flag object. A live record follows the actual flag's timer,
including legitimate duration increases/decreases; expired or replaced flags
cannot reuse it. RemoveFlag and flag clearing discard the record. A manually
added flag or XP readiness alone does not establish an earning source. Timed
records are session-local; earned kill receipts remain durable.

The mapping includes Cyclone, Superman, Oblivion, Fatal Strike, Accuracy, Shield,
Fly, Shuriken Vortex, Blade Flurry, Super Cyclone, Cannon Barrage, Blackbeard's
Rage, Dragon Cyclone, Maniac Dance, Omnipotence and Chain Bolt. This list is
implemented mapping coverage, **not acceptance of every underlying skill**.
Raw activation branches that previously skipped PrepareSpell now explicitly
capture XP preparation before consuming readiness. WarriorModes owns a cast
scope for its dedicated admission path. SuperCycloneTime is assigned to the
caster, matching its flag and timestamp, rather than to the attack target.

Private `career-xp-effects-earn` and `career-xp-effects-reload` passed against
SHA256 `CC3ECE84B68A1DE440DB34CE64ABEC24BC113BFAD6586B970ECBF6BC2B81D882`.
Four actual activation handlers (Cyclone, Superman, Fatal Strike and Shuriken
Vortex) establish credit for calculated melee finishing hits through ReceiveAttack.
The fixture checks readiness-only refusal, manually added flag refusal, expiry,
legitimate duration adjustment, removed/re-added flag refusal, duplicate death
notification and exact receipt snapshots after a separate process.

The fixture supplies learned skills and ordinary equipment prerequisites; it does
not prove skill acquisition or the complete attack sequence of Fatal Strike or
Vortex pulses. Retained equipment must load its stat/weapon cache before casting;
the fixture now does so. Existing receipts from earlier attempts are retained,
and version2 probe evidence is separate from the earlier timer implementation.

- [x] Track owned timed XP status lifetimes without altering KO achievement rules.
- [x] Validate the four activation/kill routes above, expiry and cold receipts.
- [ ] Validate remaining mapped activations, transformations and delayed attacks.
- [ ] Finish103 claim, reward use and persistence acceptance; stage remains disabled.

Direct-cast isolation and the existing Champion Blade Tempest/Twilight regression
also passed on this executable. No client patch or live deployment was made.


## XP transformation ownership (2026-10-02)

Accepted Golem1270, WaterElf1280 and NightDevil1360 casts now record an owned XP
transformation after the real form and activation timestamp are installed. The
record requires the installed XP spell and the expected form IDs from the source:
214/274,213/273 and217/277 respectively. Credit requires the canonical ordinary
body/session, that form, the same activation timestamp and an unexpired actual
transformation duration. Assigning TransformationID clears prior proof, including
assignment of the same ID; non-XP transformations cannot inherit an old receipt.
No transformation is persisted by this observer; earned kill receipts are saved
through the existing transactional XP death decision.

Private `career-xp-transform-earn` and `career-xp-transform-reload` passed on
SHA256 `31CD705B240A873B038013982D10C6F0688AB72355AE2B8C840C2F1471741784`.
An ordinary character with supplied learned level0 skills uses real cast handlers,
then calculated melee and ReceiveAttack to kill supplied ordinary monsters.
All three casts earn receipts; duplicate notification, expired transformation,
reassigned form and Untransform do not add credit. A separate process compares
the exact saved receipt snapshot and confirms no active transformation proof.
Higher-level form IDs are source-mapped but not separately exercised in this
fixture. One initial finishing-hit assertion failed; the same executable passed
the full rerun. Its cause was not established, so broader concurrency acceptance
remains pending as already recorded above.

- [x] Implement Golem/Water Elf/Night Devil owned XP lifetimes.
- [x] Validate level0 ordinary activation, calculated kills, expiry/replacement/
  cancellation refusal and exact cold receipts for all three.
- [ ] Complete remaining mapped activation/delayed paths and103 claim/reward use.
- [ ] Native acceptance and live deployment remain pending.

Direct-cast ownership isolation and the Champion Blade Tempest/Twilight
regression also passed on this transformation candidate.


## Remaining timed routes and milestone103 acceptance (2026-10-02)

- [x] Restore XP Accuracy1015 applying the existing StarOfAccuracy status. Its old
  branch consumed readiness but its effect code was commented out. This restores
  the existing status; it does not claim a new accuracy formula or combat rebalance.
- [x] Validate ordinary activations and calculated monster kills for Accuracy,
  Shield, Fly, Chain Bolt, Oblivion, Cannon Barrage, Blackbeard's Rage, Blade Flurry,
  Super Cyclone, Dragon Cyclone, Maniac Dance and Omnipotence. Exact saved receipts
  survive a separate process for all12 ordinary characters.
- [x] Enable the installed103 contract: task4, require20, progress1, open level1,
  one bound EXP Ball723700. Require20 saved accepted XP kill receipts, independent
  of runtime KOCount and prior client progress; reuse the existing atomic claim.
- [x] Refuse the real2830 claim at19 earned Lightning kills. Earn kill20 using
  an actual Shuriken Vortex6010 activation and its production6012 pulse handler.
  The same pulse after cancellation leaves a fresh monster alive.
- [x] Refuse an unrelated ordinary character and busy trade. Eight concurrent
  claims produce one bound ball; actual item use grants exactly one ball's XP,
  including duplicate-use refusal. Claimed progress, reward receipt, XP, daily
  usage and possessions match after a separate process; repeat claim changes none.
- [ ] Native client display/control acceptance and live deployment remain pending.

Timed route earn/reload passed against
`5DA65B06FD9E365D0C76A736D890F26F2C6FA7B25CE550D91EE936B0C42606FC`.
Final `career-xp-claim-earn` and `career-xp-claim-reload` passed against
`030FA2BD758C788676D46727497C44C9D5BD139CD5603B97B6A6A02CB48F10CD`.
The initial claim build correctly failed its catalog guard because the expected
Require20 argument was omitted; the final build supplies that explicit contract.

Fixture scope: profiles, learned skills, equipment and the separate PathOfShadow
prerequisite are supplied. Real equipment handlers, cast handlers, damage/death,
claim and reward-use handlers earn the tested progress. No skill acquisition,
native animation or timer-scheduler acceptance is implied by these fixtures.
The claim fixture uses level120/rebirth1 so its bound EXP Ball can be consumed.
The timed-route fixture prepares profiles before loading, preserving progression
consistency; an initial in-memory profile mutation was refused by character save.
The optional weapon setup now respects spells with no catalog weapon requirement.

Revora counts qualifying **player-owned finishing kills** across XP activations.
Companion/guard/reviver bodies cannot produce receipts; a summoned pet or an
instant heal/revive does not create a lasting XP effect on its owner. Unused
catalog entries are not represented as restored skills. Direct damage, supported
active timed effects and XP transformations feed one capped20-receipt journal.
This milestone is not a claim that every catalog skill or historical packet is
implemented. Historical unchecked103 sections above describe earlier checkpoints
and are superseded by this acceptance section. Earlier transient candidate issues
remain recorded; native/live reliability gates are not marked complete.
