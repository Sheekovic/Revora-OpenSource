# Weekly and Monthly PK War restoration

Implemented against baseline `0ce9590`, 2026-09-15. The isolated server build and
59 ordinary-player PK checks and 232 focused wardrobe/progression/Elite PK
regressions pass after the halo correction. The preceding tournament batch also
passed 713 quest/event/wardrobe regressions. Genuine-client acceptance and
deployment are separate pending gates.

The [official PK War guide](https://co.99.com/guide/quests/pkwar.shtml) establishes
Saturday US weekly play, first-of-month monthly play, four level divisions and
a sole surviving champion. Both occupy the 20:00–21:00 window. The installed
achievement identities are 11116/11118 for weekly entry/win and 11117/11119 for
monthly entry/win. None is an Elite PK or ordinary Arena victory substitute.

Revora uses America/Los_Angeles with UTC persistence. General Bravery 2314 now
returns to valid terrain near Twin City (446,246), preserving mesh 310. The stored
NPC is parked on map 6000. Old winner NPCs 2315 and 2317 no longer execute
their independent purse grants. Each event has its own dated run per division:
1–99, 100–119, 120–129, 130+. Admission is capped at 64 per division and closes
at 20:19. If the first day is a Saturday, both independent events are offered;
a character can own only one active tournament place at a time.

The old weekly/monthly maps 1559 and 1518 are aliases of base map 1508, not
missing terrain. The Revora three-stage route uses owned copies of 1505, 1508 and
700, with 12,324, 4,981 and 1,587 valid landing tiles respectively. These actual
installed arenas become smaller without invisible artificial walls. Survivors
transfer automatically at 20:28 and 20:40; each transfer includes ten seconds of
preparation with combat blocked. Fighting is otherwise available during entry,
subject to the same preparation after each successful admission.

From 20:22 onward, one remaining eligible fighter may win. A champion must have
participated in a field with at least two real admitted characters and dealt
positive accepted damage to another entrant. An empty field, an uncontested
entrant or multiple survivors at 21:00 produces no champion. Leaving, death,
disconnect or changing level division forfeits the current place. Reentry to the
same dated run is forbidden. Participation evidence is recorded only after a
genuine competitive field has been admitted successfully.

Selected Revora rewards per contested champion:

| Event | CPs | RVPs | Gold | Bound EXP Balls | Native champion honor |
| --- | ---: | ---: | ---: | ---: | --- |
| Weekly | 100,000 | 1,000 | 1,000,000 | 3 | 7 days |
| Monthly | 250,000 | 2,500 | 5,000,000 | 5 | 30 days |

These purses are explicit Revora economy choices, not historical CO prize claims.
Saved claims are atomic and remain available after logout, full bags and failed
writes. The winner achievement and dated honor entitlement commit with the result;
claiming currency later cannot renew the honor. Native status flags 37/36 are
the client-confirmed weekly/monthly halo identities. Compact legacy database keys
21/20 are not native title-list IDs: those native IDs belong to cross-server Elite
PK. Old status rows remain archived, but cannot project those unrelated titles.
Verified results govern the halo; monthly takes display priority if both are owned.
See the [protocol evidence correction](PROTOCOL_RESEARCH.md#pk-halo-identity-correction---2026-09-15).

All event combat, including receive damage, spells, reflection and death, is
owned by the current run and admitted sessions. No normal PK crime, item loss
or external NPC attack is allowed. Use one shared combat gate with Elite PK to
avoid acquiring two event locks in opposing orders. Player inventory locks precede
that gate; UI reconciliation and prize installation happen under the owner lock
after leaving timer-owned event transitions. Interrupted runs cancel on restart;
completed results and unclaimed prizes survive.

Validation covers ordinary NPC admission; all eight level boundaries; Pacific
summer/winter schedules and coincident weekly/monthly dates; owned damage and
outsider rejection; preparation; three installed arena stages and actual jumps
to each clerk; partial stage transfer; failed admission and reconnect; stale
reservation cleanup; failed result commit followed by finalization after the
deadline; exact weekly/monthly honor expiry; full bags; failed and concurrent
purse claims; immediate and durable currency updates; restart cancellation and
unclaimed-prize recovery. Elite and local PK share the same combat gate, exercised
with opposite lease order. Ordinary Arena signup and team import exclude PK
entrants. The legacy entity loader returns dynamic map IDs to Twin City on login;
the new login hook also relinquishes any in-memory owned place.

Implementation: `LocalPkWar.cs` owns the dated field, canonical sessions, arena
transfers and combat. `LocalPkResults.cs` owns run/entry/claim transactions and
backs up old weekly/monthly status rows. Old NPC handlers are intercepted even
when the new coordinator is unavailable. Timer transitions never take another
player's inventory lock; title refresh follows outside the combat gate.

The [official halo guide](https://co.99.com/guide/quests/honorhalo.shtml) supports
seven-day weekly and thirty-day monthly duration. Starting that clock at the
saved victory, automatic stage transfer, requiring a contested field and positive
damage, safe deaths for every name color, and retaining an unclaimed purse are
explicit Revora choices. They are not claims of exact historical client behavior.

Validation executable SHA-256:
`E04A5E43A33ED6368D4C600D6E379C74C719BC96C7BB1F0379BCC2CF888C2E7D`.
The fixture uses a disposable PostgreSQL schema, including forced constraints for
admission, result and prize failures. The build remains under
`obj/chi-refill-validation/bin`; no live executable or installed client changed.
Native halo visuals, real network combat, client acceptance, deployment and public
publication remain pending separately.

## Bound P7 champion reward (2026-09-21)

New Weekly, Monthly and Class PK tournaments add one bound catalog P7 soul to
the champion's existing purse and EXP Balls. Eligibility, winner resolution,
currencies and honors are unchanged. BP PK uses its separate claim path and is
not upgraded by this batch. Solo attendance and non-winning participation do
not become prize claims.

The saved tournament `valuable_reward` flag defaults false for existing rows;
new `Open` inserts set true without replacing an existing run. The soul UID
joins the original claim's reward array. Currency, all items and the checked
claim insert commit together; capacity includes the extra soul. Previously
completed claims are never reopened.

Build, 65 Local PK checks and 120 Class PK checks pass. Ordinary contested
combat earns Weekly, Monthly and class champion results. Coverage includes
concurrent claims, purse/receipt rollback, overflow, exact four-item Weekly
capacity, saved bound-soul receipts, exact item UIDs through production item
reload, and coordinator restart. An older Warrior class contract is marked
before enrollment, won through combat, reloaded after coordinator restart and
claimed twice for its original five-ball bundle only.

Logs: `obj/chi-refill-validation/local-pk-p7.log` and
`obj/chi-refill-validation/class-pk-p7.log` (ignored). This is item reload and
coordinator recovery evidence. A follow-up uses the complete isolated entity and
login schemas. Ordinary Weekly, Monthly and Class champions are loaded through
fresh production character/item loaders, then attempt the claim again. All
currencies and exact bound-soul/EXP-Ball UIDs survive without extra payment.
Character profiles are saved before claims, never afterward to backfill rewards.
Follow-up logs: `local-pk-reconnect.log` and `class-pk-reconnect.log` in the same
ignored validation directory. Genuine-client presentation and live deployment
remain open; these tests do not claim a native socket login.

## Weekly champion through full process restart (2026-10-01)

Three private phases, career-weekly-win/claim/reload, now use retained ordinary
level100 characters4294908403/4294908404 with their existing gear and purses.
The real coordinator and installed General Bravery admit both into the same
level100-119 division. Production dynamic maps progress through1505,1508 and700.
The fixture advances the event clock, submits a controlled one-point normal
ReceiveAttack call and invokes the loser's normal death method. The coordinator
must persist positive accepted winner damage and a completed competitive result.
No winner flag, reward row, equipment or levels are supplied.

Both characters return and save before abrupt game exit, with no prize claim and
exact prior item/purse snapshots. After database/game restart, the winner's
ordinary General Bravery claim pays100,000 CPs,1,000 RVPs,1,000,000 silver,
three bound EXP Ball packs722136 and one bound catalog P7 soul. All previously
owned bag UIDs remain. Exact claimed items, currencies, result, entries and
achievement-evidence snapshots survive another database/game restart. Two claim
retries per participant change nothing; the loser receives no prize.

The first attempt checked competitive flags before the coordinator's next tick.
Both admissions were saved but competitive=false, so that assertion correctly
stopped the fixture. Startup cancelled that unfinished October3 run. The
corrected fixture ticks after admission and uses a fresh controlled October10
run; no prior result, item or admission was deleted to make it pass.

All three phases pass on candidate SHA256
CE1B425A68EB9082F15EF2C39FD2F0B0EC6D21980360CB1B5EA3FFB708522F10.
This covers Weekly PK committed-result/unclaimed-purse recovery, not Monthly or
Class PK full-process recovery. Calendar time, NPC approaches, damage and death
are controlled; authenticated socket/native fighting, natural scheduling,
mid-COMMIT failure and live deployment remain open. No gameplay rebuild or live
process/database change occurred. Evidence: WeeklyProcess and the private
arena-process-career-weekly-{win,claim,reload}.log files.

## Monthly champion process continuation (2026-10-01)

The retained Weekly participants continue through General Bravery's Monthly
option using a controlled November1 20:00 Pacific window. Both keep level100,
class15, equipment and prior rewards. Real dynamic maps1505/1508/700, normal
ReceiveAttack with a controlled one-point damage input, ordinary death and
coordinator finalization produce a saved competitive champion before any claim.

The first earning phase reached and saved the completed result, then stopped:
its fixture compared the entire old Weekly evidence snapshot and incorrectly
rejected the newly earned Monthly entry/win evidence. Recovery kept that result.
The corrected comparison proves every non-evidence component exact, retains all
old evidence and permits only11117/11119 additions before recording the pending
checkpoint. No match/result/history was reset or replayed to manufacture success.

career-monthly-claim and career-monthly-reload pass. After database/game restart,
the ordinary winner receives250,000 CPs,2,500 RVPs,5,000,000 silver, five bound
EXP Ball packs722136 and one bound P7; every old bag UID remains. Another full
restart preserves exact reward UIDs, purse, result, entries and evidence. Both
winner and loser attempt two further claims without mutation. The monthly-win
phase's original assertion failure is retained as a fixture failure, not reported
as an uninterrupted passing run.

Evidence: OtherPkProcess/OtherPkSnapshot(false), candidate
CE1B425A68EB9082F15EF2C39FD2F0B0EC6D21980360CB1B5EA3FFB708522F10.
Clock/NPC approach/damage/death are controlled; no native/TCP fighting, natural
calendar, lost COMMIT reply or deployment acceptance is claimed. No gameplay
source or live database/process changed.

## Durable interrupted-fight return (2026-10-01)

A source audit found that classic PK recovery depended on the coordinator's
in-memory arena/member dictionaries. They are empty after process restart, and
no classic admission record retained the original PK mode. An unknown-map town
fallback could not establish correct event return or mode restoration.

Weekly, Monthly and Class PK now reserve revora_pk_returns before each arena
transfer, recording the run/UID, target arena and original PK mode. The checked
transaction verifies the current owner, open supported run and admission, and
rejects another outstanding return. Arena transition ownership remains with
TournamentCombat.Gate; the persistence code locks the database character row
without requiring an unrelated inventory lock. It does not mutate inventory.

Normal Home acknowledges return only when the saved character coordinates match
the current destination outside that arena. Login recovery uses the pending
record independently of current map allocation: it sends the character to
Twin City1002(408,330), restores/sends the saved PK mode, saves the profile and
then acknowledges. A failed acknowledgement leaves the record retryable.
BP retains its separate admission recovery. Existing classic rows without a
return reservation are not backfilled with invented modes or arena identities.

Build and four private phases pass: career-pk-return-enter/recover/reload/exit.
Two ordinary retained characters choose Peace and Team modes, enter Weekly PK,
and advance toward stage2. A private NOT VALID constraint rejects the new arena
reservation; neither moves. Removing it permits the next tick to transfer both
to map1508 and save the latest arena. Abrupt process exit and database/game restart
cancel the match; ordinary login restores both exact modes and town coordinates,
acknowledges both returns, preserves all previous rewards and invents no prize.
A second restart preserves acknowledgements/state. A fresh Weekly admission and
normal arena-clerk exit also acknowledge both returns with restored modes.
The second clean login checks receipt/destination persistence, not general PK-mode
preference persistence outside event recovery.

An initial development check exposed use of SkillTable.LockOwner during a stage
tick, where the inventory lock is deliberately not held. That helper was replaced
with a location-receipt owner check/database row lock, avoiding an inverted
inventory/event lock acquisition. The unfinished development attempt was retained
and recovered on login before the fresh December5/12 fixtures; no result/history
was deleted. Final candidate SHA256:
B558BF74A339C92158604D48BD6ED9AB8F079055CF44545DCE13137CF8F9DF70.
The source build uses the committed-owner-WIP overlay and has existing warnings.

This runtime fixture covers Weekly PK; Monthly/Class share the modified route but
have no separate interrupted-mode process fixture in this batch. Concurrent
network stress, host power loss, native UI
and deployment remain open. No live process or database was changed.

## Return acknowledgement fault and retry (2026-10-01)

career-pk-ack-enter/recover/reload all pass on the same
B558BF74A339C92158604D48BD6ED9AB8F079055CF44545DCE13137CF8F9DF70 candidate.
A fresh controlled January2 Weekly field reaches stage2 and exits abruptly.
After restart, a private NOT VALID CHECK(NOT returned) constraint rejects the
first return acknowledgement with the specifically checked PostgreSQL23514 /
private_pk_ack_fault identity. The ordinary character has already reached and
saved Twin City1002(408,330) in Peace mode, but its return record remains pending.
All prior reward/item/currency state is exact. The constraint is removed in a
finally block; repeating the ordinary login callback completes both returns and
restores the second character's Team mode. No cancelled-round claim exists.

Another database/game restart retains the acknowledged records, town destinations
and exact earlier rewards. No gameplay rebuild was necessary. This tests a
known rolled-back acknowledgement and same-process retry followed by cold reload;
it is not a lost-COMMIT reply or host power-loss test. General PK-mode preference
persistence outside an outstanding event return remains outside this fixture.

## Retained champion combat and cold claim (2026-10-01)

The retained ordinary Windwalker enters Weekly PK through General Bravery
(NPC 2314, option 1), progresses through maps 1505, 1508 and 700, and wins
with the normal melee attack handler. The saved round is `pk-0-20280527-3`:
two competitive admitted characters, the retained winner and no preclaimed
prize. Both characters return to Twin City; achievements 11116 and 11118
reference this earned round.

A fresh process claims through the same NPC (option 3). Exact deltas are
100,000 CPs, 1,000 RVPs, 1,000,000 silver, three bound 722136 EXP Ball items
and one bound P7 soul. All prior inventory UIDs remain owned. The claim and
another fresh-process replay compare the complete retained career, Arena history,
PK entries/result/claim/return snapshots and reject any duplicate delivery.

The opponent's level 140 is a supplied prerequisite; the retained character's
level, gear and rewards are preserved. Calendar, staging intervals, attack
cooldown and proximity are controlled. The opponent does not attack back; this
is earning and persistence evidence, not PvP balance or native-client acceptance.

Initial setup stopped before admission because wardrobe loading used the real
clock instead of the future test calendar. The background scheduler had opened
an empty May 20 round, subsequently cancelled on startup. Those records remain;
no result was reset. The fixture now sets the wardrobe clock before character
loading and activates the event clock only after the prior-state guard. The
successful route uses the following Saturday, May 27.

Validation: `career-retained-weekly-win`, `career-retained-weekly-claim` and
`career-retained-weekly-reload` passed against candidate SHA256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
No production code, live deployment or client patch changed in this batch.

## Retained Monthly champion route (2026-10-01)

The same retained level-140 Windwalker enters Monthly PK through General
Bravery (2314, option 2), advances through all three arena stages and defeats
a supplied ordinary level-140 opponent using the production melee attack
handler. Round `pk-1-20280601-3` records two competitive admissions, the winner,
returns to Twin City and achievements 11117/11119 before any prize claim.

The fresh-process NPC claim awards exactly 250,000 CPs, 2,500 RVPs, 5,000,000
silver, five bound 722136 EXP Ball items and one bound P7 soul. Repeated claims
and another process reload preserve the full saved career, Arena, Weekly PK and
Monthly result/entry/claim/return snapshots without extra delivery.

Before entry, ordinary sealed trade and warehouse actions free seven bag slots
(35 to 28), preserving transferred items on the peer and storing bound items in
released warehouse slots. Checkpoints follow each completed transfer. The helper
accepts an explicit checkpoint and peer so this preparation does not rewrite the
historical Grail checkpoint. Six Monthly prize items then leave 34 bag slots used,
reserving room for the Class PK prize.

Opponent progression is supplied; calendar, stage waits, proximity and attack
cooldown are controlled. The opponent does not retaliate. This establishes the
ordinary earning and persistence path for this bracket, not class balance,
native-client acceptance, real-time scheduling or live deployment.

Validation: `career-retained-monthly-win`, `career-retained-monthly-claim` and
`career-retained-monthly-reload` passed against candidate SHA256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
No production code, live deployment or client patch changed.

## Fresh tournament qualification (2026-10-03)

The ordinary saved trainee1096323 advances from70 to130 through the installed
EXP Ball handler, retaining the five-player Last Man Standing checkpoint as its
starting boundary. The level132 main and this trainee now share the130+ Weekly
and Monthly PK bracket. Neither character level nor experience is injected.

`career-fresh-tournament-train` consumes414 balls: two bound Quiz prizes and
412 item723700 purchases from shop2888 at27 CP each, totaling11,124 earned CPs.
Six bounded processes retain progress after every consumed item. The controlled
calendar spans42 days from2041-04-23; the ten-per-day allowance is checked after
every use, and41 attempted eleventh uses on the preceding day are refused without
consuming the item or adding experience. Reusing each consumed single-item UID
also leaves the saved snapshot unchanged.

The initial fixture used an incorrect SQL attribute-column name and failed before
any consumption. It was corrected to the installed `atributes` column. Recovery
of the empty probe requires zero balls/spend/cap attempts, level70 and the exact
prior LMS checkpoint; no character progress or rewards were repaired by SQL.

`career-fresh-tournament-reload` passes in a separate game/database process:
exact nested career/roster state, saved experience/attributes, daily allowance,
done=true, level130 and the matching tournament bracket survive. The authoritative
checkpoint is now `revora_fresh_tournament_training_probe`; earlier probes remain
historical. Redacted batch1-5 and final training/reload logs are retained in the
private candidate directory.

This validates a qualification prerequisite, not a Weekly/Monthly victory, Class
PK eligibility, natural elapsed-day play, native rendering or deployment. The
Trojan trainee still differs from the Warrior main for Class PK. No production
code or game build changed. Private candidate SHA256 remains
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.

## Fresh Weekly and Monthly victories (2026-10-03)

The earned level132 Warrior1096322 and level130 Trojan1096323 enter through
General Bravery2314 as ordinary non-GM players. The weekly run is
`pk-0-20410608-3`; the subsequent monthly run is `pk-1-20410701-3`. Each crosses
owned maps1505,1508 and700, then reaches one champion through two calculated
melee attacks. No damage, death, winner, achievement or reward is injected.
Both competitive admissions and acknowledged town returns persist; original
Peace mode is restored and both players are alive after the tournament return.

Each event runs prepare/earn/claim/reload in separate private game/database
processes. The earned victory is saved unpaid before restart. Weekly then pays
100,000 CPs,1,000 RVPs,1,000,000 silver, three bound EXP Balls and one bound P7.
Monthly pays250,000 CPs,2,500 RVPs,5,000,000 silver, five bound EXP Balls and one
bound P7. Repeat winner claims and a losing entrant's claim change nothing.
Final cold checks compare the complete nested career/roster snapshot, all five
loaded bags against saved UIDs, exact bound reward UIDs and purse/claim receipts.
Weekly11116/11118 and Monthly11117/11119 evidence uses the respective earned run.

Weekly preparation stores four items through the actual Ape City warehouse
packet. Monthly's initial capacity check then refuses that full warehouse before
storing anything or entering the event. Its retry returns to General Bravery and
requires the exact weekly snapshot again, preserving the failed log, before
using Desert City10011 on map1000 to store six items normally. The fixture loader
now optionally initializes that warehouse before loading saved items. An extra
monthly cold reload explicitly verifies all six stored UIDs in the loaded bank.
The early weekly harness ambiguity for KnownPersons was fixed before execution.

The latest authoritative checkpoint is `revora_fresh_monthly_probe`, stage2;
`revora_fresh_weekly_probe`, stage2 and earlier probes are historical. Subsequent
fresh phases must load all four used town warehouses, including Desert City.
All eight event phases pass, plus the strengthened monthly reload. Redacted
logs remain under the private candidate directory with their phase names.

The scheduler dates/deadlines, NPC approaches, attack positions and attack
cooldowns are controlled. These two-player scripted victories do not establish
competitive balance, natural opponent retaliation, full-field tournament load,
Class PK qualification or native-client acceptance. Live deployment remains
pending. No production source/build changed; candidate SHA256 remains
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
