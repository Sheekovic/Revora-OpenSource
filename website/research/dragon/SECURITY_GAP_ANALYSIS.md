# Dragon Security Gap Analysis

Dragon is BSD-licensed reference material, but the reviewed implementation is not acceptable as a
Revora drop-in component.

## Rejected unchanged behavior

- wildcard discovery of `Dragon.Module.*.dll`;
- loading every matching DLL from the launcher's mutable working directory;
- no signature, publisher, exact-hash, client-build, or signed-profile enforcement;
- editable JSON Account/Game endpoints;
- `PAGE_EXECUTE_READWRITE` allocation for a DLL pathname;
- unbounded module-load polling;
- local `Flash.ocx` loading and the resulting obsolete ActiveX attack surface;
- website interception through a general `ShellExecuteA` hook;
- arbitrary runtime plugin extensibility;
- arbitrary trailing client arguments;
- elevation and antivirus-exclusion guidance;
- general process injection as the default product architecture.

The connect hook recognizes broad port ranges rather than a fixed client build/state and modifies
any matching IPv4 `connect` call in the process. Configuration parsing lacks a fail-closed signed
schema. Module paths are written into another process and loaded before the normal entry point,
which is behavior security products reasonably scrutinize.

## Future hardened Revora-owned bridge (design only)

A future review may consider a fixed signed x86 launcher component with exactly one compiled
allowlist of Revora-owned modules. It would verify the complete signed client profile, module
publisher signatures and exact hashes before process creation; expose no plugin discovery or
editable endpoints; omit Flash and website modules; use `PAGE_READWRITE` only for a fixed path;
enforce bounded deadlines and deterministic cleanup; and fail closed on architecture, integrity,
session, endpoint, or signature mismatch.

It would run as the same non-elevated user only after Windows proof, bind local IPC to the exact
launcher-created PID/session, authenticate messages, keep passwords out of the client, retain raw
launch tickets only in zeroed memory, and consume short-lived tickets once. Defender and
Malwarebytes acceptance would be explicit gates. Any Dragon-derived code would retain BSD
attribution and receive line-by-line security review.

This design is not implemented or approved. The manual playable baseline precedes it.
