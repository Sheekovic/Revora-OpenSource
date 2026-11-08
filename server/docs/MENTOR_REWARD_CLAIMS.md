# Mentor stone and blessing claims

Baseline: `8f154f9`. M6.35 restores ordinary-player claims through the existing
2067 actions 4 and 3. Implementation and fixture validation are complete;
genuine 6711 acceptance and deployment remain pending.

## Delivered behavior

A +1 Stone costs exactly 100 contribution points. Active and retained balances
can combine; only whole stones that fit within the 40-slot inventory are debited.
Each delivered item is a new saved 730001, Plus=1, unbound, stack=1. The debit,
all item identities and claim receipt commit together. For example, 250 points
with two free slots yields two stones and preserves 50 points. One hundred
one-point retained fragments remain claimable within the bounded history batch.

Blessing contributions are stored in hours. Claiming 24 hours now adds 86,400
seconds, without crediting an upstream mentor. Claims deliver at most 65,535
hours per request and only whole hours that fit the uint remaining-time field.
The reward stays owed when no time fits. Active and departed apprentices share
one claim transaction; lifetime contributions and the other reward types remain
unchanged. Larger durable balances are retained; only native ushort views cap.

Both claim services read authoritative ownership and balances under the current
character owner lock, the existing contribution lock and database row locks.
Client-supplied mentor IDs or displayed totals grant no authority. Duplicate
historical memberships, stale inventory/preimages, failed writes and silently
skipped delivery writes cannot create partial claims. Failure fences the session
for authoritative reload. Each successful claim has a separate durable receipt.

## Blessing persistence prerequisite

`BlessingBalance` tracks the saved preimage separately from pending online
countdown ticks. Grants and character saves compare it under ownership; a stale
session cannot overwrite a newer durable duration. Grants now commit the duration
and existing mentor contribution delta together. Rapid grants no longer depend
on a five-second snapshot save. Periodic mentor snapshot saving is removed;
the old `SaveApprenticeInfo` compatibility method is intentionally a no-op.
EXP, stone and blessing writers own their individual transactions instead.

Countdown ticks use the character lock, expiry clears its flag, and normal
character saves persist the remaining seconds. This preserves the existing
online-only countdown; it does not silently introduce offline expiration.

## Evidence and remaining work

The [official mentor guide](https://co.99.com/guide/guides/mentors.shtml) confirms
100 composing points per +1 Stone, dropped +1 items as the composing source,
and Praying Stones as the blessing source. Existing local code establishes the
hours/seconds mismatch and item grant properties. See also the dated
[protocol record](PROTOCOL_RESEARCH.md).

This batch preserves the existing Revora whole-hour contribution ratio and
existing AddBless grant sources. The guide does not prove that ratio. Dedicated
Praying Stone consumption and dropped-equipment earning transactions remain
open; this is not a claim that the complete mentor earning lifecycle is restored.
No retroactive achievement or contribution is fabricated. The historical
49 duplicate apprentice IDs require a separate evidence-preserving remediation.

## Validation

Targeted checks cover ordinary players, exact item/time delivery, retained
balances, capacity, concurrent/repeated claims, stale sessions, rollback at each
transaction boundary, immediate cache projection, reload, wide native views and
countdown saves. The +1 suite has 29 checks and the blessing suite has 39.
The final isolated build passes all 36 selected suites: 2585 checks, plus 16
producer/overlay/silver audit checks (2601 total). Logs are under ignored
`obj/chi-refill-validation/mentor-rewards-final-*.log`. Final `Revora.exe` SHA-256:
`37EEE5823EAC07D2920011DD7A3C42A0BA7031421E949294A5FE81B7DC8A2A86`. All database writes use disposable fixture schemas.
No live binary, production schema, client content or public patch is changed.

M6.36 subsequently binds mentor blessing credit to atomic Praying Stone use and
excludes generic event/quest grants; see [Praying Stone earning](PRAYING_STONE_EARNING.md).
Dropped-equipment contribution, exact original share evidence and native acceptance
remain open. The M6.35 validation totals above describe that historical checkpoint.
