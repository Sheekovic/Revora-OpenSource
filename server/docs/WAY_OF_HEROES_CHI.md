# Way of Heroes Chi Study

Implemented and privately validated 2026-10-02; not deployed live.

Installed stage 605 requires total Chi score 500, opens at first rebirth level
101, and awards one bound NormalChiPack 3005126. Ordinary Chi unlock itself
requires level 110. The pack's existing atomic consumer grants 500 Chi points.
The stage previously refused every claim.

The claim now locks and reads the saved `chi.chipowers` blob inside the existing
award transaction. It derives score from the four attribute values per power,
using the same integer percentage calculation and maxima as `CalculatePoints`.
The spendable Chi wallet, runtime `Points` cache and client progress are not
eligibility. Missing/empty powers score zero. Ordered powers 1–4 and exact blob
length are required. Unstudied zero attributes are allowed; invalid type/value,
truncation, trailing data and duplicate/out-of-order powers fail without a prize.
The existing owner lock, inventory/session checks, bound plus-0 output, saved
progress and unique claim receipt remain authoritative.

## Validation

`tools/CheckCandidateChiClaim.cs` adds two composed private process phases:
`career-chi-claim-earn` and `career-chi-claim-reload`. One ordinary non-GM character
receives supplied first-rebirth/level-110 qualification and 2,000 initial CPs.
No Chi powers, donation, prize or claim record is inserted by fixture SQL.

The actual refill request charges the configured full-refill CP price. Actual
unlock and study requests produce Dragon and Phoenix powers. Attribute types
use the ordinary selection; value rolls are controlled to their maxima. One
400-point power cannot claim, including when its cached score is forged to
1,600. Unlocking an unstudied second power still cannot claim. Studying it gives
800 total score and leaves exactly 3,300 spendable Chi after both unlock/study
payments. This demonstrates transitions, not natural roll odds or CP earning.

Eight concurrent claim requests yield one bound pack and one nonlegacy receipt.
Using the pack twice consumes it once and leaves exactly 3,800 Chi. Game and
PostgreSQL stop/restart; a fresh process compares exact scoped profile, items,
Chi state, Way of Heroes progress and claim/use records. Replaying the claim
changes nothing. Decoder checks separately verify 499/500 boundary arithmetic,
empty states, excessive count, truncation, trailing bytes, duplicate power,
invalid attribute type, over-maximum value and nonzero value with absent type.

The isolated committed-source-overlay production build passed with existing
legacy warnings; the composed harness compiled with warnings as errors. Both
process phases passed on the first gameplay run. Private game SHA-256:
`1A636D79499F1F3CA47AF2BE44E568E9A7F9228D7112B2262376AD8BEC7ED5D7`.
Logs: `arena-process-career-chi-claim-{earn,reload}.log`. Both game close paths
completed and PostgreSQL stopped. The previous private executable/manifest were
preserved. Unrelated owner worktree edits remain excluded from the build.

Packets are dispatched in process. Authenticated/native UX, natural starting
qualification/funding, representative odds, interrupted commits and deployment
remain open. The separate retained main-career checkpoint and achievement totals
are unchanged.

## Follow-up earning contracts

The later Chi stages 702/804/905/1003 now award 1/3/5/10 usable bound
SeniorTrainingPills (3003126). The server's durable pending boost, normal
item-use/training handlers, reward counts and cold restart passed private checks.
See [the Senior Training Pill report](WAY_OF_HEROES_SENIOR_PILLS.md), including
the unresolved native client activation gate. This is not another Chi pack.

Follow-up: equipment-composing stages 602/706/802/902/1007 were implemented in
`3563f12e` using documented Revora slot rules and privately validated ordinary
composition, reward use and cold restart. See [the total-bonus milestone
report](WAY_OF_HEROES_TOTAL_BONUS.md). Their native/live gates remain pending.

## Fresh unforced study through all five milestones (2026-10-03)

The ordinary level133/class25/twice-reborn career starts from the exact nobility
checkpoint with6,000 earned spendable Chi and no unlocked powers. Seventeen
owned items move through Desert City warehouse10011 to reserve reward capacity.
All five stage requests605/702/804/905/1003 refuse at zero saved score.

Normal unlock/study/refill packets unlock all four powers and produce1,585 total
score after522 ordinary rolls. Attribute type/value randomness is unmodified;
the fixture keeps attributes whose integer percentage is at least99 using the
normal lock mask. Unlock and locked-study costs are charged, and refills deduct
16,864 previously earned CPs in total. No Chi blob, score, wallet, value, power
or result is supplied. This is one observed run, not a guaranteed cost or odds
estimate. Intermediate scores can decrease when unlocked attributes reroll.

Five bounded study processes preserve every accepted roll/payment checkpoint.
Their endpoints are120/240/360/480/522 rolls and scores328/740/737/1469/1585.
The terminal stage is not marked ready until saved score meets1580. Exact reloads
between batches compare the full nested career and Chi ownership state.

A separate claim process grants605's bound NormalChiPack3005126 and consumes it
once for exactly500 additional wallet points. Higher stages grant1/3/5/10 bound
Senior Training Pills3003126,19 total, with exact saved reward UIDs. The pills
remain owned for later JiangHu progression; this batch does not claim their use.
Reusing the consumed normal pack and repeating all five milestone requests pay
nothing. A final game/database restart preserves score1,585, all19 pills, the
five nonlegacy claim receipts, wallets and all five loaded bag UID sets.

Preparation, five study batches, claim and final reload pass. Latest checkpoint
is `revora_fresh_chi_probe`, stage2; previous snapshots are historical. Redacted
numbered study logs and final phase logs remain in the private candidate folder.
Native Chi display/stat rendering, competitive balance and live deployment stay
pending. No production source or game build changed. Candidate SHA256 remains
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
