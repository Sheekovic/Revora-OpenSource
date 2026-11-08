# Restored event lifecycle integration

## Active world-timer repair (2026-09-27)

The world timer intentionally returns before retired legacy event logic. Several
restored services had been added below that return, so initialization and component
tests could pass while their recurring callbacks never ran in the composed game.
Team PK exposed the problem through actual admission interruption/reconnect.

The following restored calls now run above the return in a serialized pulse:

| Callback | Responsibility |
| --- | --- |
| Quarantine.StartScheduled | Existing server-local Team Tournament opening calendar |
| SkillTeamTournament.OpenAt | Wednesday Skill PK opening with durable date identity |
| FashionCompetition.Tick | Due fashion judging/settlement |
| MrMs2016.Tick | Restored 2016 event lifecycle |
| MrMs2017.Tick | Recurring Revora flower contest lifecycle |
| GuildSeasonWar.Tick | Restored guild season lifecycle |
| GuildManagers.Tick | Guild manager maintenance |
| GuildBeastEvent.Tick | Guild beast lifecycle |
| GuildWarPurses.Tick | Guild/Super Guild purse recovery plus Team Tournament deadline, return and reward polls |
| GuildPrizeRollover.Tick | Guild prize rollover |
| ClanWar.Tick | Restored clan event lifecycle |

Team Tournament's own tested UTC deadline/retry logic closes its round; its
existing StartScheduled method owns calendar eligibility and durable daily identity.
The unreachable duplicate fixed-minute opening/closing block was removed.

Each restored callback is attempted even if an earlier one throws. Failures name
the service and exception type without secrets, limited to once per service per
30 seconds. The pulse has one owner; a completion marker is emitted once after
all calls return successfully. Services retain their own enablement, eligibility,
calendar, retry and persistence guards. A completion marker proves invocation,
not that every service opened an event or granted a reward.

- [x] Isolated committed-source-overlay build passes (legacy warnings retained).
- [x] Two actual private game/database startup/shutdown cycles execute the pulse,
  retain 31 checked roadmap tables and exit normally. The retained second-cycle
  log contains no restored-pulse/calendar/poll/settlement failure marker.
- [ ] Due-calendar matches, ordinary-player earning/recovery for every service,
  injected callback failures, native presentation and live deployment remain
  separate acceptance gates.
- [ ] Audit remaining legacy war start/end sites against their restored replacements
  before enabling them. Their presence below the return alone is not approval to
  revive duplicate or retired lifecycle logic.

Evidence: `obj/restored-event-hooks-build.log`,
`obj/restored-event-hooks-startup.log`. Candidate executable SHA-256:
`BEF7495CCE473560AEC7E6198FB50B64FF35FD50B4DC8CDB1574FA4483960C5E`.
Both private processes are stopped. Previous candidate artifacts are retained;
no live server or client deployment occurred. Existing per-feature test totals
and wardrobe classifications are unchanged.

## Remaining legacy war audit (2026-09-27)

| Legacy site | Active implementation / remaining gap | Decision |
| --- | --- | --- |
| Sunday Guild War start/end | `GuildSeasonWar.Tick` already calls its owned `Finish` and `Start`; `GuildWar.Start/End` delegate to this service | Keep the replacement. Restore its three-second scoreboard publication in the same tick. |
| Wednesday Super Guild War | `SuperGuildWarPurses.Tick` delivers saved prizes but does not start/end battles. The only automatic `Start/End` sites are below the return. | Calendar integration remains incomplete. |
| Elite Guild War | Its durable battle methods exist, but automatic `Start/End` and scoreboard calls remain below the return. | Calendar integration remains incomplete. |
| Golden War / `PoleTwin` | Legacy start/end calls remain below the return; implementation still uses mutable scores and legacy persistence. | Do not activate this as a restored durable earning route. |
| Legacy Elite PK | `ElitePkEvent.Current.Tick` already runs in the active world timer and owns Friday registration | Keep the active replacement; do not also register legacy tournament timers. |
| Skill Team PK | Opening block is commented out and ordinary service remains closed | Keep its documented incomplete status. |

`SuperGuildWar.SaveStart` and `EliteGuildWar.SaveStart` use random UUIDs. Their
initializers cancel active rows and clear the pending UUID. Reusing the legacy
opening window after a restart could therefore create another battle for the
same scheduled slot. Their automatic end conditions are also narrow clock
windows, not persisted deadlines. Simply moving those blocks would not meet
the roadmap's replay and recovery requirements.

Next implementation: retain each war's existing calendar, give scheduled slots
stable persisted identities and deadlines, and verify opening, restart refusal,
late settlement, ordinary admission and reward recovery before wiring activation.
This audit does not claim those calendars are repaired. Sunday scoreboard
publication is a server-side repair; native display remains unverified.

Validation: isolated committed-source-overlay build passed after the scoreboard
repair (`obj/war-lifecycle-audit-build.log`; existing legacy warnings retained).
No new runtime, calendar, client or deployment acceptance is claimed.

### Super Guild calendar follow-up

The subsequent Super Guild batch reconnects `SuperGuildWar.Tick` with stable
Wednesday identities, saved UTC deadlines and late/restart reconciliation.
The earlier audit table records the defect found at that point; Super Guild's
code integration is now repaired, while Elite Guild remains disconnected.
22 focused checks and two actual private startup cycles pass. Ordinary scheduled
combat/reward, native and live gates remain open.
[Evidence](SUPER_GUILD_WAR_RECOVERY.md#active-scheduled-lifecycle-2026-09-27).

### Elite Guild calendar follow-up

`EliteGuildWar.Tick` is now also on the active pulse, with stable event-date
identities, saved closing deadlines and delayed/restart reconciliation. Its
five existing days are retained; the opening second is widened to the remainder
of the 21:01 minute.29 focused checks and two actual private startup cycles pass.
The earlier audit's Super/Elite calendar gaps are now repaired at code and private
startup scope; scheduled ordinary-player earning remains unverified.
[Evidence](ELITE_GUILD_WAR_RECOVERY.md#active-scheduled-lifecycle-2026-09-27).

## Callback failure containment (2026-10-02)

`tests/WarCalendar/Verify-RestoredCallbacks.ps1` invokes the production
`Thread.RunRestoredTick` wrapper from the hash-checked private candidate
`34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`.
Seven scenarios pass (107 assertions, including 100 repeated failures):

- Exceptions return failure without escaping the wrapper.
- Logging contains the service identity and exception type, excluding the
  synthetic sensitive exception message and stack.
- Repeated failures still execute while their logs are suppressed; suppressed
  failures do not extend the original throttle timestamp.
- Another service can report independently.
- An expired throttle permits another report and refreshes its timestamp.
- A recovered callback returns success without logging another failure.
- The production aggregation expression attempts a subsequent callback even
  after the earlier callback reports failure.

The first fixture run incorrectly required two immediate UTC readings to differ;
Windows clock resolution can make them equal. The corrected assertion compares
the refreshed timestamp against the deliberately expired timestamp instead.
No gameplay change was required. The runner compiles only the fixture, invokes
no server entry point or actual event callback, reads no configuration and opens
no database connection. This is wrapper-level evidence, not injected failure in
the complete world pulse, a due-calendar match, concurrent pulse ownership or
an event's reward transaction. Those broader gates remain separate.

Current calendar follow-up: both Super and Elite Guild War have subsequent
scheduled ordinary-player earning and saved-prize recovery across actual private
game/database processes. The older audit table above records historical gaps;
it is not the current activation state. See the process sections of
[Super Guild War](SUPER_GUILD_WAR_RECOVERY.md) and
[Elite Guild War](ELITE_GUILD_WAR_RECOVERY.md). Their controlled clocks/damage,
native acceptance and live-deployment limitations still apply.

### Actual pulse fault and concurrent ownership follow-up

The same fixture now also invokes production `Thread.TickRestoredEvents`.
Nine additional assertions pass, for 116 total including the wrapper repetition
checks. No gameplay binary change or server startup was needed.

An injected exception in Super Guild War's clock propagates out of its actual
`Tick` into the pulse wrapper. Elite Guild War's clock and the final Clan callback
both execute afterward. The failed pulse logs the expected redacted failure,
does not announce completion, and releases its owner. Removing the fault allows
the next pulse to reach both later callbacks and emit its single completion
marker. A third pulse blocks inside the Super callback while a competing pulse
is invoked: the competitor invokes none of the observed callbacks. Releasing the
owner permits the later callbacks to finish, with no new failure/completion log
and no stranded ownership.

Scope: this uses the actual callback list and synchronization, with unopened
services, empty player pools, an off-calendar noon clock and future war-poll
deadlines. It does not initialize event databases or run due matches. Observed
clocks establish invocation of Super/Elite/Clan, not full execution of every
disabled service. Injected active-battle or reward-transaction failures and
native/live acceptance remain separate gates. The earlier wrapper-only paragraph
describes the first batch; this follow-up extends that specific scope.
