# Trade-partner admission, probation and achievement 10126

2026-09-15, M6.32, based on `53f19a1`. Server implementation and isolated checks
are complete; genuine 6711 acceptance and live deployment remain pending.

Two ordinary authenticated players can now become trade partners through mutual
consent. Both partner records, a shared probation start, the consent receipt and
achievement 10126 commit together. The old handler wrote the wrong counterpart
identity into one live record, saved the two directions independently, retained
unbounded request state and could not break a partnership with an offline peer.

## Evidence and behavior

The official [Trade Partners guide](https://co.99.com/guide/guides/tradepartners.shtml),
checked 2026-09-15, describes mutual agreement, a three-day wait and at most
twenty partners. The installed achievement catalog identifies 10126 as adding a
trade partner. `6711/ini/Achievement.dat` SHA-256:
`DF78DD498353B0B089F9B4228CADA5D79F4547AAE7B705349816671B2C29EBFC`.
The achievement is recorded at accepted admission; restricted trading waits for
probation to finish. Neither sending a request nor loading old rows earns it.

The [2046 protocol record](PROTOCOL_RESEARCH.md) documents the inherited
request/reciprocal-accept action, reject and break actions. Server-add packets
are not accepted as client commands. Names come from server state and are bounded
to sixteen bytes. Complete frames require length, family and suffix validation.
Packet layout confidence remains inherited source evidence, not a new native
capture. The optional overlapping 2071 flags are not reinterpreted here.

Requests bind both canonical sessions and entity instances and require the same
nearby map view. Revora bounds requests to two minutes, one outgoing request per
second and sixteen incoming invitations per session. Repeated requests cannot
extend expiry. Expiry, current sessions and proximity are checked again after
database owner locks. Reject clears the owned request. No verified native nonce
distinguishes an arbitrarily delayed packet from an identical response to a later
fresh invitation; the server authorizes only its current pending consent.

Both character locks and database entity rows use UID order. Admission enforces
the twenty-partner limit on saved rows, rejects duplicate historical directions,
and commits both records, one active unordered-pair receipt and both achievements.
It verifies affected rows and completion evidence before cache projection.
The native lists and achievement/title state refresh after commit.

New consent uses an authoritative UTC timestamp and preserves a compatible local
tick value in the old table. The loader verifies that value against the consent
receipt, then reads its timestamp as `DateTimeOffset.UtcDateTime`; this avoids
the installed Npgsql driver's local `GetDateTime` conversion. New probation is
exactly seventy-two elapsed hours, including across daylight-saving changes.
Countdown packets use remaining minutes rounded upward, so a partial minute does
not display as complete. The retained HoursLeft compatibility property converts
hours safely without integer overflow.

Removal atomically deletes both directions and closes active consent, even if the
peer is offline. Earned achievement remains permanent. Login reload after game
pool registration and post-commit re-resolution of online peers prevent stale
offline-removal snapshots from becoming the active list. Invalid loads preserve
the previous snapshot and fence the session for recovery.

Matching mature legacy pairs retain trading eligibility without achievement
backfill. Their original tick format lacks timezone provenance, so it continues
to be interpreted in the host's local timezone. A newly accepted request that
repairs a one-sided pair restarts both probation records together; it cannot
inherit old one-sided trust. Duplicate directions and receipt/timestamp conflicts
remain intact for reviewed repair rather than arbitrary migration.

Revora's existing locked-item trade path now requires mutual mature live records
when offering equipment and rechecks the saved mutual relationship inside the
settlement transaction, after both database owner rows are locked. Missing,
inconsistent or probationary saved relationships cannot transfer a locked item even
if both caches are stale. Original item identity, lock and payment remain in the
existing atomic trade transaction. This scope does not claim full equivalence
with Conquer's broader Forbidden Sale, Merchant or item-protection systems.

## Validation and deployment

The read-only public audit found zero partner rows, duplicate directions, missing
reverse rows or self references. This is a point-in-time snapshot. The ignored
x64 audit executable was hashed before execution:
`A6FEAB9FDC686A890C159B80E234FE8626339F9D121F968942C8722DA73CD669`.
It output aggregates and column types only, with no identities or secrets.

The 51 isolated partner checks cover ordinary-player consent, immediate identity
and achievement state, replays, reject, offline break/reload, proximity, malformed
frames, reconnects, full capacity, probation boundaries and daylight saving.
Injected first/second direction, receipt, evidence and removal failures prove
rollback and recovery, including silently skipped writes. Fixtures also exercise
historical rows, expiry and real movement while database locks are held. Native
trade handlers prove both rejection of stale saved eligibility and successful
original-UID locked-equipment/payment settlement after maturity.

Related regressions pass: 43 friend, 19 trade, 452 EXP, 74 progression, 91 title,
89 auction and 16 source/overlay audit checks. **835 checks total.**
Final isolated `Revora.exe` SHA-256:
`475A891BD37F14E69717D0DD1D65BBA64B741C934E88D2A4E8CE32C82CBD5277`.

No live binary, public schema, client tree or public release was changed.
Startup creates the new consent table and partner lookup index. Before deployment,
preserve the normal database/binary rollback and validate two ordinary genuine
6711 clients: request, accept, reject, offline break, reconnect, countdown,
achievement state, denied early locked-item trade and permitted mature trade.

Producer coverage is now **99/337**, leaving 238 missing producer identities.
This does not establish a per-character route to 320 or complete mentor admission.
