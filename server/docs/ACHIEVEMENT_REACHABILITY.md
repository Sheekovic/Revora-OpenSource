# Achievement reachability checkpoint

Updated 2026-09-30 through the complete equipment quest journey, earned 50-mount collection and approved Supervisor earning. This is a source and prerequisite audit,
not a live-client acceptance result.

The [numeric producer inventory](research/ACHIEVEMENT_PRODUCER_INVENTORY.csv)
accounts for all 337 defined achievements: **334** have source producers and
**3** do not. Rising Star requires 320 distinct verified achievements. Source
count alone now exceeds that threshold, but a compatible 320-achievement career
remains unproven. Historical client bits do not establish earning or availability.

The new [career gap ledger](research/ACHIEVEMENT_CAREER_GAPS.json) separates
known unavailability from incomplete supplier evidence. Current arithmetic is
337 minus three missing producers, disabled Elimination 11010, unavailable Asura
Hell admission 10726 and catalog-impossible 200 mounts 11232: **331 is only an
upper ceiling after those known exclusions**. With three additional unproven
collection thresholds and four unproven travel admissions withheld, **324 entries
remain in the planning pool**, leaving four above the target. Those 324 are not marked reachable,
compatible or earned. An additional undiscovered conflict can shrink that pool;
verified new suppliers can restore the three withheld collection candidates.
Ordinary admission evidence is also required for maps1360/1361/1362/1364;
see the [bounded route review](TRAVEL_ADMISSION_GAPS.md). Terrain support does
not establish a playable entrance, and these four routes are not classified
as permanently unavailable.
Pirate normal-startup earning restores the two weapon-supplier candidates.
Production melee now earns Pirate11322/11323 and Taoist11222 with quest-awakened
weapons, with exact proficiency and achievement persistence after restart.
The documented fixtures control eligibility, equipment enhancements, proximity
and cooldown; these are not a compatible320-achievement character journey.
See the [loaded weapon-supply audit](MASTERY_WEAPON_SUPPLY.md) and the
[Taoist restoration contract](TAOIST_EPIC_RESTORATION.md).

Reproduce with `python tools/audit_achievement_career.py --output
 docs/research/ACHIEVEMENT_CAREER_SUMMARY.json --check` (one command line).
The audit derives missing producers from source, rejects double-counted IDs and
requires reviewed evidence hashes to match. A changed source/document requires
classification review, not blindly copying new hashes. The [generated summary](research/ACHIEVEMENT_CAREER_SUMMARY.json)
keeps `compatible_career_verified=false`; it is not a completion percentage.


Flame Lit adds owned Dragon Rune completion 11016; Ancient Devil adds legitimate
nine-class-amulet collection 11020. The table below records earlier producer
families; the numeric inventory is authoritative for the full current list.
Elimination 11010 now has a source producer, but `EliminationLifecycle.ReleaseReady`
remains false. Its [ordinary-player acceptance](ELIMINATION_ACCEPTANCE.md) is
pending; this source count must not be presented as 334 playable achievements.

The [missing-producer work inventory](research/MISSING_ACHIEVEMENT_WORK.json)
partitions all three missing identities into three work packages. It is generated
from the current source inventory and the hash-verified installed client catalog.
It contains numeric identities and implementation prerequisites, not copied client
descriptions or invented original rules. Run
`python tools/audit_missing_achievement_work.py --client ../../6711 --output docs/research/MISSING_ACHIEVEMENT_WORK.json --check`
to verify it. The ten equipment-set identities now have approved definitions and
ordinary earning/reload coverage; see [the implementation contract](EQUIPMENT_SET_ACHIEVEMENTS.md).
Remaining producers are arena occupation10302, Champion11123,
and newcomer aggregate10930. Supervisor10224 now uses the approved documented
Revora category ranking, with ordinary donation/gift, transaction rollback and
reload checks; native acceptance and deployment remain pending. Equipment aggregate 10931 now has a complete
19-chain private earning and restart check; see [the contract](EQUIPMENT_QUEST_AGGREGATE.md).
The remaining unresolved rules must
not be replaced by arbitrary grants. No package is complete merely because its
requirements have been classified.

The [permanent collection checkpoint](COLLECTION_ACHIEVEMENTS.md) adds ten threshold
producers. First- and ten-design routes in both closets are earned in the ordinary fixture;
the fifty-garment route is also earned. The [fifty-mount route now passes](COLLECTION_FIFTY_MOUNTS.md); the 100/200-design routes remain unverified, and 200 mounts are impossible with the current compatible
catalog. These limitations are included in the source-versus-earning distinction.

The [loaded supplier audit](COLLECTION_SUPPLIER_CAPACITY.md) finds only 82 garment
and 20 mount designs across the CP mall and Vesper. Those suppliers alone cannot
support the next 100-garment/50-mount milestones. Other event/pack outputs need
an explicit acquisition inventory; more catalog-only checks cannot close this gap.
The current union including atomic mount packs, Planet and Easter reaches a
potential 92 garments and 52 mounts. The combined 50-mount earning route now passes;
the 100-garment supplier gap is still eight designs.

The [equipment checkpoint](EQUIPMENT_ACHIEVEMENTS.md) adds five saved-state
producers. Garment, Fan/Tower and accessory routes are earned; the latest accessory
fixture explicitly covers the left slot after correcting its classification. The accessory route continues Terato loot
through equipment and production login recovery. The talisman route spends the
original boss-funded purse in the existing CP mall and survives production reload.

The travel service accounts for 46 of those calls. Forty-five have reviewed terrain/setup,
including two fixed world aliases and restored garden/Grotto routes; one destination gap and the full ordinary-route audit remain open.
Their presence in the source inventory does not mark all destinations complete.

| Current producer | IDs |
| --- | --- |
| First refinery application | 10431; [committed effect and material transaction](REFINERY_ACHIEVEMENT.md) |
| Mutual marriage | 10129; [consent and atomic spouse links](MARRIAGE_ACHIEVEMENT.md) |
| Twenty daily Arena entries | 11111; [registered admissions on a saved Pacific day](ARENA_ACHIEVEMENT_CHECKPOINT.md) |
| Solo Arena class victories | 11101-11109, 11309; [captured opponents and recovery](ARENA_ACHIEVEMENT_CHECKPOINT.md) |
| First nine Arena wins | 11112; [ordered results and cold recovery](ARENA_ACHIEVEMENT_CHECKPOINT.md) |
| Owned guard summon | 10119; [mined ore, instruction and registered summon](GUARD_SUMMON_ACHIEVEMENT.md) |
| Observed silver, mount and guild state | 10106, 10107, 10108, 10218, 10219 |
| Mutual mentor admission | 10124 and 10125, distinct role receipts from [accepted admission](MENTOR_ADMISSION.md) |
| Mutual trade-partner admission | 10126, saved for both players by [the probation transaction](TRADE_PARTNER_ACHIEVEMENT.md) |
| Mutual friend acceptance | 10123, saved for both players by [the consent transaction](FRIEND_ACHIEVEMENT.md) |
| Owned cosmetic changes | 10113, 10114, 10116; [committed selection](COSMETICS_RESTORATION.md) |
| Worn armor dye | 10115; [paid visit and dye transaction](ARMOR_DYE_EARNING.md) |
| Accepted travel visits | 46 numeric regions; [runtime scope and remaining destination gap](TRAVEL_ACHIEVEMENT_RESEARCH.md) |
| Virtue-to-Meteor exchange | 10110; [Shelby's actual reward transaction](VIRTUE_REWARD_EARNING.md) |
| Observed level and Virtue thresholds | 10310, 10311, 10317 |
| Observed Battle Power thresholds | 10312, 10313, 10314; [canonical mentor/clan sharing](BATTLE_POWER_SHARING.md) |
| Weapon mastery | 33 IDs in the [reviewed mapping](WEAPON_MASTERY_ACHIEVEMENTS.md) |
| Fixed weapon skills | 10504 through 10519 in the [reviewed skill mapping](WEAPON_SKILL_ACHIEVEMENTS.md); committed skill and receipt required |
| New subclass P1/P9 transitions | 14 IDs in the [subclass contract](SUBCLASS_PROGRESSION.md); legacy-holder migration pending |
| Atomic rune equip transitions | 11310, 11311; one and five valid slots within a loadout |
| Full-level rune class achievements | 11312-11321; [fifth maximum equip or equipped upgrade](FULL_LEVEL_RUNE_ACHIEVEMENTS.md), current class and one complete set |
| Trainer class promotions | 50 IDs across ten class paths; [atomic promotion and offering routes](CLASS_PROMOTION_EARNING.md), including the shared Taoist first promotion |
| First rebirth | 11030 and 10315; [one committed stone rite](FIRST_REBIRTH_COMPLETION.md), distinct quest and growth evidence |
| Second rebirth | 10316; [committed token rite and exact recovery](SECOND_REBIRTH_RESEARCH.md); separate Abyss quest 11031 remains open |
| Local CTF objectives and result | 10230, 10301 |
| Demon Exterminators | 11001 |
| Dis City stage turn-ins | 11002, 11003, 11004, 11005; combat, treasury, exchanges and daily ranks implemented; difficulty variants and native acceptance pending |
| First Study Point award | 10319; [eight committed quest/boss reward sources](STUDY_REWARD_ACHIEVEMENT.md), not a raw balance or pending entitlement |
| Owned boss kills | 10526 and 10529; [committed Terato/Spook victories](OWNED_BOSS_ACHIEVEMENTS.md), actual killer or registered companion owner |
| Terato Dragon | 10932; [canonical fifth-floor boss kill and reward](TERATO_DRAGON.md) |
| Release the Souls | 11012 |
| Power Arena | 11013, 11014 |
| Snake Islands | 11018 |
| Sky Pass | 11019 |
| Moon Box final box choice | 11022 |
| Simon's classic Ancestor Box exchange | 11027 |
| Actual local Arena admission | 11032 |
| Revora NPC Quiz Show | 10120, 11128 |
| Weekly/monthly PK fields and champions | 11116, 11117, 11118, 11119 |
| Class PK champion | 11131 |
| Couple PK NPC edition: admitted field and pair championship | 11115, 11121 |
| BP PK contested admission | 11114; [owned server route and token claims](BP_PK_IMPLEMENTATION.md), original halo/native acceptance pending |

The source audit recognizes the reviewed observation literals and direct
`ProgressionEvidence.Complete` calls in the feature tree. It rejects a newly
dynamic call until reviewed. It is not a general semantic analyzer: changes to
the central evidence writer, aliases, direct database writers or producer
architecture require a manual audit too. Current production writes were checked
against the central evidence table; test-only fixture grants are excluded.

## Recruit and equipment prerequisites

The [numeric prerequisite audit](research/QUEST_PREREQUISITE_AUDIT.json) is
hash-bound to the already-reviewed `Questinfo.ini`. It contains 797 records but
only 790 distinct mission IDs. Seven IDs occur twice. A dictionary that silently
chooses the last row would conceal edition conflicts.

Recruit category 1 contains 72 records, tutorial category 2 contains nine, and
equipment category 4 contains 150. These counts are not an achievement completion
contract. Specifically:

- Recruit records mix older class-specific introductions, another numbered
  introduction chain, and the later 3478â€“3481 newcomer sequence. Their listed
  predecessors include seven missing IDs: 601, 603, 604, 605, 800, 806 and 6457.
- Several recruit rows restrict participation to level 50 or lower. A level-140
  character cannot simply be sent through every listed row without selecting
  the intended edition and recovery rules.
- Class alternatives must remain alternatives. For example, the stored class
  filter for mission 661 and its Warrior destination disagree. Neither its name
  nor one NPC visit proves the correct class route.
- Eighteen equipment rows, 950â€“967, have blank names and no useful NPC route.
  Treating every TypeId=4 row as a required quest would include these placeholders.
- Mission 1834 follows 1833 but has maximum level 65 while the preceding step
  requires level 98. That breaks a normal nondecreasing-level traversal. This is
  a diagnostic, not a claim that rebirth could never lower a character's level.
- The audit reports listed edges without asserting the native `Prequest` branch
  semantics or inventing a replacement for absent rows.

The [official achievement guide](https://co.99.com/guide/guides/achievement.shtml)
separates quest completions from character growth and event honors. It does not
define the exact terminal mission set for 10930 or 10931. Newcomer aggregate
10930 remains pending. A [reviewable Revora proposal](NEWCOMER_ACHIEVEMENT_PROPOSAL.md)
now isolates the unique installed3478-3481 introduction chain; the numeric audit
does not certify its original aggregate meaning, and owner selection remains open. The owner-approved Revora equipment edition now records
10931 from all 19 terminal receipts, with private earning and reload validation.

The [official Azure Pill guide](https://co.99.com/guide/quests/pill.shtml) provides
a concrete equipment route and says the chain resets after rebirth. Its current
PC reward/claim details differ from the older iPad page. The
[official Power Book guide](https://co.99.com/guide/quests/powerbook.shtml) requires
its own Trial Cave objectives, timed kills and item interactions. Merely finishing
the newly restored classic Labyrinth does not finish that separate quest.

## Rebirth and remaining producers

Current rebirth changes items, character attributes, class, skills and rebirth
count through multiple writes in `GameState.Reborn`. NPC branches consume the
rebirth item afterward; the second route accepts an Exemption Token. Neither a
rebirth count nor that shortcut establishes completion of the distinct quest
achievements 11030/11031. Their restoration needs a selected quest route and an
atomic final transition, including ordinary-player failure/reload tests.
The [M6.52 material prerequisite](FIRST_REBIRTH_MATERIALS.md) now provides
Waterlord Clean Water earning and atomic seven-gem stone crafting; these do not
by themselves record a rebirth completion or prove the entire gem supply route.

Growth predicates, equipment/proficiency milestones, travel regions, class and
subclass progression, interactions, combat objectives, local PK events and the
remaining quests all need dedicated producers. For instance, the current battle
power calculator's duplicated `ExtraBattlePower` and uncapped guild contribution
are now corrected. Clan and mentor sharing now use validated source margins and live canonical
eligibility; three threshold achievements are enabled. Exact unpublished mentor
ratios and native presentation are not claimed as verified. See the
[BP sharing checkpoint](BATTLE_POWER_SHARING.md).

Next implementation sequencing: finish BP PK halo acceptance; add reviewed character-growth and interaction
producers; select and implement complete recruit/equipment/rebirth quest editions.
The inventory records source calls. A destination or milestone is complete only
after its ordinary-player earning path, durable evidence and replay/failure
checks are verified. Travel destinations with missing access remain pending.

Five audit checks pass: sparse IDs and catalog drift, inventory/source consistency,
duplicate/absent prerequisite detection, and level-reversal detection. This
documentation batch does not require a Host build or deployment.

The [exploration-region checkpoint](TRAVEL_ACHIEVEMENT_RESEARCH.md) identifies 76
native rectangles for 46 active travel achievements and tests their geometry.
That initial geometry checkpoint preceded the integrated travel producers. The
current inventory includes their 46 identities; ordinary access to every
destination and native acceptance remain separate open gates.


## Arena occupation identity gap (2026-09-26)

The hash-bound installed Achievement.dat identifies 10302 as an arena occupation
objective with icon key Achievement_OccupySupreme. This establishes the identity,
not an event trigger, recipient rule or timing. It is now separated from the
guild-office work package in the missing-producer audit so completing automatic
offices cannot be mistaken for completing this objective.

Bounded searches of the official site did not establish an exact achievement
mapping. The official CTF guide describes base occupation, while Guild War also
uses an arena; neither similarity proves this identity. The local disabled
PoleDomination implementation is likewise insufficient evidence. No receipt is
added to these unrelated results. Required evidence is an original achievement
trigger mapping or an explicit documented Revora event decision, followed by
ordinary-player occupation, contested result, persistence and replay checks.
This leaves 10302 open; the source count remains 320/337.


## Proposed Revora occupation boundary (2026-09-27)

A new owner question proposes 10302 for eligible winning-guild participants in a
completed regular local Guild War. This decision remains pending; no producer or
new grant is enabled. The earlier original-trigger uncertainty remains unchanged.

The existing implementation supplies a concrete authoritative boundary:
`GuildSeasonWar` commits `revora_guild_battles.state='won'` and the final keeper
with `GuildWarPurses.Freeze`. The latter saves each eligible ordinary winner's
UID and guild in `revora_guild_purses`, then marks `purses_frozen`. Its snapshot
requires the canonical authenticated session and winning guild, presence on map
1038 at finalization, and a recognized rank. Fallen eligible members count;
absent members and a later guild join do not. No new personal-damage minimum is
proposed: that would change the existing support-member eligibility policy.

If approved, record 10302 for each frozen recipient within that same final-result
transaction. Use the battle day as the source key. Recovery must join a won,
frozen battle to its matching keeper/guild recipient row and character UID;
never reconstruct old participation from current guild membership or map position.
Purse collection is not required, so full bags and delayed payouts cannot prevent
credit. Legacy sealed results without recipient rows remain unproven. Preserve
separate purse achievement 10228 and all reward/leadership rules.

Validate winning leader/member/fallen support versus absent, stale, fake and
losing participants; empty/cancelled battles; leadership changes; atomic rollback;
replayed settlement and login; and ordinary capture/finalization followed by
process restart. This would be a documented Revora definition, not proof of the
original meaning of Achievement_OccupySupreme. Native and deployment gates stay
open. Source reviewed at `4cfd8ee5`; current inventory remains 332/337.
