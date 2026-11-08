# Portals, Maps, and NPCs

This milestone starts from movement/jump commit
`d41b33ebcd0d1e11d09499de3d7bdb26f825d02d`. The previously accepted walking,
running, jumping, position persistence, and reconnect behavior is retained.

## Reviewed production slice

- Maps: Twin City `1002` (`newplain.DMap`) and Arena/Forum `1004`
  (`forum.DMap`). Both server-side map files are private and exact-hash pinned.
- Portals: five owner-authorized reference rows, comprising the Twin City entrance
  area and the return portal.
- NPCs: five stable owner-source UIDs on map 1004. Two are visible at the reviewed
  entry cell `(51,70)` under the exact 18-cell range rule.
- Dialog: a Revora-owned fixed informational dialog. It performs no script, SQL,
  command, reflection, dynamic compilation, or plugin dispatch.

The imported data provenance is the owner database archive SHA-256
`f5e88d2729306ca6946356a52b3964f9ddb245db9f1e40a0539e0e126ade3d63`
and canonical allowlisted content SHA-256
`16630c8952354e3bf1521e471c990275e83842e8d32ed1cb8de4fb930cec753b`.
V009 creates the restricted reference catalog. V010 imports exactly five portal and
five NPC rows and is safe to rerun without duplicating them.

## Security and persistence

Portal use is bound to the authenticated Character UID, the authoritative current
map, and both the server position and request position being within four cells of a
reviewed portal. The destination map must be loaded and the destination cell must be
accessible; a bounded four-cell search may select the nearest safe cell. Map and
coordinates are committed in one optimistic PostgreSQL update before visibility is
changed.

NPCs are loaded from reviewed PostgreSQL reference rows. A dialog requires the NPC
to be on the authenticated character's map, visible to that session, and within 18
cells. Unknown options and remote/wrong-map activation fail closed.

## Owner acceptance

1. Use the existing approved launcher and log into the existing character normally.
2. In Twin City map 1002, travel to the portal area around `(401,387)` and enter it.
3. Confirm the client enters map 1004 near `(51,70)` and remains connected.
4. Log out normally, log in again, and confirm map 1004 and the saved coordinates
   are restored.
5. Approach a visible NPC, open its dialog, choose the informational option, and
   close the dialog.
6. Continue walking and jumping and confirm the Game connection remains stable.
7. Return the visible result and any sanitized launcher report; do not capture raw
   authentication or Game packets.

## Rollback

Stop `revora-game`, restore only the backed-up Host artifact, remove the separately
installed `forum.DMap` if it was absent before deployment, then start the service and
verify one READY event. V009/V010 are additive reference-data migrations and can
remain dormant under the prior Host. If data rollback is required, delete only the
single documented import `source_id` in one transaction; never alter identities,
characters, UID sequences, or movement state.
