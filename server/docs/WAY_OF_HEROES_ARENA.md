# Way of Heroes: Arena victories

Implemented and privately validated 2026-10-02; not deployed live.

| Stage | Requirement | Opening gate | Bound reward |
| --- | --- | --- | --- |
| 204 | One saved Arena victory | Level31 | Small Praying Stone1200000 |
| 506 | Twenty saved Arena victories | First rebirth, level15 | EXP Ball723700 |

These are the installed StageGoal definitions. Higher rebirth passes the existing
opening-gate rule. Wins are cumulative committed local Qualifier results, not a
daily counter. Each match must be finished, name the claimant as winner, carry
entry evidence version1 and its result-commit receipt, and have consistent winner
and loser statistic records. Queueing, entering, losing, cached counts and old
unverified results cannot substitute for this evidence. A recorded surrender win
uses the same committed result contract; no new damage requirement is invented.

The ordinary claim transaction holds the character row, tests eligibility, inserts
the bound item and records the unique stage claim atomically. No GM or account-name
restriction is introduced. Existing Arena rewards remain independent of these
once-per-character milestone rewards.

## Private process evidence

CheckCandidateArenaClaims.cs provides career-arena-claims-earn/reload. Two ordinary
non-GM characters receive supplied level130/first-rebirth prerequisites.

- [x] Before any match and after first admission, stage204 refuses.
- [x] Stage506 refuses before each of the first twenty wins, including at nineteen.
- [x] Twenty sealed signup/acceptance sequences use normal match admission,
  Attack.HandleAttack melee, terminal result coordination and map return.
- [x] Eight concurrent claims after the first win award one bound Praying Stone.
  Normal use grants259200 seconds (three days) of blessing; replay cannot consume
  the item again. Exactly one matching stone-use receipt remains.
- [x] Eight concurrent claims after twenty wins award one bound EXP Ball. Normal
  consumption increases experience, records allowance use and rejects item replay.
- [x] The losing character cannot claim either milestone.
- [x] Full private game/database restart preserves exact scoped item/claim,
  blessing/experience, result, commit and match snapshots for both characters.
  Repeated claims after reload leave both snapshots unchanged.

The probe's initial compilation found an ambiguous Console name; qualification
fixed the fixture before gameplay execution. Both earning and restart then passed.

Game SHA256:
8C768AA14485D4AD542D7206B51F0CA4B263603581BD3F3EA3B83D4B4D1DC4AD.
The private build uses the committed-source overlay, excluding unrelated owner WIP.
The test advances the Arena ledger clock, admission/attack cooldowns and return
waits; supplies attack/defense/health values; and repeatedly fights the same peer.
It proves ordinary server-handler composition, not natural elapsed-time matchmaking,
combat balance, native packet acceptance or production deployment. The private log
also contains an unrelated vote-pulse PostgresException; no globally clean-runtime
claim is made. Live deployment and native-client acceptance remain pending.
