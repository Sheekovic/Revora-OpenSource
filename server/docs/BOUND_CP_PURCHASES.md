# Bound CP purchases

Owner request, 2026-09-25: items purchased with bound CPs must remain bound,
including item rewards opened from purchased packs.

`CpShopPurchases` replaces the general shop's CP branches. Bound payment
(`dwExtraInfo3=2`) and explicitly bound-CP shops create bound items; ordinary CP
payment creates unbound items. Existing specialized exemption-token and cosmetic
shops already preserve the payment binding and retain their own handlers.

The general path validates the owned session, frame, configured stock, positive
price, quantity, currency, NPC distance, durable balance and inventory. It plans
the entire purchase before payment. Only matching bound state, item type and
plain enhancement metadata can share a stack. Full inventory can accept a
purchase that fits existing compatible stacks. A transaction updates stacks,
inserts items, debits the selected currency and records a purchase receipt.
Identical recent purchases are suppressed for one second, matching existing shop
retry handling. Session failures after commit require reload rather than a
second charge or grant. There is no purchase nonce in the reused request.

The previous CP branch explicitly cleared binding and is removed. The inventory
`Add(id,plus,times,bound)` helper now applies its bound argument. Auto-stacking,
ordinary shop stacking and manual merges cannot mix bound and unbound items;
manual splits copy source binding.

Purchased SpeedArrow, UltraStone, ToughDrill and class Super-equipment packs now
copy their source binding to all item outputs. SmallLotteryTicketPack 724002 uses
the transactional fixed-output service and grants three tickets with inherited
binding. LotteryDraws already retains binding when spending bound tickets.
FixedOutput, Starter, DailySignIn, EasterGold, PrizeCenter, GainRefinery and
HossuHair pack handlers also preserve a bound source instead of explicitly
clearing it. Existing forced-bound rewards stay bound.

This change concerns item purchases and item outputs. It does not change prices,
lottery odds, monster drop rules or currency rewards, nor does it retroactively
reclassify existing unbound inventory of unknown provenance.

Validation uses ordinary non-GM fixtures and a private schema/lease, with mixed
stacks, both CP currencies, bound-only shops, full inventory, malformed requests,
invalid quantities, stale balances, concurrent duplicate buys, pre/postcommit
failures, production reload, purchased pack opening and manual split/merge tests.
Build/test logs are `obj/chi-refill-validation/bound-cp-build.log` and
`obj/chi-refill-validation/bound-cp-tests.log`. The build passed and the combined
starter/rebirth/purchase suite passed 942 checks. Source-only; no running server or
live player records were modified.

## Reviewed source integration (2026-10-04)

The previously pending implementation is now included in the source release
batch. The private `obj/bound-reviewed/bin/Revora.exe` build uses the staged
purchase changes and committed versions of unrelated modified server files.
Its SHA256 is
`828E280BB5358CFFF8BEF4E4B0D2C5B5F048E83A87F752F5329D18C598190B22`.
The existing starter/purchase suite passes959 checks against this build,
including ordinary sessions, exact payment, binding, concurrent requests,
pre/postcommit failure recovery, purchased-pack opening, stack operations and
the specialized subclass/Inner Power/P6 shop precedence regression. This is
not a claim that every pack family or native client interaction was retested.
Logs: `obj/bound-reviewed-build.log` and `obj/bound-reviewed-packs.log`.
The temporary database was stopped; the live game was not deployed or restarted.

The shared general shop resolves garment shop502 to merchant6002. Specialized
progression shops remain regular-CP-only and retain their own purchase service;
they are dispatched before the general path. No new packet layout is inferred.
