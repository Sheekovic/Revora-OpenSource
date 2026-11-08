# Final Revora client structure

| Path | Purpose |
|---|---|
| `Env_DX9/` | Approved patch-7630 DX9 engine and generated `Revora-Conquer.exe`. |
| `Env_DX8/` | Retained engine fallback; not selected by the Revora DX9 release. |
| `ani/`, `c3/`, `data/`, `data2/`, `map/`, `sound/`, `video/` | Proprietary gameplay assets retained unchanged. |
| `ini/`, `Script/`, `font/` | Game configuration, UI scripts, and fonts. |
| `AutoPatch/`, `Download/`, `PATCHDATE/` | Legacy engine update data retained only where runtime-required; PlayRevora owns future updates. |
| `LauncherAssets/` | PlayRevora artwork/runtime assets only; login loaders and bridges are excluded. |
| `RevoraUpdates/` | Atomic update state and rollback journals created by PlayRevora. |
| `Revora-Server.dat` | Protected patch-7630 server list containing the sole `Revora` entry at `79.110.49.34:9958`. Runtime ETW proves the client resolves its patched relative base name from the client-root working directory. |
| root launcher files | Lightweight WPF launcher configuration and signed manifests. |

Research, dumps, logs, PDBs, owner-canary integrations, and staging tools are
not part of the player package.

The canonical input inventory contains 82,358 files totaling 7,392,495,025
bytes. Its immutable pre-change manifest is retained outside the player tree.
