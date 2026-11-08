# SHEEKOVIC map-1002 movement evidence — 2026-08-29

This is the sanitized correlation of the genuine production session under the
approved client SHA-256
`902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`.

## Confirmed ordered boundary

- Account 276/1086 validated at `16:10:51.2920949Z`; 32/1055 sent at
  `16:10:51.5822901Z`.
- Game connection accepted at `16:10:51.7678211Z`; 28/1052 and handoff validated
  at `16:10:51.9265567Z`.
- Character 1006 sent, genuine action 74 received, and map 1002/coordinates
  430,378 sent by `16:10:52.0861908Z`.
- After the normal initialization traffic, the first genuine movement arrived at
  `16:11:35.4453517Z`.
- Revora received **49** valid-framed movement packets through
  `16:11:46.9648439Z`. Each had decrypted declared length 20 and ID 10005.
- Every movement was classified `non-action` and discarded. No authoritative
  movement was echoed and no position was persisted.
- The next receive failed inside bounded framing before a packet event could be
  emitted. The sanitized journal preserved `InvalidDataException` category
  `protocol-validation` at `16:11:47.2080081Z`; the connection scope then disposed
  the server socket. Thus the server initiated the final close after its framing
  rejection. The old logger intentionally did not retain whether the rejected
  unseen frame failed its length or `TQClient` footer check, so that narrower
  distinction is **not recoverable** from this run and is not guessed.
- PostgreSQL remained at UID 2, map 1002, x=430, y=378, direction=0, row_version=1.

## Exact movement layout

Exact-client constructor `0x58198f–0x581a31` and handler
`0x581a57–0x581d5c` establish:

| Offset | Width | Meaning |
|---:|---:|---|
| 0 | 2 | declared length 20, little-endian |
| 2 | 2 | packet ID 10005 |
| 4 | 4 | direction value; client consumes the low byte modulo 8 |
| 8 | 4 | numeric Character UID; handler rejects a foreign UID |
| 12 | 4 | movement mode; client consumes the low byte |
| 16 | 4 | client tick/timestamp |
| 20 | 8 | encrypted-wire footer `TQClient`, excluded from declared length |

The prior sanitized run did not persist the 49 packets' direction/mode/timestamp
values. Those values are therefore not attributed to the owner run. The layout is
confirmed statically, while the observed length/ID/count are confirmed genuine.

## Root cause and correction

The production loop handled only packet 10010 actions. Its explicit `packetId !=
ActionPacketId` branch logged and discarded every valid 10005 movement. This is
the exact implemented defect that prevented authoritative movement and persistence.
The new gameplay slice parses, validates, persists, echoes, and broadcasts 10005;
it handles exact-client action 137 jumps and keeps the connection read loop alive.
