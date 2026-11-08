# Revora single-entry native login architecture

The owner enters credentials once, in the native Patch-7630 login UI. The
launcher performs update, signed integrity/repair, profile selection, and game
start only. For the private canary it starts a hash-gated x86 loader after the
approved process exists; that loader loads only `RevoraNativeLogin.dll`.

The DLL hooks the proven DX9 submit RVA `0x0025EA32` (preimage
`68 B0 00 00 00`) and credential accessor RVA `0x00259F05` (preimage
`68 30 01 00 00`). Modes 2 and 5 are copied into bounded attempt-owned buffers
only while the explicit submit is active. At the final Account `send` IAT
boundary, only a 472-byte request on the exact Account socket is held. The DLL
authenticates over validated HTTPS, binding the request SHA-256, realm, profile,
contract, correlation ID, and source/session context before releasing the
unchanged request. Failure suppresses it and clears the attempt.

The existing AccountHost authorization consumer, outer cipher, opcode-1637
3-Way encoder, random UInt32 world handle, and MaTriX Game server are retained.
Post-decoded 1637 RVA `0x009FD0A2` (preimage `68 4C 06 00 00`) captures the
successful UInt32LE at `+4`. Pre-transport opcode-2429 RVA `0x009FC095`
(preimage `55 8B EC 51 53`) consumes it once at `+4`.

No launcher credentials, ticket UI, named pipe, graphics proxy, LoginLoader,
native TCP password, or unconditional success exists in this path.
