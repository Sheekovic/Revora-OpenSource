# Owned housing construction and travel

2026-09-20, baseline `eefb4ab`. The five certificate-funded construction levels
and existing CP certificate shop now have owned transactions and ordinary-player
server tests. This is part of M6.64b, not completion of the full housing system.

## Player service

Installed House Admin 30156 at Twin City 331,536 provides a short menu for
construction, upgrades, entry and certificate purchases. Each paid action has a
two-minute session offer and a separate confirmation. Consuming that offer
prevents concurrent confirmations from buying or building twice. Reconnecting
does not preserve an unconfirmed offer. Admission requires the canonical live
character, proximity, saved Twin City location and no trade, booth or poker table.
No GM status or character/account allowlist is required.

Certificate purchases preserve the existing configured CP prices. At inspection
these are 8,000 / 4,000 / 6,000 / 8,000 / 10,000 CPs for levels 1-5. They are
Revora prices, not independently verified original-shop prices. Exact balances
are accepted. The item and CP deduction commit together; a full bag or failed
item write cannot consume CPs. These prices are separate from construction.

Construction consumes the appropriate owned certificate plus 300,000 / 500,000 /
2,000,000 / 5,000,000 / zero silver, respectively. These certificate-route costs
come from the [published housing guide](https://co.99.com/guide/guides/house_change.shtml)
and the item-identity audit in [housing research](HOUSING_EARNING_RESEARCH.md).
Levels must be built in order. Payment, saved level, map reservation, construction
receipt and the corresponding 10102/10103/10104/10105/10122 achievement commit in
one transaction. Old house rows alone do not backfill construction achievements.

The owner must empty the house before renovation. Occupants or stored furniture
prevent the upgrade; the service never deletes furniture to make space. The
existing immediate Revora renovation behavior is retained. It does not claim the
original maintenance-delayed timing. Gem alternatives, installments and mined
voucher acquisition remain separate unfinished routes.

## Ownership, travel and recovery

`HouseMapRegistry` assigns persistent IDs in 60000-65534, preserving the same map
identity through all five levels and reconnects. It no longer derives identity
from the owner's truncated character ID. Runtime maps are published from saved
ownership after payment commits. If publication fails, the committed home remains
recoverable through reload; the player is fenced from further mutations.

The login loader reconstructs saved houses before generic invalid-map recovery.
The former blanket rule returning IDs above 7008 to Twin City now preserves only
a validated owned house. A character saved inside another owner's house returns
to Twin City. Invalid saved house coordinates use a legal landing. Entry checks
both runtime arrival and the saved map ID; silently suppressed arrival fences the
session. Existing player teleport overloads enforce owner-only house entry.
Guest/spouse admission is not implemented by this batch.

The installed client and server indexes both register:

| Level | Terrain | Index asset |
| --- | --- | --- |
| 1 | 1098 | house01.7z |
| 2 | 1099 | house03.7z |
| 3 | 2080 | Nhouse03.7z |
| 4 | 1765 | Nhouse04.7z |
| 5 | 3024 | family.7z |

Terrain hashes are recorded in the research document. Existing server portal
coordinates are retained and tested by walking to each exit. Native visuals,
collision agreement and map-link behavior still need genuine-client review.

The read-only baseline found zero production house rows. A future valid legacy
house without furniture can acquire its durable identity without granting an
unearned construction receipt. Invalid owner/level/terrain records and ambiguous
legacy furniture stop for reconciliation. Furniture is not reassigned based on a
name or truncated map ID. This is not a completed historical-house migration audit.

## Validation and remaining work

The isolated build, 68 housing checks, 203 Adventure regression checks and three
producer-audit tests pass. The final housing checks use copied production
house/furniture schemas, ordinary non-GM characters, the installed House Admin,
all five actual terrain floors, real NPC dispatch, accepted walking and production
character/item/title reload. They cover concurrent confirmations, exact funds,
full bags, insufficient silver, trading, foreign entry/login, runtime-map eviction,
all construction levels, real exits and failed item/house/receipt/achievement writes.
Renovation failure preserves the old runtime house; retry charges once. The funds
are fixture starting balances, not proof of a newly earned gold/CP hunting journey.

The storage participant's earlier 14 checks separately cover concurrent map
reservations and recovery in a fresh process. The new journey suite does not
claim a full Host restart or a rendered native playthrough.

Source startup and NPC/login callers are connected. No production build was
deployed, no service restarted and no live schema or player data changed.
Remaining M6.64b work: full installment/gem earning integration,
owned furniture placement/removal and item boxes, guest permissions, original
timing/edition reconciliation, historical records and genuine-6711 acceptance.

The [material-route follow-up](HOUSING_MATERIAL_ROUTES.md) recovers the seven
original exchanges and restores their missing copper mining prerequisite.
All seven exchange handlers and the ordinary House Agent overlay are now connected
and tested. The complete two-level mined-material server journey passed 2,509
checks with exact payments; genuine-client acceptance remains open.

The [gem-route follow-up](GEM_HOUSING_ROUTES.md) connects all five published
gem/silver alternatives. Build and 217 housing checks pass, including an ordinary
mining-to-composition-to-first-house route and saved payments/achievements for all
five levels. Higher-level gem quantities are fixture inputs, not a claim of
complete mined-gem earning acceptance. The older 68-check count above records
the certificate-service checkpoint, not the current expanded suite.

The [installment-service follow-up](HOUSING_INSTALLMENTS.md) connects the display
home and manager, partial deliveries and final level-5 construction. Build and
290 housing checks pass. Full earned-material and native acceptance remain open.


## Fresh earned five-level construction and cold entry (2026-10-03)

`CheckCandidateFreshHouse.cs` continues ordinary non-GM UID1096322 from the
completed Snow Banshee snapshot, with all three warehouses loaded. It purchases
and consumes each certificate through House Admin30156's normal offer/confirmation
flow, funding all five levels from the character's earned balances. The actual
certificate prices are8,000/4,000/6,000/8,000/10,000 CPs:36,000 total. Construction
also pays300,000/500,000/2,000,000/5,000,000/zero silver:7,800,000 total.

For every level, repeating the purchase confirmation produces no additional
item or CP charge; repeating construction consumes nothing further. The five
saved build/material records and achievements10102/10103/10104/10105/10122 exist.
The final owned map uses terrain3024 and retains its registered owner and map
identity. Ordinary entry, a legal arrival tile and the actual exit portal pass.

A separate game/database process verifies the full saved house, map, builds,
materials, balances, inventory and prior-career snapshot before entry, then enters
and leaves the same registered home and confirms exact state again. Both
`career-fresh-house-earn` and `career-fresh-house-reload` pass on the first runtime
attempt. `revora_fresh_house_probe` stores the final checkpoint. The post-Snow
receipt export predates these five additional earned identities.

Candidate SHA-256 is unchanged:
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
NPC/portal approach is controlled. This proves the paid certificate construction
route and server-side entry/recovery, not gem/installment routes, native housing
rendering or live deployment. No production source changed in this batch.
