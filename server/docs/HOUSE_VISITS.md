# House visits

Source implementation, 2026-09-20. Not deployed or accepted in the genuine client.

Ordinary players can use House Admin (1002, 331,536), select **House visits**,
and invite an online mutual friend. Both players must speak to House Admin and
remain within six paces, alive and outside trade, booth and poker activity.
The recipient reviews the named host and accepts or declines. Invitations expire
after two minutes; the host can withdraw outstanding invitations. Visits are free.
The friend list has six entries per page, capped at fifty eligible nearby friends.

An accepted guest enters the owner's saved house at any of its five levels.
Guests can walk and leave through its installed exit. They cannot furnish the
house, remove furniture, or open, deposit into or withdraw from the owner's boxes.
Leaving ends entry permission; reconnecting inside a foreign house returns the
character to Twin City. Withdrawal affects pending invitations, not admitted guests.

Achievement 10131 belongs to the host and 10132 to the visitor. Merely inviting,
reviewing or declining earns neither. The installed Achievement.dat identifies
these requirements (SHA-256
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`).
The NPC consent interaction and guest access policy are explicit Revora decisions;
original retail guest UI/protocol parity has not been established.

## Persistence and ownership

Housing startup initializes `revora_house_visits` and a narrowly scoped trigger
on character map writes. Consent locks both owners in UID order and checks saved
mutual friendship, house ownership, level, map and both saved city locations.
The arrival receipt commits in the same transaction as the character's map write.
Only a committed arrival earns the two role-specific achievements. Login recovers
missing achievement projections from that receipt after a failed write.

Invitations bind canonical sessions and entity instances. Acceptance requires a
reviewed, unexpired invitation; repeated acceptance cannot produce another arrival.
A separate session entry permit grants travel only, leaving furniture and storage
ownership checks intact. Failed persistence fences affected sessions for reload.
Existing house renovation checks reject occupied houses.

## Validation and release limits

The isolated build and 101 house-visit checks pass. Coverage includes all five
house levels, concurrent acceptance, expiry, withdrawal, decline, friendship
removal, session replacement, forged NPC replies, consent/arrival/map/achievement
write failures, reconnect recovery and foreign item-box access. Malformed transport
frames are tested against the component validator; the validly framed bad-input
case goes through the actual dispatcher. These are not genuine-client tests.

Related regressions pass: 444 housing/storage, 43 friendship and 91 title checks.

The achievement audit now finds 236 of 337 IDs with source producers; this does
not establish an attainable per-character route to 320 or an overall completion
percentage. Three producer-audit tests pass.

Before live release, validate the short NPC menus, named consent, house entry,
movement/exit and achievement display on genuine 6711 using ordinary accounts.
Review and back up the production schema before initialization. No production DDL,
server restart, client modification or public patch was performed in this batch.
