# Architecture

The verification-cache, strict local-version, signed patch-chain, safe extraction and rollback design is specified in [LAUNCHER_VERIFICATION_AND_TRANSACTIONAL_UPDATES.md](LAUNCHER_VERIFICATION_AND_TRANSACTIONAL_UPDATES.md). The legacy unsigned per-file update contracts must not authorize client readiness.

`MainWindowViewModel` coordinates independent services and contains no patching
or process logic. `DownloadService` is the only HTTP owner. It disables redirects
so an update origin cannot silently redirect downloads to another host.

`UpdateService` resolves ordered semantic releases from `channel.json`, requires
each patch to declare its predecessor, downloads a complete release to a
same-volume staging tree, validates sizes and SHA-256 hashes, then installs it
with rollback. The local version advances only after successful installation.

`ClientDoctorService` compares every master-manifest entry to local size and
SHA-256, repairing only missing or mismatched official files. It never deletes
files absent from the manifest.

`GraphicsSettingsService` writes `Env_DX9/revora_render.ini` atomically. Proxy
activation uses Windows' normal local `d3d9.dll` loading behavior; there is no
process injection, IAT hook, or memory patch in the launcher. Existing unknown
DLLs are never overwritten or deleted.

The dual-profile process coordinator resolves root `play.exe` and the client-root
working directory beneath the validated game root and starts the signed bootstrap
without arguments, shell execution, or elevation. It observes the signed descendant
chain and accepts only the approved DX8 or DX9 executable path; it never starts a
profile `Conquer.exe` directly or recreates the bootstrap's private session value.

## Trust model

- Every URL must remain HTTPS and on the configured origin.
- Every destination must remain under the configured game root.
- File length and SHA-256 are checked before installation.
- Redirects are rejected.
- Downloads are data; the updater never launches them.
- Production manifests should be signed offline. Hashes alone do not protect
  against compromise of the update web origin.
