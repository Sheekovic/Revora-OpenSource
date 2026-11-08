# Consolidation record

Date: 2026-11-08

| Source repository | Source branch | Source commit | New component |
| --- | --- | --- | --- |
| Sheekovic/PlayRevora-Launcher | `main` | `43687c6882a2fe3f698ce08058c0363d84f19db7` | `launcher/` |
| Sheekovic/Revora.Web | `Main` | `a0387afab29bd8b4280d035aab3fb378d080724b` | `website/` |
| Sheekovic/Revora6711-3D | `main` | `33bb6661291e72bb5ce51028c70226f45633fca9` | `server/` |

The machine-readable [source manifest](source-manifest.json) records counts,
intentional changes/exclusions, and hashes for the existing vendored libraries.
Imported files were compared against each source commit before staging.

## History and publication

This public repository starts from a clean snapshot of the three selected
default branches. It does not publish the old private histories or unused
branches, which also contain versions from before the current rewrite.
Full local Git mirrors retain the original refs and history; original GitHub
repositories remain unchanged. No original repository is deleted, renamed,
archived, made public, or force-pushed by this migration.

The original mirrors contain 89 launcher commits, 186 website commits, and
2,112 game-server commits across their available refs. A bounded pattern
scan examined 557, 1,418, and 16,472 unique blobs respectively without printing
credential values. Generic password matches included code variables and
synthetic fixtures; this is not an exhaustive security audit.

The unused compiled launcher graphics-proxy DLL is excluded. Existing Npgsql
and Microsoft support DLLs are retained with upstream license notices and
SHA-256 records. No proprietary client or runtime game tables are added.

## License

The owner requested public open-source publication and stated that the current
game-server code was rewritten. The root MIT license covers Revora-authored
material on that basis. Existing third-party notices, conditions, and copyright
remain in force. The migration does not certify historical ownership or grant
rights to missing external assemblies, game clients, or proprietary assets.

## Build qualification

- The relocated launcher builds with zero warnings/errors and its 110 existing
  foundation tests pass on the migration Windows machine.
- The local machine has .NET SDKs through 9.0.320. The website and Admin require
  .NET 10, so their build/test qualification runs in the monorepo CI.
- The full game-server build is blocked by existing external assembly
  dependencies. See [SERVER_BUILD.md](SERVER_BUILD.md).
- No production deployment or real-client compatibility test is part of this
  repository consolidation. Existing release endpoints are not migrated here.

## Existing component automation

Historical workflows under `website/.github/` are retained as source evidence.
GitHub runs workflows only from the root `.github/workflows/`; the new root
workflow supplies validation and component build/test jobs. Old deployment,
security-scan, and release workflows are not automatically activated.
