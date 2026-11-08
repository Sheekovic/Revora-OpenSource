# Manual Playable Baseline M1 Evidence

## Genuine Windows result

Owner Test M1 ran once on 2026-08-29 and is complete; it must not be rerun. The owner-returned
sanitized report and screenshot establish a conclusive bidirectional Account-transport baseline:

- reviewed RSA-testing configuration-only executable SHA-256
  `902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`;
- launched and connected process ID `22652`;
- complete donor/profile verification and the configuration-only difference audit passed
  (`64` records, `256` permitted byte positions, `253` bytes actually different);
- one genuine encrypted 276-byte request decrypted to length `276`, packet ID `1086`;
- one encrypted 12-byte packet `1055` was sent and decoded by the genuine client;
- the visible response was `Failed to login: Invalid account or password!`;
- the client remained running, no public listener was used, and neither credentials nor packet
  payloads were persisted.

This confirms the Server.dat trust-anchor replacement, signed loopback Server.dat, Revora realm
selection, initial Account cipher in both directions, 276/1086 framing, 12/1055 controlled-error
framing, PID attribution, and the stated redaction boundary. It does not confirm successful
authentication, the successful 1055 form, Game handoff, character selection, or world entry.

## M2 static boundary

Exact-client disassembly of the response handler at `0x58963f` establishes that a successful
1055 reads two 32-bit values at offsets 4 and 8, a 16-byte NUL-terminated address beginning at
offset 12, and a 32-bit port at offset 28. The exact client later constructs a 28-byte packet 1052
at `0x588cc7`–`0x588d2c`, carrying the same two 32-bit values at offsets 4 and 8, a 16-bit client
field at offset 12, ten bytes at offset 14, and a 32-bit version field at offset 24. This is
corroborated by the pinned BSD-3-Clause Comet source and separately by evidence-only Redux and
HeroesCoServer layouts.

The remaining pre-owner-run blocker is the Game connection transition. Reviewed sources agree
that a DH/Blowfish exchange occurs before or around packet 1052, but their serialization, IV,
footer, and activation timing conflict, and no genuine 5165 Game transcript exists. Sending a
successful 1055 without implementing the exact expected Game handshake would knowingly stop at
an unclassified encrypted connection and would not meet the M2 publication gate. No M2 owner
package is published until that transition is established without guessed bytes.
