# Character creation and world-initialization evidence — 2026-08-29

## Classification

Two owner sessions used the approved configuration-only client SHA-256
`902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`.
The Windows reports identify PID 17480 at `2026-08-29T13:18:27.8409933Z`
and PID 14212 at `2026-08-29T13:20:01.8621288Z`. Remote TCP cannot
carry a Windows PID, so these PIDs are correlated to the sanitized server
sessions by the report timestamps and their unique ordered Account/Game
events, not represented as server-side process attribution.

Attempt 1 conclusively created `SHEEKO`. Attempt 2 found that character and
did not send `NEW_ROLE`, but remained at `Initializing`. `SHEEKO2` belongs to
an earlier failed attempt and is not evidence for these sessions.

## PostgreSQL proof

The read-only post-run query found exactly one character owned by canonical
account `vpsowner`: legacy UID 1, name `SHEEKO`, mesh 1003, profession 100,
level 1, hair 410, map 1002, coordinates 430,378, created at
`2026-08-29T13:19:05.329584Z`. There are no duplicate legacy UIDs,
case-insensitive duplicate names, orphan characters, open owner sessions, or
active handoffs. The single successful `INSERT ... RETURNING` statement is an
atomic PostgreSQL transaction. No separate durable character-creation audit
table exists; the sanitized `CharacterCreated` service audit is the durable
operational evidence of the decision.

## Attempt 1 timeline

| UTC | Sanitized event |
|---|---|
| 13:18:47.4133481 | genuine encrypted 276/1086 validated |
| 13:18:47.7910126 | encrypted 32/1055 sent |
| 13:18:48.0500845 | Game connection accepted |
| 13:18:49.6360692 | DH/Blowfish complete; 28/1052 and `TQClient` validated; handoff consumed |
| 13:18:49.6461356 | `NEW_ROLE` sent |
| 13:19:05.3217549 | 112 encrypted bytes yielded declared 104/1001 with valid footer |
| 13:19:05.3234568 | registration frame validated |
| 13:19:05.3261936 | zero Account accepted; name valid; reserved empty/accepted; mesh 1003 valid; profession 100 valid; Account word matched |
| 13:19:05.3295840 | PostgreSQL allocated legacy UID 1 and committed `SHEEKO` |
| 13:19:05.3347729 | encrypted 1004/2100 `ANSWER_OK` write completed; `CharacterCreated` audited |

The registration handler then returned and the server disposed that Game
connection. This was a Revora-implemented reconnect assumption and accounts
for the client-visible disconnect; it was not proven client-required. Later
exact-client analysis showed that the `ANSWER_OK` handler continues the same
Game/loading state. Windows Emergency Stop happened later and did not stop or
restart the VPS service.

## Attempt 2 timeline and last accepted boundary

| UTC | Sanitized event |
|---|---|
| 13:20:14.1988172 | genuine encrypted 276/1086 validated |
| 13:20:14.5168185 | encrypted 32/1055 sent |
| 13:20:14.6831358 | Game connection accepted |
| 13:20:16.2754056 | DH/Blowfish complete; 28/1052 and `TQClient` validated; handoff consumed |
| 13:20:16.2805703 | existing `SHEEKO` selected; entrance 1004/2101 and the then-current 1006 were written; no `NEW_ROLE` |
| 13:21:55.6526805 | peer/network I/O ended while the server awaited the post-1006 action |

The server did not observe action 74. The deployed build did not audit other
post-1006 packet IDs, so it cannot prove that no unrelated packet arrived;
however, it never reached `FirstWorldEntrySent`. TCP ended before the server's
two-minute deadline, while the Windows client remained visibly at
`Initializing`. The evidence therefore supports a client-side/network close,
not a Host exception and not an Emergency Stop of the VPS service.

## Exact root cause

The deployed encoder declared an 80-byte 1006 for the six-byte name `SHEEKO`
and began its packed strings at offset 71. In the exact approved client,
constructor `0x592056` registers the string pack at packet offset `0x57`
(87), and handler `0x592135` reads fixed fields through offset 86. At
`0x5925c2` the handler constructs action 74 only after completing those reads
and string extraction. The short packet therefore could not complete the
exact 5165 user-information state transition, so the client never emitted the
map-entry action.

The corrected packet is 100 declared bytes for `SHEEKO`: fixed bytes 0–86,
string count 2 at 87, name length/name at 88–94, and spouse length/`None` at
95–99. The remaining version-specific fixed fields are bounded zeros. This is
checked byte-for-byte by an exact-client-derived fixture; the old 80-byte
layout is rejected by that fixture.

That run correctly proved the former 1006 defect, but its provisional
24/1010 response claim was later disproved by the 15:45 UTC genuine run.
The exact approved client uses declared 32/10010 for action 74. The corrected
current evidence and byte layout are recorded in
`CHARACTER_WORLD_INITIALIZING_PUBLIC_REGISTRATION_20260829.md`; this historical
record must not be used as support for the older action packet generation.

## Preserved evidence

The root-only evidence directory is
`/root/revora-game-evidence/20260829T132557Z-world-initializing`. It contains
the complete sanitized journal, database proof, service state, firewall state,
connection inventory, and SHA-256 inventory. It contains no credentials,
owner address in this document, raw packet, ticket, DH secret, or cipher key.
