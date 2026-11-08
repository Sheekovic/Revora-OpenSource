# Original housing material routes

2026-09-20, exchange implementation baseline `e935226`. The copper prerequisite
and all seven ordinary NPC exchanges are now connected in source. Native and
full mined-material journey acceptance remain open.

## Published recipes and installed identities

The primary [Build a House guide](https://co.99.com/facebook/guides/buildhouse.shtml)
specifies the four-step permit chain below, totaling 2,500 ores. The ordinary
desktop guide link currently redirects to the site's error page; the official
Facebook guide remains readable. The primary
[Upgrade a House guide](https://co.99.com/guide/guides/upgradehouse.shtml) specifies
the three-step copper chain, totaling 1,200 copper ores. Construction additionally
costs 300,000 and 500,000 silver respectively, already enforced by Housing.

| Actor | Input | Output identity |
| --- | --- | --- |
| Craftsman | 5 ores | 721171 Timber |
| Carpenter | 10 Timber | 721172 RosewoodVou. |
| Carpenter | 10 RosewoodVou. | 721173 TimberVou. |
| House Agent | 5 TimberVou. | 721170 HousePermit |
| Stoneman | 12 copper ores | 721175 CopperOreVou. |
| Stoneman | 10 CopperOreVou. | 721176 OreVoucher |
| House Agent | 10 OreVoucher | 721174 UpgradeCert. |

Both installed client and server catalogs agree on all seven identities.
The exact catalog hashes and decoder are recorded in
[housing research](HOUSING_EARNING_RESEARCH.md). Native tooltips independently
confirm the last permit exchange and both voucher exchanges in the copper chain.
They place House Agent at Twin City 342,518 and Stoneman at Twin City 228,397;
these differ from older guide coordinates and current server placements.
The Craftsman accepts the explicit ordinary whitelist: iron 1072010-1072019,
copper 1072020-1072029, Euxenite 1072031, silver 1072040-1072049 and gold
1072050-1072059. All 41 identities and names match both installed catalogs.
This interprets the guide's generic ore wording; it does not establish retail
eligibility for special/event ores. Stoneman accepts only the ten copper grades.

A fresh read-only metadata inspection found:

| Actor identity | Current installed location |
| --- | --- |
| 30158 Craftsman | Market 1036, 141,107 |
| 30159 Carpenter | Market 1036, 168,99 |
| 30160 Stoneman | Market 1036, 184,103 |
| 30157 HouseAgent | Map 5000, 83,95 |

At the research baseline no corresponding exchange handlers existed. The new
`HousingMaterials` handler owns all four actors. Source startup overlays House
Agent at Twin City 342,518, using the installed appearance and leaving its database
row unchanged. Placement checks terrain and runtime NPC conflicts before moving
the actor; repeated placement is idempotent. The three Market actors retain their
installed locations. Stoneman's older Market placement still differs from its
6711 tooltip and needs native presentation review.
The reviewed x64 inspector binary SHA-256 for this follow-up is
`B6F315F701292F079B32C4E2D23E707EFB3D8A1E4587AAD16185F000C9C8B6B6`.
It uses a read-only transaction and prints public metadata only. Live house count
remains zero at this checkpoint. No NPC database row was changed.

## Restored copper mining prerequisite

The owned mining service's first extra-loot slot tried item IDs 31072050-31072058.
None exists in the installed server catalog. Its remaining slots produced
Euxenite, silver, gold and special loot, but no copper. That prevented the
published level-2 certificate route from being earned through mining.

For Maple Forest terrain 1025, including the existing logical alias 1218, that
otherwise unusable first slot now yields 1072020-1072029. Both catalogs identify
these as copper grades 1-10. All ten grades are eligible and initialization
requires their definitions. The earlier normal-gem branch remains unchanged.
Other terrain retains its previous extra-loot selection.

The inherited 40% extra-loot gate and 30% first-slot selection are explicit
**Revora odds**, not recovered retail probabilities. Copper therefore has a 12%
chance after the normal-gem branch fails; total per-interval probability is
`(1 - configured gem chance) * 0.4 * 0.3`. Each copper grade is equally likely
within that slot. Later Maple Forest extra-loot branches run less often because
this slot can now succeed. Original retail drop-rate parity remains unresolved.

The existing canonical session/tool/location checks, two-second interval,
inventory capacity, durable yield receipt, item insertion, failure fencing and
reload behavior are reused. No client packet or mining animation layout changes.

Validation: isolated build, 137 mining checks and 246 Adventure journey checks
pass. These suites overlap; they are not 383 independent player journeys.
The 43 added assertions cover all ten grades on both logical mine maps, saved
item identity, interval replay rejection and the 40/30 selection boundaries.
Existing malformed/ownership/payment/failure mining tests and the full walked
mining/islands/Waterlord/crafting/first-rebirth journey remain green. Grades are
controlled fixture draws, not a measured distribution or native playthrough.

## Exchange implementation and validation

Each exchange requires a canonical ordinary player near the matching NPC, outside
trades, booths and poker. A two-minute, session-owned confirmation snapshots the
selected items and is consumed once. Selection uses bound materials first and
propagates binding to the output. Locked, suspicious, temporary, stored and
wardrobe items are excluded. Legacy zero stack counts mean one unit. Mixed ore
grades and partial stacks are supported; consumption may free the output slot.

The owner lock, saved location, exact bag identity and material preimages are
checked inside one transaction. Material debits, reward insertion, exchange
receipt and per-item receipts must each write successfully. Only after commit
does inventory update. An unexpected persistence failure fences the session for
reload; duplicate confirmation cannot debit again. No new packet layout is used.

The isolated build and 117 housing checks pass. Tests use ordinary non-GM sessions,
real NPC dispatch, concurrent confirmations, production character/item reload,
all seven recipes, mixed bound grades/stacks, all five ordinary ore families,
unrelated rewards, capacity before/after consumption, invalid/expired offers and
suppressed delete/update/reward/receipt writes with reload retries. Saved materials
remain intact on rejected requests. The exchanged permit and upgrade certificate
also feed real construction and persisted achievement credit. Walking tests join
House Admin to House Agent and the three Market actors; the installed NPC catalog
has no overlap at the proposed Agent location.

Inputs and starting funds in these exchange tests are fixture supplied. They do
not prove the complete 2,500-/1,200-mined-ore journeys, a full Host restart or
genuine-client presentation. Furniture, guest access, later gem/installment routes
and native acceptance remain open. No new producer identity is added; coverage
remains 233/337. No deployment, live schema change, client patch or restart.

## Full mined-material acceptance fixture

The first full run completed both constructions and both persisted achievements,
but failed its final accounting assertion. A focused rerun isolated a real
purchase defect: buying two return scrolls saved their items but left the
400-silver debit only in memory (runtime 998600, saved 999000). Periodic reconnects
therefore restored spent silver. Final full-quantity acceptance remains open
until the repaired route passes its complete accounting assertion.

`tests/PotencyHunt/Verify-PotencyHunt.ps1 -HousingEarningOnly` extends the fast
`-HousingOnly` checks with one ordinary player's complete level-1 and level-2
material route. It supplies starting silver, buys and equips the actual shop's
Pickaxe, mines all 3,700 ores through the production mining service, and performs
the 667 required exchanges through NPC dispatch. The forty-slot bag limit remains
active; no ore, voucher or certificate is inserted by the fixture.

Travel uses accepted walking, installed portals and the Forest Mine assistant.
The Pharmacist's existing shop supplies two Twin City Gates per collection trip,
used through the normal item handler. The player reconnects every ten trips and
after each construction. Assertions require saved materials/tool, exact mining
and exchange totals, no residual vouchers, both building achievements, no CP
spending and exact silver for the tool, scrolls and construction.

Market entrance 211,196 and the Craftsman/Carpenter occupy separate walking
components in the installed 384x384 server floor. Walking to 155,107 followed by
an ordinary bounded jump to 141,111 reaches the craftsmen's component. The actual
server jump handler accepts that transition, after which ordinary walking reaches
all three actors. No NPC relocation or new travel privilege is needed.
The reviewed Market floor SHA-256 is
`aaca8678fe2061e7f0221324dc6cbade8cad195eeb971b5b2eeb1ad69950ea03`.

Mining draws are controlled copper successes and the two-second clock is
advanced in the fixture. The jump helper resets timing state to exercise the
production distance/landing checks; it is not an anti-speed timing test.
This validates required quantities and transaction
composition, not random drop frequency, real-time completion duration, native
rendering, the gold-earning prerequisite or a full Host restart. Ordinary client
acceptance and the remaining housing features retain their separate gates.

## Durable return-scroll purchases

`TravelScrollShop` now owns silver-stock purchases of 1060020-1060024 from the
installed nearby shops. It preserves their catalog prices, supports 1-40 units,
merges eligible unbound stacks (including a full bag with stack room), and keeps
non-silver/premium routing with its existing handlers. Locked, bound, stored and
temporary stacks are never merged. Frame, canonical session, location, stock,
bag identity, purse and stack preimages are checked before committing stock,
silver and a purchase receipt in one transaction. An identical request within
one second is suppressed; later purchases remain available.

Both direct shop dispatch and the packet entry point are connected. Successful
commit immediately projects the saved purse and item stack. Missing payment,
item-insert, stack-update or receipt writes roll back the complete purchase and
fence for reload. No purchase can create stock while retaining its saved payment.

The isolated build and 148 fast housing/scroll checks pass, including all five
gate types, exact balances, concurrent duplicates, reconnects, full-bag merging,
invalid stock/quantity/seal/context, stale stacks and each suppressed write with
reload retry. The long fixture now checks saved silver after every scroll
purchase, so a recurrence fails at the first trip. This repair is source only;
no production deployment, schema mutation or client patch was performed.

## Completed post-repair server route

The same post-repair `-HousingEarningOnly` process completed with exit 0 and
**2,509 checks passed**. Its final assertions confirm 3,700 ores earned through
the production mining handler, 667 completed exchanges, 149 collection trips,
both house levels and achievements surviving production reload, zero CP spending,
and **139,400 silver** remaining from the fixture's initial 1,000,000 after the
1,000-silver pickaxe, 59,600 in return scrolls, and 800,000 construction costs.
The owned fixture schema was cleaned by the runner. The local transcript is
`obj/chi-refill-validation/housing-earned-fixed.log`; tested gameplay baseline is
`b6264ab` (later `5b005a5` added static research only).

This result supersedes the earlier failed final-accounting run. Milestone c4
now records server acceptance; c5 retains genuine-client acceptance explicitly.
The controlled mining draws/clock, initial fixture purse and jump-timing limits
above still apply. No live deployment or native acceptance is implied.
