# Local Arena participation

Implemented 2026-09-15 UTC. Code and isolated validation only; not deployed.

Achievement 11032 is earned by entering a real local Qualifier fight for the
first time. The installed 6711 achievement predicate establishes participation,
not victory, a queue request or a daily match count. The client evidence is the
same private `Achievement.dat` recorded in the quest/event research, SHA-256
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.

The [official Qualifier guide](https://co.99.com/guide/quests/arena.shtml), read
2026-09-15 UTC, establishes level 40 eligibility, opponent matching, acceptance
and separate spectator access. Starting an accepted fight after both transfers
succeed is Revora's explicit interpretation of participation. This does not
claim every rule in the existing Arena matches that guide's historical edition.

## Earning path and persistence

Use the normal Qualifier interface, accept the matched opponent and enter the
assigned dynamic arena. Two distinct, canonical, fully loaded ordinary players
at level 40 or above must both have accepted and be in the fight state. The
registered group, each player's group reference and their exact map objects
must agree with the arena created by that group. Fake characters, disconnected
sessions, watchers, failed transfers and self matches supply no evidence.

`QualifierGroup.Import` performs the existing transfers and fight setup before
calling `ArenaParticipation.RecordStart`. An import guard prevents concurrent
calls from allocating and moving the same group into multiple maps. Its map
check now requires both players to reach the owned arena: previously, two failed
transfers leaving players together in a city could pass the check.

`revora_arena_participation` keeps one immutable first-entry receipt per character,
including opponent, match UUID, timestamp and rule version. Both receipts and
achievement 11032 commit together in stable character-ID order. Later matches do
not overwrite the first receipt; a new opponent can still earn theirs. No items
or currencies change. Achievement and wardrobe projections refresh after commit.
Ordinary achievement loading restores the saved result after reconnect.

A persistence failure rolls back both participants' writes and fences further
actions with a reconnect message. A subsequent legitimate match can earn any
missing receipt. The running group also supports an idempotent retry while all
eligibility conditions still hold; no player request exposes that writer.

## Validation and limits

The staged build and `Verify-PotencyHunt.ps1 -ArenaOnly` pass 23 checks against
the disposable PostgreSQL schema. Tests invoke the real Qualifier import,
dynamic map constructor, transfers and post-start bridge. Coverage includes
ordinary level-40 players, same-match identities, concurrent import/replay,
achievement reload, spectators, repeat opponents, fake/under-level/unaccepted
competitors, wrong state, ended/unregistered/stale groups, outside-map and
disconnected players, failed transfers, second-receipt rollback and self matches.

Regression modes: 63 progression, 91 wardrobe and 78 Elite PK checks. No packet
layout was changed or newly certified. This bridge does not certify the legacy
Arena's ranking, currency settlement, matchmaking timing or damage accounting.
Real-client signup, acceptance, fight entry, achievement notification and
reconnect acceptance remain pending, together with deployment.
