# Automatic Account Bootstrap Evidence

## Scope

Owner Test 4 is a bounded loopback interoperability test, not playable login. Success means one launcher Play action authenticates over HTTPS, obtains one 60-second ticket, verifies and starts the exact reviewed client, and causes its genuine encrypted length-276/type-1086 request to reach `127.0.0.1:9958` without placing the real account name or password in the legacy client. The bridge sends only the statically supported controlled Account error packet 1055. Ports 9958 and 9959 remain non-public.

## Exact inputs

- Canonical reconstructed `Conquer.exe`: SHA-256 `e3a4570314a0e9c06cee62e41662cd745da811cfdc8c28a85fb626920155db5c`.
- Reviewed Test 3 RSA intermediate: SHA-256 `902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`.
- Deterministic Test 4 output: SHA-256 `6d03cd53edde97c4354eea42b07e7fa79f7132d65f4710ba45f7c6b5da59e11d`.

The private patch metadata accepts only the canonical input and asserts every original byte, including the separately reviewed testing RSA replacements. No client executable is stored in Git or in the Owner Test package.

## Static findings

### Confirmed

- The exact client dispatcher's `CMD_GAMESTART` branch calls VA `0x50ee04` with internal command `0x480`.
- VA `0x50ee04` calls `PostMessageA` with Win32 message `0x464`, carrying `0x480` as the command argument, to the internal client HWND stored at `0x691c94`.
- The main handler's internal `0x480` case sets an Account-start flag; a later owned dispatch invokes the Account transition at VA `0x44cda9`.
- That transition resolves the selected endpoint and group/server before calling the connection/request builder at VA `0x522ea2`.
- The builder performs maximum-length checks, but Test 4 and Test 4b prove that empty state is not sufficient to reach it. The earlier empty-buffer acceptance claim is withdrawn.
- Genuine manual-login bytes `15 94 56 65` decrypt under the evidenced initial state to length 276 and packet ID 1086. The raw words 37909 and 25942 are encrypted/intermediate observations, not identifiers.

### Strongly Supported

- The observed Revora server-selection completion follows the dispatcher call at VA `0x44a3f4` to state reset VA `0x44b152`.
- Preserving that reset and then posting `0x480` is the narrowest static transition into the genuine Account state machine without legacy UI input.
- The client-generated compatibility identity is fixed and non-secret. Modern identity is established only by the launcher ticket and bridge/API transaction.

### Owner Test 4 result

Owner Test 4 was a genuine partial failure: authentication, ticketing, bridge readiness, exact patched-client launch and visible Revora selection passed, but no Account connection occurred and the ticket was revoked after timeout. The callback, stub and `0x480` delivery are therefore unproven. See [OWNER_TEST_4B_EVIDENCE.md](OWNER_TEST_4B_EVIDENCE.md).

Owner Test 4b also failed: external `PostMessage(hwnd, 0x480, ...)` reached no Account
connection because `0x480` is not the actual top-level Win32 message used by the client.
Test 4c moved to the exact one-time login-surface completion boundary and used the client's
own `0x50ee04` wrapper. It installed only fixed non-secret compatibility strings; modern
authentication authority remained the PID/session/build-bound one-time ticket.

Owner Test 4c also failed at the automatic transition boundary: its exact patched client launched
and the reusable workspace verified, but no Account connection occurred. None of the internal
transition stages are promoted from that result. Test 4d adds fixed one-byte, one-shot breadcrumbs
at exact asserted call sites solely to identify the first missing stage; it does not add another
success-path hypothesis.

### Rejected

Flash/ActiveX distribution, UI autofill, clipboard or synthetic input, runtime injection, sideloading, APC/remote-thread loading, executable-memory modification, elevation, services, drivers, persistence and antivirus exclusions remain rejected.

## Archived Test 4 deterministic changes

The combined private metadata records 64 testing-RSA replacements and these three asserted bootstrap changes:

| File offset | Original | Replacement | Purpose |
|---|---|---|---|
| `0x230` | `d2b52000` | `00c02000` | Extend `.text` virtual size only to its existing raw size so the reviewed stub is mapped. |
| `0x4a3f4` | `e8590d0000` | `e807221c00` | Route completed server selection through the exact bootstrap stub. |
| `0x20c600` | 21 zero bytes | `e84debe3ff6a006880040000e8f327f0ff83c408c3` | Call original reset, post `0x480` once, restore stack and return. |

The stub stays within existing `.text` raw data and before `.rdata`. It creates no section and disables no integrity, operating-system or security check.

## Ticket and bridge contract

- Ticket: 32 random bytes; database stores SHA-256 only; bound to account, realm, launcher session, build and launch; expires in 60 seconds.
- The launcher transfers it to a pinned buffer, requests `VirtualLock` on Windows and immediately zeroes the transfer copy.
- The bridge uses IPv4 loopback, backlog one, one connection, a 30-second timeout and exactly 276 encrypted request bytes.
- The accepted TCP four-tuple must resolve to the exact launcher-created PID.
- Valid input atomically consumes the ticket. Every earlier failure makes a bounded one-time invalidation attempt; replay is rejected.
- Reports contain safe state, time, build/executable hash, PID, length, packet ID, ticket lifecycle, response state and failure category only.

## Rollback

Test 4 constructs a separate sibling directory and never modifies Test 2c. Emergency Stop closes only the exact child and bridge.

V004 is additive. Before applying it, take and verify a narrow PostgreSQL backup. If deployment fails before any ticket is issued, stop only Admin, drop only `identity.launch_tickets` as `revora_migrator`, and remove only ledger version 4. Once a ticket exists, use forward recovery and preserve evidence.
