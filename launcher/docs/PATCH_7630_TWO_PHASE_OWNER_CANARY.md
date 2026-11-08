# Patch 7630 two-phase owner canary

This private Owner-Test flow deliberately requires two credential entries.
The launcher submits the first entry only to the fixed validated HTTPS origin
`auth.playrevora.com`, immediately clears its password control and temporary
request buffers, and retains only a 60-second one-use ticket in memory. The
native client receives the second entry through its unchanged login UI.

The hash-gated DX9 integration redirects only `170.33.9.35:12000` to the Revora
Account endpoint. It holds the exact 472-byte opcode-1942 `send`, hashes those
unchanged final wire bytes, and sends only the digest plus a fresh correlation
identifier over a unique per-launch named pipe. The pipe is restricted to the
current user, and the launcher verifies the connecting PID is the exact process
it launched before completing a fresh challenge response.

The launcher redeems the in-memory ticket over HTTPS. Only an explicit ready
response releases the original buffer through the original `send` function;
all IPC, timeout, TLS, cancellation, malformed-response, or launcher-exit paths
fail closed. Other sockets, endpoints, and packet sizes pass through unchanged.
No password, ticket, full digest, packet body, authorization identifier, or
world handle is written to launcher or integration diagnostics.

This is SHEEKOVIC-only, Owner-Test-only, and not a permanent or public UX.
