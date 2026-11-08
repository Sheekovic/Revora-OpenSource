# Launcher dual-profile foundation (L1)

## Scope

L1 adds verified DX8/DX9 profile selection and a safe process lifecycle boundary. It does not implement authentication, Launch Grants, IPC, file download/repair changes, client modification, binary distribution, automatic fallback, or self-update.

## Client-root layout

The launcher is installed at the client root. Profile trees are independent and immutable:

```text
Client root/
  PlayRevora.Launcher.exe
  client-manifest.signed.json
  Env_DX8/
    Conquer.exe
    TQPlat.dll, TQAnp.dll, license.dll, TqNDProtect.dll, ...
  Env_DX9/
    Conquer.exe
    TQPlat.dll, TQAnp.dll, license.dll, TqNDProtect.dll, ...
```

Required files must stay inside their profile tree. The verifier rejects absolute, UNC, traversal, duplicate, and reparse-point paths. No DLL is copied or substituted between the root, DX8, and DX9 environments.

## Exact launch contracts

| Profile | Expected signed descendant | Profile directory | Launcher-supplied arguments |
|---|---|---|---|
| `dx8` | `Env_DX8/Conquer.exe` | `Env_DX8` | none |
| `dx9` | `Env_DX9/Conquer.exe` | `Env_DX9` | none |

Direct-profile L1.1 is now an explicit Owner-Test gate. Each signed profile supplies exactly one structured `blacknull` argument and its own environment directory as working directory. Normal PlayRevora launch no longer starts root `play.exe` or `AutoP.exe`. This contract remains unproven at runtime until the owner completes the test documented in `DIRECT_PROFILE_LAUNCH_L1_1.md`; Identity and Grants must not begin before that result.

`ProcessStartInfo.ArgumentList` carries arguments. Shell execution and automatic elevation are disabled. Only the process created by the launcher is tracked; concurrent duplicate launch is rejected. An exit during the two-second startup observation is reported as a startup failure. Later normal exit is monitored without triggering a profile loop.

## Selection policy

The persisted choices are Automatic, DirectX 9, and DirectX 8. Automatic prefers a verified, supported DX9 profile, then an eligible verified DX8 profile. Inconclusive capability detection requires visible user selection. A valid explicit override is never silently changed. Selecting Automatic resets the override.

Capability collection is deliberately minimal: Windows version/architecture and relevant system DirectX runtime availability. GPU identifiers, serial numbers, hardware IDs, and fingerprinting data are neither collected nor transmitted. GPU vendor is recorded as `Not collected` until a reliable privacy-preserving provider is justified.

Automatic fallback never starts another executable itself. The launcher now
recognizes the evidenced signed-bootstrap sequence in which approved DX9 exits
and approved DX8 is subsequently created by the same descendant chain. It
reports `DirectX 9 was unavailable; continued with DirectX 8.` and monitors the
surviving DX8 process. Explicit DX9 remains strict; it does not silently accept
DX8 unless a future explicit fallback preference is added.

## Manifest trust model

The version-1 signed client manifest covers launcher compatibility, issue/expiry timestamps, rollback metadata, profile IDs, executable and working-directory paths, structured arguments, requirements, required files, and SHA-256 hashes. Canonical bytes are emitted in fixed property order, with profiles/files sorted deterministically.

Signatures use ECDSA P-256 with SHA-256 from the .NET cryptography implementation. Only a SubjectPublicKeyInfo public key and key ID are configured in the launcher. No private key exists in this repository or its output. Tests create an ephemeral, explicitly test-only key in memory.

Verification fails closed for an unknown schema/algorithm/key, invalid signature, issue/expiry failure, unsupported profile or architecture, unsafe/duplicate path, contract drift, missing critical file, size mismatch, or SHA-256 mismatch. L1 does not retrieve a manifest or repair a file. Production must provision the reviewed public key and an offline-signed manifest before Play can become enabled.

## Rollback boundary

Rollback consists of reverting the launcher binaries/configuration and signed manifest as one signed release unit using the manifest's previous-version metadata. L1 never mutates either profile tree, so rollback does not restore or copy proprietary files. Preferences may be reset to Automatic independently.

## Future authentication seam

Authentication will attach after profile selection and verification but before `GameProcessCoordinator.LaunchAsync`. A future grant must bind to the selected profile and verified derivative executable hash. It must use a separate current-user/session IPC service and must not alter the structured launch contract. No authentication, token, credential, grant, or IPC code is present in L1.

## Validation

The deterministic foundation suite uses fake filesystem, clock, hardware, and process providers. It never executes a proprietary artifact. Static packaging validation must confirm that no `Conquer.exe`, TQ profile DLL, signing private key, or generated grant material is tracked or emitted by the launcher project.
