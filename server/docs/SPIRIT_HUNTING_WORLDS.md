# Spirit hunting worlds and remaining source research

Date: 2026-09-16. Build milestone only; not deployed.

## Completed behavior

Daily Spirit Beads now accept ordinary monsters in 12 additional public worlds:
1207, 1208, 1212, 1214, 1215, 1217, 1762, 1927, 1999, 2054, 2055 and 2056.
These cover additional Adventure regions and all six installed Frozen Grotto
floors. The first Grotto uses runtime map 1762; achievement region 1926 names
that same floor. Adventure Zone 1217 uses terrain 1075. Standalone Potency
map 1075 is not admitted by this rule.

The shared eligibility predicate also applies to existing Anniversary beads.
Their separate level boundary, cap, eligibility and rewards remain in force.
No missing Anniversary admission NPC or campaign has been invented.

The map must be the canonical registered public world, with matching logical
identity, reviewed terrain identity and unowned map metadata. Arbitrary copies,
owned maps, wrong floors, removed worlds, training/event worlds, guards,
companions, adventurers and boss-classified monsters remain excluded. The
registered monster must match its catalog identity and owner. Eligibility is
checked again after taking the inventory/monster locks and before committing.
Existing per-life duplicate protection, bead ownership, capped progress,
transaction rollback and reconnect behavior remain in force.

## Evidence and limits

The [official Spirit Bead guide](https://co.99.com/guide/quest/spirit-bead.shtml)
describes level-based wilderness credit and separately lists leader and named
boss bonuses. It does not enumerate map IDs. Extending these verified public
hunting regions is a Revora integration policy, not a verified historical
client-side eligibility predicate.

[Travel identity research](TRAVEL_ACHIEVEMENT_RESEARCH.md),
[region facts](research/TRAVEL_REGION_FACTS.csv), ExplorationWorld and
AdventureTravel establish the map identities and ordinary admission paths.
Read-only installed public monster spawns confirm Cateran/Huge Snake/ape,
Water Devil/serpent and Grotto populations. Monster configuration identifies
the fixture species as normal monsters. These inspections never read player
records or change the production database.

Read-only catalog inspector: Revora-owned C# x64 helper, compiled against the
isolated build, run from the installed runtime directory. Final inspector hash:
`8AC1FBF1B5C00CA41EADF6F25AA464EF824C578744869A1BA236F468D5FD0FD8`.
Its source and binary remain ignored under `obj/chi-refill-validation/`.

## Remaining boss and leader work

The [Sword Master guide](https://co.99.com/guide/monsters/sword_master.shtml)
places the encounter in the player's house, summoned with Sword Soul once
daily. Its installed monster record 4170 exists; no active spawn/use route was
established in this audit. A later source audit found and disabled an unsafe
legacy item/2032 summon continuation; see [restoration](SWORD_MASTER_RESTORATION.md).
A complete owned summon, house admission, victory,
loot and team-reward lifecycle must precede its 1,000-spirit hook.

The [Snow Banshee guide](https://co.99.com/guide/monsters/snow_banshee.shtml)
places the boss on Grotto floor 2 with a half-hour respawn. Installed 4171 is
named IceQueen; 4172 is SnowBansheeSoul. The legacy NPC 899990 time-window
summon and a Golden War summon use IceQueen, while the old NPC 2999 handler
is commented out. No normal spawn row was found for 4170, 4171 or 4172.
Do not award the original named bonus to arbitrary IceQueen deaths on that
basis. Reconcile encounter identity, availability and durable life ownership.

The guide's 10-spirit wild leader label does not identify installed families.
Banditleader 1401 and Cateranleader 1405 have numerous common spawn groups;
8100/8200 families have Messenger/Aide labels, and 8300 families mix King,
Boss and unrelated names. A substring match is insufficient evidence. This
milestone retains ordinary level credit where eligible and does not assert a
new leader classification. Existing owned Terato and Spook bonuses remain.

## Validation

The isolated server build passed, followed by 596 Soul Ball and 511 Anniversary
checks (1,107 total), plus three producer-audit checks. The producer inventory
remains 219 present / 118 absent, a source upper bound rather than per-player
completion proof.

Every added world has a catalog-monster credit/reload case. Soul Ball tests
reject private copies, wrong floors, missing/owned metadata, removed maps,
training and Potency maps, and a corpse removed while waiting for its owner's
transaction lock. An ordinary player uses the real Grotto Envoy and installed
first-to-second-floor portal, fills an issued Super bead through 125 actual
`Entity.Die` calls, reconnects mid-hunt, and redeems/reloads the complete prize.
Monster placement and lethal death invocation are fixture supplied; this is
server quest integration evidence, not a native combat playthrough.

Existing Anniversary beads are filled to their cap in each added world and
redeemed/reloaded with their entire reward. Its prior full 15,000-spirit hunt,
duplicate-payment and rollback cases still pass. Fixtures use only a disposable
PostgreSQL schema.

Server SHA-256:
`4CF4226FBBCB558DD7C750AC43260B097711641B3B64B48F66F5E5048831B5B6`.
Final harness SHA-256:
`4D77EEBFCFB4C3A71238BA0EC548ADE76E1C4A2D29F1E5D416A40B6BA789E225`.
Ignored logs: `soul-worlds-final.log` and `anniversary-worlds-final.log` under
`obj/chi-refill-validation/`.
Native 6711 presentation, production deployment and server restart acceptance
remain open. No new achievement producer is claimed by this map correction.


## Banshee follow-up (2026-09-21)

A later item audit found that Banshee Spirit 3004259 fell through into Promotion
Pack consumption while its separate unsafe confirmation spawned PurpleBanshee
7565. Both paths now preserve the item pending restoration. This distinct house
family must not be silently aliased to wilderness IceQueen 4171 or
SnowBansheeSoul 4172. See [catalog, official evidence and next steps](BANSHEE_RESTORATION.md).
