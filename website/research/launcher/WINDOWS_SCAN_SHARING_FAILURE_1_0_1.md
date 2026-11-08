# Windows scan sharing failure in Launcher 1.0.1

## Owner evidence boundary

The genuine Windows report recorded launcher version `1.0.1`, release
`c12fb6d4-94c0-4dda-9918-516500000001`, successful signed-manifest validation, and an
`IOException`-class `filesystem-or-lock` failure during critical-file scanning. The 1.0.1 report did
not retain the relative path, HRESULT, native error, operation, retry count, stack, or handle owner.
Consequently the exact file and external owner from that historical run are **Unknown**. The stale
`version.dat` and 1.35 GB UI values are not evidence of the locked file.

## Reproduced defect

`DesiredStateScanner` opened a critical file once for `read-only-hash` with `FileShare.Read`. A
Windows handle opened with `FileShare.None` caused Win32 `ERROR_SHARING_VIOLATION` (32), surfaced as
HRESULT `0x80070020`, and aborted initialization without retry. A controlled Windows regression used
the manifest-relative `Conquer.exe` fixture and reproduced this exact failing layer. The test handle
is the proven owner in the reproduction only; it is not attributed to the historical owner run.

## Correction in 1.0.2

Read-only hashing now reopens the file for every bounded exponential-backoff attempt, retains the
original `FileShare.Read` integrity policy, never accepts cache state for an unreadable critical file,
and fails closed when retries are exhausted. Initialization, repair, and full-repair operations are
serialized so cancellation cannot leave a competing scan. Signed client manifests reject launcher,
bootstrapper, test-marker, and `.revora-launcher` control paths.

Diagnostics schema 2 records exception type, Revora-only sanitized stack frames, HRESULT, native
error, operation, safe manifest-relative path, attempt count, retry/exhaustion classification, and
state history. It continues to exclude absolute paths and authentication material.
