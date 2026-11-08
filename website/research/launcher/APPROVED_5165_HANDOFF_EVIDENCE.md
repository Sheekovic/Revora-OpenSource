# Approved 5165 launcher-to-client handoff evidence

## Scope

This record applies only to the reviewed RSA-testing configuration-only executable with SHA-256
`902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`. The permitted executable
difference remains limited to the reviewed embedded `Server.dat` RSA verification modulus. No
fixed-address patch, UI automation, injection or compatibility module was executed for this
investigation.

The executable used for local static analysis is retained outside Git at
`/root/revora-research/launcher-canonical/revora-5165-stable/Conquer.exe`. Windows CI independently
downloads the same hash from the signed test desired-state publication. The executable is never
committed.

## Public test-channel diagnosis

On 2026-08-30 both public test manifests returned HTTP 200 without redirects:

| Endpoint | SHA-256 | Bytes |
|---|---|---:|
| `https://playrevora.com/patch/v2/test/manifest.json` | `b0237870474713c9f911fd891919f56f4ed458c28aa627041541969f44cf346c` | 6,933,146 |
| `https://playrevora.com/launcher/v1/test/manifest.json` | `87ed39c3ee995d2de57a48f12985f7e6ccc8f6be2c3d8f1ae7ab95fc83f1a9d3` | 4,157 |

The supplied “signed patch service is temporarily unavailable” screen was a real fail-closed
runtime result, not the deterministic visual-test Error state. The downloaded test executable was
started without `--test-channel`, selected the intentionally absent stable URLs, and received 404.
TLS and the test publications were healthy. Test packages now carry an exact
`Revora.TestChannel` marker; the stable default remains unchanged, and the marker selects only the
separately signed test manifests. An invalid marker fails closed.

## Evidence matrix

| Candidate | Exact-client evidence | Runtime/security result | Decision |
|---|---|---|---|
| Command line | Startup parses `%s %s %s`; token one must equal `blacknull`. Tokens two and three call the native setters that publish `LOGINUSER` and `LOGINPASS`. The password setter copies the value, XOR-transforms it with `0x2a`, then stores it for the existing 1086 builder. | A suspended Windows process created from the exact executable exposes all supplied tokens through the OS process command line before client code runs. A password or bearer ticket here violates the secrecy requirement and can enter process/crash telemetry. | Proven client input surface; rejected for secrets. Only `blacknull` remains permitted. |
| Environment/inherited process data | The import table contains CRT `getenv`, but both call sites are within the bundled OpenSSL environment lookup path (`ENV`/`default`). No Revora/account/ticket environment name, parser or path into `LOGINUSER`/`LOGINPASS` exists. | Environment blocks are also inherited process data and are not a secret boundary. | No supported authentication surface proven; rejected. |
| Named pipe | No `CreateNamedPipe`, `ConnectNamedPipe`, `CallNamedPipe`, `WaitNamedPipe` import/string or account-path consumer exists. | A separate process can create a pipe, but the unchanged client has no evidenced reader. | Unsupported. |
| Shared memory | No `CreateFileMapping`, `OpenFileMapping`, `MapViewOfFile` import/string or account-path consumer exists. | A mapping would be useless without changing or injecting client code. | Unsupported. |
| Local loopback Account handoff | M1 genuinely proved that a signed loopback `Server.dat`, manual credentials and a PID-attributed listener can carry 276/1086 and 1055. Later VPS evidence proved the real Account/Game path. | The exact client still needs its native login-submit transition. Loopback does not itself cause that transition, and successful Game endpoint handling must remain public-IP compatible. | Useful protocol boundary, not launcher-only authentication. |
| Existing configuration/login protocol | `Server.dat` selects the Account endpoint; 276/1086 carries the native username/password representation. Manual genuine-client operation is production-proven. | The protocol has no evidenced ticket field and cannot consume launcher memory directly. Substituting a bearer token into the legacy fields exposes it if command-line supplied and still requires submission. | Manual fallback only. |
| AccountBind registry/application path | The binary contains `AccountBind.exe`, `AccountRecord`, and `SOFTWARE\\Tqdigital\\Conquer\\Accounts\\`; the companion executable is absent from the approved client profile. | This is a legacy remembered-account/registry mechanism, not a memory-only ticket interface. Restoring it would introduce credential persistence and an unverified binary. | Intentionally rejected. |
| Version-bound compatibility module | The executable imports `LoadLibraryA`/`GetProcAddress`, but no first-party module contract or authentication callback is exported or documented. Previous PE-entry/control-flow experiments froze or closed before Account transition. | Any proposed module would currently require injection, an API/IAT hook, a private internal call, or UI control. No repeatable evidence proves a stable interface, and the PE itself has no relocation table or ASLR flag to “preserve.” | Blocked; no module implemented. |

## Exact static observations

- PE32 Intel i386, image base `0x00400000`, entry RVA `0x0019d668`.
- No base-relocation directory and `DllCharacteristics == 0`; the approved legacy executable does
  not itself opt into ASLR/NX/modern PE mitigations. Revora must not claim otherwise.
- `blacknull` is stored at image VA `0x0067d514`; the startup parser at `0x004b9d88` parses three
  whitespace-delimited strings and compares token one at `0x004b9da4`.
- On non-empty tokens two/three, calls at `0x004b9e6b` and `0x004b9e82` reach the username and
  password setters (`0x0044b335`, `0x0044b38e`). These addresses are research evidence only and
  are not used by launcher code.
- The normal launcher starts the client with only `blacknull`.

## Decision

No safe unchanged-client launcher-ticket handoff is proven. The compatibility feature remains
disabled and stable promotion remains blocked. Implementing a speculative hook would violate the
approved evidence boundary. The smallest genuine evidence gap is a supported, non-secret-bearing
way to cause the client to consume a launcher session, or explicit approval for a narrowly reviewed
client compatibility change after its exact wire/control behavior is established.

The temporary supported flow remains: the launcher verifies/repairs the approved client, then the
player manually enters the account credentials in the genuine client. It is **not** launcher-only
authentication.
