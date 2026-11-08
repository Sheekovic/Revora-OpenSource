# Owner Environment Differential Collector

The standalone tool in `tools/OwnerEnvironmentDifferential` compares the same approved Patch-7630 client run on the owner's Windows 11 PC and Windows Server. It is read-only, starts no proprietary executable, and makes no launcher, Bridge, network, security-product, or client modification.

Collection covers the bounded OS/display/runtime/file/process/module/event/compatibility/security/TCP-metadata/log/ACL/filesystem evidence requested by the owner. Absolute client and user-profile paths, user names, and the opaque official-bootstrap session argument are redacted. Module enumeration uses documented process-module metadata only; no process memory or dumps are read.

The observer follows every authorized DX8/DX9 `Conquer.exe` successor under the
same client root. It records each PID lifetime separately and waits through a
bounded successor window after the first process exits, so an official
DX9-to-DX8 fallback is not mistaken for total termination.

Application, WER, SideBySide, and Defender events are admitted only when their
timestamp is inside the exact observation window and their evidence names an
exact approved executable beneath the normalized tested client root. If the
event exposes a target process ID, it must also match a PID observed in that
run. A historical event for another tree—such as `Revora7630` while testing
`7630-original`—is excluded rather than attributed to the current run.

Comparing an older client tree is optional and non-blocking. Run this collector
separately against each tree, then pass the two resulting JSON files to
`Compare-RevoraOwnerEnvironments.ps1`. The comparator is read-only; do not copy,
repair, or modify the clean working tree as part of that investigation.

The comparator uses a deterministic score order: relevant owner-only block/crash events; critical/startup file differences; owner-only loaded modules; mitigation/AppCompat differences; security product differences; file zone/ACL differences; runtime differences; remote-session state; display differences; OS differences. It returns exactly one leading evidenced category or an explicit insufficient-evidence result.

The package is private and carries a detached signature under the existing non-production Owner-Test Windows CNG key. No production key is created and no binary is published.

## Version 3 corrections

Version 2 exposed two Windows PowerShell 5.1 defects. First, output from the `Safe` helper was pipeline-unrolled: a single active monitor became a scalar, and StrictMode rejected `.Count`. Version 3 array-normalizes every zero/one/many command boundary and every remaining count expression. Second, the relative output string was reused instead of being pinned to the invocation directory, and the final .NET write had no output preflight. Version 3 resolves the path once, creates and write-tests it before observation, writes temporary files, atomically renames both reports, rolls back failed commits, and emits stage-only failures without sensitive paths.
