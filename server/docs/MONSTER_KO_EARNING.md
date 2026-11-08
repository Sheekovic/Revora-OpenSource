# Monster KO earning path: implementation evidence

Status: accepted-death counter and persistent threshold producers implemented
and tested; normal-cadence Cyclone combat with periodic buffer expiry now passes.
Activation/expiry concurrency and native acceptance remain separate gates.

## Confirmed identities and pre-fix behavior

The installed 6711 Achievement.dat, SHA-256
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`,
identifies 10520, 10521 and 10522 as monster-hunting KO thresholds 10, 100
and 300. Its descriptions do not specify accumulation across XP sessions.

The current server treats KOCount as an XP-chain counter. Thread.cs resets it
when OnKOSpell is false, after submitting it to KoBoard.New. Cyclone, Superman,
Oblivion and Maniac Dance activation reset it when starting outside a KO mode;
Dragon Cyclone resets it on activation. Entity.OnKOSpell also includes Super
Cyclone and Omnipotence. These are current Revora behaviors, not a claim that
all original-client chain rules have been recovered.

Game/Screen.cs:monsterBuffersCallback increments the killer's ushort KOCount
while preparing a monster death packet. This callback belongs to each observing
player. The Ghost/Killed check and increment have no shared death-claim lock.
Thus source inspection exposes a possible multi-observer race and makes credit
rely on screen processing. This is a concurrency finding, not yet a reproduced
live incident. The counter can also wrap at ushort.MaxValue.

Entity.Die dispatches to owned encounter services before DieCore. For example,
DisCityQuest.TryDie locks the character, saves the defeat and loot, commits,
then invokes DieCore. PotencyHunt.Die instead owns a slot DeathHandled guard.
A universal pre-dispatch counter would wrongly credit rejected or rolled-back
encounter defeats. Adding only a screen-callback achievement hook is unsuitable.

## Next implementation batch

1. Give each accepted monster death one stable claim, distinguish respawn lives,
   and snapshot its player killer and active XP chain. Preserve encounter-owned
   commit boundaries; reject companions, guards, fake actors and stale sessions.
2. Advance a server-owned chain count once per accepted death. Screen packets
   read the accepted count and never mutate it. Avoid taking a character lock
   while holding a monster/population lock in the opposite order.
3. Commit threshold evidence with owned death receipts where available. For wild
   kills, persist deduplicated receipts before exposing credit. No database write
   per observing screen and no lifetime-kill substitute for the XP chain.
4. Keep completed achievements through relog; interrupt unfinished chain state
   on disconnect/restart. Define chain activation/expiry ownership together with
   the counter so the buffer timer cannot race a kill increment or reset.
5. Verify actual lethal combat through 9/10, 99/100 and 299/300; two observers,
   concurrent lethal calls, respawn, inactive/expired chains, new sessions,
   rejected encounter deaths, suppressed evidence writes and full reload.

No producer is enabled by this document. Native XP/KO presentation and live
acceptance remain separate gates. No new class restriction is proposed.

## Accepted-death counter implementation

MonsterKoCounter now records one count per monster life from Entity.DieCore,
after death state is assigned. It requires the canonical ordinary player,
matching killer and map-owned monster, and excludes companions, guards and
revivers. Revival from zero HP reopens the life under the same lock used by
snapshot/record operations. A death outside an active KO mode is consumed
without increasing the chain; activating a mode cannot replay that death.

Screen callbacks read the saved per-death snapshot. Concurrent increments on
different monsters use the killer's KO lock. The ushort wire count saturates
rather than wrapping. Existing XP activation/timer reset callers still own
when the chain starts or ends; fully synchronizing flag expiration and resets
with future persistent progress remains unfinished.

Build and 17 focused fixture checks pass (one shared baseline check plus sixteen
KO checks), including an actual Entity.Die call without a screen callback,
64 duplicate death notifications, 64 distinct concurrent deaths, read-only
snapshot observations, respawn, immutable earlier snapshots, inactive chains,
replay after activation, eligibility exclusions and saturation.
Run the maintained harness with `-MonsterKoOnly`. Most cases call the accepted
receipt observer directly; this does not claim full lethal-packet or XP-timer
acceptance. No achievement threshold is awarded by this counter-only batch.

## Durable threshold receipts and achievements

Accepted counts 10, 100 and 300 now save one durable milestone per character
and threshold. The receipt is committed before achievement projection. Projection
inserts 10520, 10521 or 10522 and marks the receipt completed in one transaction.
A duplicate chain cannot create a second receipt or a second achievement.
The progression read path recovers pending receipts on login, so suppressing an
achievement write or receipt completion does not require earning the chain again.

The counter does no database work on other counts. At most three milestone rows
exist per character. Writes use parameters and no credentials or packets are
stored. Database I/O and progression refresh run after releasing the monster
life lock. Existing encounter-owned locks may still be held by the caller.

The staged build and 29 focused checks pass. Tests run 300 actual Entity.Die
calls with canonical ordinary characters, inspect all six threshold boundaries,
reload the full character, retry in another XP chain, and inject suppressed
achievement/finalization writes followed by login recovery. XP activation and
monster construction are controlled fixtures and loot is suppressed to isolate
KO behavior. These are production death-path tests, not full lethal-packet tests.

An initial receipt-insert failure fences the player and cannot grant credit;
there is no durable recovery record if the database never accepted that receipt.
The accepted monster death is not rolled back by this separate receipt path.
This limitation must not be described as a transaction spanning monster death,
loot and achievements. Full XP activation/expiration/reset concurrency, combat
packet routes and genuine client presentation remain open acceptance work.

## Lethal damage and XP deadline checks

The focused suite now drives all 300 threshold kills through Handle.ReceiveAttack,
including a nonlethal hit that must leave the counter unchanged. This covers the
server damage/death handlers with calculated damage; it does not cover native
packet decoding, attack calculation, target selection or XP-skill activation UI.

A retained XP flag no longer implies a live KO mode. Entity.HasLiveKoMode checks
the server timestamp and positive duration for Cyclone, Superman, Super Cyclone,
Dragon Cyclone, Oblivion, Maniac Dance and Omnipotence. Exact expiry is inactive.
Accepted KO increments and Cyclone/Superman kill-time extension use this check,
so a kill cannot revive an already expired timer. Existing activation resets now
also distinguish a live chain from a stale retained flag.

The buffer callback drains an expired KO counter under its counter lock and only
submits a nonzero completed count to KoBoard. It rechecks the mode while holding
that lock. Skill activation still mutates timestamps/flags through its legacy
paths; this is not proof of a fully atomic activation/timer state machine.

Build and all 40 focused checks pass, including seven exact-deadline cases,
nonlethal damage, lethal threshold credit, expired-flag rejection, no expired
kill extension, single counter drain and preservation of a live chain. Native
acceptance, activation/reset concurrency and live deployment remain pending.


## Retained hunt, XP activation and cold receipts (2026-10-01)

The retained ordinary Windwalker continues from the Snow Banshee loot checkpoint.
Preparation uses ordinary Stone City storage and the installed trainer's option25
when Omnipotence12860 is missing. The hunt buys compatible weapon626004 from
silver shop5 at its loaded price, equips it normally, and restores the previous
bow afterward. The new weapon remains owned; existing assets are preserved.

One hundred loaded wilderness monsters die to normal attack calculations. Their
accepted deaths charge XP; the actual BuffersCallback projects the XP-ready flag,
and the decoded spell handler activates Omnipotence with its weapon requirement.
Three hundred further loaded monsters die to normal melee calculation while the
mode is live. No KO counter, XP charge, XP flag, damage, HP or monster death is
assigned. Thresholds10/100/300 receive exactly three completed durable receipts.
The measured controlled KO loop took3.55 seconds.

The first hunt also earns an unbound Meteor from an actual monster death and
collects it through a sealed floor-pickup packet. A repeated pickup leaves the
full snapshot unchanged. Its UID/type/collector join the saved monster-origin
receipt and achievement10524. The Meteor roll is controlled; its configured
chance is unchanged. Other ordinary hunt rewards remain preserved.

Phases `career-retained-hunt-prepare`, `career-retained-hunt-earn` and
`career-retained-hunt-reload` pass against unchanged private game SHA-256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
A fresh game/database process retains all four achievements, their receipts,
the exact Meteor and prior career. The transient KO counter correctly reloads0.
Achievement refresh neither adds receipts nor changes owned state. Final
checkpoint: `revora_retained_hunt_probe` step2 / `RetainedHuntSnapshot`.

The first earning attempt reached100 kills and collected the Meteor, then correctly
failed to activate Omnipotence with the bow. Recovery compared the full snapshots:
only the100-kill 700-CP reward, single Meteor, its pickup receipt and achievement
were added. An exact snapshot hash guarded acceptance of that retained progress;
no reward was reset or duplicated. A compatible silver-bought weapon resolved the
spell prerequisite, and a fresh100 kills recharged the nonpersistent XP state.
The one-time recovery code was removed; final cold verification passes.

Controls and limits: NPC/monster proximity and attack cooldowns are advanced by
the fixture. It uses an offline send queue for one buffer callback, not an
authenticated socket or native client; the timer readiness stamp is controlled.
This proves production charge/activation/kill/receipt paths under those controls,
**not** that300 single-target kills fit a naturally paced40-second Omnipotence.
Representative AoE/rate/activation-expiry concurrency and genuine-client display
remain unverified. These receipts do not establish a fully compatible320 career.
There is no gameplay change, live restart or deployment in this batch.

## Ordinary attack cadence and synchronized status expiry (2026-10-03)

A focused follow-up uses the earned fresh character's learned Cyclone1110 and
repaired equipment.100 calculated wilderness kills charge XP; the real buffer
callback exposes XP readiness and the decoded learned spell activates Cyclone.
Each subsequent attack waits at least510ms and passes the normal500ms production
gate. No attack timestamp, XP counter, KO counter, flag, duration, damage or
monster HP is assigned. Proximity is still controlled; this is not native travel.

The first300-kill chain took160,875ms and its three achievements survived cold
reload. That fixture called the buffer only for activation, so it did not prove
periodic flag expiry. Source inspection found that accepted kills extend the
Cyclone/Superman KO timestamps while the separate status flag retained its original
20-second deadline. The generic buffer expiry loop can therefore remove an
otherwise live, kill-extended XP mode.

`Entity.KOSpellTime` now updates the existing mode flag's remaining duration and
refresh timestamp together with its corresponding KO timer, retaining the20-second
cap and remove-on-death behavior. Other XP modes and their duration rules are
unchanged. This does not claim atomic activation/expiration under concurrency.

The focused10-check regression fails the old4A8F6167 build on the Cyclone status
deadline and passes the new private build for both Cyclone and Superman,
including matching durations, the cap, death removal and eventual expiry.
Run `tests/MonsterKoTimer/Verify-KoTimer.ps1`. The fixture uses synthetic timers
and no database; the separate saved-character run checks normal combat/buffers.

The corrected ordinary-character run passes300 kills in161,424ms, with the real
`BuffersCallback` invoked every two chain kills. It remains active well beyond
the catalog's20-second initial duration through normal accepted-kill extension.
Another game/database restart preserves the exact nested snapshot, three completed
KO milestones and achievements10520/10521/10522; the transient chain resets0.
The second chain creates no duplicate milestone/achievement rows. There are no
attack-stamp resets or timer overrides in either normal-cadence run.

Evidence: [fresh normal KO](research/FRESH_NORMAL_KO.json). This closes the specific
attack-cadence/chain-duration prerequisite through learned Cyclone, not all XP
modes, natural target-to-target navigation, concurrent state-machine ownership or
native presentation. Private build18CAC3A8 preserves the previous4A8F6167 binaries
and manifest under ignored `obj/ko-timer-install`; no live deployment occurred.
