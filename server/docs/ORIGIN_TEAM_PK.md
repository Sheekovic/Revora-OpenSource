# Origin-qualified cross-server Team PK awards

Current local earning edition (2026-09-30): see
[local tournament wardrobe](LOCAL_TOURNAMENT_WARDROBE.md). Ordinary level-130+
regular Team PK now has a separate frozen-roster earning producer. The older
origin-qualified contract below remains isolated; local teams are not represented
as foreign origins.

This is a **local Revora edition** of the four level-130 cross-server Team PK
wardrobe titles. It uses the M9 origin tournament substrate: a finalized
`cs-team-pk-v1` run with at least two origin realms. Each competing side is an
exact five-member `(realm, uid)` roster frozen before start. Team placements
1, 2, 3 and 4–8 award King Team, Dominator Team, Power Team and Elite Team to
**every** frozen roster member.

The dated 2018 Team PK event delivered packs to team leaders. This edition
grants each eligible roster member directly. That delivery improvement is
explicit. Two maps in one realm are not a cross-server win.

## Durable contract (2026-09-22)

- Event key `cs-team-pk-v1`. Other tournament events cannot capture these awards.
- `FormTeam` stores exactly five distinct identities from one origin, each with
  a live session owner, and enrolls the captain. Generic `OriginTournaments.Enroll`
  rejects `cs-team-pk-v1`. After start the roster is frozen.
- Capture requires exactly one placed captain per team, joined through
  `revora_origin_tpk_teams` on `(run, realm, captain)`, then expands that
  frozen roster. A non-captain placement is rejected before any award insert.
- Capture requires the run coordinator and a finalized two-origin roster of
  captains whose teams still have five members.
- Team placement 1 stores King Team `20592059` (500 points).
- Team placement 2 stores Dominator Team `20602060` (450 points).
- Team placement 3 stores Power Team `20612061` (400 points).
- Team placements 4–8 store Elite Team `20622062` (300 points). Placement 1
  does not also receive Elite Team.
- Source `cs-tpk-revora-v1`, source key `realm:run:title`, fourteen days from
  finalize. `team-pk-2018` remains the earlier persistence-fixture source.
- Materialization authenticates the current live session owner. Pending awards
  are keyed by `(realm, uid)`, so a later process owner after restart can
  deliver them. The same numeric uid on another origin cannot consume or
  display the grants.
- Production login/reconnect/logout call `OriginBind`. Live inter-realm battle
  and native acceptance remain pending, so the 72-key audit stays predicate-only.
- Replay, concurrent delivery and a failed grant stay single-write.

## Lifetime (Revora edition)

The 2018 Team PK article documents fourteen-day King/Dominator/Power/Elite
Team titles in the highest division. This Revora edition stores those four
keys for fourteen days from finalize (`AwardRule` days=14). That is the local
edition lifetime matching that dated contract.

Native socket login, Host restart, a live inter-realm Team PK battle and
deployment remain pending.
