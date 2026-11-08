# Team Tournament (Quarantine) earning and recovery

## Active route audit and hit eligibility (2026-09-27)

This is an active legacy event outside the previous52-family reward inventory.
`Thread.cs` schedules `Sistemas.Quarantine.Start/End` at20:00/20:30 on Monday,
Tuesday, Thursday, Friday and Sunday. `Game/Attacking/Handle.cs` dispatches player
hits in map10360 to its handler. This is distinct from the unused empty
`Game/Features/Event/Quarantine.cs` file.

The old handler checked the attacker map twice, dereferenced garments before
checking null, swallowed equipment failures, and allowed invalid paths to fall
through into personal hit/death counters. It did not enforce its declared spell
allowlist or require both canonical opposing-team registrations. The combat
caller still assigned one damage to rejected event attacks.

`EligibleHit` now requires the active event, distinct living player entities,
both arena locations, canonical owner/entity references, exactly one team per
participant, opposing teams, reference-matched registered sessions, the exact
181325/181525 team garments, and the declared skill allowlist. Invalid attacks
return false before score/counter/teleport writes; the caller assigns zero damage.
Accepted hits retain the existing scoring and100-point end condition (now>=100).
This is a combat eligibility fix, not a complete event restoration.

Validation: isolated legacy source build passed; compiled-assembly
`tests/Quarantine/HitEligibilityTests.cs` passes39 checks. Both team directions
and all five allowed skills pass eligibility; nulls, closed event, forbidden
spell, self/outside-map hits, missing/wrong garments, unregistered/same/dual teams
and a stale registered session are rejected. Rejected handler calls preserve
team and personal counters. Valid-hit teleport/world combat, race ownership and
payout are not covered by this focused fixture. Logs:
`obj/quarantine-hit-build.log`, `obj/quarantine-hit-tests.log` (ignored).
No private gameplay refresh, live deployment or native acceptance is claimed.

## Remaining coherent event work

- Canonical, concurrency-safe lifecycle and admission/return ownership, including
  previous PK mode and safe restoration of the player's original garment.
- Durable score/result snapshots and eligible winner entitlements. Current
  `GiveReward` performs separate writes for300M CPs,100B gold and50K RVPs;
  interrupted/offline delivery and duplicate payout protection remain unverified.
- Add a frozen valuable event reward with atomic payment/item delivery, capacity
  handling and reconnect/restart recovery; preserve existing completed receipts.
- Ordinary combat-to-result-to-claim validation, installed arena terrain and
  safe return checks, then Host/native acceptance and deployment.

These unchecked requirements keep this event outside completed reward-family
coverage. Existing enormous legacy currency amounts are recorded evidence,
not new approved economy settings or a claim of balance.


## Preserve equipment on refused entry (2026-09-27)

Legacy Join ignored `Equipment.Remove(9)` failure, cleared the occupied garment
reference and installed its team item after already changing PK mode, roster and
map. A full inventory therefore made entry capable of replacing the player's
original equipped reference without moving it safely to the bag.

Join now owns the character item lock, rejects duplicate roster membership
before side effects, and requires successful removal of an occupied garment
before PK mode, roster or teleport changes. It no longer unconditionally clears
the slot after an ignored removal result. An absent/null owner or equipment/
inventory context is rejected. Successful admission still uses the legacy team
uniform; this is not yet durable garment/PK-mode restoration or atomic admission.

The isolated build passes and the compiled-assembly fixture now passes44 checks
(39 previous combat checks plus five admission checks). A full40-item bag retains
the exact garment reference, item count, town location, PK mode and absent team
membership; a repeated join preserves the existing team and uniform. The first
fixture attempt omitted Inventory's cached Objects array and failed its own
full-bag prerequisite; populating both backing representations corrected that
fixture. No game rebuild was repeated for that fixture-only correction.
Logs: `obj/quarantine-entry-build.log`, `obj/quarantine-entry-tests.log`.
No runtime refresh, live deployment or native acceptance.

Remaining: saved admission/return records, safe owned uniform identities, PK mode
and original garment restoration, lifecycle concurrency and durable atomic
winner purses with a valuable reward. Full successful admission, reconnect and
combat-to-payout remain separate unverified paths.


## Shared runtime lifecycle ownership (2026-09-27)

Start, End, Join, score/teleport handling and roster cleanup now share one
nonblocking Interlocked owner. A busy operation returns without mutation or
combat damage; all acquired ownership is released in finally. The100-point
winning hit invokes the already-owned end path, avoiding reentry into the guard.
Repeated starts retain the current roster/scores, and repeated/concurrent ends
cannot simultaneously execute the legacy payout body. The public started flag
is volatile for scheduler/dialog readers. Entry still owns the character item
lock after acquiring lifecycle ownership.

The isolated source build and51 compiled-assembly checks pass (44 earlier checks
plus seven lifecycle checks). Tests cover a held owner rejecting operations,
32 concurrent starts, repeated concurrent starts preserving roster identity and
score,32 concurrent ends, and a later valid start/end. Runtime lifecycle tests
use an empty online-recipient array; they do not prove reward transactions or
successful live combat/return. Logs: `obj/quarantine-lifecycle-build.log` and
`obj/quarantine-lifecycle-tests.log`.

This closes runtime overlap, not process persistence. A failed legacy payout
still lacks durable recovery; exactly-once currency/item delivery, offline
claims, admission/uniform restoration and full combat-to-prize tests remain the
next implementation requirements. No runtime refresh or live deployment.


## Durable round lifecycle and terminal snapshot (2026-09-27)

Server startup now initializes `revora_quarantine_rounds` and cancels unfinished
previous-process rounds without selecting a winner. Same-process initialization
is idempotent. Start stores its UUID/time before publishing the active round;
retry uses that same identity and verifies the saved preimage.

End freezes scores, end time and the sorted eligible winner identities before
changing the runtime active flag or delivering the existing purse. Finalization
is transactional and verifies exact saved values on retries, including a prior
committed write whose runtime installation has not occurred. While a terminal
snapshot is pending, new hits and joins are refused. Winner identities must be
canonical ordinary active sessions in the event map at snapshot time; delivery
cannot expand that list later. Terminal records survive startup recovery.

Validation: isolated source build,51 existing runtime checks and10 actual
PostgreSQL ledger checks pass. The database fixture uses a separate owned schema
in the private candidate and drops only that schema afterward. A trigger rejects
terminal updates to verify retryable active state and released lifecycle
ownership. Repeated SaveResult models a committed-write/runtime-installation
gap; it is not injected TCP ambiguity. A simulated new-component initialization
cancels the leftover round and preserves prior results; actual Host restart is
still pending. Logs: `obj/quarantine-ledger-build.log` and
`obj/quarantine-ledger-tests.log`. The runner uses the standard application
binding redirects; an initial missing test config failed before schema creation.

Reproduction: `tests/Quarantine/Verify-Ledger.ps1` against the matching isolated
build, with the already-running owned private database connection supplied only
through `REVORA_QUARANTINE_TEST_CONNECTION`. The fixture rejects every endpoint
except127.0.0.1:25439/revora_candidate_game and refuses an existing fixture schema.
The tested private database is stopped afterward. No candidate binary refresh,
live deployment or native acceptance.

**Still incomplete:** durable participation/return and per-hit recovery, frozen
valuable reward terms and atomic offline/retry-safe purse delivery. GiveReward
still has its historical multi-write currency path. If that path fails after
terminal commit, this batch preserves evidence but does not deliver the missing
purse automatically. It must not be described as a completed recovery feature.


## Atomic frozen winner purses (2026-09-27)

New terminal results now freeze one purse per snapshotted eligible winner in the
same transaction as the result. Each retains the existing300M CPs,50K RVPs and
100B silver, plus one randomly selected catalog P7 soul. Soul type is chosen
before the transaction and retained across its retry. Delivery creates the soul
bound and permanent. Historical closed results are sealed without retroactive
purses; this does not repeat earlier legacy payments.

The old separate currency writes are replaced by one transaction holding the
character and pending-receipt rows. It checks saved silver/CP/RVP and inventory
preimages, balance capacity, one free bag slot, catalog phase and canonical
healthy session ownership. Currencies, item and paid receipt commit together;
postcommit updates install exact currency values and the same item identity.
A failure fences the session for reload; it does not guess whether to pay again.
Repeated/concurrent claims cannot consume an already-paid receipt.

The normal reward poll visits at most32 due online owners per10-second pass,
using a cursor. Offline or full-bag entitlements stay pending and deliver after
reconnect/capacity becomes available. This covers players already included in
the terminal snapshot; disconnected participants before finalization still need
durable admission/combat evidence and are not newly declared eligible here.

Isolated build,51 runtime checks and24 ledger/purse checks pass. The added
fixtures verify frozen terms, foreign-owner refusal, full-bag deferral, triggered
item-insert rollback of all currency/receipt changes, concurrent single claim,
exact CP/RVP/silver cache and database values, bound soul/item identity, replay,
paid/pending component recovery and production Tick delivery after reconnect.
Result rosters and the one-row P7 catalog are controlled fixture inputs; the
fixture is not a full ordinary combat-to-prize route or production character
loader test. Binding/identity are checked against actual saved item rows.
Logs: `obj/quarantine-purse-build.log`, `obj/quarantine-ledger-tests.log`.

Remaining: durable admission/return/per-hit state, safe uniform identities and
original garment/PK-mode restoration, complete ordinary event earning and loader
recovery, actual Host migration/crash and native acceptance. The legacy cleanup
still needs review for full bags or changed equipment. No runtime refresh or
live deployment. This batch does not increase completed-family coverage.


## Owned transient uniform cleanup (2026-09-27)

New uniforms use `ConquerItem.ItemUID.Next`, replacing the small random UID range.
The current round tracks the exact issued object and session owner. End removes
only that object from the equipped garment slot or the same owner's bag, under
the character item lock. It never clears an unrelated replacement garment and
never tries to add the temporary uniform to a full bag. The existing committed
cosmetic projection helper clears equipped state; client removal and recalculated
stats follow. No unrelated durable item row is deleted by this transient cleanup.

The isolated build and56 runtime plus24 database/purse checks pass. Five new
checks cover a full bag, replay, replacement garment preservation, stale-session
refusal and removal of the owned unequipped uniform while retaining the current
garment. Logs: `obj/quarantine-uniform-build.log` and
`obj/quarantine-ledger-tests.log`.

These are current-process object leases, not durable admission records. Leaving
mid-round, disconnected ownership, restart recovery, automatic original-garment
reequip and previous PK-mode restoration remain open. Their absence still keeps
the full event restoration incomplete. No runtime refresh or live deployment.


## Saved admission and retryable return (2026-09-27)

Entry now reserves the round, owner, team, previous PK mode, original garment
identity and unique bound uniform before changing equipment, team or location.
Only one unreturned reservation per character is allowed. Terminal results end
admissions in the result transaction; startup marks cancelled admissions due
for return. A bounded online poll retries pending returns, including interrupted
entry and stale sessions after reconnect.

Return saves Twin City 1002 (327,303), deletes only the recorded owner's uniform
with its expected type, projects that removal, and restores the recorded PK mode
before acknowledging completion. Unrelated replacement garments are preserved.
A failed acknowledgement retains the pending record for retry; an acknowledged
replay cannot overwrite a later PK choice. Currency balances are not rewritten
by the return location save. Original garments remain preserved in the bag;
automatic re-equipping is still pending.

The isolated build and56 runtime plus38 database/recovery checks pass. Added
checks cover reservation ownership, duplicate refusal, entry acknowledgement,
startup cancellation, return acknowledgement failure, saved town coordinates,
owned-uniform cleanup, replacement garment preservation, PK restoration,
fresh-session retry and admission to a later round. Expected database trigger
failures exercise rollback/recovery. Logs: `obj/quarantine-return-build.log`
and `obj/quarantine-ledger-tests.log`.

Return fixtures begin with the cached character already at the destination;
they do not validate actual Teleport, equipped-uniform stat recalculation,
production character loading or full ordinary combat-to-prize travel. Per-hit
persistence, original-garment reequip, actual Host migration/restart and native
acceptance remain open. No private game refresh or live deployment occurred;
completed reward-family coverage is unchanged.


## Original garment restoration (2026-09-27)

Pending return now attempts to re-equip the recorded original garment when the
slot is empty and that exact item is still in the owner's bag. A replacement
already equipped is retained. The character row is locked, saved item ownership,
type, warehouse and position are verified, and other saved garments in the slot
prevent the move. The saved move commits before inventory/equipment projection;
projection failures retain the pending return and fence the session for reload.
An already committed position can be retried without creating another item.
Return also holds the existing per-character session-install lease throughout.

Isolated build and56 runtime plus44 database/recovery checks pass. Six new
checks cover occupied-slot preservation, foreign database ownership refusal,
triggered move rollback, successful retry, identical saved/cache item identity
and harmless replay. Tests invoke the real restoration method, but do not yet
exercise the complete return's equipment-stat refresh or actual world travel.
Logs: `obj/quarantine-reequip-build.log`, `obj/quarantine-ledger-tests.log`.
No live deployment or private game refresh. Full event earning, durable hit
state, actual Host recovery and native display acceptance remain pending.


## Actual private process startup recovery (2026-09-27)

`tools/Verify-MoonBoxDatabase.ps1 -CheckRoadmapStartup -CheckQuarantineRecovery`
now starts the real private game and database across two database cycles (three
game starts, including the seeded recovery). All game exits were normal, with
loopback listeners and no emergency kill. Startup created the Team Tournament
tables using the runtime role and retained the existing28-table roadmap checks.

A synthetic active round with20/10 scores and one entered admission becomes
cancelled, with no winner or purse invented. Its exact uniform identity/team/PK
mode remain on an ended, unreturned admission. A separate synthetic closed
100/90 round retains its winner snapshot and unpaid300M CP/50K RVP/100B silver/
P7-type receipt unchanged through both cycles. No character, balance or item was
created for these probes. The two owned probe rounds and dependent rows were
removed after successful verification; private database and game are stopped.

Only the private candidate was refreshed to gameplay commit `48c55d50`, SHA256
`32DA7B6DE3AACABAD2402A8D3043B08245368566064A20BC351E6320118CEBE6`.
The prior executable and manifest were preserved. The build uses the documented
committed-source overlay excluding unrelated worktree edits; it is not a clean
release build. Log: `obj/quarantine-host-recovery.log`.

This closes actual startup/migration and interrupted-round cancellation evidence
for these records. It does not prove an in-flight process crash, ordinary-player
combat/admission/return travel, per-hit persistence or native display. Live
migration/deployment remain gated. No completed earning-family count increase.


## Durable accepted hit records (2026-09-27)

Each accepted hit now commits a sequenced round record (attacker, target, spell,
team and timestamp) with the updated team aggregate before runtime score or
personal counters change. Both canonical players must have active, entered,
unreturned admissions with the exact issued uniform identities and opposing
teams. The round row is locked and its saved score must match the runtime
preimage. A failed transaction produces no gameplay hit. Database exceptions
freeze further scoring and new admission for the round; finalization reads its
saved aggregate before freezing the result. An ambiguous hit is never retried,
so a committed record cannot be counted twice. Restart still cancels unfinished
rounds rather than manufacturing a winner from partial scores.

Isolated build and56 runtime plus52 database/recovery checks pass. Eight added
checks cover unissued uniforms, both teams' saved hits, atomic hit/aggregate
updates, triggered aggregate failure rolling back the inserted hit, unchanged
personal counters, paused subsequent scoring and finalization from saved scores
instead of a deliberately changed cache. Valid-hit tests call the persistence
boundary with controlled admissions; the failure case uses HandleQuarantine.
They do not prove the ordinary combat packet/teleport/result-to-purse route or
a real connection-loss-at-commit scenario. Logs: `obj/quarantine-hit-build.log`
and `obj/quarantine-ledger-tests.log`.

This new hit-table migration has component validation only; the earlier actual
Host startup evidence covers the previous candidate build. No private refresh
or live deployment for this batch. Full ordinary earning, actual new-schema
startup, in-flight crash and native acceptance remain open.


## Deadline retry and hit-table process recovery (2026-09-27)

The regular reward poll now checks the tournament lifecycle under its existing
nonblocking owner. At30 minutes from its UTC start, on100 hits, on a scoring
fault or on a pending result, it retries settlement at most once per10 seconds.
This avoids depending exclusively on the legacy20:30 short scheduler window.
Failed database settlement remains pending and does not discard the round.

Isolated build,56 runtime and56 database checks pass. Four added checks exercise
an unfinished29-minute round, failed31-minute settlement beyond the scheduler
window, bounded retry and successful finalization after database recovery.
This is controlled-clock component evidence, not a30-minute real-time match.
Logs: `obj/quarantine-deadline-build.log`, `obj/quarantine-ledger-tests.log`.

The private candidate was then refreshed with this source batch. Actual game
startup migrated the hit table and retained a synthetic accepted-hit identity
through two database cycles, alongside the previous pending-return and unpaid-
purse assertions. Three normal game exits, no emergency kill, owned synthetic
rows removed and private database stopped. Command remains
`Verify-MoonBoxDatabase.ps1 -CheckRoadmapStartup -CheckQuarantineRecovery`;
log: `obj/quarantine-hit-host-recovery.log`.
Candidate SHA256:
`158A50176A0ECD01FDFECE7AF2A2EF0B09F339D75EA4F24BDF6966C177AA2DA6`.
This is an isolated overlay build, not a clean release. Synthetic startup records
do not establish ordinary combat-to-prize earning or real in-flight crash safety.
Native/live gates remain open; no live deployment.


## Restored public tournament clerk (2026-09-27)

Ordinary access was still blocked by the retired-desk list. Actual private
startup identified the loaded legacy clerk as900183, `PK Black&White`, map6000
(54,82), mesh6056, Talker. The new placement accepts only that exact retired
identity, removes it from the old map/secondary index, and creates TeamMarshal
at Twin City (273,140), mesh50340. Other identity collisions fail rather than
replace unrelated NPCs. The floor and nearby NPC occupancy are checked before
removal. Repeated placement recognizes the exact restored profile.

The restored ID is exempt from plaza parking and is no longer closed. A new
ordinary dialog handler runs before retired-desk dispatch, uses concise world
text and only accepts nearby canonical living players outside trade/booth.
Joining continues through the saved admission path. No GM/account allowlist.
The old invitation already points to this location.

Isolated build,59 runtime and56 database checks pass. Three additional runtime
checks verify the desk is open, plaza parking skips it and an ineligible remote
request cannot join. The hit fixture now starts one second before finalization
to avoid a cross-clock precision race; no production timing rule was changed.
Two actual private game/database cycles (three game starts) pass with normal
exits and the clerk-ready marker at the checked location. Initial discovery
runs correctly rejected the legacy identity; only the exact observed profile
was added for restoration. Log: `obj/quarantine-clerk-host.log`.
Private candidate SHA256:
`B10153EC31EF8D10C7F50D86E87D9E4614834A256BADA2A59F520292ED8789CA`.
The NPC dispatch hunk is staged separately from unrelated owner NPC changes;
the isolated build overlay contains that same dispatch. No live deployment.

Loaded world placement and handler checks do not yet prove ordinary socket
join/combat/return/reward or native visibility. Those gates remain open.


## Ordinary in-process world route (2026-09-27)

`tests/Quarantine/WorldTests.cs` now exercises the combined production route on
loaded Twin City and10360 terrain with the real item catalog. Two canonical,
non-GM test characters approach TeamMarshal and enter through `Npcs.GetDialog`.
The event assigns opposite teams, moves both into the arena and equips uniforms.
One participant starts with a saved original garment. One hundred accepted
`HandleQuarantine` callbacks produce the winning result, automatic purse delivery
and real `Entity.Teleport` returns. Both PK modes restore; the original garment
re-equips with its saved UID/position, and both temporary uniforms disappear.

Assertions verify100 saved hit records, two completed admission returns, exactly
one paid winner receipt, the same permanent bound catalog P7 UID in cache/items,
300M CPs/50K RVPs/100B silver in cache and database, no loser currency reward,
and no duplicate purse on completion/claim replay. Clerk placement is repeated
on real terrain and remains singular. Full stat refresh executes on entry and
return. The isolated suite now passes59 runtime and67 database/world checks;
log: `obj/quarantine-ledger-tests.log`. The verifier compiles the new fixture.

This is an offline, in-process integration route: initial characters/equipment
are supplied, the clock opens the round directly via Start, sockets are inactive,
and accepted combat callbacks are invoked directly without attack packet/range
validation. Terrain/catalog inputs come read-only from the private candidate;
all fixture database writes remain in the owned schema and are removed after
testing. It is stronger than controlled winner snapshots but is not ordinary
Account/Game TCP, production reconnect loading, natural scheduled admission,
in-flight process crash or native rendering acceptance. Those remain open.
No new gameplay build, candidate refresh or live deployment for this test batch.


## Earned reward reload and full-bag second round (2026-09-27)

The combined world fixture now saves the first winner's actual profile and
reloads a fresh canonical non-GM character through production `LoadEntityState`
and `ConquerItemTable.LoadItems`. All three earned balances and the exact bound,
permanent P7 UID survive. Repeated claim/completion still pays nothing extra.
This excludes the wrapper's external website grant providers; it is character
and inventory loader evidence, not a full Account/Game TCP reconnect.

The reloaded character then enters another round with40 bag items and no original
garment, wins another100 accepted hit callbacks, and returns safely. The entire
second purse stays pending: no partial currency payment or dropped soul. After
one fixture filler item is removed from both saved inventory and cache, claim
and replay deliver exactly one second purse and a second bound P7, bringing the
cached totals to600M CPs,100K RVPs and200B silver. Bag occupancy returns to40.
Entry armor is now saved as well as cached so the loader reproduces usable gear.

The focused suite passes59 runtime plus74 database/world checks. Test-only
screen/character initialization follows the existing offline harness; no gameplay
implementation changed. Log: `obj/quarantine-ledger-tests.log`.
Still open: actual socket combat/admission, natural scheduled access, real
in-flight process failures and native display. No build refresh or deployment.


## Durable scheduled-day identity (2026-09-27)

The production scheduler now calls `StartScheduled(Now64)` rather than the
unrestricted internal/manual Start path. It preserves the existing server-local
Sunday/Monday/Tuesday/Thursday/Friday20:00:00-55 window. A namespaced invariant
local date hashes to a stable128-bit round identity. Any saved row for that date
prevents reopening after normal completion or startup cancellation. Pending
same-process start writes reuse their identity/time; unrelated pending starts
are not overwritten. The existing primary key arbitrates concurrent inserts.
Manual/test Start retains its separate behavior.

Isolated build,59 runtime and80 database/world checks pass. Six additional
checks cover wrong-hour rejection, eligible start, repeated ticks after an early
completion, the next eligible date, component startup cancellation followed by
a repeated scheduled tick, and Wednesday/Saturday exclusion. These use supplied
calendar values, not real waiting or a new process launched in the20:00 window.
Logs: `obj/quarantine-schedule-build.log`, `obj/quarantine-ledger-tests.log`.
Only the scheduler call hunk is staged in the owner's otherwise modified Thread
file, and the validation overlay contains that same hunk. No private refresh or
live deployment. Full socket and native gates remain open.


## Ordinary TCP clerk access and safe invitation landing (2026-09-27)

The existing ordinary MoonBoxTester/RoadmapProbe career now reaches TeamMarshal
through real Account/Game TCP in two actual game/database restart cycles. The
probe follows hash-verified Twin City terrain, receives the closed-event dialog,
sends the unavailable join option directly and verifies it stays in town with
identical inventory/currencies. It walks back to1002 (327,303). Existing career
receipts, permanent Honored Hero selection and expired Homecoming refusal also
remain verified. Both game exits were normal; private game/database stopped.

The first approach exposed the clerk's normal3x3 blocked footprint. The corrected
approach is (275,140), and the event invitation now lands players there rather
than on the NPC at (273,140). The world fixture checks this landing against the
loaded floor after NPC placement. All19 configured arena entry/hit-relocation
points were also open in the installed10360 terrain; no coordinates there changed.

Run the existing full completed-career command with
`-CheckHomecomingReturn -CheckHomecomingExpired -CheckTournamentAccess` added.
The new option requires `-CheckSecondRebirth` and its existing prerequisite flags.
`obj/quarantine-clerk-wire.log` contains two successful ordinary TCP clerk checks;
`obj/quarantine-wire-landing-build.log` and `obj/quarantine-ledger-tests.log`
record the isolated build and59 runtime/80 database-world checks.
Private candidate SHA256:
`5717E4C49DB525914728C426A00AF9B9F2AC0977F975C6FC51B3014CE8941CAE`.

This verifies the closed-calendar route, not an open-round TCP admission or
actual attack packets. The100-hit earned route remains in-process evidence.
In-flight process crashes and native rendering remain open. Only the private
candidate was refreshed; no live deployment.


## Refused or redirected arena admission (2026-09-27)

The general teleport method can refuse travel or fall back to town when the
requested arena is unavailable. Admission now requires the exact selected arena
spawn before issuing a uniform or acknowledging entry. Failed travel removes
both transient roster entries and invokes the existing durable return path to
restore the original garment and PK mode. A failed return remains retryable by
the existing admission poll; arrival failure cannot manufacture a winner.

The one-admission-per-character-per-round contract is preserved. Repeating a
cancelled admission now returns before insertion instead of violating its
primary key; a completed return from a different round does not block entry.

The real-terrain world fixture removes the arena and its map metadata, invokes
the ordinary NPC join route, then restores the fixture map. Five checks verify
town return/no roster, original garment and PK mode, a returned but never-entered
receipt with no uniform, safe repeated admission and no winner purse. The
existing earned and full-bag routes still pass. Isolated build and **59 runtime
plus85 database/world checks** pass (`obj/quarantine-admission-build.log`,
`obj/quarantine-ledger-tests.log`). The build retains the documented committed
source overlay for unrelated owner work. No private game refresh, live deployment,
open-round TCP or native acceptance is claimed by this batch.

## Late-round process interruption and earned payout recovery (2026-10-01)

Two existing ordinary event characters (4294908401/4294908402) buy eligible
Trojan armor from the installed silver shop and equip it through ItemHandler.
The scheduled-opening entry point is supplied an eligible local calendar date;
TeamMarshal's ordinary NPC dispatch admits opposing teams on loaded map10360.
The first proposed armor identity was absent from the installed shop, so the
harness selects an eligible catalog entry instead. That failed lookup happened
before purchases or admission.

The first round records99 accepted Fast Blade event-hit callbacks and no prize.
Both characters are saved in the arena, then the game exits abruptly without
normal shutdown. On a full game/database restart, startup cancels that unfinished
round while preserving its99-hit ledger. Production admission return restores
both to Twin City/Capture mode, removes the recorded team uniforms, acknowledges
both returns and retains the exact prior item/currency/event-reward baseline.
Claim retries award nothing for the cancelled round. Neither fixture wore an
original garment; restoring a displaced original garment remains covered by the
earlier in-process test, not this new process run.

A subsequent eligible scheduled opening admits the same players.100 accepted
callbacks finish the match, return both players and pay only the White winner:
300,000,000 CPs,50,000 RVPs,100,000,000,000 silver and one bound P7 soul.
These are preserved legacy amounts, not a new economy adjustment. The single
winner entitlement and its exact soul UID persist. Another abrupt exit and full
cold restart preserve the complete event ledger, both purchased armors and
prior rewards; return/claim retries do not change the snapshot. The reload also
checks the winner's loaded soul and both loaded currency balances against the
saved values. The main retained career is untouched (211 stored identities).

Phases `career-tournament-interrupt`, `career-tournament-recover`,
`career-tournament-earn` and `career-tournament-reload` pass against private game
SHA256 `17976314CE4445B573179FF4AC744F2581196BA7E242D2A6BE2A09C31AF713A6`.
Checkpoint: `revora_tournament_process_probe` step3 / `TournamentProcessSnapshot`.
The reload phase was repeated after adding explicit loaded-balance/item checks;
no match or prize was replayed.

The test drives the production event-hit handler directly: socket attack decoding,
spell ownership/range/calculated damage and genuine client combat are not proved.
Calendar input and NPC approach are controlled. This covers interruption at99
saved hits and after paid completion, not interruption inside result/claim COMMIT,
unpaid-victory recovery, concurrent sockets or host power loss. No production
source change, deployment, live restart or client release was made.
