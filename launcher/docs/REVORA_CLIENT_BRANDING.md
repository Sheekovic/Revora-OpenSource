# Revora client branding

Native opcode-1942 authentication remains paused and unresolved. This branding
release does not claim that login works.

| Visible area | Original | Revora result | Owner | Rollback |
|---|---|---|---|---|
| Executable FileDescription | Conquer Online | Revora Online | `Env_DX9/Revora-Conquer.exe`, RVA `0x011AF420` | Deterministic reverse patch |
| Executable ProductName | Conquer Online | Revora Online | `Env_DX9/Revora-Conquer.exe`, RVA `0x011AF564` | Deterministic reverse patch |
| Launcher title/icon/art | mixed owner-test launcher | PlayRevora production presentation | WPF resources under `Assets/` | Restore signed patch backup |
| Native window title | Conquer Online - Dragon`s Ascension | Revora - Dragon`s Ascension | `ini/UStrRes.ini`, key 10375 | Restore prior hashed file |
| Realm/server list | 213-row official list | one playable `Revora` realm | client-root `Revora-Server.dat` | Restore/remove from patch backup |
| Account endpoint | official endpoints | `79.110.49.34:9958` | encrypted client-root `Revora-Server.dat` | Restore/remove from patch backup |
| Normal-exit browser destination | `co.99.com/signout` fallback | `https://playrevora.com/en/features` | `ini/oem.ini` `[ExitPopUp] url`, plus the derivative's safe English fallback reference | Restore `ini/oem.ini` and original executable |
| Normal exit URL | `http://co.99.com/signout/` | `https://playrevora.com/en/features` | derivative string storage and two confirmed exit references | Deterministic reverse patch |
| Website/account shortcuts | NetDragon HTTP links | `https://playrevora.com/` and `/register` | root `1.url`, `2.url` | Restore prior hashed files |
| Shortcut icon | Conquer icon | approved PlayRevora icon | root `conquer.ico` | Restore prior hashed file |
| Login/loading art | packed proprietary archives | intentionally retained | engine data packages | Blocked pending a format-safe asset owner; archives are not blindly edited |
| Engine/runtime filenames | NetDragon/TQ names | intentionally retained | required runtime | Required engine identity, not player-facing Revora branding |

The executable patch manifest records original/replacement bytes and hashes.
The source asset for launcher branding is the existing approved Revora artwork
in `src/PlayRevora.Launcher/Assets`. `Revora-Server.dat` is deterministically
generated as gzip XML protected by 2048-bit RSA PKCS#1 type-1 blocks. The
private signing key remains outside Git; only its public modulus is embedded in
the derivative executable.

## Release-candidate file hashes

| File | Original SHA-256 | Revora SHA-256 |
|---|---|---|
| `Env_DX9/Conquer.exe` → `Revora-Conquer.exe` | `4889B6DAB71068D687A9757F64FB1033E9CE1C560714DF3B62634229776A65CE` | `93E5EF1946B9FE3161135E206DC5D5E2158E8379534E101A42B4127FA0E9092C` |
| `Revora-Server.dat` | generated | Recorded by the signed release manifest |
| `ini/UStrRes.ini` | `B4C36DDF7F25C5614101AC83C0B8A86CFFF965D4E3F07BB376005703C3515C87` | `2217B1A8EC4E75DBDA14C15BB1A6AA41CD420B960388844E0ED7D1D84063FB36` |
| `conquer.ico` | `63467C242434E685783F6EA4C3E106FF4AA4EDBA15AB17FF871AE1A5B521C4CD` | `4219AF37179B0A288C9E5FBF5973AD78B260FD609AE4754B43F10AA252431682` |
| `1.url` | `C7257F078460FBD8A26ABE286559BD2F3A6F2128EBCE7B1D7B64BE971DDD66C2` | `A967B0B4FDF1B7F5662D446EE69D84366FC61298CFF7BA37489A7B7332DF38B6` |
| `2.url` | `C6DA8A6993C5AB97760C145F0C01E345EFBC626335809CB878C16BDDC0BEEF7B` | `A531B7D4B030362F951F3D0D4574C197C5A7DE71AA4D9FDDF52E7E51C74FF5CC` |
| `readme.txt` | `2AEC10F239A533301058DFFE55719DBB4A672E5DFCB116FC171B82D273EC20D4` | `F177D6D99166F64410DEA4FA3B1F4E1134EB0F57200F04A10D9F5208403A9F2A` |
