# Classic dance earning route

2026-09-20, baseline `de95de0`. Ordinary players can earn achievement 10121
through an accepted classic dance in Twin City. Source and transaction tests
are complete for this route; native presentation and deployment remain open.

## Evidence

The installed achievement catalog identifies 10121 as dancing in Twin City.
Its SHA-256 remains
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`;
the bounded decoded record was checked directly for this batch.

Existing server action 81 dispatches to `GeneralData.ChangeAction`, and
`ConquerAction.Dance` identifies classic action 1. Installed `3dmotion.ini`
contains all eight classic action numbers for all four standard body profiles,
including both unarmed and weapon animation entries. Client and server spell
catalogs agree on the seven additional dances, IDs 1380..1410 in steps of five,
at level zero. The numeric dance-to-skill association is a source/catalog
inference, not a newly observed native request.

| Local evidence | SHA-256 |
| --- | --- |
| Client `ini/3dmotion.ini` | `9951727aa1ed461fdbb083cc49fb35299fbd15a146a2289f842a6c5df33533f1` |
| Client `ini/magictype.dat` | `618431607e8db81f5e8c6093d801329b73bd9a5af4aa57fc8a38348a832173c2` |
| Server `bin/Debug/database/magictype.txt` | `97ecb413566f5fea541090d1a1c1498ed2acf0874efa6056984583c65c187a71` |

The read-only spell audit reused `audit_warrior_epic.item_rows`, including its
encrypted round-trip check. No client assets were copied into Git. The official
[action interface guide](https://co.99.com/guide/guides/actionint.shtml) describes
the action menu and shortcuts; it does not establish wire values.

The legacy `Action.dat` is not treated as a 6711 authority: its record layout
does not use the achievement cipher, and the existing isolated reference audit
describes it as a much older remnant. No inferred decode was used for gameplay.

## Admission and persistence

The basic dance requires no book or subclass. Dances 2..8 require the matching
available learned skill. This batch does not claim their book acquisition paths
or Performer transformation dances are complete.

The service rejects dead, fake, replaced, unloaded, reload-fenced, trading,
mounted, flying or transformed players, mismatched request UIDs and high-word
aliases. Other emotes retain their existing handler. The existing bounded
family-10010 decoder rejects malformed framing before dispatch.

Only the canonical public map 1002 and its reviewed Twin City travel rectangle
can award 10121: x in [348,508), y in [209,434). Using that existing city region
is Revora's location interpretation; the original dance trigger's precise bounds
have not been traced. Dancing in the surrounding Wind Plain or other worlds
remains available without city credit. Request-supplied coordinates are ignored;
outgoing coordinates come from the player's accepted server position.

The character row is locked and achievement evidence committed before projecting
the accepted dance. A missing write fences the session without awarding or
projecting the action. A repaired retry remains possible. The first completion
is retained for life; a per-session success cache prevents repeated dancing
from repeatedly writing it. Production title login restores durable evidence
independently of legacy client bits. No historical backfill is performed.

## Validation and limits

The isolated build passes. `-ProgressionOnly` passes **122 checks**, including
48 new dance checks. They exercise the actual serialized action dispatcher,
ordinary basic dance, all seven learned-skill gates, concurrent requests,
suppressed evidence, retry, title-login reload, six region-edge cases, foreign
UIDs, aliases, malformed frames and rejected player states. Basic dancing is
the complete no-material earning path; extra skill ownership is fixture supplied.
The reload check recreates achievement state and calls production title login;
it is not a full Host restart or full character lifecycle test.

Nine quest packet regressions and three producer-audit tests pass. The source
inventory is now **227/337**, with 110 producers missing. Neither these counts
nor the source tests prove a per-character route to Rising Star's 320.

Genuine 6711 menu/hotkey requests, all weapon/body animation combinations,
Performer transformations, original location-boundary parity and achievement
notifications remain native acceptance work. No live deployment or client patch.
