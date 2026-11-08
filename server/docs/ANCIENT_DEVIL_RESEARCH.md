# Ancient Devil restoration contract

Read-only research at `5a8d997`, 2026-09-20. The quest is not restored, deployed
or accepted in the genuine client. Achievement 11020 still has no trusted producer.

## Edition and item requirements

The official [PC quest guide](https://co.99.com/guide/quests/ancient.shtml) describes
level-50 admission, one daily attempt, class guard trials, certificate exchanges,
an amulet-funded summon at (218,208), and a Dragon Ball or six Meteors after victory.
It lists eight of nine class amulets. The [iPad edition](https://co.99.com/ipad/quests/ancient.shtml)
instead lists level 80, six of seven amulets and five Meteors. Do not combine those
editions or import numerical claims from player tips as authoritative rules.

The installed 6711 catalog and loaded server catalog agree on ten class item
pairs. All ten amulet descriptions require nine distinct class amulets, including
the Windwalker edition. Prefer that installed version evidence over the older
web count. It does not resolve daily accounting, certificate drop odds or newer
class guard-exchange behavior.

| Class | Certificate identity | Amulet identity |
| --- | --- | --- |
| Warrior | 710016 | 710011 |
| Trojan | 710017 | 710012 |
| Fire Taoist | 710018 | 710013 |
| Water Taoist | 710019 | 710014 |
| Archer | 710020 | 710015 |
| Ninja | 710634 | 710635 |
| Monk | 711335 | 711336 |
| Pirate | 711599 | 711600 |
| Dragon Warrior | 3004946 | 3004947 |
| Windwalker | 3301711 | 3301712 |

The first five certificate descriptions identify their corresponding guard and
amulet exchange. The five newer certificate descriptions provide no instructions.
Pair identities/names alone do not prove their exchange scripts. In particular,
723583 is a different NinjaAmulet item: the Ancient Devil catalog uses NinjaSpell
710635. Do not substitute the similarly named item.

The guide restricts classic characters to their class guard and permits several
newer classes to challenge any of the older guards. It does not say that choosing
a different guard lets one character earn every class's amulet. Cross-class
cooperation/trading is a plausible implication, not yet a verified earning rule.
Do not implement a solo all-class vending loop and call it the original quest.
Windwalker handling is absent from the retrieved PC guide.

## Installed world evidence

A bounded read-only transaction inspected only NPC, map, spawn and monster
metadata in the configured server database. It changed no schema or rows.

- World 1052 uses terrain/base 1082, with map status 8192.
- NPCs 300011-300015 are the five published guards at (229,233), (191,201),
  (181,176), (206,179) and (243,219), respectively.
- No matching Exorcist or switch was found in the inspected NPC rows. A separate
  read-only check found zero Exorcist NPC rows and zero SOB NPC rows on 1052;
  no Exorcist SOB row was returned either. No active
  quest handler for the guard IDs or these item identities was found in Npcs.cs.
  Exorcist admission and summon interaction still need implementation.
- Families 7000-7005 are DevilGuard and the Warrior/Trojan/Archer/Fire/Water
  devils. Each has one spawn-table row on world 1052. Row counts do not establish
  live population, certificate sources or isolated trial ownership.
- Adjacent family 7006 is **Guild Beast**, with its spawn on Guild War map 1038.
  It is not evidence for an Ancient Devil boss and must not be relabelled/reused
  based on numeric proximity. No Ancient Devil server family was identified.
  The [client profile follow-up](ANCIENT_DEVIL_SUMMONS.md) identifies presentation
  metadata, not a working server encounter.

Both installed map indexes register terrain 1082 as `map/map/idland-map.7z`,
trailing value 256. The world-to-base alias explains the absence of a standalone
1052.map; it is not a reason to invent another terrain or patch the map index.

The read-only terrain audit reserves each guard and the proposed switch's 3x3
NPC footprint. A conservative four-direction flood from the switch approach
(216,208) reaches 3,079 cells and a point within six paces of every guard and the
switch. This is a geometric probe, not an approved entrance landing or proof of
native visuals, jumps, trial maps, population, combat or quest exit behavior.

## Reproducible evidence

`tools/audit_ancient_devil.py` checks exact input hashes, all twenty client/server
item identities and descriptions, all ten nine-class requirements, target map
registrations and the shared walking component. It emits only identifiers,
hashes, geometry and limitations. It neither opens a database nor writes assets.

```
python tools/audit_ancient_devil.py --client ../../6711 --runtime bin/Debug
python -m unittest discover -s tests -p test_ancient_devil_audit.py
```

The installed-data audit, six parser/topology tests and three presentation-profile
tests pass. Tests cover missing
and duplicate target registrations, unrelated historical duplicates, truncated
and trailing index data, actor reservations, disconnected terrain, invalid
terrain encodings/dimensions and out-of-bounds actors.
The added profile tests reject missing/duplicate Ancient Devil sections and
missing/duplicate/changed expected fields without copying proprietary assets.

| Artifact | SHA-256 |
| --- | --- |
| Client ini/itemtype.dat | d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a |
| Active server database/itemtype-new.txt | 1ed7a23b7a34b5e3706277f97e421e3cdf0b269eeb5be7367a4ebc0e4f22b3ef |
| Server terrain database/maps/1082.map | b1974128a5de1efff2be6fa46482aa75ab46e6d4123e01332ce9117c2ef2574f |

No proprietary descriptions, terrain or index files are added to Git. Private
read-only database inspection code and the audit output remain under ignored obj.

## Implementation sequence and remaining decisions

1. Resolve certificate earning/exchange for all ten professions and the intended
   cross-class transfer path. Inspect installed quest/UI evidence and any lawful
   retained Revora content before selecting a Revora fallback policy.
2. Restore Exorcist admission, a valid return path, owned class trials and durable
   certificate acquisition. Keep NPC dialogue brief and in-world. Record explicit
   Revora choices for unpublished encounter/drop values; do not advertise parity.
3. Implement checked certificate exchanges and an owned summon transaction using
   nine distinct valid amulets. Reject duplicate-class substitutions and arbitrary
   client item types. Bind the resulting encounter to its durable summon and
   eligible participants. Define daily eligibility against the actual earning
   contract rather than charging all helpers on an unrelated admission.
4. Register a proven client-compatible Ancient Devil/guard encounter. Commit
   completion and once-only owned rewards with restart recovery. Award 11020 at
   the catalog's completed amulet-collection boundary, not merely a teleport or
   an unrelated generic monster death. Do not substitute 7006 Guild Beast.
5. Validate real class hunts, legal cooperation/transfers, nine-amulet collection,
   summon, combat, rewards and reload with ordinary players. Include stale items,
   insufficient space, replay, concurrent summons, failure/abandonment and restart.
6. Complete genuine-6711 NPC, travel, map, boss HP, drop and achievement acceptance
   before the applicable live deployment gates.

This research establishes the version-specific collection and terrain contract.
It does not close the earning path or justify a new achievement producer yet.

## Staged rule implementation

`AncientDevilRules` now implements the class/guard matrix, class certificate
exchange identity and an exact nine-distinct-amulet selection validator. The
isolated server build and `tests/AncientDevil/Verify-Rules.ps1` pass. The fixture
checks every supported promotion rank against all five guards, every unsupported
byte class, all ten choices of omitted amulet, order independence, duplicate
classes/UIDs, zero identities, unrelated items, nulls and wrong selection sizes.
It does not validate inventory ownership, persistence or a playable journey.

The following are explicit Revora restoration policies, not recovered retail
scripts:

- A successful trial earns the participant's own class certificate, regardless
  of which permitted guard issued the trial. That certificate exchanges for the
  same class amulet; choosing another guard cannot manufacture other classes.
- Windwalkers may use any of the five guards, extending the published permission
  for Ninja, Monk, Pirate and Dragon Warrior to the installed tenth class.
- Generic unspecialized Taoists must specialize before entering a class trial.
  Known profession ranges follow the existing server class predicates.
- Cross-class collection will use transferable amulets through ordinary checked
  player trade. Certificates remain personal earning evidence. This transfer
  path is a Revora policy inferred from the collection requirement, not verified
  original behavior; earning, provenance and transfer tests still need building.

The trial family comes from the selected guard (7001 Warrior, 7002 Trojan,
7004 Fire, 7005 Water, 7003 Archer). These are installed identities, not proof
that their existing shared spawns implement isolated trials. The eventual service
must bind the profession and selected trial to a durable attempt, check current
eligibility, and mint nothing on an unrelated monster death or class change.

The offering validator accepts exactly nine selected physical UIDs with distinct
approved amulet types. It intentionally does not inspect a whole bag or consume
an optional tenth amulet. The transaction service must separately revalidate
current ownership, eligible item state, provenance, daily entitlement and summon
exclusivity under the owner/database locks before consuming items. No dispatcher,
NPC, drop, exchange, summon, reward or achievement is enabled by these rules.

The next source prerequisite is the [staged trial transaction store](ANCIENT_DEVIL_STORE.md).
Its owned receipts, pending claims and atomic exchanges pass isolated database
checks; runtime encounters, earning, transfers and boss integration remain open.
