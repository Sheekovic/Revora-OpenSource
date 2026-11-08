# Retired Native Input Test 3b Direction

> Superseded before publication: Revora will not use UI Automation, child-window messages, `SendInput`, clipboard input, or credential autofill. The Account-cipher evidence below remains applicable; the input design is retained only as rejected-history documentation. See `LAUNCHER_AUTHENTICATION.md`.

## Purpose and evidence boundary

Test 3b independently evaluates two boundaries: delivery of bounded first-party input to the exact launched client, and the genuine initial Account transport. It never infers input success from a socket connection and never infers a connection from an empty report.

The native-input report records these states separately: `client-started`, `login-controls-discovered`, `username-delivered`, `password-delivered`, `login-action-delivered`, and `account-connection-observed`. Owner confirmation of visible username population and the expected masked password-character count is required before the helper enables its separate Login action. A manual-login path is explicitly labeled `manual-in-game-login`.

## Rejected input strategy

Static analysis and Test 3 disproved blind top-level `PostMessage` as a working boundary. Test 3b:

1. enumerates only windows owned by the exact launcher-created PID and records class/visibility metadata without window text;
2. requires an explicit owner click immediately before input;
3. brings the exact window to the foreground and revalidates the foreground HWND and PID before every bounded `SendInput` key event;
4. sends Account, Tab and Password once without the clipboard;
5. clears credentials immediately after delivery;
6. does not send Enter until the owner confirms both visible field outcomes;
7. aborts on focus loss or ownership change.

This method is no longer pursued and no autofill package will be published.

## Exact Account cipher evidence

The exact approved input executable is SHA-256 `e3a4570314a0e9c06cee62e41662cd745da811cfdc8c28a85fb626920155db5c`; the deterministic Test 3/3b output is `902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`.

Address-independent static analysis of the exact executable identified:

- two 256-byte initial counter streams generated from seeds `0x9d` and `0x62` by the client cipher initializer;
- a per-byte transform using those streams, a nibble swap and XOR `0xab`;
- no server seed dependency before the first Account request;
- the Account request constructor writing little-endian length `276` and packet ID `1086`;
- username at offset 4, a 128-byte password area at offset 132, and realm at offset 260;
- the first 16 password bytes transformed with the client's 12-round RC5 routine before transport encryption;
- Account response ID `1055`; a zero value at offset 4 selects an error path and error code `1` at offset 8 selects a recognized deterministic error.

Applying the evidenced initial client-to-server transform to the genuine manual-login bytes `15 94 56 65` yields `14 01 3e 04`: little-endian length `276`, packet ID `1086`. This is corroborated by direct construction in the exact client and the BSD-3-Clause Comet 5187 reference. The raw credential frame was not persisted or decrypted for reporting.

Test 3b's controlled response is exactly 12 plaintext bytes before the corresponding server-to-client transform: length `12`, ID `1055`, success/token field zero and error code `1`. It is a non-authenticating response and does not guess a success or handoff layout.

## Probe and reports

The probe binds only `127.0.0.1:9958`, backlog one, reads no more than 512 bytes, uses a five-second idle timeout, keeps ciphertext/plaintext only in memory and zeroes working buffers. It persists only byte count, ciphertext SHA-256, validated length/ID, state transitions, provenance and response status. It never persists username, password, password block, raw packet bytes or reusable tokens.

A random session ID and connection sequence identify the report. Updates rewrite one stable `native-protocol-3b-{sessionId}.json`; snapshots cannot masquerade as separate connections. Native input has a separate `native-input-3b-{sessionId}.json` report.

## RSA key boundary

The root-only key at `/srv/revora-client-repository/keys/native-login-test3/serverdat-private.pem` is mode `0600` and was verified by regenerating both the exact Test 3 `Server.dat` and patched executable hashes. It is the private testing key for RSA-protected DAT configuration, not an Account-login RSA envelope. No login-packet RSA operation was found in the evidenced request builder. The key is outside Git and all Windows/public artifacts.

## Acceptance boundary

The server-side cipher and controlled response are evidenced and tested, but genuine client-visible behavior remains pending Owner Test 3b. Success must state separately whether fields populated automatically, whether Enter was helper- or manually-delivered, whether the Account connection appeared, whether the encrypted header validated, whether response 1055 was sent, and whether the owner observed the expected controlled error.
