# Revora active native login path — Owner Test 4

## Corrected static boundary

The approved DX9 image is PE32 with preferred base `0x00400000`, size
`18,594,824`, and SHA-256
`4889B6DAB71068D687A9757F64FB1033E9CE1C560714DF3B62634229776A65CE`.

Test 3 disproved the earlier universal-dispatcher interpretation. Preferred VA
`0x00B5D3B2` (RVA `0x0075D3B2`, file offset `0x0075C7B2`) begins an MSVC
exception-frame prologue (`push 0x4A8` followed by the EH helper), but the only
normal-UI-side call identified is at `0x0065E69D` inside the alternate routine
starting at `0x0065E38D`. The verified hook never executed during the owner's
normal Login action, proving that branch was inactive for that mode.

The dominant normal Login path validates the credential control through
`0x00659F05`, then calls `0x0065EA32` at `0x006599AA`. That routine reaches
`0x0065E72A`; within it, `0x00659F05` modes 2 and 5 populate separate bounded
128-byte buffers before account setup and the normal request send. The Bridge
therefore hooks:

- submit RVA `0x0025EA32`, preimage `68 B0 00 00 00`;
- credential extractor RVA `0x00259F05`, preimage `68 30 01 00 00`;
- the existing account-only `send` IAT boundary after the native frame is
  finalized.

The submit and extractor hooks share one bounded attempt. Extraction is ignored
outside that attempt. The send gate remains restricted to the Account port,
expected envelope sizes, approved image/profile, and the current attempt.
Raw credentials and packets are never written to diagnostics.

## Owner-Test behavior

The package is private, signed with the existing Owner-Test trust material, and
transactional. Select explicit DX9, enter the disposable website-registered
account, and press Login exactly once. Preserve
`RevoraDiagnostics/native-login-owner-test-4.jsonl` and launcher diagnostics
before retrying, repairing, rolling back, or relaunching.

The expected successful sequence is submit entry, bounded credential capture,
account request classification, HTTPS authorization, pending authorization,
one native request release, native opcode 1637, Game connection, and one-time
world-grant consumption. Every emitted event is categorical and secret-free.

Test-2 and Test-3 artifacts remain immutable historical evidence.
