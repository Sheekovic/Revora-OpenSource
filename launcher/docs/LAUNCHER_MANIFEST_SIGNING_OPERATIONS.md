# Launcher manifest signing operations

## Trust boundary

The L1 verifier accepts schema version 1 and `ECDSA_P256_SHA256`. It imports an X.509 SubjectPublicKeyInfo public key, reconstructs canonical bytes with `SignedManifestCanonicalizer`, and verifies SHA-256/ECDSA using .NET cryptography. The signature is not part of the canonical payload.

Production signing is an offline release operation. The private key must never enter Git, a developer checkout, CI variables, launcher output, client packages, manifests, logs, crash reports or the web server. Prefer a non-exportable operator-controlled HSM key. If a file-backed key is temporarily unavoidable, generate it on an offline encrypted operator workstation, encrypt it at rest, restrict its ACL, maintain an offline recovery copy under two-person control, and securely destroy working copies after the ceremony.

Development and production trust roots must be different keys, different key IDs, different access controls and different signing hosts. A production launcher must not trust a development key.

## Deterministic signed bytes

The operator tool must reference the same launcher foundation assembly or a byte-for-byte compatibility test vector. It must not reimplement JSON canonicalization independently.

Canonical property order is:

1. `schemaVersion`
2. `manifestVersion`
3. `issuedAtUtc`
4. `expiresAtUtc`
5. `minimumLauncherVersion`
6. `rollback.previousManifestVersion`
7. `rollback.previousManifestSha256`
8. root required files sorted ordinally by relative path
9. profiles sorted ordinally by ID
10. within each profile, arguments in declared order, required files sorted ordinally by relative path, and architectures sorted ordinally

Hashes are uppercase hexadecimal. Times are normalized to UTC by the canonicalizer. The separate signer must use the .NET ECDSA P-256 SHA-256 signature representation consumed by `ECDsa.VerifyData`; an OpenSSL DER-encoded ECDSA signature must not be substituted without an explicit conversion and compatibility fixture.

Before production use, add a standalone operator-only signing project that accepts an unsigned manifest and private-key provider, calls `SignedManifestCanonicalizer.Canonicalize`, signs once, immediately verifies using only the exported public key, writes the signed manifest atomically, and emits only manifest/artifact hashes. It must redact private material and must refuse test key IDs for a production channel.

## Exact profile contracts

The signed profile identifiers and launch fields are fixed:

| Field | DX8 | DX9 |
|---|---|---|
| ID | `dx8` | `dx9` |
| Executable | `Env_DX8/Conquer.exe` | `Env_DX9/Conquer.exe` |
| Working directory | `Env_DX8` | `Env_DX9` |
| Arguments | `[]` (official bootstrap supplies descendant arguments) | `[]` (official bootstrap supplies descendant arguments) |
| Priority | lower than DX9 | higher than DX8 |
| Automatic fallback allowed | explicit manifest boolean | explicit manifest boolean |

At minimum, each profile's `requiredFiles` must contain its critical executable and its own native `TQPlat.dll`, `TQAnp.dll`, `license.dll`, and `TqNDProtect.dll`. Every remaining native/runtime dependency required by that environment must also be enumerated from the reviewed client baseline. Root, DX8 and DX9 paths must never be mixed or substituted.

Reviewed baseline critical entries are:

| Path | Size | SHA-256 |
|---|---:|---|
| `Env_DX8/Conquer.exe` | 18,534,160 | `F417A25AB8B037D37619E304618BE615F7645CAF39F760EFF98D988796EE4E1E` |
| `Env_DX8/TQPlat.dll` | 6,550,288 | `E46149B7ED0A3BC142FD22CADF1C30FF6A42204B08F6B5920221A2DF0E69815A` |
| `Env_DX8/TQAnp.dll` | 5,338,160 | `7D50029DA473CAFC19DB8A2F776E7989B78E8F39379521A1F137213F09307E32` |
| `Env_DX8/license.dll` | 7,017,376 | `12CCA47D01E343890E85B6FDEB834948818FE59645A2D78041F4B64672A2DBD6` |
| `Env_DX8/TqNDProtect.dll` | 1,117,088 | `C78CF9CD91E47429AD12F5CE639497762D485FC6D6B38E601871DE17B7DA66F2` |
| `Env_DX9/Conquer.exe` | 18,594,824 | `4889B6DAB71068D687A9757F64FB1033E9CE1C560714DF3B62634229776A65CE` |
| `Env_DX9/TQPlat.dll` | 6,550,288 | `E46149B7ED0A3BC142FD22CADF1C30FF6A42204B08F6B5920221A2DF0E69815A` |
| `Env_DX9/TQAnp.dll` | 5,338,160 | `7D50029DA473CAFC19DB8A2F776E7989B78E8F39379521A1F137213F09307E32` |
| `Env_DX9/license.dll` | 7,017,376 | `12CCA47D01E343890E85B6FDEB834948818FE59645A2D78041F4B64672A2DBD6` |
| `Env_DX9/TqNDProtect.dll` | 1,117,088 | `C78CF9CD91E47429AD12F5CE639497762D485FC6D6B38E601871DE17B7DA66F2` |

The root installation contains the launcher, its reviewed assets/configuration, and `client-manifest.signed.json`. Schema v1 binds `minimumLauncherVersion` but does not embed a hash for the root launcher executable or updater. Before public release, the release envelope/self-update trust design must add signed root artifact hashes; this is a documented provisioning prerequisite, not a reason to weaken profile verification.

## Key generation and provisioning ceremony

The next production operator steps are:

1. Establish an offline signing workstation or HSM and a two-person release approval record.
2. Generate a non-exportable NIST P-256 ECDSA production key in that boundary. Do not perform this step in the repository or on the public web server.
3. Assign a non-secret immutable key ID such as `revora-client-manifest-prod-2026-01`.
4. Export only the public SubjectPublicKeyInfo DER bytes and independently fingerprint them with SHA-256.
5. Transfer the public bytes through an authenticated channel and verify the fingerprint out-of-band with two operators.
6. Provision the Base64 SPKI public value and key ID into the production launcher configuration/build input. Review the resulting diff to prove no private material is present.
7. Generate a complete unsigned manifest from the immutable reviewed client baseline. Validate duplicate/path/profile rules locally before moving it to the signer.
8. On the offline signer, canonicalize using the shared L1 implementation, sign using ECDSA P-256/SHA-256, and immediately verify with the exported public key.
9. Return only the signed manifest, its SHA-256, the public-key fingerprint, key ID, issuance/expiry values and operator audit record.
10. On a clean online verification host, validate the signed manifest using the exact production launcher and compare all client file hashes. Do not enable Play until this succeeds.

Signed patch indexes and package metadata use the same channel-separated ECDSA P-256 trust model and explicit canonicalizers. Signing operators must validate source/target continuity, archive size/hash, the complete declared archive/target path set, target manifest digest and expiry/revocation state. Patch archives never contain or derive a signing key. See `LAUNCHER_VERIFICATION_AND_TRANSACTIONAL_UPDATES.md` for schemas and transaction gates.
11. Archive the signed release record and public metadata. Keep private-key backups offline under dual control.

No production private key was generated or installed during L1 owner-review preparation.

## Rotation, revocation and rollback

Routine rotation requires a launcher trust-store update that temporarily trusts both the current and next public keys. Release that launcher through the existing trusted channel, measure adoption, begin signing with the next key ID, then remove the old key in a later minimum-launcher-version release.

The current L1 configuration pins one key. Therefore multi-key rotation and emergency revocation require a reviewed launcher update before production provisioning. The next implementation should introduce an immutable key ring with explicit states (`active`, `retiring`, `revoked`) and a compiled emergency deny-list. A manifest signed only by a revoked key must fail even during rollback.

Emergency response:

1. Stop manifest publication and CDN promotion.
2. Revoke the compromised key in the launcher trust policy.
3. Generate a replacement key in a new offline/HSM boundary.
4. Publish a separately trusted launcher security update containing the new public key and revoked-key deny-list.
5. Re-sign a known-good client manifest with a new version and bounded validity.
6. Never reuse the compromised key, key ID, manifest version or rollback authorization.

Rollback metadata identifies the prior manifest version and SHA-256. Rollback still requires a valid, non-revoked signature, compatible minimum launcher version, and unexpired authorization. It cannot authorize unsigned files or cross-profile substitution.
