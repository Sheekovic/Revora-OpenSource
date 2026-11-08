# Way of Heroes Nobility claims

Implemented and privately validated 2026-10-02. Not deployed live.

The installed stage catalog specifies the following one-time claims, all opened
at second rebirth, level 121. They previously refused every request.

| Stage | Current donation | Bound prize | Chi on item use |
| --- | ---: | --- | ---: |
| 806 | 30,000,000 | 3005127 | 1,000 |
| 907 | 100,000,000 | 3005128 | 2,000 |
| 1005 | 500,000,000 | 3005129 | 3,000 |

The existing atomic claim service now reads the owner's saved `nobility` row.
Rank, runtime donation mirrors, client progress and lifetime payment receipts
cannot establish eligibility. Missing ownership is zero; duplicate or negative
saved ownership is an error. The existing unique owner/stage receipt prevents
another award even after weekly donation reset and subsequent donations.

These claims acquire the same PostgreSQL advisory transaction lock as donation
payments and weekly resets, before locking character and donation rows. This
orders the eligibility decision and award against a reset, without reversing
the payment lock order. The claim retains its saved opening gate, inventory
capacity/session checks, bound plus-0 prize and atomic progress/receipt writes.
Pack use remains owned by `ChiPointItems`, with durable wallet compare-and-swap,
consume and receipt. Other unfinished Way of Heroes stages remain refused.

## Composed process evidence

`tools/CheckCandidateNobilityClaims.cs` adds `career-nobility-claims-earn` and
`career-nobility-claims-reload` to the private process harness. Three new ordinary
non-GM characters use the existing donation request handler. Starting silver,
second rebirth and level 121 are supplied fixture qualifications; donation rows,
claim prizes and Chi rewards are earned through the services.

For each threshold the fixture verifies:

- A forged runtime donation with no saved donation cannot claim.
- Paying one silver below the threshold saves the payment but cannot claim.
- A further one-silver payment meets the threshold with exactly 100 silver left.
- Eight concurrent claim packets produce one nonlegacy claim receipt and one
  bound plus-0 pack; repeated item use awards its exact Chi amount only once.
- A fresh game process after PostgreSQL stop/start restores donation, wallet,
  items, progress, payments and claim/use records to the exact scoped snapshot.
  Claim replay leaves that snapshot unchanged.

The isolated committed-source-overlay production build passed with existing
legacy warnings. The composed harness compiled with warnings as errors; both
phases passed. Game SHA-256:
`07D18D97AE40D6256A5F0A1B83A46C591C7D3CF1AA34AB88485FC623E9C8057C`.
Logs are `arena-process-career-nobility-claims-{earn,reload}.log` in the private
candidate root. The initial fixture phase allowlist was corrected; its rejected
attempts stopped before game setup. The successful earn was preserved and the
reload was rerun independently. Both normal game closes passed and PostgreSQL
was stopped afterward. Prior private executable/manifest backups were retained.

This tests in-process request dispatch, not authenticated TCP or native UI.
It does not prove naturally earned starting silver/rebirth, an actual weekly
reset race, interrupted commit recovery, or broader career completion. The reset
ordering is code-reviewed, not a claimed reset stress result. The retained main
career checkpoint remains `revora_retained_talisman_probe`, step 4; these separate
fixture characters do not change its achievement total. Native acceptance and
live release remain pending. Unrelated owner worktree edits remain excluded.

## Fresh earned CP donation and all three Chi rewards (2026-10-03)

The existing ordinary level133/class25/twice-reborn career1096322 starts from the
exact fresh twenty-win Team Arena checkpoint, with zero saved donation and zero
Chi points. Two owned items move through Desert City warehouse10011 to make room
for all three prizes. Claims806/907/1005 refuse before payment.

The standard donation request converts100,000 previously earned CPs into
5,000,000,000 donation. The installed board places the character as King, and
10318/11120 evidence is earned. The fixture calculates the necessary King
threshold from the actual private board and verifies funds before paying; it
never supplies donation rows, currency, rank or achievement receipts. Two
immediate repeated donation requests at the same controlled payment clock are
refused without a second debit. This is the existing one-second duplicate window,
not a claim that future intentional donations of the same amount are forbidden.

`career-fresh-nobility-pay`, `claim`, `use` and `reload` all pass on their first
executions, each in a separate private game/database process. After the unpaid
threshold restart, ordinary stage packets grant one bound plus0 item each:
3005127,3005128 and3005129. Another restart preserves the unconsumed items; their
normal item packets grant exactly1,000/2,000/3,000 Chi, totaling6,000. Reusing each
consumed UID and replaying the stage claims changes nothing.

Final cold validation preserves the entire nested career/roster, CP debit,
saved donation and payment receipt, King rank, claims, three item-use receipts,
6,000 loaded Chi points and all five loaded bag UID sets. Latest authoritative
checkpoint is `revora_fresh_nobility_probe`, stage3. The prior post-tournament
107-identity audit remains a dated inventory; these new receipts are outside it.

This private board result does not guarantee King against a different live
leaderboard. NPC/warehouse proximity and the duplicate-payment clock are
controlled; native UI/ranking rendering and live deployment remain pending.
No production code/build changed. Candidate SHA256 remains
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
