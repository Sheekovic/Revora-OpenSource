# Lottery ticket restoration

Current source status: the normal lottery now persists payment, pending result,
rerolls and claims. Version-three Magnolia Super seeds include LotteryTicket.
Live deployment, native acceptance and original box/odds parity remain open.
The exchange-only checkpoint and original plan below are retained as history.

Baseline `84b5535`, 2026-09-20. Source work only; no live deployment.

The approved client and server catalogs agree: LotteryTicket 710212 exchanges
for three SmallLotteryTicket 711504 at Lady Luck. Small tickets pay three per
initial draw. `python tools/audit_lottery_tickets.py` checks both exact catalog
hashes, encrypted round trip, identities, requirements and contract text.
The official [2016 Vegas guide](https://co.99.com/guide/event/2016/vegasadventure/)
independently identifies Lady Luck's old-to-small ticket exchange. It does not
publish the ratio; that comes from the installed catalogs. The test reads the
installed NPC's public database placement and walks to it; no NPC is relocated.

At baseline, server NPC 923 exposed exchange option 2 but always reported no
tickets. Its other options admit level-70 players to map 700. `Game/Lottery.cs`
uses three small tickets for an initial draw and one for each of two rerolls,
but payment, pending prize and acceptance are separate memory/database writes.
Pending prize recovery, duplicate acceptance, bounded random selection and
packet admission therefore need repair before this reward joins Magnolia.
The historical guide does not establish exact prize odds or box-specific pools.

The catalog exchange now uses existing NPC dialogue admission and the
owned gift transaction service. It preserves source binding, charges one exact
eligible bag unit, and grants three separate small tickets and a durable receipt in
the same transaction. No GM requirement, CP charge or quest/title grant.
Do not add LotteryTicket to Magnolia until its complete draw/reward path is
durable and validated. The overall Magnolia bonus chance remains 20%.

## Exchange validation and remaining work

The fixture uses the installed public NPC 923 in Twin City (1002), at 340,412, ordinary
non-GM accounts and production NPC option dispatch. It walks to the NPC rather
than inventing a placement. Sources in both bindings and zero/single/multiple
stacks yield three separate, matching-bound outputs. Source removal/decrement,
all three output inserts and the receipt commit together. Any suppressed source,
third output or receipt write rolls back the whole exchange; reconnect can retry
the preserved source. A changed durable source fences the session for reload.

Remote, wrong-dialogue, direct item-use, dead, trading and unusable-source paths
cannot exchange. Worst-case output capacity is checked before payment, crediting
the source slot only if it disappears. The short per-player exchange debounce
also covers separate old-ticket UIDs. Repeat schema initialization preserves
completed receipts. Existing Meteor Scroll and Money Bag contracts stay intact.

This validates supplied-ticket exchange, not a Magnolia-earned lottery draw.
Pending-prize recovery, whole draw/reroll/claim transactions, source binding
through rewards, historical selection rules and native acceptance remain open.
The old ticket is intentionally still excluded from Magnolia's bonus pool.

Validation: isolated x86 build, two exact catalog identities, **659 gift
checks**, **799 Magnolia checks** and three fresh-process assertions pass.
Logs: `obj/chi-refill-validation/lottery-exchange.log` and
`obj/chi-refill-validation/lottery-exchange-magnolia.log`.
The existing older gift receipt schema is extended in place and reinitialization
with completed exchange receipts is tested. No live database or client changes.

## Implementation plan at the exchange-only checkpoint

Source inspection finds only the daily `LotteryEntries` counter loaded/saved by
`EntityTable`; pending item ID/plus/sockets and reroll count live on `Entity`.
`Process` deserializes 1314 before validating length and dispatches Accept,
AddJade and Continue without a durable request/prize identity. All five box NPCs
share `LotteryTable.LotteryItems`; their category dialogue does not prove
category-specific selection. Preserve this as observed implementation, not
verified official behavior.

1. Audit loaded prize rows against the installed catalog; reject invalid rows
   and replace unbounded retry selection with bounded, explicit Revora weights.
   Do not invent official odds or imply distinct box pools without evidence.
2. Persist an owned pending draw, exact prize metadata, binding and reroll
   count with ticket consumption in one transaction. Reconnect must restore the
   existing result without charging or drawing again.
3. Claim the pending result and insert its exact owned item atomically. Reject
   replay, stale sessions, full inventory and unknown pending results before
   changing payment/rewards. Rerolls charge one ticket, at most twice, while
   preserving the previous prize on rollback.
4. Admit only bounded, valid existing 1314 frames and nearby registered box
   interactions. Validate ordinary exchange-to-draw-to-reroll-to-claim journeys,
   reload, concurrency and suppressed writes in the isolated database.
5. Only after those paths pass, add the ticket to a new frozen Magnolia bonus
   version and validate the actual earned route. Keep original/native parity,
   the two other ambiguous packs and rollout gates explicit.

## Owned normal draw and earned Magnolia route

Follow-up to `4f070bb`. NPCs 925..929 on map 700 and existing 1314 actions use
`LotteryDraws`. Startup snapshots 191 configured normal prize rows after their
catalog load and rejects absent items, invalid rank/chance, plus or sockets.
All boxes still use the configured common pool: separate category weighting is
not established by these rows. Selection preserves the two legacy acceptance
comparisons using unbiased cryptographic bounded draws instead of shared
FastRandom modulo output. This is Revora selection policy, not official odds.
After 4,096 rejected candidates it fails without payment; the random helper also
has a finite rejection limit. Invalid RNG/configuration cannot consume tickets.

An owner-locked transaction checks the complete bag and exact usable ticket
preimages, consumes three units, writes a pending prize and records the source
UIDs/stacks/units with that result. Repeated initial interactions restore the
same pending draw. Its box, prize ID, plus, sockets, durability, binding and
reroll step are durable. Legacy volatile fields and LotteryEntries do not
authorize claims; the new draw ledger is authoritative. No new daily cap is
introduced. The old NPC's daily/VIP-limit prose remains a presentation gap.

Each of at most two rerolls costs one eligible ticket and atomically replaces
the result. Binding is cumulative: any bound initial/reroll ticket binds the
result. A session debounce covers duplicate draw/reroll requests. After
reconnect, revisiting the original box restores the result without requiring
another ticket. Claim inserts the exact prize and marks that result claimed in
one transaction. Suppressed inserts/updates, stale bags/sources, a full bag or
invalid ownership cannot lose a paid result or grant a duplicate.

1314 admission validates the consumed fields, declared length, seal and action;
it does not infer incoming length from the outgoing display. The existing
serializer's byte setters no longer erase adjacent plus/socket/reroll fields.
No native request nonce is verified, so a delayed Continue after a claimed draw
cannot be distinguished from another intentional paid draw. Durable claims
remain once-only; a short debounce is not represented as wire replay protection.

All 191 configured outcomes were drawn, claimed and cold-reloaded with exact
plus/socket metadata. Fault tests cover ticket debit, result, payment receipt,
reroll, prize insertion and claim update; other checks cover binding, both reroll
limits, compact/malformed frames, full inventory and bounded random exhaustion.
The actual Magnolia ticket also completes exchange, failed/retried draw,
reconnect and claim: 812 Magnolia checks and the 39-identity audit pass.
The final combined gift/lottery suite passes **1,319 checks**. Its log is
`obj/chi-refill-validation/lottery-draws-final.log`; the earned route log is
`obj/chi-refill-validation/magnolia-earned-lottery.log`. These counts include
shared prerequisites and walking assertions, not 1,319 distinct features.

Anniversary uses the same old packet ID, but every Anniversary box is explicitly
closed by `TwinCityPlaza` and none is installed on maps 1002/701. That closure and
its legacy implementation are preserved. Its 17-row pool is not part of the
normal lottery, not tested as a public earning route and not enabled here.
Native rendering, full Host restart, closed campaign restoration, historical
pool/odds parity and authorized rollout remain open. No production data changed.

## Retained Magnolia-to-lottery route (2026-10-02)

The retained main's seven banks are full. A normal mutual trade moves five
existing unbound Meteors/DragonBalls to its existing spouse, preserving their
exact identities in the nested two-character snapshot and leaving35 bag slots
occupied. No item is deleted or supplied to free capacity.

On controlled Magnolia day2035-02-01 the main buys a Super seed at the configured
CP price, plants it through the item handler, and receives the normal harvest,
bound rune and LotteryTicket710212 bonus. BonusRoll selects a successful bonus
and ticket slot6; this is outcome-controlled earning, not proof of natural odds.
Lady Luck923 option2 exchanges the ticket for three small tickets. Option1 admits
the level140 main to map700. Box925 option1 consumes those three tickets and
persists a pending draw; LotteryDraws.Roll is controlled to0 for deterministic
selection. The fixture never inserts a ticket, prize or achievement directly.

After full game/database restart, revisiting the original box restores the pending
prize with no tickets remaining. A sealed1314 Accept claims it once; replay changes
nothing. A second restart preserves the exact nested career, transferred items,
Magnolia and lottery receipts, claimed item UID and achievement10118.
`career-retained-lottery-{earn,claim,reload}` all pass against candidate SHA-256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`;
probe `revora_retained_lottery_probe` finishes at step5.

Prior funding and character qualifications retain their documented provenance.
Proximity/calendar and random outcomes are controlled. This adds retained ordinary
handler evidence, not natural drop-frequency, native UI or authenticated-network
acceptance. No production binary, public service or deployment changed. The
existing delayed-Continue ambiguity described above remains; replay refusal here
refers to the paid result's claim, not unlimited replay protection for new draws.
