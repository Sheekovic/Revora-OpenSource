# Revora Launcher Architecture

## Security boundary

`Revora.Launcher` is a Windows x64 WPF composition/UI project. `Revora.Launcher.Core` contains platform-neutral signed-profile, hashing, safe-path, staging, rollback, and process-launch policy.

Owner-test and future public launcher publishing is framework-dependent. The one-time prerequisite is the supported Microsoft .NET 10 Desktop Runtime for Windows x64. Revora does not repackage that runtime. The intended eventual public port remains .NET Framework 4.8.1 WPF with a sub-10 MB target, after authentication/bridge/update contracts stabilize.

The launcher runs `asInvoker`, installs no service or scheduled task, invokes no shell, performs no injection, and never loads an arbitrary DLL. It launches only `Conquer.exe` from a contained client directory after every critical file matches a signed profile. `UseShellExecute` is false and the only compiled argument is `blacknull`; no UI accepts arguments or endpoints.

The endpoint policy is fixed to `playrevora.com`, Account TCP 9958 and Game TCP 9959. These values are public configuration, not secrets or a server security boundary.

## Signed profiles and updates

The envelope signs the exact UTF-8 payload bytes using ECDSA P-256/SHA-256 and identifies `revora-launcher-production-v1`. The production public key is deliberately not invented in this milestone, so the developer artifact has no approved profile and launch remains disabled. An offline private-key ceremony and embedding only its public key are release gates. No Authenticode publisher identity is fabricated.

Profiles require patch 5165, fixed endpoints, one exact `Conquer.exe`, unique normalized forward-slash paths, bounded sizes, lowercase SHA-256, and HTTPS download URLs. Validation rejects absolute paths, traversal, backslashes, links/reparse points, duplicate files, wrong endpoints, wrong keys, tampering, HTTP, and unexpected hashes.

Updates use new `.partial` files in a separate staging tree, bound response/file sizes, hash before activation, and directory renames for atomic activation. Activation retains a rollback directory and restores it if the staged move fails. Manifests contain no commands.

Owner tests use one sibling directory named `Revora Owner Test Workspace`. Initial creation verifies and copies the signed Test 2c tree once. A marker binds a random workspace identifier and baseline build to a SHA-256 checksum. Later tests verify every uncontrolled baseline file, restore only reviewed `Conquer.exe` and `Server.dat` rollback copies, apply exact-hash deltas atomically, and verify the complete target profile. Unmarked, reparse-point, case-conflicting or altered workspaces fail closed; the tool never recursively deletes an existing workspace.

After the first successful workspace creation, the launcher remembers only the selected source path in the current user's local application-data directory. A later small test package revalidates both roots and the cryptographic marker before reuse. This local convenience record contains no credential, ticket, key or endpoint and is never included in reports.

## Flash decision

Selected direction: keep the standalone launcher injection-free and make no Flash compatibility claim. The preferred next experiment is a first-party native login surface or legitimate client configuration that avoids the ActiveX dialog, after a clean client is available and the handoff boundary is established in an offline Windows environment.

Ruffle is useful for separately rendering/testing ActionScript 1/2 SWFs, but it is a desktop/web emulator and not an ActiveX COM control implementing the interface embedded by `Conquer.exe`. Replacing the in-process UI would need host integration and message bridging, not merely copying Ruffle beside the client.

No compatibility module is selected. Remote-thread/APC injection, general-purpose loaders, antivirus exclusions, registering/bundling an old OCX, and DLL search-order sideloading are rejected.

## Research classification

| Project | Pinned commit | License/class | Architecture and decision |
|---|---|---|---|
| `conquer-online/dragon` | `7594b699b08725daba78b60386a429da557b3a13` | BSD-3-Clause; reusable with attribution | Suspended process plus `QueueUserAPC` injection; detours Winsock and `LoadLibraryExW`; recommends elevation/exclusions. Research only; no code copied. |
| `darkfoxdeveloper/COFlashFixer` | `a1a7c462e2d2f63384068f9320255d5514cfe82e` | Root MIT, but a hook file attributes implementation to another author; provenance unclear | `CreateRemoteThread` injection and Detours redirect to local proprietary `Flash.ocx`. Rejected. |
| `darkfoxdeveloper/SmartConquerLoader` | `42c6610d643e4926dd0ea5b6b8d8d427102ac1a2` | Root MIT; bundled `ConquerCryptKey.dll` provenance unverified | Injected native hook, editable multi-server configuration, legacy .NET, bundled binary. Rejected. |
| `OpenConquerOrg/ConquerLoader` | `068898b70722a61dd3539028f458e5379b3fe9ad` | No repository-wide license; hook subtrees GPL-3.0 | Plugin system, injection/hooking, broad configuration and legacy dependencies. Restricted/unknown; rejected. |
| `ruffle-rs/ruffle` | `c246db7f857fe3e434cfb96d78d93f09a5a1fb15` | MIT or Apache-2.0 | Maintained Rust desktop/web Flash emulator supporting AS1/2/3; no ActiveX drop-in. Offline SWF research candidate only. |

Dragon’s BSD notice must be preserved if code is ever reused. This milestone copied no third-party code.
