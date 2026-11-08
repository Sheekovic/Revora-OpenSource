# Spook named attacks

Implemented against `626189c` on 2026-09-16. The owned Exorcism hunt now uses
three named attacks with server-enforced warnings, damage areas and a brief
stun. Native effect/HUD acceptance, original terrain and exact historical
numbers remain open. No client or production server was deployed.

## Evidence and baseline correction

The [official Thrilling Spook guide](https://co.99.com/guide/monsters/thrilling_spook.shtml)
describes Soaring Leap as a ranged attack that stuns its target, Bloody Battle
as ranged damage, and Surging Shake as heavy damage. It does not establish exact
server damage formulas, footprints, cooldowns or difficulty-specific spell IDs.

The local server `database/magictype.txt` and approved client's
`ini/MagicEffect.ini` contain matching names at the following identities:

| Attack | Spell ID | Local evidence |
| --- | ---: | --- |
| Soaring Leap | 11321 | Server name/sort 43; client name, action 496 and a two-second intone field |
| Bloody Battle | 11310 | Server name/sort 35; client name and a six-second warning field |
| Surging Shake | 11309 | Server name/sort 42; client name and impact/earthquake metadata |

These are confirmed local catalog identities. They are not a captured native
cast or proof of a historical grade-to-spell mapping. Older 10360/10361/10362
server labels differ from the client's EarthTremble/EarthDrop/Titan labels.
Initialization validates the three selected level-zero server records. Existing
SpellUse and timed character-update builders are reused; no new packet family,
layout or effect asset is introduced.

The prior hunt's raw attack configuration did **not** prove normal defense
handling. Inspection found that `Handle.Execute` still entered the old
name-based Spook branch, applying random 1,000-2,000 damage and sometimes the
wrong stun effect. Its seventh random choice was also unreachable. That branch
is removed. The owned encounter now intercepts combat before legacy dispatch;
the new fixture verifies exact common-calculation damage and defense reduction.
The old documentation's stronger damage-calculation claim is corrected.

Inspected private file SHA-256 values:

- Server `bin/Debug/database/magictype.txt`:
  `97ECB413566F5FEA541090D1A1C1498ED2ACF0874EFA6056984583C65C187A71`.
- Client `6711/ini/MagicEffect.ini`:
  `7CB74AD7C9ED7FABB91DA5F0FF1C79CA97BD344F0EEC7D2D7B7545AEE70D411F`.

## Server behavior

The boss rotates through Leap, Bloody Battle and Shake. Every attack warns the
admitted party and fixes its threatened location when preparation starts.
It begins a cast only against a live admitted player within twenty tiles.
Only the registered boss and its current owned instance can resolve it.

| Attack | Minimum warning | Revora footprint and consequence |
| --- | ---: | --- |
| Soaring Leap | 2 seconds | Only the marked player, within radius 2 of the marked location; normal physical damage and a 2-second stun |
| Bloody Battle | 6 seconds | Admitted players within radius 5 of the marked location; normal physical damage |
| Surging Shake | 2 seconds | Admitted players within radius 8 of the boss's original location; 150% of normal post-defense damage |

Each resolution has four seconds of recovery. The existing two-second world
sweep can resolve a pending attack after everyone retreats; leaving ordinary
melee range cannot freeze an already-warned attack. Movement during preparation
is delayed. Retiring or defeating the boss cancels its cast. The geometry,
rotation, recovery and scaling are explicit Revora balance choices, not claims
about exact original CO numbers.

All three attacks use the common monster physical calculation, including its
class/level defenses and mitigation, with final damage capped at remaining HP.
Lethal damage uses normal player death. `Attackable` invulnerability, dead or
replaced sessions, departed teammates and outsiders are excluded. A busy player
inventory lock is skipped rather than blocking the world thread. Concurrent
callbacks cannot replay a resolved cast. Each hunt has its own cast state.

Soaring Leap's stun is owned by the current hunt and session. It blocks ordinary
walking, jumping, short in-instance teleport, attack dispatch, target checks and
direct attack application. It expires after two seconds and ends when the player
leaves the hunt, the hunt is released or the boss dies. It does not become a
permanent global movement flag. Server return/cleanup remains possible. The
existing timed Dizzy update is sent as presentation support; its precise native
appearance still requires genuine 6711 acceptance.

## Validation

The final isolated x86 build passes these suites on the same server assembly:

| Suite | Checks |
| --- | ---: |
| Exorcism hunt, rewards and named combat | 590 |
| Shared combat/class calculations | 669 |
| Grotto, Dragon Slayer and Terato | 262 |
| Sky Pass movement and jumps | 34 |
| Soul Ball earning and rewards | 559 |
| Gameplay total | 2,114 |
| Producer audit tests | 3 |

The focused fixtures verify warning boundaries, exact normal damage, stronger
Shake damage, physical defense, marked-target isolation, area edges, dodging,
actual movement denial and post-expiry jumping, direct damage suppression,
concurrent callbacks, lethal damage, expiry, replacement, missing actor ownership,
invulnerability, inventory-lock contention, boss death and instance cleanup.
Existing ordinary-player earning, persisted team victory, item/currency bundles,
Spook spirits and reload/failure recovery remain covered. These are server
fixtures, not evidence of a person completing the live client encounter.

Server SHA-256:
`D35A8DB488EF842CE244B304B80EDC6164A7530DC6995B5B38C56B5AB8256CE9`.
Focused harness SHA-256:
`7DA1B38440B6506CA040E560EBF295C9BAD25989AE2CD81A8404061005EFADC3`.
Ignored logs are under `obj/chi-refill-validation/`, named
`spook-combat-final.log`, `spook-regression-combat.log`, and
`spook-combat-regression-{grotto,sky,soul}.log`.

The achievement inventory remains 219 producers present and 118 absent. This
combat repair adds no achievement producer. Alchemist Dan purchase validation,
original Black Burrow terrain, native spell/stun/HUD rendering and restart
acceptance remain unfinished parts of the broader Exorcism milestone.
