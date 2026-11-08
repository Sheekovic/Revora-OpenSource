# Paired movement earning checkpoint

Latest: 2026-09-27. Achievement 10130 now has a validated server earning route;
native acceptance and deployment remain pending. Earlier sections retain their
historical review scope (initial base `4466e7f`).
The hash-bound 6711 achievement catalog requires walking hand in hand with the
opposite sex. A pending invitation or a client-supplied interaction flag cannot
prove that accepted movement happened.

## Cancellation admission repair

Existing `Handle.InteractionStopEffect` and `InteractionRefuse` used a supplied
UID to clear another character's state. Stop also teleported that character to
its current location. Neither required a reciprocal pair. Both now use
`WithInteractionPair`: distinct healthy canonical players, exact actor entity,
non-companion participants, reciprocal partner UIDs, matching map IDs and map
object, and at most 18 tiles apart. The 18-tile reach is a Revora admission bound,
not a verified original interaction distance.

Both session leases and owner locks are acquired in UID order without waiting
for a contested lock. Revalidation occurs inside those locks. Invalid requests
leave both participants untouched; valid stop/refuse clears both the pairing and
active-effect state. Existing response serialization is retained.

Isolated build and **91 appearance checks** pass, including 18 cancellation
checks: unrelated, one-way, self, distant, cross-map, fake, dead and substituted
actor cases for both operations, and successful ordinary reciprocal stop/refuse.
Logs: `obj/interaction-cancel-build.log`, `obj/interaction-cancel-tests.log`.
Fixture prestige warnings about absent ranking definitions do not certify prestige
behavior; these checks concern cancellation only. No native client was exercised.

## Remaining earning contract

The request handler currently writes mutual partner UIDs before acceptance.
Those legacy fields alone do not prove the target consented. Add a bounded offer
owned by the actual recipient and bound to both current sessions, with expiry,
acceptance replay protection, replacement and disconnect cleanup.

`GeneralData` has legacy walk/jump follower updates gated on interaction flags
and bodies 1006/2005. They are not yet an authoritative paired-movement receipt:
validate both current participants, the same map instance, legal destination and
accepted mutual consent before moving a follower. Confirm which interaction mode
is native hand-holding rather than a different paired effect. Do not infer this
from `TwoMovements` packet 1114 alone; no reviewed inbound route was established.

Only an accepted ordinary movement should save both players' achievement evidence,
with persistence-failure handling and repeat/concurrent movement tests. Opposite-sex
eligibility must use the reviewed body classification. Cancellation guards are a
prerequisite, not completion of consent, movement, native acceptance or 10130.
Source coverage remains 320/337.

## Recipient-owned consent checkpoint

Follow-up on `804fb55`: request and accept now share the ordered, nonblocking
session/owner guard. A request cannot replace an interaction already in progress.
It creates one two-minute pending offer on the recipient, referencing both exact
sessions and entity objects. Only that recipient can consume it; expired,
replaced, cancelled, distant, cross-map or stale-session acceptance is rejected.
Cancellation removes pending and accepted proof. Effect activation requires the
shared accepted pair in addition to legacy interaction flags. It resolves its
partner from server state, preserving the existing effect route's independence
from the packet's supplied target UID.

Build and **115 appearance checks** pass, including 24 additional consent checks.
They cover pending state, inviter self-acceptance rejection, seven acceptance
scenarios, consumed-offer replay, accepted effects and rejection when legacy flags
are forged after cancellation. Logs: `obj/interaction-consent-build.log` and
`obj/interaction-consent-tests.log`. These invoke production handlers with ordinary
fixture sessions; they are not native UI or movement acceptance tests.

Review also found that `Attack.Damage` and `ResponseDamage` both access
`Buffer.Damage`, despite distinct fields in the declared protobuf class. This
batch deliberately preserves the wire serializer and accessors. The resulting
interaction mode must not be treated as verified hand-holding evidence. Resolve
that alias with approved-client evidence before emitting 10130 or changing shared
combat packet semantics.

Explicit disconnect cleanup for an already accepted partner remains open. A
stale pending invitation cannot be accepted by a replacement session, and offers
are weakly keyed with one slot per recipient, but that does not prove immediate
cleanup of the remaining player's active interaction state. Validated paired
movement, concurrency stress, native acceptance and the earning receipt remain
unfinished. No new achievement coverage or live deployment is claimed.

## Session termination and follower admission

Follow-up on `c978433`: invitations are indexed by both issuer and recipient,
with a shared closed flag also used by accepted pairs. `GameState.DisconnectOwned`
closes only that exact session's proof before its other callbacks. It never takes
the partner's locks. A late old-session callback cannot close a replacement
session's accepted pair merely because their character UIDs match.

Before a new interaction operation, its locked owners refresh local state. Closed
or expired invitations and accepted pairs with stale participants/map identity are
cleared. This lets the remaining player issue a new invitation without relogging.
The shared proof is invalid immediately on disconnect; peer-side legacy fields
are cleared lazily at the next interaction, not through an unverified client packet.
Weakly keyed state remains bounded to one pending/issued/accepted entry per session.

`HasAcceptedInteraction` checks live canonical participants, exact accepted proof,
reciprocal UIDs, map identity and a 20-tile bound. Existing follower jump, walk and
join-animation branches in `GeneralData` use it before their peer-side operations.
This rejects stale consent and cross-map followers; it does not turn those legacy
branches into a fully locked, validated movement transaction. Concurrent movement,
terrain admission, native mode identity and the achievement receipt remain open.

Build and **139 appearance checks** pass. Twenty-four new assertions exercise real
session disconnect from either participant with both pending and accepted offers,
invalidated movement proof, reinvitation without relogging, replacement-session
acceptance, late old-UID cleanup and cross-map rejection. These test the shared
admission predicate and lifecycle directly; they do not certify full walk/jump
packet execution. Logs: `obj/interaction-disconnect-build.log` and
`obj/interaction-disconnect-tests.log`. No live deployment or additional earning
coverage is claimed.

## Owned follower movement execution

Follow-up on `a03c522`: `MoveInteractionFollower` now performs peer mutations
inside the ordered nonblocking session leases and owner locks. It revalidates
accepted consent, active effect state, the existing moving-body gate (1006/2005),
matching maps, exact leader destination and the follower's original shared tile.
A separated follower is not snapped across the map. Terrain, range, race movement
permission, stun state and the applicable warrior stance restriction are checked.
Walk/run permit one cell, slide two, and jump twenty, matching the existing
leader movement distances. Unknown ground movement modes are rejected.

Accepted follower movement updates its previous/current coordinates and facing,
then runs the ordinary travel-arrival hook and existing packet publication.
Facing is derived before overwriting the old location. Slide followers now reach
the same two-cell destination as their leader rather than taking a single step.
A failure during mutation/publication requires both participants to reload.
Primary leader movement still uses its existing handler and persistence path;
this is not an atomic two-character database movement transaction.

Build and **156 appearance checks** pass, including 17 new checks covering the
actual `GeneralData` walk/run/slide/jump handlers, legal terrain, nine rejected
follower states, concurrent replay and a deliberately held peer lock. Twelve
concurrent follower attempts move/publish once from the same starting tile.
A contended lock rejects mutation without blocking. Logs:
`obj/interaction-movement-build.log`, `obj/interaction-movement-tests.log`.

Nonblocking rejection may leave participants separated; automatic resynchronizing
or cancelling the pair after such a skipped update is not yet validated. Native
hand-holding mode identity remains unresolved because of the shared packet
accessor documented above. Achievement 10130 is still disabled, and no native
client acceptance, deployment or added source-coverage claim is made.

## Recovery after a skipped follower update

Follow-up on `6076256`: a participant initiating a new invitation may replace
that participant's separated accepted pair. Only the initiator gets this recovery
permission; an unsolicited request cannot dissolve the recipient's still-active
pair. The old shared proof closes, each participant clears closed state during
owned refresh, and a new recipient-owned invitation is required. Recovery never
moves either character automatically.

Build and **160 appearance checks** pass. Four new assertions continue the real
peer-lock-contention fixture: an outsider cannot break the separated pair; the
participant can reinvite without relogging or auto-acceptance; the follower walks
to the shared tile while consent is pending; new acceptance restores production
paired walking. Logs: `obj/interaction-recovery-build.log` and
`obj/interaction-recovery-tests.log`.

This resolves the documented reinvitation recovery gap. It does not establish
which native interaction mode means hand-holding, repair the shared packet-field
alias, or certify a real client animation. Achievement 10130 remains disabled
pending that evidence. Source coverage and deployment state are unchanged.

## Approved-client interaction constructor (2026-09-26)

Read-only static inspection of approved DX9 SHA-256
`3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60`
now identifies constructor VA `0x8BB1F7`. Its sixth argument is stored at native
message offset `0x60` with presence mask `0x4000`; its seventh argument is stored
separately at `0x84` with mask `0x800000`. Request caller `0x72442A` passes type
46, an effect code and an additional UI-owned value. Accept caller `0x7A3DE6`
passes type 47; refusal caller `0x7A3EFC` passes type 48. Both observed latter
callers put zero in the seventh argument. These are constructor facts, not proof
of server protobuf tag meanings or a working native conversation.

The request-side branch at `0x724303` maps UI selectors 24/25/26/27/28/29/30/43
to effect codes 100000/100001/100002/100003/100004/100005/100007/100010. None is
labelled hand-holding by the inspected evidence. The two masks suggest distinct
field ordinals 15 and 24, consistent with current server message slots, but the
serializer must still be traced before declaring that mapping verified.

`python tools/audit_paired_interaction_native.py` passes **33 exact instruction
checks** with executable hash/architecture/base gating. It reads one approved
binary and emits only numeric facts; it does not execute or copy client content.

This narrows the next investigation to the UI selector labels and message
serializer. Do not blindly change `Attack.ResponseDamage` from its current
Damage alias to protobuf field 12: the constructor evidence does not establish
that field 12 is either observed interaction argument. No accessor, wire behavior
or achievement producer is changed. Achievement 10130, source coverage and native
acceptance remain pending. Follow-up should inspect the `0x822B60/0x822B64` UI
state writers and serializer for the constructor's message class, then test the
proven mapping through the existing consent/movement fixtures.

## Verified serializer fields and dance selector (2026-09-27)

The approved binary's message serializer at `0x8BA476` writes native offset
`0x60` using field number 15 (`0x8BA5D6` / `0x8BA5D9`), and offset `0x84` using
field number 24 (`0x8BA6AC` / `0x8BA6B2`). Both call `0xAB7CC0`, which writes a
wire-type-zero tag then its value. Tag construction at `0xAB86B6` shifts the field
number by three bits before OR-ing the wire type. The corresponding parser
checks tags `0x78` and `0xC0`, then loads offsets `0x60` and `0x84`. This confirms
the field mapping independently of the constructor's presence-bit inference.
The separate offset `0x54` serializes as field 12; it is not either argument.

Current server `CMsgInteract.Damage` is field 15 and `unkown14` is field 24.
The `ResponseDamage` property aliases Damage, while its similarly named DTO member
is field 12. Keep these distinct when repairing interactions: moving the property
to field 12 would not recover the second constructor argument. No getter or
interaction behavior is changed in this research batch.

UI selector 43 is set at caller `0x728E20`. The following branch references
`STR_DOUBLEDANCE_CURSORTIP_MALE`, whose installed localization prompts selection
of a dance partner. This links selector 43 / effect 100010 to a double-dance UI
route, not to verified hand-holding. The extra selector argument comes from its
UI state and is serialized as field 24; its complete semantics remain open.
Localization provenance: `6711/ini/Cn_Res.ini` SHA-256
`de05f60546fe635b9d3a37c5134761ddf27d5c21851058cac2e65e585ef76599`.

The reproducible audit now passes **61 instruction checks**, plus exact resource
key/localization/hash checks. The former 33 checks are included, not additive.
Only one approved executable and the localization input are read; nothing is
executed or installed. Remaining work: trace selectors 24-30 and the incoming
interaction processing path to a reviewed hand-holding action, then join it to
accepted opposite-sex movement and durable 10130 evidence. Current native accept
and refusal callers still use zero for field 24; do not invent a changed exchange.
Achievement coverage remains unchanged and native acceptance is pending.

## Hand-holding earning route (2026-09-27)

Approved DX9 effect **100004**, UI selector 28, is hand-holding. Dialog 360's
control 2436 binds callback 0x4FF343, which selects 100004; its installed GUI
label identifies hand-holding. The separate effect-indexed icon branch selects
`Mutual_HoldHandPic`. `tools/audit_paired_interaction_native.py` passes **70 exact
instruction checks**, plus command-map, icon-switch and hash-bound resource
checks. See the protocol research entry for addresses and input hashes. These
are static facts, not proof that the native animation renders correctly.

The accepted offer now captures the requested effect independently of later
packets/session effect fields. An accepted 100004 pair with opposite-sex ordinary
body identities can lead ground movement with either participant. This removes
the legacy 1006/2005-only restriction for that specific mode. Existing session,
consent, owner-lock, map, tile, movement and follower-state guards still apply.
Unknown body identities, same-sex pairs and other effects do not qualify.

After a successful follower walk/run, both owners receive 10130 evidence in one
transaction, with entity row locks in UID order and saved-appearance preimage
checks. The evidence primary key prevents duplicate completion. A successful
pair remembers its receipt to avoid a database transaction on every step. Both
achievement/title views refresh after commit. Failure fences both sessions;
there is no partial achievement award. Legacy coordinate writes and network
publication precede this receipt transaction and are **not** atomic with it.
Jump/slide alone, invitations, unaccepted/revoked pairs, dance/hug and altered
session-effect values do not earn the achievement.

Validation: isolated server build, **186 appearance/interaction checks** (160
existing plus 26 earning checks), and the native static audit pass. Coverage
includes ordinary male/female leaders, acceptance without movement, immediate
achievement views, title-login recovery from empty legacy bits, subsequent and
concurrent replays, nonqualifying modes, separation/death, second-owner receipt
failure rolling both back, and stale persisted appearance. The fixture now loads
installed Prestige/wardrobe definitions for the new refresh path; it does not
substitute fabricated ranking values. Logs: `obj/handholding-build.log` and
`obj/handholding-tests.log`. This is server-handler/disposable-schema evidence,
not a full native login, animation or production deployment test.

Source inventory is now **322/337**, with **15** missing producers. That count
is a source audit, not an overall completion percentage or proof that one player
can earn every achievement. Native hand-holding acceptance remains pending.

## Retained couple hand-holding (2026-10-02)

The retained female main and existing male spouse4294908901 use the normal
interaction request and acceptance handlers with hand-holding effect100004.
Acceptance alone produces no10130 receipt. In an installed walkable Twin City
corridor, the ordinary ground movement handler moves both characters one tile
and credits both; a second paired step produces no duplicate receipt. Full game
and database restart preserves the exact nested career and both achievements.
Replayed acceptance after restart creates no interaction and changes no snapshot.

`career-retained-hands-{earn,reload}` pass on candidate SHA-256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`;
`revora_retained_hands_probe` finishes at step1. This uses existing earned gender
and marriage state, with controlled starting proximity and ordinary non-GM fixture
sessions. Native interaction UI and authenticated packet acceptance remain pending.
No production binary or live deployment changed.
