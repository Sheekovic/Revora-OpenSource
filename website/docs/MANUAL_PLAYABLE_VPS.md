# Manual Playable Baseline — private VPS path

The M2-H1 owner evidence proved Account authentication and 32/1055 but did not reach Game because the exact client rejects every `127/8` Game address before `connect()`. The approved correction is server-side: the reviewed configuration-only client remains hash-identical and receives the numeric VPS IPv4 in signed `Server.dat`; 1055 advertises the same numeric IPv4 and port 9959. The address is supplied only in protected operational configuration and is intentionally absent from this repository document.

`Revora.Host` now owns the genuine protocol lifecycle. Its bounded OwnerTest adapter:

1. accepts 276/1086, decrypts in memory, rate-limits by source, and validates through `PostgreSqlGameDataStore`;
2. generates an eight-byte cryptographically random wire ticket (the exact legacy field width), stores only SHA-256, binds it to the fixed realm, and expires it after 30 seconds;
3. sends 32/1055 with the reviewed numeric Game endpoint;
4. performs the unchanged canonical DH/Blowfish exchange and validates 28/1052 plus `TQClient`;
5. atomically consumes the ticket once; and
6. sends the genuinely proven encrypted 1004 login-channel `NEW_ROLE` response;
7. validates `104/1001`, creates the character atomically, returns registration-channel `ANSWER_OK`, and retains the same authenticated Game/cipher session; and
8. sends `1006` on that same connection, then answers action 74 with the starter-map action and `1110`. Existing-character logins use entrance `ANSWER_OK` followed by the same world-entry sequence.

The `NEW_ROLE` layout is identical in the pinned BSD-licensed Comet 5065 and COPS v7 sources: packet 1004, login/entrance channel 2101, string list `SYSTEM`, `ALLUSERS`, empty suffix, `NEW_ROLE`. Genuine Windows evidence now proves that response through the visible character-creation screen. The exact registration and first-entry evidence is documented in `CHARACTER_CREATION_AND_FIRST_WORLD_ENTRY.md`.

The Windows launcher contains no Account/Game server or protocol probe. It verifies the complete donor and signed target profile, transactionally updates only `Conquer.exe` and `Server.dat` in the existing owned workspace, launches the exact approved executable, and writes process-only sanitized telemetry.

## Exposure and rollback

The service runs as the non-login `revora-game` OS identity with a matching peer-authenticated PostgreSQL login role that is a member of the non-login, least-privilege `revora_game` role. Systemd removes capabilities, denies new privileges, restricts address families, protects the host filesystem, and permits writes only under `/srv/revora-game/data` and `/srv/revora-game/logs`. `MemoryDenyWriteExecute` is intentionally unavailable because the .NET JIT requires executable anonymous memory; all other reviewed sandbox controls remain active.

Before a run, nftables must contain no unrestricted 9958/9959 accept. Insert only two exact owner-source `/32` TCP accepts followed by explicit drops for those ports. After the single run, delete those four temporary rules and stop `revora-game.service`. Never change the firewall default policy.

Rollback stops only `revora-game.service`, removes only its temporary nftables rules by recorded handles, restores `/srv/revora-game` from its narrow backup, and verifies that no 9958/9959 listener remains. Website, Admin, nginx, PostgreSQL, AMP and ADS are not restarted or modified.

## Evidence boundary

DH/Blowfish bytes are unchanged and genuinely proven against the exact owner client. The production onboarding target is one Account login and one Game connection: create one character, retain the cipher/session, and display that character in starter map 1002 without relaunch or another credential prompt. Any earlier failure is reported as a server boundary and does not trigger alternate protocol candidates.
