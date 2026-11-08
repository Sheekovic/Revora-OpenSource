# Mentor admission and role achievements

2026-09-15, M6.33, based on `ba2c13e`. Admission is implemented and tested in
isolation. This does not complete departure, contribution claims or BP sharing.
Nothing in this milestone is deployed or accepted in the genuine 6711 client.

Both invitation directions now require owned, expiring mutual consent. The saved
relationship, admission receipt and distinct achievements commit together:
10124 for the mentor and 10125 for the apprentice. Sending an invitation, replaying
an old response or loading historical relationships does not create evidence.
The old handlers accepted unsolicited responses and projected membership before
the database insert; their unguarded insertion API is removed.

## Rules and evidence

The [official mentor guide](https://co.99.com/guide/guides/mentors.shtml), checked
2026-09-15, gives the same-rebirth level gaps of 30, 20 and 10. A higher-rebirth
mentor may accept an earlier-rebirth apprentice. Capacity by mentor level is:
70–109: two; 110–119: three; 120–129: five; 130–133: seven; 134+: ten. The guide
retains pre-rebirth capacity; Revora uses current and applicable stored first/
second pre-rebirth levels. Missing historical levels are not invented.

The installed achievement catalog maps 10124/10125 to adding an apprentice/mentor.
`6711/ini/Achievement.dat` SHA-256:
`DF78DD498353B0B089F9B4228CADA5D79F4547AAE7B705349816671B2C29EBFC`.
The [protocol record](PROTOCOL_RESEARCH.md) documents inherited 2065 action/UID
semantics and 2066 display layouts without claiming native capture verification.

Requests bind the exact sender and receiver sessions and entity objects. Revora
policy bounds them to two minutes, sixteen incoming invitations and one outgoing
request per second. Direction and explicit acceptance must match the invitation.
Both players must be ordinary authenticated, fully loaded, alive and nearby on
the same map. Expiry and eligibility are rechecked after database owner locks.
There is no verified unique wire nonce distinguishing an arbitrarily delayed
identical response from one against a later fresh invitation.

Lock order is both character owners by UID, then `MentorExperience.Sync`, then
database entity rows by UID. This respects existing EXP/claim ownership. Admission
requires saved level/rebirth facts to match the current players; it does not
claim completion from an unsaved or stale eligibility snapshot. Existing mentors,
full saved capacity and ancestry cycles prevent admission. Duplicate apprentice
identities affecting the mentor or its existing members require reviewed repair.

The new membership starts all contribution fields at zero. Existing memberships
and balances are never overwritten by admission. Relationship, receipt and both
achievement rows are checked before commit; failed or silently skipped writes
roll back and fence both sessions for reload. Cache projection rebuilds both
roles through the production loader, sends both information records even with
zero shared BP, and refreshes achievements/titles after commit.

Display construction now tolerates an absent spouse and bounds encoded names.
MentorInformation assembles its inherited spaced name fields at serialization,
so setter order cannot overlap them. ApprenticeInformation reserves room for all
three bounded names plus the suffix, growing its old allocation only as needed.
These are source-layout corrections; native display acceptance remains pending.

## Validation and unfinished work

The read-only public audit was repeated: 591 relationship rows, 49 duplicated
apprentice identities, and no null identity/negative EXP rows in its checked
predicate. No historical row was changed. The ignored x64 inspector was hash-
verified before execution:
`5EAD5B330978729599084D11DD430DB08E7221703CADF29067759AD44DA7FAC1`.
Only schema and aggregate facts were output.

55 final admission checks cover all capacity boundaries, retained rebirth levels,
rebirth ordering, both invitation directions, distinct role achievements,
contribution preservation, malformed packets, bounded display records, declines,
expiry after database waits, reconnects, competing mentors, stale eligibility,
duplicates and transaction fault/recovery cases. Related regressions passed:
452 EXP, 43 friend, 51 partner, 74 progression, 91 title, 89 auction and 16 audit
checks, before the final admission-only ambiguity guard. 871 checks in this batch.
Final isolated `Revora.exe` SHA-256:
`46A4862B80F28B2469FBCB3B7AD8985CEEE708BE926D4B6F33983BF14D441A96`.

Admission receipts record history; they are not an active-membership substitute.
The legacy departure path still deletes contribution rows immediately. It does
not implement the guide's fee/reset interval or retained-reward recovery. That
path must be replaced with an owned departure transaction and claimable retained
balances before the mentor system can be called complete. The guide states a
50,000-silver apprentice departure fee and a three-day relationship reset; their
precise active/delayed state and native presentation need explicit implementation.
Blessing/+1 source and claim transactions, BP sharing and the historical duplicate
remediation remain open as documented in [mentor EXP](MENTOR_EXPERIENCE_TRANSACTIONS.md).

No production schema, running binary, client content or public release changed.
Before deployment, preserve database/binary rollback and complete the remaining
mentor lifecycle plus ordinary genuine 6711 invitation, display, earning and
reconnect acceptance. Producer coverage is **101/337**; 236 producer identities
remain missing. A per-character 320-achievement route is still unproven.

Follow-up: [M6.34](MENTOR_DEPARTURE.md) replaces immediate deletion with retained
contributions, fee delivery and the admission cooldown, and restores retained EXP
claims. Blessing/+1 claims and the native lifecycle are still unfinished.

## Retained ordinary mentorship follow-up (2026-10-02)

The same retained career now has normal mutual admission, team-earned apprentice
EXP and mentor Virtue, Shelby spending, replay refusal and full-process persistence.
See [the retained route](VIRTUE_REWARD_EARNING.md#retained-mentorship-team-training-and-shelby-exchange-2026-10-02).
The main character earns10124; its saved trainee earns10125. This does not yet
prove the main character's own apprentice route or native acceptance.

## Retained apprentice admission after paid reincarnation (2026-10-02)

The same retained main character now earns10125. The existing ordinary senior
4294903001 (ArenaProbe0, level140, twice reborn) cannot admit this main at level140.
Sereth's normal dialogue sells Oblivion Dew for2,000CP and confirms the configured
paid reincarnation into Water132, level15. The fixture now chooses the character's
current body suffix in the sealed1066 request: female2001 stays female2001.
It previously hardcoded the male choice3. No level, profession, body, relationship
or achievement is assigned directly to obtain admission.

After normal Stone City storage frees one bag slot, the purchase and rite each
save a checkpoint. A sealed2065 RequestMentor from the main and the senior's
AcceptRequestMentor establish the relationship. The main retains its existing
trainee4294908501, creating a valid three-character chain. A full game/database
restart preserves the exact nested career snapshot, new admission, senior receipt,
and main10125. Replayed acceptance and reincarnation without a live offer change
nothing. Both `career-retained-apprentice-earn` and
`career-retained-apprentice-reload` pass on private game SHA-256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
Probe `revora_retained_apprentice_probe` finishes at step3.

The main remains Water132, level15, with normal level110 recovery pending; this
is not a claim that recovery was exercised again in this batch. The senior's
starting qualification and main's earlier funding retain their supplied fixture
provenance. NPC proximity is controlled, sessions are ordinary non-GM server
fixtures, and authenticated/native invitation presentation remains pending.
No production binary, live deployment or client release changed.
