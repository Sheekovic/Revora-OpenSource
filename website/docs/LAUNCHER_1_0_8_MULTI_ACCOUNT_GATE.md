# Revora Launcher 1.0.8 multi-account test gate

## Scope

Launcher 1.0.8 permits a player to start another verified `Conquer.exe` explicitly while one or
more game clients are already running. It does not perform authentication; each account still
authenticates inside its own genuine client window. The launcher tracks every process it starts,
keeps `Open Another Account` available, and returns to `Ready` only after the last tracked game
exits. One initialization cycle still claims at most one automatic launch.

The patch safety boundary is unchanged. A running game continues to block application of managed
file repairs. Full Repair is disabled while a launcher-tracked game is active, and the existing
process guard is still evaluated inside the exclusive patch lock before any live-file mutation.
The launcher never terminates a game process.

## Evidence

- Implementation commit: `88110e09d39e19581418b2f9e588bf4929235b45`.
- Release-evidence commit: `f0ce940e28b5df21ea40caa25c03400c1725a715`.
- Windows workflow: [run 33376104060](https://github.com/Sheekovic/Revora/actions/runs/33376104060).
- Portable launcher/core/bootstrapper Release builds: zero warnings and zero errors.
- Cross-platform launcher security/state suite: 43 passed, 0 failed.
- Genuine Windows WPF/process fixture: two verified client processes launched; independent exit
  tracking passed; final exit returned to `Ready`; automatic launch did not repeat.
- Windows repair-safety fixture: an additional account launch was permitted while a game existed,
  but a managed executable repair remained blocked and made no live-file change.
- Exact candidate Defender result: zero detections; engine `1.1.26070.7`, signatures
  `1.457.425.0`; no exclusions added.
- Exact candidate ClamAV result: zero detections; ClamAV `1.4.3/28106`.

## Immutable test artifact

- URL: `https://playrevora.com/downloads/Revora-Launcher-1.0.8-test.zip`
- Bytes: `4,259,072`
- SHA-256: `f7406da78ef055d08b940a7819836a8bb8e58e177cc5ec0a51398d656193f412`
- Delivery: HTTPS `200`, range `206`, `Content-Type: application/zip`, safe attachment filename,
  `Accept-Ranges: bytes`, and test-only `private, no-store` caching.

The owner accepted the genuine multi-account flow. Stable Launcher 1.0.8 was promoted without a
runtime rebuild on 31 August 2026. The accepted test ZIP remains immutable and available for audit.

## Stable publication

- Stable URL: `https://playrevora.com/downloads/Revora-Launcher-1.0.8.zip`
- Stable bytes: `4,258,914`
- Stable SHA-256: `3c7db8a4cf077b05c456bf485fec13a362ebf7b5e3a5e5bc04a81e31308e0720`
- Stable launcher manifest: `https://playrevora.com/launcher/v1/stable/manifest.json`
- Stable manifest SHA-256: `fee4872fc121204c7cdec9f3b25e44f90b89298bba4d2803734a2e458a58f4b3`
- Stable release identity: `51650108-0000-4000-9000-000000000001`
- Stable package contains no test marker and carries the explicit stable-channel record.
- All ten manifest objects returned HTTP 200 with their declared sizes and SHA-256 values before
  manifest cutover.
- Public package delivery passed HTTPS GET/HEAD, byte-range HTTP 206, attachment filename, content
  length, immutable caching, and exact hosted-byte verification.
- English and Arabic download pages display version 1.0.8, the stable URL, exact package size, exact
  checksum, and multi-account support without test or internal release terminology.

The previous 1.0.7 package remains immutable for rollback. The client desired-state manifest and
gameplay client files were not changed. Existing 1.0.7 launchers discover the signed 1.0.8
self-update; the genuine game continues to authenticate each account inside its own client window.

Production backup and rollback root:
`/srv/revora-backups/launcher-stable-1.0.8-20260831T095500Z`.

Authenticode remains unavailable, so the executable files are not Authenticode-signed. Content is
authenticated by Revora's signed ECDSA release manifest. Microsoft Defender and ClamAV passed the
exact stable ZIP; Malwarebytes evidence is unavailable.

## Owner test

1. Close every Revora game and launcher window.
2. Back up the existing launcher files, then extract the complete 1.0.8 test ZIP into the existing
   writable Revora client folder.
3. Verify the ZIP SHA-256 above and run `Revora.Launcher.exe` normally.
4. Let verification finish. Confirm exactly one game opens automatically and log in to account A
   inside that client.
5. Return to the launcher and select **Open Another Account**. Confirm exactly one additional game
   opens; log in to account B inside it.
6. Confirm both characters can remain online and move independently.
7. Close only account B's game. Confirm account A remains connected and the launcher still offers
   **Open Another Account**.
8. Close account A's game. Confirm the launcher returns to **Ready / Play Revora** and does not
   launch another client automatically.
9. With a game open, confirm **Full Repair** is unavailable. Do not deliberately corrupt the
   working client; the Windows fixture already proves live repair remains blocked.

Stop on a duplicate automatic launch, a disconnected first client, inability to open the second
client, or any repair/update attempt while a game is running.
