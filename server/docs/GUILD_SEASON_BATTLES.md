# Weekly Guild War results (M7.1b)

The regular Sunday Guild War now produces durable, provenance-backed battle
results for the planned Holy Grail route. This completes the battlefield
prerequisite. M7.2a-d add claims, exchanges, rollover and cup stat corrections;
[M7.3](HOLY_GRAIL_CRAFTING.md) completes the server Grail crafting and wardrobe
route. [M7.2e](GUILD_WAR_PURSES.md) adds atomic regular Sunday rank purses;
native acceptance remains open.

## Calendar and ordinary play

The existing map 1038 and Sunday 15:00-16:00 server-local hour are preserved.
Five consecutive scheduled Sundays form a season. The first season begins
September 20, 2026; the next begins October 25. These repeating dates and the
existing local hour are an explicit Revora adaptation of the five-week source
edition documented in [Holy Grail research](HOLY_GRAIL_RESEARCH.md).

Stored start/end times use UTC `timestamptz`. Each season also records its local
timezone identifier and ending instant; a mismatched calendar cannot silently
replace it. Daylight-saving changes keep the local battle hour. Empty or missed
weeks do not shift subsequent rounds. Wednesday's separate Super Guild War
does not count toward this season.

The scheduler polls throughout the battle hour, so it no longer depends on
observing one exact minute to start or finish. Starting late still preserves the
scheduled deadline. The existing guild invitation, native response route and
map entrances remain available. Start clears old pole ownership, scores and
damage; a prior week's owner cannot win an empty new week.

Only canonical, fully loaded, living ordinary players with their actual current
guild/member objects can damage the owned structures. The exact map and structure
references must match, and the player must be within view distance. Fake players,
replaced sessions, fake member objects, copied structures and remote attacks are
rejected. No GM status is needed. Ordinary shared attack calculations still supply
damage. Seasonal contribution is capped at the structure's remaining HP.

Pole 810 and gates 516074/516075 retain their installed identities and health.
Breaking a gate opens its existing mesh. The guild with most accepted damage in
a pole cycle captures it; a tie chooses the lower guild ID deterministically.
The holder cannot damage its own pole. Capture restores the pole and gates once;
the old dispatcher no longer applies the finishing damage again after that reset.
Per-cycle contribution storage is bounded at 4,096 player/guild pairs.

## Durable ownership and recovery

Additive tables:

- `revora_guild_seasons`: calendar identity, timezone and boundary.
- `revora_guild_battles`: one scheduled day/round, deadline, state and final owner.
- `revora_guild_captures`: ordered capture receipts with the winning damage total.
- `revora_guild_contributors`: player/guild damage evidence for each capture.

Capture, contributor rows, battle ownership and the existing guild win/loss
counter adjustment commit together. Every required write checks its affected
row count. A suppressed write leaves the prior owner and health intact, allowing
the finishing attack to retry. Runtime ownership and guild counters are projected
after commit. Recovery can read a committed capture whose runtime acknowledgment
was lost without recording another win.

At or after the deadline, the last captured guild becomes the saved winner if
it still exists. A dissolved holder or a battle without a capture yields an empty
result. Premature End calls do not award a season victory. Finalization is guarded
by the battlefield owner and a locked database row. A committed terminal result
can be reconciled after a missing runtime acknowledgment; repeated End calls do
not repeat finalization.

Startup cancels interrupted active battles. It does not invent a winner from
stored pole names or resume uncheckpointed combat. A start row with no matching
runtime owner is likewise cancelled on retry. Completed and cancelled days cannot
restart after a clock rollback. A new scheduled week remains available.

At M7.1b, the local purse still used the old payment loop.
[M7.2e](GUILD_WAR_PURSES.md) supersedes it for the regular Sunday war with
result-bound entitlements and atomic delayed delivery. Loose purse tokens remain
separate from seasonal proof. See that milestone for its validation and limits.

## Validation

Staged build SHA-256:
`DD3BC3BDA734B6CC9625026350FFD9EE6E184F64A9714B8BC206486F8A238033`.

`tests/PotencyHunt/Verify-PotencyHunt.ps1 -GuildSeasonOnly` passes 43 checks using
ordinary non-GM fixtures, the actual map cache and the three installed structure
rows copied into a disposable schema. The cache SHA-256 is
`1707D73636D727AC471673736DC3965596AEE43E09818DA4B67C0D1C8BD4B2FD`.

Checks cover scheduled concurrency, stale ownership, canonical combat, overkill,
weighted/tied captures, gate destruction, suppressed database writes, win/loss
rollback, concurrent finishing blows, exact deadlines, uncertain-commit projection
recovery, saved results after initialization, interrupted battles, dissolved
guilds, five-week grouping, DST and the separate Wednesday event. Recovery fault
cases model committed database state with a missing runtime projection; they do
not claim an actual network failure or a full Host restart.

Related final-build suites pass: CTF 48, Elite PK 78, weekly/monthly PK 60,
Class PK 116, Couple PK 43, Potency/daily bosses/Extraction 216 and combat 669.
Total with the focused suite: 1,273. The ledger fixture deliberately excludes
the legacy purse loop's player payouts. Their subsequent migration and reload
tests are documented in [M7.2e](GUILD_WAR_PURSES.md).

No live listener, restart, deployment, client patch or production-row mutation
was performed. Genuine-client invitation, gate/pole rendering and a complete
ordinary guild match remain acceptance tasks.
