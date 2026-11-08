# Flame Lit restoration contract

Research and staged rules, 2026-09-20. No replacement player service or new
achievement producer is enabled by this batch.

## Primary and installed evidence

The official [PC Flame Lit guide](https://co.99.com/guide/quests/flamelit.shtml)
places the event in Guild War's final two hours. Flame Taoist starts the rune
chain; ten ordered stones span Twin City and the Guild map. The last stone opens
only in the final thirty minutes. The final rune is Dragon Rune, and EXP is
claimed from the Taoist after war ends. The guide says weekly completion and
restart from the beginning after losing a rune. It gives no numerical EXP table.
Player-authored tips hosted on the official site are not treated as authoritative
reward or scheduling specifications.

Installed client and active server item catalogs agree on eleven identities
729960–729970, including each rune's next-stone coordinates. The original
Achievement.dat row is UI 60216, achievement 11016, score 10; its description
requires obtaining the Flame Lit Dragon Rune. Completion therefore belongs to
the committed tenth transition, not admission or an arbitrary EXP claim.
All catalog hashes and terrain hashes are pinned by `tools/audit_flame_lit.py`.

| Step | Consumed rune | Next rune | NPC | Intended map and coordinates |
| --- | --- | --- | --- | --- |
| 1 | 729960 | 729961 | 4453 | 1002 (350,327) |
| 2 | 729961 | 729962 | 4454 | 1038 (317,270) |
| 3 | 729962 | 729963 | 4455 | 1038 (236,291) |
| 4 | 729963 | 729964 | 4456 | 1038 (194,168) |
| 5 | 729964 | 729965 | 4457 | 1038 (115,53) |
| 6 | 729965 | 729966 | 4458 | 1038 (316,378) |
| 7 | 729966 | 729967 | 4459 | 1038 (136,182) |
| 8 | 729967 | 729968 | 4460 | 1038 (38,94) |
| 9 | 729968 | 729969 | 4461 | 1002 (350,321) |
| 10 | 729969 | 729970 | 4462 | 1038 (62,59) |

Flame Taoist is identity 4452, mesh 6810, intended at Twin City (353,325).
Stone meshes are 6570–6660. A bounded read-only query of the configured NPC
catalog found the Taoist and stones 1/9 parked on map 5001 at (60,40), (51,83)
and (55,60). Stone 10 is at (60,55), close to but different from its rune's
coordinates. The other seven stones match their installed Guild-map coordinates.
No database rows or active client assets were changed.

## Existing defects and integration decisions

The legacy `Npcs.cs` cases 4452–4462 perform separate inventory removal/addition
and EXP updates. They have no durable weekly completion or exact-rune provenance.
No right-click handler exists for the rune family, despite the installed item
instructions. The tenth stone depends on `GuildWar.Flame10th`. The current
GuildSeasonWar start resets that flag, while its old enabling code is below an
unconditional return in Thread.WorldTournaments and never executes.

The active GuildSeasonWar source schedules Sunday 15:00–16:00 in the host's local
zone. The replacement will follow the actual persisted war interval and its
canonical active coordinator, without changing that schedule. Its opening is
the later of war start and two hours before war end; the last stone opens thirty
minutes before end, clipped to start for a shorter war. The current one-hour war
therefore yields a one-hour Flame Lit window. This is an explicit Revora
adaptation, not a claim about the guide's historical regional timetable.

The existing server reward policy is one EXP-ball unit per lit stone, with two
extra units for the tenth (twelve total). Retain these units as Revora economy
policy; the official guide does not verify them. The replacement must use checked
EXP progression and persist reward delivery with rune consumption. Weekly identity,
lost-rune restart, delayed claims and interrupted-war handling need explicit
transaction rules; a calendar test or bare GuildWar flag is insufficient authority.

## Staged rules and validation

`FlameLitRules` supplies the exact ten item/NPC/site transitions, bounded range,
UTC interval validation, last-half-hour restriction and existing reward units.
It requires an active-war input but does not authenticate that input: the future
coordinator must prove battle ownership independently. It has no NPC or item
dispatcher and writes no inventory, EXP or achievement state.

The isolated build and 153 deterministic rules checks pass. The read-only live
catalog audit confirms all eleven client/server rows, achievement identity and
local stone approaches. Four synthetic terrain checks pass for invalid lengths,
dimensions, flags, out-of-bounds stones, blocked approaches and range edges.
Nearby walkable cells do not prove access through closed Guild gates, a complete
walking route, PvP eligibility or genuine-client presentation.

## Implementation sequence

1. Persist one owned weekly run, exact current rune UID, ordered transition
   receipts, restart generations and terminal reward receipt. Commit achievement
   11016 with the owned Dragon Rune transition.
2. Replace legacy NPC paths and connect normal owned rune use. Review the current
   weekly run before admission/claim and retain pending rewards through reconnect.
3. Restore the three parked actors and exact tenth-stone position in the runtime
   world. Validate ordinary travel and Guild gate behavior without bypassing PvP
   or admission rules.
4. Exercise earned progress, loss/restart, final-stone timing, partial/final EXP,
   concurrent use/claim, stale items, failed writes and reload. Then complete
   genuine-client and real-Host acceptance before release.
