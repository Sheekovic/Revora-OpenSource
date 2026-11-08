# Recover Arena rows on a login cache miss

The character login loader now asks both ArenaTable and TeamArenaTable to recover
an existing statistic before creating a first-time row. Team Arena previously
attempted an insert directly when its global cache missed. A saved team identity
can now survive cache loss without duplicate insertion. Both recovery methods
also replace cache entries whose EntityID is zero or belongs to another owner.
Concurrent recovery publishes one valid cached object per owner and Arena type.
A valid existing cache is preserved, including unflushed legacy changes.

The new team reader restores saved points, Honor, current/previous counters,
rank and appearance fields; null legacy numeric fields retain the existing zero
interpretation. Duplicate persisted identities throw instead of choosing a row.
No row means genuine first-time creation remains the login loader's responsibility.
The team startup loader now reads TodayWin and TodayBattles as UInt32, matching
the statistic fields rather than truncating at the old byte boundary.

The combined disposable-schema fixture buys points through the ordinary service,
removes both caches, concurrently calls the production recovery methods and checks
saved balances and one persisted row. It also checks invalid cache replacement,
valid-cache preservation, full startup team loading above 255, absent identities
and duplicate-row rejection. This is production-loader evidence, not an actual
client reconnect or the full EntityTable login sequence. Stale caches from economic transaction failures are now handled by the explicit
invalidation path below; the full socket disconnect/reconnect flow remains open.

Source is wired into EntityTable login; no deployment/restart or native-client
acceptance occurred. See QUEST_EVENT_STATUS.md for passing build/test totals.
Ignored log: obj/chi-refill-validation/arena-login-recovery-tests.log.


## Economic transaction failures

ArenaCacheRecovery marks the exact solo/team statistic objects invalid using weak
object keys and removes only matching cache entries. It performs no database work,
so an outage cannot prevent invalidation. Result completion, daily/custom/ranking
payout, rollover, point purchase and Honor purchase failures use this path before
the existing session fence and reconnect message. Inventory-only conversions and
unrelated quest failures do not discard Arena caches.

Both saver overloads reject marked objects even after the owning session has left
GamePool, including unmanaged legacy players. Login recovery skips marked objects
if another caller reintroduces them. A later failure from the old session cannot
remove replacement cache entries. Markers last for the lifetime of the failed
objects and do not retain permanent account/UID tombstones. Process restart starts
with fresh objects loaded from persistence.

Honor purchases now flush both canonical legacy statistic objects before charging,
so cache invalidation on a failed purchase preserves independent team counters as
well as previously earned Honor. Managed balance fields remain transaction-owned.
This does not migrate the remaining team result writer or finish live coordinator
activation.

Tests first commit a real point purchase, deliberately model a failed cache
projection, and invoke the production failure path. They verify reload from saved
points/Honor, both detached save overloads, replacement-entry protection and
reintroduced invalid objects. Actual injected purchase and Honor receipt failures
also exercise catch-path invalidation and retained balances/team progress. The
post-commit projection failure is simulated directly; it is not native reconnect
acceptance. Ignored log: obj/chi-refill-validation/arena-cache-recovery-tests.log.
