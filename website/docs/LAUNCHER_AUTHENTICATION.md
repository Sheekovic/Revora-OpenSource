# Launcher Authentication and Client Bootstrap

> Historical research boundary: this flow is not part of the production cinematic launcher.
> Launcher 1.0.6 and later perform signed repair and verified process launch only; username and
> password entry remains inside the genuine game client. The server-side Account/Game security
> described here is retained, while the launcher-authentication client is dormant for the isolated
> Manual Playable H1 evidence tooling.

The exact-hash transition and Owner Test 4 acceptance boundary are recorded in [AUTOMATIC_ACCOUNT_BOOTSTRAP.md](AUTOMATIC_ACCOUNT_BOOTSTRAP.md). Legacy UI credential autofill is permanently rejected.

The user enters a game-account name and password only in Revora Launcher. The launcher posts them to the fixed HTTPS endpoint `https://admin.playrevora.com/launcher-api/v1/session`. The password is used for modern PostgreSQL-backed verification and is never placed in `Conquer.exe`, its UI, command line, environment, configuration files, logs, reports or signed profiles.

The server returns a cryptographically random 256-bit launch ticket once. PostgreSQL stores only its SHA-256 digest. The row binds it to the account, Revora realm, random launcher session, signed client build ID, intended launch ID and a 60-second expiry. The raw ticket remains in launcher memory and is zeroed when the attempt ends. Persistent login is not implemented; a later refresh credential must be revocable and protected with Windows DPAPI. Remembering only the account name is permitted.

## Required flow

1. Verify the signed profile and every client file.
2. Authenticate through the fixed HTTPS endpoint.
3. Issue one bound, digest-stored launch ticket.
4. Use a proven deterministic bootstrap to enter the genuine Account state without legacy credentials.
5. Associate the exact launch and ticket through access-controlled, short-lived local IPC.
6. Atomically consume the launch ticket and send the evidenced Account response containing a short-lived Game handoff.
7. Atomically consume the Game handoff.

Steps 1–4 now have first-party implementation and static evidence. Owner Test 4 is the first genuine Windows gate for step 4 and the encrypted 276/1086 boundary. The bridge atomically consumes the launch ticket and sends only controlled error response 1055; it does not yet issue a successful legacy Account response or Game handoff. Steps 5–7 therefore remain fail-closed beyond this bounded bridge association.

Revora rejects credential autofill, `PostMessage`, synthetic keyboard input, clipboard automation, runtime injection, APC/remote-thread loading, DLL sideloading, executable-memory modification, elevation, Windows services, persistence, antivirus exclusions, reusable client secrets and embedded database credentials.

API failures are generic and rate limited. Ticket material is never logged. Response policy constrains its size, lifetime and fixed realm. The endpoint and realm are not editable. Public 9958/9959 stay closed.

The next evidence gate is Owner Test 4 observation of the deterministic transition and exact PID-bound 276/1086 connection. If that passes, the following milestone is the genuine successful Account response and single-use Game handoff. Revora does not fall back to password forwarding or UI automation.
