# Earned collections and Fashion placements

## Ordinary acquisition route (2026-09-25)

`-CollectionAcquisitionOnly` reuses the scheduled boss acquisition fixture with
one bounty per character, without running the 6,549-material rune journey. The
first ordinary character starts without currency/items, defeats one actual
scheduled boss through damage application, and receives its saved 50,000 RVP
bounty. Vesper then sells ten distinct permanent unbound designs at the existing
2,500 RVP price each. Actual wardrobe requests store each item; replaying the
request does not create another item. The remaining 25,000 RVPs persist.

| Purchased designs | Item IDs |
| --- | --- |
| Five garments | 189695, 194875, 187405, 189065, 188915 |
| Five mount armors | 200517, 200531, 200613, 200559, 200560 |

The fourth design cannot grant the five-design collection title. The fifth
qualifies for Fashionist or Swift Chaser respectively. The permanent Solar and
Lunar Monkey purchases also qualify for Monkey Rider, Solar Rider and Lunar
Rider. Two monkey variants do not qualify for the eight-variant Saint Rider.
Production character/item and uncached wardrobe reload retain exactly one of
each design, all five titles, and the exact remaining purse. Ordinary requests
equip each earned title. No GM status or character-name exception is used.

## All three Fashion placements

Two additional ordinary characters each earn their own scheduled boss bounty
and buy two garments or one garment respectively. Their real durable collections
join the first character's ten designs in the monthly snapshot. The resulting
placements are first, second and third; no collection/ranking/award rows are
seeded for these results. Fashion Icon, Idol and Master are awarded and equipped,
then restored by production character/item/wardrobe reload with unchanged RVP
balances and one source receipt apiece. Each expires at the original month close
plus 30 days, without extending its lifetime on reload.

**55 checks pass** against the existing isolated build. The new mode is available
in `tests/PotencyHunt/Verify-PotencyHunt.ps1`; the ignored validation wrapper uses
the isolated output directory. The default rune acquisition mode keeps its
83-bounty target. This batch adds validation and audit evidence, not new prices,
items or reward rules. The wardrobe audit now has 27 code-validated, 39
predicate-only and six unimplemented rewards across 72 keys.

Limits: level/rebirth qualification, time and inter-map placement are controlled.
Boss damage is supplied by the fixture through the normal damage receiver; this
does not establish real combat difficulty or class balance. No socket login,
production Host restart, native presentation or deployment is claimed. Owner WIP
remains in the local build and was not staged in this checkpoint.

## Romance and Fantasy fox extension (2026-09-25)

The expanded fixture passes **65 checks**. It replaces the first buyer's Fiery
Dragon purchase with Pacific Cloud (200614) and adds Celestial Fox Fantasy
(200596). Eleven designs cost 27,500 RVPs, leaving 22,500 from the earned bounty.
Bright Purple Romance (194875), Fierce Cloud (200613) and Pacific Cloud qualify
the ordinary male buyer for Love Joker and Love's Madness. The Fantasy fox
qualifies for Successor to Legend, with no base Celestial Fox/Immortal Fate grant.
All eight tested collection titles survive production reload and equip normally.

The second ordinary character is created with female body 2001. She earns her
own boss bounty and buys two garments plus Fierce/Pacific Cloud. Bright Purple
Romance alone is insufficient for Sweet Fairy; the complete set grants it plus
Love Joker, but not Love's Madness. Sweet Fairy equips and survives full character,
item and wardrobe reload with body 2001 and exactly 40,000 remaining RVPs. The
eleven/four/one-design collections still generate first/second/third Fashion
placements and all original expiry checks pass.

This supersedes the earlier ten/two/one-design fixture quantities above, while
preserving its earning and acceptance limits. The current inventory has 31
code-validated, 35 predicate-only and six unimplemented rewards. The remaining
five collection routes are Saint Rider, Big Fan, Euro Collector, Immortal Fate
and Slam Dunk Leader. This extension does not establish suppliers for those sets
or claim their completion. No production feature, price or earning rule changed.
