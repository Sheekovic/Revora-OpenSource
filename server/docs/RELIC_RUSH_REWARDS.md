# Relic Rush reward claims

2026-09-21. Source-tested claim change; not deployed or native-client accepted.

Saved first-, second- and third-place entitlements now each pay one random bound
P7 soul alongside their existing configured CP, silver and RVP purse. Existing
pending placements qualify; consumed claims remain closed. Invalid placement
values no longer fall through to the third-place reward.

The canonical ordinary player owns the inventory and variable locks. A full
inventory or purse leaves the prize pending. The saved placement is locked and
compared before its consumption, currency updates and soul insert commit in one
transaction. Affected-row and balance checks reject stale writes. Client state
updates after commit; an exception requires reconnect before further mutation.

Build and 125 combined Local PK/LMS/Relic Rush checks pass, including 16 new
claim assertions: all three placements, ordinary eligibility, full bag, purse
overflow, suppressed entitlement/item writes, concurrent claims, exact item UID
and binding after fresh reload, replay, stale cached placement and invalid rank.
Logs are ignored under `obj/chi-refill-validation/relic-prize-build.log` and
`relic-prize-tests.log`.

Those original claim tests start from saved placement entitlements. The following
batch extends result delivery; neither batch certifies native gameplay or live
deployment.

## Durable ranking and queued delivery

`Create` initializes `revora_relic_placements`. At finish, the positive-scoring
top three are frozen and saved in one transaction before notifications and reset.
Ties use ascending character UID, making retry order deterministic. The cycle,
placement, character and score identify the saved result. A repeated cycle must
match its saved roster; it cannot replace recipients or reset imported prizes.
Failed writes retain the frozen result for the next tick and stop further scoring.

Saving a result no longer requires a connected player or overwrites their legacy
claim flag. At the prize NPC, an existing placement is claimed first. Otherwise,
the oldest queued result and variable-vault placement transfer atomically under
locks. Failure rolls back both sides and fences the player for reload. The normal
claim transaction then pays its purse and soul. More wins remain queued and old
completed results are never imported twice.

Build and 134 combined Local PK/LMS/Relic Rush checks pass. New result fixtures
exercise the actual finish route with controlled scores, an offline scorer,
zero-score exclusion, deterministic ties, rollback after the second placement
insert is suppressed, frozen retry, replayed saved cycle, multiple wins, import
rollback, returning-character reload and legacy-plus-queued claims. Logs:
`obj/chi-refill-validation/relic-result-build.log` and `relic-result-tests.log`.

The admission/return work below supersedes that earlier prerequisite gap.
Remaining: real-process restart acceptance; native scoring, reward display and
reconnect; deployment.
An interruption before the ranking transaction commits still loses that active
round's in-memory score evidence. The tests reload characters and reinitialize the
store in-process; they do not certify a server-process restart or a full native
scoring journey. Relic Rush restoration remains incomplete.

## Reserved entry and interrupted-round returns

`RelicAdmissions` records the complete ordinary-player roster and original PK
modes before any arena transfer. Admission acquires actor locks without waiting
under the event lock. A failed reservation transfers nobody; a failed transfer
cancels the round and returns all reserved participants without generating ranked
prizes. Outstanding returns prevent admission into another round.

Death, departure, disconnect and round completion schedule owned returns. The
return path restores the saved PK mode and persists the town destination before
acknowledging completion. Failed acknowledgement leaves a retry record. Startup
marks interrupted reservations ended; the connected login path safely returns
their owners. Committed rankings and queued prizes remain intact, but unfinished
rounds are cancelled rather than resumed. No winner is inferred from map position.

Build, 144 focused Local PK/LMS/Relic Rush checks and the 216-check existing
gameplay suite pass. New fixtures verify actual registration/start, reservation
failure, saved original modes, interrupted-round recovery with fresh character
reload, acknowledgement failure/retry, elimination, ranked finish and unavailable
arena cancellation. Existing hold scoring and final-minute behavior still pass.
Logs: `obj/chi-refill-validation/relic-admission-build.log`,
`relic-admission-tests.log` and `relic-scoring-regression.log`.

Recovery is exercised in-process with fresh character loads. Full server-process
restart, native transport/map/PK-mode acceptance and deployment remain unverified.

## Composed startup reservation recovery

The existing private startup verifier now accepts `-CheckRelicRecovery`. It starts
the actual candidate game, inserts two synthetic interrupted reservations while
the game is stopped, then checks recovery in another game process and again after
a PostgreSQL restart. The reservations must remain ended but not returned, and no
ranked prize may appear. Only the fixed probe rows are removed after success.

`tools/Verify-MoonBoxDatabase.ps1 -CheckRelicRecovery` passed: three actual game
starts and normal `@close` exits, two database lifecycle cycles, loopback listeners,
and Arena preparation before its caches. The new Relic tables initialize under
the constrained runtime database identity. Game SHA-256:
`B27950E7C4BA29B5C6D001082656AC50EF47CFCF75EC4C68AE71642874FD465B`.
Source baseline `344ea0e`; the private manifest records the owner's pre-existing
`System/IniFile.cs` modification, so this is not a clean-source release artifact.
The previous executable/manifest are preserved. Log:
`obj/chi-refill-validation/relic-process-recovery.log`.

This verifies composed startup and synthetic reservation persistence only. It
does not exercise authenticated players, actual admission before interruption,
login return, or prize claiming across processes. Those complete process/native
acceptance gates remain open. The private game and database are stopped; no live
server, installed client or production schema was changed.

## Earned rankings and exact process recovery (2026-10-01)

`career-relic-earn`, `career-relic-claim` and `career-relic-reload` pass on
private full-server SHA256
F020C8A2B264B6D420B62A5DFA5421387E11B8D5F6AC44BB8305FEEC1608049B.
Four ordinary non-GM characters continue from the exact Wave Defense checkpoint.
Installed NPC registration rejects duplicate enrollment; public Tick admits all
four into the composed server's arena. Proximity, calendar and pulse deadlines
are controlled. Scores are earned through relic pickup and production hold
pulses, not assigned to the score dictionary: six ordinary points, three ordinary
points and ten points from five final-minute double pulses. Public departure
callbacks drop the first two holders' relic and preserve their scores.
The fourth participant never scores.

At the deadline, the actual finish path saves ranks 1/2/3 with scores 10/6/3,
returns all four participants and acknowledges their admissions. The game exits
abruptly before claims. After PostgreSQL/game restart, exact inventories, purses,
placements and admission rows match. Login recovery and ordinary NPC claims pay
each ranked player's configured placement purse plus one bound P7 soul. The
zero-score player receives nothing. Repeat claims change no purse or item.
A second full restart preserves exact item rows/UIDs, balances and imported
placements and again refuses duplicate payment. Earlier Wave rewards remain.
`revora_relic_process_probe` stores the final fingerprint.

This closes earned ranked-prize process persistence for this controlled route.
It does not prove a natural eight-minute contest, native movement/PK presentation,
transport disconnect, actual PvP death, a mid-round crash, lost COMMIT reply or
host power loss. The departure callbacks are invoked directly with canonical
players still available for return; no authenticated socket is exercised.
No production code or live deployment changed. The separate retained achievement
career is unchanged.

The later [admitted-round interruption batch](EVENT_INTERRUPTION_RECOVERY.md)
passes actual NPC entry, abrupt exit before progress, town/combat-mode recovery,
no unearned prize and exact prior rewards across two cold restarts. It supersedes
the missing early-round process boundary only; later-wave/native gates remain.

## Fresh PvP relic drops and cold placements (2026-10-03)

`CheckCandidateFreshRelic.cs` continues the fresh five-character career after
Wave Defense, entering four ordinary players through NPC900000005. Duplicate
registration leaves exactly four participants. The fifth character stays outside.
No currency, profile, scores, damage amounts, deaths or placements are seeded.

The Archer holds for six points, then the Trojan for three. Three calculated
Warrior melee attacks across the two fights eliminate those holders and cause
production death/return handling to drop the relic. Normal town revival handles
any returned dead player. The Warrior then holds during the final-minute double
score window for ten points. Finalization returns all four participants and
persists the expected10/6/3 placements; the zero-score Monk has no placement.

Separate claim and final reload phases pass, including exact new item UIDs,
permanent bound P7 grade, saved currency, consumed placements and repeated-claim
refusal. Rewards per placement:

| Place | CPs | RVPs | Silver | Items |
| --- | ---: | ---: | ---: | --- |
| 1, Warrior | 10,000 | 30 | 1,000,000 | One bound P7 |
| 2, Archer | 5,000 | 15 | 400,000 | One bound P7 |
| 3, Trojan | 2,500 | 8 | 150,000 | One bound P7 |
| No score, Monk | 0 | 0 | 0 | None |

All three phases pass against unchanged private candidate SHA-256
`D48A6C00F6BF84889581ADFEABEB924C4C7448C5DC3C6D17F8674129C43665B6`.
Logs: `arena-process-career-fresh-relic-{earn,claim,reload}.log` under
`C:\Revora\DisposablePostgres\moon-box-candidate`.
The snapshot includes the prior career and this roster's admissions/placements;
loaded prize ownership is independently checked after restart.

This extends the earlier callback-only fixture with calculated PvP deaths.
It still controls calendar, relic approach, pulse deadlines, attack cooldowns
and town revival timing. It does not claim a natural eight-minute competitive
contest, native movement/rendering, transport disconnect or mid-round resumption.
No production code or live service changes in this batch.
