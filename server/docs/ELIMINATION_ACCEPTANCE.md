# Elimination ordinary-player acceptance

Status: **private full-route earning and rescue/den process recovery pass;
authenticated admission, combat, claim and reward restart pass; native acceptance remains open**. Source validation
passes 195 transactional/interaction/lifecycle checks, 141 rules and six terrain
checks. This checklist does not mark those external gates complete.

Use an isolated Windows acceptance Host and disposable saved data, with the
approved genuine 6711 DX9 client. Record server commit/build hash, client EXE
hash, map-index/terrain hashes, test date and result for each step. Keep passwords,
authentication packets and reusable tokens out of reports. No new client patch
is assumed: the explicit Revora den uses the existing mine base 1021.

`EliminationLifecycle.ReleaseReady` remains false in normal source. Enable the
service only in the isolated acceptance build, for all eligible test players.
Do not introduce an owner/GM allowlist, enable the live Host, or restart production
to carry out this checklist. Separately authorized release work follows acceptance.

Use a non-GM character reborn once or twice, at least level 70, with twelve free
bag slots. Quest admission and return choices charge no CPs or silver. Starting
level/rebirth are test prerequisites, not claimed earning coverage.

- [ ] At Georgia in Maple Forest (423,55), confirm her model, readable brief
  dialogue and ordinary admission. Gather all twelve distinct Beauty Herbs;
  confirm visible bound items and refusal to gather the same plant twice.
- [ ] Discard one herb through normal inventory use, reconnect and recover it
  from Georgia. Confirm exactly one replacement. Exchange the twelve herbs for
  one bound Fancy Spice and check immediate inventory updates.
- [ ] Use Fancy Spice at (311,256). Confirm the private forest loads, the spice
  is consumed once, RockBehemoth appears and normal attacks work. Reconnect
  before defeating it; use Georgia's return choice without paying another spice.
- [ ] Defeat Behemoth and speak to Landy. Confirm her model/dialogue and return.
  Report the poisoning to Georgia, then kill FireSpiritL43 in public Maple Forest.
  Confirm one bound Fire Pill per qualifying player kill, capped at ten. Discard
  one pill, recover it, and deliver the exact ten pills for the cure.
- [ ] Enter the den through Georgia. Confirm the mine renders with the reviewed
  collision grid, exit Georgia is reachable, six sequential RockLackeys appear
  at their sites and RockLeader appears only after the sixth defeat. Exit and
  reconnect after three kills; confirm only the remaining fights are required.
- [ ] Defeat RockLeader, return and claim from Georgia. Check one bound level-1 Tortoise Breaker rune (4030101), no P7 soul, one EXP-ball unit
  at the character's current level, exactly 50 Study Points and achievement 11010.
  Check immediate displays, then reconnect and repeat the claim: no second reward.
  Confirm no unrelated title or quest achievement is granted.
- [ ] On a separate eligible run, test player death, disconnect, daily expiry
  and replacement login during each private encounter. Verify a valid public
  return, no duplicate material charge and no unearned victory. At daily reset,
  old materials must not complete a new run; Georgia can remove owned old supplies.
- [ ] On the isolated acceptance Host only, stop/restart with the character saved
  inside the rescue, and separately inside the den after three kills. Confirm
  startup returns the character to Maple Forest and preserves the paid rescue or
  three earned den kills. A subsequent ordinary entry resumes the proper stage.
- [ ] With a second non-GM account, verify the first player's private actors are
  neither visible nor attackable from the second player's world. Check ordinary
  NPC access remains available to both eligible accounts.

Keep separate results for visual rendering, real movement, persistence, immediate
updates and process restart. A passing server fixture or a terrain hash is not a
substitute for these observations. Any failure remains an unchecked gate with its
reproduction steps and exact artifacts. Do not publish a new client/Host bundle
for this checklist alone.

Source and adaptation details: [store/lifecycle](ELIMINATION_STORE.md),
[identity and terrain evidence](ELIMINATION_IDENTITY_RESEARCH.md).


## Paid rescue across actual process interruption (2026-09-27)

- [x] Start the actual isolated game with Elimination enabled only inside the
  path/hash-checked test process. The source ReleaseReady gate remains false.
- [x] Supply an eligible ordinary reborn profile, load it through production
  persistence, register with Georgia and gather all twelve herbs through the
  real NPC dispatcher. Exchange them for the bound Fancy Spice and use it through
  the ordinary item handler. No quest stages, materials or scene bindings are seeded.
- [x] Interrupt that process after the consumed-spice admission is durable. After
  database/game restart, startup returns the player to Maple Forest (309,256),
  retires the old scene and preserves the original rescue contract.
- [x] The ordinary Georgia return option re-enters without another spice or new
  contract. A second game restart repeats recovery/reentry without invented
  kills, items, completion or prizes.
- [x] Subsequent private runs complete combat/cure/den/claim and interrupted-den
  recovery; the authenticated packet journey also passes. See the full-route
  and authenticated sections below (September27?28 evidence).
- [ ] Native observations and live activation/deployment remain pending.

Run `tools/Verify-ArenaProcess.ps1 -EliminationOnly`. The shared harness uses its
existing exact marked disposable clone and loopback listeners. Reflection enables
only the helper process's release flag before startup, for every eligible fixture
player; the production source gate is unchanged. The ordinary profile's level,
rebirth and positions are controlled inputs, not a native movement career.
The first process deliberately exits without shutdown; the verification phases
close normally. All three phases pass, the clone is removed and private database
stopped. Evidence: `obj/elimination-process-run.log` and the build log
`obj/elimination-map-index-build.log`.

The test found and fixed an actual startup blocker: the den expected its cached
terrain filename in DMaps.MapPaths, but the installed GameMap index supplies the
native mine filename. See the identity record below. Private executable SHA256:
`835679C5DBC57ACAC6CD0EDCAA1F0F4D1ED8466736A7F0D395B870A172FEF4C1`.
No live process, database or client installation was changed.


## Full route and interrupted den in actual game processes (2026-09-27)

The same `-EliminationOnly` suite now continues the earned-spice character through
the full route. It defeats the owned Behemoth through ReceiveAttack, reports to
Landy and Georgia through normal NPC handlers, kills ten actual loaded
FireSpiritL43 actors in public Maple Forest, exchanges their ten bound Fire Pills
for the cure, and enters the den through Georgia. No replacement monsters, cure
items, kills, quest stages, achievements or final rewards are supplied.

- [x] Defeat three sequential RockLackeys, verify no final claim or achievement,
  then deliberately interrupt the actual process with the character in the den.
- [x] Restart the database and game. Startup returns the character to Maple
  Forest, preserving the three earned kills. Ordinary reentry presents the
  fourth fight; complete the remaining three lackeys and RockLeader.
- [x] Claim through Georgia. Verify one bound Tortoise Breaker rune, one EXP-ball
  unit at the claim's level, 50 Study Points and exactly one11010 evidence row.
  No P7 soul is given. Replay leaves level, EXP and Study Points unchanged.
- [x] Reload another actual game process, replay the NPC claim, and verify the
  same saved level/EXP/Study Points, rune UID and claim timestamp fingerprint.
- [x] Later September27?28 runs close the authenticated packet journey and
  lost final-COMMIT response recovery; see the dedicated sections below.
- [ ] Native rendering, movement/combat UX, representative balance and deployment
  remain pending. The normal source ReleaseReady gate remains false.

The six-launch sequence includes two deliberate interruptions and two database
restarts. It reuses private build
`835679C5DBC57ACAC6CD0EDCAA1F0F4D1ED8466736A7F0D395B870A172FEF4C1`;
no rebuild or live deployment was needed. Damage amounts and proximity are
controlled fixture inputs passed through the production damage path, not proof
of naturally played client combat. The existing loaded Fire Spirit population
is used without seeding replacement actors. Evidence:
`obj/elimination-full-process-final.log` and
`tools/CheckCandidateEliminationLifecycle.cs`.


## Lost final COMMIT confirmation (2026-09-27)

The six-process `Verify-ArenaProcess.ps1 -EliminationOnly` route now also loses
the successful PostgreSQL COMMIT response for the first final claim. The bounded
relay targets only `revora_elimination_claims` in the marked loopback disposable
database; it neither logs packet bodies nor changes the transaction outcome.
The ordinary fixture completes the herb, rescue, cure and seven den victories
before exercising this failure. No final eligibility or reward receipt is seeded.

- [x] Confirm the game fences the stale character after the lost response. Its
  live inventory, level, XP and Study Points still contain the pre-claim image.
- [x] Confirm the database has exactly one claim. Eight stale NPC retries and a
  refused character save leave the committed item fingerprint unchanged.
- [x] Reload through production persistence, recover the bound rune, 50 Study
  Points, expected XP and single11010 evidence row, and reject repeat claims.
- [x] Start another game process and compare exact rune UID, level, XP, Study
  Points and original claim timestamp with the saved final fingerprint.

All six phases passed on unchanged private game SHA-256
`DD61E4AD95D583179236E66A11FD820D2A0A39275CB175404C3F51D802C407CB`.
Driver evidence: `obj/elimination-commit-loss.log`; assertions are in
`tools/CheckCandidateEliminationLifecycle.cs`. Repeated phase logs overwrite
previous instances; the driver records both successful final-phase executions.
The clone was removed after success. Existing supplied eligibility, controlled
proximity/damage and private-only release activation limitations still apply.
The later September28 section closes authenticated socket acceptance. Native
acceptance and deployment remain open; source release stays disabled. This
section specifically closes lost-final-COMMIT recovery.


## Authenticated herb and rescue admission (2026-09-27)

`Verify-ArenaProcess.ps1 -EliminationWireOnly` passes real SRP/DH login and two
game processes around a database restart on private executable
`A40A9692DAD1C63CEF1C6F7E53A4F77F02AA98CDFDEA413FB7E352F7A8E67403`.
No gameplay rebuild was needed. Evidence: `obj/elimination-wire-process.log`.

- [x] An ordinary first-reborn character registers through Georgia over TCP,
  gathers all twelve herbs through NPC packets, retries each consumed offer,
  and observes exactly twelve bound inventory items on the socket.
- [x] Exchange those herbs for one bound Fancy Spice, observe its exact UID/type/
  binding, and send actual1009 item-use twice at the clearing. Exactly one rescue
  contract exists, the original spice is consumed, and the socket bag is empty.
- [x] Verify private server instance identity separately from the client's
  Maple Forest terrain identity; the latter correctly uses base map1011.
- [x] Restart database/game, authenticate again and use Georgia's rescue-return
  option over TCP. Reentry preserves the same single rescue/run, gives no new
  spice, charges no CPs or silver and grants no final claim.

Eligibility, account and proximity are controlled fixture inputs. ReleaseReady
is enabled only in the isolated helper process for every eligible test player.
The test does not supply herbs, spice, quest progress or reward receipts. Stale
NPC requests are fenced by a location response because the production handler
may correctly ignore them without sending a dialogue. This is ordinary admission
and reconnect coverage, not native movement or full socket combat/cure/den/claim.
The final claim loss/recovery test above remains separate. Production activation,
native validation and deployment are still pending. The owned clone is cleaned.


## Full authenticated combat and final-reward restart (2026-09-28)

The same `-EliminationWireOnly` suite now has three phases: admission, restarted
rescue/combat/claim, and another restarted authenticated final-reward check.
All pass on unchanged game SHA-256
`A40A9692DAD1C63CEF1C6F7E53A4F77F02AA98CDFDEA413FB7E352F7A8E67403`.
Evidence: `obj/elimination-wire-full.log` and the three private
`arena-process-elimination-wire*.log` files.

- [x] Send ordinary melee attack packets to the owned RockBehemoth; speak with
  Landy and Georgia using actual NPC packets.
- [x] Kill ten startup-loaded FireSpiritL43 actors through attack packets,
  observe exactly ten bound pills on the socket, and exchange them for the cure.
- [x] Enter the den through Georgia and kill its six sequential lackeys and
  RockLeader through attack packets. Seven saved den kill rows must exist.
- [x] Claim through Georgia over TCP, receive exactly one bound4030101 rune and
  50 Study Points, and require one claim plus one11010 evidence row.
- [x] Restart database/game again, authenticate, verify exact rune UID/type/
  binding, saved level/XP/Study Points and claim timestamp, then replay the
  claim without changing that fingerprint. The socket observes the same rune.

Proximity and screen registration are supplied so this isolates packet combat,
not walking/navigation. The existing level100 first-reborn,10,000-HP fixture
is retained; no additional gear, damage override, HP reset, forced monster death,
proficiency award or cooldown advancement was added. Attacks are paced550ms and
bounded to100 attempts per actor. The normal damage calculation and encounter
handlers decide the result. The observed run finished with9997 HP; that is a
fixture observation, not a difficulty/balance certification. No monster or
quest completion was seeded. Private-only ReleaseReady activation remains.

The earlier direct-handler interruption and lost-COMMIT scopes remain distinct.
Native models, movement, collision, UI and real-player difficulty review are
still unchecked. Live activation, signing and deployment remain pending; this
batch changes only helpers/docs, leaves the source release gate false, and
cleans its owned disposable database after success.

## Gate reconciliation (2026-10-03)

The earlier combined pending rows are now split to match their subsequent
recorded results. Retained driver logs for the full route, lost-COMMIT recovery
and authenticated route were inspected, together with the current
CheckCandidateEliminationLifecycle assertions. Their hashes and historical
build identities are recorded in [the reconciliation evidence](research/ELIMINATION_GATE_RECONCILIATION.json).
These are September27?28 runs, not a new regression on the October3 executable.
No tests were repeated, no new earning count claimed, and ReleaseReady remains
false. Native rendering/movement/UI, representative balance and approved release
still require their own evidence. Original supplied eligibility and controlled
travel/damage limitations remain in each dated section.
