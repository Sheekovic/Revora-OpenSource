# Guild PK Tournament restoration and Way of Heroes 607

Status (2026-10-03): complete server-side through normal startup, ordinary
qualification/matches/rewards, stage607 claim/use and cold persistence. Dated
sections below retain the implementation history. Native client acceptance and
production deployment remain pending; no live process was changed.

## Evidence and identity

The installed candidate `database/Slayer/stagegoal.txt`, section 6-7, requires
one Guild PK Tournament entry (task type 32) and awards one bound EXP Ball
723700. Stage607 was initially absent from the owned rules; it is now implemented
in `Game/Features/WayOfHeroesClaims.cs` and privately validated below.

The [official tournament guide](https://co.99.com/guide/quests/guild_tips.shtml)
describes eight qualifying guilds, seven-day membership eligibility, two groups,
round-robin matches and a Saturday final. Banner possession earns points.
Its weekday time table says 20:30 while its prose says 21:30; exact timing is
therefore contradictory, not verified. These are historical rules, not proof
that the installed server implements them.

The [official expansion overview](https://co.99.com/activity/2010expansion/legendsreturnseason2/index.shtml)
separately identifies Guild Contest qualification and the final-eight tournament.
Confidence is high that ordinary Guild War participation alone is insufficient.

## Initial code findings (before restoration)

- `Game/Features/Tournaments/EliteGuildWar.cs` owns a pole battle on map 2071,
  with a different schedule and capture ledger. It does not establish entry
  into the two-group tournament. Do not substitute its winner/capture rows.
- `Game/Features/GuildSeasonWar.cs` owns five Sunday rounds on map 1038.
  Its contribution records likewise do not prove tournament entry.
- `Network/GamePackets/Data.cs` and `UnionData.cs` contain the enum value
  `GuildPKTourny = 3249`. An enum is not packet-layout or handler evidence.
- A bounded production-source search found no named Guild PK implementation;
  this is not proof that every differently named legacy route is absent.
- `WayOfHeroesClaims.RecordGuildMembership` inserts one row per character
  with `ON CONFLICT(uid) DO NOTHING`. This is permanent join-achievement
  evidence, **not current uninterrupted membership age**. Reusing its timestamp
  would admit guild-switching characters incorrectly. Preserve that achievement
  history and establish separate current-membership tenure evidence.

## Implementation sequence

- [x] Identify the installed stage and distinguish it from existing guild wars.
- [x] Audit current join, leave, kick, disband and identity-incarnation routes;
  add transactional tenure records before enforcing seven-day eligibility.
  Unknown legacy tenure must not be backdated or guessed.
- [x] Establish qualification, deterministic ties, and persisted weekly seeds.
  Document Revora scheduling where the historical source contradicts itself.
- [x] Implement persisted round-robin/final ownership, isolated battlefields,
  ordinary NPC entry, banner combat, scoring, exits and bounded recovery.
  No sponsorship wagering is needed for this earning requirement.
- [x] Persist character entry only after eligible admission into the actual
  match; a signup, map ID, runtime counter or failed teleport is insufficient.
- [x] Enable 607 through committed entry evidence and the existing atomic
  one-time claim transaction; retain the installed bound EXP Ball reward.
- [x] Validate non-GM admission, tenure boundaries and guild switching,
  duplicate entry/claims, interruption recovery, reward use and cold reload.
- [ ] Complete native-client acceptance separately before live deployment.

Stage607 now raises server-side Way of Heroes coverage to77/79. These checks
cover tournament ownership, not every legacy guild-management crash projection.

## Admission-record prerequisite (2026-10-02)

`WayOfHeroesClaims.RecordGuildMembership` now also writes
`revora_guild_admissions` in the caller's existing transaction. Founding calls
it from `Game/Features/GuildFounding.cs`; ordinary recruitment calls it from
`GuildAdmission.Join` in `Sistemas/Union/UnionTable.cs`. Each admission replaces
the latest guild, database timestamp and unique admission ID. Rejoining even
the same guild starts a new record. Permanent `revora_guild_earning` history
retains its original meaning. No legacy dates are invented.

The new table is **latest admission evidence, not yet an eligibility source**.
`Guild.ExpelMember` still updates guild rank and guild ID separately after
runtime changes. `Guild.Disband` and `Database.GuildTable.Disband` likewise
contain multiple operations. These paths need transactional departure ownership
and crash validation before uninterrupted tenure can be trusted. A tournament
check must also confirm the current character and guild lifetimes, not merely
compare a stored guild number.

Validation: private candidate build passed; `career-guild-admission` passed on
executable SHA256
`711C8BE074ED465520388F9AB8C0BB308551A28DD5B36C4DC7926AABE2F62119`.
The fixture calls the production recording method in a rolled-back transaction
against a retained ordinary-character row. It verifies same-guild reset,
guild switch, unique admission IDs, unchanged join-achievement history,
rejection without a transaction, and rollback restoration. It does not claim
native recruitment, committed cold-reload, departure or tournament coverage.
No production deployment occurred.

## Departure invalidation prerequisite (2026-10-02)

Database triggers now remove latest-admission evidence in the same transaction
as a guild-ID change, a zero guild/rank update, guild deletion, or guild-ID
replacement. Unchanged membership and ordinary rank changes retain the date.
This covers committed legacy departure writes without pretending their whole
runtime projection sequence is atomic. A failed transaction restores the
admission record along with the membership change. Guild-ID reuse cannot
inherit the deleted guild's admission record. An index bounds lookup by guild.

The recording method now locks and checks saved character membership before
writing either earning record. This guards against recording a different guild
or recording membership after departure. Trigger search paths are bound at
installation, following the existing repository pattern.

The expanded `career-guild-admission` candidate fixture passed on SHA256
`02599165D35AFAFA72C72BD78C1B7D5816E127C03344937510AFC5623666E6C5`.
It checks rank-zero and guild-zero invalidation, unchanged-membership saves,
mismatched admission refusal, guild deletion/ID replacement and savepoint
rollback. Disposable guild rows and all fixture membership changes roll back;
retained player data is preserved. The build and whitespace checks passed.

Still pending: full ordinary leave/kick/disband handler recovery, committed
cold-process tenure tests, eligibility clock boundaries, tournament qualification,
match ownership and actual event entry. No stage607 completion is claimed.

## Seven-day predicate and ordinary recruitment (2026-10-02)

`SevenDayGuildMembership` requires a transaction and a UTC decision time. It
matches current character membership, an existing guild and a recorded admission,
then requires seven elapsed days. Matching rows are share-locked until the
caller commits or rolls back; eventual event admission must save its decision
inside that same transaction. The method has no tournament consumer yet.

- [x] Candidate ordinary founder uses the existing guild NPC packet route;
  a second ordinary character accepts an invitation through guild packets.
- [x] Ordinary quit removes admission evidence; rejoining creates a new ID.
- [x] Entity saves preserve admission records, membership and CP balances;
  a separate cold process compares exact saved snapshots.
- [x] Before seven days is refused; exactly seven days and later are accepted;
  missing/wrong guild IDs are refused.
- [x] A legacy guild member without an admission date is refused even with a
  far-future decision clock. A competing connection cannot acquire an exclusive
  character-row lock while the eligibility transaction holds its shared lock.

Candidate build and earn/reload phases passed on SHA256
`FAB931EC5C5BCD148B12090B85FF88B03A18C2E9EAA335D15FD33758A7CBB352`.
The fixture supplies initial levels and funds, but uses production handlers for
founding, invitation, joining, quitting and rejoining. Native sockets/rendering,
crash recovery during quit/kick/disband, and actual tournament admission remain
unvalidated. Completion remains 76/79; no live deployment.

## Qualification and pairing rules (2026-10-02)

The [official Guild Contest guide](https://co.99.com/guide/quests/guild_contest.shtml)
identifies a Monday monster-hunting qualifier, four level bands and eight
qualifying guilds. Scores are ordered by points, then by who first reached the
score. This supplies the missing qualification identity; existing pole captures
must not be substituted for these hunt scores.

`Game/Features/Tournaments/GuildPkRules.cs` implements pure qualification and
pairing rules, with no rewards, saved scores or enabled event yet:

- Positive committed scores select at most eight distinct guilds, ordered by
  descending points and ascending attainment time. Equal timestamps use guild
  ID as a documented Revora fallback. Duplicate guild records are rejected.
- Odd/even seeds form the two groups from the official tournament guide.
  Three rounds cover every pair within each group exactly once, with no guild
  fighting twice in a round.
- Vacant seeds create no match, participation credit or synthetic victory.
  This supports sparse qualification without inventing opponents; handling
  final standings and unopposed finals remains pending.
- Level-band boundaries follow the qualifier guide.

`tools/Verify-GuildPkRules.ps1` passes with warnings treated as errors. It checks
point/time/ID ordering, input reversal, duplicate/invalid data, every guild pair
for each field size from zero through eight, per-round conflicts, and level
boundaries. Game compilation also passes. The next integration must supply
durable monster-kill scoring and freeze weekly seeds before creating matches.
Neither qualification nor stage607 is player-accessible yet.

## Durable scoring and seed freeze (2026-10-02)

`Database/GuildContestTable.cs` adds a disconnected persistence component for
contest windows, entries, registered monster lives, kill decisions and frozen
seeds. Startup does not initialize it and no gameplay handler calls it yet.
Runtime integration must own contest creation, genuine arrival, map/band and
monster-life registration, guild lifetimes and authenticated killer attribution.
The ledger alone does not prove that a player killed a monster.

Scoring serializes against the contest row, checks the scoring window, current
saved guild membership, registered entry and matching monster map/band. A life
can score once. Same-owner receipt recovery remains possible after closing;
another owner cannot take that receipt. Freeze refuses before the deadline,
orders committed kill counts through `GuildPkRules`, and commits all seeds plus
the frozen state together. Later freeze calls read the saved seeds. Lock and
statement waits are bounded.

The candidate `career-guild-ledger-earn` and `-reload` phases passed on SHA256
`0524D10B2A0907579BC53849EE60DC1995E035602A8C72450D30296B634DFE64`:

- [x] Eight concurrent copies of one kill produce one receipt; wrong owner,
  absent entrant, unknown life, wrong band and out-of-window attempts fail.
- [x] Ten unique lives yield exactly six and four points for two fixture guilds.
- [x] Concurrent freezes and a separate cold process retain those seeds/counts;
  closing prevents new scoring and retains same-owner receipt recovery.

These tests supply entry and monster-registration rows; ordinary event earning
is not validated. An initial fixture serialized untyped DateTime parameters
eight hours off UTC (measured 28,800 seconds); it was retained. The corrected
fixture uses explicit DateTimeOffset values. Production ledger timestamps
already use the shared explicit timestamptz parameter helper. Game compilation
passes; no deployment or additional completed milestone is claimed.

## Combat attribution integration (2026-10-02)

`GuildContestCombat` binds a registered monster body to a durable life ID,
contest, private map object, family and level band. Registration rejects guards,
revivers, companions, dead/unowned bodies, public maps and duplicate registration.
The normal `Entity` death path consults this binding before irreversible death.
Unregistered monsters bypass it without database access. Registered targets
require a canonical player/session, current guild/member objects, matching map
and band, and an accepted ledger decision. A write refusal leaves the monster
alive for retry. Reviving the body clears its old binding; an event spawner must
register its new life before exposing it again.

Candidate combat validation uses normal calculated melee damage and
`Handle.ReceiveAttack` on a private map. It verifies unrelated kills do not
score, an unentered guildmate cannot finish the target, the eligible killer
scores once, repeated death cannot score again, and a registered new life can
score. A narrowly scoped trigger suppresses a score insert: the monster stays
alive, then the retry succeeds after the trigger is removed. Guard registration
is refused. A cold process verifies the two saved points and frozen seed.

The fixture supplies the contest and entry row and invokes registration; no
scheduled NPC admission or population manager exists yet. These remain required
before activation. Initial combat checks passed on SHA256
`CD9FB31CD5CA7DF26D7BD9F4B35C2EF28022A0BCF1F5E50E108DE39456F87FB9`;
the strengthened runtime ownership/write-refusal batch uses
`BB2C4DDF8CC1467CF4BD6F4B7BC1A0218D69065D9DBF59D5364C29E9D6EE0744`.
Both combat earn/reload phases passed on the latter build. The retained direct
XP kill/cap earn/reload regression also passed. Game compilation and whitespace
checks passed. Stage607 remains disabled; no live deployment occurred.

## Reserved entry and transfer recovery (2026-10-02)

`GuildContestAdmission` now provides a locked-session transfer service. It
checks the five-minute registration window, current guild ownership, level band,
and a registered private map. It commits the return location before teleporting.
Only actual arrival with matching saved membership can create an active entry.
Return disables scoring and closes the transfer. A partial transfer remains
recoverable before listeners start: recovery returns saved event locations to
Twin City and closes the reservation without inventing an entry receipt.
Characters already saved elsewhere are not moved by this recovery.

The database limits each character to one outstanding qualifier transfer.
Map ownership is recorded per contest/band, allowing historical map IDs to be
reused across completed process lifetimes. The runtime coordinator must still
own map creation and cold recovery sequencing before this service is exposed.

Candidate `career-guild-entry-earn` / `-reload` checks cover too-early admission,
actual teleport/confirmation, duplicate entry refusal, return, rejection of
confirmation while still in town, interruption after teleport but before
confirmation, and denial of new scoring after return. Tests use an ordinary
retained guild member and real teleportation; the candidate supplies contest/map
registration and directly calls the transfer service. NPC and scheduler access
are not yet implemented.

Initial entry/recovery checks passed on
`013F16C26B2FC43560796B9AE96D33AF7D532C72D316412F1B39C0A79C63A861`.
The tightened guild/band/clock checks and post-return scoring case pass on
`6B02CAE1370C55BA810B77F2E605C543AE5F0CFFEF7BDA3333BCEACD11FDFB62`.
A fixture revision first collided with a retained weekly window; the unique
start constraint rejected it. The revised fixture uses a distinct week and
preserves prior rows. No live deployment or stage607 completion is claimed.
Game compilation passed; retained score-ledger earn/reload regression passed on
the tightened build. Startup recovery remains explicitly invoked by the private
fixture until the event coordinator is integrated.

## Scheduled qualifier coordinator (2026-10-02)

`GuildContestCoordinator` now owns the Monday registration window, four private
level-band maps, bounded populations and scheduled closure. The common NPC router
and world tick call it, but production startup does **not** activate it yet.
Cold preparation returns interrupted entrants and cancels unfinished contests;
it preserves frozen results. Activation requires that preparation first.

Documented Revora map/population choices:

- Four separate private copies of installed map700 replace the historical
  shared map's four districts. The registrar is placed near Twin City409,288,
  avoiding existing NPCs; each private map has an exit envoy.
- Each band targets16 monsters, adding at most4 per two-second pulse. Dead
  bodies are removed and replacements receive fresh life registrations.
- Loaded profile IDs55,58,2411,2416 supply appearance and combat values. Local
  RedDevilL117 is level135 and IcySerpent is122; event copies use the guide's
  levels97,117,120,130. The installed IcyBladeDevil profile supplies the fourth
  band because an exact BladeDevil profile is absent. Copies use isolated family
  IDs950607–950610, suppress ordinary loot and leave global profiles unchanged.

These are Revora adaptations, not verified original terrain, population density
or combat balance. The [official guide](https://co.99.com/guide/quests/guild_contest.shtml)
supports the calendar, level bands and qualification objective.

Candidate `career-guild-coordinator-earn` and `-reload` pass on
`3B6B8B213DEC1742BE964A0AC61531B7653A1C439227A65F9211C268F529762F`:

- [x] Ordinary NPC admission at levels99,119,129,130 reaches four distinct maps.
- [x] Real calculated melee deaths earn four saved points for their guild.
- [x] Populations reach16 per band and replace killed monsters within that cap.
- [x] Closing freezes the score, returns online players, and safely updates an
  offline entrant's saved location under a session lease.
- [x] All four maps are removed after return; the final seed survives cold load.
- [x] Disposed maps release their monster timers and cannot recreate them.

`Map.Dispose` previously retained its monster timer; disposal now clears and
disposes it, timer creation rejects disposed maps, and callbacks reject disposed
owners. Tests verify timer references clear and stay clear after another start
attempt. This change is shared infrastructure, not a claim about total RAM usage.

Fixture corrections: the first recruitment setup omitted the normal partner and
mentor loaders; they are now loaded. A retry crossed daylight saving time using
seven UTC days and missed registration; retries now advance local Mondays and
assert the UTC window. Earlier fixtures/results remain retained. The deterministic
test holds the coordinator gate while advancing its clock; this is not native
client or unrestricted live-concurrency acceptance.

Still required before activation: qualifier prize claims/live score display,
Guild PK group and final matches, combat/exit edge cases, stage607 claims and
native acceptance. The 76/79 count is unchanged. No live deployment occurred.
Game compilation and whitespace checks passed. The existing Champion
Blade/Twilight private-arena regression also passed on the final build.


## Qualifier scoreboard and cancelled finalization (2026-10-02)

The coordinator now sends the remaining hunt time and top eight guild scores to
players in its four owned maps every six seconds. It reuses the existing Message
right-corner channels 2108/2109; no new packet layout is inferred. A single bounded
query reads committed kill counts while open, or saved seeds once frozen. Ordering
matches qualification: points descending, time of reaching that score ascending,
then guild ID. Cancelled contests have no standings and are explicitly rejected
by finalization. Returning entrants receive the existing scoreboard-clear message.

Private candidate executable:
`DC72103B6C4FBD2A095262E10A43BFF5B81F696102F3F82828273513290845CF`.

- [x] Game build succeeds with the existing isolated committed-source overlay.
- [x] `career-guild-coordinator-earn` repeats actual four-band NPC admission,
  calculated monster kills, replenishment, closure, offline return and timer cleanup.
- [x] Live score text reflects the four committed kills and the remaining time.
- [x] Frozen score text retains four points in a separate cold process.
- [x] Cancelling a contest prevents finalization and produces no seeds or scores.
- [ ] Native client rendering, scoreboard clearing and live acceptance remain pending.

The v3 candidate probe preserves earlier v1/v2 results and uses the next unused
local Monday. The deterministic fixture holds the coordinator gate while advancing
time; it does not establish unrestricted live concurrency or visible DX9 output.
No production activation, deployment or client patch occurred. Qualifier treasury
prize claims and Guild PK matches are the next work; stage607 and the 76/79 count
remain unchanged.


## Qualifier treasury prizes (2026-10-02)

The [official Guild Contest guide](https://co.99.com/guide/quests/guild_contest.shtml)
was checked again: ranks1-8 award60M,50M,40M,35M,30M,25M,20M,15M silver;
only a winning guild's leader or deputy may claim. It does not clearly specify
whether that silver enters the officer's purse or the guild fund.

Documented Revora choices: credit the guild treasury, use the existing registrar
instead of eight separate awarders, and allow the oldest unclaimed qualifying
hunt to be collected without an invented expiry. A two-minute NPC offer names
one exact contest internally; replaying that offer never advances to another
week's award. Current standard deputy rank990 and leader1000 are eligible;
honorary deputy rank980 and ordinary members cannot claim.

`revora_guild_contest_owners` is bound transactionally on confirmed arrival.
Deleting a guild or changing its ID invalidates its contest bindings in the same
transaction; recreating an old ID cannot revive them. Fresh kill receipts require
an eligible binding. Old private fixtures without that evidence are not backfilled
into claimable prizes. Production remains inactive, so no live entitlement is lost.

A prize transaction locks the frozen contest, saved officer and current guild,
checks the binding and rank, and saves the unique receipt plus treasury delta
atomically. The NPC holds session, inventory and guild ownership through commit
and updates the loaded guild fund before sending its normal guild packet.
No character purse is credited. Guild deletion after payment retains the audit
receipt. The existing unused `GuildTable.SaveFunds` has no active caller; normal
donations, arsenal spending and beast funding use committed fund balances.

Validation:

- [x] The private v4 coordinator fixture completes an ordinary four-band hunt.
- [x] A non-GM deputy claims60M through the real NPC router; an ordinary member
  and a request without a valid offer receive nothing.
- [x] Repeated leader/deputy clicks leave one receipt and one treasury credit.
- [x] A cold process loads the credited fund and rejects further NPC payment.
- [x] Eight concurrent ledger retries resolve the already-committed receipt.
- [x] Suppressing the treasury update causes failure; rolling back restores the
  prior receipt and exact fund. No partial second prize survives.
- [x] Transactional guild ID replacement/deletion invalidates bindings; reusing
  the old ID cannot bind to that contest. All fixture identity changes roll back.
- [x] Pure rule checks cover all eight exact reward values and invalid ranks.
- [ ] Native visible guild-fund refresh and production acceptance remain pending.

Initial actual NPC earn/reload executable:
`5C5964680E42BCBF52F234EC472E1236D484C17BCB81B1EB1F55D7D9C14E1393`.
Final executable (adds the binding lookup index):
`7F7E5F75DA554EC4372E863F275C5279C3C59E09EAC97E7102B2E3BF95BEB9B2`.
Coordinator earn/reload with retained prize verification and transaction faults
passes on the final executable. The earlier v1-v3 fixtures remain intact.

This completes the qualifier's server-side prize path, not Guild PK group/final
matches or stage607. The 76/79 count remains unchanged. No live deployment,
restart, client patch, or native acceptance is claimed.


## Saved group schedule and final-pairing rules (2026-10-02)

`GuildPkSchedule` creates each weekly group schedule in one transaction from
frozen qualifier seeds with recorded guild bindings. It saves the server timezone,
match identities, opponents, UTC start/end times and one slot per guild per round.
Concurrent/repeated creation returns the existing schedule. Qualifier closure
requests creation only after returning players and disposing its maps. Cold
preparation recovers recent frozen qualifiers that have no schedule yet; older
than seven days are outside the current tournament week. No retrospective matches
or participation receipts are synthesized for missed old weeks.

The [official tournament guide](https://co.99.com/guide/quests/guild_tips.shtml)
contradicts itself about weekday hours. Revora uses its table/admission pairing:
Tuesday-Thursday20:30-22:30, registration20:25-20:30. Saturday finals use21:30-23:30,
registration21:25-21:30. These are documented Revora scheduling choices. UTC times
are calculated using local calendar dates and persisted once.

Pure `GuildPkRules` now calculates group standings only from a complete set of
terminal scheduled results: win3, draw1, loss0; cancelled matches award nothing.
Duplicate, missing, foreign or contradictory match results are refused. Equal
group points use the original qualifier seed as a Revora tie-break. Finals pair
corresponding group positions. A vacant opposite-group position creates no match,
synthetic win or participation credit. Final match outcome/overall-ranking and
reward resolution still require implementation.

Validation on private executable
`F3BA6AD25E177B95CC1B8D9F056C87FC4D8EDC3E117627FEBFB41FA2F6678747`:

- [x] Pure warnings-as-errors checks cover all field sizes0-8, every group pair,
  complete standings, wins/draws/cancellations, tie ordering and final pairing.
- [x] Calendar checks cover all four rounds and reject a non-Monday qualifier.
- [x] The retained ordinary one-guild qualifier gets a weekly record and zero
  fictional group matches.
- [x] A synthetic eight-guild frozen qualifier creates12 matches and24 slots.
  Each guild has exactly three rounds with the expected saved two-hour windows.
- [x] Concurrent creation retains the same match IDs and snapshot on cold reload.
- [x] Recovery creates a fresh synthetic schedule after qualification was saved
  without a week record; subsequent concurrent and cold retries preserve it.
- [ ] Actual match entry, seven-day enforcement at arrival, banner combat,
  result producers, persisted standings/finals and native acceptance remain pending.

The eight-guild input is a private scheduling fixture, not evidence of eight
ordinary guilds completing matches. Neither this schedule nor the pure result
functions enable607. The roadmap count remains76/79; production remains disabled.


## Guild PK admission and interrupted return (2026-10-02)

`GuildPkAdmission` now provides the match-transfer component. The future runtime
coordinator must bind a canonical private map to a scheduled match during its
five-minute admission window. Entry requires the correct guild/opponent slot,
week phase, surviving qualifier guild binding, canonical session/guild objects,
saved current membership and seven days of recorded uninterrupted membership.
There are no GM/name/account exceptions. Each admission decision uses one UTC
snapshot for eligibility and receipt timestamps.

Reservation stores the exact admission UUID and return coordinates. Confirmation
requires real arrival in the same registered map, saved character location and
that same admission UUID, with tenure checked again inside the owning transaction.
A reservation or failed teleport does not create an entry receipt. Same-match
re-entry after leaving is refused. Return preserves a character already elsewhere.
Cold preparation returns unfinished transfers, deactivates entry rows and cancels
matches whose runtime maps belonged to the prior process. It never recreates
participation or resumes an abandoned battlefield.

Private `career-guild-pk-entry-earn`/`-reload` fixture coverage:

- [x] An ordinary non-GM character reaches the canonical private map via real
  `Entity.Teleport`; saved arrival and transfer state agree.
- [x] Before-window map binding and an unrelated dynamic destination are refused.
- [x] Seven days minus1ms refuses entry; exactly seven days accepts it. This
  boundary uses a scoped private timestamp injection, restored and checked exactly.
- [x] Duplicate entry refuses, and voluntary return persists Twin City plus an
  inactive participation row.
- [x] A reservation while still in town cannot confirm arrival.
- [x] Changing the admission UUID after reservation refuses confirmation, even
  when the numeric guild is unchanged; the fixture restores the original UUID.
- [x] A second character left after teleport but before confirmation returns on
  cold startup, with no fabricated entry and the abandoned match cancelled.
- [ ] Player-facing Guild PK NPC routing, camps, banner combat, match outcomes,
  stage607 claims and native client acceptance remain unfinished.

The test supplies a synthetic three-guild qualifier to create a real scheduled
pair; it does not claim that those guilds played an actual tournament. It invokes
the admission component directly rather than a not-yet-written Guild PK NPC.
No production activation, deployment, restart or client patch occurred.

Initial executable:
`BF8BFE983DD76A652DC16EE65AE97D34583FEC82753134846FD39FDCB5C44691`.
Final executable with one timestamp per admission decision:
`648F3DCFA787AC0252B48F35499A280623FB08FF9AAA379004AC04B259E5DAC3`.
Earlier fixture matches and their cancelled outcomes remain retained. The 76/79
count is unchanged; verified match participation and reward claiming still need
end-to-end integration before607 can be enabled.


## Guild PK NPC and arena lifecycle (2026-10-02)

`GuildPkCoordinator` is reached through the existing qualifier NPC/world-tick
hooks, but only after the still-private activation step. It places an emissary
near Twin City412,262, creates up to four isolated map700 arenas during scheduled
registration, and places an exit envoy in each. Actual entry calls the tested
admission component; the NPC cannot create participation by itself. A match's
saved window and guild pairing select its arena.

Registered participants can leave through the envoy, and same-match re-entry is
refused. The coordinator starts the saved match state at its scheduled hour and
returns online and offline participants at closure. It keeps each map until all
pending transfers and online occupants are out, then disposes it. Unregistered
arrivals are sent to Twin City327,303 without receiving entry credit, so they do
not pin a finished arena forever.

The combat/result producer is **not implemented yet**. Consequently these private
lifecycle tests close unresolved arenas as cancelled, with no winner or points.
This is safe incomplete behavior, not a completed playable Guild PK event.
Production activation remains disabled. Opposing camps, the banner, combat and
KO rules, results/finals/rewards,607 and native acceptance are still required.

Private candidate executable:
`ABA40A01B4CCE8670335A0079F67D46087D0CEB697CEF931F4E52E248595390D`.
`career-guild-pk-coordinator-earn` and `-reload` pass:

- [x] Before-window NPC entry refuses without moving the character.
- [x] Four ordinary non-GM characters enter through the real NPC router and
  reach the same scheduled private arena.
- [x] The exit envoy returns one character; re-entry does not add participation.
- [x] The scheduled start changes the saved state to active.
- [x] Closure returns online characters plus one disconnected participant,
  deactivates all four entries, and leaves no pending transfer or registered map.
- [x] Cold verification retains the four genuine arrivals and cancelled outcome,
  with null winner and zero battle scores.
- [ ] Unregistered-arrival cleanup is implemented but not separately exercised.
- [ ] Opposing-guild combat, full four-arena concurrency and native display are
  not established by this fixture.

The fixture supplies a synthetic three-guild qualification and ordinary members
of one participating guild. The two coordinator gates are held during deterministic
clock advancement; this does not prove unrestricted live concurrency. No live
server restart/deployment or client patch occurred; the count remains76/79.


## Opposing camps and combat integration seam (2026-10-02)

Match map binding now records the two scheduled guild identities and separate
terrain-valid camps. Revora anchors these near30,50 and70,50 in private map700,
searches at most eight tiles for a legal landing clear of NPCs, and requires at
least20 tiles between camps. These are Revora layout choices, not verified original
Guild PK map coordinates. Entry chooses the camp from the bound guild identity;
a third guild or unrelated map cannot obtain a camp. The same lookup is available
for the future defeat/respawn route.

Private executable
`C537CD39208A729AE5B6D0FEB3926344C9E64C0271D04151BEE9BC3093D25DE6`
passes `career-guild-pk-coordinator-earn` and `-reload`:

- [x] An ordinary member leaves one guild and joins the opposing guild through
  the real guild invitation/join packet handlers. No GM access or allowlist is used.
- [x] Members of both scheduled guilds enter through the actual Guild PK NPC.
- [x] Both sides reach their registered camps, at least20 tiles apart; a third
  guild cannot resolve a camp in that match.
- [x] The existing voluntary exit, duplicate refusal, online/offline closure and
  cold persisted-arrival checks still pass.

The deterministic clock advances beyond the real saved recruitment timestamp;
the fixture does not backdate that admission. Earlier private match fixtures are
retained. This still uses synthetic qualifying seeds and does not prove real
opposing-guild combat or native presentation.

Code inspection confirms the next integration seam in
`Game/Attacking/Handle.cs`: the `CanAttack(Entity,SobNpcSpawn,...)` and
`ReceiveAttack(Entity,SobNpcSpawn,...)` overloads already delegate owned structures
to `GuildSeasonWar` before generic structure HP changes. A Guild PK banner must
likewise intercept both eligibility and mutation, validate exact arena/actor and
confirmed participant ownership, and persist capture/possession decisions before
awarding results. This is source-code evidence, not a newly verified client packet.
Banner combat, player defeat limits, outcomes/finals and607 remain unfinished.
No live activation/deployment/client patch occurred;76/79 is unchanged.


## Durable banner capture and possession ledger (2026-10-02)

`GuildPkBannerTable` stores banner ownership, capture sequence, capture receipts
and possession time against a registered scheduled match. It is a persistence
component: no game attack handler calls it yet, and its direct invocation cannot
prove a player destroyed a visible banner. The future runtime combat owner must
supply that proof before requesting a capture.

Capture checks active match/time, expected sequence, opposing ownership, current
saved location, exact confirmed arrival/admission identity, current guild lifetime,
seven-day tenure and fewer than20 defeats. The match and participant rows are
locked. The old owner's earned seconds, new capture receipt and new ownership
commit together. Repeating the same numbered decision resolves its receipt;
another actor cannot claim that sequence.

A true replay response means that decision exists; it does **not** assert that its
guild remains the current owner after later captures. The runtime must project
`Pulse`'s current snapshot, including keeper/capture number, rather than blindly
installing the requesting guild. A failed or uncertain capture must leave runtime
HP/ownership unprojected until the committed snapshot has been resolved.

Possession accrues whole seconds, advancing the stored accounting timestamp only
by credited seconds. Repeated ticks cannot add those seconds twice; a capture
starts a new interval and drops the previous owner's fractional second (documented
Revora interpretation). Time is capped at match end. Cancelled/completed matches
cannot accrue more. A dissolved guild loses ownership before any further accrual;
previous committed scores remain historical evidence.

Private executable:
`A3F23A6AFEA574AE6C976F03908091748F6370E5C21E9D2635F4F0D8D8965E36`.
The coordinator earn/reload fixture uses ordinary NPC entrants from both guilds,
then invokes the ledger directly under a deterministic clock:

- [x] Before-start and wrong-admission capture requests refuse.
- [x] Eight concurrent copies save one capture; another actor cannot take that
  receipt, and the owning guild cannot recapture its own banner.
- [x] Three alternating captures credit15 seconds to the first guild and10 to
  the second; repeated possession ticks do not duplicate credit.
- [x] Suppressing the capture insert rolls back the old owner's pending score
  and preserves the previous keeper/sequence; retry after removing the fault succeeds.
- [x] Cold verification preserves three captures and both scores; cancelled-match
  ticks leave the total25 unchanged.
- [ ] Physical banner damage, spawn/movement/HP updates, unknown-commit runtime
  projection, player defeat routing and final results remain unimplemented.

No protocol/layout is added by this ledger. Native acceptance and live activation
remain pending. The fixture's cancelled match is not a tournament victory, and
stage607 stays disabled. The roadmap count remains76/79.


## Spawned banner combat integration (2026-10-02)

`GuildPkBanner` owns the registered arena's exact banner and admitted
session/guild/member references, installed after saved arrival confirmation.
Normal SobNpc attack eligibility and mutation handlers intercept managed arenas.
A copied object, other guild, owning guild, expired window or unregistered session
cannot qualify. Lethal damage requests a durable capture; the committed snapshot
supplies the keeper rather than assuming the requesting guild owns the banner.

Revora uses 1,000,000 HP and the installed Guild War pole appearance. The neutral
anchor is (50,60); capture moves the banner near its owning guild's camp, searching
legal terrain and avoiding NPCs, and restores full HP. Existing SobNpcSpawn
serialization and normal Screen registration expose the attack target. Relocation
removes the old Screen target. Non-magic damage animation precedes authoritative
HP/reset packets. No new wire layout is inferred.

Private executable SHA256:
`5478D6CA04D251B7677293204F830CEA47E34039BE038318D492C32D81BDB54C`.
The coordinator earn and separate cold-reload phases passed with retained fixture
qualifier ending `000018`, ordinary non-GM NPC entrants, real melee calculation
and the actual attack eligibility/mutation handlers:

- [x] No banner during registration; match start spawns it with 1M HP.
- [x] Nearby entrant resolves the canonical banner through Screen.TryGetSob,
  without test-only Screen insertion; movement reload resolves relocated banners.
- [x] Nonlethal damage lowers HP without a capture receipt.
- [x] Copied-target eligibility refuses; direct copied-target damage leaves the
  canonical banner unchanged. Owning-guild eligibility refuses.
- [x] Three alternating lethal captures move/reset the banner and save receipts.
- [x] Suppressed capture INSERT preserves HP, position, keeper and sequence;
  snapshot recovery resolves prior ownership and retry succeeds.
- [x] Possession reaches 7,180/10 at the two-hour cutoff (ten neutral seconds).
  Online/offline return and map cleanup pass.
- [x] Cold reload preserves three captures and total7,190; post-close pulse
  cannot accrue further points.

The injected failure intentionally logs one banner-hit InvalidOperationException.
This tests a rolled-back write, not network loss after COMMIT. Unknown-commit
snapshot recovery is implemented but lacks a separate runtime injection test.
Full client input/rendering, including magic animation ordering, awaits native
acceptance. Player-versus-player rules, defeats, early/terminal results, finals
and rewards remain unfinished. The temporary coordinator cancels unresolved
matches; these scores are not tournament wins. Production activation and607
remain disabled; the roadmap count remains76/79.


## Opposing-player combat ownership (2026-10-02)

Both player attack eligibility and actual damage now validate the active Guild PK
battle and canonical admitted session/guild/member references. A combat lease
holds both session identities, item owners and the battle gate through health
mutation. Contention refuses the hit; it does not wait while holding another
combat owner's locks. Leaving revokes the runtime admission. Existing class
calculation, flying/skill restrictions and PK-mode checks remain in the normal
attack path; no class damage formula is replaced.

The [official Guild PK guide](https://co.99.com/guide/quests/guild_tips.shtml),
rechecked 2026-10-02, describes protection beneath the banner but gives no radius.
Revora defines a three-tile shelter around one's owned banner. Sheltered players
cannot attack outward either. A neutral banner gives no guild this protection.
The radius and outward-attack restriction are documented Revora rules, not claims
about the original wire protocol or exact original implementation.

Private executable SHA256:
`C28964B03414E53F2719335651450060EAD08917E71FF5B5687B5A48BB80A145`.
Coordinator earn/cold-reload phases passed with retained fixture qualifier ending
`000019`:

- [x] Registration-time player attacks fail eligibility and direct damage.
- [x] Same-guild attacks fail both checks.
- [x] A real ordinary unregistered character cannot attack or be attacked from
  another map, or after teleporting into the owned battlefield without admission.
- [x] Opposing admitted players outside shelter deal calculated nonlethal melee
  damage through the normal handler.
- [x] Owning-guild shelter blocks incoming and outgoing eligibility/direct damage.
- [x] Banner targeting/capture, rollback/retry, return/cleanup and cold saved
  capture/score checks still pass in the same fresh fixture.

This batch does not prove class-wide balance, full input-packet/native acceptance,
lethal player routing, the20-defeat cap, result settlement or reward delivery.
Those remain required before activation; normal legacy death consequences must
be replaced for this battlefield before enabling it. No production deployment,
new earning milestone or completed-count increase occurs here.


## Numbered defeats and camp returns (2026-10-02)

`GuildPkDefeatTable` commits a numbered receipt and the entrant's defeat count
in one transaction. It locks the match and current entries and checks both saved
admission identities, opposing eligible guilds, current saved maps, active entry,
seven-day tenure and the match window. Receipt replay resolves before checking
current match state; another killer/admission cannot inherit that numbered defeat.
The twentieth committed defeat deactivates the entry.

The runtime keeps the same request pending across database/projection failures
and blocks that entrant's combat until recovery. Retrying the same receipt cannot
increment another defeat. Recovery rechecks the current canonical session under
its session/item ownership. After a saved defeat, the player heals at the guild
camp; defeat20 uses the durable return-to-town route. Five seconds of recovery
before further combat is a documented Revora choice. Cold interrupted-transfer
recovery still returns entrants rather than recreating an unfinished battle.

The normal lethal player handler now returns before legacy loot/PK/JiangHu death
consequences. The death hook follows the existing Absolution and Slayer branches,
so it does not bypass their normal evaluation, and precedes relic-death wear,
ordinary death credit, guild PK donations and player loot. UID-based death routes
resolve a canonical player killer. Combat is revalidated after reflected damage
so a knocked-out attacker cannot finish the old attack after returning to camp.

Private executable SHA256:
`00477D2C8006E059DDB0081299AE455CD559677D57B85FF0C4B027DF9CA3787F`.
Coordinator earn and cold-reload fixture qualifier ending `000023` cover ordinary
opposing NPC entrants. The victim is set to1HP to produce actual lethal calculated
melee hits without replacing the server's damage calculation:

- [x] Defeats1-19 each save one receipt, heal and return to the exact guild camp.
- [x] Combat is blocked during recovery; the next eligible hit earns the next
  numbered defeat. Every saved receipt can be replayed without another increment.
- [x] Defeat20 returns the player to Twin City; number21 is refused.
- [x] Suppressed defeat INSERT leaves saved count/receipts at zero, HP at1 and
  position unchanged. Combat is blocked while pending. Removing the fault and
  pulsing resumes the original request and returns the player once.
- [x] Killer PK points, victim silver balance and inventory object count remain
  unchanged across all20 defeats.
- [x] Cold reload retains20 receipts and the exact counter, with inactive returned
  entries. Banner captures, possession totals and map closure checks still pass.

The first private defeat fixture used100HP but its calculated hit was not lethal;
that assertion failure did not validate a knockout. It is retained as failed
fixture evidence. The final controlled1HP fixture above exercises actual lethal
hits. No claim is made here about class-wide balance, individual rune activation,
reflection-specific fault injection, disconnected poison attribution or native
client animation. Those combat edge cases remain part of acceptance before live
activation. Unknown network loss after COMMIT is resolved by receipt identity in
code but is not separately injected by the before-INSERT failure test.

Early/end-of-match results, all-opponents elimination settlement, finals and
rewards are still unfinished. Production activation and607 remain disabled;
76/79 is unchanged. No live server restart, deployment or client patch occurred.


## Atomic match settlement (2026-10-02)

`GuildPkBannerTable.Settle` locks the match, accrues committed possession, checks
saved current entrants and writes one result receipt plus terminal match state
in one transaction. The receipt records winner (nullable), reason, exact scores
and settlement time. Captures, defeats and settlement share the match-row lock.
The runtime resolves pending defeats before settling and holds the battle gate,
so no accepted concurrent hit can race its result. A failed result write retains
the arena and retries; closure no longer fabricates a cancellation at the deadline.

`GuildPkRules.Decide` defines the outcomes:

- Both sides have no remaining eligible entries: cancelled, no group points.
- Only one side has remaining entries: that side wins, regardless of banner score.
- With both sides remaining, the deadline compares scores; equal scores draw.
- From30 minutes onward, a positive score at least1.5 times the opponent's score
  wins early. Zero versus zero never qualifies. Decimal comparisons avoid overflow.

Voluntary departure and loss of current guild/admission/map ownership count as
leaving the battlefield; these are documented Revora interpretations. A saved
active disconnected entrant retains its slot until its recorded return/removal;
this batch does not invent a separate reconnection grace period. A failed write
never proves an empty field. Cold recovery still cancels interrupted active
matches, rather than reconstructing an unobserved winner. Previously completed
results remain immutable under repeat settlement and possession pulses.

Private executable SHA256:
`3575BBBA6EF77A9E0E9C7BA798A705BDC163EBE03C7DB11DB3475BFA6EAD647D`.
`Verify-GuildPkRules.ps1` passes the before/at30-minute boundary,149/100 versus
150/100, reversed leads,0/0 and positive/zero, maximum long totals, deadline wins
and draw, empty cancellation and elimination. Existing0-8-guild standings/finals
pairing checks also pass.

Coordinator earn/cold-reload fixture qualifier ending `000024` uses actual
ordinary NPC admission, three captures,20 defeats, voluntary exit and an offline
entrant. It proves:

- [x] Once the last opposing entrant leaves, the remaining guild wins.
- [x] Suppressed result INSERT leaves no receipt and keeps the match active with
  its map/occupants intact; removal of the fault permits the next tick to finish.
- [x] Exactly one elimination result saves winner=first guild and scores152/10.
- [x] Online/offline return and map cleanup occur after successful settlement.
- [x] Eight concurrent settlement retries and a late possession pulse preserve
  the single receipt and total162; cold restart retains that result and20 defeats.

Pure decision coverage is not native/full-input proof of every outcome. Actual
runtime lead/timeout/draw/empty scenarios, missed-window cancellation, saved final
brackets/placements, weekly rewards and activation remain to be completed or
validated. No new milestone is checked off: stage607 stays disabled and76/79
remains unchanged. No live deployment or client patch occurred.


## Saved standings and Saturday final brackets (2026-10-02)

`GuildPkSchedule.Advance` locks the tournament week and cancels scheduled matches
whose admission deadline has passed without an opened arena. It waits for all
group matches to become terminal and requires matching winner/score receipts
for every completed match. Cancelled matches contribute zero group points.
Existing pure rules then determine saved standings and same-place cross-group
finals. Standings, final matches, unique guild/round slots and phase transition
commit together; a partial final write rolls everything back.

Final time uses the week's saved timezone and Saturday21:30-23:30. Vacant opposite
group ranks produce no invented match or victory. A zero-final field can finish
its schedule without a placement or prize. Final matches reuse the established
admission and arena routing; their actual play/placement remains to be validated.
The coordinator polls progression every30 seconds, bounded to16 recent weeks.
It does not backfill old inactive weeks or award historical unobserved results.

Private executable SHA256:
`E679F46A1FA982E1C92F5EADCF3ABBA65EF16398D17FAB4687D33422E0E51DCE`.
Coordinator earn/reload passed. Evidence is deliberately separated:

- [x] Ordinary three-guild fixture `...000025`: the played group result feeds
  saved standings, with3 points for its winner and one final against the other
  group's matching rank. The unmatched third guild receives no synthetic final.
- [x] Synthetic eight-guild fixture `...000026`: missing result receipt refuses;
  an unfinished group match leaves standings/finals absent.
- [x] Suppressed final INSERT rolls back all standings/finals and keeps the group
  phase. Eight concurrent retries create exactly8 standings,4 finals and8 unique
  final slots, pairing equal group ranks at the persisted Saturday time.
- [x] Cold reload preserves the exact final IDs, participants and timestamps.
- [x] Synthetic missed-window fixture `...000027`: immediately before admission
  closes, matches remain scheduled; at the deadline, unopened matches cancel.
  All12 group and4 final windows cancel without a winner, result receipt or entry.
  The eight saved group standings have zero points.

Synthetic scheduling inputs are not earning evidence. The first reload attempt
found a test query that assumed one match per qualifier; it was corrected to
select the group match explicitly once finals exist. The rerun passed with the
same candidate executable and retained final IDs.

Saved final placements, treasury reward claims, full final NPC/combat scenarios,
remaining lead/timeout/draw/empty runtime acceptance and native presentation remain
unfinished. Progression does not manufacture any of them. Production activation
and stage607 remain disabled; the roadmap count stays76/79.


## Final placements and ordinary played final (2026-10-02)

`FinalPlacements` requires exactly the final pairs derived from the saved group
results. Completed finals assign the winner/loser to their bracket's two ranks;
cancelled finals assign neither rank. A missing bracket/opponent does not promote
another guild into a fictional title. Revora's documented final-draw tie-break
uses the stronger original qualifier seed. This is separate from the match's
preserved draw result and is not claimed as a verified original Conquer rule.

`GuildPkSchedule.Advance` now checks all finals are terminal and every completed
final has its matching result receipt. Placement rows and the week's completion
commit atomically. Cancelled-only weeks finish with zero placements. The unique
week/rank and week/guild keys prevent duplicate prizes being inferred from retries.
These rows are earned placements, not yet delivered rewards.

Private executable SHA256:
`0619C3C3D5B8D34D95F7B418B045576159ADE5AC8236956205CC8DA6F8FA20A8`.
Pure rule tests cover0-8-guild fields, final draws/seed ties, reversed winners,
cancelled finals, missing and duplicate results. Coordinator earn/reload passed:

- [x] Ordinary two-guild final fixture `...000028` starts from controlled frozen
  qualifier seeds, then uses the actual Saturday NPC window and separate camps.
  It proves final play; it does not independently prove both guilds earned their
  supplied qualifier seeds through the monster hunt.
- [x] Actual calculated banner capture at10 seconds saves one owner. At just before
  30 minutes the match stays active; at30 minutes the ordinary banner pulse settles
  a lead win with1790/0. The coordinator returns both entrants and disposes the map.
- [x] Rejecting the second placement INSERT rolls back the first, leaving zero
  ranks and the week in finals. Eight concurrent retries save only champion and
  runner-up and complete the week.
- [x] Cold reload preserves the final receipt, exact winner/ranks and returned
  entries. Existing group, bracket and missed-window fixtures remain consistent.
- [x] A week whose16 group/final windows all went unplayed completes with zero
  placements; no reward rank is invented.

Guild treasury prize delivery, individual exploit/reward policy integration,
remaining runtime timeout/draw/empty and combat-edge acceptance, stage607 earning
and native presentation are still outstanding. No production activation, live
restart or client patch occurred;76/79 remains unchanged.


## Guild treasury prize delivery (2026-10-02)

The Guild PK Emissary now offers the saved tournament prize to the current guild
leader or deputy during Saturday23:30 until midnight in the week's saved timezone.
It binds the dialogue offer to that week/guild for two minutes and checks actual
NPC proximity, canonical session/member/guild ownership and current office again
under session, item and guild leadership locks. `OfficerAt` explicitly checks the
correct event NPC; the existing qualifier still checks its own registrar.

The transaction locks the completed week, current officer and guild treasury,
validates retained guild-lifetime ownership and the saved placement, then commits
one prize receipt with both currency deltas. Duplicate claims return the current
treasury balances without another payment. Both balances are projected into the
shared guild object and the claimant receives the guild update. Funds go to the
guild treasury, not the claiming character's purse.

Rank1-8 silver/CP rewards follow the recorded guide table:120M/3000,100M/2000,
80M/1000,65M/600,50M/500,40M/400,30M/300,20M/200. Negative/overflowing balances
refuse before mutation. A completed schedule without an earned placement owes
nothing. A disbanded/reused guild cannot inherit the old owner's claim.

Private executable SHA256:
`381648ABE3E67D2A95D2453B09F1A2959ED3A845F5107023C5EBF2EAF9E8B2F6`.
Pure rules validate all8 payouts and the exact opening/midnight boundaries.
Coordinator earn and cold reload pass against the ordinary played final `...000028`:

- [x] Ordinary-member, before-opening and midnight requests refuse.
- [x] Suppressing the treasury UPDATE rolls back the inserted receipt and both
  balances; no partial silver/CP payout remains.
- [x] The real NPC refuses just before opening and lets a non-GM guild leader
  collect120M silver plus3000CP at opening. Runtime guild balances update.
- [x] Eight concurrent leader/deputy retries return the existing claim, with one
  receipt and unchanged balances. Repeated actual NPC claims do not pay again.
- [x] Separate cold reload preserves the receipt and exact database/runtime funds.

The first NPC test correctly refused because a reused officer helper checked the
qualifier NPC's coordinates. That helper now takes the event registrar explicitly;
the final NPC and restart tests above passed. This was a routing/eligibility bug,
not a database payout to the wrong destination.

Balance-overflow and dissolved-guild guards are implemented but were not separately
fault-injected in this batch. Native treasury display remains pending. Individual
exploit contributions/distributions, stage607 reward integration and the remaining
runtime/native acceptance cases are unfinished;76/79 stays unchanged. No live
activation, server restart or client patch occurred.


## Active participation and stage607 private claim (2026-10-02)

The installed stage6-7 catalog specifies task32, one participation point and one
bound EXP Ball723700. Revora defines participation as confirmed admission plus
presence with an eligible opposing guild member during the active match. Merely
registering, standing in an unrelated map or joining an empty match earns nothing.
Banner shelter is not a reason to deny participation; combat shelter remains intact.

The match pulse holds canonical session/item/match ownership and rechecks current
admission identities, guild membership, seven-day tenure, map, active entries,
defeat limits and retained guild ownership inside the database transaction. The
per-character receipt saves match, guild, both admission identities, opponent and
arrival/acceptance times. It is permanent historical evidence and is not overwritten
by later matches. There is no backfill from legacy map positions or old entries.
The receipt table initializes with Way of Heroes so ordinary progress reads do not
depend on private tournament activation. Stage607 uses the existing atomic
claim/item/progress transaction and the installed first-rebirth level101 gate.

Private candidate SHA256:
`BE513F4222B26702B1E7A19A826CBAED45149CE01F991505EAA59BD971289D93`.
Actual NPC-entered two-guild final fixture`...000029` supplies qualifier seeds,
not participation or reward receipts. The earlier completed final`...000028`
is retained without backfilling evidence.

- [x] Actual pre-start claims leave persisted player state unchanged.
- [x] Suppressing receipt INSERT produces no receipt; the next successful pulse
  records both opposing entrants with exact admissions and valid active times.
- [x] Eight concurrent ordinary claim packets issue one bound EXP Ball and mark
  stage607 done with one point for the eligible non-GM character.
- [x] Actual item use consumes the reward, adds experience and increments the
  EXP Ball counter. Repeated claims leave the saved state unchanged.
- [x] The opposing character retains participation but cannot claim before the
  required rebirth stage; no prize or completion receipt is created.
- [x] A separate cold process preserves both participation receipts, the eligible
  character's consumed reward/experience/counter/progress and duplicate refusal.
- [x] Final placement and guild treasury delivery/replay/cold checks still pass.

The first harness run exposed an incorrect fixture assumption: the opposing
level129 character is unreborn, so stage6 is correctly unavailable. The test now
verifies that refusal instead of supplying rebirth or bypassing eligibility.
Initial harness compilation also corrected the existing lowercase `points` field;
no production field or eligibility rule was changed to accommodate the fixture.

Individual exploit scoring/distribution, broader match acceptance, production
activation and native-client validation remain unfinished. Stage607 is a tested
private integration, not a completed live earning path; the roadmap remains76/79.
No live process, client or public service was changed.


## Kill contribution receipts (2026-10-02)

The [official Guild PK guide](https://co.99.com/guide/quests/guild_tips.shtml)
assigns kill contribution tiers by relative Battle Power. Its lower-tier wording
leaves equal and several weaker-opponent cases unspecified. Revora explicitly uses
one exploit for an equal/weaker opponent, two for an opponent1-20 BP stronger, and
three above that. This fills a rules gap; it is not a verified original formula.

Both server-calculated Battle Powers are captured at the accepted lethal hit while
holding both player sessions/items and the match lock. The pending defeat retains
those values through write/projection retries. The same transaction now saves
Battle Powers and exploits alongside the numbered defeat and victim defeat count.
An identical retry resolves the existing receipt; changed Battle Power snapshots
refuse instead of rewriting credit. Existing historical defeats retain null
contribution fields because their original Battle Powers are unknown. No credit
is inferred from today's equipment or supplied to those old receipts.

Private candidate SHA256:
`69CF1FB960FEE5E026EC9EC775B371251DA53C8DE65798D00654E2439D75D847`.
Fresh group fixture`...000030` exercises ordinary opposing combat and retains the
previous`...000025` observations. Earn and separate cold reload passed:

- [x] Twenty actual calculated lethal hits save exact BP snapshots and tier points
  once each, with camp recovery and twentieth-defeat expulsion still working.
- [x] The first suppressed defeat INSERT leaves neither defeat nor contribution;
  recovery retries the same pending hit successfully.
- [x] Every identical receipt replay preserves totals; changed-BP replays refuse.
- [x] Cold reload preserves all20 scored receipts and the completed match.
- [x] Pure rules cover equal, weaker, +1, +20, +21 and integer-limit differences,
  and reject negative BP. Other schedule/prize/final rules still pass.
- [x] Existing final placement, participation claim, rebirth refusal and treasury
  cold/replay checks remain green.

These are saved kill contributions, not paid individual rewards yet. Banner damage
contributions, revival integration, guild-funded pools and proportional payout
remain next. Live activation/native acceptance remain pending;76/79 is unchanged.


## Atomic banner damage contributions (2026-10-02)

The previously cited official guide awards one exploit per10,000 banner damage.
Revora accumulates each character's accepted damage within a match, including
partial hits across captures, then uses integer division by10,000. Remainders do
not award a point and damage beyond the banner's remaining HP earns nothing.

Each numbered hit saves the actual damage, prior HP/capture, member admission and
guild. Banner HP and hit sequence are now durable. Lethal damage, possession-time
accrual, capture relocation ownership and the contribution receipt commit together;
nonlethal damage and its receipt likewise share one transaction. Runtime projects
only committed HP/capture state. A failed projection re-reads committed state; a
same-number identical retry resolves the receipt, while changed damage refuses.
The contribution view derives totals from these receipts, avoiding a separately
updated points counter that could drift. Historical captures receive no inferred
hits or contribution. Old interrupted matches remain subject to cancellation.

Private candidate SHA256:
`5F35CF93AA72F68B9D281ED8C764211375F852D70D898C5E292244C624E23217`.
Fresh ordinary group fixture`...000031` passes earn and separate cold reload:

- [x] Calculated nonlethal combat saves exact remaining HP and accepted damage.
- [x] Suppressed nonlethal receipt INSERT preserves full HP and zero receipts.
- [x] Suppressed capture INSERT preserves keeper/HP and does not credit damage.
- [x] Suppressed lethal damage receipt rolls back the already-attempted capture;
  the subsequent real hit commits exactly once.
- [x] Three captures and the initial partial hit total2M and1M accepted damage for
  the two attackers, yielding200 and100 exploits. Overkill does not inflate totals.
- [x] Eight concurrent identical hit retries preserve four receipts; altered
  damage and a new hit on one's own banner refuse.
- [x] Cold restart retains exact contribution totals alongside20 scored defeats,
  settled group results and the retained final/claim/treasury regressions.

The authoritative totals are now ready for guild-funded individual distribution.
Revival scoring still needs a genuine player revival route: the current camp
recovery automatically restores health and must not be counted as a paid revival.
Reward pools, distribution and broader/native acceptance remain unfinished. No
production activation, deployment or client patch occurred;76/79 remains accurate.


## Genuine spell revival and camp recovery (2026-10-03)

The original guide grants10 exploits for reviving a guildmate. Immediate automatic
healing previously made that impossible. Revora now documents a five-second downed
period at camp after defeats1-19. A living eligible guildmate can use the ordinary
revival spell during that period. At the boundary, automatic recovery restores
health without contribution; defeat20 still restores/expels the player. Combat
remains blocked until the original recovery deadline even after an early revival.

A recovery receipt is unique to match/victim/defeat number. Paid revival records
the caster's current admission; automatic recovery records no caster. The same
match lock serializes both routes. It validates the saved defeat, current entries,
same guild/map, membership identities, tenure and current match window. Current
session/item/match locks also cover the runtime cast. A failed receipt write leaves
the target down and does not reach spell resource preparation. A committed receipt
can be projected again by the recovery pulse without additional contribution.
The contribution view counts only recorded player revivals, ten points each.

Normal1050/1100 spell handling now acquires the tournament eligibility lease and
commits the event receipt before the existing resurrection. Generic non-tournament
revival keeps its previous behavior. Normal online departure heals a downed entrant;
cold/offline interrupted return preserves positive saved HP and raises zero HP to
one, without inventing a spell revival or contribution.

Private executable SHA256:
`9E3DC865ACF64D66304CBE951662FBCE3B28CBC3541B9C4FDC2FD570A5E98DD0`.
The auto-recovery fixture`...000032` covers the4.999/5-second boundary with19
uncredited recoveries. The ordinary spell fixture`...000036` uses supplied1100
skill availability as a combat prerequisite, not a claimed training achievement:

- [x] Actual spell1100 restores a defeated same-guild player and earns10 exploits.
- [x] Enemy revival is refused; suppressed receipt INSERT keeps the victim down.
- [x] A repeated cast on the living target does not add another receipt.
- [x] Eight concurrent identical ledger retries preserve one credit; another
  caster or changed membership admission cannot take over the receipt.
- [x] Eighteen subsequent automatic recoveries award no points; twentieth-defeat
  expulsion,20 kill contributions and banner contribution totals remain intact.
- [x] Separate cold reload retains the one10-point revival and18 automatic receipts.

An early harness attempted1050 without establishing all its cast prerequisites;
that did not prove a revival. The accepted integration test uses normal1100.
The test also crossed midnight: legitimate daily resets changed EXP Ball usage
and Chi. Historical stage607 experience, items, progress and claim snapshots are
preserved; across dates the unrelated Chi snapshot is excluded, daily usage must
be reset, and current full state must still survive a repeated claim unchanged.
No production daily-reset behavior was changed to make the fixture pass.

Native downed/ghost display,1050-specific cast prerequisites, other resurrection
families, interrupted downed return fault scenarios and post-commit spell resource/
projection fault cases remain acceptance work. Guild-funded reward pools and
individual distribution remain unimplemented. This is private event integration,
not production activation;76/79 remains unchanged.


## Guild-funded member pools and delivery (2026-10-03)

The original guide describes guild treasury funding and proportional member
rewards but leaves allocation/rounding details unclear. Revora explicitly uses
one pool per match, funded during its five-minute admission window. A current
leader/deputy may pledge1M,5M or10M silver through the existing emissary, followed
by a confirmation bound to that match, guild and amount. Additional pledges are
allowed up to1B total. These are treasury deductions, not newly minted rewards
or player wagers; sponsorship betting remains excluded.

A unique funding receipt, pool increase and treasury deduction share one
transaction. Current officer and retained guild-lifetime ownership are checked
again under session/item/guild locks. Replaying a confirmation does not deduct
again. Once the match has a saved result, its pool uses the accepted kill, banner
and revival receipts for that guild. Leaving the match does not erase contribution
already earned. Each character receives the floor of their proportional share;
remaining silver goes by descending fractional remainder, then ascending UID for
exact ties. The shares sum exactly to the pool. Zero-point members get no share.

Cancelled matches and completed matches with no contribution refund the whole
pool to the original guild. A dissolved/reused guild cannot receive the refund:
the pool remains explicitly orphaned for operator reconciliation, retaining its
amount and funding receipts. There is no automatic reassignment or silent burn.

Allocation creates immutable per-character obligations. A bounded pulse delivers
to online players under session/item ownership with the existing durable silver
preimage, or to offline characters while holding session ownership. Purse credit
and paid receipt commit together. Runtime silver updates immediately; a subsequent
ordinary character save preserves it. A full purse leaves the obligation unpaid;
attempt ordering rotates such obligations rather than keeping them at the front.

Private executable SHA256:
`4A15DCDEC124A90A15A83BD42F97D4A72C1E9E85D8FBCA8835932F2C16058B14`.
Fresh match`...000037` and refund fixtures`...000038/39` pass earn and cold reload:

- [x] Ordinary member and at-start funding requests refuse.
- [x] Suppressed treasury deduction rolls back pool and funding receipt.
- [x] Actual leader NPC confirmation deducts1M once; repeated confirmation does
  not add another pledge or charge.
- [x] Actual combat earns200 banner points for the online fighter and10 revival
  points for a guildmate who disconnects before settlement.
- [x] Suppressed share insertion leaves the full pool funded and no obligations;
  the next pulse retries allocation successfully.
- [x] Automatic payouts credit952,381 online and47,619 offline, exactly1M total.
  The online cache, database, later character save and cold reload agree.
- [x] Two actual pledges accumulate6M. Both an empty cancelled match and an
  uncontested scoreless win refund6M once, with no member payouts, across restart.
- [x] Pure allocation checks cover unequal/equal weights, deterministic ties,
  pools smaller than recipient count, empty contribution, invalid bounds and
  large integer weights without overflow.

Retained older prize/claim snapshots remain unchanged. Their regression checks
now account for the explicitly saved pool debits/refunds and member credits;
they do not treat legitimate later transfers as a duplicate historic prize.
Daily Chi/EXP Ball reset handling from the prior batch remains separate.

Still pending: dedicated purse-cap, payment-write/post-commit projection fault,
refund-overflow and dissolved-guild scenarios; funding-total presentation after
an ambiguous disconnect; broader event/native acceptance and production activation.
Ordinary member distribution is implemented and privately exercised, but those
remaining gates keep the event staged and the roadmap at76/79. No live deployment,
public listener, client patch or server restart occurred.


## Payment fault acceptance and funding clarity (2026-10-03)

Payout persistence now rejects callers without the character's session ownership,
a matching transaction and, for online delivery, the canonical player/item lock.
The offline path refuses when that UID has an installed online session. A private
post-commit probe exercises failure before the ordinary runtime purse update.
Funding confirmation now shows the silver already pledged before asking to add
more, so reconnecting after an uncertain response does not hide the existing pool.

Private executable SHA256:
`8D859F7B79D0829EF8C5A0D67DD3996DEF5B3C96EB2A28CA5C5459466DF3CCFC`.
Earn and separate cold reload passed on match`...000040`:

- [x] A transaction-scoped maximum offline purse refuses delivery, preserving the
  unpaid obligation. Rolling back the capacity fixture restores the real purse.
- [x] Suppressing the paid-receipt UPDATE after the silver UPDATE rolls both back;
  both member obligations remain unpaid and the real purse is unchanged.
- [x] Throwing immediately after online payment commits leaves one paid receipt
  and the full durable credit. The stale online session is fenced and its save is
  refused; another payout pulse cannot credit it again.
- [x] The offline recipient still receives the correct share. Cold reload restores
  both purses; later ordinary saves and the prior claim/treasury checks pass.

The retained match resumed after correcting the harness's SessionInstall type
lookup. No earned contribution, payout or historical claim receipt was rewritten
to recover the test.

Actual NPC-funded refund matches`...000042/43` additionally cover isolated
transaction-scoped guard cases before normal settlement:

- [x] A maximum guild treasury leaves the pool funded and refuses an overflowing
  refund, with no member payout or claimed refund.
- [x] Invalidating the saved guild-lifetime eligibility holds the pool as orphaned
  with no refund to the current guild identity and no member payout.
- [x] Those injected states roll back; the real cancelled/scoreless matches then
  refund all6M once and preserve their state across cold restart.

The ownership test proves the invalidated-lifetime guard, not an end-to-end guild
disband/recreation flow. Still pending: that flow, interrupted downed-return and
additional resurrection/combat families, complete earned qualifier-to-final
integration, native presentation and normal startup activation. No live process
or client was changed; the staged event still does not raise76/79.

## Earned qualifier-to-final integration (2026-10-03)

The private earn and separate cold-reload processes passed using executable
`8D859F7B79D0829EF8C5A0D67DD3996DEF5B3C96EB2A28CA5C5459466DF3CCFC`.
Three new ordinary, non-GM characters founded guilds through the Guild Director.
Starting level/rebirth, purses, attack strength and the event clock are controlled
test prerequisites; qualification seeds, kill receipts, wins and contributions
are earned through the running event rather than inserted by the test.

- [x] Ordinary qualifier registration and six actual registered-monster kills
  produce seeds with scores3/2/1 and three qualifier prize claims.
- [x] The generated group match and final accept ordinary NPC entries, banner
  attacks and one1M silver pledge each. Both settle and remove their arena maps.
- [x] Final standings and two officer claims produce exact guild silver balances
  of159.5M,169.5M and40.5M, with CP funds2000,3000 and0 respectively.
- [x] Both earned member payouts credit exactly1M once.
- [x] All three participants claim and consume their bound stage607 EXP Ball;
  saved progression, experience, inventories and reward receipts survive restart.

Claim snapshots now record member credits already present at snapshot time so
later comparisons count only subsequent credits. Historical snapshots remain
intact. The retained fault/refund and placement checks also pass.

Remaining gates include actual disband/recreation, interrupted downed returns,
additional combat/revival acceptance and normal startup activation. Native client
presentation remains pending separately. No live deployment or client changes;
the staged event does not yet raise the76/79 roadmap count.

## Interrupted downed return acceptance (2026-10-03)

The same private executable passed another earn/cold-reload pair. A dedicated
scheduled match (`...000044` qualifier fixture) uses controlled qualification
seeds, ordinary NPC admission and an actual lethal player attack. Before the
five-second camp recovery, both characters are saved and removed from the
runtime player pool, leaving their real transfer receipts outstanding. This
models interrupted sessions; it is not an operating-system crash test.

- [x] The defeated character is durably saved at zero HP with one defeat receipt.
- [x] Cold startup returns both characters to their saved Twin City coordinates,
  marks both transfers returned and entries inactive, and cancels the match.
- [x] The defeated character receives1HP; the living character's exact positive
  HP is preserved. Assertions run before the harness reloads either character.
- [x] Replaying offline return preserves health and location. Recovery produces
  neither a revival receipt nor a match result.
- [x] Existing qualifier/final, treasury, pool and stage607 claim regression
  checks still pass in the cold process.

Actual disband/recreation and additional combat/revival acceptance remain before
normal startup activation; native presentation remains a separate pending gate.
No production process, client or reward policy changed.

## Actual guild disband and same-name refounding (2026-10-03)

Private earn and cold reload passed using the unchanged tested executable
`8D859F7B79D0829EF8C5A0D67DD3996DEF5B3C96EB2A28CA5C5459466DF3CCFC`.
A new ordinary character founds LifeGuild through the Guild Director, donates1M
silver through packet1107 and pledges1M through the Guild PK Emissary. The
qualification seeds are controlled prerequisites for this ownership test.

- [x] Actual Guild Director dissolution removes the guild row and membership,
  invalidating its saved qualifier ownership through the database trigger.
- [x] The same character founds another LifeGuild through the ordinary NPC.
  It receives a new ID and its own500,000 silver starting treasury.
- [x] The retained old qualifier offer cannot pay the replacement guild, and
  the replacement cannot enter the old guild's scheduled match.
- [x] The empty match cancels; the old1M pledge becomes orphaned with zero refund
  and no member payouts. The new treasury remains500,000 silver and0CPs.
- [x] Cold reload retains the new membership, invalidated old ownership, one
  funding receipt, no qualifier prize and the same orphaned pledge.

This proves real disband followed by same-name recreation with a new guild ID.
Direct database ID reuse remains a distinct administrative mutation, not an
ordinary founding route. Prior transaction-scoped lifetime guards remain covered.
Normal startup activation and remaining combat acceptance are still pending.

## XP Revive and Aurora Lotus integration (2026-10-03)

XP Revive1050 passed on fixture`...000046` with the prior private executable.
The catalog requires XP readiness; the earlier failed attempt lacked it.
The actual cast now verifies refusal without XP, opposing-guild refusal,
write-failure rollback, successful revival, one10-point receipt and replay.
Refused attempts preserve XP readiness; successful casting consumes it. Skills
and XP readiness are supplied combat prerequisites, not a training/XP earning test.

The active Aurora Lotus12370 floor-spell callback previously called
`BringToLifeLotus` without tournament eligibility or contribution persistence.
It now routes managed targets through the same session, item and battle ownership
checks. Downed guildmates require a committed revival receipt before resurrection;
living support targets require eligible same-guild participation. Both older
item-based Lotus paths share the guard. Outside Guild PK their existing behavior
is preserved. Dynamic-map status now uses the existing base-map fallback.

Final private build and earn/cold-reload acceptance passed with SHA256
`8F8FE430380E333ABF7AC4514DE0D2229C7C9EA0E4820624ABF225A82AE1BE50`
on fixture`...000049`:

- [x] An actual12370 cast consumes200 supplied energy and creates a floor spell;
  invoking its real timer callback removes the expired floor effect.
- [x] An opposing caster cannot revive the downed participant or heal an injured
  living opponent in range.
- [x] Suppressing the revival receipt leaves the target downed with no credit.
- [x] A wrong floor-map identity is refused. A valid same-guild cast restores
  full HP and records exactly one10-point revival contribution.
- [x] A repeated floor cast does not duplicate revival credit. Twenty-defeat,
  member payout, treasury, earned finals, disband and cold-claim checks pass.

The first Lotus fixture lacked the required epic weapon. It remains preserved;
the successful fixture supplies a temporary weapon view plus learned skill and
energy, restoring that view afterward. It proves casting/callback behavior, not
the already separate epic-weapon earning path. Earlier successful fixtures are
also retained without rewriting their receipts.

Code inspection identifies10405 as Soul Shackle and10425 as its removal/support
path, not additional resurrection spells. They do not need fictitious revival
credit integration. Broader combat edges, timeout/draw concurrency acceptance
and normal startup activation remain; native presentation is separately pending.
No live deployment or client patch was performed.

## Four simultaneous arenas and deadline outcomes (2026-10-03)

The private earn and cold-reload processes passed with the same executable
`8F8FE430380E333ABF7AC4514DE0D2229C7C9EA0E4820624ABF225A82AE1BE50`.
Fixture`...000050` creates eight ordinary characters and actual NPC-founded
guilds. Qualification seeds are supplied for this match-lifecycle test; the
separate earned qualification-to-final fixture remains intact.

- [x] Normal scheduling opens four distinct first-round arenas simultaneously.
  All eight ordinary players enter through the Guild PK Emissary, two per map.
- [x] Cross-arena player damage is rejected.
- [x] Real banner attacks produce deadline scores0–0,10–10,20–0 and15–5:
  two draws and two wins, each persisted with the `time` reason.
- [x] All eight participants return to town and all four dynamic maps are removed.
- [x] An intentionally unregistered arrival is returned to327,303 without a
  transfer or participation reward. The eight eligible entrants retain receipts.
- [x] Exact scores, winners, returned transfers and participation persist across
  the separate cold process. Retained reward and ownership regressions pass.

The initial assertion selected a historical receipt by reused dynamic-map ID.
The harness now scopes map receipts to their contest before checking results;
the actual saved scores needed no change. No match/result receipt was rewritten.
This exercises four live map lifecycles in one coordinator tick, not simultaneous
network-load stress. Remaining combat-edge review and normal startup activation
remain before the stage607 completion checkmark. Native/live gates stay separate.

## Match-owned Toxic Fog (2026-10-03)

Guild PK poison previously fell through to the legacy periodic-damage branch,
which lacked its caster and arena ownership. The shared poison entry/tick now
retains the originating player session and map instance, acquires Guild PK combat
and health ownership, and validates them before applying damage. A busy or
protected tick cannot damage the target. Effects expire within40 seconds and
are cleared when the caster disconnects, dies, leaves, or loses canonical
ownership, or when the target leaves the original arena. Foreign poison is not
allowed to fall through inside a Guild PK arena.

Damage is capped at current HP minus1, Azure Shield absorption updates both shield
and HP correctly, and poison cannot create a lethal-defeat receipt. This change
uses the existing10010 application path and existing timer dispatch; it adds no
new packet layout and does not change the separate Team Arena branch.

Private build SHA256:
`2D4140AC1766158F08FB177AF82F4B08C92B0229326637E4A00E610C8209A85C`.
Earn/cold reload passed on fixture`...000051`:

- [x] Ally application and application against a banner-sheltered opponent refuse.
- [x] The real shared Apply/Tick boundary waits two seconds and processes a given
  interval once: a controlled50% effect changes1000HP to500HP.
- [x] A500-point hit reduces a1000-point Azure Shield to500 without HP loss.
- [x] A controlled overlarge percentage stops at1HP, with no defeat receipt.
- [x] Removing the caster's canonical session clears the target's poison without
  damage. A fresh effect also clears at expiry.
- [x] Existing actual Lotus casts, reward/claim persistence, four-arena results
  and guild ownership checks still pass across restart.

The poison test controls its percentage and invokes the production application
and timer boundary directly; it does not claim coverage of the probabilistic
spell-hit selection. Reflection/rune review and normal startup activation remain.
No live process or client changed.

## Reflection ownership and reflected defeat (2026-10-03)

`Calculate.AutoRespone` mutates health directly during damage calculation, before
the normal ReceiveAttack boundary. It now owns the Guild PK session/item/battle
lease for those effects and refuses invalid or protected pairs before mutation.
The existing reflection rates are unchanged; a normally-null internal chance
delegate permits deterministic private acceptance.

Private build SHA256:
`E7C560E2AC605D4ECB9629E80351942307CB40BB9FC762591B09436B3956FB5E`.
Earn and cold reload passed on the four-arena fixture`...000052`:

- [x] A player in another arena cannot trigger reflected damage.
- [x] The actual3060 response branch reflects100 damage from a1000-damage input,
  reducing500HP to400HP while the player remains alive.
- [x] At1HP, reflection records one defeat credited to the reflecting opponent
  and leaves the victim downed in its guild camp.
- [x] Repeating the response while downed creates no second defeat. At the
  five-second boundary the normal coordinator restores full health and records
  one automatic revival with no player revival credit.
- [x] All four deadline outcomes and cleanup still pass, and the defeat/revival
  receipts survive the cold process alongside prior reward regressions.

The test supplies3060 and forces the random response decision; it exercises the
production response branch rather than claiming random-rate or training coverage.
The eight reused participants keep their original permanent participation
receipts; the harness checks those by character instead of demanding new receipts
for each later tournament. No existing receipt was changed.

Rune review, particularly delayed Fine Rain healing, and normal startup activation
remain. No production process or client changed.


## Rune healing and normal startup completion (2026-10-03)

Rune dispatch now requires a current eligible Guild PK participant before its
self effects. Support leases allow one canonical living participant to target
itself while retaining ordinary damage/revival restrictions. Fine Rain validates
living, same-guild tournament targets under combat/health ownership; its delayed
updates require the original canonical session, map instance and death timestamp.
It cannot heal after death, departure or session replacement. Redundant inner
PrepareSpell calls are removed, so each cast pays once. Progress values no longer
underflow and the completion flag is placed in the packet actually sent.

The rune acceptance fixture`...000053` passed with the production dispatch and
controlled spell costs:7 mana and3 stamina are each charged once; a living ally
heals, an enemy does not, and neither Fine Rain nor a direct Absolution attempt
bypasses the downed state. The real delayed-update boundary accepts the original
context and refuses changed map/death/session contexts. That test supplies spell
parameters; it does not claim rune acquisition or ten-second timing measurement.

Normal Server.Run now calls Prepare before timer workers and Activate after
WayofHeroes.Load. Activation ensures map700 is loaded and checks monster
profiles before exposing the NPCs. The harness no longer prepares this event
before Server.Run and asserts it is already prepared, enabled and has both NPCs.

Final private executable SHA256:
`F9508508F9942B5E472B32DCEF4D63E9BE55AB91481CCA0EF3FA6D5CDBB725DE`.
Both earn and separate cold reload passed. Fresh characters4294917701?7703
founded PathLive guilds, killed qualifier monsters, earned seeds3/2/1, fought the
generated group match/final, collected exact treasury/member rewards and used
three bound stage607 EXP Balls. The controlled event clock provides the required
membership age; saved admission dates were not backdated. Prior fixtures remain
intact in their original probe tables. Retained four-arena, reflection, disband,
refund, payout and claim persistence checks also passed.

- [x] Normal startup activation and fresh end-to-end ordinary earning.
- [x] Stage607 single claim, consumption and exact cold persistence.
- [x] Server-side milestone count updated to77/79;104 and409 remain.
- [ ] Native DX9 rendering/interaction acceptance.
- [ ] Production deployment and post-deployment acceptance.

These results do not claim every possible combat/load combination, every legacy
guild-management crash path or production operation. They establish the ordinary
server earning path and the identified ownership/reward recovery invariants.
No live restart, public binding, client patch or production deployment occurred.
