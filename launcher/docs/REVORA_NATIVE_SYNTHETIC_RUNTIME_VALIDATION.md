# Revora native synthetic runtime validation

## Initial execution blocker

The earlier orchestration command combined a synthetic target, remote loader,
named pipe, and process-control operations. The Codex command broker rejected
that command before Windows created the binary. It produced no process exit
code, stdout, stderr, Windows Application event, Defender event, AppLocker
event, or Code Integrity event. This was a tool-policy rejection, not Windows
application control.

A direct local invocation was subsequently permitted. Its first run started
the x86 process but used a report path outside `RevoraDiagnostics`. Bridge path
validation correctly rejected initialization before opening the named pipe,
while the test harness waited indefinitely. The owned process was terminated;
the harness now cancels that wait after five seconds. No security control was
disabled or bypassed.

## Pinned build

- Source commit: `ff0f8513a2c2d63c243223939a8994cc91457276`
- Configuration: Visual Studio 2022 MSVC 19.44.35228, Win32 Release, `/W4 /WX`
- Target: PE32/x86, console subsystem, ASLR and NX enabled
- Source: `tools/RevoraBridge/src/synthetic_target.cpp`, `bridge.cpp`,
  `bridge_contract.h`, and `bridge.def`
- Target SHA-256: `147050EA9F4184F6BD4CBCDF2ACF90219A79D4E196BB39991BD31CE680FBD43A`
- Target size: 52,736 bytes
- Bridge SHA-256: `4F3871A75F8FE61EFD086CFB019EB5E8065E9DD276280CDBEE6F9F8D71966853`
- Authenticode: unsigned private test artifact
- Mark-of-the-Web: absent
- Imports: Windows runtime plus MSVC x86 runtime only
- Proprietary code/data and secrets: none

## Runtime result

Twenty-five consecutive runs completed with exit code zero. Each run used the
production `PatchJump`, trampoline builder, page-protection restoration, and
instruction-cache path. Two synthetic 8 MiB image regions were requested at
`0x20000000` and `0x30000000`; the documented RVA `0x0075D3B2` resolved within
each. The suite proved exact preimage success, one-byte mismatch rejection,
jump postimage, trampoline execution, stack balance, overwritten-instruction
effect (return value 42), one callback, rollback bytes, and duplicate-submit
rejection.

The same run kept the production report writer open while a concurrent reader
observed the flushed readiness event, parsed every JSONL line, then verified
clean shutdown persistence. There were no sharing violations or incomplete
JSON records.

Categorical output:

```text
diagnostic_concurrency PASS
login_state PASS
hook_trampoline_multi_base PASS
REVORA_NATIVE_SYNTHETIC_RUNTIME_VALIDATION PASSED
```
