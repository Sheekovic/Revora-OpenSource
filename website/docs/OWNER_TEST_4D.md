# Owner Test 4d fixed-stage diagnostic

## Revision 1 workspace-upgrade correction

The original Test 4d stopped before authentication or client launch. Its marker was valid: the
documented schema-1 canonical serialization reproduces checksum
`a8c0e1e6d7f18cd9e6474feb2591785d0ac00809c4bf3fe4c075d7ce09fa7292`, and the complete
marker reproduces SHA-256 `8658bcb782507a1e3a4fdbd6aafd5b6fa344f82baf6fdb885bcebcb7bdd13c14`.

The exact failure was patch-input validation. Test 4d restored `Conquer.exe` from the canonical
Test 2c donor (`e3a4570314a0e9c06cee62e41662cd745da811cfdc8c28a85fb626920155db5c`), then incorrectly
applied a patch whose expected input was the RSA intermediate
`902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`. The existing workspace
itself was in the legitimate Test 4c state
`8040023d599116c5db3d91eb3eb97065ec15521ed8b6a3a7ac6c3a28247ed310`.

Revision 1 recognizes only the canonical, RSA-intermediate, Test 4c, and reviewed Test 4d active
hashes. It verifies 12,829 immutable files separately, fully verifies the unchanged Test 2c donor,
stages only `Conquer.exe` and `Server.dat`, applies canonical-to-RSA and RSA-to-Test4d patches in
order, verifies the final signed hash, atomically replaces those two controlled files, retains a
narrow previous-state rollback, and atomically upgrades the same WorkspaceId marker only after
success. A failed replacement restores the prior controlled state.

The package-local `Reports` directory is initialized before profile or workspace validation.
Early failures now record a precise sanitized phase/category/component and safe expected/actual
hashes, marker/build state, and rollback result.

## Test 4c classification

Owner Test 4c is genuine Windows evidence and a partial failure. Framework-dependent delivery,
workspace creation, unchanged-baseline verification, and exact patched-client launch passed.
No Account connection occurred. Internal completion, selection writes, compatibility setters,
dispatcher execution, Account transition, request builder, and socket creation remain unproven.
The owner-reported executable SHA-256 was
`8040023d599116c5db3d91eb3eb97065ec15521ed8b6a3a7ac6c3a28247ed310`.

The raw owner report was not uploaded to a server-side path. This independently written record
preserves the supplied fields and does not invent a raw evidence artifact.

## Count reconciliation

The signed clean profile contains exactly 12,831 files. `OwnerTestWorkspace` verifies 12,829
unchanged files because exactly two signed, controlled files are excluded from that loop:
`Conquer.exe` and `Server.dat`. Their signed baseline rollback copies are separately verified,
restored, atomically replaced, and the complete 12,831-file target profile is then verified.
No profile file is missing or unexplained.

## Diagnostic design

Input SHA-256:
`902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`.

Output SHA-256:
`842204b71208b88646797177f0a3a685bac9868d2e28ed8b1d85f7ce6dea0a83`.

The patch asserts all original bytes and installs a fixed diagnostic in the existing mapped
`.text` raw-data tail. It uses the client's already imported C runtime `fopen`, `fwrite`, and
`fclose` functions to append one byte per reached stage to the fixed tool-owned relative file
`.revora-owner-test/test4d-stage.bin`. The launcher clears only that exact file before launch,
requires the valid marked workspace, reads at most 13 bytes, and accepts only the strict sequence
1 through N. Duplicates, gaps, out-of-order stages, excess bytes, and reparse points fail closed.

The thirteen client stages are transition entry, original completion return, verified group,
verified server, username setter return, password setter return, dispatch-wrapper entry,
command 0x480 submission, handler consumption, Account-transition entry, endpoint handoff,
request-builder entry, and socket-connect attempt. Loopback connection and decrypted 276/1086
remain bridge-observed stages 14 and 15.

The diagnostic emits no string value, credential, ticket, packet data, key, endpoint, memory,
or absolute path. It is bound indirectly to the launcher-created exact-hash process through the
existing PID-bound bridge and signed target profile. Diagnostic instrumentation is test-only and
must not enter a production client.

## Delivery

- Current authenticated route: `/owner-downloads/client-native-bootstrap-test-4d-r1`
- Revision 1 ZIP size: 3,061,431 bytes.
- Revision 1 ZIP SHA-256: `8a75c39aab35bad9fddb9904818d8e98707e74492e91af4f844ee88168ae6e9c`.
- Original Test 4d remains archived at `/owner-downloads/client-native-bootstrap-test-4d`.
- One-time prerequisite: official Microsoft .NET 10 Desktop Runtime for Windows x64
- Public download: disabled
- Account and Game ports: not public
