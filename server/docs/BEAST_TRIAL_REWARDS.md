# Beast Trial rewards and remaining acceptance

The existing eight-wave solo event uses `beast-claim`: 1 for victory, 2 for
fourth-wave consolation. Previously the NPC cleared that flag before separately
saving currencies and variables. Failed writes could lose a claim, concurrent
requests could duplicate payment, and invalid nonzero flags received victory
rewards.

Claims now require an ordinary canonical player, serialize inventory/variables,
compare the saved entitlement and purse, then consume the claim and save all
currencies in one database transaction. Victory also inserts one bound random
P7 soul in that same transaction, following the owner's event reward policy.
Consolation retains configured currencies without a soul. Client balances/items
are updated after commit. Failures require reload; insufficient bag/purse space
preserves the claim. Invalid flags cannot claim either reward. This also applies
to previously saved, unconsumed legitimate victory flags.

Build, 332 combined Local PK/LMS/Relic/Wave/Beast checks and 216 existing gameplay
checks pass. Beast-specific cases cover both prize kinds, variable/item write
failure rollback, purse overflow, full inventory, concurrent claims, exact bound
soul and currency reload, stale entitlement replay and invalid flags. Fixtures
seed saved claims; they do not prove earning them through live combat.
Logs: `obj/chi-refill-validation/beast-claim-build.log`,
`beast-claim-tests.log`, `beast-claim-regression.log`.

Remaining after the claim batch: durable result selection/queuing (addressed in
the following batch), reserved admission and interrupted returns, owned arena cleanup,
real combat/life-loss/progression checks, real-process and native acceptance.
No production deployment or private candidate refresh was performed for this
batch. This is claim-path completion only, not event completion.

## Saved outcomes and queued prizes

Each instance now has a stable result ID and admitted player UID. Completion
freezes victory/consolation/failure, saves it in `revora_beast_results`, verifies
the saved row, and only then marks the encounter finished. Failed result writes
retain the selected outcome for the next event tick, even if the player leaves
or wave state changes. Tick/death/completion are serialized per instance; pending
completion cannot consume lives. Entry requires an ordinary canonical player.

Results no longer overwrite the legacy claim flag. A claim imports the oldest
unconsumed saved victory/consolation only when the existing flag is empty, and
atomically marks that receipt imported alongside installing the flag. Offline
results remain claimable after reload. Early failures save no payable prize;
only a saved eight-wave victory awards the bound P7 soul.

Build, 341 combined event checks and 216 existing gameplay checks pass. Added
fixtures exercise failed result insert, frozen offline victory retry, two queued
wins, repeated completion, legacy claim precedence, failed import rollback,
bound-soul/purse reload and replay, and distinct early/fourth-wave failures.
They select controlled completion states rather than playing all eight waves.
Logs: `obj/chi-refill-validation/beast-result-build.log`,
`beast-result-tests.log`, `beast-result-regression.log`.

Reserved admission/return, safe cleanup, full earned combat progression and
process/native acceptance remain open. The existing return path is not yet a
durable return reservation. No candidate refresh or deployment occurred.

## Eight-wave progression and incomplete catalog cancellation

Spawning now requires the exact regular monster and the wave-four/eight boss
definitions. Invalid wave numbers or spawning failures mark the encounter
aborted. An aborted encounter cannot select consolation or victory, even if a
late completion callback runs. The old Pheasant fallback and silent missing-mob
skip are removed. The existing return path brings a connected defender to town;
durable interrupted return and owned arena cleanup remain unfinished.

Build, 371 combined event checks and 216 existing gameplay checks pass. A fresh
ordinary solo player enters through the actual registration path, advances all
eight actual spawns, verifies live enemies prevent progression and bosses retain
15,000/35,000 HP, earns victory, claims, reloads and verifies the exact bound P7
item and purse without replay. Missing regular, mini-boss and final-boss catalog
cases verify cancellation without any result/prize. Fixture cleanup explicitly
disposes its test maps; this does not certify production arena cleanup.

Deaths are controlled by the fixture. Combat damage/balance, durable entry and
return, process/native acceptance and deployment remain open. Logs:
`obj/chi-refill-validation/beast-progression-build.log`,
`beast-progression-tests.log`, `beast-progression-regression.log`.

## Reserved entry, recoverable return and arena release

Admission now saves the solo player's original combat mode and arena in
`revora_beast_admissions` before teleporting. It rejects an outstanding previous
return, validates the ordinary player again under a nonblocking inventory lock,
and cancels failed setup without a prize. New private arenas omit ambient catalog
loading and its timers.

Completion/cancellation marks the reservation ended and returns through the saved
record. The town destination and original combat mode are saved before return
acknowledgement. Failure fences the session and preserves a retry. Startup marks
unfinished reservations ended; the actual login pipeline recovers the saved town
return before further login work. Saved results remain independent of interruption.
Only acknowledged returns release the exact owned arena, entities, floor items
and PK-free registration. Pending offline returns can retain a slot until login;
the existing 16-instance limit bounds that retention.

Build, 377 combined event checks and 216 existing gameplay checks pass. Added
cases cover failed admission, ordinary entry, failed return acknowledgement,
fresh player reload with mode restoration and owned-map cleanup, and simulated
startup recovery without fabricated results. Existing eight-wave earning and
queued prize/reload checks also pass. The startup helper is called in-process;
this is not a real game-process restart or native reconnect test.
Logs: `obj/chi-refill-validation/beast-admission-build.log`,
`beast-admission-tests.log`, `beast-admission-regression.log`.

Full combat/life-loss validation, actual process restart, native presentation and
deployment remain open. No production service or private runtime was refreshed.

## Three distinct deaths consume three lives

The actual death-path fixture reproduced a second death being ignored by the
legacy two-second duplicate filter, leaving the player dead while lives remained.
That time filter is removed. The admitted dead-player check under the instance
lock handles replay: revival makes a duplicate callback harmless, while another
actual death consumes one life regardless of elapsed time. Foreign player objects
or players outside the arena cannot consume that instance's lives.

Build, 388 combined event checks and 216 existing gameplay checks pass. The added
ordinary solo journey exercises both Entity death overloads, three rapid deaths,
two half-health revivals, duplicate callbacks, final defeat/arena release and saved
town return after reload without a first-wave prize. Combat mode is restored on
return; the ordinary character loader's normal mode reset on a later login is not
changed. Logs: `obj/chi-refill-validation/beast-lives-before.log`,
`beast-lives-build.log`, `beast-lives-tests.log`, `beast-lives-regression.log`.
Full combat balance, actual game-process restart and native presentation remain
unchecked; the private runtime and production have not been refreshed.

## Full-process eight-wave victory and unclaimed prize recovery (2026-10-01)

`career-beast-earn`, `career-beast-claim` and `career-beast-reload` pass on
private full-server SHA256
F020C8A2B264B6D420B62A5DFA5421387E11B8D5F6AC44BB8305FEEC1608049B.
The ordinary non-GM event character continues from its verified Boss Rush
checkpoint. Public Tick opens a controlled registration window; the installed
BeastTrial NPC admits it to a production-allocated private arena. Each of eight
waves has live monsters, and a tick before their deaths cannot advance the wave.
Controlled lethal ReceiveAttack calls exercise production monster deaths.
The eighth cleared wave saves an unimported victory, returns the player to town,
acknowledges the admission and closes the arena instance.

The actual game process then exits without normal cleanup, before claiming.
After PostgreSQL and game restart, the exact prior inventory, purses, result and
admission rows match. OnLogin and the ordinary NPC claim grant the configured
victory currencies and one bound P7 soul. The result is imported once and the
claim flag consumed. Repeat claims pay nothing. A further full restart preserves
exact item rows/UIDs, currencies and receipts and again refuses replay.
`revora_beast_process_probe` retains the completed fingerprint. The separate
retained achievement career and its counts are unchanged.

This proves recovery of an earned, unclaimed victory after a completed town
return. It does not prove an interrupted mid-wave or pending-return process
boundary, natural schedule, normal attack balance, authenticated/native combat,
lost COMMIT reply or host power loss. Earlier in-process interruption/life-loss
checks retain their narrower scope. No production code, live deployment or
live process changed.

The later [admitted-round interruption batch](EVENT_INTERRUPTION_RECOVERY.md)
passes actual NPC entry, abrupt exit before progress, town/combat-mode recovery,
no unearned prize and exact prior rewards across two cold restarts. It supersedes
the missing early-round process boundary only; later-wave/native gates remain.


## Retained eight-wave calculated combat (2026-10-03)

Ordinary retained character4294907001 enters through NPC900000009 with existing
earned equipment. Normal `Attack.HandleAttack` melee calculation defeats all
65 monsters across eight waves (4/5/6/8/10/11/12/9). Each monster takes one hit
with this character's gear. A tick while monsters remain alive does not advance
the wave; clearing the eighth closes the instance and records one unpaid victory
and one completed town return. No damage value or death callback is supplied.

After game/database restart, the ordinary NPC claim grants exactly8,000 CPs,
22 RVPs,700,000 silver and one bound P7. The result imports once and the claim
flag clears. A further restart preserves the exact nested prior career, item
UIDs, purses, result and admission rows; duplicate claims/login recovery add
nothing. Checkpoint: `revora_retained_beast_probe`, wrapping the retained
Extraction checkpoint. No production code change was required.

Fixture `tools/CheckCandidateRetainedBeast.cs` passes
`career-retained-beast-{earn,claim,reload}` on private executable
`71F2DA5388C13BBA67FFA936ECBDCB141AE716D21EB2CA78DC151B4AFF380A77`.
Registration timing, proximity and attack cooldown are controlled. Monster
retaliation, natural difficulty, authenticated/native combat and live deployment
remain open. These one-hit outcomes are not a balance acceptance claim, and this
adds no achievement or event-family count. The unrelated private vote-pulse
PostgresException is outside these checks.

## Fresh eight-wave earning and cold payout (2026-10-03)

- [x] Continue ordinary FreshRoadmap1 from the paid Guild Beast checkpoint,
  preserving its earned equipment and nested career. Store one existing item
  through the Ape City warehouse packet, then register through NPC900000009.
- [x] Defeat all65 monsters across eight waves through calculated melee:
  4/5/6/8/10/11/12/9 kills. Each takes one hit with the existing equipment.
  Polling an uncleared wave does not advance it. The final wave produces one
  unimported victory and one completed town return.
- [x] After process restart, claim8,000 CPs,22 RVPs,700,000 silver and one bound
  P7 through the NPC. Final cold reload and repeated claim/login recovery
  preserve exact possessions, balances, result, admission and previous career.

`tools/CheckCandidateFreshBeast.cs` passes `career-fresh-beast-{earn,claim,reload}`
against private game SHA-256
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
No production source build, live restart, deployment or patch was required.

The first attempt stopped before storage or admission: the synthetic session
omitted the Ape City warehouse normally created by `GameState`. Existing stored
items remained in the database. The fixture now initializes that warehouse
before item loading. Read-only comparison proved only an approach move from
1038(56,58) to1020(576,544); original and current full snapshot hashes were
`f8fc77a1ae2a5e9068f860c900ab1db31d353d4ce307673d1cf22021c3192b83` and
`8659ed0f8723e6e9b77a3cc37851262c4c5f21f8f615a3a2c956c35f403f386e`.
The resumed fixture accepts only that exact reviewed pair; it does not rewrite
old gameplay history or discard unrelated differences. A subsequent hash-helper
qualification error failed compilation before execution and was corrected.

Registration timing, proximity and attack cooldown are controlled. These one-hit
outcomes prove the handler earning route, not difficulty, retaliation, natural
movement or native acceptance. Full compatible-career validation stays open.

The separate `career-fresh-beast-audit` phase verifies the complete paid snapshot
and that every saved Ape City warehouse item is loaded. The sanitized export
records level128/class25/rebirth2,69 saved identities and253 absent conservative
candidates (322 total). It is a receipt audit, not independent proof of every
achievement or a completion percentage. Snapshot SHA-256:
`a274951952bfacff02653599279d66ca514549fc400386e3234c4b97c45a1eda`.
