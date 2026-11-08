# Guild War siege bomb earning (10231)

## Evidence and availability

The local 6711 achievement catalog identifies 10231 as using a bomb on the
castle gates. Item 721261 is already the server's Guild Bomb item-use case.
The [official Siege Bomb guide](https://co.99.com/guide/quests/bomb.shtml)
requires an active Guild War and closed gate, says no more than four bombs
open the gate, and describes the carrier dying in the explosion. It does not
publish a numerical damage formula. Revora uses ceiling(maximum gate HP / 4),
capped at current HP, as the documented implementation of that four-bomb rule.

Bombs are part of the existing native SubclassSupplies catalog at NPC 91008,
map 1002 (424,339). Existing loader/purchase verification is documented in
[progression-shop parity](PROGRESSION_SHOP_PRICE_PARITY.md). This batch tests
bomb use with owned persisted items; it does not repeat purchase acceptance or
claim that the separate original Saltpeter/Sulphur crafting quest is restored.

## Owned event path

Item use enters the existing GuildSeasonWar coordinator. It requires the
canonical ordinary guild member, real map 1038, the active saved battle, a
canonical closed gate within the combat range of 18, and an owned bag item.
The nearest eligible left/right gate is chosen, with UID breaking ties. Fake,
companion, dead, trading and remote-map actors cannot use this path. The range
and item-use interface follow the server combat route; native gate-click
confirmation is not verified.

The owner installation lease/item lock and nonblocking battlefield gate prevent
simultaneous event transitions during acceptance. A transaction checks saved
guild membership and active battle/capture generation, consumes exactly one
unit of the selected bomb, and saves achievement 10231. A failed write rolls
back consumption, leaves gate HP and player life unchanged, and fences the
player until reconnect. A second request from the dead carrier does nothing.

After commit the gate loses the accepted damage, opens with its existing mesh
when HP reaches zero, and broadcasts its state. The carrier uses the existing
numeric-killer death path and the achievement refreshes. Gate damage adds no
pole score or first-pillar achievement. Bombs work on low remaining HP rather
than disappearing without damage, as the old fixed thresholds could do.

Gate HP and player death are live-world state, not part of the item/evidence
transaction. A host crash after commit but before world projection can retain
the consumption/achievement without replaying the blast. Existing startup
cancels an interrupted battle; this change does not implement resumable gate HP
or exactly-once death across crashes. Native acceptance/deployment remain open.

## Validation

Isolated build and 496 guild checks pass. The bomb fixtures use the actual
item-use route and scheduled Guild War, proving out-of-event/map rejection,
fake/foreign-item rejection, suppressed-evidence rollback, stack and single-item
consumption, repeated callbacks, carrier death, four-bomb left-gate opening,
low-HP right-gate opening, already-open refusal and ordinary achievement login.

## Retained purchase, blast and cold replay (2026-10-01)

- [x] The ordinary retained guild founder purchases one721261 bomb from the
  installed SubclassSupplies NPC91008 through the sealed item-purchase request.
  The loaded catalog price is10 CPs; exactly that amount is deducted.
- [x] Guild Controller entry reaches the active Sunday battlefield. Using the
  owned bomb on its closed left gate consumes the item, deals2,500,000 damage
  (one quarter of the10,000,000 maximum) and kills the carrier. Repeating use of
  the old item object does not damage the gate or deduct currency again.
- [x] The normal revive handler returns the carrier alive to Twin City. The
  round closes empty with no pole capture, winner or Guild War purse. This
  earns10231 through guild-siege-bomb-v1, not a fabricated war-victory receipt.
- [x] A separate fresh process retains the exact saved career and10231 receipt,
  confirms the purchased item no longer exists, and rejects its sealed replay
  without changing items/currencies. The preceding earned Holy Grail, Invincible
  title and Supreme wings remain owned/equipped as appropriate.

Both lifecycle phases pass against private executable SHA-256
07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2.
Fixture: SiegeCareer in tools/CheckCandidateCareerRites.cs; private logs are
arena-process-career-siege-earn.log and arena-process-career-siege-reload.log.
Schedule, approach position and revive wait are controlled. This advances the
retained purchase/use/persistence route; it does not add resumable gate HP,
native presentation, original bomb crafting or live deployment. No gameplay
code or live server was changed.

## Fresh paid siege-bomb use and cold recovery (2026-10-03)

- [x] Continue ordinary FreshRoadmap1 from the paid Power Arena pack checkpoint,
  using its earned FreshBanner membership. Buy bomb721261 from the actual
  NPC91008 shop packet for the catalog price of10 CPs.
- [x] Enter a controlled2041-03-17 Guild War through the gatekeeper. Use the
  owned bomb beside the left gate: consume it once, deal2,500,000 damage and
  apply the bomb's player death. Reusing that item adds no damage or charge.
- [x] Normal revival returns the player to town. End the uncaptured war as empty,
  with no invented winner or purse. Achievement10231 records the siege action.
- [x] Cold restart preserves the complete nested career, payment, consumed item,
  achievement and empty battle row. A sealed consumed-item replay changes nothing.

Fixture `tools/CheckCandidateFreshSiege.cs` passes
`career-fresh-siege-{earn,reload}` on the first run, using private game SHA-256
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
Checkpoint: `revora_fresh_siege_probe`. Calendar, approach and revival wait are
controlled. Persisted gate HP, native combat/display, contested siege and live
deployment remain outside this batch. No production rebuild or live restart.
