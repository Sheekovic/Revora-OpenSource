# Level-5 house installments

2026-09-20, source baseline `521748f`. The source now connects the complete
partial-delivery service and final construction. This is server implementation
acceptance, not a deployment or a complete earned-material/native playthrough.
Published totals and local asset evidence are in the
[research ledger](HOUSING_INSTALLMENT_RESEARCH.md).

## Player journey

Follow-up validation: 310 housing checks now pass. The Lucky Amulet installment
is exercised through the installed SubclassSupplies actor, actual item/stock
loaders and ordinary purchase handler. A level-four owner walks to the shop,
buys ten amulets for 50 CPs, reconnects, walks back to House Admin, visits the
showroom and delivers the purchased stack. Repeating that journey supplies all
twenty amulets for exactly 100 CPs. Item UIDs connect the purchased stacks to
saved installment receipts; purchase and delivery reloads preserve balances and
progress. Unaffordable orders and repeated delivery confirmations consume nothing.
This uses fixture starting CPs and server movement; it does not certify a CP
earning route or native client presentation. The other four material categories
remain fixture inputs in the complete installment-construction test.

An ordinary level-4 owner asks House Admin in Twin City to visit the display
home. Level-5 owners may also visit. Senior House Manager, NPC 91017, stands
at 101,102 on showroom map 3024. Arrival is 90,70. The service uses the installed
class-5 terrain and inferred client mesh 12970. The showroom has no owner and is
distinct from every player's private house ID. Its startup overlay verifies
the terrain hash, catalog conflicts, runtime ownership, landing and actor cells.
It is idempotent and does not rewrite the existing NPC/map catalog tables.

The manager reports submitted quantities and accepts partial deliveries toward:

- 5 Dragon Balls;
- 20 Lucky Amulets;
- 40 Euxenite Ores;
- 20 Meteor Scrolls;
- 30 standard super gems.

Players may deliver categories in any order. A short quote shows exactly how
many carried materials will be taken; confirmation is required. Delivery stops
at the outstanding total, leaving excess inventory untouched. Bound materials
are chosen first with disclosure. Eligible stacks may be split, but stack sizes
must respect the installed catalog; zero-stack legacy singletons count as one.
Locked, stored, equipped, temporary and suspicious items are excluded. Super
Phoenix, Dragon, Fury, Rainbow, Kylin, Violet and Moon gems are accepted.
The seven-family restriction is an explicit conservative Revora policy; the
guide's wording does not independently prove every edition rejects the other
three families.

The last delivery does not automatically award a house. The owner confirms the
building work separately after clearing furniture and occupants. Completion
preserves the private map identity, upgrades the house to level 5 and grants the
existing construction achievement. There is no additional silver or CP installment
fee. The manager can return players to Twin City, including after completion.

## Persistence and ownership

`revora_house_installments` stores five bounded counters and completion status.
Every delivery has exact item UID/type/binding/stack/quantity receipts in
`revora_house_installment_items`. A two-minute session offer freezes preimages
and is consumed once. Under the canonical inventory lock and saved owner lock,
material debits, receipts and counters commit in one transaction. Runtime bag
changes are sent only after commit. Missing or suppressed writes roll back and
require reload; reconnect does not restore an unconfirmed offer. Farewell clears
the offer, while delivered materials remain recorded.

Final confirmation compares all five counters against the sums of durable
material receipts. It commits the house update, build receipt, achievement and
completion marker together. The build receipt's representative material UID
references the installment history; complete quantities remain in the installment
receipt table rather than being fabricated as currently held inventory.
Failed completion retains the entire paid contract and the old house for retry.
Once any installment has been paid, House Admin refuses an alternate level-5
construction route so it cannot strand the deposits. An unexpected externally
changed house level stops further mutation and retains history for reconciliation.
There is no automatic refund or guessed migration of externally altered records.

## Validation and remaining gates

The isolated production build and `Verify-PotencyHunt.ps1 -HousingOnly` pass
**290 checks**. The expanded suite retains certificate, ore-exchange, scroll,
gem-construction and mining/composition coverage, and adds:

- ordinary display-home entry, walking to the manager, persisted arrival,
  reconnect, return and separation from the private home; production construction
  of an initially absent showroom and repeat initialization;
- out-of-order payments, bound stack splitting, a 17/23-ore split across reload,
  exact caps, retained excess items and concurrent confirmation suppression;
- no premature achievement, furniture protection, alternate-route protection,
  final exact construction, reconnect and private-home entry;
- invalid ownership/context/materials, expired and cancelled offers;
- suppressed first contract insert, item delete/update, receipt and progress
  writes, plus each final house/build/achievement/completion participant, with
  production reload and one successful retry;
- refusal to complete when counters have no matching material history.

Those tests construct the level-4 prerequisite through ordinary paid certificate
services. Installment materials and starting currency are fixture inputs. That
earlier run does **not** certify the full 115-material earning journey. The
subsequent complete material route is recorded below. The corrected loaded-catalog
audit confirms Lucky Amulet costs 5 CPs in both server and client; no price change
is needed. See [pricing correction](PROGRESSION_SHOP_PRICE_PARITY.md).

The read-only production-catalog check found no conflict for NPC 91017 or map
3024. It is not proof of a deployed actor. The actor and schema are connected
to source startup, but no live schema, service or client file was changed.
Native NPC appearance, dialog presentation, showroom stairs/visuals and the full
rendered journey remain acceptance gates. Furniture and item-box source services
are recorded separately; native acceptance and guest permissions remain open.

## Complete material-earning validation

The [combined earning journey](HOUSE_INSTALLMENT_EARNING.md) now passes 533 checks.
One ordinary character earns all five Dragon Balls, twenty Meteor Scrolls and
thirty eligible Super Gems through daily Magnolia planting, buys twenty Lucky
Amulets for 100 CPs and mines forty Euxenite Ores with a purchased pickaxe. Every
material is delivered through the manager; final construction, private entry
and reconnect preserve the level-five home and achievement. All garden gifts
join back to their earning receipts, and saved/runtime currency and Study totals
agree. Starting currency/prerequisites and controlled randomness/time are explicit
test inputs. This supersedes the untested material-journey gap, but does not
close native rendering, real-time pacing, full Host restart or deployment gates.
