# Origin-qualified cross-server CTF awards

Current local earning edition (2026-09-30): see
[local tournament wardrobe](LOCAL_TOURNAMENT_WARDROBE.md). The origin-qualified
contract below remains separate; its historical statement that local competition
cannot award these keys is superseded by the owner's approved local edition.

This is a **local Revora edition** of Conqueror and Radiant Wings. It uses the
M9 origin tournament substrate: a finalized `cs-ctf-v1` run with at least two
origin realms. Placement 1 is the champion identity and receives both wardrobe
keys. Local CTF still grants only Victor.

Official wardrobe text ties Conqueror and Radiant Wings to leading the champion
guild in cross-server CTF. This edition does not simulate flag capture or a
second live Conquer listener. Two maps in one realm are not a cross-server win.

## Durable contract (2026-09-22)

- Event key `cs-ctf-v1`. Other tournament events cannot capture these awards.
- Capture requires the run coordinator and a finalized two-origin roster.
- Placement 1 stores Conqueror `20030004` (300 points) and Radiant Wings
  `60020021` (0 points) for the champion `(realm, uid)` and enrolled origin
  owner.
- Placement 2 does not receive either key.
- Materialization authenticates the champion origin's current live session
  owner. Pending awards are keyed by `(realm, uid)`, so a later process owner
  after restart can deliver them. Wardrobe refresh without that bind leaves
  the rows pending. The same numeric uid on another origin cannot consume or
  display the grants. Snapshot selection filters CS-CTF rows by realm before
  choosing the latest title.
- Source `cs-ctf-revora-v1`, source key `realm:run:title`. Pre-upgrade
  `run:title` grants rewrite to that form once during initialize and stay
  visible after restart without a second delivery.
- Production login/reconnect/logout call `OriginBind`, which authenticates
  this server's origin for wardrobe refresh. A later character name for the
  same uid still binds. Live inter-realm battle and native acceptance remain
  pending, so the 72-key audit stays predicate-only.
- Replay, concurrent delivery and a failed grant stay single-write.

## Lifetime (Revora edition)

The official wardrobe guide confirms the CS-CTF leader mapping and Conqueror's
300 title points. Its wings table lists Radiant Wings duration as blank (the
same column marks Moonlight/Starlight Permanent and also leaves Infernal Beast
and Solar Dragon blank). Catalog point value 0 is not a duration.

This Revora edition stores both keys as permanent (`AwardRule` days=0,
`expires_at` NULL). That is the local edition lifetime, not an official-page
duration claim.

Native socket login, Host restart, a live inter-realm CTF battle and deployment
remain pending. Origin-qualified CS Elite PK awards are a separate producer
([ORIGIN_ELITE_PK.md](ORIGIN_ELITE_PK.md)).
