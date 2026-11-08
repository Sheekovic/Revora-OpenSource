# Fixed weapon-skill achievements

Baseline `1a56200`, 2026-09-15. M6.24 restores sixteen achievement producers
for ordinary players who save the corresponding fixed weapon skill. These are
distinct from the weapon proficiency achievements restored in M6.5.

Follow-up [M6.25](WEAPON_SKILL_BOOKS.md) makes learning from the sixteen
matching books atomic. Native purchase acceptance, shop transactions and some
historical learning requirements remain open.

## Evidence

The [official weapon-skill guide](https://co.99.com/guide/skills/m_weaponsk.shtml)
identifies the fixed levels of the active and passive skills. The installed
6711 achievement descriptions require the named skill to be fixed. Read-only
comparison of the client and server skill catalogs matches each of the sixteen
IDs, level sequences, EXP thresholds and character-level requirements. Official
web tables have some different character-level requirements; the installed
6711 catalog controls training eligibility here. No new packet layout is claimed.

| Achievement | Skill ID | Skill | Fixed level |
| --- | --- | --- | ---: |
| 10504 | 1045 | Fast Blade | 4 |
| 10505 | 1046 | Scent Sword | 4 |
| 10506 | 5030 | Phoenix | 9 |
| 10507 | 5040 | Boom | 9 |
| 10508 | 1300 | Halt | 9 |
| 10509 | 5020 | Stranded Monster | 9 |
| 10510 | 1260 | Speed Gun | 9 |
| 10511 | 1290 | Penetration | 9 |
| 10512 | 5010 | Snow | 9 |
| 10513 | 1250 | Wide Strike | 9 |
| 10514 | 5050 | Boreas | 9 |
| 10515 | 7000 | Seizer | 9 |
| 10516 | 7010 | Earthquake | 9 |
| 10517 | 7020 | Rage | 9 |
| 10518 | 7030 | Celestial | 9 |
| 10519 | 7040 | Roamer | 9 |

Local evidence SHA-256, retained outside Git:

- `6711/ini/Achievement.dat`:
  `DF78DD498353B0B089F9B4228CADA5D79F4547AAE7B705349816671B2C29EBFC`.
- `6711/ini/MagicType.dat`:
  `618431607E8DB81F5E8C6093D801329B73BD9A5AF4AA57FC8A38348A832173C2`.
- `bin/Debug/database/magictype.txt`:
  `97ECB413566F5FEA541090D1A1C1498ED2ACF0874EFA6056984583C65C187A71`.

## Persistence and training

The final training update immediately saves the owned skill batch. Each receipt
is written in that same transaction, after checking exactly one persisted row
at the reviewed terminal level. A catalog with gaps, a changed terminal level
or a nonterminal final EXP threshold cannot establish mastery. The achievement
cache refreshes after commit. Existing eligible owners with a valid fixed skill
receive the receipt on a successful normal skill save; an unsaved cache value
alone cannot grant it. Earned achievements remain historical if a skill is later
forgotten.

Receipt failure rolls back the entire skill batch and fences the character until
reload. The production loader rejects saved levels that the packet model would
otherwise silently clamp into a valid maximum. Invalid rows are retained for
operator review rather than rewritten as earned mastery. Fake adventurers,
incomplete logins and derived rune skills cannot create these receipts.

Skill EXP multiplication and accumulation now use wide arithmetic capped at the
next threshold. Rate, gem, training-ground division and VIP operation order are
preserved, including integer rounding. One update still advances at most one
level and discards excess EXP at that threshold. Intermediate skill EXP keeps
the existing periodic-save behavior. Missing next-level definitions cannot
advance a skill.

## Validation and remaining work

The isolated x86 build passes. The EXP fixture passes 295 checks, including 67
new checks for the sixteen exact mappings, ordinary final training, production
reload, historical retention, failed transactions, concurrent recovery,
ineligible actors, invalid saved levels and overflow-safe training arithmetic.
The 27 quest/event regression modes pass 1,572 checks; three producer-audit and
twelve private-overlay checks also pass (1,882 total).

Validated assembly SHA-256:
`6C1F0C4DCC2A22C39330BF047EC83F5F2D5DE472D1295537F6E122922F316D70`.

Source coverage is now 97 of 337 achievements. This is not a demonstrated
320-achievement route. Native achievement display, acquisition of every
historical weapon/book and complete transactional book consumption remain
separate work. No owner/GM restriction is added to training or earning. No
client content, live server or public patch is changed by this milestone.
