# Quiz Show: Revora NPC edition

Implemented 2026-09-15 UTC. Code and isolated validation complete; not deployed.
This is an explicitly documented Revora edition using ordinary NPC dialogue.
The native quiz panel and its unverified answer protocol remain disabled.

## Evidence and edition choices

The [official Quiz Show guide](https://co.99.com/guide/quests/quizshow.shtml), read
2026-09-15 UTC, describes 20 questions, four answers and 30 seconds per question,
with weekend sessions at 04:00, 14:00 and 21:00. It describes experience rewards
and additional rewards for the top three. Its fractional EXP Ball rewards and
cumulative quiz titles are not certified by this implementation.

The installed 6711 achievement predicates identify 10120 as five correct quiz
answers and 11128 as winning the Quiz Show. Client `Achievement.dat` SHA-256:
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.
The old 2068 handler has no single-answer guard or confirmed sequence validation.
Its byte offsets are source evidence, not a captured genuine-client layout.
See [the transport decision](PROTOCOL_RESEARCH.md).

Revora chooses Pacific time, a five-minute enrollment window, a 256-character
cap, an original question bank, timed scoring and durable item prizes. These
choices must not be presented as a reproduction of every official quiz edition.

## Ordinary-player route

Master Wen stands in Twin City near **430,278**, runtime NPC **900000062**. The
teacher uses the installed Cloud Saint appearance; no client asset is added.
Every eligible authenticated ordinary player can enroll. No GM, owner name,
purchase, guild or character allowlist is required.

- Saturdays and Sundays: first questions at **04:00, 14:00 and 21:00 Pacific**.
- Enrollment opens five minutes early and closes exactly at the first question.
- Remain within six tiles of Wen to receive and answer the questions. Each has
  four dialogue choices and a 30-second deadline shared by all entrants.
- Selecting a choice seals it. Reopening the dialogue cannot change that answer.
  Missed questions stay unanswered. Leaving the quiz table pauses the automatic
  dialogue; speak with Wen again to resume the current question.
- Five distinct correct answers in one show earn **10120** immediately and a
  saved prize entitlement when the show ends.
- Return to Wen's claim option after settlement. A full inventory preserves the
  reward. Reconnects and restarts do not erase results or unclaimed prizes.

Each session saves 20 distinct questions randomly selected from the 24-question
Revora-owned version-1 bank. Reconnect and restart preserve that exact order.
Questions teach restored quest routes and include simple arithmetic and direction
questions. Original wording is checked into Git; no proprietary question bank is
copied or distributed.

## Scoring and rewards

A correct answer earns `40 - floor(elapsedMilliseconds / 1000)` points; an
incorrect answer earns zero. Only a first answer received before the deadline
counts. Eligible standings require at least five correct answers. Sort by score,
then number correct, then total elapsed answer time. Exact ties share a rank;
character ID is never a tiebreaker. Competition ranking skips the places occupied
by a tie (two tied first places are followed by third place).

At least two qualifying entrants are required for competitive winner evidence.
Every tied first-place finisher then receives **11128**. A lone qualifying player
receives the five-answer achievement and participation prize, but no winner
achievement. Server-owned answers determine the result even if an entrant is
offline when the session closes.

| Result | Bound EXP Balls, item 722136 |
| --- | --- |
| Competitive first place, including exact ties | 3 |
| Competitive second place | 2 |
| Other entrant with at least five correct answers, including solo practice | 1 |
| Fewer than five correct answers | 0 |

New shows also award **one bound P7 soul** to every entrant with at least five
correct answers, including solo practice and tied results. Ranking changes only
the EXP Ball count. Older shows keep their original EXP-only payouts.

These are total item prizes for one show, not per-answer rewards. The oldest
unclaimed prize is delivered first. No CP, RVP or gold purse changes. This version
does not increase the old `QuizPoints` counter or claim the native cumulative
Graduate/Master/Scholar/Professor presentation is restored.

## Valuable reward validation (2026-09-21)

`revora_quiz_runs.valuable_reward` defaults to false for existing shows; new
`Ensure` inserts set true, and conflicts cannot replace a saved show contract.
The soul comes from the loaded P7 catalog through `QuestSoulReward.Create` and
is always bound. Its UID is saved with the EXP Balls in the existing result
receipt. Item creation and the checked single-row claim transition share one
transaction. Capacity includes every EXP Ball plus the soul.

The isolated build and 36 Quiz checks pass. Ordinary NPC answers earn winner,
runner-up, tied and solo rewards; unsuccessful entrants earn none. Tests cover
exact capacity, failed soul insertion after EXP Ball inserts, failed receipt,
concurrent claims, exact item UIDs after production item reload, and an older
show's restart/earned claim without retroactive P7. The clock and known answer
bank are test inputs; no result or correct-answer rows are fabricated for these
reward paths. This is item reload evidence, not a full live reconnect test.
Log: `obj/chi-refill-validation/quiz-p7.log` (ignored).

NPC wording announces the new reward. Genuine-client display and live deployment
remain pending; native quiz transport remains disabled.

## Persistence and failure behavior

`revora_quiz_runs` owns session identity, UTC start, question order, rules and
terminal state. `revora_quiz_entries` owns enrollment. `revora_quiz_answers` has a
unique `(run,uid,question)` key with the sealed choice, correctness, score and
elapsed time. A question prompt also binds the live player object, session and
question; answer option IDs differ between the 20 questions.

Enrollment serializes against the session row to enforce the cap. Answers take
a shared session lock plus their entry lock; final settlement takes the exclusive
session lock. Thus an answer and settlement cannot pass each other. The time
check runs after the locks are obtained. The fifth-answer write and 10120 commit
together. All results, winner evidence and the terminal marker also commit
together, then online achievement/wardrobe projections refresh.

The tick recovers up to eight overdue unfinished sessions per pass, including
after restart. A failure leaves the session recoverable. Answer/prize failures
fence that player's actions with a reconnect message. Claims lock the saved
result, create bound items and save exact item UIDs atomically before installing
the inventory update. Retries cannot charge or award twice. The normal item and
achievement loaders restore the committed state.

## Validation and remaining acceptance

The isolated build and `Verify-PotencyHunt.ps1 -QuizOnly` pass **33 checks**.
They exercise the actual ordinary NPC handler, timer, persisted questions,
twenty-question competition and claim path against a disposable PostgreSQL schema.
Coverage includes Pacific boundaries/DST, enrollment/replay/cap, fake and remote
requests, late or unprompted answers, disabled malformed 2068 input, wrong answers,
fifth-answer rollback, restart/deck continuity, end boundary, failed and repeated
settlement, speed ranking, ties, solo eligibility, leaving the table, full bags,
prize rollback, duplicate claims and ordinary item/achievement reload.

Ten existing quest/event/progression/wardrobe modes provide **517 regression
checks**. Live-client NPC appearance, answer-option presentation, automatic
question changes, notification/reconnect visuals and item-use acceptance remain
pending. The native quiz panel is a separate unimplemented compatibility task.

## Fresh earned solo and contested shows (2026-10-03)

The fresh main UID1096322 and existing ordinary trainee UID1096323 continue from
Purple Banshee with their earned profiles, inventory and currency. No player,
reward, score or result is inserted by the fixture. The final contested show
`2041042014` uses the public Master Wen NPC: registration, current-question
requests and20 answer submissions for each character. The first answers20/20,
the second10/20. An unprompted first answer and repeat changes are refused.
The stored random deck is read to choose test answers; this verifies scoring and
request handling, not independent player knowledge or native presentation.

Scheduled settlement records ranks1/2 and prizes3/2. Both remain unclaimed over
a game/database restart. Ordinary claims then grant the winner three bound EXP
Balls722136 and one bound P7; the runner-up receives two bound EXP Balls and one
bound P7. Exact prior item ownership, CPs, RVPs and silver are preserved. After
another restart, all seven reward UIDs remain in their respective loaded bags;
repeat claims and settlement polling change nothing. Main10120 and11128 have
`quiz-local-v1` evidence. Two existing bag items were deposited through ordinary
Ape City warehouse requests before the contested show to reserve prize space.

Retained failures and recovery are part of the evidence:

- The original show `2041040614` became a valid solo show: the trainee was still
  dead from its earlier Arena loss, so ordinary NPC eligibility refused its
  registration. The fixture incorrectly expected two standings and failed.
  Main20/20, rank1 and the solo one-ball prize were preserved, not replayed.
- Full prior/current JSON comparison found only controlled location changes and
  main10120. Recovery verifies exact snapshot hashes
  `ca45f7610a2fb43a89eb05bffc3ee201a2c06adb0ccc345be28b4e9b2c8d50dc` and
  `a12353ec313189b3c274fa5a9f69b5b68d305c1e638df4c3e32df751f066ecf2`, then claims
  the solo bound EXP Ball/P7 once. Ordinary revival restores the trainee;
  its revival eligibility timestamp is controlled. No HP is assigned by fixture.
- An attempted next-show baseline advanced the clock too early. The scheduler
  created empty show `2041041314`, which later closed without entries/results.
  Its only snapshot difference was that empty run; both characters were alive.
  The fixture now verifies its baseline before entering registration time and
  asserts both entries before answering. The distinct April20 show passed.
- One recovery compile attempt required qualifying `System.BitConverter`; it
  never executed. Historical redacted failure logs remain in the private tree.

`career-fresh-quiz-recover`, the corrected `career-fresh-quiz-earn`,
`career-fresh-quiz-claim` and `career-fresh-quiz-reload` all pass. The authoritative
checkpoint is now `revora_fresh_quiz_probe`, stage2, with `FreshQuizSnapshot`.
It includes both earned shows, the full nested prior career and both players'
possessions. Do not replay earning against historical Purple/solo snapshots.
The deliberately empty April13 session is not counted as an earned show.

Source is `tools/CheckCandidateFreshQuiz.cs`. Logs are
`C:\Revora\DisposablePostgres\moon-box-candidate\arena-process-career-fresh-quiz-*.log`.
The unchanged private game SHA-256 is
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
The fixture controls calendar, response timing and proximity. Native quiz panel,
ordinary dialogue presentation, real-time participation and release gates remain
open. No live deployment or process restart was performed.
