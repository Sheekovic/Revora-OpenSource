# Manual Playable Baseline M2-H1

## Final owner evidence

The single permitted owner run is final. The exact approved client (`902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`, PID 352) sent genuine encrypted 276/1086, the throwaway credentials validated, and Revora sent the 32-byte/1055 success response. No Game connection occurred. Consequently no DH bytes were sent, no client DH reply or shared secret existed, Blowfish was not activated, and 1052 was not tested. Ticket revocation was attempted and the report retained no credential or cryptographic payload. The visible self-close is not classified as a crash because no exit code or exception evidence was collected.

This is a useful Account-success/endpoint failure, not Game authentication. M1 remains the conclusive bidirectional Account-error baseline.

## Proven pre-Game failure

The exact client handler at `0x58963f` reads two little-endian 32-bit handoff words at offsets 4 and 8, an ASCII address beginning at offset 12, and a little-endian 32-bit port at offset 28. IAT slot `0x60d90c` imports WS2_32 ordinal 11 (`inet_addr`). A return of `INADDR_NONE` is rejected.

After parsing the numeric address, instructions `0x589749..0x589766` inspect its first octet and reject when both conditions hold:

```
firstOctet % 7 == 1
firstOctet % 23 == 12
```

For every IPv4 loopback address the first octet is 127, and `127 % 7 == 1` plus `127 % 23 == 12`. The handler therefore branches to cleanup at `0x5897fe` before the connection routine at `0x535187`. Revora encoded `127.0.0.1:9959` exactly as configured, so the client could not attempt the Game socket. This explains the H1 boundary without testing or rejecting DH/Blowfish.

The response fields themselves are confirmed: length 32, ID 1055, word order at offsets 4/8, NUL-terminated and zero-padded 16-byte ASCII address, and uint32 little-endian port 9959. COPS v7 and Redux independently use this 32-byte layout. Comet 5065 adds a trailing two-byte value in its success encoder, an implementation difference that does not alter the exact client's endpoint reads or the proven pre-connect rejection. The existing Account cipher uses independent directional state; M1 already proved the server-to-client initial stream with 1055, and H1 did not consume that outbound state before its response.

## Correction boundary

No next owner package is published. An unchanged approved client cannot be directed to any `127/8` Game listener because of the exact guard above. A credible next run requires one newly authorized design choice:

- a narrowly restricted, non-loopback owner-machine endpoint (with independently proven access controls so it is not publicly reachable), or
- a separately reviewed configuration-only client change limited to this loopback guard.

Neither is authorized by the current loopback-only/no-further-client-change boundary. Revora now fails closed before emitting a known-rejected success response. The Game DH/Blowfish candidate and its fixtures remain byte-for-byte unchanged.

M2-H1 is a single-run protocol evidence package, not a playable-server claim. It reuses the exact M1-approved configuration-only executable (`902954c9b669118fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`) and signed loopback `Server.dat`; it makes no new client change.

The original H1 Windows tool verified the signed donor/profile, started loopback listeners on `127.0.0.1:9958` and `127.0.0.1:9959`, and launched the exact reviewed client. Its intended later DH steps were never reached.

The sanitized report contains only lengths, IDs, PID attribution, validation categories, candidate ID, cipher-transition state, token lifecycle, and the failure boundary. Credentials, raw packets, DH private material, shared secret, Blowfish key, and raw handoff tokens are never written.

Observed visible behavior: after manual Enter the client showed “Logging into account server” and later closed without a captured dialog. That observation establishes neither a clean exit nor a crash.

Rollback is local and narrow: Emergency Stop cancels both loopback listeners, closes only the launched client, zeroes temporary buffers, and retains the original Test 2c donor unchanged. No public listener, AMP, ADS, nginx, DNS, firewall, Web, or production Game service is changed.
