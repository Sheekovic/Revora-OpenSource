# Local Capture the Flag â€” Revora v1

Status: code and isolated automated validation complete; deployment and genuine
6711 client validation pending. This is M2 of the [roadmap](QUEST_EVENT_ROADMAP.md).

## Player route and rules

Talk to the existing **Guild Controller, NPC 380 in Twin City**, and choose
**Capture the Flag**. Any eligible ordinary guild member can enter while the
battle is open. Registration requires being near the actual NPC, alive, fully
logged in and outside trading or vending. No GM status or character allowlist
is involved. Each character stays assigned to one guild for that round.

The battle runs **Saturday, 21:00â€“21:30 America/Los_Angeles**. The Windows
`Pacific Standard Time` zone supplies daylight-saving conversion; database times
are UTC. A late scheduler tick starts the remaining window, not another thirty
minutes. The dated run key prevents a second match in the same weekly slot.

The official local CTF guide supports the Saturday US time window, three bases,
sixty-second flag delivery and ten points per planted flag. Revora uses those
core rules. [Official local CTF guide](https://co.99.com/guide/quests/captureflag.shtml).

The installed map 2057 contains these three bases, each with 20,000,000 HP:

| Base UID | Position |
| --- | --- |
| 20571 | 326,447 |
| 20572 | 214,269 |
| 20573 | 207,98 |

Guilds attack a base to capture it. The guild contributing the most actual damage
wins ownership; equal damage uses ascending guild ID. Base health resets after
capture. A guild cannot damage its own base. The entry camp around 482,367 is
protected, and guild members cannot bypass friendly-fire protection by changing
PK mode. Entry selects Team mode; leaving restores the previous mode.

Forty ground flags are maintained on valid tiles outside camp. Walk or jump onto
a flag, then approach within nine tiles of your guild's base before its
sixty-second deadline. One planted flag is worth ten points, independent of
level. An enemy killer can inherit the carrier's remaining time and the same
flag identity. Death never duplicates a ground flag. Logout, departure and
expiry discard possession. The Flag Marshal near camp returns players home.

## Rewards and edition decisions

- A saved flag delivery earns achievement **10230**.
- A decisive winning guild's contributors earn achievement **10301**, including
  players who disconnect after contributing. Joining without contributing does
  not earn victory. Contribution means accepted base damage or a delivered flag.
- The current leader of the winning guild qualifies for **Victor (20020003)**.
  Leadership changes transfer eligibility; the next decisive champion replaces
  the previous winning guild.

Victor belongs to the local CTF champion guild's leader. Conqueror and Radiant
Wings require cross-server CTF and are not local rewards.
[Official wardrobe requirements](https://co.99.com/guide/quest/2016/wardrobe.shtml).

Revora v1 requires contributors from at least two guilds and a unique positive
top score. A tie, an uncontested battle or a scoreless battle records that outcome
and preserves the previous champion. These attendance and tie rules are Revora
decisions, not claims about undocumented official behavior. They do not establish
that two guilds necessarily represent unrelated real-world people.

This edition supplies the wardrobe and achievement earning path. Legacy CP/silver
donation settlement, pickup/kill exploit rewards, rotating cross-server bonuses
and native ranking UI are not enabled. Brief dialogue and normal chat provide
instructions and scores. The existing optional 21:15 Steed Race overlaps this
weekly window; a wider event-calendar adjustment remains separate work.

## Persistence and recovery

`revora_ctf_runs`, `revora_ctf_members` and `revora_ctf_deliveries` are additive
tables. An event lock owns base combat and possession. Database run-row locks
serialize registration, objective receipts and finalization. Each flag has a
unique token: retries cannot create extra deliveries. A delivery and its
achievement commit together; a result, champion guild and victory achievements
also commit together. Client notifications and memory installation follow commit.

A failed write remains retryable. Startup cancels unfinished runs and clears
temporary objectives; it does not invent a winner or replay the cancelled slot.
Already committed planting achievements and prior champions survive cancellation.
Existing login handling returns characters saved in map 2057 to Twin City.

NPC access validates the current server session and actual guild membership.
Stale sessions, fake adventurers, remote NPC calls and guild switching cannot
establish participation. Ordinary gameplay creates all reward evidence.

## Validation and rollout

The isolated executable passed **48 CTF, 63 progression and 91 wardrobe checks**
(202 total). CTF fixtures use the actual installed map floor and read the three
base records and Guild Controller location; player identities and all writes
belong to a disposable PostgreSQL schema, removed after the run.

Checks cover normal NPC signup and return, actual SobNpc combat, PvP access,
concurrent pickup, delivery/expiry, death transfer, logout, changed guilds,
stale sessions, database rollback, repeated finalization, persisted wardrobe
reload, leadership transfer, insufficient attendance, ties, restart and both
Pacific summer and winter scheduling.

Run `tests/PotencyHunt/Verify-PotencyHunt.ps1 -CtfOnly` after building into the
existing `obj/chi-refill-validation/bin` directory. Related switches are
`-ProgressionOnly` and `-TitlesOnly`. Run these fixtures sequentially because they
share the disposable schema name.

Validated staged executable SHA-256:
`bb84a3de0eee921b205f5f2aeb38b665dc10a3d0e749ac98e932bd3fc78433c9`.

No server binary, live database migration or client patch was deployed for this
milestone. The M1 legacy-achievement migration review still applies. Before a
live rollout, preserve the current binary and database backup, then validate a
fresh two-guild match with genuine 6711 clients: flag/base visuals, movement,
death/return, immediate achievement state and Victor appearance/equipment.
Packet 2224 remains disabled; see [protocol evidence](PROTOCOL_RESEARCH.md).

Rollback uses the prior server binary and preserved database evidence. Do not
delete completed achievement receipts or restore a pre-event data backup over
new player progress merely to revert scheduling code.


## Final result write verification (2026-09-26)

Finalization now requires exactly one saved champion row (when there is a
winner) and one active-to-complete run update. Previously a database trigger
could suppress either write without raising an error; finalization would still
return success, potentially leaving the title, victory evidence and result out
of agreement. Both required writes now fail the transaction on zero affected
rows, retaining the active run for retry.

The isolated build and 56 CTF checks pass, including eight new assertions for
suppressed champion/run writes, rollback of champion and victory evidence,
successful retry and repeated finalization. The fixtures use ordinary registered
guild participants and a disposable database. Logs are
`obj/ctf-result-build.log` and `obj/ctf-result-tests.log` (ignored). Existing native
acceptance and deployment gates remain open. This repair adds no reward category
or achievement producer and does not certify the entire event reward economy.


## Contributor prizes (2026-09-26)

New local Revora CTF rounds award one bound random P7 soul to each contributing
member of the winning guild. Contribution uses the existing saved base-combat or
flag-delivery receipt; registration alone does not qualify. The existing minimum
two contributing guilds and untied score rules still apply. Victor remains the
leader's separate wardrobe reward. This is an owner-directed Revora reward
addition, not an original retail reward claim.

A new round freezes `valuable_reward=true`; migration gives existing rounds
`false`. Finalization chooses each soul from the loaded P7 catalog and saves its
item type in `revora_ctf_prizes` with the result, champion and achievement
transaction. A failed prize insert rolls the whole result back. Replay returns
the saved result without rerolling or adding entitlements. Cancelled, tied,
uncontested and historical rounds receive no new prize.

The Guild Controller's Capture the Flag menu offers **Claim my victory prize**
at any time. Each request takes the oldest pending prize. Claiming requires an
ordinary canonical session near NPC 380 in Twin City, its session-installation
lease, inventory lock and saved owner/prize row locks. The new bound item and
claim receipt commit together; only then is the client inventory updated. A full
bag leaves the prize pending. A failed write rolls back both item and receipt;
post-commit failure requires reconnect. Saved prizes do not expire, require an
online character at settlement, or require continued guild membership. Concurrent
requests cannot duplicate a particular prize; multiple earned rounds retain
separate claims.

Validation: isolated build and **74 CTF checks** pass. New checks cover old reward
editions, winning/losing/idle eligibility, suppressed entitlement and claim
writes, NPC dispatch, remote/fake rejection, full bags, a pending prize across
CTF initialization, fresh-session and item reload, guild departure, concurrent
claims and replay. The fixture now uses the full disposable character schema
for genuine production loading. Logs: `obj/ctf-prize-build.log` and
`obj/ctf-prize-tests.log` (ignored). Actual process restart, native dialogue/item
presentation and production deployment remain pending.

## Earned contributor prizes across actual processes (2026-09-27)

`tools/Verify-ArenaProcess.ps1 -LocalCtfOnly` now passes three game launches and
one database restart. It reuses the private gameplay binary SHA256
`E9EEF3BBD597B78F7823E6314E15A4E9183C15525E2FE9E561E392155A85526E`.
Evidence: ignored `obj/local-ctf-process.log`.

- [x] Four ordinary profiles enter through loaded NPC380 during a controlled
  Saturday window. Two winning-guild contributors and one rival capture the
  three installed bases through ReceiveAttack; an idle winning-guild member
  registers but never contributes.
- [x] The winning leader picks up an actual ground flag and delivers it through
  the event movement handler. Normal deadline settlement records the winner
  and two pending prizes; neither the rival nor idle member receives a prize.
- [x] Force game exit before claiming, restart game/database, then claim through
  the ordinary NPC dispatcher. Both contributors receive their frozen catalog
  P7 type, bound and permanent. Repeated claims pay nothing extra.
- [x] Another game process preserves exact soul UIDs/types/binding and original
  claim timestamps. Rival and idle inventories remain empty, and CP/RVP balances
  are unchanged. The run still has exactly two prize rows.
- [ ] Authenticated/native combat, automatic movement dispatch, natural calendar
  timing, interrupted active-battle recovery and live deployment remain separate
  gates. Victor's equipped restart was not tested by this prize-focused suite.

The helper supplies guild affiliation/rank, eligibility, proximity, lethal base
damage and a private event clock. It does not insert contributions, deliveries,
victory, prizes or reward items. This is offline-profile server evidence, not a
TCP/native playthrough or an earned guild-creation journey. The first attempt
failed in a reused fingerprint helper that did not support empty inventories;
the dedicated comparison now covers non-winners as well. The complete rerun
passes without production code changes. Private processes stop and the owned
clone is removed; no live database, service or installed client is changed.

### Earned Victor equipped recovery (2026-09-27)

The three-phase `-LocalCtfOnly` route now additionally verifies Victor20020003
immediately after the earned result and after both process restarts. Only the
winning guild leader owns and can equip it; the contributing member, idle member
and rival leader cannot. Each restart requires the leader's saved selection
before sending any new equip request. All four profiles also attempt Conqueror
20030004 and Radiant Wings60020021: neither is owned/equippable from this local
result, and rejected requests do not disturb Victor or add wings.

All three phases pass in `obj/local-ctf-victor-process.log`, reusing the same
gameplay hash above. Exact contributor prizes and claim timestamps are checked
again by that route. This closes the earlier equipped-Victor process gap; it
does not establish leadership-transfer earning, authenticated/native play or
deployment. No production build, rule, prize or live state changed.

### Walking dispatch and bounded jump delivery (2026-09-27)

The same process route now picks up its actual generated flag using a sealed
`GroundMovement` frame through `PacketHandler.HandlePacket`. It checks the final
coordinates and carrying flag. Delivery uses `GeneralData.PlayerJump`, starting
outside the base's delivery radius and landing on a valid tile inside it. The
saved delivery, flag removal, winner, exact P7 prizes and equipped Victor then
pass the existing three-phase restart checks.

The helper no longer directly invokes `CaptureTheFlag.Move`. Both successful
movement handlers reach `CheckForFlag` in `GeneralData`; the walk also exercises
normal packet dispatch. This does not establish a native wire capture, a jump
packet decode, authenticated TCP play or a complete walking route from flag to
base. Initial approach positions remain supplied test setup. Walkable approach
and landing tiles are selected from the installed map within bounded ranges;
the original test's assumed base-adjacent landing was blocked and was replaced
without editing terrain or relaxing production movement checks.

All three phases pass in `obj/local-ctf-movement-process.log` using the same
gameplay hash above. Only the private helper changed; native and deployment
gates remain pending. Earlier direct-movement test scope is superseded for this
pickup/delivery checkpoint.


### Active-battle recovery audit (2026-10-01)

This audit identified the gaps repaired by the implementation below. The source
at audit commit dfb8b142 had these gaps:

- `Game/Features/Tournaments/CaptureTheFlag.cs:25,128,216` retains original PK
  modes only in an in-memory dictionary tied to the original session reference.
  `CtfResults.Initialize` cancels active rounds after restart, but the member
  schema has no saved mode or return acknowledgement. A replacement session cannot
  recover that original mode from the persisted admission.
- `CaptureTheFlag.SignUp` registers the member, sets Team mode and attempts the
  teleport, then returns success without verifying destination. A failed transfer
  can leave an admitted town player in event mode. Landing selection also occurs
  after registration; move validation before committing an admission reservation.
- GeneralData's existing map-2057 login fallback teleports to 1002,408,330. Thus
  **lack of a town escape is not established**. The missing guarantee is durable
  original-mode restoration and acknowledged recovery, including transfer failure.
- `Entity.Teleport` invokes CTF Leave while departing map2057. Any recovery design
  must avoid recursively teleporting from that Leave callback. Restore/acknowledge
  only after the outer destination is reached and saved.

Repair checklist (server scope below; native acceptance remains open):

- [x] Save original mode before transfer; preserve pending ownership on failure,
  and permit safe same-round retry after a completed return.
- [x] Verify actual entry and restore failed-transfer state without inventing
  contributions or changing previously committed prize eligibility.
- [x] Recover replacement sessions after startup cancellation and normal exits;
  persist destination before acknowledging, with retry after failed acknowledgement.
- [x] Run interrupted active-round and failed-transfer/retry cases through private
  game/database restarts with ordinary characters, exact prior rewards unchanged,
  no cancelled-round winner/prize, and replay checks.

The preceding findings are historical source evidence; runtime evidence follows.
Existing CTF earning, contributor payout and wardrobe checks remain valid within
their documented scope. Native/network-stress and deployment gates remain open.


### Durable returns and failed-entry recovery (2026-10-01)

`revora_ctf_returns` now saves one pending return per character, its round and
original PK mode before the entry teleport. Completed returns can be rearmed for
same-round re-entry; an outstanding reservation cannot be overwritten. Landing is
checked before registration, and a wrong destination returns failure and restores
the original mode. Existing contribution and prize rows are not erased on return.

The login hook and CTF tick recover pending players after cancellation, explicit
exit or failed entry. Town destination and mode are saved before the return is
acknowledged. An acknowledgement failure fences the session and leaves recovery
pending. Leave only requests recovery and restores the in-memory mode; the actual
teleport runs outside that callback, avoiding recursive departure. Stale sessions
cannot request another session's return. Existing pre-migration admissions have
no recorded original mode to reconstruct; their existing town fallback remains.

Private composed-process evidence:

- Four ordinary retained profiles join through NPC380; original modes are saved,
  all four enter map2057, and the process exits before combat or settlement.
- Fresh startup cancels the round without a winner. Login recovery restores each
  mode and the exact town tile. A trigger suppresses one return acknowledgement:
  three complete, one stays pending and is safely recovered on another process.
- Another cold reload verifies acknowledged returns, no cancelled-round prize,
  and unchanged saved items, CPs/RVPs/silver, variables and earlier Duel results.
- Temporarily removing the destination map/catalog from the private fixture makes
  entry fail. The player keeps the original mode and can re-enter that same round
  after map restoration. Ordinary Flag Marshal exit is acknowledged by the tick;
  a second entry and abrupt exit then recover on another process without a prize.

Guild eligibility, calendar and approach positions are supplied. This is not
native/TCP acceptance or an earned guild-creation journey. The first retry fixture
incorrectly expected already-returned players to be teleported again after they
had approached the claim NPC; it now checks their position remains unchanged.
Concurrent tick recovery also caused nonblocking lock refusals during fixture
login. The explicit login fixture now freezes that tick and permits bounded
transient-lock retries. Tick recovery itself passes the normal-exit case. No
claims are fabricated or paid to work around fixture failures.

The six lifecycle phases used executable SHA-256
`0DEB4F99EDB7D6EECE4F72EFAA2EB3B14017CA5A6508632A215DF8F0664DA3AB`.
Final build with the stale-session request guard:
`61DB4C2F5EFE59F1F4B061AACC9D6AB9ED983EC8F05F64EB5AA4ABA59E715D46`.
Tests are `CtfInterrupted`, `CtfTransfer`, and `CtfStaleReturn` in
`tools/CheckCandidateCareerRites.cs`; logs use their `career-ctf-*` phase names.
The stale-session check supplies a return reservation to isolate owner replacement;
it passes on the final build: stale Leave does not mark the replacement reservation,
stale recovery is rejected, and the canonical session restores its mode and town
destination without changing prior assets. Both builds pass with existing warnings.
No live migration, restart, release or deployment occurred.

## Retained founded guild earns CTF (2026-10-01)

This checkpoint uses the retained ordinary character's normally founded
CareerBanner guild and the ordinary peer's normally founded RvpBanner guild.
It does not inject affiliation, combat stats, contribution rows or a winner.
The retained character begins as the level-140 Windwalker left by the completed
all-class rune journey; its saved items and earlier achievements are preserved.

- [x] Both saved guild members enter through Guild Controller NPC380 option11.
  The peer lands one normal melee attack on a different base, earning a saved
  contribution. The retained character captures a 20,000,000-HP base with 876
  normal melee attacks through Attack.HandleAttack and normal damage calculation.
- [x] A sealed walk packet picks up an event-spawned flag; another walk delivers
  it to the captured base. The saved delivery earns10230. Production settlement
  records a winning result,10301, one unclaimed bound P7 entitlement and the two
  permanent leader wardrobe awards (Conqueror20030004/Radiant Wings60020021).
  The opposing contributor receives no victory prize.
- [x] A separate fresh process claims the saved P7 through NPC380 option12.
  Repeating the dialogue gives no second item. Item UID, P7 grade, permanent
  binding and saved claimed state are checked; silver, CPs and RVPs are unchanged.

- [x] A third cold process preserves the exact saved checkpoint and repeats the
  claim twice without another item, currency change or result change. All three
  lifecycle phases pass.
The checkpoint snapshots the retained career, items, paid class changes, prior
receipts, peer items, CTF run/members/delivery/prize rows and leader award rows.
These are two active guild contributors, not a simulated full PvP battle.
Calendar, approach positions and attack cooldowns are controlled. Walking packet
acceptance and normal damage are exercised; continuous navigation, native UI and
natural calendar observation are not claimed. The earlier full-bag and failed
entry/return evidence remains separately scoped above.

Private executable SHA-256:
07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2.
Fixture: RetainedCtf in tools/CheckCandidateCareerRites.cs; private logs:
arena-process-career-retained-ctf-{earn,claim,reload}.log.
No production rebuild, live migration, restart or deployment.

## Fresh paid guilds, capture and cold reward (2026-10-03)

The fresh level133, twice-reborn Warrior continues after the seven subclasses
and max-level rune set. Its level130 Trojan opponent creates FreshRival through
the ordinary Guild Director for25,000 previously earned CPs. Both guild leaders
are ordinary non-GM characters; the original FreshBanner guild is preserved.

Preparation passed, but the first earning phase stopped before event entry:
the saved configuration guild counter had regressed from51103 to51011. Only
the two nested copies of that global counter differed in the complete snapshot.
Guild membership, payment, inventory and progression were unchanged. This exposed
the periodic save overwriting the transactional allocator with a stale in-memory
counter. It was not a missing guild or a CTF admission failure.

`Server.SaveAllocationCounters` now uses a parameterized atomic UPDATE with
GREATEST for both guild/item counters. The guild counter also includes the
largest persisted guild ID, repairing this observed regression. The periodic
save calls that method, and missing configuration rows fail explicitly.
`CheckCandidateAllocationCounters.cs` exercises the production method against
connection-local temporary tables: stale saves, forward saves, existing-guild
repair, other-server isolation and a missing/malicious-looking server key.
The final ordinary save restored51103. The original paid-guild checkpoint then
matched exactly, so guild creation was not replayed and no reward/profile repair
was applied.

The isolated .NET Framework4.8 build passes using the installed Visual Studio
MSBuild and the committed source overlays, plus this change. Unrelated owner
work remains excluded. The parent modern .NET10 Directory.Build.props is not
imported for this legacy build; existing legacy warnings remain reported.
The initial dotnet MSBuild attempt failed on legacy resource generation, and the
first Visual Studio attempt inherited incompatible parent nullable settings.
The counter checks initially passed but used an unrecognized runner marker;
renaming the phase corrected reporting before the CTF retry.

New private candidate SHA-256:
`D48A6C00F6BF84889581ADFEABEB924C4C7448C5DC3C6D17F8674129C43665B6`.
The prior executable and manifest are retained in ignored
`obj/counter-validation` for rollback. This changes only the private candidate,
not the running production server.

On the controlled Saturday2041-08-03 round, both guilds contribute normal base
combat. FreshBanner captures its base in871 calculated melee hits and delivers
one flag through accepted movement. Normal event finalization returns both
participants and records the winner; the opponent receives no winner prize.
A separate process claims exactly one permanent bound P7 soul through NPC380
option12, with no currency changes or repeat payout. The final reload verifies
the exact saved prize UID in the loaded inventory, P7 grade/binding, unchanged
career checkpoint, receipts10230/10301, and permanent leader awards20030004 and
60020021. Claim replay remains harmless.

Fixtures: `CheckCandidateFreshCtf.cs`, `CheckCandidateAllocationCounters.cs`.
Private logs under `C:\Revora\DisposablePostgres\moon-box-candidate`:
`arena-process-career-allocation-counter-save.log` and
`arena-process-career-fresh-ctf-{prepare,inspect,earn,claim,reload}.log`.
The diagnostic snapshot hashes before/current were
`1c1eca8d9eba5dfd3e0984027f32005dfed6786118b1e99436b1f6992a4adc74` and
`5e7d9883c32ddb52f60b28b77b0ec80db98be1a6c592b6e61d090d283ccb4084`.
Calendar, approach and cooldowns are controlled; this is not a full competitive
PvP battle, native display acceptance or deployment. A fixture-only missing
flags declaration was corrected before the initial execution.
