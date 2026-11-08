# Mutual marriage and achievement

Source review and repair, 2026-09-25. Client achievement 10129 identifies marriage;
10130 separately requires paired walking and is not awarded here. Identity evidence
uses installed Achievement.dat SHA-256
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.
The existing Attack request/accept boundary and native uncertainty are recorded in
[protocol research](PROTOCOL_RESEARCH.md#marriage-requestaccept-server-boundary-2026-09-25).

The previous active handler accepted without a saved proposal, checked neither
proximity nor canonical sender identity there, and changed spouse names only in
memory. Marriages now requires a two-minute proposal bound to the original sender
session, both Entity instances and the intended recipient. One pending proposal
per recipient and a one-second sender cooldown bound request state. Accepting a
superseded proposal, spoofing the sender, replacing a session or replaying success
cannot create another marriage.

Both ordinary authenticated players must be alive, single, on the same live map,
within twelve tiles, and outside trade/booth activity. Existing Flowers male/female
body classifications supply opposite-sex eligibility, rather than the old final
mesh-digit guess. Ordered player locks protect proposal state and serialization.
Database owner locks and exact name/single-state preimages precede both spouse
updates and both achievement receipts in one transaction. Consent and proximity
are checked again after database waits and before commit. Postcommit spouse packets,
achievements and effects are projected only after persistence; failure fences both
sessions so autosave cannot overwrite the committed state with stale data.

Isolated Debug/x86 build, 66 social checks and five producer audits pass. The new
cases use the actual Attack handler with ordinary fixture players: valid mutual
consent, concurrent accept replay, unsolicited accept, expiry, distance, same-sex
rejection, replacement session, spoofed sender, superseded proposer, stale saved
spouse, suppressed spouse writes and suppressed achievement writes. Both saved
spouse directions and both completion receipts are checked, plus fresh production
achievement loading. This is controlled handler/database validation, not a genuine
client proposal exchange or complete character login test.

Remaining acceptance: full character reload of spouse names, delayed database-lock
expiry and crossing-proposal races, native proposal/accept presentation, and the
complete marriage-dependent event route. Divorce/name-change policy and paired
walking are separate work; no new rules for them are introduced. No historical
marriage achievement backfill, live deployment or client patch was performed.
Unrelated owner edits were present in the isolated assembly.

## Character reload and consent races

The additional isolated validation passes 69 social checks and 634 collection
acquisition checks against the existing built server assembly. Three social cases
exercise consent expiry during an actual PostgreSQL owner-row lock wait, crossing
mutual proposals, and two recipients accepting the same proposer concurrently.
They produce no expired marriage, no deadlock and only one reciprocal spouse pair.

Two ordinary full-character fixtures then marry through the Attack handler and
reload through the production character/item loader. Both spouse names and both
10129 achievements survive; a subsequent acceptance replay creates no additional
completion. The reloaded pair satisfies CouplePkEvent's existing Married predicate.
That is a prerequisite test, not the complete admission/combat/reward event route.

Five inventory audit tests also pass. Full character spouse reload, the database
wait and proposal races are now covered. Native UI, full marriage-dependent event
earning, divorce/name-change policy and paired walking remain separate open work.
No new production source changes, deployment or native-client test in this batch.

## Marriage-dependent event integration

The Couple PK suite now creates all spouse pairs through marriage request/accept.
Both initial opposing couples reload their full characters and run the normal
stat-calculation phase before NPC admission. Sixty event checks pass through
contested combat, championship, both independent claims and exact reward reload.
See [the event evidence](COUPLE_PK_WAR.md#earned-marriage-through-couple-pk-2026-09-25)
for controlled team/time/combat inputs and unchanged native/live gates.


The retained ordinary career now also completes mutual marriage after Sereth's
paid gender rite and supplier-earned flower deliveries, with both spouse links,
prior assets and achievement10129 preserved across game/database restart.
[Exact route and fixture limits](FLOWER_QUEEN_EARNING.md#retained-suppliers-gender-rite-and-marriage-2026-10-01).
