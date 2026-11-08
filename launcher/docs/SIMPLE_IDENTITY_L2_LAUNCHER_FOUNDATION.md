# Simple Identity L2 launcher foundation

The launcher is the HTTPS authentication agent. The feature remains disabled and is not wired to normal launch in L2. It stores no password. A successful session may be persisted only through Windows DPAPI (`CurrentUser`); logout revokes remotely and deletes that local blob.

The selected and verified `dx8` or `dx9` profile is resolved before requesting a game grant. Grants are never placed in process arguments or files. Client delivery is an intentionally unimplemented seam pending signed derivative-client evidence.

Endpoints require HTTPS and normal platform certificate validation. Login, session rotation, logout, and profile-bound grant request methods are available. The server uses one revocable opaque session token rather than a long-lived JWT or duplicated access/refresh secret. Authentication values are excluded from launcher diagnostics. Production configuration keeps `simpleIdentityEnabled=false` until the HTTPS host and database integration gate passes.

Direct launch remains authoritative: `Env_DX9/Conquer.exe blacknull` or `Env_DX8/Conquer.exe blacknull`, with its profile directory as the working directory. The missing legacy named-pipe log is non-blocking for the owner-proven login-screen contract.
