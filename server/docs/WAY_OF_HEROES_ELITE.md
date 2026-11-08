# Way of Heroes Elite PK placement claims

Implemented and privately validated 2026-10-02; not deployed live.

| Stage | Earned placement | Bound pack | Chi on use |
| --- | --- | --- | ---: |
| 801 | Top 8 | 3005127 | 1,000 |
| 901 | Top 3 | 3005128 | 2,000 |
| 1002 | Champion | 3005129 | 3,000 |

These are the installed stage definitions; all open at second rebirth level 121.
The claim now joins saved `revora_epk_placements`, completed runs and registered
entries. Saved bracket and signup level must agree. Champion placement also
requires the run's champion outcome and matching winner. Runtime ranks, title
ownership, prize-pack possession and client progress do not qualify.

The [existing placement writer](ELITE_PLACEMENT_ENTITLEMENTS.md) verifies the
completed bracket and contested participation before issuing these rows. Its
place 5 means the tied fifth-through-eighth tier. A complete/uncontested run can
retain legitimate non-champion placement rows; the milestone preserves those
without awarding a champion claim. Cancelled/running runs do not qualify.
The event prize's collection deadline does not erase the historical one-time
placement milestone. This does not extend or change the separate event payout.

The existing owner lock, saved stage-opening check, capacity/session guards,
bound plus-0 output and atomic prize/progress/unique-receipt transaction remain.
Packs use their existing atomic Chi wallet/consume service.

## Eight-player composed tournament

`tools/CheckCandidateEliteClaims.cs` adds `career-elite-claims-earn` and
`career-elite-claims-reload`. Eight distinct ordinary non-GM characters start with
supplied level 140 and second rebirth. Before the tournament all three claims
refuse and no prize appears. No placement, result or claim receipt is inserted
by fixture SQL.

Ordinary Envoy signup enters all eight into one highest-level bracket. The actual
event controller schedules quarterfinals, semifinals, final and bronze. Clock
advances are controlled. Each match records damage through ReceiveAttack, followed
by a controlled death through Entity.Die; this is not a full natural combat or
balance test. The production result transaction saves all eight earned placements.

Each entrant sends eight concurrent requests per milestone. Exactly eight receive
Top 8, three receive Top 3, and one receives Champion: twelve claim receipts.
Each resulting pack has the installed identity, binding, plus zero and single
quantity. Normal item use/replay produces 6,000 Chi for the champion, 3,000 each
for runner-up and bronze, and 1,000 each for the remaining five: 17,000 total.
Lower placements cannot claim higher tiers.

After game and PostgreSQL stop/start, a fresh process compares the exact scoped
player profile/purses, inventory, Chi, Way of Heroes and claim/consumer rows.
The entire selected run, registered entries, bouts and placements also match
their stored snapshot. Replaying all claims changes nothing.

The isolated committed-source-overlay build passed with existing legacy warnings;
the harness compiled with warnings as errors. An omitted fixture namespace import
was corrected before gameplay. Both process phases then passed. Private game
SHA-256: `8BF48E9CF22242B0AF926A0FC6091E747B1838D99F1A839FEF8B088F05858A52`.
Logs: `arena-process-career-elite-claims-{earn,reload}.log`. Both normal game closes
completed and PostgreSQL stopped. Previous private executable/manifest retained;
unrelated owner worktree edits remain excluded.

This does not validate authenticated/native bracket UI, natural calendar/combat,
other level brackets, the complete/uncontested non-champion variant, interrupted
claim commits, or live deployment. Supplied rebirth/level and controlled damage,
death and clock are explicit limits. Main retained-career counts are unchanged.
