# Durable weapon-skill book learning

Baseline `85f2ae7`, 2026-09-15. M6.25 replaces sixteen legacy book-use handlers
that removed an item before saving the learned skill. Known skills now preserve
the book; a new skill and consumption of exactly one selected book commit
together. All eligible ordinary players use this path.

[M6.26](SILVER_OWNERSHIP.md) adds the shared silver-balance ownership
prerequisite for the remaining shop purchase transaction.
[M6.27](WEAPON_BOOK_SHOP.md) completes the reviewed books' purchase transaction;
native acquisition acceptance and inherited silver revision gates remain open.

## Reviewed identities and learning rules

The installed 6711 item catalog matches the following book identities to the
skill identities reviewed in [M6.24](WEAPON_SKILL_ACHIEVEMENTS.md). The
[official pharmacist guide](https://co.99.com/guide/npcs/pharmnpc.shtml) provides
eleven weapon-book learning requirements and their 5,000-silver prices.
Fast Blade and Scent Sword retain their existing character-level-40 minimum,
also supported by the [weapon-skill guide](https://co.99.com/guide/skills/m_weaponsk.shtml).
Learning requirements and subsequent training thresholds are distinct.

| Book ID | Skill | Learning requirement |
| --- | --- | --- |
| 725005 | Fast Blade | Character 40, applicable proficiency 5 |
| 725010 | Scent Sword | Character 40, applicable proficiency 5 |
| 725011 | Wide Strike | Glaive proficiency 4 |
| 725012 | Speed Gun | Spear proficiency 4 |
| 725013 | Penetration | Dagger proficiency 4 |
| 725014 | Halt | Long hammer proficiency 4 |
| 725026 | Snow | Wand proficiency 4 |
| 725027 | Stranded Monster | Halberd proficiency 4 |
| 725029 | Phoenix | Character 30; no extra proficiency gate |
| 725030 | Boom | Hammer proficiency 4 |
| 725031 | Boreas | Poleaxe proficiency 4 |
| 725040 | Seizer | Existing unrestricted Revora learning |
| 725041 | Earthquake | Existing unrestricted Revora learning |
| 725042 | Rage | Existing unrestricted Revora learning |
| 725043 | Celestial | Existing unrestricted Revora learning |
| 725044 | Roamer | Existing unrestricted Revora learning |

The last five are absent from that published pharmacist table. Their preserved
Revora rules are not certified original-CO requirements. Applicable proficiency
IDs come from the matching loaded 6711 skill definition, including its supported
epic-weapon alternatives. Subsequent training continues to use the installed
catalog's character-level and EXP requirements. Neither learning a book nor
owning a weapon grants a fixed-skill achievement.

Read-only comparison finds all sixteen books in both server and installed-client
shops 141 and 423, using silver. The server item catalog prices each at 5,000.
This confirms catalog availability, not a native purchase acceptance test.
No catalog content or prices are changed in this milestone.

Local evidence SHA-256, originals retained outside Git:

- Client `6711/ini/itemtype.dat`:
  `D49F0658BF01387B99C284DDEC81713838F2EAE3ADB8E63666E5C906F63CF87A`.
- Client `6711/ini/Shop.dat`:
  `DAA33B6DA34371C37C2B6C615CA3F2D08471539B7A4514BF19843F6A4D4B0CA1`.
- Server `bin/Debug/database/shops/Shop.dat`:
  `9CBF67CF0EDF03A781A50D50F581544878BA352A3019CC91CB14B411F21552F3`.

## Ownership, recovery and client projection

The service requires a canonical fully loaded player, the exact inventory item
reference, no trade or booth, and an unlocked, unexpired inventory book. It
holds the character item lock and database owner-row lock. Required character
levels must qualify in both cache and durable storage. A required proficiency
must likewise qualify in cache and a unique saved row, read under a shared row
lock. Pending unsaved training defers use without spending the book.

One transaction inserts the absent level-zero skill and deletes or decrements
the exact owned book, comparing its owner, type, stack, bound state, location,
lock and expiry metadata. A stale item, existing durable skill or failed write
rolls back everything and fences the character for reload. A committed skill
is installed and sent only after commit, alongside the inventory update.
Repeated use sees the learned skill and preserves remaining books. Concurrent
book use and periodic skill saves share the same owner lock.

Bound and unbound books work, including stacks and the legacy zero-stack
single-item convention. Learning requires no free inventory slot. Timed books
must have matching durable expiry metadata. Read-only schema inspection found
`items.days` stored as text and `daystamp` as bigint; the parameterized predicate
matches that actual schema without casting arbitrary text. The bounded Revora
x64 metadata inspector is ignored at
`obj/chi-refill-validation/WeaponBookInspect.exe`, SHA-256
`D6CBCF0CA6690906590EFD60A08A2BD114E716664F36F79FDDC191897A099155`.
Its transaction was read-only and emitted column names/types only.

## Validation and remaining gates

The isolated x86 build and 349 EXP/skill/book checks pass, including 54 new
book checks. They exercise every book identity, exact unit consumption,
production skill saves and reload, eligibility boundaries, failed transactions,
stale state, concurrent use, temporary restrictions, expiry, full inventories
and book-to-fixed Fast Blade training. The 27 quest/event regression modes pass
1,572 checks; three producer-audit and twelve overlay checks also pass
(1,936 total).

Validated assembly SHA-256:
`0D9A485871969D4C25E3CA7B6780F2175F313DB73648142A46BA082FA59DFD79`.

The existing generic shop purchase path still inserts items and deducts silver
through separate writes, with unchecked price multiplication. Its transaction,
quantity bounds and native purchase acceptance remain prerequisites to closing
the full acquisition audit. The old handlers for other spell books are outside
this reviewed set and remain pending. Exact original learning requirements for
the five unlisted books also remain open. This milestone changes no packet
layout and is not deployed or installed as a client patch.
