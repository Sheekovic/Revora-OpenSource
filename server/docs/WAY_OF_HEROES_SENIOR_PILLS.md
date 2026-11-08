# Higher Chi milestones and Senior Training Pills

Implemented and validated in the private candidate on 2026-10-02. Not deployed.

Stages 702/804/905/1003 use saved Chi scores of 1300/1400/1450/1580 and award
1/3/5/10 bound Senior Training Pills (3003126). Cached scores and spendable Chi
are not eligibility. Existing transactional claims, inventory capacity checks,
unique receipts and supplied catalog/open-level requirements remain in force.

## Documented Revora rule

Under the owner's instruction to use recommendations, a pill arms one saved
boost for the next successful free or paid Jiang Hu training. It does not expire
on logout. Another pill is refused while one is pending. Restoration does not
consume it; insufficient funds, invalid stage/action and rolled-back training
also preserve it. Both bound and unbound permanent inventory pills are accepted;
locked, suspicious, expired/temporary, warehouse and wardrobe items are refused.
An ordinary character must already have Jiang Hu. There is no GM/name allowlist.

The ordinary quality roll is unchanged. If its result is below level 6, a
separate 10% roll promotes it to level 6. Existing level-6 results are preserved
without another draw. The measured normal level-6 probability is 25.114270650%;
the strengthened probability is 32.602843585%. These are Revora probabilities,
not a claim about original Conquer odds. The prior research is in
[the Jiang report](WAY_OF_HEROES_JIANG.md).

`revora_jiang_pill_uses` records item ownership, source stack, bound state,
arming time, consumption time and resulting quality. A partial unique index
allows one pending boost per owner. Owner/session locks serialize item use and
training. Arming consumes one item/stack unit and inserts its pending record in
one transaction. Successful training updates the pending record, Jiang state,
CPs and Study Points in one transaction. Database errors or interrupted runtime
installation fence the character for reload; committed state is not retried as
another payment. No pending flag depends on process memory.

## Private validation

`tools/CheckCandidatePill.cs` is integrated into the composed process harness.
Five phases passed on private executable SHA-256
`75326353BAC684257EF30AC32B0EE21F4E77E61B790233630F9B4084F9FE3434`:

- [x] `career-pill-earn`: ordinary level-130, twice-reborn character with supplied
  50,000 CPs earns four 400-point powers through actual unlock/study requests.
  Value rolls are controlled maxima; this does not measure natural Chi odds.
  Claims fail below threshold, with an unlocked unstudied fourth power and with
  a forged runtime score. Eight concurrent requests per stage award exactly
  19 bound pills across four nonlegacy claims.
- [x] No-Jiang and locked-pill refusals; ordinary Jiang creation and paid training;
  concurrent/replayed pill use consumes once. Another pill while pending and
  invalid training requests leave saved state unchanged. Save with a pending boost.
- [x] `career-pill-resume`: after game and PostgreSQL restart, compare the exact
  scoped snapshot. Force a failure before training commit; reload verifies that
  the pending boost, currency, items and Jiang state did not change. Exercise
  rescue draws 9 and 10, paid/free training, restoration while pending and natural
  level-6 preservation. Consume all 19 earned pills through ordinary requests.
- [x] `career-pill-reload`: another cold restart compares exact scoped state and
  confirms claim replays grant nothing.
- [x] `career-pill-edges`: a separate ordinary character receives a supplied
  two-pill stack and zero CPs. Arming rollback preserves both units. A forced
  interruption after arming commit recovers one pending boost and one remaining
  unit on reload. Insufficient CPs and duplicate arming preserve state. Successful
  free training consumes the first boost; the last stack unit arms another.
  Exhausted free-training resources and zero CPs preserve that pending boost.
- [x] `career-pill-edges-reload`: cold restart preserves the second character's
  exact state, with one consumed boost and one pending. Decoder checks cover
  each higher Chi threshold at minus one, exact and plus one.

The committed-source-overlay build passed with existing legacy warnings; the
composed harness compiled with warnings as errors. The first invocation exposed
missing harness phase/UID wiring, fixed before gameplay. The edge fixture first
assumed talent stayed zero after free training; existing `SendStatusMode` clamps
runtime talent to at least one. Corrected the assertion to exhausted FreeCourse
and the saved consumption receipt, then resumed the retained successful operation
without resetting its database. The subsequent two edge phases passed. One
local variable shadowing compile error was corrected before that rerun.

## Remaining gates

- [ ] Native 6711 pill activation and UI acceptance. The server uses existing
  validated 1009 item-use action 4; the harness dispatches those frames in process.
  This proves the ordinary server route, not that the installed pill's client UI
  emits this action. If it only opens another dialog, add a verified ordinary
  service route before publication. No new guessed Jiang action is accepted.
- [ ] Live deployment and authenticated/native acceptance. No live process was
  restarted and no client patch was published.
- [ ] Natural acquisition/funding progression, long-duration training timing,
  interrupted training post-commit installation, and cross-session delayed
  network replay beyond the current single-pending/session ownership checks.

These four stages count as server implementations with private evidence;
native/public availability is not claimed. Main-stage aggregate prizes remain
separate. All fixture rows and existing private backups are retained.
