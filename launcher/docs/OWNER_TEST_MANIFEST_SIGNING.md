# Owner-Test manifest signing milestone

Owner-Test schema 2 separates a small signed CriticalLaunchSet from the complete Repair Catalog. Existing schema-1 packages migrate by treating only each signed profile executable as critical; an old full-scan receipt never gates launch.

## Scope and trust separation

This milestone provisions a disposable, explicitly non-production ECDSA P-256 Owner-Test root. It does not create or authorize the production signing key.

The checked-in launcher configuration remains:

- `buildChannel`: `production`
- `manifestKeyId`: empty
- `manifestPublicKeySpkiBase64`: empty

It therefore remains fail-closed and does not trust the Owner-Test root. The private Owner-Test package is generated outside Git with `buildChannel` set to `owner-test`, an `OWNER-TEST - NON-PRODUCTION` header marker, the Owner-Test public SPKI, and the signed manifest. It contains no private key or proprietary client file.

## Reviewed operator tool

`tools/PlayRevora.ClientManifestSigner` provides four explicit operations:

```text
keygen   <encrypted-private.pem> <public-trust.json> <key-id>
generate <client-root> <unsigned.json> <key-id> <manifest-version> <issued-utc> <expires-utc>
sign     <unsigned.json> <encrypted-private.pem> <signed.json>
verify   <signed.json> <public-trust.json>
verify-tree <signed.json> <public-trust.json> <client-root>
```

The tool:

- creates only P-256 Owner-Test keys whose ID contains `owner-test`;
- encrypts PKCS#8 private material with AES-256-CBC, SHA-256 and 250,000 PBKDF iterations;
- reads passphrases interactively without echo and rejects redirected secret input;
- calls the launcher's `SignedManifestCanonicalizer` directly;
- hashes files in their native client locations without copying them;
- rejects absolute, UNC, traversal, duplicate and reparse-point paths;
- partitions root, `Env_DX8` and `Env_DX9` entries without cross-profile substitution;
- writes outputs atomically and refuses to overwrite an existing output;
- reports redacted operator failures without paths, keys or stack traces.
- can perform a read-only full signed-tree verification on an isolated copy without executing a client binary;

For owners who do not want a passphrase workflow, `keygen-windows` creates an ECDSA P-256 key in the current-user Windows CNG key store with private export disabled. `sign-windows` asks CNG to sign by key name. No private-key bytes or passphrase are accepted by the tool, written to disk, placed on a command line, or packaged. This key is machine/profile-bound; loss of the Windows profile requires an Owner-Test trust-root rotation.

## Approved Patch-7630 manifest

The reviewed client tree contained 82,356 files and zero reparse points. Six mutable/runtime-generated files were excluded explicitly:

- `debug/2026_8_31.log`
- `Env_DX9/d3d9.dll`
- `Env_DX9/revora_d3d9.jsonl`
- `Env_DX9/revora_render.ini`
- `Env_DX9/debug/2026_8_31.log`
- `ini/GameSetup.Ini`

The signed immutable baseline contains:

| Partition | Files | Contract |
|---|---:|---|
| Client root | 82,190 | Relative files outside both environment trees |
| DX8 | 79 | `Env_DX8/Conquer.exe`, working directory `Env_DX8`, argument `blacknull` |
| DX9 | 81 | `Env_DX9/Conquer.exe`, working directory `Env_DX9`, no arguments |
| Total | 82,350 | Every file hashed in place |

The official-bootstrap revision supersedes the direct-launch contract above for the current private Owner-Test package. Its signed schema-2 repair catalog contains 82,336 immutable files (`root=82,176`, `DX8=79`, `DX9=81`) after applying the audited mutable/log exclusions. Its four-entry CriticalLaunchSet is root `play.exe`, root `AutoP.exe`, and the independent DX8/DX9 `Conquer.exe` files. Both profile argument arrays are empty because PlayRevora launches only `play.exe`; the official bootstrap owns all descendant arguments.

Private official-bootstrap package (not published):

- Path: `C:\Users\Administrator\Desktop\PlayRevora-OwnerTest-OfficialBootstrap-20260831\PlayRevora-Launcher-OwnerTest-OfficialBootstrap-7630.zip`
- Size: 13,593,988 bytes
- SHA-256: `4A20B6977C603EF153504249BCF720FF7DB98A83E7A79D1CAF7D53FF87A3E113`
- Signed manifest SHA-256: `48F3C77B051A7D2E55BE6E5E30D8809370AFEF3A14E2F8F6BCDED68A63D591EB`
- Signed no-update index SHA-256: `19208CF004B651955012A29E859E4D30D837B1C1F9AC80BDA0CA75272AC2CE42`

The manifest was regenerated because the signed launch contract materially changed; this is not a normal-launch full scan. It was signed with the existing non-exportable Windows Owner-Test key. No production key was created and no proprietary executable was run.

Owner-Test public fingerprint:

`9A432BC85C244358A68560943B641A18B05B8E6F12DA1CFECF3A9BB72E10D631`

Signed manifest SHA-256:

`E2ADFB3AAA81A70AAA0EA5FED38F1FDD38613E4A9388445A226954249339EF68`

The manifest contains relative client paths only and remains outside Git. An altered manifest version was rejected by ECDSA verification.

## Owner-Test package

The private package is reported directly to the owner and is not committed, released, deployed or uploaded. To test manually, extract its contents into the approved Patch-7630 client root so the launcher, signed manifest, `Env_DX8` and `Env_DX9` share the expected hierarchy. The first verification will hash the complete 7.39 GB baseline and may take several minutes.

The package must never be presented as a production launcher. Its public key authorizes only the disposable Owner-Test channel and its manifest expires on 2026-09-30 at 23:59:59 UTC.

### Verification incident

The first owner verification correctly rejected a post-signing replacement of `sound/login.mp3`. The owner confirmed that replacement as the intentional finalized Revora asset. The full evidence and verifier diagnostic correction are recorded in `OWNER_TEST_HASH_MISMATCH_INVESTIGATION.md`; the replacement manifest keeps the MP3 signed. Future manifest generation excludes only audited launcher/runtime mutable paths and legacy logs through the generic policy.

Private package metadata:

- Package size: 13,592,781 bytes
- Package SHA-256: `1E5B0E65280AAF3FDE00CFE03DC531BA380C2DAF0E59D1F155975FB461DD156C`
- Owner-Test launcher SHA-256: `AAE87CB309E9DDF31B618D0F2DC5D3A83C513F0B382539168707BC2846D917F2`
- Owner-Test launcher managed assembly SHA-256: `D88EAE182F049050079F9239E5FD51BA3EB1F5C03FD87A8E37BCD411B5D4FCE3`
- Public trust JSON SHA-256: `78BB88526639BDE691D353568AB86246DCF66730DB5B18281F7D69D803BBAAFA`

## Private-key retention and deletion

The encrypted Owner-Test private key is outside Git in a directory whose ACL permits only the current Administrator account and SYSTEM. Its passphrase was entered through a no-echo console and was not written to source, package, manifest or log.

Recommended disposition after owner testing:

1. Retain the signed manifest, public trust JSON, public fingerprint, package hashes and test record.
2. If no further Owner-Test manifests are required, remove the encrypted private-key file and any backup from the signing workstation.
3. On SSD-backed storage, ordinary overwrite/delete cannot guarantee removal from wear-levelled cells. Prefer destroying the encrypted volume/key protector or deleting the disposable encrypted VM/disk that held the key.
4. Clear Volume Shadow Copy, backup and synchronization copies only under the owner's normal retention procedure; never use broad recursive deletion commands.
5. If temporary retention is required, keep the encrypted key offline, preserve its restrictive ACL, store the passphrase separately under owner control, set a destruction date, and never reuse it for production.
6. Never commit, email, upload or include the private key in the Owner-Test package.

Compromise of this disposable key requires discarding the Owner-Test package and manifest, generating a new Owner-Test root with a new key ID, and rebuilding the private package. It must not affect the production trust root because none exists yet.
