# UnknownMan DragonBall-for-XP exchange

Implemented and privately validated2026-10-02. Not deployed live.

UnknownMan3825 is loaded at Market1036 (212,204), confirmed in the private
catalog and the [official Market NPC list](https://co.99.com/guide/npcs/marketnpc.shtml).
The [official enlightenment guide](https://co.99.com/guide/guides/enlightenment.shtml)
lists0.5 Enlighten Point when a DragonBall is used for experience. Revora grants
50 internal units per consumed DragonBall;100 internal units fund one donation.

## Retained Revora economy

| Level before exchange | DragonBalls | Enlighten Points |
| --- | ---: | ---: |
| 1-109 | 1 | 0.5 |
| 110-129 | 2 | 1 |
| 130-134 | 10 | 5 |
| 135+ or configured level cap | Refused | 0 |

One successful exchange per server-local calendar date. Existing Revora XP
amounts are preserved, including all29 explicit amounts for101-129 and the
lower-level bands. The130-134 fallback is13,901,336 XP for10 DragonBalls; this is
unusually low compared with129's322,122,547 XP and is not claimed authentic.
Its economic adjustment remains a separate documented review, not a silent
change bundled with transaction safety. The NPC displays the exact price/XP.

## Transaction and access

- Every eligible ordinary player can use the existing NPC. No GM/name allowlist.
  Require canonical, alive, loaded, uncursed session, proximity within6 tiles,
  the Market map, no trade/booth and an available XP profile.
- Option0 quotes the current level-based price and XP. Option1 requires that
  session's quote, unchanged level/day and age at most2 minutes. A forged option,
  expired quote or remote NPC cannot consume materials.
- Prefer bound DragonBalls, then ascending UID. Accept normal owned permanent
  unlocked, non-suspicious bag items; support partial stacks and legacy zero
  stacks as one item. Scrolls are not automatically unpacked or substituted.
- Lock the owner and session through payment/installation. An owner-row lock
  serializes daily admission with the unique(uid,day) receipt. Check the legacy
  saved last-use date as well, preserving earlier same-day usage. Compare the
  full calendar date, not DayOfYear; prior-year usage does not block today.
- One transaction consumes the exact owned stack preimages, persists XP/profile
  and progression effects, writes the item/count receipt and last-use timestamp,
  and credits enlightenment points. Runtime installation occurs after commit.
  Failures fence the session; reload recovers committed state without restoring
  spent items or granting a duplicate exchange.

## Private evidence

`tools/CheckCandidateDragonExperience.cs` uses seven ordinary supplied characters,
real Market placement and the normal NPC dialog dispatcher. Input DragonBalls and
levels100/120/130/135 were fixture prerequisites; no XP or exchange receipt was
injected to satisfy earning.

- [x] Prices1/2/10 each consume the expected materials and grant the corresponding
 50/100/500 internal points plus actual XP.
- [x] Eight concurrent confirmations charge once at each price. Same-day repeats
  retain their extra item. Direct, remote and expired quote refusals preserve
  scoped item/XP/currency/receipt state.
- [x] Level135 and insufficient materials refuse without payment or reward.
- [x] Injected rollback preserves items, XP, daily eligibility and points.
- [x] Post-commit install interruption fences a stale save; reload restores the
  committed two-item payment, XP and100 points without restoring the materials.
- [x] A previous-year last-use timestamp allows today's exchange; a three-item
  stack becomes two after a one-ball purchase.
- [x] A real game/PostgreSQL restart preserves exact saved XP, level, currencies,
  items, point balances, last-use timestamps and receipts. Replay remains harmless.

Build SHA-256: `7C702031D84FEC8BC56D46CE689DBDD028A55294E1317499EC02B9DC3F675867`.
Logs: `arena-process-career-dragon-experience-{earn,reload}.log`.
The committed-source-overlay build excludes unrelated owner WIP and retains
existing legacy warnings. The composed harness compiles with warnings as errors.

Remaining: native dialog/XP/point-display acceptance and live deployment.
The subsequent credit boundary batch covers overflow, daily allowance and
midnight failure/replay; see ENLIGHTENMENT_PROGRESS.md for that separate evidence.
