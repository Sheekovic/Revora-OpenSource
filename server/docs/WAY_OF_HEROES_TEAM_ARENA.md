# Way of Heroes: Team Qualifier victories

Implemented and privately validated 2026-10-02; not deployed live.

| Stage | Requirement | Opening gate | Bound reward |
| --- | --- | --- | --- |
| 305 | One saved Team Qualifier victory | Level61 | Praying Stone1200001 |
| 507 | Twenty saved Team Qualifier victories | First rebirth, level15 | EXP Ball723700 |

These are installed StageGoal definitions. Higher rebirth passes the existing
opening-gate rule. Victory credit belongs to each admitted member on the winning
side, including non-captains. The predicate joins saved result statistics to the
result, admission and original roster. It checks the winner/loser leader pair,
claimant's side, complete participant counts and agreement of every saved member's
outcome with the original admitted side. Old results without admission evidence,
queueing, entry alone, losing and cached counts do not qualify. Wins are cumulative,
not restricted to one day. Normal committed surrender wins also count.

The existing result writer commits the full roster and result atomically. The
ordinary milestone transaction validates saved eligibility and writes its bound
reward, stage state and unique claim receipt together. No GM, owner-name or leader
restriction is added to these claims.

## Private evidence

CheckCandidateTeamArenaClaims.cs supplies four non-GM first-rebirth level130
characters and forms two teams of two. career-team-arena-claims-earn/reload verify:

- [x] Before the first victory and after admission, both winning-side players are
  refused stage305. Stage507 refuses through nineteen victories.
- [x] Twenty normal sealed signup/acceptance sequences enter the owned team map.
  Melee attacks defeat both opposing members; normal result coordination saves
  all four outcomes and returns the players.
- [x] Eight concurrent claims per winning player award one bound seven-day
  Praying Stone after win1 and one bound EXP Ball after win20.
- [x] Both winning players consume their own stone for604800 blessing seconds,
  consume their milestone EXP Ball for experience and reject item-use replay.
- [x] Both defeated players are refused both milestone claims.
- [x] Actual private game/database restart preserves exact scoped claims, items,
  blessing/experience, admission rows, result rows and member statistics for all
  four players. Post-reload claims change none of these snapshots.

The first fixture attempt reached signup outside the scheduled Team Qualifier
window and correctly did not match. The fixture then explicitly opened the
in-process admission switch and restored its prior value on completion; no
production calendar change was made. The retained players were reused, not reset.
Earning and cold reload then passed.

Game SHA256:
5460E27ADA6CCCC73047BAF5B1E181A3BA8D2332CF56C1227640A479F94B4298.
The committed-source overlay excludes unrelated owner WIP. Fixture levels, teams,
combat stats, clock and shortened waits are controlled inputs. These checks do not
prove natural scheduled matchmaking, combat balance, native UI acceptance or live
deployment. Native-client and release gates remain pending.

## Fresh two-versus-two first victory (2026-10-03)

The earned roster now completes a normal two-versus-two Team Arena match.
Archer1096324 and Monk1096326 advance from50 to70 using28 shop-bought EXP Balls
each:756 earned CPs per player,1,512 total. The real ten-ball daily cap refuses
attempted eleventh uses, and three controlled dates cover each training path.
No level, experience, equipment, attack power, defence or HP is supplied.
One main-character item is stored normally in Desert City for reward capacity.

Warrior1096322 and the Archer form one team; Trojan1096323 and the Monk form
the other using actual create/invite/accept packets. Ordinary Team Arena
signup/accept packets enroll all four. Three calculated melee attacks defeat
the opposing roster. The saved match records two winners and two losers, all
four leave their match, and any dead loser revives through the normal handler.
The fixture then exits both teams and saves the unchanged fifth roster member.

Preparation, earned match, reward claim/use and final reload run in separate
private game/database processes. Both winners claim Way of Heroes305 once,
receive one bound1200001 and consume it for exactly604800 additional blessing
seconds each. Repeat305 claims, losing-player305 claims and premature507 claims
leave the complete snapshot unchanged. Final cold comparison includes the
nested career, qualification item/currency changes, team statistics/admissions,
match result, daily allowances and all five loaded bag UID sets.

The first earn attempt failed while constructing an invitation because the
main synthetic session lacked loaded friends/partners. It earned no match or
prize. Loading normal social data fixes the harness; the retry required the
exact saved preparation snapshot without reseeding or replacing it. The failed
redacted log is preserved. All four final phases pass. The authoritative
checkpoint is `revora_fresh_team_arena_probe`, stage3.

This closes the fresh first-victory305 route only. Twenty-win507, full-field
Team PK/Skill Team PK, natural combat timing, native acceptance and deployment
remain open. Scheduler/cooldown/proximity and training dates are controlled;
synthetic opponents do not establish competitive balance. No production code
or game build changed. Candidate SHA256 remains
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.

## Fresh twenty-win milestone and rebirth refusal (2026-10-03)

The same four earned players complete19 additional normal two-versus-two
matches, each with three calculated attacks (57 additional,60 across20 matches).
Every match uses sealed signup/acceptance, admitted rosters, normal combat
settlement, return, revival and team exit. Each result is checkpointed before
continuing. Both winning-side players have20 saved victories; both opponents
have20 saved losses. Stage507 remains refused before win20.

The main's507 claim after restart pays exactly one bound723700, consumed through
the ordinary item handler. EXP advances from5,674,342,680 to21,051,668,520;
the saved daily allowance becomes one use on controlled2041-07-11. Final cold
checks preserve the nested career, all20 results/admitted rosters, allowance,
claim and loaded bag UIDs. Repeat305/507 claims do not change them.

The first claim fixture incorrectly expected the unre?born Archer to receive507
as well. Its claim correctly refused because the installed gate requires first
rebirth/level15, even though its20 victories are valid. The main's preceding
claim/use had committed, so no earning or reward phase was replayed. A read-only
comparison found only the main's new507 receipt, serialized Way progress, EXP
and corresponding allowance changes. The recovery requires both exact SHA256
snapshots, stage1/wins20, the exact main EXP and one507/723700 receipt, the two
rebirth values and zero Archer507 receipts before advancing the probe alone:

- Previous snapshot: `9f639ef6e64f1fa243e105c9763f615947a7e8c970b6e605adb10299caa7e2fd`.
- Reviewed current snapshot: `6b1b0dd8840759a837bb2d221b7c80272620a7509e9dd42cba15ad3303b339a2`.

The recovery and separate final reload pass. All three non-main fighters remain
without507 receipts; no rebirth or eligibility is bypassed. The failed claim log
and comparison snapshots remain in the private candidate directory. Latest
checkpoint: `revora_fresh_team_twenty_probe`, stage2/wins20. Earlier probes are
historical. The Archer can claim later only after earning its rebirth gate.

These scripted matches use controlled dates, proximity and attack/return clocks;
they do not establish natural PvP balance or native-client behavior. Full-field
Team PK/Skill Team PK and live deployment remain pending. No production code or
game build changed; candidate SHA256 remains
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
