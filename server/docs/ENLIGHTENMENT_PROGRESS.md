# Enlightenment progress

2026-10-02. Admission, durable interaction, daily reset and deferred XP are
implemented and privately checked. Milestone606 is enabled from a committed donor
receipt, with usable bound rewards and ordinary-player cold persistence checked.
EXP Ball/potion and DragonBall-for-XP point credits are atomic. Not deployed live.

## Rules and evidence

The [official guide](https://co.99.com/guide/guides/enlightenment.shtml), checked
2026-10-02, requires donor level90 and a recipient at least20 levels lower,
limits receipt to five times daily, resets unused donor points at midnight,
and describes conversion every20 online minutes. It does not publish the exact
XP amount or the wire layout.

Revora uses these explicit custom choices:

- Maximum interaction distance18, same canonical map, no trade or booth.
- Immediate XP is one tenth of the recipient's current EXP Ball value. A second
  award of that same captured amount becomes payable after20 counted minutes.
  Later level changes do not recalculate the saved deferred amount.
- Deferred awards run sequentially, with at most100 remaining minutes queued.
  Tick credits at most one minute per call; disconnected time is not credited.
  Timer eligibility requires a canonical, alive, loaded session which can
  receive XP (no curse or configured training-ground XP block). Long stalls or
  reconnects may delay payout; sub-minute progress is not persisted.
- The first award starts its timer at acceptance, not at earlier login time.
- Replay identity is donor/recipient/server-local calendar day/request value
  from bytes4-7. An exact repeat in that day cannot charge or award twice.
  This is not proof of native nonce semantics or cross-day replay freshness.
- Legacy positive waits up to100 minutes migrate once into saved entitlements,
  preserving exact remaining minutes. Partial first blocks use the same tenth
  of the recipient's EXP Ball at migration; historical payouts are not inferred.
  Larger invalid waits fail closed for investigation.

## Implemented protections

- [x] Validate managed1127 frame:40 bytes including seal, declared length32,
  packet identity and connection-owned donor before mutation. Native acceptance
  remains unverified.
- [x] Reject fake, fenced, dead, remote, self-target and insufficient-level
  interactions. Take both owner locks in UID order using nonblocking acquisition,
  recheck canonical sessions and hold session leases through commit/installation.
- [x] Save donor debit, recipient daily count, immediate XP/progression side
  effects, deferred entitlement and interaction receipt in one transaction.
- [x] Persist each countdown decrement; mark an entitlement paid in the same
  transaction as its XP. Failures fence sessions; stale saves cannot restore
  spent points or unpaid timers after a committed operation.
- [x] Use a durable local-calendar-day record for online/login resets. Daily
  base/nobility/VIP grants and recipient count reset together; queued awards stay.
  Remove independent EntityTable reset and both login timer-rounding writers.
- [x] Preserve separate nullable saved balance images and parameterized generic
  save guards. A runtime change after save capture remains pending.

## Private validation

`tools/CheckCandidateEnlightProgress.cs` uses ordinary non-GM supplied characters
and the production1127 handler. Controlled clocks advance time without sleeping.
Every reload phase restarts both game and the private PostgreSQL process.

- [x] Accepted interaction debits100 units, records one receipt and immediately
  grants XP; exact replay changes nothing.
- [x] Deferred XP stays unchanged at minute19, pays at minute20 and does not
  repeat on a duplicate tick. Exact profiles/balances survive cold restart.
- [x] Eight concurrent copies charge once; five stacked receipts are accepted,
  the sixth is refused, and the queue retains99 minutes after its first tick.
- [x] Time before the first award is excluded. Reconnect preserves99 exactly,
  excludes two offline hours, and pays the first queued award after its remaining
 19 minutes. Four additional entitlements remain pending across cold restart.
- [x] Acceptance rollback leaves both saved profiles/balances and receipts
  unchanged. Interrupted post-commit installation fences both sessions; reload
  recovers the committed debit/XP/queue and exact replay remains harmless.
- [x] Interrupted payout installation cannot restore the unpaid timer through a
  stale save. Reload shows the one committed payout and no duplicate XP.
- [x] Online midnight replaces donor points with the daily grant, resets the
  recipient count and preserves the pending award. Repeated reset is harmless.
  A payout rollback preserves the final unpaid minute; reload and a later tick
  pay once. The midnight fixture initially assumed700 remaining donor units;
  its actual daily-initialized balance was400 after acceptance. The assertion
  now checks the actual before/after debit; the existing receipt was retained.
- [x] Recheck malformed/remote admission; the isolated balance-helper cold
  snapshot stays compatible. Its fresh-run expectation now preserves39 minutes
  instead of reproducing the retired login rounding.

Evidence builds:

- Initial interaction/payout/cold check:
  `9955E6E70271E9F925181536E5E864724882099E009859B1B14015BBF9165341`.
- Concurrent/limit/reconnect and interrupted-install checks:
  `FED521CE87638DCE204C0939D3BD41CD5CE6E3FFA57194E2FDC671B4108A7CDD`.
- Midnight/payout rollback and admission/cold regressions:
  `3EFFA1AD66A1B2E4FA7B7CC3A78C7B1C2385917F8706936B86159940D153BB2F`.

Final executable `E1AE9305402442AD12794FDBEEE79AA7F041CF247D633CED5FF2947FBB418621`
also preserves the legacy auto-hunt XP tally calculation. All three retained
interaction/day/edge cold snapshots plus the isolated balance cold snapshot
passed against that final build. Auto-hunt UI acceptance is not claimed.

The committed-source-overlay build excludes unrelated owner WIP; existing
legacy warnings remain. The composed process harness uses warnings as errors.
No private database or prior earning receipt was reset.

## Remaining work and release gates

- [x] Credit EXP Balls and EXP potions atomically with actual consumption.
- [x] Replace UnknownMan3825's legacy exchange with atomic payment, XP, daily
  admission and point credit. Ordinary price tiers, quote/refusal, rollback,
  interrupted installation and cold persistence pass. See
  [DragonBall exchange rules and evidence](DRAGONBALL_EXPERIENCE.md).
- [x] Check consumable-credit overflow, daily allowance boundaries and midnight
  failure/replay cases beyond the interaction reset coverage above.
- [x] Enable606 from a real donor receipt, then validate the three bound Meteors,
  ordinary claim/use, duplicate protection and cold persistence.
- [ ] Native1127 input/animation, visible timer, point/XP updates and reconnect
  acceptance. The managed serializer does not establish native compatibility.
- [ ] Live deployment after the separate owner/client gates.

Way of Heroes is now74/79 enabled; stage606 is the new substage.


## EXP item credits and milestone606 (2026-10-02)

EXP item services prepare the current daily balance before consumption, then
save10 internal units (0.1 point) in the same transaction as the owned item
consumption and XP/buff. A refused use does not grant points. Full-save preimage
guards and fencing cover failure between database commit and runtime install.
Supported EXP Balls retain their existing IDs723700/723911/722136/723744/722057;
EXP potions retain722700/723017. The ordinary interaction spends100 units.

Stage606 checks a non-legacy donor receipt, not a request or the recipient's XP.
The installed definition is task31, one successful enlightenment, opening1101,
three bound Meteors1088001. Failed interactions cannot qualify it.

`tools/CheckCandidateEnlightClaim.cs` passed earn and cold-reload phases on
`465F0683E57CABBF5B9E5FF22A49D2B3AA60CC8941590553082EFB9D4E0C7FE7`:

- Supplied ordinary donor level120/first rebirth and recipient level90, with
  the donor's spendable points set to zero as a fixture prerequisite. Consumable
  inputs and upgrade equipment were supplied; no points or receipt were granted
  to satisfy the actual earning assertion.
- A bound hour potion and nine bound EXP Balls earn exactly100 units. An active
  bonus refuses a second potion. Each consumed UID replay grants nothing.
- Claims refuse both before point earning and after earning points but before
  a real enlightenment. Successful donation spends100 and qualifies606.
- Eight concurrent claims produce exactly three bound, single-stack Meteors.
  All three perform normal equipment-level upgrades under a deterministic
  success roll; upgrade and claim replays preserve state.
- Recipient immediate and20-minute deferred XP work with auto-hunt tally enabled.
  The managed tally increments for both awards; native UI display is not claimed.
- Injected credit rollback preserves the item, XP and points. A potion credit
  committed before interrupted runtime installation survives reload; a stale
  save is refused and spent-UID replay cannot add more points or buff time.
- Exact cold snapshots include the actual saved experience, level, rebirth,
  EXP Ball allowance, potion timer, item/currency/claim state and award/potion
  receipts. This expands the earlier balance-only helper's cold snapshot scope;
  that older helper did not compare the experience column.

The build retains existing legacy warnings; the composed harness compiles with
warnings as errors. No live deployment or native acceptance is claimed.


## Credit boundary completion (2026-10-02)

The additional ordinary-character process fixtures in CheckCandidateEnlightClaim.cs
passed without changing production point-credit code:

- `career-enlight-credit-edges`:10 actual EXP Balls credit100 internal units;
  the11th leaves its item/XP/points untouched. A failed next-day use rolls back
  allowance and consumption; reload and retry use exactly one new-day allowance.
- Supplied balance65525 accepts10 units to reach65535. Another use cannot wrap
  the counter, consume its item or grant XP; the session is fenced and reload
  preserves the item and ceiling balance. This is a synthetic numeric boundary,
  not a claim that ordinary daily play can naturally reach that balance.
- `career-enlight-midnight`: the10-use allowance is exhausted at23:59:59. At
  exactly00:00, an injected consumption rollback preserves item/XP and the old
  allowance ledger while the independent daily point reset remains committed.
  Retry consumes one new-day allowance and adds10 points; spent-UID replay is inert.
- Both fixtures compare exact credit/allowance/profile/item snapshots after a
  real game/PostgreSQL restart. The midnight fixture initially expected the old
  runtime EXP Ball counter to remain10 after login. Legacy login may reset that
  display counter; the authoritative dated allowance ledger remained10. The
  corrected check verifies that ledger plus unchanged XP and retained item, then
  verifies the successful new-day counter1. Existing progress was retained.

Boundary earn/cold build:
`7C702031D84FEC8BC56D46CE689DBDD028A55294E1317499EC02B9DC3F675867`.
Both retained cold snapshots also passed after the Champion cap fixes on
`2BF0E12C7EBF9B4D61CE4B88CBF7E5071B40766D676673AF122622207C5D1969`.
Native/client and live gates above remain open.
