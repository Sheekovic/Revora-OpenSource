# P6 soul shop

Owner request, 2026-09-22: native NPC shop on map 1002 at (401,339),
with accessible regular-CP pricing.

Implemented shop UID 91010, mesh 46090, name P6SoulMerchant. It stocks all 27
P6 souls shared by the loaded server and current 6711 client catalog, at
2,500 CPs each, following the owner's revised price. No P7 souls are sold.
Items are unbound. This matches the owner's basic furniture price.

`P6SoulShop.Create` registers stock and the NPC during normal startup, validates
P6 catalog identities, detects an incompatible NPC UID, and is idempotent.
Prices use the shared server item catalog, so any other CP shop listing these
same souls also sees 2,500 CPs. Purchases use the existing transactional native
progression-shop path: range, balance, quantity, inventory and stock validation;
CPs and items commit together before immediate player updates. No GM required.

The revised 2,500-CP price builds successfully and the matching client archive
was regenerated and hash-verified. All 77 purchase checks pass at the revised
price, including 67,500 CPs for all 27 souls, rejection with only 2,499 CPs,
remote rejection with sufficient funds, persistence, immediate balances and
rollback/concurrency. An initial database timeout prevented the first run;
after PostgreSQL responded again, the rerun completed and removed its disposable
validation schema. No database restart or configuration change was performed.
Current logs: `obj/chi-refill-validation/p6-shop-build.log` and `p6-shop-tests.log`.

## Prepared client data

`python tools/ClientPatches/prepare_p6_shop.py` stages the existing client's
itemtype.dat, Shop.dat and npc.ini changes under
`obj/chi-refill-validation/p6-shop`. Exact originals, per-file SHA-256 values,
a manifest and `p6-shop-client.zip` are preserved outside Git. Encryption is
round-trip verified; all 27 client rows must be P6 and match the source stock.
It reuses the existing Storekeeper appearance with a distinct shop label.

The native client's known positive silver-price requirement is retained:
carry one silver per requested unit; the server charges only regular CPs.
Native purchase acceptance remains pending by the owner's existing decision.

This batch is built and staged, not deployed or publicly released. The server
needs the updated build and players need the three matching client files.
No live restart, donor modification or public download mutation was performed.
Release should use the established approved patch publication process; this
private ZIP is not a signed public launcher manifest. Rollback restores the
previous server build and the three exact backed-up client originals.
