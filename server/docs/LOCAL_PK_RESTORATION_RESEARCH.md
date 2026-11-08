# Remaining local PK earning routes

Research checkpoint: 2026-09-15 UTC. Weekly and monthly wars now have a
[validated implementation](LOCAL_PK_WAR.md); [Class PK](CLASS_PK_WAR.md) is also
implemented and validated. The [Couple PK NPC edition](COUPLE_PK_WAR.md) uses
explicit spouse confirmation and validated pair results; native holding-hands/hug
entry remains unverified. BP PK remains an unresolved separate route. Arena and
Quiz bridges are not substitutes.

## Confirmed identities

Installed 6711 achievement predicates, from the private client evidence already
hashed in the quest/event research:

| Achievement | Required event fact |
| --- | --- |
| 11114 | Participate in BP PK War |
| 11115 / 11121 | Participate in / win Couple PK Tournament |
| 11116 / 11118 | Participate in / win Weekly PK War |
| 11117 / 11119 | Participate in / win Monthly PK War |
| 11131 | Win Class PK War |

No current generic PvP victory, NobleWar result, legacy flag or client-authored
counter establishes these different facts. BP PK's specific edition still needs
evidence; do not equate it with Extreme PK solely because both mention power.

## Official rule evidence

Sources read 2026-09-15 UTC; version differences below are deliberate findings.

- [Weekly and Monthly PK War](https://co.99.com/guide/quests/pkwar.shtml): four
  level divisions (below 100, 100–119, 120–129, 130+), successive arena stages and
  one survivor per division. The current guide uses Saturday for US weekly play,
  Sunday for EU, and the first of each month for monthly play, during 20:00–21:00.
  General Bravery admits players near Twin City 446,246. The page's protection
  wording conflicts regarding red/black names; select and publish Revora's safe
  death rules instead of claiming a universal historical rule.
- [Class PK War](https://co.99.com/guide/quests/propkwar.shtml): separate class and
  level divisions, including separate Water/Fire Taoists; one surviving champion.
  The current page uses Monday 19:30–20:30 with 15-minute enrollment through the
  envoy near 452,291. Its reward/timing remnants disagree with other editions.
- [2009 class schedule](https://co.99.com/content/2009-01-05/20090105170710035.shtml)
  separated classes across weekdays. It is not interchangeable with the later
  all-classes-on-Monday edition. The 2012 prize upgrade also lists different
  Monday rounds and rewards.
- [Couple PK Tournament](https://co.99.com/guide/quests/couplepk.shtml): Friday
  21:00–22:00, married pairs, team membership and a paired handholding/hug action
  at enrollment. The guide is near 444,290. A one-player win or an arbitrary
  two-person team does not establish a couple tournament result.
- [Honor Halo](https://co.99.com/guide/quests/honorhalo.shtml): weekly, class and
  couple halos last seven days; monthly lasts thirty. Halo lifetime and wardrobe
  title/wing entitlement are separate records, not interchangeable numeric IDs.
- [Extreme PK](https://co.99.com/content/2008-11-04/20081104190650175.shtml): separate
  male/female preliminary fields and a combined final. Its page contradicts
  itself on the start hour. It does not establish the BP PK achievement mapping.

## Existing server gaps and implementation contract

The old schedules remain below the early return in `Thread.WorldTournaments` or
commented in `CreateTournaments`. Do not remove that return. NPC 2314 now routes to the owned weekly/monthly coordinator; its old
1559/1518 branches and old purse handlers are intercepted. The generic `KillTournament` uses mutable rosters and
direct prize callbacks. It is not an authoritative winner receipt producer.

Restore distinct, durable run identities, frozen enrollment divisions, safe
admission and transfer outcomes, elimination/withdrawal records, finite deadlines
and atomic winner claims. Record actual participation only after valid admission
to a genuine competitive field. Test insufficient turnout, restart cancellation,
outsiders, late entry, spectators, class changes, dead/revived players, duplicate
claims and disconnected champions. Couple entries need both owned sessions and a
persisted pair identity. Physical map and NPC IDs need a bounded installed-catalog
audit before enabling their selected routes.

Combat ownership needs particular care: do not add a second event lock underneath
the existing Elite PK lock while allowing teleport callbacks to acquire them in
reverse order. Queue travel outside competing event locks or establish a proven
lock order with concurrent transition tests. Existing Elite PK, CTF, city guards
and paid player services must continue to work for ordinary characters.
