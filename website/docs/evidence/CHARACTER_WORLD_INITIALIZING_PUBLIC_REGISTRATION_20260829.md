# Genuine public-registration world-entry boundary — 2026-08-29

This record contains sanitized production evidence only. It contains no password,
raw packet, handoff token, cryptographic key, owner address, or database secret.

## Correlated identity and persistence

- Launcher report timestamp: `2026-08-29T15:44:55.5179171Z`.
- Exact approved configuration-only client SHA-256: `902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`.
- Client PID reported by the owner: `12320`.
- Immutable AccountId: `cfeabd75-9d13-40a3-b4c3-567fcc1f9ca3`.
- Character UID/name: `2` / `SHEEKOVIC`.
- Character state: mesh `1003`, profession `100`, level `1`, map `1002`, coordinates `430,378`.
- Character commit: `2026-08-29T15:45:29.520496Z`; one row owned by the AccountId.

## Ordered sanitized server timeline

| UTC | Confirmed event |
|---|---|
| 15:45:09.7175041 | Account `276/1086` validated |
| 15:45:10.1512102 | encrypted successful `32/1055` sent |
| 15:45:10.4203010 | Game TCP connection accepted |
| 15:45:12.0718925 | DH/Blowfish complete; `28/1052` and `TQClient` validated; handoff consumed once |
| 15:45:12.0849856 | `NEW_ROLE` sent |
| 15:45:29.5090860 | declared `104/1001` and footer validated |
| 15:45:29.5131990 | registration parsed and validated |
| 15:45:29.5204960 | character committed atomically |
| 15:45:29.5323430 | declared `103/1006` sent on the same cipher/session |
| 15:45:29.6578323 | genuine declared `32/10010` received; former Host mislabeled it non-action |
| 15:45:39.6727947 | Host timed out the unsupported post-1006 path and disposed the session |
| 15:45:46.3854104 | second Account `276/1086` validated |
| 15:45:47.1775148 | second `28/1052` validated; existing character found; no `NEW_ROLE` |
| 15:45:47.1801056 | entrance acknowledgement and declared `103/1006` sent |
| 15:45:47.2754320 | genuine declared `32/10010` received; again mislabeled non-action |
| 15:45:48.2331746 | separately framed declared `304/1134` quest-status request received |
| 15:46:50.6984270 | peer/network I/O ended while no action response had been sent |

## Exact failure and correction

The last mutually accepted gameplay boundary was server `103/1006`, followed by a
valid client `32/10010` action request. Revora required the older `24/1010`
shape and therefore ignored the genuine action-74 request. It sent neither the
authoritative `32/10010` response nor `16/1110`, leaving the client at
`Initializing...`. This was an action-generation mismatch, not a rejected 1006,
failed character commit, cipher failure, invalid map, or client crash.

The corrected server parser/encoder follows exact client builder `0x54d9f8` and
receive branch `0x54e127`. Packet 1134 remains separately classified and is not
guessed into an action response. Deterministic production-path tests now replay
the genuine observed `1006 → 32/10010 → 304/1134` order and the subsequent
75/76/77/78/97 initialization action chain for both same-session creation and
existing-character login.
