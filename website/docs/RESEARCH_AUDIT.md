# Approved Repository Static Audit — Phase 1

This document preserves the original two-source audit. The 2026-08-28 multi-source extension (COPS documentation, Redux, and Dragon plus refreshed Comet/Heroes provenance) is recorded in `RESEARCH_SOURCE_MATRIX.md`, `PROTOCOL_EVIDENCE_MATRIX.md`, `FEATURE_MATRIX.md`, and `LEGACY_DEFECT_REGISTER.md`. The same no-execution quarantine boundary applies.

Date: 2026-08-28 UTC

## Acquisition boundary

Both approved repositories were cloned bare with `core.hooksPath=/dev/null`, a blob filter, and no checkout. Selected source/configuration/SQL/Markdown blobs were fetched for static reading. No repository code, hook, script, MSBuild target, archive, EXE, or DLL was run, loaded, built, restored, or extracted. Submodules were not initialized.

## Comet

- URL: `https://github.com/conquer-online/comet`
- 5065: `cc9c788a27fbace85325955a67b96b4e8e43843e` (2022-10-22 UTC)
- 5187: `18446a8a54b06e9f2c0d69d76f7fec46254910ab` (2022-10-22 UTC)
- License: BSD 3-Clause; copyright line “Spirited © All Rights Reserved”.
- Architecture: separate Account and Game hosts, Core/Network/Shared libraries, RPC handoff, database repositories, packet codecs, ciphers, and test projects. It is an educational login skeleton, not a playable server.
- Dependencies/build surface: legacy pinned branches target .NET 6 and reference Microsoft hosting/EF Core, Pomelo MySQL, BouncyCastle, xUnit, Docker/Compose, and CI configuration. No bundled EXE/DLL/archive paths were present in the inspected tree inventory.
- Relevant weaknesses to avoid: unbounded packet-processing channels; synchronous socket `Send`; no strict size checks visible before several codec reads; SHA-1 password hashing in its SQL design; configuration files expect DB credentials; and adjacent-patch protocol logic must not be treated as 5165 truth.
- Licensing conclusion: suitable as a reference and potentially as a source of adapted BSD-licensed code only with the notice/conditions/disclaimer preserved. Revora's phase-1 networking implementation is independent and contains no copied Comet code.

## HeroesCoServer

- URL: `https://github.com/DuneGameStudio/HeroesCoServer`
- master: `008b0e997d63778204bb6bb6c91b88908d2e4892` (2021-05-12 UTC)
- License: no license file or grant found. Treat all expression as all-rights-reserved/unlicensed.
- Architecture/completeness: monolithic .NET Framework 4.8 WinForms/console server with shared static world state, custom APM sockets, custom packet builders, direct MySQL access, large gameplay/event surface, unsafe/native crypto, and bundled resources. It appears feature-heavy but is not a trustworthy or cleanly testable baseline.

Observed critical/high risks:

- SQL dump includes privileged `ProjectManager` and `GameMaster` accounts with plaintext passwords `1111` and `2222`; application authentication compares recoverable/plain values.
- `Program.cs` hardcodes database name/user/password (`heroesconline`, `root`, `root`).
- GM/PM privileges and cheat commands are gated by hardcoded character names in packet/chat paths, not an authorization policy.
- Custom SQL builder and connection construction use string concatenation; injection and escaping correctness are high risk.
- The socket layer treats each receive callback as a complete logical message, uses a fixed 1024-byte buffer, and lacks correct general TCP stream reassembly.
- Many broad/empty catch blocks suppress network, parsing, and state errors.
- Global mutable `Hashtable`/world collections and non-cryptographic shared `Random` are used across connection/session flows without clear ownership; races, token collision/reuse, and stale sessions are likely.
- Game handoff lookup is not shown as atomic/single-use and may return null into unchecked state.
- Packet handlers index raw arrays at fixed offsets with inconsistent or absent length validation and no global maximum.
- Listener binding may choose a host interface and is not safely loopback-default.
- Native interop imports `msvcrt.dll`, `winmm.dll`, and `libeay32.dll`; the project references bundled ManagedOpenSsl/OpenSSL binaries.
- A pre-build event runs `powershell Expand-Archive Resources.zip`, which is prohibited and was not executed.
- A hardcoded symmetric password string `myKey123` appears in `Connection/decrypt.cs`.
- SQL dump includes real-looking email/game data and should be treated as potentially sensitive legacy data.

Risky repository blob inventory (Git SHA-1 object IDs, not trust attestations):

| Path | Git blob ID | Handling |
|---|---|---|
| `PacketHandling/NPCDialog.rar` | `6a632f693f58bf87a68161895a954fdea3c3f19e` | Not fetched intentionally; never extract |
| `Resources/Resources.zip` | `48378e1b75b37f8e5a1227decb4e68f43f8c4dd6` | Not fetched intentionally; referenced by dangerous pre-build event |
| `lib/Managed/ManagedOpenSsl.dll` | `447e37cec4086cc1e74b68167c51832125be8fa5` | Never load/run |
| `lib/Native/Native-x64/openssl.exe` | `67abcb896661b000a941566fe1920c38022927a3` | Never run |
| `lib/Native/Native-x86/openssl.exe` | `fbc7561fce2e95e65ba3a3a980fe945209108428` | Never run |
| x64 `libeay32.dll` / `ssleay32.dll` | `84ea405801c30e98951b8a16b49b40f51876dc9a` / `b5d28643661f08e1892f18da1c604ca793610fff` | Never load/run |
| x86 `libeay32.dll` / `ssleay32.dll` | `3bfa075366dad97d86284a368e0f2d902b5316c3` / `25218ac0fa9403b9a6dea97ed8c3b6b800cef080` | Never load/run |
| `Thumbs.db` | `294b8d5abd74d5536f15557e63499d64508d5b51` | Irrelevant binary metadata |

## Recommendation

Continue with a clean Revora foundation. Use Comet's licensed separation and adjacent-patch facts as reference, while correcting its backpressure, authentication-storage, framing-validation, and lifecycle weaknesses. Use HeroesCoServer only to form hypotheses about exact 5165 IDs/offsets. Do not copy it, restore it, build it, or use its DB/resources/binaries.

The pinned text evidence is not sufficient to implement 5165 authentication encryption safely. The smallest next evidence step is a lawful, hash-pinned authentic vanilla 5165 client plus approved static inspection and controlled loopback packet capture; client execution/modification requires explicit approval under current rules.
