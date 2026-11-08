# Patch-7630 identifier bridge evidence

Approved DX9 image: `Env_DX9/Conquer.exe`, SHA-256
`4889B6DAB71068D687A9757F64FB1033E9CE1C560714DF3B62634229776A65CE`,
preferred image base `0x00400000`.

## Processed Account response

The opcode-1637 specialized handler begins at VA `0x00DFD0A2` (RVA
`0x009FD0A2`). Its exact five-byte preimage is `68 4C 06 00 00`. The handler
loads its parsed packet pointer from `this+0x404` at `0x00DFD0D3`, then loads
the little-endian identifier from packet offset `+4` at `0x00DFD0D9`.

The specialized parser reaches this handler only after the ordinal-63 call at
`0x00DFCC00` has returned and the generic parser has accepted opcode `0x0665`.
Accordingly, this seam observes the handler representation after both Account
transport decryption and message-specific 3-Way decoding. It does not inspect
socket ciphertext.

## Outgoing Game request

The original outgoing packet pipeline begins at VA `0x00DFC095` (RVA
`0x009FC095`). Its exact five-byte preimage is `55 8B EC 51 53`. The sole packet
argument is loaded at `0x00DFC09B`; packet bytes begin at object offset `+4`.
The original routine copies the validated packet to its internal output buffer
and then applies the client's existing transport processing at `0x00DFC0BE`
and, according to connection state, `0x00DFC103`/`0x00DFC165`, before delegating
to the original send path at `0x00DFC174`.

The bridge therefore writes opcode-2429 offset `+4` at function entry, before
the existing framing, sealing/encryption, and send path. It changes no other
packet bytes and does not hook encrypted Game traffic.

## Legacy corroboration

The owner-supplied LoginLoader binary (SHA-256
`4E0F9E2634C257A9070C0CEA113E726AB2B873E44244C35D358BAD295FD1B885`)
independently establishes the field contract: its incoming adapter at DLL RVA
`0x34A0` retains the dword at opcode-1637 offset `+4`, and its outgoing adapter
at DLL RVA `0x32A0` writes that same dword at opcode-2429 offset `+4`. No code or
binary material from that DLL is included in Revora.
