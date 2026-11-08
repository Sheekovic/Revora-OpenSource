# Patch-7630 credential boundary

## Scope and binary identity

This analysis is limited to the approved DX9 `Conquer.exe` (18,594,824 bytes,
SHA-256 `4889B6DAB71068D687A9757F64FB1033E9CE1C560714DF3B62634229776A65CE`).
Addresses below use image base `0x00400000` and are valid only for that image.

## Root cause

The former credential hook at RVA `0x00259F05` was misclassified. Its six-way
mode switch reads server configuration. Direct callers pass modes 0 through 5;
the mode branches reference `PicServerIP`, `PicServerPort`, `BindServerIP`,
`BindServerPort`, `ServerIP`, `ServerPort`, `BGPServerIP`, `BGPServerPort`,
`KeyServer`, and `AccountBindServer`. It is not a username/password accessor.

The old submit hook at RVA `0x0025EA32` is a downstream login routine. In the
actual Login command method beginning at RVA `0x0025969C`, the control reads have
already occurred before the call to that routine at VA `0x006599AA`:

1. VA `0x0065974F`: account control at `dialog+0x0BB8` is read by
   `0x00984B8D`; the result is published to `dialog+0xA64C`.
2. VA `0x006597AB`: password control at `dialog+0x0EB0` is read by
   `0x00984B8D`; the bounded result is passed immediately to `0x0065F084` and
   its temporary string is destroyed at `0x00659800`.
3. VA `0x0065989D`: mode 2 of `0x00659F05` reads server configuration.
4. VA `0x006599AA`: the downstream routine at `0x0065EA32` is called.
5. VA `0x0065EB04`: that routine invokes mode 1 of `0x00659F05`, then enters
   the Account-message path.

This explains the canary exactly: capture was armed after both genuine control
reads, and the installed hook waited for unrelated mode 2/mode 5 calls that do
not occur in that downstream path.

The frozen ordinal-55 evidence is consistent with this order: the password is
published before CMsgAccountEx and its ordinal-9/ordinal-55 construction. The
producer observed at RVA `0x008FFA96` is downstream transformation/publication,
not evidence that the server-configuration mode switch supplies credentials.

## Corrected boundary

The integration hooks the explicit Login command entry at RVA `0x0025969C`,
preimage `68 0C 01 00 00`. It arms one capture before invoking the original
method. That method is the bounded command path that performs both native UI
reads and calls the prior downstream login routine; it is not a keystroke,
focus, clipboard, or background-monitoring path.

The static import patch observes the single call boundary at RVA `0x002597E8`,
preimage `51 50 8B CE E8 93 58 00 00`. At that point the two genuine getters
have completed, `ECX` is the bounded username C string and `EAX` is the bounded
password C string, immediately before both are passed to the client's own
password-publication routine. This avoids the later ordinal-55 transformed
password state and avoids depending on temporary MSVC string-object layout.

The stub preserves flags and all general registers, passes exactly those two
pointers to the normally imported Revora DLL, executes all displaced
instructions, and returns to the next original instruction. While and only
while the explicit Login command is armed, the DLL accepts one pair on the same
thread. It validates bounded NUL termination, then disarms when the later
proof-construction call takes the pair.

The first paired-boundary build incorrectly copied the displaced relative
`CALL` encoding unchanged into the new section. From the trampoline it resolved
to RVA `0x011E8948`, not the genuine publication routine at RVA `0x0025F084`.
The deterministic patcher now recalculates that displacement and records the
resolved target in its manifest; tests require the relocated call to resolve
exactly to `0x0025F084`.

If capture or HTTPS completion fails, proof construction now emits a bounded,
syntactically valid but intentionally non-authorizable Revora proof. AccountHost
therefore returns the existing native status-1 response instead of leaving the
client indefinitely on the account-login screen. It cannot consume an existing
authorization because its challenge identifier and proof are zeroed.

Duplicate, malformed, cross-thread, partial, timed-out, and exceptional attempts
fail closed and zero both bounded buffers. Only a complete pair can reach HTTPS.
No value, value length, hash, pointer, or content is written to diagnostics.

## Evidence confidence

- Function entry preimages and instruction/call ordering: statically proven
  against the approved executable.
- Control identity: proven by the two direct getter calls and their subsequent
  account/password publication paths.
- Corrected state-machine behavior: covered by the Revora-owned synthetic
  runtime and foundation tests.
- Genuine-client behavior after the correction: not yet tested; installation
  and an owner canary require separate authorization.
