# Collection event acquisition evidence

Owner decision, 2026-09-30: recurring Revora event rewards selected. The pending
supplier questions below are historical. The selected earning contract is
documented in [Planet collection rewards](../PLANET_COLLECTION_REWARDS.md);
original-event research remains provenance, not the implemented edition.

Reviewed2026-09-27 against source824bf106. This closes a research gap, not an
earning route. The four collections remain predicate-only until normal players
can obtain every required permanent design, receive/equip the title and reload
without lost ownership or duplicate payment. No producer counts change.

## Original sources and unresolved implementation details

| Collection | Primary source evidence | Still required |
| --- | --- | --- |
| Saint Rider | [Baby Monkey Party](https://co.99.com/guide/event/baby_monkey_party.shtml) links the title to all eight monkey mounts. Its level80 event uses bananas, fragments and transformation masks; transformations require a permanent bag mount and preserve binding. A temporary login mount does not satisfy the collection. | Map every material/mask to6711 IDs; reconstruct all eight outputs; confirm unpublished reward odds and calendar edition. Do not count the two existing Vesper variants as the complete set. |
| Euro Collector | [European Football Championship](https://co.99.com/guide/event/european_championship.shtml) supplies six national jerseys through Pirlo for269 Fan Points each; a senior title requires six jerseys. Silver cheering is listed at1,700,000 silver per point. | Match national names to the local Fashion/Legend pairs, implement durable Fan Points and exchanges, and choose a recurring calendar or documented Revora edition. Do not silently enable the dormant non-atomic RVP shop. |
| Immortal Fate | [Legend of Celestial Fox](https://co.99.com/guide/event/legend_of_celestial_fox.shtml) distinguishes permanent Divine Fox, Celestial Fox and Celestial Fox(Fantasy). Pills upgrade the permanent mount in stages. It also lists a direct permanent Celestial Fox pack for9,999CP. The guide describes a2017 event. | Identify exact pack/material IDs and official probability tiers, or approve a documented Revora supply route. A Fantasy grant cannot substitute for the predicate's plain Celestial Fox. The guide establishes an item source, not independently the English title mapping. |
| Slam Dunk Leader | [Dream Manager?Detailed Version](https://co.99.com/guide/event/dream_manager_-_detailed_version.shtml) names the title and uniform rewards. Five starters unlock manager rewards. Two Rings of Fame exchange for an autograph; three permanent uniforms may also combine into a signed uniform, preserving binding if any input is bound. | Map every uniform/pack/ring to6711 IDs; verify the exact title set and team-power thresholds. A full original manager simulation and a Revora event reward exchange are different product editions. |

The guides support acquisition mechanisms. They do **not** supply a complete,
version-bound6711 wire protocol, all item IDs, all probability tables or a present-day
Revora calendar. The current wardrobe predicates remain authoritative until more
specific evidence justifies a correction. No year-specific event is re-enabled.

## Loaded server identities

The actual `ConquerItemInformation.Load()` method was run in a read-only local
catalog harness against the already tested assembly. It loads `itemtype-new.txt`
then `ItemtypeSub.txt`; the older `itemtype.txt` is **not** the active source and
omits the Fantasy fox. All26 requested designs were present after the real load.

| Local IDs | Loaded names / distinction |
| --- | --- |
| 200553?200558 | CuteMonkey, YokMonkey, SmartMonkey, JoyfulMonkey, FunnyMonkey, CharmingMonkey |
| 200559 /200560 | SolarMonkey /LunarMonkey; existing Vesper producers |
| 200595 /200596 | CelestialFox /CelestialFox(Fantasy); distinct collection predicates |
| 194675 /194685 | MythofDCCavaliers /LegendofBCWarriors |
| 194695 /194705 | EpicofACRockets(Signed) /DynastyofTCLakers(Signed) |
| 193735 /193775 | DaybreakRooster(Fashion/Legend) |
| 193745 /193785 | LionofFury(Fashion/Legend) |
| 193755 /193795 | ValiantHero(Fashion/Legend) |
| 193765 /193805 | BlueResurgence(Fashion/Legend) |
| 192655 /193815 | GermanyJersey /GermanyJersey(Legend) |
| 192665 /193825 | SpainJersey /SpainJersey(Legend) |

These are catalog identities, not proof of a working supplier. The descriptor
audit below establishes three national mappings explicitly and retains one
inference. Naming alone is insufficient. Signed and unsigned basketball designs
must not be swapped merely because they resemble each other.

Provenance:

- Loader: `Database/ConquerItemInformation.cs:15`; configured paths:
  `ServerBase/Constants.cs:118`.
- Loaded `itemtype-new.txt` SHA-256:
  `D66C0489807D877E19053C8C04994D6738FFCCF5C86565F216399F5DBFD55279`.
- Loaded `ItemtypeSub.txt` SHA-256:
  `625B6432F5A23AA5833C24D4A4C6BE709DD244B4E5F71E6CFBA6FE0272CD84B0`.
- Read-only harness: ignored `obj/CollectionNamesEvidence.cs`; invokes the
  production loader and prints only26 requested IDs/names. No database connection,
  character creation, reward mutation, proprietary file upload or server restart.

## Implementation sequence after edition selection

1. Choose original-source restoration or documented Revora event rewards. The owner
   question covers these four remaining collections; no answer is assumed.
2. Confirm each required ID, permanent duration, binding, duplicate-design behavior
   and ordinary-player supplier. Keep the existing collection predicates strict.
3. For an original edition, implement the proven acquisition chain and document
   every custom calendar/odds decision separately. For a Revora edition, attach
   missing-design rewards to explicitly selected event results with clear earning
   requirements; do not create free completion flags or GM-only suppliers.
4. Commit payment/progression, item creation and reward receipt atomically; keep
   full-bag/overflow rewards pending and make retries retain the original design.
5. Validate public entry, complete earning, wardrobe admission, title equip and
   fresh reload with a non-GM character. Then run composed Host and native gates.

Pending product decision and item research do not block independent roadmap work.
No event, shop, price, reward odds or live service changed in this research batch.

## Euro jersey descriptor mapping (2026-09-27)

A bounded read of the eight193735-193805 rows in the active server base catalog
and the existing local decoded client catalog resolves part of the national-name
gap. The server base and override hashes still match the provenance above; the
override has no matching rows for these eight IDs. The decoded local file is
`6711/ini/itemtype.dat.cypterplm.dec`, SHA256
`48342F2EA53FE0B26CD92E8DA2E7F602EE2C2948976DFF3538BB1BB008C121E6`.
Its derivation from the current encrypted file was not independently rerun in
this audit; it is corroborating local material, not a new native-client proof.

| Fashion / Legend IDs | Country | Evidence and confidence |
| --- | --- | --- |
| 193735 /193775 | France | Fashion description explicitly names France; matching design names link the editions. High confidence for the catalog mapping. |
| 193745 /193785 | England | Both descriptions identify a Three Lions design. England is the remaining country in the official six-country list after the five other mappings. Strong inference, not an explicit country name in these rows. |
| 193755 /193795 | Portugal | Both descriptions explicitly name Portugal. High confidence. |
| 193765 /193805 | Italy | Both descriptions explicitly name Italy. High confidence. |
| 192655 /193815 | Germany | Previously loaded names explicitly identify Germany in both editions. |
| 192665 /193825 | Spain | Previously loaded names explicitly identify Spain in both editions. |

The [official European Championship guide](https://co.99.com/guide/event/european_championship.shtml),
reopened for this audit, lists these six countries and269 Fan Points per jersey.
It does not list the numeric item IDs. Therefore the ID mapping comes from local
catalog evidence; matching guide order is not treated as proof. A future supplier
can use the catalog's actual design names without asserting the inferred England
mapping as verified retail behavior.

This removes the France/Portugal/Italy naming research prerequisite. It does not
prove an active supplier, approve prices/calendar, or make Euro Collector earned.
The owner has been asked to choose recurring event rewards or Vesper purchases
for the four missing collections; no answer is assumed. No catalog text/assets
are added to Git, and no build, database write or live change is needed.
