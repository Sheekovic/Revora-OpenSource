# Leftover Power Arena refinery packs

M6.56b9q, 2026-09-23. Server implementation. Not deployed.

Ordinary 1009/action-4 and direct `UseItem` of the 48 reachable Power Arena
packs go through `PowerArenaPacks`. Those leftover cases fail closed.

## Leftover terms (preserved)

Sixteen families, three tiers (Primary/Medium/Superior), leftover
`GetNextRefineryItem` extras 0/1/2, and the installed **724449 → 724445**
one-hand Breakthrough identity. Badge costs remain 2/6/12. Wang
**500010** exchanges bound packs; pack opening keeps the pack's Bound flag
on the material. Stack remainders stay.

Owned IDs: **723130–723135**, **723651–723677**, **723684–723686**,
**724136–724147**. Other leftover refinery cases stay live-split-write.

Ordinary earning is Power Arena floor Agates (bound **720148** Refinery
Badges) then Wang option 10/230 exchange, then pack use.

## Owned contract

Badge exchange and pack opening share canonical inventory preimages, source
consume, output insert and receipts `revora_power_exchanges` /
`revora_power_pack_results` in one transaction. Failed source, output or
receipt writes roll back together. Direct daily P7 is unchanged.

## Remaining work

HonorShop leftover GetNextRefineryItem packs outside this set are owned by
[HONOR_REFINERY_PACKS.md](HONOR_REFINERY_PACKS.md). HonorShop leftover
GainRefineryItem packs are owned by [GAIN_REFINERY_PACKS.md](GAIN_REFINERY_PACKS.md).
Unrelated leftover source+timer, other leftover packs,
Host restart, native acceptance and deployment remain open. Parent
[M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.

## Fresh earned badges to usable outputs (2026-10-03)

The fresh ordinary career now earns twelve badges in two calculated-combat
Power Arena sessions and exchanges them in two six-badge transactions through
Wang. Both medium packs723655 survive cold restart before opening. Ordinary
item use consumes each exact pack and creates one bound724406 output, with
unforced result rolls and matching persisted output receipts. Both outputs and
all prior possessions survive another full process restart. Duplicate exchange
and consumed-pack requests grant nothing.

All five phases in the [fresh Power Arena batch](POWER_ARENA.md#fresh-two-session-earning-and-reward-recovery-2026-10-03)
pass. This closes normal earned exchange/opening cold recovery for this selected
family/tier; it does not claim every family/tier, native presentation, natural
combat balance or live deployment.
