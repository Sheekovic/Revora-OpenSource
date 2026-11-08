# Simple Identity and Grants L2.1 Owner-Test

The private Owner-Test launcher uses `https://auth.playrevora.com/` with platform certificate validation. Repository and production defaults remain disabled. Registration and login occur only over HTTPS; passwords are transient and never written by the launcher. The returned revocable opaque session is protected with current-user Windows DPAPI, rotated on launcher restoration, and deleted on logout.

The launcher resolves and verifies the chosen DX8/DX9 direct profile before requesting a grant. The request binds the grant to realm `revora-owner-test`, profile `dx8` or `dx9`, and client contract `patch-7630-owner-test`. The grant remains memory-only. L2 does not deliver it to or inject it into the proprietary client, so this milestone does not claim authenticated world entry.

## UI states

The existing visual hierarchy is preserved. The Owner-Test build displays a compact identity panel with username, transient password, Register, Login, and Logout controls plus signed-out, signing-in, signed-in, authentication-failed, and session-expired messages. Start Game remains fail-closed when Owner-Test identity is enabled and no valid launcher session exists.

## Owner procedure

1. Extract the private package outside the client.
2. In Windows PowerShell run `./Install-PlayRevoraOwnerTest.ps1 -ClientRoot 'E:\Conquer\7630-original'`.
3. Start `PlayRevora.Launcher.exe` from the client root. Confirm the `OWNER-TEST / NON-PRODUCTION` marker.
4. Register a synthetic Owner-Test account or sign in with one. Close and reopen the launcher to verify DPAPI-backed session restoration/rotation.
5. Resolve Automatic, DX8, or DX9 and press Start Game. The already-proven direct launch contract remains `Conquer.exe blacknull`; no `play.exe`, `AutoP.exe`, Bridge, WARP, or D3D9On12 path is used.
6. Logout and confirm the launcher returns to Signed out. Server-side tests separately prove one-time grant redemption and replay rejection.

Rollback uses the package's `Rollback-PlayRevoraOwnerTest.ps1` against the same client root. It restores only files recorded by the transaction journal.
