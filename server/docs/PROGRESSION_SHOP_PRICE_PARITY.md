# Progression shop economy parity

Owner decision, 2026-09-20: client prices must match the server economy.

## Corrected evidence

The previous audit incorrectly used `database/itemtype.txt`, which is not the
loaded base catalog. `ServerBase/Constants.cs` points `ItemBaseInfosPath` at
`database/itemtype-new.txt`; `ConquerItemInformation.Load` reads that path.
All 28 progression-shop prices in the loaded catalog already match the client.
Lucky Amulet is **5 CPs**, Heavenly Stone is **30 CPs**. The earlier claims of
75/150 CPs and 24 unavailable stocked items were incorrect for this server.
No installed catalog was changed, and the erroneous staged patch was not released.
Its ignored manifest is explicitly invalidated and must never be deployed.

The corrected `tools/ClientPatches/prepare_economy_prices.py` verifies the source
catalog path, exact catalog/client/executable hashes, both shop stock lists, and
all 28 prices. Output is isolated under `obj/chi-refill-validation/economy-prices-active-catalog`.
The change list is empty and the output is byte-identical to the original client.
It preserves all non-price fields, unrelated rows, and line endings, with full
encryption round-trip validation. No new price patch is required.

Loaded server catalog SHA-256:
`1ed7a23b7a34b5e3706277f97e421e3cdf0b269eeb5be7367a4ebc0e4f22b3ef`.
Client preimage and unchanged output:
`d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a`.

## Purchase validation

The subclass fixture now calls the real item and shop loaders without replacing
prices or stock lists. Seventy checks pass against the existing isolated build:
all eleven subclass purchases cost 465 CPs, all seventeen Inner Power books cost
3,600 CPs, and saved currency/item quantities agree with runtime state. It also
covers quantity limits, bound-CP refusal, range/trade restrictions, concurrent
spending, study-book consumption, Vesper rune purchases, and database rollback.
The disposable schema was removed. Test currency is fixture-provided; this does
not prove a complete CP-earning journey, native clicks, or a running Host's state.

`python tools/audit_level5_housing.py --require-price-parity` now passes against
the loaded catalog: twenty Lucky Amulets cost 100 CPs. The previous shop fixture's
price overrides are removed, preventing them from masking later catalog drift.

Genuine-client purchase acceptance and the original one-silver-per-unit native
buy workaround remain separate gates. No server deployment or client release
occurred in this correction.
