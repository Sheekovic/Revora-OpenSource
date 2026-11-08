# Wind Sage skill and scroll transactions

M6.55b2, reviewed 2026-09-16 against source `56c8fa7`. This is the ordinary
instructor service for existing scroll owners. The separate Ninja Epic Quest
earning route and rebirth rules now have source implementations and automated
validation; genuine-client acceptance remains open.

## Problem and resulting behavior

NPC 4720 previously removed a Twilight Scroll before attempting to learn
Twilight Dance. Repeated requests could waste scrolls. Other epic arts learned
the spell before removing the scroll, and several scythe options checked level
without checking the current class. These separate cache/database operations
could not provide an atomic exchange.

The seven reviewed options now use the canonical nearby ordinary character in
public Job Center 1004, bounded existing 2031/2032 dispatch, and current Ninja
eligibility. New skill, optional exact scroll debit, saved progression and
receipt commit in one owner transaction. No GM or character allowlist exists.
Existing learned arts return without spending or resetting level, EXP, previous
training or soul metadata. Projection uses the existing skill and inventory
packets only after commit. Database failures preserve payment and fence for
reload. Legitimate later relearning is not blocked by a permanent receipt key.

| Option | Art / logical spell | Minimum level | Minimum class | Cost |
|---|---|---:|---:|---|
| 60 | Bloody Scythe / 11170 | 40 | 52 | Free |
| 61 | Mortal Drag / 11180 | 20 | 51 | Free |
| 69 | Gaping Wounds / 11230 | 40 | 50 | Free |
| 80 | Twilight Dance / 12070 | 40 | 50 | Twilight Scroll 3004460 |
| 81 | Shadow Clone / 12090 | 50 | 50 | Shadow Scroll 3004459 |
| 82 | Super Twofold Blade / 12080 | 40 | 50 | Free |
| 83 | Fatal Spin / 12110 | 15 | 50 | Fatal Scroll 3004461 |

All options also require current class at most 55. Free pricing preserves the
existing Revora service. Bound scrolls are preferred over tradable copies; both
are valid. Locked, suspicious, equipped, stored, wardrobe or timed scrolls are
rejected. One unit is consumed, including the legacy zero-stack singleton
representation. The receipt records exact item UID/type, prior stack and binding.

## Primary and local evidence

The [official Ninja skill guide](https://co.99.com/guide/skills/m_ninja.shtml)
sets promoted Bloody Scythe at level 40 and Mortal Drag at 20. Installed spell
records agree. The four epic art levels also agree with the local catalog.
The source's previous level-20 Bloody Scythe gate was incorrect. The existing
promotion table maps the required rank to 52 at 40 and 51 at 15.

Gaping Wounds is less clear: the official page calls it a birth skill but its
table starts at level 40; the local level-zero record says 1. This change keeps
the existing instructor's level-40 requirement and adds class/ownership checks.
It does not claim to resolve automatic birth grants or retail timing.

The [official Ninja Epic Quest guide](https://co.99.com/guide/quest/2014/ninja_epic_quest.shtml)
identifies the three scrolls as Wind Sage learning materials. Existing source
and both local item catalogs agree on the numeric mappings above. Scroll use
does not require the quest's admission level; the scroll is the exchange
material, and no unverified personal quest-completion prerequisite is invented.

Read-only evidence SHA-256:

- Server `bin/Debug/database/itemtype.txt`:
  `594ffe5f252c9445a6087004372519a6f150f089c7bff42d4ec30400911a0e88`.
- Client `6711/ini/itemtype.dat`:
  `d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a`.
- Spell identities use the unchanged server/client hashes in
  [the catalog audit](REBIRTH_SKILL_AUDIT.md).

Catalog content was read locally; no proprietary files or raw catalog rows are
committed. No new native packet interpretation or capture is claimed.

## Validation and remaining work

The isolated build passes. `-NinjaTrainingOnly` passes **408 checks**, including
310 existing promotion/Assassin checks and **98** new Wind Sage checks. These
cover actual ordinary NPC/continuation dispatch at installed coordinates, all
seven learning boundaries, both bindings, zero/single/stacked payments, bound
preference, full reload and repeated/concurrent requests. Invalid class, rank,
map/session, item state and framing cannot grant an art or spend its material.
Suppressing every participating write rolls back skill, payment and receipt;
external profile, item ownership, stack, binding and skill conflicts are fenced.

Scrolls in these fixtures are deliberately seeded owned items. The tests prove
the instructor exchange, not an Epic Quest earning path. Genuine-client UI and
combat acceptance and production deployment remain open. The separate earning
and inheritance follow-ups now provide their own runtime evidence. The
[official second-rebirth guide](https://co.99.com/guide/quests/2rebornskill1.shtml)
states that only Super Twofold Blade among the four new arts is retained;
the [inheritance follow-up](REBIRTH_SKILL_AUDIT.md) now implements the specific
exceptions across first/second/paid rites and clears active summons after commit.

The full earning route now has an [implemented trials, hunting and validation report](NINJA_EPIC_QUEST.md). The ordinary paid and hunted-material runtime routes now pass combined earning, conversion and reconnect fixtures; genuine-client acceptance remains open.
