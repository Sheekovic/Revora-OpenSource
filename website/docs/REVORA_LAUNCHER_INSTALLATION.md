# Revora Launcher installation and recovery

1. Install the supported .NET 10 Desktop Runtime if the package states it is framework-dependent.
2. Extract the complete launcher ZIP into either a new writable user-owned folder or an existing Revora 5165 client folder. Do not use a drive root, Windows, Program Files root or the user-profile root.
3. Keep `Revora.Launcher.exe`, `Revora.Bootstrapper.exe`, their runtime files and `Assets/` together.
4. Start `Revora.Launcher.exe` as a normal user. Do not use **Run as administrator**.
5. The launcher verifies the signed manifest, recovers interrupted changes, downloads a complete client when the portable folder is new, checks all critical files and uses cached verification only for unchanged passive files.
6. Use **Full Repair** to hash every managed file. Pause/Resume affects downloads, not an in-progress atomic replacement.
7. **Open Folder** opens only the resolved client root. **Diagnostics** writes a sanitized support ZIP to `Desktop/Revora Diagnostics`.
8. If a managed file is locked, close the genuine client and retry. Do not terminate the launcher while it says **Applying**; if power loss occurs, restart it and recovery runs before scanning.
9. To use another account, leave the first game open and choose **Open Another Account**. Each click opens one additional genuine client; account credentials are still entered only inside each game window. Close every running game before repair or update.

The reviewed owner/test ZIP contains `Revora.TestChannel`; launch `Revora.Launcher.exe` normally.
Release engineering may still use `Revora.Launcher.exe --test-channel`. A package without the exact
marker or switch remains on stable, and normal players must not create or modify channel markers.

For a disposable repair acceptance test, make a separate full copy or filesystem snapshot of the client. Never delete/corrupt the only working client. Delete one non-critical managed file and replace one disposable managed data file in the copy, start the launcher there, repair, and verify screenshots/settings remain. Restore or delete only that disposable copy after evidence is captured.

Rollback data lives below `.revora-launcher/rollback/<release-operation>/`. Do not manually combine rollback directories. Export diagnostics first, close the game, and use a reviewed release rollback manifest or support procedure.
