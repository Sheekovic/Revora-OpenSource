# Sky Pass: implemented Revora edition

Latest correction: [admission cleanup race](ISLAND_ADMISSION_RACE.md). A waiting
tick no longer abandons an admission that arrived while it waited for the owner
lock. The corrected build passes the race, genuine-departure and authenticated
claim/restart fixtures; native and deployment gates remain pending.

## Earned prize across actual process restarts (2026-09-28)

`tools/Verify-ArenaProcess.ps1 -SkyOnly -ResetOwnedFixture` passes three actual
game launches and intervening PostgreSQL restarts using private executable
SHA256 `6CF84170A95EF52B68170194D25CE6EA613AA5DD6A6B2F90DFF7ED3D45019E4B`.
The helper compiles with warnings as errors. No gameplay code changed.

An ordinary non-GM profile enters through Daniel's production NPC dispatch,
selects the four saved open gates and the final L80 battle gate, and defeats
the startup-spawned family3024 through production damage/death processing.
The Stage Guard consumes the earned721104 and issues a bound721109 prize pass.
No claim or11019 achievement evidence exists at this point. The character is
saved, then the game exits abruptly without its graceful shutdown path.

A fresh process/database restart and canonical character/item load preserve the
exact prize-pass UID and saved run. The production Sky login recovery returns
the character to Daniel; his normal dialogue restores access to God Cloud.
Claiming pays four bound Meteors and one bound Tortoise Breaker rune, with five
exact item UIDs in one receipt and one11019 evidence row. A further restart
preserves the complete run, receipt, item and achievement snapshots. Attempting
another climb on the same Pacific day leaves all those snapshots unchanged.

Controls: the fixture supplies a level100 profile, NPC/combat proximity, the
clock and gate draws. The production damage receiver receives sufficient damage
to defeat the final monster; attack calculation, movement and native/socket UI
are not exercised here. There is no helper-only world initialization or seeded
quest pass, reward, achievement or completion. This covers the route with four
open gates and one final battle, not every false-gate or final-tier variation.
The earlier isolated suite remains the evidence for those variations.

An initial reload fixture omitted LoadItemStats before the recovery callback's
healing, preventing the next interaction. Adding the normal stat load corrected
the fixture; no server repair was required. The final three-phase run includes
explicit absent-before-claim and exact-reloaded achievement checks. The owned
clone was removed and private PostgreSQL stopped. Native acceptance and live
deployment remain pending.

## Authenticated prize claim and inventory recovery (2026-09-28)

This follow-up supersedes only the claim/reload transport limitation in the
preceding process checkpoint.

The existing three-phase Sky suite now uses actual SRP/DH authentication and game
socket requests for the second and third launches. All phases pass on the same
6CF84170 private executable; both helper programs compile with warnings as errors.

The earned prize phase still uses the controlled server-process route above and
ends abruptly. After PostgreSQL/game restart, normal login recovery returns the
ordinary account to Daniel. The probe sends his NPC opening/confirmation and
God Cloud's claim over the game socket. It verifies the resulting maps and the
immediate inventory's exact five item UIDs, types and binding flags against the
server's earned items. The removed prize token cannot remain as an extra item.

A fresh authentication after the next restart verifies those same inventory
items. Daniel refuses another same-day entry over the socket without changing
the durable run, claim or achievement snapshot. No reward or completion state is
seeded, and the gameplay executable is unchanged.

NPC proximity is still supplied by the fixture. The full climb, combat and
movement are not authenticated in this suite; this result closes only the
earned prize recovery/claim, immediate inventory and duplicate-entry transport
checks. No native client was run or production service changed. Owned clone
cleanup and private PostgreSQL shutdown succeeded.

## Original implementation record

2026-09-15 UTC. Code and 40 isolated route checks pass. Deployment, native tracker
acceptance and genuine-client visual testing remain pending.

## Player route

Speak to Daniel in Dreamland, map 1012 near (155,165). Every ordinary authenticated
character may enter; the installed mission defines no minimum level. Finish an
existing contest first. Each gate identifies the level of its possible opponent.

The first four stages each have five choices. One open gate is drawn and saved
when the climb starts. Choosing it advances to the next stage; the other choices
lead to their corresponding battle platforms. Defeat that platform's monster,
then return its bound pass to its Stage Guard to try another gate. The open gate
does not change on retries. Five distinct choices therefore suffice per stage.
This sealed gate rule is an explicit Revora choice; no historical random weight
is claimed.

All fifth-stage choices require a battle. Its guard exchanges the earned tier
pass for Sky Prize Token 721109 and sends the player to God Cloud. God Cloud
consumes that exact pass and awards four bound Meteors and achievement **11019**
together, once per Pacific day. This fixed reward follows a choice in the
[older official Sky Pass guide](https://co.99.com/ipad/quests/skypass.shtml).
Binding and the daily accounting rule are explicit Revora choices; the
[current guide](https://co.99.com/guide/quests/skypass.shtml) also limits rewards daily.

A full bag preserves a defeated monster's pass entitlement with its guard and
preserves the final reward until enough slots are free. Death, leaving or reconnect
abandons unfinished stages and their issued battle passes. Recovery returns to
Daniel in Dreamland in this edition. An already-earned prize pass stays claimable;
Daniel restores access to God Cloud. All dialogue is available to ordinary players.

## Identities and movement

The [classic dungeon checkpoint](CLASSIC_DUNGEON_RESEARCH.md) records source hashes
and edition differences. The server contains all 25 monster families 3000–3024,
52 required NPCs, the first-stage passes 721100–721103, five final-tier passes
721104–721108 and the prize pass 721109. `SkyPassWorld` validates those identities
and unique spawn records before enabling the feature.

The final families descend in level: 3020 (118) issues 721108 (L120), 3021 (108)
issues 721107, 3022 (98) issues 721106, 3023 (88) issues 721105, and 3024 (78)
issues 721104 (L80). The full-route fixtures verify that order. Existing combat
stats are retained; no historical random loot weight is inferred from them.

Gate and guard positions are retained from the installed NPC table. They stand
on pedestals across narrow gaps; registering their collision footprints makes
some unreachable within the shared six-tile quest radius. Sky Pass uses a bounded
twelve-tile dialogue radius and searches reviewed nearby valid landing tiles.
It does not flatten terrain or permit long player jumps. The fixture exercises
the existing production jump handler's valid-landing/distance rule on a battle
platform, with movement timing bypassed in the offline fixture. Every gate/guard
pair is reachable from the actual NPC transition landing within the dialogue radius.
Native interaction range and visuals still require genuine-client acceptance.

## Persistence and authority

- `revora_sky_runs` owns the current run UUID, sealed gates, stage, selected battle,
  phase, precise issued token UID and battle-start timestamp.
- `revora_sky_steps` preserves an immutable sequence including run ownership,
  gate draws, transitions, issued UIDs and actual defeated monster UID.
- `revora_sky_claims` uniquely owns each character/Pacific-day reward and its
  produced item UIDs. It also prevents a run from being redeemed twice.

Monster credit requires a real owned world entity, its matching selected family,
a canonical nearby player, one death of that monster life and the same run UUID
captured before death processing. A delayed death cannot credit a replacement run,
even at an equal timestamp. Teammate-only credit and client-authored quest status
do not supply these proofs. Every token, transition and payout uses the player's
item lock and a database transaction; post-commit inventory and achievement refresh
follow successful persistence. Failed persistence fences the character until reload.

Mission **6350** is projected through the existing quest status/detail writers.
The installed objective references slot 6, which is populated from saved prize
state. Native Begin only refreshes the authoritative projection. The tracker is
not used as a completion record and its visual behavior has not been live-verified.

## Validation

`tests/PotencyHunt/Verify-PotencyHunt.ps1 -SkyOnly` passes 40 checks:

- Ordinary admission, fake/remote/forged request rejection and native projection.
- Real production player jumps, all five final-tier routes and all 25 actual
  monster/gate/guard associations through genuine NPC and monster-death paths.
- Wrong/distant kills, concurrent duplicate deaths and foreign persisted token ownership.
- Fixed gate retries, full-bag pass recovery and exact final prize-pass exchange.
- Guard/claim rollback, concurrent final reward, daily replay prevention and reload.
- Actual monster-inflicted death, unfinished restart recovery, ordinary travel
  abandonment and a deterministic late-death/replacement-run race.

Staged build SHA-256:
`C4999CB2DA2387233A0BF52342E258621CD340E291116C9F51DDEAC43F3E95BC`.
The batch also passed 612 existing quest/event/progression/wardrobe regressions;
the final tier-mapping correction was followed by another complete Sky route suite.
Tests use disposable schema state; the live executable and client remain unchanged.

## Daily reward correction (2026-09-21)

New runs now guarantee one bound level-1 Tortoise Breaker rune (4030101)
beside their Meteors. The earlier P7 upgrade was missed by the initial daily
reward audit; pending claims now ignore the obsolete P7 flag. Existing issued
items and completed receipts remain unchanged. New `rune_reward` flags default
false on old contracts; Snake ferry retries and Sky transitions preserve the
saved terms. This is a Revora reward choice, not an original-game reward claim.

Reward UIDs remain in the existing claim array, committed together with final
completion. Capacity includes the rune and, for Sky Pass, the consumed prize
pass's freed slot. Existing contracts keep their Meteor-only payout.

Isolated build and 45 route checks pass, covering ordinary earned travel,
old contracts with obsolete P7 flags, full bags, final-claim rollback, concurrent
claims, exact bound rune receipts, reload and no daily P7. Sky covers all 25
monster/guard associations; Snake covers the 35-jump crossing. Log:
`obj/chi-refill-validation/sky-rune-tests.log` (ignored).
Native client acceptance and deployment remain pending.


## Fresh career earned climb and cold reward (2026-10-03)

The existing ordinary level124 Warrior (UID1096322) continues from the saved
introduction checkpoint, with no supplied gear, funds, quest progress or tokens.
Eight existing items move through the ordinary Phoenix warehouse handler to
make bag space. Daniel admits the character; unforced gate choices across five
floors require eight monster kills, completed with eleven calculated melee
attacks. The earned prize pass survives a private game/database restart.

The normal login recovery returns the character to Daniel, who restores access
to the unpaid summit. God Cloud consumes that exact pass and issues four bound
Meteors plus one bound Tortoise Breaker rune, with one11019 sky-pass-v1 receipt.
There is no daily P7. Exact reward ownership and the nested prior career survive
cold reload; Daniel refuses a second daily admission without another reward.

Validation: `tools/CheckCandidateFreshSky.cs`, phases `career-fresh-sky-earn`,
`career-fresh-sky-claim`, `career-fresh-sky-reload`. Initial harness admission
failed before gameplay because the new phase was omitted from its allowlist.
The corrected earned phase passed. The claim phase paid correctly but its final
replay assertion included the controlled walk from return landing163,157 to
Daniel155,167. A recursive before/current comparison found only those two
coordinate changes; all rewards, claims, steps and prior career were unchanged.
The private checkpoint alone was advanced after exact SHA-256 checks:

- Saved paid snapshot: `e35c37ca181637a7bc5a7ec738f6c8ebed95490bb7dfbdc59d7675efd97d9b83`.
- Reviewed current snapshot: `ce7fb52e5f46aa7bde0d2c8ef35835781a92d21b61ed818e875303133ceecca4`.

The fixture now approaches Daniel before saving the paid checkpoint. No run,
reward, earning receipt or item was reset or reissued. Cold reload and replay
pass with36 bag items. Evidence is handler-level: approach and attack cooldowns
are controlled, normal map respawns run, player pet/screen timers are omitted,
and this does not establish retaliation, difficulty or native movement quality.
Private prerequisite terrain is staged as in the preceding career batch.
Game binary SHA-256 remains
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
No live deployment or client acceptance is claimed.
