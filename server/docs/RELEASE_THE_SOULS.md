# Release the Souls earning bridge

M4.4a connects the Daily Quest Envoy's existing soldier route to persistent
objectives, native mission 6049 and verified achievement 11012. Ordinary-player
code and automated checks are complete. Live activation and genuine-client
acceptance remain pending.

## Edition and ordinary-player route

Official editions differ: the [earlier expansion guide](https://co.99.com/guide/quests/10expansion/release_the_souls.shtml)
required sixteen remains, the [2012 edition](https://co.99.com/guide/quest/2012/Release_the_Souls.shtml)
required eight, and the [newer guide](https://co.99.com/guide/quests/release_the_souls.shtml)
describes three. The installed 6711 mission explicitly requires **level 70 and
three souls**, so those are this restoration's eligibility and objective rules.

The Daily Quest Envoy (79882) sends eligible players to the Desert Guardian,
near Desert 536,643. The guardian keeps its installed appearance but uses runtime
NPC identity **301**, matching the client's mission target. The database's older
15140 actor is removed from the loaded map; its legacy payout switch is removed.

Accept the task, find three **different** Soldier Remains among the nineteen
installed actors (65822–65840), and remain still for each three-second inspection.
Return to the guardian to claim the reward. The NPC must remain nearby and the
inspection must belong to the same canonical character session and Pacific day.
Moving, cancellation, disconnect or death prevents unfinished inspection credit.
Repeated clicks on one soldier do not count as different souls. Each player can
use the same world actors independently; the old timer no longer relocates shared
NPCs or treats the legacy counter as completion evidence.

One run and payout are allowed per **America/Los_Angeles calendar day**. An
unfinished inspection cannot cross midnight. Yesterday's progress remains in
history, but today's route starts fresh. A new daily completion never creates a
second copy of the permanent achievement.

## Rewards and paid assistance

The existing Revora purse is preserved: **2,500 CPs and 15 RVPs**, plus a bound
EXP Ball (722136), bound Chi Token (729304) and **50 study points**. The study
amount matches the installed mission; the old server default was 25. EXP Ball
item delivery, extra currencies and omitted random soldier bonuses are explicit
Revora v1 choices. This code does not claim to reproduce the historical direct
120-minute XP payout. Native reward text needs live review for this item delivery.

The current server's **5,000-CP** temple-assistance price is preserved. It requires
the full fee up front, explicit confirmation within two minutes, normal inventory
space and point limits. Its normal purse still pays after deduction, producing
a default net cost of 2,500 CPs. Paid assistance finishes that day's mission but
**does not grant 11012**. Only personally releasing the three souls earns the
achievement. The dialogue states this before payment.

The accepted run snapshots currency, study, token and assistance-price settings.
It cannot silently change price or rewards halfway through the day. A duplicate
confirmation cannot charge again. A full bag, insufficient CPs or point overflow
leaves the reward available. The Daily Envoy's legacy reset applies to its other
tasks; it cannot reset this new earning record or replay its reward.

## Persistence and client projection

`revora_soul_runs` holds one row per character/day: state, distinct visited actor
IDs, reward/fee settings and delivered item UIDs. Normal and paid final states
remain distinct. Currency, study points, two inventory items, final state and
normal-path achievement evidence share one transaction. The character item lock
and database row locks serialize claims. The M6.59 follow-up below compares all
three saved balance preimages and the complete inventory UID set, and requires
every item/completion write to succeed. Client updates follow commit.
Failure fences further mutations until reconnect.

Native mission 6049 is registered in the existing quest-list catalog. Accepted,
objective and finished states are projections of the saved run. They are rebuilt
on login and refreshed at the Envoy/guardian. Client Begin packets cannot accept
or complete this authoritative quest. The legacy quest cache is updated for normal
serialization and GUI paging, but never authorizes payouts or achievements.
The quest-cache save now parameterizes its character identity.

Private client evidence was read-only:

- `Questinfo.ini`: `f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14`
- `Achievement.dat`: `df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`
- `itemtype.dat`: `d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a`

See [protocol evidence](PROTOCOL_RESEARCH.md) for the existing 1134/1135 writers,
corrected list bounds and the runtime guardian alias. No new packet layout,
proprietary asset or client file is committed.

## Validation and remaining work

The isolated build passes **35 soul-quest checks**. They use ordinary players,
actual NPC dispatch and timer completion, native quest serialization/reload,
independent players sharing the same remains, malformed/client-authored state,
midnight boundaries, cancelled inspection, repeated claims, injected database
failures, point limits, paid assistance and ordinary item-loader recovery.

All **459 regression checks** also pass: 9 quest-packet, 31 Moon Box, 95 Power
Arena, 44 Demon, 78 Elite PK, 48 CTF, 63 progression and 91 wardrobe checks.
This batch totals **494 checks**. Validated staged executable SHA-256:
`e7ae915a37ef33225d17bf211c4de0f355662155b340b31148ce7272a0c9c3a6`.

Run `tests/PotencyHunt/Verify-PotencyHunt.ps1 -SoulsOnly`. All fixture modes use
the disposable `revora_potency_validation` schema and must run sequentially.
The native tracker, inspection bar, guardian pathfinding, reward text and live
currency displays still need genuine 6711 acceptance. These automated checks
do not claim a complete real-client login session.

No live database migration, server restart/replacement, client patch or website
release was performed. Preserve the current binary/database before activation,
review M1's legacy achievement migration, then verify the full ordinary-player
route and paid alternative. Rollback restores the old binary while preserving
earned currency, items and new quest/achievement records.

Other Daily Envoy tasks have no established matching achievement in this audit.
Their older payment/completion implementations are not certified by this change,
and are not assigned unrelated historical achievements. Quiz and local tournament
earning bridges are separate remaining M4 work.

## M6.59 transaction and ordinary route follow-up (2026-09-16)

The reward now compares saved CP, RVP and Study Point balances against the
canonical player's expected balances. It verifies the saved bag UID set and
checks both gift inserts, terminal receipt update and earned achievement write.
Missing writes roll back the entire bundle and require a normal reconnect.
Acceptance and each inspection likewise refuse to project unsaved progress.
Saved owner eligibility and a future daily record prevent accepting or paying
an earlier day after a backward clock change. Existing reward snapshots remain
intact; no daily price, gift identity or three-soldier requirement changes.

Paid offers belong to one session, expire after two minutes, and are cleared by
login or farewell. The full fee is still required up front. Guardian/remains
2032 continuations are bounded before parsing; echoed NPC identity, canonical
ownership, physical proximity and day are checked, including immediately before
commit. This is source/fixture validation, not new native wire capture evidence.

Positive Study Point awards now earn 10319 in the same reward transaction.
Ordinary three-soldier completion also earns 11012; paid assistance does not.
Both use saved evidence and immediate normal achievement refresh. A prior
positive Study Point balance alone grants no evidence.

Validation uses ordinary characters, production character/item reloads,
concurrent claims, canceled/replaced/expired offers, malformed frames, stale
wallets/bag, backward days and deliberately suppressed acceptance, inspection,
currency, gift, completion and both achievement writes. A complete ordinary
walking route reaches three actual soldier positions, resumes each saved
inspection, returns to the Guardian, and consumes the earned EXP Ball for exact
EXP. Its remaining bound Chi Token survives reload; this batch does not claim
validation of the seven-token redemption chain in that batch. M6.60 subsequently
validates seven complete daily routes and their actual-token exchange; see
[Chi Token cash redemption](CHI_TOKEN_CASH.md).

The isolated build passes 487 Release Souls checks (including the 392 shared
prerequisite checks), plus 519 Daily Envoy/Price Hunt checks and 9 quest-packet
checks: 1,015 checks across suites. Three producer-audit tests also pass.
Logs: `obj/chi-refill-validation/soul-transactions.log`,
`soul-envoy-regression.log`, `soul-quest-packets.log` (ignored).
Final staged server SHA-256:
`49645C6034545586BE8AEE91598A5C30B697890497080C29860F548A88E934E0`.
Final harness SHA-256:
`D841917A1648D93131DD229CDA711943E3DDB4673BFFE6B1911BA7958FA6068D`.
Source coverage remains 222/337; this adds an earning path, not another
achievement identity. Preserve reward receipts and earned evidence on rollback.
No live deployment, client patch or native visual acceptance is included.

## Owner reward upgrade (2026-09-20)

New accepted runs freeze `valuable_reward=true` and guarantee an additional
unbound catalog P7 soul at successful completion. Existing runs default false
and retain their prior rewards. This is a Revora policy, not a retail drop claim.
Release Souls includes its existing paid option without the earned achievement;
Moon Box includes both the Moon Box and three-Meteor endings, never refusal.
Existing repeat limits and other gifts remain unchanged. Capacity includes the
extra item. Completion, exact soul UID, other rewards and any consumption or
payment are committed together; NPC text reflects the upgraded contract.

Isolated build and 504 checks pass, covering old contracts, full bags,
failed claims, concurrent claims and item reload. Release Souls additionally
checks a suppressed soul insert and the complete walked route through using its
EXP Ball and reconnecting. Moon Box checks both successful endings, refusal and
zero saved P7 items after final-claim failure.
Log: `obj/chi-refill-validation/release-souls-p7.log` (ignored).
Live deployment and genuine-client acceptance remain pending.

## Fresh ordinary career and real prayer deadlines (2026-10-03)

The production-created fresh character now earns three distinct prayers without
supplying progress or overriding the quest clock. Immediate ticks grant nothing;
actual3.1-second waits complete each prayer, and duplicate prayers do not advance.
Separate unclaimed/paid processes verify exact non-P7 rewards, achievement11012,
prior career preservation and repeat refusal. This current new-run rune reward
supersedes the historical P7 edition described above; existing contracts retain
their documented policy.
[Evidence and limitations](CAREER_STARTING_BOUNDARY.md#fresh-release-souls-with-real-prayer-deadlines-2026-10-03).
