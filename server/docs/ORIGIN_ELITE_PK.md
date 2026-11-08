# Origin-qualified cross-server Elite PK awards

Current local earning edition (2026-09-30): see
[local tournament wardrobe](LOCAL_TOURNAMENT_WARDROBE.md). The origin-qualified
contract below remains separate; its historical statement that local competition
cannot award these keys is superseded by the owner's approved local edition.

This is a **local Revora edition** of the four level-130 cross-server Elite PK
wardrobe titles. It uses the M9 origin tournament substrate: a finalized
`cs-epk-v1` run with at least two origin realms. Placements 1, 2, 3 and 4–8
receive Legendary, Peerless, Outstanding and Expert. Local Elite PK still
grants only Infernal Beast.

Official wardrobe text ties these four keys to cross-server Elite PK standing
in the level-130 division. This edition does not simulate live duels or a
second Conquer listener. Two maps in one realm are not a cross-server win.

## Durable contract (2026-09-22)

- Event key `cs-epk-v1`. Other tournament events cannot capture these awards.
- Capture requires the run coordinator and a finalized two-origin roster.
- Placement 1 stores Legendary `20352035` (500 points).
- Placement 2 stores Peerless `20362036` (450 points).
- Placement 3 stores Outstanding `20372037` (400 points).
- Placements 4–8 store Expert `20382038` (300 points). Placement 1 does not
  also receive Expert.
- Each award is stored for the placed `(realm, uid)`. Capture records the
  enrolled owner; delivery authenticates the current live session owner.
- Materialization authenticates that origin's current live session owner.
  Pending awards are keyed by `(realm, uid)`, so a later process owner after
  restart can deliver them. Wardrobe refresh without that bind leaves the
  rows pending. The same numeric uid on another origin cannot consume or
  display the grants. Snapshot selection filters CS Elite PK rows by realm
  before choosing the latest title.
- Source `cs-epk-revora-v1`, source key `realm:run:title`.
- Production login/reconnect/logout call `OriginBind`, which authenticates
  this server's origin for wardrobe refresh. Live inter-realm battle and
  native acceptance remain pending, so the 72-key audit stays predicate-only.
- Replay, concurrent delivery and a failed grant stay single-write.

## Lifetime (Revora edition)

The October 2016 Elite PK Q&A confirms cross-server level divisions and
rewards, but describes a fourteen-day **halo**. That is not wardrobe-title
expiry. Later public Elite PK pages that list 30-day placement titles are a
later edition.

This Revora edition stores all four keys for fourteen days from finalize
(`AwardRule` days=14, `expires_at` = earned + 14 days). That matches the
already-encoded CS Team PK title contract and the 2016 event window length.
It is the local edition lifetime, not an official-page 6711 title duration
claim. Halo projection remains out of this slice.

Native socket login, Host restart, a live inter-realm Elite PK battle and
deployment remain pending.
