# Manual Playable Baseline

## Decision and scope

The fixed-address automatic UI bootstrap experiment is frozen after Owner Test 4j. The next
product gate is a genuine manual patch-5165 session: an owner enters credentials for a throwaway
development account in the legacy client, the Account service decrypts and parses the genuine
request, and a verified Account-to-Game response advances the unchanged game flow. Manual entry
is permitted only for this development milestone; launcher-only authentication remains the
production design.

No Test 4k/4l-style binary transition, `PostMessage`, UI automation, keyboard synthesis, runtime
hook, injection, or fixed-address diagnostic patch is part of this milestone.

## Test 4j final classification

The two genuine Windows reports are reproducible evidence with the following shared result:

- the exact reviewed Test 4j executable launched and its startup controls were valid;
- trace transport and partial-order validation passed;
- stages 1, 2, 4 through 16, and stage 25 executed;
- `wrapperPostRejected` was true;
- the top-level window was never reported shown and the internal wrapper never returned;
- command `0x480` was not consumed, the pending field was not polled, and the readiness guard was
  not reached;
- no Account transition, socket connection, or encrypted 276/1086 request occurred;
- each launch ticket was revoked;
- the owner saw the client freeze at its loading surface and then close in both runs.

This is a failure of the invasive UI-bootstrap/control mechanism, not an Account-server failure.
The pre-CRT diagnostic sequence issue was fixed before these runs. Further fixed-address patches
would be unsupported iteration and are rejected for this milestone.

## Evidence already retained

The existing genuine manual-login observation remains applicable:

- exact client preparation and launch succeeded;
- the owner manually selected Revora, entered throwaway credentials and pressed the in-game Enter
  action;
- the client opened an Account connection;
- the repeated initial ciphertext prefix was `15 94 56 65`;
- the independently implemented initial cipher transforms that prefix into little-endian length
  `276` and packet ID `1086`;
- no password bytes or packet payload were persisted.

The repository contains bounded stream handling and tests for fragmented input, the initial
Account cipher, the 276/1086 header, and a 12-byte encrypted controlled-error response with packet
ID `1055`. Owner Test M1 conclusively proved genuine client-visible acceptance: the client showed
`Failed to login: Invalid account or password!` and remained running.

## Approved M1 resolution

The unchanged signed Test 2c executable has SHA-256
`e3a4570314a0e9c06cee62e41662cd745da811cfdc8c28a85fb626920155db5c`. Its `Server.dat` verifier
uses the historical embedded 2,048-bit public modulus. Revora does not possess the corresponding
historical private signing key. Consequently, Revora cannot create a modified loopback/Revora
`Server.dat` that this unchanged executable will accept.

The earlier manual Account observation used the reviewed RSA-testing executable
`902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`, whose only client change
replaces the 64 embedded modulus immediates so a root-only Revora testing key can sign the
loopback-only `Server.dat`. That testing key protects configuration construction; it is not part
of the Account password packet.

Therefore all of the following cannot simultaneously be true with current evidence:

1. `Conquer.exe` is byte-for-byte unchanged Test 2c;
2. only a newly generated Revora `Server.dat` selects loopback;
3. no historical private key, hosts-file modification, public endpoint, or runtime redirector is
   used.

The owner approved the reviewed configuration-trust-anchor replacement exclusively for Manual
Playable Baseline M1. The exact output must be called the **Reviewed RSA-testing
configuration-only client**. It is not the unchanged Test 2c executable, not an unmodified client,
and not the production final client.

## M1 implementation boundary

M1 reproduces the approved output transactionally from the exact Test 2c donor and asserts the 64
reviewed four-byte modulus-immediate locations. A machine audit rejects any other offset or final
hash. The private signing key stays root-only; only the signed loopback `Server.dat` and public
verification modulus are delivered. Manual legacy fields remain available and unchanged.

The authenticated owner package is `3,041,097` bytes with SHA-256
`4cd3aba8ac5e43bf76a722132bca3f227dfc02a58f5ce98e2046a5c27ce24224`.
It contains no Conquer executable or private key.

The protected route is `/owner-downloads/manual-playable-baseline-m1` on
`https://admin.playrevora.com`. Unauthenticated access is challenged. Windows CI run
`33245784994` passed the WPF, Native Login, x86 fixture, launcher, Windows compilation, and
framework-dependent package checks. The pre-M1 Admin executable is retained at
`/srv/revora-admin/backups/manual-m1-20260829T093559Z/Revora.Admin`; this path is operational
evidence and is not part of any client package.

Hosts-file changes, DNS manipulation, arbitrary hooks, Dragon binaries, and public listeners are
not acceptable shortcuts.

## M2 Account and Game boundary

The completed M1 owner run proved that the genuine client sends encrypted 276/1086 and accepts
the controlled encrypted 1055 error. See `MANUAL_PLAYABLE_M1_EVIDENCE.md`.

Exact-client response-handler and Game-connect-builder disassembly establish a 32-byte
successful 1055 and the 28-byte 1052 field boundaries. M2-H1 now supplies one bounded canonical
Game handshake candidate after resolving the serialized format against three independent 5065
sources and the exact 5165 parser. Authentication and playability remain blocked on genuine-run
confirmation of:

- credential transformation semantics and safe password comparison for the 16-byte password
  field;
- the client's acceptance of the resolved DH/Blowfish serialization, IVs, activation point, and footer behavior;
- genuine confirmation that the two 1055 handoff words are returned in 1052 as the static client
  indicates;
- character list/create/select and map-entry layouts.

No alternate Game handshake is attempted. H1 sends exactly one candidate and stops after 1052,
disconnect, or timeout. Character/world packets remain outside this milestone.

## M1 one-run behavior

The owner package will contain only first-party framework-dependent tooling and signed metadata.
It will verify the selected client/profile, alter no client executable at runtime, start a
loopback-only Account probe before launch, accept one PID-attributed connection, bound the request
to 276 bytes, decrypt only in memory, persist only sanitized length/ID/state metadata, send the
controlled 1055 response, zero working buffers, and stop cleanly. The owner will use a throwaway
account and manually interact with the in-game login once.

Expected visible result for the first run is a deterministic controlled invalid-account response,
not character selection. Expected network evidence is one encrypted 276/1086 request and one
encrypted 12-byte/1055 response. Failure closes only the local tools, revokes any ticket if used,
restores controlled configuration files atomically, and leaves the donor untouched.

Ports 9958 and 9959 remain non-public. AMP, ADS, nginx, DNS, firewall, Revora.Web, and the public
Download page remain unchanged.
