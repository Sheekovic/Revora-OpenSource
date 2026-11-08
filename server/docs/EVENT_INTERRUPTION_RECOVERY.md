# Unfinished event process recovery

2026-10-01 private validation. No live deployment or native acceptance.

Nine phases in `tools/CheckCandidateCareerRites.cs` exercise actual ordinary NPC
admission followed by abrupt process exit, login recovery and a further cold
restart for Beast Trial, Wave Defense and Relic Rush. Phase names are
`career-interrupted-{beast,wave,relic}-{interrupt,recover,reload}`.
Private full-server SHA256:
F020C8A2B264B6D420B62A5DFA5421387E11B8D5F6AC44BB8305FEEC1608049B.

The Beast character retains its previously earned Extraction/Boss Rush/Beast
prizes. The four Wave/Relic characters retain their previous two Wave prizes and
ranked Relic prizes. No character, receipt or item is deleted or recreated.

For each event, the ordinary actors start in town, select Capture combat mode,
enter through the installed registration NPC and reach the production arena.
The test checks durable admissions are active and unreturned, saves their arena
positions and exits the actual game process with Environment.Exit. It never calls
event cleanup or graceful shutdown before that exit. Registration time and NPC
proximity are controlled; Wave uses a supplied four-player team.

After game and PostgreSQL restart, production startup has ended all interrupted
reservations without acknowledging their returns. The ordinary loader and event
OnLogin recover every actor to Twin City, restore the reserved Capture mode and
acknowledge the return. NPC claim attempts grant no unfinished-round prize.
Repeated login/claim callbacks and another game/database restart preserve exact
prior item rows/UIDs, currencies and saved result/placement receipts. The normal
loader's later combat-mode reset is not changed; reserved Capture restoration is
asserted during the actual pending-return recovery.

`revora_event_interruption_probe` retains a per-event fingerprint and completion
flag. Read-only fingerprints cover all five event actors, including the actors
not participating in a given interrupted event. The separate 320-achievement
career remains unchanged.

Scope: interruption shortly after admission, during the first wave or Relic's
opening peace period. These rounds are forfeited, not resumed. This does not
prove partial-progress checkpointing, every late-wave crash point, operating
system power loss, lost COMMIT replies, authenticated socket reconnect, native
map/PK display or deployment. Earlier earned-result recovery checks establish
separate post-victory boundaries. This batch changes only probes/documentation.
