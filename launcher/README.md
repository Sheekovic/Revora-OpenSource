# PlayRevora Launcher

Standalone .NET 8 WPF launcher providing sequential updates, SHA-256 repair,
Revora Display Proxy settings, and safe direct client launch.

## Build

```powershell
dotnet restore PlayRevora.Launcher.sln --locked-mode
dotnet build PlayRevora.Launcher.sln -c Release --no-restore
```

Edit `launcher.json` in the publish directory before deployment. Place the
reviewed x86 proxy at `LauncherAssets/d3d9.dll` and set `proxySha256` to its
SHA-256. The launcher refuses to overwrite or remove an unrecognized
`Env_DX9/d3d9.dll`.

## Update layout

```text
/updates/channel.json
/updates/master_manifest.json
/updates/baseline/...
/updates/v1/manifest.json
/updates/v1/files/...
/updates/v1.1/manifest.json
/updates/v1.1/files/...
/updates/v2/manifest.json
/updates/v2/files/...
```

Every patch declares its exact predecessor through `requiresVersion`. Files are
downloaded into a same-volume staging directory, size/hash checked, then applied
as a release transaction with rollback. `version.json` advances only after the
whole release succeeds. Repair replaces only missing or mismatched official
files and does not remove unknown local files.

Production should additionally sign `channel.json` and manifests with an
offline release key. HTTPS and file hashes protect transport/content integrity,
but an origin compromise could otherwise publish malicious, correctly hashed
content. Downloaded files are never executed by the updater itself.

## Generate the master manifest

```powershell
dotnet run --project tools/PlayRevora.ManifestGenerator -c Release -- `
  "C:\path\to\assembled" "manifests\master_manifest.json" 1.0.0 `
  "Env_DX9/d3d9.dll" "Env_DX9/revora_render.ini" "version.json"
```

The Display Proxy, its mutable INI, and the launcher's local version marker are
excluded because they are managed separately from the immutable client mirror.
