# Ancestor Worship transactions

Owner clarification (2026-09-21): **remove direct daily quest P7 rewards only**.
One-time quests, event rewards and both Magnolia custom packs keep their bound
P7 souls. Daily Hunt, Rare Materials, Soul Ball, Release Souls, Magnolia's base
harvest, Exorcism and Worship retain their other completion rewards. Custom
Magnolia packs give one bound P7 soul, 5,000 CPs and 1,000 RVPs. Existing items
and completed receipts are preserved. See [current policy](DAILY_REWARD_POLICY.md).


Source validated 2026-09-21; not deployed or accepted in the genuine client.

Ordinary players at level 50 or above travel from Daily Envoy 79882 to hall
3081 and approach any of the six registered statues 98130-98135. One daily
completion gives a bound level-1 Tortoise Breaker rune (4030101), the original unbound Chi Token 729304,
configured CP/RVP (defaults 2,500/15), and one EXP-ball amount below level cap.
Two inventory spaces are required. Dialogue stays brief.

The rune addition is a Revora reward choice under the owner's valuable daily
reward requirement. It uses the same class-neutral catalog identity as Summer
Hunt. Existing rune equipment eligibility is unchanged. No daily P7 is granted.
The nullable `rune_uid` receipt column preserves historical receipts; completed
days are never reopened for a retroactive rune. New claims save the exact rune
UID alongside the Chi Token, purse and EXP in the existing transaction.

Latest validation: isolated build and 521 Worship checks including shared
prerequisites passed. New rune coverage includes all six statues, simultaneous
claims, exact receipt UID and fresh item binding, suppressed rune-insert rollback,
one-slot rejection/two-slot admission, day rollover and autosave. Log:
`obj/chi-refill-validation/worship-rune-tests.log` (ignored). This is server and
persistence evidence, not native rendering or deployment acceptance.

The entity row, saved inventory/profile/currency preimages, both item inserts,
EXP/level/skill/Arena effects, daily projection and unique player/day receipt
commit together. Memory and client updates follow commit. Failures fence the
session; a reconnect reads the saved outcome. Replay cannot award twice.

The authoritative receipt uses the existing Pacific quest calendar. The latest
paid date also blocks clock rollback. Saved legacy paid state for the current
reset date imports as a zero-reward receipt. Old daily reset/autosave projections
cannot reopen a paid receipt. The legacy worship column remains a small integer;
no schema conversion is required. Mission 90004 is not rewarded again.

Validation: isolated server build and 520 Worship checks, including 403 shared
first-rebirth checks. Six real Envoy-to-statue routes walk reachable hall tiles;
concurrent claims, separate autosave races, exact item/profile reload without a
post-claim save, suppressed purse/profile/item/daily/receipt writes, stale state,
capacity, overflow, eligibility, Pacific midnight, legacy import, rollback,
level cap and level-70 skill/Arena admission failures all pass. Disposable test
schema only. Native dialogue, item binding display and live restart remain open.

Daily Envoy / Daily Price regression: 556 checks pass. Achievement producer
audit remains 240/337 (97 missing); this is not an overall completion percentage.


## Retained hall entry and cold daily claim (2026-10-01)

Retained ordinary character4294908703 continues from the corrected Market Errand
checkpoint. NPC79882's ordinary entry option reaches installed hall3081, with
statue98130 loaded. The process exits there before any Worship reward is claimed.

A fresh game/database process preserves the hall location, prior inventory/purses,
quest records and absence of a Worship receipt. Ordinary statue claim then grants
the configured2,500CPs/15RVPs, one unbound Chi Token and one bound level1 Tortoise
Breaker rune. Silver and Study Points are unchanged. The durable non-legacy receipt
records the statue, Chi UID and rune UID, with no P7/soul UID. Repeated claim cannot
add another reward. Another abrupt exit leaves that paid checkpoint for cold reload.

The final phase compares the full saved items, purses/position/EXP, Study Points,
prior Price/Rare/Market records and Worship receipt, then repeats the claim at the
same day and a controlled earlier day. The original paid state remains exact. All three process phases pass.
No achievement count is added to the main retained320-career character by this
separate ordinary-profile route.

`WorshipCareer` in `tools/CheckCandidateCareerRites.cs` owns
`career-worship-enter`, `career-worship-claim`, `career-worship-reload`.
Calendar and NPC approach coordinates are controlled. This does not repeat or
extend the previous six-statue movement/level-up tests into native acceptance;
continuous walking, native presentation and deployment remain pending.
No production source change, live restart or deployment is part of this batch.
Private executable SHA-256:
`B08AF6399258E9F2A07BC8557018FEBA5F7EF3D1A78208D4C67738BB12C5FCA6`.

## Fresh production-created career (2026-10-03)

The fresh ordinary career now enters through the Envoy, reloads before worship,
claims at statue98130 and preserves the exact reward/career after another process
restart. Same-session and cold duplicate prayers pay nothing. No supplied quest
qualification or clock override is used; controlled proximity and native/live
limits remain. [Combined evidence](CAREER_STARTING_BOUNDARY.md#fresh-ancestor-worship-and-combat-filled-soul-ball-2026-10-03).
