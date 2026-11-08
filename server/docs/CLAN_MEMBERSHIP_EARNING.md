# Clan membership earning prerequisites

Reviewed at source commit f6aa04f. The hash-bound installed Achievement.dat
(`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`)
identifies 10304 as joining a clan. The original review below preceded the accepted-membership implementation
described at the end. Existing membership alone is not historical earning evidence.

## Source findings

`Game/Features/Clans/Clan.cs` contains the existing membership implementation:

- `Join` (line 299 at this checkpoint) sets in-memory clan ID/rank/name before
  separate UpdateClanID and UpdateClanRank writes. Member insertion and outgoing
  state follow those writes without an encompassing transaction or receipt.
- `Join` and `Recruit` request cases (631/656) forward requests but do not keep a
  session-bound, expiring invitation/application that the accept paths verify.
- `AcceptRecruit` (675) trusts the leader UID and acceptance byte to find a clan.
  Its target lookup uses a dictionary index before checking null. The shown path
  checks leader rank and capacity but does not match a retained invitation.
- `AcceptJoinRequest` (698) checks the accepting leader and capacity, then joins
  the supplied online member without matching a retained application. The member
  service itself does not reject prior membership or reserve the final slot.
- `CreateClan` also saves clan metadata and character membership separately.
  Founding cannot be counted as a completed transactional earning route yet.

These are source findings, not results of attacking the live server. Legacy
packet action values and layouts are inferred from this implementation; no
native 6711 clan interaction has been verified by this review.

## Implementation and acceptance sequence

1. Bind invitations/applications to both canonical sessions, character bodies,
   intended clan, direction and expiry. Require mutual consent, current leader
   authority, ordinary eligible players and valid proximity through commit.
2. Serialize capacity and membership changes with an explicit lock order. Save
   clan ID, rank and the accepted-membership achievement receipt in one database
   transaction, checking durable preimages. Project live membership only after
   commit and fence/reload on failed projection.
3. Test both directions through the existing packet boundary, including unsolicited
   accept, replay, expiry during database waits, replacement sessions, remote
   players, existing members, competing final-slot accepts, revoked leadership,
   suppressed writes and ordinary-player reload. Review quit/kick/disband
   concurrency before claiming clan lifecycle safety.
4. Validate founder creation and costs separately, then native UI and deployment.
   Five territory achievements require their own durable event outcomes; joining
   or founding must not grant those achievements.

The initial review changed no clan gameplay, packets or database rows.

## Existing packet boundary under repair

The active Clan.Process route reads packet ID 1312, action byte at offset 4,
peer UID at offset 8 and acceptance byte at offset 16. Existing request actions
are Recruit=9 and Join=11; matching accepts are 10 and 12. This source-derived
layout is not newly verified client protocol. The repair will retain those
existing actions, validate bounded framed input, and use server-owned sender
identity and consent state. No additional packet family will be enabled.

## Accepted-membership implementation

ClanMembership now intercepts the four existing membership actions before the
legacy cases. Incoming frames require the existing 1312 header, consistent
length, TQClient seal and a bounded 26-1024 byte size. Unsupported clan operations
retain their existing handler; this is not a security claim about that full family.

Invitations and applications expire after two minutes and bind both canonical
sessions/bodies, direction and the intended Clan instance. A one-second request
cooldown and one pending offer per receiver bound transient state. Consent is
required in both directions; declining clears the matching offer. Both players
must remain alive, nearby on the same map instance and outside trade/booth
activity. The leader must still lead that clan and the applicant must be clanless.
There are no GM or owner-name eligibility requirements.

Ordered player locks serialize competing accepts. Owner-row locks followed by
the clan-row lock precede durable leader/membership checks and the twelve-member
capacity query. Both in-memory and durable capacity must allow admission. Saved
membership/rank/donation reset and 10304 evidence commit in one transaction after
rechecking consent and eligibility. Only then are live member state, spawn,
clan information and achievement projected. Projection failure fences both
sessions for reload; a transaction failure creates no earned membership.

The build and 27 focused checks pass with `-ClanMembershipOnly`: actual Clan.Process
requests in both directions, concurrent duplicate accepts, ordinary character
reload, unsolicited/remote/expired/replacement-session/prior-member/revoked-leader/
declined rejection, suppressed membership/evidence writes, malformed frames,
unknown peers, competing clans and competing final-slot admits. Clan creation is
a fixture here; no founder cost/creation claim is made. The isolated fixture
initializes an empty tournament list for existing clan-info rendering.

Still open: invitation expiry during an actual database wait, full server restart
of clan registries, quit/kick/disband/leadership-transfer
coordination, spouse-rank transitions, native invitation/accept UI and deployment.
No territory achievements, historical backfill, public patch or live writes.

## Founder transaction and ordinary earning journey

Clan.CreateClan now delegates to ClanFounding; the old separate NPC silver debit
is removed. The owner must be clanless, meet the configured minimum level and
stand at registrar 9450 in the actual Twin City map. The existing price/fund
configuration is preserved (current defaults: level 50, 100,000 silver price,
50,000 initial clan fund). The name must fit the existing 2-16 character field,
encode losslessly within its 16-byte legacy text limit and contain no controls.

The owner lock and a founder gate precede owner-row and transaction-scoped
PostgreSQL advisory locks. The database checks case-insensitive name uniqueness
and advances the persistent clan ID counter past durable and live registry IDs.
Clan insertion, founder membership, silver debit and join achievement 10304
commit together. Insufficient funds, duplicate names, old membership and save
failure cannot leave a free clan or a charged character without the clan.
Live registry, balance, clan information and the existing world announcement
follow commit. Failed projection fences the founder; fresh process recovery of
a failure between commit and registry installation remains to be verified.

Build and 45 focused clan checks pass. The added path submits a real NPC name
packet, creates the clan, checks one fee and configured fund, reloads the founder,
and admits another ordinary player by invitation. Failure injection covers clan,
entity, achievement and specifically the later money-decreasing update, proving
rollback after clan insertion. Competing case-insensitive names charge one
founder. Eligibility rejection covers insufficient silver, level, distance and
invalid names. Clearing and rebuilding Kernel.Clans with production LoadClans
restores the founded clan, fund and invited member from the disposable schema.
This is registry reload evidence, not a fresh Host process or native UI test.

Remaining lifecycle work is unchanged: quit/kick/disband/leadership coordination,
spouse transitions, wait-expiry, fresh-process recovery, native UI and deployment.

## Quit and kick transactions

ClanDepartures consumes the existing self-quit and named-kick actions before the
legacy switch. Quit only targets the sender, and leaders must use disband or
transfer rather than quit. Kick requires matching live and durable leader
identity/rank and can target only one identified member of that clan. It cannot
kick the leader, a foreign character or an arbitrary online name. The old branch
that accidentally saved zero clan fields to the requesting leader is unreachable
for these actions.

The service acquires character session leases and online owner locks in UID
order without waiting on a busy lease/owner. Busy operations leave membership
unchanged and can be retried. This coordinates offline changes with the existing
login ownership mechanism; a full simultaneous login/disconnect test remains
open. Durable owner-row and clan-row locks guard the update. Membership, rank
and donation clear together; the live roster changes after commit. Online target
state clears immediately, including clan name/shared BP, and receives departure
and spawn packets. Offline targets load the saved departure when they return.
Earned 10304 evidence is deliberately retained. Failed writes preserve membership
and fence the affected online sessions for reload.

Offers also carry per-session membership versions. Founding, joining and leaving
invalidate the affected character's older applications/invitations, so a previous
application cannot be accepted after joining and quitting another clan.

Build and 72 focused checks pass. Added checks cover ordinary self-quit, online
and offline kicks, replay, leader preservation, reload with historical credit,
member/outsider/self/foreign-target rejection, durable rank mismatch, suppressed
departure writes, invalid kick name bounds, old application invalidation, and a
busy login lease followed by successful retry. Existing founder/payment/admission
checks and production registry reload also pass.

Disband, leadership transfer, departure during real login/disconnect, remaining
members' shared-BP/roster presentation, and native/client/Host acceptance remain
open. No live deployment or public patch was performed.

## Confirmed disband and retired identities

The registrar's existing options 9/19 now require a two-minute confirmation
bound to the leader session, character body and Clan instance. Cancel is handled
before the generic NPC farewell return. Confirmation checks the same live offer
again after acquiring ownership and database locks and before commit; a cancelled,
expired, replaced-session or unauthorized request cannot disband the clan.

The service reads durable member UIDs, acquires their login leases and online
owner locks in UID order, then locks the owner rows and clan row. Busy locks
leave the clan unchanged. A rechecked member set and durable leader rank precede
one transaction clearing all memberships/donations, deleting incoming/outgoing
clan links and deleting the clan. Expected row counts and absence of residual
links catch suppressed writes. Success packets and the existing announcement
occur after commit, using the captured clan ID for registry removal. Offline
members load the saved departure; earned 10304 receipts remain. No fund refund
is introduced, preserving the former disband economy.

Clan relation request handlers share a gate with disband, and relation maps are
concurrent dictionaries so readers cannot fail merely because a link disappears.
The owned revora_clan_ids counter advances past known durable/live IDs at founding
and past the retired ID during disband. A committed ID cannot be reused after
its clan is deleted. This prevents old clan references becoming a new clan's
identity. Allocation changes and disband floor updates are transactional.

Build and 92 clan checks pass. Added tests cover actual NPC request/confirm/cancel,
concurrent confirmation replay, online/offline cleanup, retained achievement
credit, bidirectional relation cleanup, interrupted relation enumeration, retired
highest-ID nonreuse, invalid member/distance/expiry/replacement/durable-rank
requests, suppressed member/clan/relation writes, and reload after rollback.

A related Clan War boundary previously included clanless living players in its
winner snapshot, paid them, then dereferenced their absent clan. It now ignores
closed callbacks, filters current clan members, and rechecks the expected winning
Clan and map under each recipient's owner lock before payment. Two regression
checks verify no departed-member payout/exception and no closed-event payout.
This is not complete event-result persistence: CP/RVP group reward atomicity,
occupation/result durability, event/departure races and native tournament
acceptance remain open. No territory achievement is awarded by this repair.

Remaining acceptance includes leadership transfer, actual login/disconnect and
relationship-edit races, database-wait confirmation expiry, fresh-process
postcommit recovery, remaining peers' roster/shared-BP UI, native acceptance and
deployment. The passing registry reload remains an in-process isolated test.


## Leadership transfer checkpoint (2026-09-25)

The existing leader-issued action 21 now requires an online member of the same
canonical clan. Sorted session leases and online owner locks protect all affected
members; a transaction locks saved owners and clan authority, verifies an unchanged
member set, and saves both the clan leader identity and every member rank. A
suppressed write rolls back the entire transfer. Legacy null donation values are
read as zero, consistently with the existing roster loader.

The new leader becomes rank 100, members whose saved spouse name matches the new
leader become rank 11, and everyone else becomes rank 10. This preserves the
existing name-based spouse rule; it does not introduce or validate marriage rules.
After commit, online ranks and roster packets update and previous membership and
disband offers are invalidated, including transfer-away-and-back sequences.

Build and 107 focused clan checks pass. Added checks exercise ordinary action-21
transfer, offline spouse-name fixtures, character and production registry reload,
old-leader replay, stale invitations/disband confirmation, unauthorized recipients
and saved authority, suppressed clan/rank writes, and competing transfers. The
concurrent test starts two handler calls; it does not prove every interleaving.
No new achievement, currency or event outcome is awarded by leadership transfer.

This supersedes the earlier pending implementation of leadership transfer. Actual
login/disconnect and relationship races, native packet/UI acceptance, fresh-process
recovery and deployment remain pending. Clan War results, atomic event rewards and
territory earning are still separate incomplete roadmap work.


## Retained founder across fresh processes (2026-10-01)

The ordinary retained career (UID 4294907001) was clanless and guildless before
this run. `career-clan-earn` submitted the registrar 9450 name-input packet through
PacketHandler, creating CareerKeep and earning 10304 from `clan-founder-v1`.
Repeated submission charged exactly one configured founding fee; CPs, RVPs and
all saved item rows were unchanged. No clan or achievement was injected.

`career-clan-reload` started a fresh composed game process and recovered the
production clan registry, founder membership and leader rank. The saved clan row,
receipt, currencies and item rows matched the post-founding checkpoint exactly.
Repeating the NPC submission after restart did not charge or grant again.
Both phases passed against private game SHA-256
`B08AF6399258E9F2A07BC8557018FEBA5F7EF3D1A78208D4C67738BB12C5FCA6`.

This closes ordinary successful founding and fresh-process persistence for the
retained career. It does not test a crash between commit and live projection,
network login, native clan UI, or territory event results. NPC proximity was
controlled in the fixture. No live deployment or client publication occurred.


## Fresh career founding and cold membership (2026-10-03)

Main fresh career UID1096322, now level128, founds FreshKeep through registrar9450
and the sealed2032 name-input handler. The configured100,000-silver fee is
charged exactly once; CPs, RVPs and every item row remain unchanged. Clan601
includes the founder and writes one10304 clan-founder-v1 receipt keyed to601.
Cold loading preserves membership before the next guild-founding phase. After
that phase, a further restart and repeated clan/guild submissions preserve exact
organizations, purses, items and the nested prior career. No membership or fee
is supplied directly.

`tools/CheckCandidateFreshAffiliations.cs` passes
`career-fresh-affiliations-clan`, `career-fresh-affiliations-guild` and
`career-fresh-affiliations-reload` on their first run. Latest checkpoint is
`revora_fresh_affiliations_probe`, stage `guild`, nesting all prior Ancient,
contributor, quest and reward snapshots plus exact affiliation rows/counter.
Candidate SHA-256 remains
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
NPC proximity is controlled; this is ordinary handler earning, not native UI
acceptance, a new concurrency/fault suite or live deployment. The preceding
post-Ancient receipt audit is historical after these legitimate mutations.
