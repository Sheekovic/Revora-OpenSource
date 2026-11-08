# Owner-Test HashMismatch investigation

## Result

The Owner-Test package and verifier reproduced a legitimate fail-closed mismatch. The signed manifest, launcher package layout, and client-root resolution were not the source of the reported failure.

The complete 82,350-entry comparison found one mismatch:

| Relative path | Category | Expected size | Actual size | Expected SHA-256 | Actual SHA-256 |
|---|---|---:|---:|---|---|
| `sound/login.mp3` | SizeAndHashMismatch | 303,018 | 4,642,253 | `4DB0FF8009FDD98ADF7A0CFEE73FCB3EE7DA285DCEF2388E69C6F7C8663E0547` | `47954F0FC81375E74AA9ADE759F88B3ADC3BE41C6A43614920EF929D2F271B87` |

The current file's last-write time is 2026-09-01 04:22:32 UTC, after the first Owner-Test manifest was generated. The same replacement hash and size are present in the staging parent and disposable client copy. No launcher source path writes `sound/login.mp3`. The owner subsequently confirmed this is the finalized intentional Revora login-music customization. It remains immutable signed content in the replacement Owner-Test baseline at 4,642,253 bytes and SHA-256 `47954F0FC81375E74AA9ADE759F88B3ADC3BE41C6A43614920EF929D2F271B87`.

The remaining 82,349 signed files matched. Six unsigned runtime/mutable files were present and correctly did not affect verification:

- `debug/2026_8_31.log`
- `Env_DX9/d3d9.dll`
- `Env_DX9/revora_d3d9.jsonl`
- `Env_DX9/revora_render.ini`
- `Env_DX9/debug/2026_8_31.log`
- `ini/GameSetup.Ini`

The audit also found 14 legacy `*.log` files incorrectly included in the existing signed manifest (`AutoPatch/Update.log`, root `debug.log`, three root `p2plook*.log` files, `p2pTemp1.log`, and eight `ShopClient/log/shop_log_*.log` files). They all matched during this reproduction and are not part of the current mismatch list, but their mutability makes their inclusion a manifest-generation defect. `ini/DefaultGameSetup.ini` was reviewed as a shipped default asset; the mutable `ini/GameSetup.Ini` remains excluded.

No client file was modified or repaired during this investigation.

## Package and root-resolution audit

The private package contains eight files. All eight are outside the signed 82,350-file client set, so extracting the package over the client root overwrites no manifest-covered client file. The package files are the signed manifest, launcher configuration, launcher executable/managed/runtime metadata, PDB, and `LauncherAssets/d3d9.dll`.

The package's `launcher.json` specifies `gameRoot` as `.`. `MainWindowViewModel.Create` resolves this with `Path.GetFullPath(config.GameRoot, AppContext.BaseDirectory)`. Therefore, when the package is extracted into the full client root as documented, Root, `Env_DX8`, and `Env_DX9` resolve beneath that exact directory and remain separate. Running the package from a standalone package directory instead would fail with missing root files; it cannot produce the observed single-file hash mismatch.

## Diagnostics correction

The verifier previously discarded the failing path and exact mismatch category, then recorded only the high-level `HashMismatch` readiness reason. It now records a redacted `client_file_verification_failed` event with:

- profile partition (`root`, `Dx8`, or `Dx9`);
- normalized relative path;
- category: Missing, SizeMismatch, HashMismatch, SizeAndHashMismatch, or AccessFailure.

Expected/actual content, credentials, absolute paths, hashes, and exception details are not written to ordinary launcher diagnostics. User-facing behavior remains generic and launch remains fail-closed.

## Future manifest generation

The signer formerly excluded dated runtime logs with a one-time hard-coded list. It now uses a centralized mutable-path policy covering launcher package files, launcher preferences/logs/assets, every `*.log` file, runtime debug directories, Display Proxy state, and mutable game settings. This prevents a future manifest from signing launcher-owned or client-runtime mutable outputs. Immutable client assets such as `sound/login.mp3` remain covered intentionally.

The defective first Owner-Test manifest was not regenerated to conceal the investigation result. A separately versioned replacement manifest is created only after owner authorization, using the finalized client tree and the existing Owner-Test key.
