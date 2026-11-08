# Revora Native Login Packet Binding — Owner Test 3 correction

## Test-2 preservation and classification

Functional Owner Test 2 is consumed and preserved unchanged. Its private ZIP is
`PlayRevora-Native-Login-Packet-Binding-OwnerTest-2-20260901.zip`, size
79,887,465 bytes, SHA-256
`FEEE2F5535745EA8FDC201DBFD18CF58F4B75628B64A45A62A2936DA93DCDD8C`.
The run launched approved DX9, loaded the Bridge, and reached the native loading
state. No HTTPS native authorization reached the server. The active JSONL was
locked against the launcher and contained only `login_bridge_loaded` and
`login_bridge_ready`; it is retained as failure evidence and is never reused.

## Earliest corrected boundary

Static PE evidence for approved DX9 establishes image base `0x00400000`.
`0x00B5D3B2` is a preferred VA, not an RVA: its RVA is `0x0075D3B2`, its file
offset is `0x0075C7B2`, and its entry preimage is `68 A8 04 00 00`. Test 2
patched the single callsite at RVA `0x0025E69D` (`E8 10 ED 4F 00`). That was not
a semantic coverage guarantee.

Test 3 detours the function entry at RVA `0x0075D3B2`. A Revora-owned x86
trampoline preserves the overwritten `push 0x4A8` instruction and resumes at
entry plus five bytes. Initialization verifies the preimage, page-protection
transition, instruction-cache flush, and installed jump postimage before
emitting `login_bridge_ready`.

The second Test-2 defect was cross-thread lifetime: credentials and attempt
state were `thread_local` and cleared immediately after the dispatcher call.
An asynchronous or different-thread native send therefore could not match the
attempt. Test 3 uses one locked process attempt with an eight-second lifetime,
copies credentials into bounded local send-time buffers, and clears every copy
after authorization outcome. It still classifies only the documented 440/1098,
480/1102, or 472/1942 account envelope on a port-9958 peer.

## Diagnostics contract

The report is `<client-root>/RevoraDiagnostics/native-login-owner-test-3.jsonl`.
The Bridge opens it with read/write sharing, writes one newline-terminated JSON
object per transition, and flushes every line. The launcher reads the live file
with read/write/delete sharing and bounded retry. `RevoraDiagnostics` remains
mutable runtime output and is not repair-manifest content.

No event contains credentials, HTTP bodies, packet bytes, grants, tokens,
nonces, password material, private keys, or database credentials. Correlation
identifiers are random per attempt and are diagnostic labels, not credentials.

## Test-3 functional sequence

1. Install transactionally into the approved clean client root.
2. Start PlayRevora, select explicit DX9, and press Start Game.
3. Use one disposable account registered at `https://playrevora.com/register`.
4. Enter it in the native login UI and press Login exactly once.
5. Preserve the live JSONL before rolling back.

The expected path is submit, bounded capture, classified native request,
validated HTTPS, pending authorization, one unchanged send, native 1637, then
game handoff. Any failure remains fail-closed and categorical.

## Rollback boundary

The package installer backs up every launcher/manifest/Bridge artifact it
replaces. The rollback script restores those exact files and removes Test-3
files that had no predecessor. It never modifies either proprietary
`Conquer.exe`.
