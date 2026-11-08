# Genuine 5165 Game handshake source matrix

Retrieval date: 2026-08-29. This record describes static evidence only. No third-party server or binary was built or executed.

## Pinned sources

| Source | Commit | Deterministic source-archive SHA-256 | License | Relevant files |
|---|---|---|---|---|
| [Comet](https://github.com/conquer-online/comet) | `cc9c788a27fbace85325955a67b96b4e8e43843e` | `7e8a1b33874620082aad481c564f31a5e121b62b1a87706861d81d2efd16bdc9` | BSD-3-Clause | `MsgHandshake.cs`, `DiffieHellman.cs`, `BlowfishCipher.cs`, `MsgConnect.cs`, `Server.cs` |
| [COPS v7 Emulator](https://gitlab.com/conquer-online/servers/cops-v7-emulator) | `84919e25e79eded39314ea50bfd787427eadd59f` | `11b17a893109e7d4e851c5101e6bd0cabd561a01005fd7602054a70d36815052` | BSD-4-Clause; bundled component notices also apply | `client.cpp`, `server.cpp`, `msgloginproofa.*`, `msgloginchallenges.*`, `diffiehellman.*`, `blowfish.*`, `msgconnect.*` |
| [Redux](https://github.com/conquer-online/redux) | `383c0f136e80d38475aa757b2f8a1fc2c24c6831` | `5600334d9144bc966ae8dd9bddcfdb903e46b86788d69c1817a95db6a103f74b` | Ambiguous repository notice; behavioral evidence only | `BlowfishExchange.cs`, `GameCryptographer.cs`, `[1052]Connect*.cs` |

Redux is not incorporated. Its repository lacks a clear root license grant and its README notice is ambiguous, so it is used only as an independent behavioral comparison.

## Exact-client static anchor

The approved M1 configuration-only x86 client was inspected as data only. Its executable SHA-256 remains `902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`; no diagnostic client was created or run.

The exact parser/builder evidence includes:

- `0x588261`: client DH-reply construction, `TQClient` trailer, default-cipher encryption.
- `0x5883fd`: server DH message decryption and bounded parsing.
- `0x588480–0x588491`: server message bounds (`12..1024`).
- `0x5884cd–0x5884e6`: `TQServer` trailer validation.
- `0x5884ec–0x588562`: two length-prefixed IVs, each required to be eight bytes.
- `0x58895a`: bounded little-endian 32-bit length-prefixed string parser used for P, G, and server public key.
- `0x5887e8–0x5887f7`: shared-secret bytes and their exact length passed to Blowfish key setup.
- `0x5a0543–0x5a054d`: Blowfish key length capped at 72 bytes, proving that a normalized 64-byte DH secret is not truncated to the standard 56-byte limit.
- `0x588cc7–0x588d2c`: 28-byte packet 1052 construction.

Addresses are evidence for this exact hash only and are not runtime integration points.

## Byte-level comparison

| Property | Comet | COPS v7 | Redux | Exact 5165 client | Classification |
|---|---|---|---|---|---|
| Default Blowfish seed | ASCII `DR654dt34trg4UI6` | same | same | string at VA `0x6235d4`; used before DH parsing | Identical; client-proven |
| Initial IVs | zero/zero | zero/zero | zero/zero | default cipher is initialized before first receive | Identical; client-compatible |
| Server prefix | 11 random bytes | 11 random bytes | 11 random bytes | parser begins structured data at offset 11 | Identical structure |
| Server size | LE `int32` at offset 11; value is wire length minus 11 | same | same | bounded LE value, trailer included | Identical; client-proven |
| Junk | LE `int32` length then random bytes (normally 12) | same | same | skips bounded declared amount | Semantically equivalent |
| IV order on wire | server-encrypt first, server-decrypt second | same | names are client-IV then server-IV; same directions | reads two eight-byte values in this order | Semantically equivalent |
| P | LE `int32` length + uppercase ASCII hex | same | same | length-prefixed ASCII parser | Identical; client-proven |
| G | LE `int32` length + ASCII `05` | same | same | same parser | Identical; client-proven |
| Server public value | LE `int32` length + uppercase ASCII hex, no leading zero | same | same | same parser | Identical; client-proven |
| Server trailer | ASCII `TQServer` (8 bytes) | same | same | exact validation | Identical; client-proven |
| Typical server wire length | 333 bytes for 128-char public value | 333 | 333 | accepted by bounds/parser | Identical candidate |
| Initial server encryption | whole message and trailer under default Blowfish CFB64 | same | same | default cipher decrypts before parsing | Identical; client-proven |
| Client prefix | 7 random bytes | 7 random bytes | 7 random bytes | builder reserves seven bytes | Identical structure |
| Client size | LE `int32` at offset 7; total wire length is `7 + size` | same | same | builder/parser use this relation | Identical; client-proven |
| Client public value | after bounded junk: LE `int32` length + uppercase ASCII hex | same | same | exact builder | Identical; client-proven |
| Client trailer | ASCII `TQClient` | same | same | builder appends exact trailer | Identical; client-proven |
| Client DH reply encryption | default Blowfish CFB64; stream state continues while reading header and remainder | same | same | exact builder uses default cipher | Identical; client-proven |
| DH P | `E7A69E...E857F` | same | same | exact parser accepts supplied P | Identical |
| DH G | hex `05` | same | same | exact parser accepts supplied G | Identical |
| Peer public validation | weak in legacy source | weak | weak | not a safety specification | Revora must reject values outside `2..P-2` |
| Shared secret | unsigned big-endian normalized integer bytes | same semantic value | same | passes variable bytes + length | Semantically equivalent; client-proven length handling |
| Leading zero | omitted | omitted | omitted | variable length passed explicitly | Semantically equivalent |
| Blowfish key limit | 72 bytes | 56 bytes | OpenSSL full supplied length | exact client cap is 72 | **Conflict resolved in favor of exact client: up to 72** |
| New IV assignment, server view | inbound gets second wire IV; outbound gets first | same semantics | same semantics | parser retains both; use is consistent with peer direction | Semantically equivalent |
| Cipher activation | only after complete client DH reply validates | same | same | builder sends reply under old cipher then switches | Identical |
| First encrypted Game message | declared packet plus `TQClient`, all under new cipher | same | same | normal send path appends trailer | Identical; client-compatible |
| 1052 declared length | 28 (wire consumes 36 with trailer) | 28 | 28 | exact constructor writes 28/1052 | Identical; client-proven |

## Canonical H1 candidate

Only one candidate is selected; no runtime fallback or alternate is permitted:

1. Generate cryptographically random 8-byte server-encrypt IV, 8-byte server-decrypt IV, and ephemeral private exponent.
2. Serialize the 333-byte canonical server message described above (12 junk bytes, 128-char P, `05`, 128-char server public value), append `TQServer`, and encrypt all 333 bytes with default Blowfish CFB64 using zero IVs.
3. Receive/decrypt exactly the bounded client reply under the same default inbound stream: first 11 bytes, validate total-length relation, then the remainder. Require `TQClient`, bounded junk, and a 1–128 character uppercase/lowercase hexadecimal public value.
4. Reject the peer value unless it is in `2..P-2`. Derive `peer^private mod P`, normalize as unsigned big-endian bytes without a leading sign byte, and initialize Blowfish using the full length (maximum 64 here; implementation ceiling 72).
5. From the server perspective, decrypt client traffic with the second IV sent on the wire and encrypt server traffic with the first IV.
6. Activate the new cipher only after the full DH reply validates. Read one encrypted normal frame: decrypt its four-byte header without resetting stream state, bound declared length to 28 for H1, then decrypt the remaining 24 bytes plus eight-byte `TQClient` footer. Parse only 28/1052.

Random padding values and ephemeral keys are intentionally variable; their positions and lengths are fixed. Deterministic fixtures use fixed test-only inputs, never production randomness.

## Remaining conflicts and evidence boundary

No unresolved disagreement affects the selected H1 wire bytes, IV direction, activation boundary, or footer behavior. The COPS 56-byte Blowfish cap conflicts with the other sources but is decisively incompatible with the exact client’s explicit 72-byte cap; it is rejected for Revora.

The single H1 run ended before the Game connection: the exact client's Account-success handler rejects every `127/8` destination before calling its connection routine (see `docs/MANUAL_PLAYABLE_M2_H1.md`). Therefore the canonical candidate was neither exercised nor rejected. DH, Blowfish, Game authentication, and playability remain unproven, and the candidate implementation is unchanged.
