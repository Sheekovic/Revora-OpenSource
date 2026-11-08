# Credential lifecycle and threat model

Credentials originate only from the native Login action. The accessor hook
invokes the original function first, accepts only modes 2/5 and validated
capacities, then copies into fixed attempt buffers. They are never passed to
the launcher, command line, environment, IPC, report, or native Account TCP.

At the classified final Account request boundary, HTTPS uses normal Windows
certificate and hostname validation with bounded connect/send/receive/total
timeouts. Authorization must be durably ready before the unchanged native
request is released. Buffers are cleared with `SecureZeroMemory` after success,
rejection, timeout, exception, reconnect, shutdown, or unload.

Threats addressed: unknown executable/profile, hook preimage mismatch,
duplicate/replayed attempt, cross-socket request, malformed length, TLS failure,
authorization race, stale handle, reconnect, and diagnostic disclosure. Every
case fails closed. The integration does not defeat a fully compromised client
process; its purpose is bounded compatibility, not an anti-cheat boundary.
