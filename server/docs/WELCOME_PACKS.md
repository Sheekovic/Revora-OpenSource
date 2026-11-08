# New-player welcome pack chain

> Retired from new-character grants on 2026-09-25 by owner request. New
> characters start at 1002 (327,303) with a pending RevoraStarterPack entitlement.
> The description below documents the legacy chain for already-owned items;
> it no longer describes new-character delivery. See REVORA_STARTER_PACK.md.

M6.56b7, 2026-09-22. Server implementation; not deployed.

Ordinary new characters receive pack **723753** from the `JustCreated` login
path in `GeneralData`, beside the separate **727026** chain. That second chain
is not merged here.

Use of 723753 → 723755 → 723767 → 723772 → 723774 → 723776 is ordinary
item-use 1009/action-4. One transaction consumes one owned unit, inserts the
existing outputs, persists any CP or silver award, and writes a
`revora_welcome_pack_uses` receipt.

## Executable gates and rewards

| Pack | Gate | Currency | Items (existing handler terms) |
| --- | --- | --- | --- |
| 723753 | level ≥ 1 | +10 CP | 3×1000000, 3×1001000, successor 723755 |
| 723755 | level ≥ 10 | none | 10×723700 EXP Ball, 3×723017 potion, successor 723767 |
| 723767 | level ≥ 70 | +20 CP | 5×723017, 1×1080001, successor 723772 |
| 723772 | level ≥ 100 | +1,000,000 CP | 10×723017, successor 723774, class gem 800010/800410/800610/800010/800010/800508 |
| 723774 | level ≥ 110 | +30 CP | successor 723776, Moon Box 721090, 10×723017, class gear, then extra 120269+8 and 150269+8 |
| 723776 | level ≥ 140 and rebirth 2 | +5,000,000 silver | 5× bound 3009003 |

Level-110 duplicate necklace/ring after every class branch is preserved.
Trojan and Ninja weapons stay bound plus-8; other class gear stays unbound
plus-8 or plus-0 as in the live handler. Inventory.Add still creates one bag
slot per unit (no stacking). Exact output capacity credits the source slot
only when the source is deleted.

## Persistence

Under the inventory owner lock the service checks the live source, complete
durable bag UID set and restrictions. CP uses `entities.conquerpoints`
compare-and-swap. Silver uses `SilverBalance`. Failed writes roll back
together. Direct `ItemHandler.UseItem` shares the service. The six legacy
cases are fail-closed. 727026 is owned by [StarterPacks](STARTER_PACKS.md)
and is not merged here.

Downstream 723017 / 723700 remain the existing EXP potion and EXP Ball
services. 721090 remains the Moon Box identity.

## Remaining work

Native-client acceptance and deployment remain open. The separate 727026
chain through terminal 727050 is [STARTER_PACKS.md](STARTER_PACKS.md).
Parent [M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.
