# Guild inscription and achievement 10226

## Evidence and ordinary-player route

The local 6711 Achievement.dat catalog (SHA-256
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`)
identifies 10226 as inscribing equipment in the Martial Arsenal. Existing server
packet 2203 action 1 and bulk 2204 are the inscription entry points; action 2
removes an inscription. These layouts are source-derived, not native captures.

An ordinary current guild member can inscribe owned bag or worn equipment in an
unlocked matching arsenal. Head slot zero retains its legacy alias to slot 8.
The service resolves the actual session item, checks canonical membership and
arsenal ownership, and validates saved item owner, type, position, warehouse,
stack size, inscription flag, plus and sockets. The established donation formula
is unchanged. No leader, GM, VIP or owner-name requirement is added.

## Transaction and client state

Under the existing session lease, item lock and guild gate, one transaction
updates the item flag, inscription row, member donation, guild-specific slot
sum and achievement evidence. The guild row and slot are locked. Failed writes
roll back all of these; the player is fenced until reconnect. Post-commit
projection updates the actual item and arsenal display. Login now restores the
saved item inscription flag in the common item deserializer, rather than only
in the standalone item lookup.

The owner can remove their own inscription, including after item/arsenal reload.
Removal uses the original saved donation amount and atomically reverses its
contribution; the earned achievement remains. Other guild members cannot remove
it through these packet paths. Replayed inscription/removal is a no-op. Bulk
requests handle each unique item separately; a valid owned item can succeed even
when another entry is unowned. This is not an all-or-nothing bulk transaction.

Periodic arsenal maintenance now shares the guild and player item gates and
skips fenced sessions, avoiding the interval between commit and projection.
Donation enumeration uses the inscription collection lock. Legacy offline guild
cleanup, donation recomputation after item upgrades, full battle-power expiry
reconstruction and all simultaneous guild membership changes are not redesigned
by this milestone. No deployment or native-client acceptance is claimed.

## Validation

The guild suite exercises ordinary bag and equipped inscription, immediate item
and achievement state, production character/arsenal reload, replay, foreign
items, foreign removal, wrong/locked slots, simulated sessions, saved ownership
mismatch, scoped guild totals, evidence failure and removal rollback/retry.
Synthetic bulk dispatch covers mixed ownership and an oversized count against
a short frame. Native packet padding/selection behavior remains pending.


## Confirmed equipment eligibility and capacity

The [official guild guide](https://co.99.com/guide/guides/guild1.shtml) requires
Elite-or-better equipment and limits registrations per member per arsenal:
7 for non-reborn characters below level 100, 14 for the higher-level non-reborn
band, 21 after first rebirth and 30 after second rebirth. Revora explicitly puts
level 100 in the 14-item band; the guide's below/above wording leaves the exact
boundary implicit. This boundary is an implementation decision, not a native
client observation.

New registrations now enforce catalog presence, quality and a saved-count cap
using the locked character's saved level/rebirth. Existing inscriptions remain
removable even when they exceed the new cap or fail today's quality threshold.
No existing items are deleted. The legacy donation formula remains unchanged.


## Durable enhancement purchase deadlines (2026-09-27)

New enhancement purchases now save `enhancement_expires_utc_ticks` with the
existing guild fee, enhancement level and guild enhancement total in one
transaction. The existing 30-day duration is retained as Revora behavior; this
change does not claim new official-rule evidence. UTC comes from the purchase
clock, and production `ArsenalTable.LoadArsenal` restores the exact deadline.
An upgrade verifies the saved deadline as well as the saved enhancement state,
so a stale projection cannot overwrite a newer deadline. Duplicate same-level
requests neither charge nor extend the deadline.

`GuildTable.Load` initializes the additive column before loading guilds. The
zero default explicitly represents old purchases whose deadline was never
stored. No backfill pretends to know their purchase date or renews them at login.
Future deployment needs a reviewed policy for those undated purchases. No live
schema or running server was changed here.

Validation: legacy x86 build passed (existing warnings retained); the isolated
PostgreSQL guild suite passed 954 checks, including exact UTC persistence,
production arsenal reload, deadline rollback, unchanged unrelated guild slots,
and rejection of a stale saved deadline without a fee. This is component
validation, not fresh-Host or native-client acceptance.

The next section records expiry settlement and reconstruction validation.
Fresh-Host/native acceptance and complete guild identity reconciliation remain
open. The later recovery snapshot checkpoint records deadline retention only.


## Expiry settlement and deterministic power (2026-09-27)

The [official guild guide](https://co.99.com/guide/guides/guild1.shtml) gives
slot power thresholds of 2M/4M/10M donation for 1/2/3 power and sums the five
strongest slots. `Rebuild` now applies those thresholds without incremental
flags, so falling donations, repeated ticks and reloads produce the same result.
The former calculation included overlapping boundaries, inconsistent increments
and no complete downward reset. A member login no longer clears guild power.

Enhancement remains Revora's existing level-one/two floor for a slot, following
the intent of the legacy `getbpEnchant` helper: effective slot power is the
larger of natural power and active purchased level. This floor interpretation,
the existing five-point purchase budget, current prices and 30-day duration
are preserved Revora behavior, not newly verified original-client rules. Rank
percentages are not changed by this checkpoint.

The maintenance loop settles all due slots for one guild in one transaction,
under its guild gate and saved guild-row lock. Each update compares the saved
level, raw power and UTC deadline; failure rolls back every slot and the guild
total. Expired bonuses are excluded from the calculated combat total even when
settlement fails, and the next pulse retries. Replays have no financial effect.
Expired slots can be purchased again through the ordinary leader service.
Legacy undated enhancements retain their existing benefit without a fabricated
deadline; conversion of historical purchases remains a deployment decision.

Production slot/inscription materialization rebuilds power before publishing
the guild. The existing one-second maintenance pass sends changed arsenal
state and shared power to eligible online members under player/guild locks;
busy players retry on later passes. This is not a synchronous global broadcast.

Validation: x86 build passed with existing legacy warnings, and 980 isolated
PostgreSQL guild checks passed. Added coverage includes threshold boundaries,
top-five selection, falling donations, enhancement floors, locked slots,
production loader reconstruction, the tick immediately before/exactly at
expiry, settlement failure/rollback/retry, reload before and after settlement,
cross-guild isolation, undated legacy preservation, fresh purchase after expiry,
and ordinary non-leader power projection. Evidence: ignored
`obj/arsenal-expiry-build.log` and `obj/arsenal-expiry-tests.log`.

No fresh game Host, native-client display, production migration or deployment
was run. The full recovery publication and rollout gates stay open; deadline retention
in the read/copy layer is covered by the checkpoint below.


## Recovery snapshot deadline retention (2026-09-27)

The committed `GuildIdentity` arsenal read and copy paths now retain
`EnhancementExpiresUtcTicks`. Zero remains the legacy undated marker. Negative
values, values beyond `DateTime.MaxValue.Ticks`, and missing saved timestamps
reject recovery rather than silently creating an undated enhancement. Schema
preparation includes the same additive deadline initialization as guild startup.

The isolated guild suite passed 987 checks after an x86 build with existing
warnings. Seven added checks cover the production arsenal recovery read with
matching association marks, exact deadline retention, independent copied state,
and rejection of negative/out-of-range database and supplied snapshot values.
Evidence: ignored `obj/arsenal-recovery-deadline-build.log` and
`obj/arsenal-recovery-deadline-tests.log`. Validation compiled the committed
recovery source plus this change, excluding the owner's separate recovery WIP.

These checks exercise the recovery read/copy layer, not publication of a
recovered guild into a fresh running Host or native client behavior. Derived
recovery consumers and runtime replacement remain separate acceptance work.


Subsequent actual isolated startup at gameplay `19f7471b` passed two game/database
cycles and verified the new deadline schema after each run. This advances the
startup gate only; no logged-in guild/player recovery was exercised.
[Startup evidence](ROADMAP_STARTUP_ACCEPTANCE.md#arsenal-deadline-startup-validation-2026-09-27).

## Retained ordinary arsenal across fresh processes (2026-10-01)

- [x] The retained non-GM guild leader donates 4,600,000 silver through the
  ordinary service, opens headgear slot 8 for 5,000,000 guild silver, inscribes
  an already-owned equipped item, and buys enhancement level two for 100,000
  guild silver. Repeated sealed 2203 requests do not charge again.
- [x] A fresh process retains the inscription, 1,362,330 arsenal donation,
  two battle-power points, zero remaining guild funds and the 30-day deadline.
  Achievement 10226 uses the ordinary guild-inscription-v1 producer.
- [x] Advancing the controlled clock by 31 days settles expiry through the
  production refresh/publication path: enhancement and bonus power become zero,
  while the inscription, achievement and currencies remain intact.
- [x] A further fresh process retains that exact expired state; repeated unlock
  and inscription requests remain no-ops.

The first earning run completed its payments and save, then failed in the test
snapshot query because it misspelled guildsilverdonation. That original phase
is not reported as passed. A guarded recovery compared the previous retained
checkpoint against the exact four expected changes: personal silver, the owned
item's inscription, guild funds and maximum enhancement. It also verified saved
donation and deadline values before resuming, without another purchase.

Recovery, cold reload, expiry and expired reload phases all passed against
private game SHA-256
07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2.
Evidence: tools/CheckCandidateCareerRites.cs and isolated
arena-process-career-arsenal-*.log files. No production change or rebuild was
needed. Clock and proximity are controlled; native display, natural elapsed
30-day observation and live deployment remain unverified.
