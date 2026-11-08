# Ordinary EXP potion transactions

Date: 2026-09-16. M6.56b2b2b. Server implementation; not deployed.

Ordinary players can use catalog potions 722700 (10 minutes) and 723017
(60 minutes) through the existing item-use path. One transaction consumes one
owned unit, saves the double-EXP duration and records the use. This repairs a
Magnolia reward prerequisite; Magnolia bonus issuance is still pending.

## Evidence and policy

Both loaded server and active 6711 catalogs identify these durations. The client
catalog was inspected read-only and its encryption round trip checked; SHA-256
`D49F0658BF01387B99C284DDEC81713838F2EAE3ADB8E63666E5C906F63CF87A`.
No client files changed. The [official FAQ](https://co.99.com/guide/faqs.shtml)
says blessing and potion EXP time cannot be stacked for a longer effect.
Revora preserves the potion whenever an EXP bonus is already active. This
explicit no-overwrite policy is not a claim about a captured native refresh
interaction. Kill EXP uses the existing configured double-EXP multiplier;
non-kill quest/consumable EXP does not gain that multiplier.

The catalog distinguishes tournament participation packs 720793/720981, which
award immediate EXP, and SuperExpPotion 728201, which advertises five-times kill
EXP for two hours. Their **executable** one/two-hour timer uses are repaired in
[tournament EXP packs](TOURNAMENT_EXP_PACKS.md) without adopting those catalog
readings. Combined RVP/EXP timer packs still remain outside this ordinary-potion
transaction.

## Ownership, persistence and packets

Only a healthy canonical ordinary session may use its exact owned, unlocked,
non-suspicious inventory item outside trade/booth. Warehouse, equipment,
wardrobe, expired or substituted sources cannot be consumed. Binding and
zero-stack historical singletons are preserved. The existing 1009 action-4
frame is bounded to 32..128 total bytes with checked length, packet ID, action
and seal; no new wire family is introduced.

Under the inventory owner lock, the transaction locks the character, checks
the saved timer preimage and complete bag UID set, consumes one source unit,
writes the timer and inserts a `revora_exp_potion_uses` receipt. All required
writes check affected rows. Cache/item/timer updates follow commit. Failed
writes roll back together and require reload before retrying.

`DoubleExperienceBalance` keeps the durable timer preimage separate from the
pending countdown. Countdown, autosave and grants share the owner lock; stale
grants or saves cannot overwrite a newer durable bonus. Login refresh sends
the existing timer update without extending or writing the timer. Concurrent
ticks at the same timestamp decrement once. The existing online-countdown,
offline-pause model remains: normal saves checkpoint elapsed seconds, while a
hard crash can restore seconds elapsed since the last checkpoint. Continuous
wall-clock durability is not claimed.

## Validation and remaining gates

The isolated Debug x86 build passed. Ordinary potion tests passed 472 checks
(including 392 first-rebirth fixture prerequisites); regressions passed 552
Magnolia, 452 experience and 490 gift-pack checks: 1,966 gameplay checks total.
Three achievement audits passed; the producer inventory remains 219 present
and 118 missing, which is not proof of a per-character achievement route.

Coverage includes both IDs/bindings, legacy/single/stacked sources, concurrent
use/save/countdown, actual kill and non-kill EXP awards, expiry, production
reload, full bags, nullable historical timers, stale durable ownership and
timer preimages, suppressed source/stack/timer/receipt writes, ineligible
actors and malformed frames. Tests mutate only the disposable validation
schema. Ignored logs: `obj/chi-refill-validation/exp-potions-first.log`,
`exp-potions-magnolia.log`, `exp-potions-experience.log`, `exp-potions-gifts.log`.

Server SHA-256: `8997B6C2BCEF0B9F232F596B285651D8ECC64E76E8377C43F056EABAC7BD203E`.
Harness SHA-256: `D83575845C1300396764E6ED88BDCA1547803E0E56C4A844C845FDF0BC79F405`.

Genuine 6711 timer display, expiry/reconnect acceptance and live activation
remain open. No server restart, live migration, client patch or publication
occurred. Preserve use receipts and current timer/item state on rollback.
