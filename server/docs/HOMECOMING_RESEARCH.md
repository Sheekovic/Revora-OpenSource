# Homecoming editions and activity prerequisites (M8.1a-b)

2026-09-21 follow-up: the May announcement remains accessible and confirms its
fixed historical eligibility window and single VIP-based title. Both linked
event roots remain inaccessible through the research browser. Focused official
domain searches did not recover title lifetime or VIP evaluation timing.
Owner decision, 2026-09-22: implement a present-day Revora adaptation using
30 days of verified inactivity, retaining level 120+ or reborn eligibility and
the installed VIP-tier mapping below. Unknown activity must not qualify. The
fixed 2016 dates are historical evidence, not the selected eligibility rule.
Owner follow-up, 2026-09-22: titles last 30 days from the qualifying return,
with the VIP tier snapshotted at that return. Later upgrades or delayed claims
must not change that tier or extend expiry. This establishes the product rule,
not by itself implemented eligibility or claims. The implementation sections below
record subsequent source progress.
Do not repeat unavailable-page searches without new evidence. Native acceptance
remains pending at the owner's explicit direction; continue server-side work.

Research checked 2026-09-16 against the official notices and installed wardrobe
requirements. Homecoming now has a validated server snapshot/grant path for proven
returns and process-owned interrupted-session recovery behind an exclusive
database lease. The later sections record successful VIP7 socket login, Host
restart, equipped selection and expiry checks with simulated elapsed time. Other
VIP-tier socket cases, native rendering and deployment remain pending. Label it separately from the historical editions; ordinary VIP login
alone does not earn a returnee title.

## Confirmed dated evidence

The [May 12, 2016 announcement](https://co.99.com/news/2016-05-12/homecoming_quests.shtml)
identifies a May 12-June 12 campaign for level 120+ or reborn heroes. Its historical
eligibility specifies departure between January 1, 2014 and April 2, 2016, with
no return during April 3-May 5, 2016. It describes goal points and prize exchange,
daily sign-in at Welcome Specialist (Twin City 307,247), and one VIP-dependent
title per returning hero. It does not specify title duration or the VIP evaluation
moment. The May 6-11 gap is not resolved by this announcement.

The [June patch announcement](https://co.99.com/news/2016-06-16/patch_6270.shtml)
sets Homecoming Guide at June 16-July 31. The [June promotional notice](https://co.99.com/news/2016-06-27/homecoming_guide.shtml)
links separate returning-player title rewards and a Guide/Traveler activity open
to all players. The [June 23 correction](https://co.99.com/news/2016-06-23/homecoming_guide.shtml)
requires the Traveler's confirmation when a Guide adds them. These establish a
separate edition; they do not carry May's absence dates into June.

The installed wardrobe mapping remains:

| Key | Title | VIP tier |
| --- | --- | --- |
| 20282028 | Earth Knight | 0 |
| 20302030 | Sky Knight | 1-4 |
| 20292029 | Glory Knight | 5-6 |
| 20312031 | Paladin | 7 |

Tier mapping is installed-client evidence already inventoried in the
[reward matrix](research/WARDROBE_REWARD_MATRIX.csv), not a conclusion from missing
web artwork. The ordinary authenticated-player path must check earned campaign
eligibility before choosing its tier.

The announcement links resolve to `event.co.99.com/veteran1605/` and
`event.co.99.com/veteran1606/`. Both returned 502 during this check; the linked
June title artwork also failed. The mail landing page redirected to the official
error page. Search-result archive indices are mutable: the old indexed May
listing was page 201, while the same article appeared on page 208 when opened.
Use the stable dated article URLs above for claims, not an archive page number.

## Repaired activity persistence

Repository baseline: `c6f3095`. `EntityTable` previously wrote `LastLogin` as
64-bit .NET ticks, read it through a 32-bit helper, and interpreted nonzero values
as a decimal calendar date. Normal tick values failed parsing to zero; loading
then substituted the current time. Saving also wrote the previous observation
before advancing memory, and failed writes could still advance runtime history.

`CharacterActivity` now decodes modern local ticks or a valid legacy
`yyyyMMddHHmmss` calendar value. Missing, nonpositive and unsupported numeric
values remain `DateTime.MinValue` (unknown); they do not establish absence.
The first successful save normalizes them to the current observation in ticks.
Malformed nonnumeric/out-of-range database values fail strict loading rather
than being silently treated as old activity.

Saves condition the row update on the exact loaded raw timestamp and advance
character/guild projections only after a successful write. A stale loaded
session cannot overwrite a newer saved timestamp. A backward clock adjustment
cannot reduce the known local activity value. Guild member loading uses the same
decoder. There is no schema rewrite or retrospective backfill.

This field remains **last successful character-save activity**, not an account
admission log. It cannot prove that no character on an account logged in, recover
lost historical observations, establish a first campaign return, or remove local
time/DST ambiguity. `GuildLastLogin` and the separate login-alert protocol are
not reinterpreted by this change. No title is granted from this prerequisite.

## Implementation sequence still required

1. Recover the chosen edition's title lifetime, first-return contract and VIP
   evaluation point; retain missing evidence explicitly. Do not invent reward
   quantities from the promotional lists.
2. Implement the selected 30-day verified-inactivity rule as an explicitly
   documented Revora edition, retaining level/rebirth and VIP tiers.
3. Capture trusted account/character presence and first return before routine
   login/save updates, keeping unknown historical activity ineligible.
4. Freeze campaign identity, eligible recipient, tier and reward duration in
   durable receipts; deliver the single earned title atomically through the
   established progression award path. VIP changes or reconnects must not create
   a second title claim.
5. Add Guide/Traveler only with confirmed goal/reward rules and mutual consent.
   Validate ordinary players, concurrency, replay, restart and expiry, then
   genuine 6711 NPC/wardrobe display.

## Validation

Staged build SHA-256:
`FDC7A129FCDBAA4E1C4A13BE22E189609488F1DE3220F5E0C21B167FB21A7FE5`.

The full-schema guild suite passes 344 checks, including 21 new activity cases:
null/malformed dates, leap-day ticks and packed dates, exact current-save values,
ordinary reload, clock rollback, suppressed writes, stale history, legacy
normalization and unknown-history initialization. All fixtures are ordinary
non-GM characters in a disposable schema. This is persistence validation, not
Homecoming earning-path or native-client acceptance.

Related suites pass: progression 74, titles 91, appearance 23, friends 43,
mentor admissions 55, auction/silver persistence 89 and Potency/daily bosses/
Extraction 216. Total: **935 checks**.

No live restart, deployment, client patch or production-row mutation occurred.

## Authenticated account observations (2026-09-22)

`AccountActivity` now creates `revora_account_activity` before Account/Game
listeners start. After successful SRP proof verification and before issuing a
game-forwarding credential, the server records a database UTC timestamp against
the stored canonical account name and matching character identity. It maintains
first/last observed authentication and a count, with row-safe concurrent updates
and monotonic last observation. Missing, stale or ambiguous identities and
suppressed writes fail the observation; forwarding is rejected on failure, with
only an exception type logged. No credentials or network payloads are persisted.

Build and 39 isolated account lookup/verifier checks pass, including six new
observation cases: unknown history, saved timestamps, concurrent case variants,
clock rollback, rejected writes and stale character identity. The standalone SRP
verifier suite also passes. Logs: `obj/chi-refill-validation/account-activity-build.log`,
`account-activity-tests.log`, `account-activity-srp.log`.

This records authenticated presence, not verified absence. A player may remain
online without authenticating again. Online-session coverage, interruption/gap
handling, first-return receipts, level/rebirth and VIP selection, title lifetime
and atomic claims still need implementation. Neither old character-save values
nor this authentication table alone qualify a player for Homecoming. No reward
is enabled, no private/runtime refresh occurred, and native acceptance remains
pending by owner choice.


## Durable game-session presence (2026-09-22)

Before acknowledging game entry, `AccountActivity.Open` records a unique session,
canonical account, matching character and database UTC opening time. Each
`GameState` owns its identity; repeated opens are idempotent and a closed state
cannot reopen. Session registration cleanup closes only that session. A delayed
old disconnect cannot close a newer login. Failed entry writes reject entry;
failed closure writes leave an open, uncertain record and log only exception type.
Database clock rollback cannot put closure before opening.

Startup deliberately preserves interrupted open sessions. These must block any
future inactivity eligibility until a separately validated recovery/coverage
policy resolves them. This batch does not infer logout from a restart, implement
first-return eligibility or enable rewards. Session history retention and gap
recovery remain outstanding. No private runtime refresh or deployment occurred.

Build and 49 isolated account checks pass, including ten new presence cases:
overlapping sessions, delayed old closure, duplicate closure, restart uncertainty,
suppressed closure, clock rollback, stale character identity, idempotent GameState
entry/exit and rejected reopening. Logs: `obj/chi-refill-validation/account-presence-build.log`
and `account-presence-tests.log`. These are persistence/lifecycle fixture results,
not a real client login or native acceptance result.


## First-return evidence receipts (2026-09-22)

Successful authentication now freezes an account return observation in
`revora_account_returns` before advancing `last_auth`, in the same transaction.
The observation requires at least 30 elapsed UTC days since the latest of first
observed authentication, last authentication and every recorded session closure,
with no open session. Unknown history cannot qualify. The receipt preserves the
canonical account, authentication sequence, current character identity, start of
inactivity and return timestamp. This is account return evidence, not a title
eligibility or reward claim.

Account row locks serialize authentication and new session openings. Concurrent
case variants cannot each consume the same inactivity interval. Suppressed
receipt insertion aborts authentication without erasing the prior interval;
a suppressed authentication update rolls back its newly inserted receipt.
Interrupted open sessions still block qualification: startup cannot safely infer
that another process stopped. Process ownership/recovery remains outstanding;
this batch does not close that gate or grant credit for unknown historical gaps.

Build and 58 isolated account checks pass, including nine new return cases:
short absence, concurrent return, frozen duration/identity, active session,
recent logout, insufficient history, rejected receipt insertion, rejected auth
update and a later distinct interval/retry. Logs:
`obj/chi-refill-validation/account-return-build.log` and
`account-return-tests.log`. Title eligibility still needs durable level/rebirth
and VIP evaluation, the owner-selected lifetime, atomic grants and player-facing
access. No deployment, runtime refresh or native acceptance is claimed.


## Approved title term and tier snapshot (2026-09-22)

The owner selected 30 days from the qualifying return, with VIP snapshotted then.
Implement expiry as the saved qualifying-return UTC instant plus 30 days; a
late claim cannot restart the term. Keep the existing level 120+ OR reborn gate
and Earth/Sky/Glory/Paladin tier mapping. Do not backfill old return observations
using a later VIP value: observations without contemporaneous character evidence
cannot establish the approved tier.

Implementation must account for the existing loader's VIP minimum and expiry
adjustments (`EntityTable`) and its separation of stored versus pending VIP state
(`VipState`). Reading only today's `entities.viplevel` during a later claim would
not meet the rule. This inspection establishes the next integration requirement;
no VIP snapshot or title claim is implemented by this decision record.


## Return snapshot and ordinary title grants (2026-09-22)

The authentication transaction now freezes a versioned character snapshot with
level, rebirth, raw VIP level/days, VIP duration metadata, last-reset ticks and the
server timezone. It stores the selected title and expiry at return UTC plus
exactly 720 hours. A missing character or invalid/ineligible profile gets no
title. Legacy return rows without contemporaneous snapshots are not backfilled.
Failure to store the selected tier rolls back both receipt and auth timestamp.

Snapshot rule v1 maps frozen raw VIP 0 to Earth Knight before the login floor.
VIP 1-7 still apply Math.Max(4, vip); when a daily reset is due, a last remaining
VIP day reduces VIP 6/7 to 5. This does not change VIP pricing or decay.
Sky Knight, Glory Knight, Paladin and Earth Knight are reachable from snapshots.
The level gate is 120+ OR reborn. Out-of-range or missing profile data cannot
qualify. Later profile changes never select a different return title.

Ordinary achievement/wardrobe refresh now materializes a qualifying saved return
as a `homecoming-revora-v1` award, with the original return as `earned_at`.
The receipt is locked and acknowledged in the same transaction as the grant;
replay/concurrent refresh cannot duplicate it. Missing snapshots and expired
unclaimed returns are ignored. A claimed title expires at the original deadline,
including removal of an equipped title. Delayed login/claim never extends it.
Receipt identity uses an account-name hash plus auth sequence; no new credential
material or player-facing administrative endpoint is introduced.

Build passes, with 80 account checks, 117 title checks and 122 progression checks.
New account cases cover immutable profile/expiry, effective VIP tiers and the
exact expiry/reset boundaries, level/rebirth gates, bad/missing history and
transaction rollback. New ordinary-title cases cover grant/equip/reload, delayed
expiry, replay/concurrent retry, rejected grant/acknowledgement, old snapshots and
expired pending returns. Fixtures use disposable schemas; title integration seeds
return receipts. These results do not prove a combined native login journey,
actual process restart or interrupted-session recovery. Existing open/uncertain
sessions still block qualifying return evidence. No deployment or runtime refresh.

Logs: `obj/chi-refill-validation/homecoming-snapshot-build.log`,
`homecoming-snapshot-tests.log`, `homecoming-titles.log`,
`homecoming-progression.log`. Native acceptance remains pending by owner choice.


## Integrated server return flow (2026-09-22)

The title suite now exercises an ordinary persisted account/character through
`AccountActivity.Authenticated`, session opening/closure, a qualifying return,
`ProgressionEvidence.Read` and the ordinary wardrobe login/equip path. It creates
no return receipt or title award directly. Fixture time passage is represented
by aging prior activity/session observations in the disposable schema.

The flow rejects unknown history, open presence and recent logout. Four
concurrent case-variant authentications then produce exactly one return after a
simulated 31-day absence. Changing durable and cached VIP/level afterward does
not alter its frozen Paladin award. The integration verifies the grant's exact
saved timestamps and receipt acknowledgement, database reload/equip, validity
one microsecond before expiry, and removal/unequip exactly at expiry.

The title suite passes 127 checks (ten new integration cases); log:
`obj/chi-refill-validation/homecoming-flow-titles.log`. Production source is
unchanged from the preceding snapshot/grant batch. This test enters at the
post-proof authentication hook: it does not perform SRP exchange, socket login,
real process restart or native-client acceptance. It closes the gap between the
return producer and wardrobe consumer in the server fixture. This batch left
process ownership unfinished; later sections record exclusive-lease recovery.
No deployment or private runtime refresh occurred.


## Process-owned session recovery (2026-09-22)

Each durable game session now stores the live process identity that opened it.
`AccountActivity.Initialize` closes open rows this process does not own, including
legacy rows with a null owner, using the same `GREATEST(opened_at, clock)` closure
as a normal disconnect. Same-process reinitialize keeps this process's live
sessions. A suppressed recovery write fails startup recovery and leaves the
interrupted row for retry; it does not invent inactivity.

Recovery records closure at recovery time. That restart does not by itself create
a 30-day absence. After the recovered closure ages thirty UTC days with no later
open session, ordinary authentication can freeze return evidence and the existing
wardrobe path can grant the snapshotted title.

Isolated Account lookup checks now pass **88** cases, including live-session
preservation, previous-process and null-owner recovery, failed-write retry,
blocking before recovery, and qualification after an aged recovered closure.
The title suite passes **189** checks, including six recovered-session cases:
the leftover presence still blocks, startup recovery closes it, an aged
recovered closure can freeze a Paladin return, and ordinary wardrobe read/login
owns that grant. These are disposable-schema results, not a Host restart or
native login. Native acceptance remains pending by owner choice. No deployment
or runtime refresh.


## Exclusive process lease (codex-0001)

A different process GUID is not treated as process death. `Initialize` creates
schema, then acquires a session-level PostgreSQL advisory lock (`6711,1`) on a
dedicated unpooled connection, then recovers foreign sessions. `pg_try_advisory_lock`
failure throws before recovery. The lock is released only when that connection
closes (clean `Relinquish`, process exit, or backend death). Same-process
reinitialize keeps the existing lease and its own sessions.

A second live process therefore cannot close the holder's sessions, including
when the second process would later fail to bind listeners. After the holder
process exits, the successor may acquire the lock and recover leftover sessions.

Two actual AccountLookup child processes cover challenger-denied while the
holder lives, and takeover recovery after the holder exits. GUID-substitution
fixtures remain for owner-column recovery; they are not the exclusivity proof.
Native acceptance and deployment remain pending.

## Runtime lease loss while serving (codex-0003)

Ownership is required for the whole serving lifetime, not only `Initialize`.
`SessionOpened`, `SessionClosed` and `Authenticated` run on the locked lease
connection. `HoldLocked` confirms the original advisory lock in `pg_locks` on
that backend; it does not call `pg_try_advisory_lock` again, so a dropped lock
cannot be silently reacquired. A dead or stolen lease fences this process until
exit. Intentional `Relinquish` still allows a later acquire.

`Recover` uses the same protected connection inside the acquire lock, so a
second process cannot take ownership between acquire and recovery. If closure
cannot be trusted after fence, `SessionClosed` skips the write and leaves the
open row for the successor.

Two-process proof: terminate the holder's lease backend with `pg_terminate_backend`
while that process stays alive; the holder cannot open sessions, authenticate,
qualify a return, or reacquire; a successor recovers the interrupted session.

A second two-process case terminates the backend after the holder has confirmed
the lock and before `Recover` runs, then asserts `IsFenced` before `Relinquish`.
`ProtectLease` fences every exception unless the lease connection still holds
the advisory lock; ordinary validation `InvalidOperationException`s therefore
stay unfenced only while that connection is healthy. Closed-connection cases
cover `Recover`, `SessionOpened`, `Authenticated` and `SessionClosed` in
separate child processes and require the fenced flag immediately, without
`Relinquish`. Raw AccountLookup output is saved at
`obj/chi-refill-validation/homecoming-lease-tests.log`. Native acceptance and
deployment remain pending.


## Ordinary all-tier earning and recovery (2026-09-26)

The dedicated `-HomecomingPublicOnly` fixture passes 470 checks: 406 shared
prerequisites and 64 focused checks across raw VIP 0 through 7. Ordinary saved
accounts exercise the post-proof authentication hook, session open/close,
concurrent qualifying returns, all four frozen title tiers, wardrobe equip,
full character reload and exact 720-hour expiry. Later VIP/level changes do not
alter the frozen entitlement; repeated login cannot renew an expired award.

Inactivity is simulated by aging real activity/session producer rows in a
private test schema. Level/rebirth/VIP are supplied prerequisites; no return or
award receipts are seeded. This is server earning-path evidence, not actual
socket authentication, a live 31-day wait, Host restart or native acceptance.
Production code is unchanged. Evidence: `VerifyHomecomingPublicTiers` in
`tests/PotencyHunt/TitleTests.cs`; local output
`obj/homecoming-public-tests.log`. Deployment remains pending.


## Actual active-player socket presence and restart (2026-09-27)

The private ordinary earned-career account now passes two actual Account/Game
TCP login and Host/database stop/start cycles with `-CheckHomecomingPresence`.
Each login increases durable authentication activity and adds exactly one game
session. All recorded sessions have the canonical character UID, a process
owner and a non-null closure at or after opening. No return receipt is created
for this recently active account. The complete session-history fingerprint and
authentication count survive the intervening stopped-database restart unchanged.

This uses existing candidate gameplay, without a rebuild, synthetic activity,
eligibility changes or seeded return/award receipts. Existing career reward and
equipped Honored Hero persistence checks also remain passing. The private game
and database are stopped afterward; no live deployment or restart occurred.

Validation: `tools/Verify-MoonBoxDatabase.ps1` with the existing full career
flags through `-CheckSecondRebirth`, plus `-CheckHomecomingPresence`.
Ignored evidence: `obj/homecoming-presence-wire.log` (two passing cycles).
This closes the active-player presence/restart prerequisite only. Actual socket
qualification after inactivity, return-title grant/equip/expiry, interrupted
presence recovery in the full Host and native acceptance remain open. Prior
all-tier component tests do not substitute for those transport checks.


## Actual interrupted-session recovery (2026-09-27)

The same private ordinary career now passes a deliberate Host termination while
its real authenticated Game TCP session remains connected. The probe requires
EOF/reset after termination, not a timeout. SQL then requires exactly one open
session, canonical ownership, increased authentication activity and no return
receipt. Its complete history fingerprint survives the stopped-database restart.
The next actual Host/login cycle closes the interrupted presence, adds exactly
one new normally closed session, and leaves zero open rows and zero return
receipts. All earned career receipts, item identities and equipped Honored Hero
remain intact after the interruption and restart.

The existing hash/path/PID-owned crash harness now supports the completed
second-rebirth career; crash-target validation is retained. An initial harness
attempt stopped at its old Soul Trial/crash exclusion before the probe; the
revised exclusion permits the completed-career route. The passing run uses
`-CheckHomecomingPresence -CheckCourierCrash` with the full career flags through
`-CheckSecondRebirth`. Evidence: `obj/homecoming-interruption-wire.log`.

This is actual Host interruption recovery, without synthetic activity/awards or
a game rebuild. Database/power-loss and in-flight ambiguous reward commits are
not tested. Qualifying-return socket earning/equip/expiry and native acceptance
remain open. The private processes are stopped; no live restart or deployment.


## Qualifying ordinary socket return and ownership persistence (2026-09-27)

The private earned-career character now receives its Homecoming title through
actual Account SRP authentication and Game login. Only inactivity is simulated:
the existing canonical activity and closed-session timestamps were shifted back
31 days in the disposable database, retaining ordering and an external JSON
preimage. No character eligibility, VIP, return receipt or award was inserted or
changed by the fixture. The original live-earned character remains reborn twice.

The first probe expected the VIP6 tier and failed its ownership assertion. A
read-only inspection showed the actual saved VIP7 snapshot and a correctly
materialized20312031 award: 31 days of inactivity and exactly30 days of validity.
The probe expectation was corrected; no reward or receipt was rewritten.

Two subsequent actual Host/database cycles pass ordinary3301 equip, return-title
ownership at login, exact frozen receipt preservation and nonrenewal. SQL verifies
the canonical account/character, VIP7/rebirth2 snapshot, award-recorded state,
unrevoked matching event award and expiry exactly720 hours after the return.
Honored Hero is restored after each equip check, and all career receipts and
items remain intact. Consequently this proves Homecoming ownership persistence,
not persistence of its equipped selection. Actual expiry and other VIP tiers
through sockets remain open; existing all-tier component checks cover those
rules separately. Native rendering and deployment remain pending.

Command: the full ordinary career flags through `-CheckSecondRebirth`, plus
`-CheckHomecomingReturn` on `tools/Verify-MoonBoxDatabase.ps1`.
Evidence: ignored `obj/homecoming-return-wire.log`; the protected private tree
retains `homecoming-activity-preimage.json` outside Git. The fixture refuses to
age history twice and reuses a single existing return on retries. The earlier
no-return presence mode intentionally no longer applies to this account after
its qualifying entitlement exists. Game/database are stopped; no game rebuild,
live restart or deployment occurred.


## Equipped return-title socket persistence (2026-09-27)

The qualifying-return probe now leaves20312031 selected. Two actual private
Host/database cycles pass; the second login requires Homecoming ownership and
its equipped flag before any new equip request. SQL independently verifies the
saved five-byte title record with the equipped byte set. Honored Hero remains
permanently owned, and its separate entitlement is still checked; selecting
Homecoming does not erase the earlier career reward. The frozen return receipt
and720-hour expiry remain unchanged.

Validation reuses `-CheckHomecomingReturn` and the full career flags. Evidence:
`obj/homecoming-equipped-wire.log`. No additional timestamp fixture changes,
game rebuild or live deployment. The earlier31-day inactivity simulation still
limits this evidence. Actual expiry and other VIP-tier socket cases remain open,
along with native rendering and deployment. Private game/database are stopped.


## Expired return through actual socket login (2026-09-27)

Two actual private Host/database cycles now verify expiry enforcement. The
existing matched return receipt and event award were aged31 days together,
preserving the30-day duration and inactivity interval. Their original JSON
preimage is retained as `homecoming-expiry-preimage.json` outside Git. No new
receipt or award was created; account activity remains recent, preventing a
second qualifying absence. This simulates elapsed time, not a real30-day wait.

Login no longer sends ownership/equipped state for20312031, and an ordinary equip
request cannot restore it. The saved wardrobe no longer contains its record.
The exact expired return receipt remains unchanged before/after both cycles,
and the corresponding historical award still matches its original duration.
Permanent Honored Hero can be equipped again and survives restart; all prior
career rewards remain intact. No gameplay fix or rebuild was necessary.

Validation: full career flags plus `-CheckHomecomingReturn
-CheckHomecomingExpired` on `tools/Verify-MoonBoxDatabase.ps1`.
Ignored evidence: `obj/homecoming-expiry-wire.log`. Private processes stopped.
The expiry mode is explicit and idempotently reuses already-expired evidence;
the positive active-title mode is no longer applicable to this expired fixture.
Other VIP-tier socket cases, natural elapsed-time observation, native rendering
and deployment remain open. Existing component fixtures cover all tier rules.
