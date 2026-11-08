# Guild transporter journeys and achievement

2026-09-20, baseline `a57f979`. Extends the owned Adventure passage service to
all four installed guild transporters and connects completed travel to 10117.
Native acceptance and deployment remain pending.

## Evidence and existing edition

The installed achievement catalog describes 10117 as using the guild portal;
its icon identifier is `Achievement_UseFactionCon`. Catalog SHA-256:
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.
The existing server routes and a fresh read-only database inspection confirm:

| Conductor | Installed location on public map 1038 | Existing destination | Fare |
| --- | --- | --- | --- |
| 9884 | 348,320 | 1216 at 12,481, near Old Explorer | 1,000 silver |
| 9885 | 348,329 | 1015 at 792,573, Bird Island | 1,000 silver |
| 9886 | 339,338 | 1217 at 536,558, hunting grounds | 1,000 silver |
| 9887 | 331,338 | 1001 at 337,325, Mystic Castle | 1,000 silver |

Guild Controller 380 in Twin City at 319,325 admits ordinary unguilded visitors
to the public transport area. This existing service has no winning-guild or GM
requirement. Terrain validation may select the nearest legal destination tile.

The read-only inspector is the previously reviewed
`obj/chi-refill-validation/WaterlordAccessInspect.cs`, executed as x64 with binary
SHA-256 `42112104822191A621877C09A1FBB6025B39B6018B0F1BF67B271E9AAD6C9CAE`.
It reads protected connection settings without printing them and selects only
public NPC/map metadata in a read-only transaction. All four actors are present
in `sobnpcs`, with their existing Spanish transporter names and type 2.

The [official 2015 notice](https://co.99.com/news/2015-11-13/notice_regarding_guild_conductress_blocking_portals.shtml)
confirms a Guild Conductress service with player-controlled placement. It does
not verify these particular fixed destinations or prices. This batch preserves
Revora's installed fixed-route edition; it does not implement guild-leader
relocation or claim historical routing parity.

## Payment, arrival and recovery

Previously only 9884 used an owned payment record. The other three branches
deducted silver before an unchecked teleport. They now use the same canonical
session, proximity, trading, destination, saved source-map and silver-preimage
checks. Their short dialogue describes the actual destination and fare.

Each character has one bounded passage row. The silver debit and a pending
passage with its conductor/destination commit together. Another conductor cannot
redirect or charge over that pending trip. Repeating the original request after
reconnect resumes it without a second fare. A later intentional trip pays again.

The destination map write consumes the matching passage through the existing
arrival trigger. Suppressed passage consumption rolls back that map write. After
teleport, the service also checks the saved map and consumed passage; runtime
position alone cannot certify arrival if a database write was silently skipped.
An interrupted trip remains recoverable from its saved state.

Achievement delivery is a separate recoverable transaction after arrival. It
requires a completed paid passage with a valid conductor/destination pair and
locks the character and passage before inserting 10117. A failed achievement
write leaves the successful journey intact and fences further mutations; the
normal login path retries from the completed passage. It never charges again.
This is not an atomic database transaction spanning payment, teleport and award.

The migration adds conductor/destination columns with 9884/1216 defaults, because
the prior ledger supported only that route. Existing completed records can prove
that historical journey; pending records and mere guild-map presence cannot.
Unrecorded legacy travel on the other three routes is not backfilled. Generic
client achievement assertions remain disabled.

## Validation scope

The Adventure fixture retains its complete ordinary walking journey from mined
materials through Guild Controller, conductor 9884, the Adventure Islands,
Waterlord, crafting and first rebirth. Its conductor arrival now earns 10117.
The three additional routes use their actual installed actors and terrain, with
concurrent purchase, production character/title reload and failure/retry checks.
They do not each repeat the full mining and island journey.

New failures suppress payment, saved arrival and achievement writes. A request
at a different conductor cannot reuse the pending fare. Recovery verifies exact
remaining silver, a single trip and once-only achievement credit. Saved arrival
and login recovery are exercised through production components; a whole Host
restart and genuine-client journey remain unverified. The old-schema migration
test preserves a pending paid passage and supplies its original route defaults.
The isolated build, 203 Adventure journey checks and three producer audits pass.
No live database migration, service restart or patch.
