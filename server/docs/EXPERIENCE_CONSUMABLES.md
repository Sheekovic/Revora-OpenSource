# Atomic EXP Ball consumption

M6.56a, 2026-09-16. Baseline `8091bfd` completes recorded first-life recovery.
This milestone completes ordinary standard and power EXP Ball consumption.

## Rules and evidence

| Items | Preserved Revora reward | Limits | Mentor source rule |
| --- | --- | --- | --- |
| 723700, 723911, 722136 | Existing full-width `ExperienceMath.Ball` curve | Ten per server-local calendar day; level below 137 and configured cap | Bound item flag excludes credit |
| 723744, 722057 | `(LevelExperience / 100) * 10`, including existing integer rounding | Below configured cap; no standard-ball daily charge | Existing power-ball credit rule retained |

The baseline `ItemHandler` branches establish these local numerical rules.
The official [September 2025 Power EXP Ball notice](https://co.99.com/news/2025-09-01/treasure_shopping_mall__power_exp_ball_gets_price_cut_starting_sept__1.shtml)
corroborates a ten-percent reward, but is later than the target edition. It
is not proof of exact 6711 rounding, limits or mentor behavior. The ordinary
ball formula, level cutoff and daily limit remain Revora rules; this change
makes their transactions reliable without silently rebalancing them.
Intrinsic binding of historical item IDs remains a separate catalog question.

All five installed identities use the ordinary item-use route. The legacy
branches that granted EXP before removing the item are removed. No GM status,
account allowlist, new packet layout, NPC or client patch is required.

## Transaction and persistence

Under the inventory-owner and existing mentor-effect locks, the handler checks
canonical login ownership, loaded progression, selected inventory reference,
item location/binding/stack, lock/suspicion/wardrobe and expiry metadata. Dead,
cursed, capped, trading or incompletely loaded characters keep their item.
Training Ground EXP restrictions are respected before any charge.

A pure reward plan precedes mutations. One database transaction persists:

- The complete progression profile against its saved preimage.
- One standard-ball allowance, when applicable.
- One exact selected item unit, including legacy zero-stack singletons.
- Level skills, Arena admission and eligible mentor contribution.
- First-life or reincarnation recovery consumption, if triggered.

Failed or suppressed writes roll everything back and fence the session for a
production reload. The committed item removal/stack and progression use the
existing immediate client updates. The owner lock also serializes autosaves.
The standard item level limit applies to its use; if its reward restores the
character above that limit, remaining standard balls are preserved.

`revora_exp_ball_days` stores one authoritative row per character, with calendar
day and used count (0?10). First use imports today's persisted legacy
`entities.expballs` only when `lastresettime` belongs to that exact date.
Yesterday's counter starts a fresh day. Later uses trust the ledger, so a legacy
mirror reset or reconnect cannot refill an already consumed allowance. An
online character receives the next day's allowance without reconnecting;
backward clock movement cannot reopen an earlier day. The legacy field remains
a compatible mirror and is updated in the same transaction. Power balls do
not spend or reset the standard allowance. Startup creates the table
idempotently through the normal initialization path.

## Validation and remaining work

The focused disposable-database suite includes 392 first-rebirth prerequisite
checks and 64 EXP consumable checks: all five identities both bound and unbound;
production reconnect; exact selected stack/singleton debit; concurrency with
autosaves; legacy allowance import, online midnight and clock rollback;
malformed item preimages; restrictions and timed items; suppressed profile,
item, counter, skill, mentor and recovery writes; and real first-rite receipts
consumed through the EXP Ball entry point. Mentor relationships and inventory
supplies are fixture inputs; this does not claim a newly walked event journey.

The initializer and handlers are built locally. Genuine 6711 display acceptance
and deployment remain open. No live server restart, client patch or website
publication occurred. Compound EXP packs and daily quest reward bundles remain
under M6.56b; their surrounding transactions are not certified by this change.

Validation completed: Debug x86 build; focused 456, experience 452, progression
74, Demon Exterminators 44, Quiz Show 33, Power Arena 95, first-life recovery
442 and default 216 (1,812 gameplay checks), plus three achievement audits.
The producer inventory remains 219 present / 118 absent. Logs are retained
locally in `obj/chi-refill-validation/exp-items*.log`. Server SHA-256:
`C135583F2C051B9BEE84505321ADEDD7044EE8C741EEA3C515E94DBA08286512`.
