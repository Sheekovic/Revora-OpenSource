# Way of Heroes: Capture the Flag entry

Implemented and privately validated 2026-10-02; not deployed live.

Installed stage609 (task33, first rebirth level101 opening) asks the character to
join Capture the Flag once and awards one bound EXP Ball723700. The existing
CTF guild membership, NPC proximity and Saturday21:00-21:30 Pacific rules remain.
The milestone does not require winning or delivering a flag.

## Arrival evidence

CTF registration and return recovery are saved before teleport, so registration
alone cannot prove participation. Successful SignUp now confirms current canonical
map ownership and saved entity map2057, then records entered_at on the matching
run/member/guild row in an active-run transaction. Re-entry preserves the first
timestamp. Arrival persistence errors fence the session for recovery.

Qualification requires that saved arrival, or an existing matching owned flag
delivery for the same run/member/guild. The latter preserves previously proven
participation without inventing historical arrival times. An unfinished old
registration qualifies only on a subsequent successful entry. Leaving or a later
server interruption does not revoke genuine historical entry. Claim, bound reward
and unique receipt retain the existing transactional protections.

## Private process checks

CheckCandidateCtfClaim.cs supplies one ordinary non-GM character with level130,
second rebirth and2,000,000 CPs. The existing Guild Director creates its guild
through the normal paid dialog. Event time is controlled; calendar waiting is
not claimed. A direct Register call is a deliberately incomplete negative fixture,
not the positive earning path.

- [x] No participation, closed-window entry and registration without arrival all
  refuse609 without changing owned milestone state.
- [x] The ordinary Guild Controller joins the character to map2057 and saves its
  first arrival. Flag Marshal exit, normal event cleanup, re-entry and another
  exit preserve that first timestamp.
- [x] Eight concurrent claims after leaving produce exactly one bound EXP Ball.
  Ordinary item use consumes it once; replay cannot add another use.
- [x] Event closure and a full private game/database restart preserve exact scoped
  member/arrival, return, delivery, item/EXP/allowance and milestone snapshots.
  Repeated claims change nothing.

Game SHA256:
0E55C56AAA4FA027A367209F03ED5987BC2932D4E7A8B0A5A1663186857933BA.
Build completed with existing warnings using the committed-source overlay,
excluding unrelated owner WIP. The initial fixture tried re-entry before the
normal event tick acknowledged the previous return. The fixture now advances
its controlled clock and ticks before re-entry. Restart recovery retained the
first run's genuine arrival and cancelled that interrupted event; validation
continued in the following controlled weekly window without resetting the guild,
character or earning history. Final earning and cold reload passed.

Historical delivery fallback is implemented but not independently awarded in
this fixture. This is entry/reward validation, not a new complete CTF victory,
native client acceptance or live deployment.
