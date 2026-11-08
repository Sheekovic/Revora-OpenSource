# Historical reward decisions

Owner decision, 2026-09-22: use documented Revora earning rules for Fashion
Icon/Idol/Master, Euro Planet wings and Honored Hero. Option B below is the
approved implementation target, including its durations and replay protections.
These rules are not claims about retail behavior. Approval does not establish
implementation, activation or deployment. Fashion scheduling/grants now have server fixtures; Euro Planet now has a tested four-weekend server earning route and fixed-term grant ([evidence](PLANET_WEEKEND.md)). Honored Hero now has a tested receipt-based server grant; see its
[implementation and remaining gates](HONORED_HERO_REWARDS.md). Native acceptance remains pending
at the owner's explicit direction. This does not replace the full roadmap.

## Why an explicit choice is needed

The owner requested genuine earning requirements. The installed catalog confirms
Fashion competition placements but not scoring or award duration. Honored Hero
20572057 has no established source. Euro Planet's original threshold and duration
remain unknown. These missing rules cannot be inferred from a matching name in a
different edition.

A bounded follow-up checked the official sources below. It did not close those
gaps; do not repeat the same queries without a new lead:

- [European Football Championship](https://co.99.com/guide/event/european_championship.shtml)
  associates Planet wings with upgrading mount armor and directs players to Boger,
  but does not publish the missing threshold or duration. Its jersey/mount title
  conditions are separate from the wing condition.
- [2021 Anniversary packs](https://co.99.com/guide/event/202105_sales_anniversarypacks.shtml)
  sells a 90-day bound Planet-wing box. This later shop offer does not establish
  the original Euro reward condition or the corresponding 6711 wardrobe grant.
- [Origin server events](https://co.99.com/guide/event/origin_server_-_exclusive_events.shtml)
  uses Honored Hero as an event progression category. It does not identify
  wardrobe reward 20572057 and is not sufficient evidence for that title.
- Searches for the exact Fashion Icon/Idol competition names did not recover an
  authoritative scoring contract. Search absence is not proof that none existed.

## Reviewable alternatives

Option A retains original requirements: keep these grants locked and only resume
research when a dated original guide, client contract or other authoritative
source becomes available. Other confirmed earning paths continue independently.

Option B adopts explicit Revora editions. Approved rules:

| Reward | Proposed Revora earning rule | Duration |
| --- | --- | --- |
| Fashion Icon / Idol / Master | Monthly local collection competition. Finalize a server-side snapshot of currently owned permanent garment and mount-armor designs. Count each catalog design once using its verified star rating as points. Rank by points; equal scores share placement, and the following placement skips the tied count. An eligible entrant needs at least one scored design. No payment or GM requirement. | 30 days from season close; delayed claims do not extend the term. |
| Euro Planet wings | A recurring weekend hunt with three distinct objectives: an eligible monster hunt, a guarded encounter and successful delivery. One saved completion per character per weekend; four distinct weekend completions unlock one wing claim. Exact actors, map and combat tuning must be reviewed before implementation. | 30 days from the fourth completion. |
| Honored Hero | One-time completion of five distinct existing ordinary routes: Soul Trial, Courier Loop, Path Hunt, first rebirth and second rebirth. Count durable final receipts, not individual stages, purchases or repeated lives. Existing evidenced completion may count; unproven historical state may not. | Permanent, once per character. |

These are Revora rules, not claims about retail behavior. Implementation requires
checking the catalog-to-reward mapping and existing duration support before a
grant can be enabled. If a selected item cannot represent the proposed duration,
resolve that discrepancy instead of silently substituting a permanent award.

Fashion's [snapshots and award consumer](FASHION_COMPETITION.md) now have server
fixtures, a monthly coordinator and merchant guide; process/native acceptance
remains pending.

## Implementation and acceptance after selection

Use ordinary authenticated-player eligibility, durable participation/result
receipts, atomic grants and replay protection. For competitions, freeze results
before claims; make ownership snapshots and ties reproducible after restart.
For weekend progress, use explicit Pacific calendar boundaries and distinct
weekend identities, with no repeated credit from the same final receipt.

Validate earned completion, concurrent claims, full-inventory failures where
applicable, reconnect, expiry and process restart. Preserve all existing native
6711 and deployment gates. The current disconnected test session exposes zero
DX9 adapters, so a green server test cannot close native acceptance.

Keep NPC dialogue brief and in-world. Put detailed scoring and eligibility on an
optional rules page. No source checkmark changes until an implemented contract
has the evidence required by its scope.
