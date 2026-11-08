# Forgotten Realms expedition — Revora edition

Owner approved 2026-10-01. These are documented Revora rules, not claims about
the original quests associated with these terrain names.

Rowan at Twin City (319,299) offers five destinations to ordinary level100+
characters. No VIP, GM or character allowlist is required; entry is free.
Defeat ten Lost Sentries in a land, then claim one bound universal rune4030101
from its Trail Keeper. Each land pays once per character per UTC week, resetting
Monday at00:00. Maximum five runes per week; no P7 rewards. Players may revisit
completed lands. After a week changes, return to Rowan to accept the new hunt.
Every Trail Keeper offers a free return to Rowan, regardless of hunt progress.

| Map | Land | Landing | Trail Keeper |
| --- | --- | --- | --- |
|1360|Deserted Wild|290,294|288,294|
|1361|Flame Plain|278,278|276,278|
|1362|Death Cave|340,340|338,340|
|1363|Asura Hell|280,280|278,280|
|1364|Corpse Forest|338,338|336,338|

Ten registered Lost Sentries are added per available land, on connected,
unoccupied terrain within25tiles of the landing, initially at least12tiles
away. They have100,000HP,1200–1800 base attack, inherited AlienSerpent defense
and a45second respawn. These are ordinary monsters, not bosses; actual player
damage depends on defenses. Existing catalog populations are retained.

Terrain files are hash-checked. Missing terrain keeps its destination closed.
An unexpected terrain/profile or NPC collision fails startup instead of silently
placing an unsafe route. The guide occupies a separate tile from the landing.
The normal map timer owns respawning and resets per-life kill deduplication.

`revora_forgotten_realms` retains admission, capped kills and exact reward UID
by character/map/week. Only registered sentries killed by the same canonical,
nearby ordinary player count. Repeated callbacks do not reuse a death. Reward
claims hold the character and inventory locks; gift and unique receipt commit
together. Full bags, missing progress and replay preserve the entitlement.
Uncertain failures fence the session for reload. UTC week conversion is explicit
in SQL so a database session time zone cannot shift the stored week date.

## Private validation

Build SHA-256:
`102604CE650FAD38027D5FA07EEFAB57D1A0CFB887217AFF9F5D912F6748627D`.
The existing build warnings remain. The x86 focused helper compiles with warnings
as errors. **49 parent checks and eight fresh-process checks pass.**

The fixture copies only selected public catalog rows into its disposable schema,
loads the five maps through the production Map constructor, then uses the normal
NPC dispatcher for entry, claim and return. All five actual world transitions
earn travel receipts through `TravelProgress`; no completion receipts are seeded.
It exercises actual monster deaths, ownership/distance/level checks, full bags,
injected reward rollback, parallel claims and replay. A separate process reloads
the four earned rewards, refuses their replay, finishes a partial fifth hunt and
claims its rune. Exact receipt/item ownership remains saved afterward. A new
week starts empty, and the production map timer revives a sentry whose next life
earns a new kill.

Controls: supplied ordinary character profiles and level, clock, hunt proximity,
direct monster death calls and advanced respawn time. Unrelated random CP/gold
drops are disabled. This is not combat balance acceptance, authenticated login,
full Server.Run, PostgreSQL restart or native navigation/rendering. Existing
session-lease ownership is relinquished before the recovery child begins.

Tests: `tests/PotencyHunt/ForgottenRealmsTests.cs`, runner switch
`-ForgottenRealmsOnly`. The private wrapper uses loopback PostgreSQL25439,
`revora_candidate_game` and disposable schema `revora_potency_validation`.
It temporarily copies the exact previously reviewed1363 floor into the private
candidate and removes only that unchanged copy in finally. No production data,
runtime, client index or server index is changed.

## Remaining release gates

The [1363 map/index repair](GLOOMY_JAIL_STAGE.md) remains staged. Its server floor
and coordinated client/server indexes need release integration; native acceptance
is explicitly pending. The private test constructs this world from its staged
floor and does not prove native map loading. Achievement10726 therefore stays
unavailable in the installed-environment planning audit. The other four maps now
have a tested ordinary admission route; their former admission-only exclusions
are removed. No live deployment or restart is included in this batch.
