# Revora Auth Client v1

The selected DX9 derivative imports `RevoraAuthClient.dll` through the PE import
table. There is no loader, injection, proxy DLL, connect hook, or ordinal-55
dependency.

At the genuine CMsgAccountEx construction call (RVA `0x009B0342`, preimage
`E8 B7 CC 17 00`) the imported cdecl function receives the existing fourteen
arguments. Arguments 2 and 3 are the bounded native username/password values and
argument 13/14 is the 472-byte output/capacity pair. The function uses the values
only for one synchronous, certificate-validated HTTPS request, clears its copies,
and writes a version-1 proof packet for the existing Account outer cipher/send.

The 32-bit opcode-1059 seed is not a sufficient authentication challenge: it has
only 32 bits and is observable on the connection. AccountHost therefore retains
the unchanged 8-byte 1059 packet but creates an independent 256-bit random nonce,
a 256-bit connection binding, a unique challenge ID and a 12-second expiry. The
HTTPS service locates exactly one pending connection using the normalized source,
realm, profile and client contract; ambiguity fails closed. After Argon2id password
verification it atomically stores the expected HMAC-SHA-256 proof and returns the
ephemeral proof material over TLS. Receipt of HTTP success therefore precedes the
native proof send.

The proof packet is `[length:u16le, opcode:0x7F11, magic:RVA1, version:u16le,
usernameLength:u16le, challengeId:16, clientNonce:32, proof:32, username:utf8]`.
The HMAC transcript is challenge ID, server nonce, client nonce, connection
binding, protocol version, normalized username and realm. AccountHost atomically
consumes it once. Failure uses the established status-1 opcode 1637 path.

The processed-1637 stub at RVA `0x009FD0A2` and outgoing-2429 stub at RVA
`0x009FC095` call imported exports. Only a successful length-52/status-2 response
captures the nonzero UInt32LE at +4; exactly one structurally valid 2429 receives
the same four bytes at +4.
