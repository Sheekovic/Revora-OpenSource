# Arena cutover checklist

Source inspection: 2026-09-21. This is the current integration checklist;
older dated prerequisite notes remain historical evidence, not a live task list.

- [x] Owned solo/team admission, result retention, completion scheduling and
  shutdown drain exist. Ordinary signup/matchmaking routes select those owners
  when joint preparation is active.
- [x] Catalog loads once before worker creation; preparation rejects workers,
  listeners, loaded Arena caches and live sessions. Cold process preparation and
  an unprepared actual game start/close are tested.
- [x] Five-second Team Arena return is enforced at Export and the scheduler,
  with retained results required before return. See the timing validation in
  [owned scheduling](ARENA_OWNED_SCHEDULING.md#five-second-postmatch-return-2026-09-21).
- [x] Fix the level-70 eligibility boundary: `TeamQualifier.CanFight` now
  rejects only levels below 70, matching the [official guide](https://co.99.com/guide/quests/teamqualifier.shtml).
  Ordinary leader matching, three level-70 participants, excluded level-69
  teammate and persisted result reload pass in the 1,116-check Arena run.
- [x] Synchronize initial level-70 points in both saved Arena rows and caches,
  with profile/skill rollback, preserved Honor/counters, conflict rejection,
  concurrent saves and reader reload. Build and 495 focused Experience checks
  pass ([scope](EXPERIENCE_REWARD_TRANSACTIONS.md#shared-initial-arena-points-2026-09-21)).
- [x] Complete the bounded compiled balance-writer audit and replace the two
  administrator RAM-only writes with audited, guarded transactions. Build and
  1,131 combined Arena checks pass; scope and exclusions are in the
  [writer audit](ARENA_ADMIN_BALANCES.md#bounded-compiled-writer-audit).
- [x] Connect joint preparation in `Server.Run` between catalog loading and
  worker creation, before caches/listeners. Build, two cold helper processes and
  two actual game startup/normal-close cycles pass across database restarts
  ([evidence](ARENA_STARTUP_PREPARATION.md#composed-startup-activation-2026-09-21)).
  This is candidate source activation, not deployment or live-fighter acceptance.
- [x] Actual process refusal-to-shutdown, interruption and restart retain both
  owned Solo/Team results for five supplied non-GM profiles; two recovery launches
  preserve winners, counters, point mirrors and no duplicate credit.
  [Process evidence](ARENA_PROCESS_RECOVERY.md).
- [x] Recover the ninth Solo win across interruption, load its500 Honor/EXP Ball/
  bound rune prize through ordinary delivery, and preserve exact reward UIDs and
  payment time on another restart. The process regression exposed and fixed a
  missing rune in new startup-recovered entitlements.
- [x] Continue the recovered Solo winner through twenty completions, deliver both
  daily prizes and spend 800 of the earned 1,000 Honor through the normal shop
  handler; preserve the purchase and 200 balance across another process restart.
  The process regression also fixes effective/durable VIP mismatch on reentry.
- [x] Continue the recovered Team roster to nine wins and verify equal daily
  Honor/EXP Ball/bound-rune rewards for the winning leader and non-leader,
  with no duplicate credit after another actual game process reload.
- [ ] Authenticated fighter teardown/return, Team twenty-match earning,
  interrupted ninth Team/twentieth Solo results,
  custom prize journeys and native combat remain open; profiles and outcomes
  are controlled inputs.
- [ ] Complete ordinary-player native signup, team acceptance/combat, spectator
  behavior, result UI, return position and reconnect acceptance. Last recorded
  private DX9 preflight had no adapters in the disconnected Windows session;
  actual native acceptance remains unavailable until that infrastructure works.
- [ ] Complete release review, backup/rollback and authorized deployment.

Shared Arena points, daily ranking formula and daily rollover are existing
Revora policies documented separately; the retail guide is not evidence that
all of those policies match retail. Source checkmarks do not certify deployment.

## Process probe findings (2026-09-21)

The attempted fighter recovery probe did **not** reach Arena admission or result
capture. Loading the existing x64 fixture assembly into the x86 game first
failed with `BadImageFormatException`. Compiling a separate x86 fixture assembly
resolved that failure, but its minimal character insert then violated the real
`entities.onlinepoints` NOT NULL constraint. Component-test factories therefore
cannot be reused unchanged against the candidate's full character schema.

This is a probe setup failure, not evidence of failed gameplay recovery. The
process shutdown/restart milestone remains unchecked. A replacement must create
complete ordinary character records before attempting both retained results,
interruption, startup recovery and a second restart without duplicate credit.
Offline transport fixtures still cannot prove authenticated socket teardown or
native gameplay acceptance.

The draft tools are retained only under ignored
`obj/chi-refill-validation/arena-process-draft/`; they are not supported validation
commands. The empty, owned `revora_candidate_arena_process` clone was inspected
and removed. The original candidate configuration matches its manifest hash and
PostgreSQL was stopped. No production data or client files were changed.

The2026-09-27 [replacement process probe](ARENA_PROCESS_RECOVERY.md) resolves the
setup failures described above without relaxing the full schema. Its successful
scope began with retained Solo/Team outcomes and replay-safe saved statistics;
the later extension also verifies the recovered nine-win item/Honor payout.
Native played matches remain outside this probe. The old unsupported draft remains
historical; use the committed Verify-ArenaProcess.ps1 entry point.
