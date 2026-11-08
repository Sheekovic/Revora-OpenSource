# Genuine patch-5165 portal correction

## Production evidence boundary

The genuine SHEEKOVIC session reached map 1002 position `(401,387)` and then
Revora closed the Game connection with `protocol-validation`. No decoded action
audit, portal-selection audit, database transition, action 86, packet 1110, or
NPC packet occurred. PostgreSQL retained map 1002 position `(401,387)`.

This places the failure before `GameplayWorld5165.UsePortalAsync`, inside the
strict action-frame parser. The server, rather than the client, initiated the
close after the parser raised `InvalidDataException`.

## Exact-client evidence

For approved client SHA-256
`902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`, the
action-85 caller at `0x518082` invokes the exact 32-byte special-action builder
at `0x54da8c`. Its arguments produce:

| Offset | Width | Meaning |
| --- | ---: | --- |
| 0 | 2 | declared length 32 |
| 2 | 2 | packet ID 10010 |
| 4 | 4 | character UID |
| 8 | 2 | authoritative/source X |
| 10 | 2 | authoritative/source Y |
| 12 | 4 | timestamp |
| 16 | 2 | action 85 |
| 18 | 2 | direction |
| 20 | 2 | portal-cell X selected by the client |
| 22 | 2 | portal-cell Y selected by the client |
| 24 | 4 | zero |
| 28 | 4 | `0xffffffff` sentinel |

The same constructor/sentinel is used for action 137. Revora previously
accepted it only for action 137 and incorrectly required an all-zero tail for
action 85.

The exact-client action-86 receive branch at `0x54e2c9` verifies the character
UID, consumes the 32-bit map-document identity at offset 8, and consumes the
coordinates at offsets 20 and 22. Revora therefore retains its bounded order:
action 86, packet 1110, new-map visibility, then nearby NPC visibility. Cipher
state remains continuous on the existing Game connection.

The owner-provided 5165 source corroborates the server order in
`Game/Character.cs::Teleport`: remove old visibility, send action 86 using the
map/document identity, send map status, and then populate the new screen. It is
supporting evidence only; the approved exact client remains authoritative.

## Correction scope

Only action 85 joins action 137 in accepting the exact special-constructor
tail `(0, 0xffffffff)`. Generic actions still require an all-zero tail, and a
portal frame with the generic tail is rejected. UID ownership, source-map and
proximity checks, destination map/collision checks, atomic persistence, rate
limits, and cipher framing are unchanged.

## Genuine acceptance and dialog-close follow-up

The owner subsequently proved portal row 73 end to end: action 85 was decoded,
the map-1002 to map-1004 transition committed at `(51,70)`, and movement and
jumping remained connected on map 1004. NPC UID 10000 was visible and its
dialog opened. Closing with the window X remained connected.

Choosing Goodbye produced a genuine accepted `16/2032` option `255`, action
`101`, immediately followed by a server-side `protocol-validation` close. No
action-102 acceptance event was emitted. That first correction was incomplete:
it modeled the following close record as 15 bytes and tested only the direct
parser.

The full exact-client constructor chain resolves the missing byte. Instructions
at `0x41b236`–`0x41b265` call constructor `0x54497a` with option `0`, action
`102`, and an empty packed-string collection. Constructor `0x54497a` calls
serializer `0x599659`; that serializer initializes its result to one at
`0x59965e`–`0x599660` for the collection-count byte and returns one even when
the count is zero. The constructor adds 15 at `0x5449da`, proving declared
length `16`, ID `2032`, option `0`, action `102`, and zero bytes 12–15. The
owner source independently treats packet 2032 as having its packed-string count
at byte 12 and a first string length at byte 13, corroborating that the fixed
region includes this count byte.

That constructor correction remains valid for action 102, but the next genuine
production audit proved it was not the frame selected by this UI path. The
running Host and process image both matched SHA-256
`93878f699338df090601015cc41e3446d16ab63e35fe7703ade02f8ee7d0eaec`.
At both `19:36:53Z` and `19:37:28Z`, the client emitted two complete identical
records: declared `16/2032`, option `255`, action `101`, trailing bytes all zero.
Both records passed decryption, `TQClient` footer validation, declared-length
framing, and packet-ID dispatch. The first reached `Npc2032Accepted` and cleared
the active dialog. The second reached `ParseNpcDialogAnswer`, but no acceptance
event followed because `GameplayWorld5165.SelectNpcDialogOptionAsync` rejected
it with `InvalidDataException` (`No bounded NPC dialog is active.`). Revora then
initiated the close under `protocol-validation`.

The production correction is therefore bounded duplicate-Goodbye idempotency,
not another packet-layout exception. A duplicate option-255/action-101 record is
accepted only when the same session has just completed a bounded dialog close.
Opening another dialog or changing maps resets that replay state. Other options,
actions, inactive-dialog selections, malformed tails, and undersized frames
remain rejected. The exact 16-byte action-102 close remains supported only in
the same bounded close state; the global frame minimum is unchanged.

The regression now drives the production-observed duplicate action-101 sequence
through the real DH/Blowfish Game connection and frame reader. It covers both
records in one socket write, headers and bodies fragmented across writes,
separate delivery, bounded action-102 replay, and authoritative movement after
every closure. The parser-only fixture that encoded 15 bytes was deleted.

The replay allowance is single-use and bound to the parsed close event on the
current Game session. An exact duplicate consumes it; a third duplicate is
rejected. A valid action-102 close confirmation may consume the same allowance
once. Opening another dialog, changing maps, or losing NPC visibility clears it.
NPC activation and answer events additionally share a per-session rolling limit
of 12 events per two seconds. This does not change the genuine two-action-101
client path and does not relax framing, NPC ownership, range, or map checks.
