# Owner gameplay-source intake

Reviewed 2026-08-29. The originals remain in the Git-ignored, mode-0700
`research/isolated/owner-source-20260829` intake and were extracted read-only on a
`nosuid,nodev,noexec` tmpfs. They were never built or executed.

| Material | Owner filename | Bytes | SHA-256 | Static result |
|---|---:|---:|---|---|
| Source archive | `ReVora.zip` | 10,793,402 | `060df1cf72e5be6701b354c6879a68d06c03e941f54deb5d5dfc91051774e9a2` | ZIP, 843 entries, 40,183,341 uncompressed bytes; no traversal, absolute paths, symlinks, case collisions, or unsafe separators |
| Database archive | `revora.zip` | 109,779 | `f5e88d2729306ca6946356a52b3964f9ddb245db9f1e40a0539e0e126ade3d63` | ZIP containing one 1,145,278-byte SQL export; no unsafe archive paths |
| Extracted SQL | `revora.sql` | 1,145,278 | `d912367110f5a18836234060fc43425b7d853ee441dbba461a2d99c90ae42f9c` | Navicat/MySQL 5.6 export; quarantined as sensitive |

ClamAV 1.4.3 (3,628,034 signatures) reported zero detections for both archives
and zero detections across 727 extracted scan targets. A clean malware scan is
not a provenance or license grant.

## Source inventory and provenance

The source is C# targeting .NET Framework 4.5 with x86 configurations. It has 672
`.cs` files and coherent patch-5165 markers: `WhoIsPacket` emits 5165, movement is
10005, and the action dispatcher routes jump action 137. Major areas are NPCs,
items, features/events, MySQL persistence, packet handling, maps, characters,
monsters, and world state.

No root license, COPYING, NOTICE, or file-level general license grant was bundled.
The owner's authorization covers owner-controlled original material. It does not
establish rights for third-party dependencies or copied fragments. Consequently,
Revora may adapt reviewed owner-authored behavior, while every dependency or
foreign fragment needs an independent grant before reuse.

The archive bundles 12 DLLs, five executables, and six PDBs. These include legacy
server outputs, an unknown `CO2_CORE_DLL`, an OpenSSL-era native library, MySQL,
NHibernate, ManagedOpenSsl, Antlr, Iesi, Remotion, and a Windows Firewall interop
assembly. None is built, loaded, copied, committed, or deployed. Known package
names do not establish the provenance of the bundled binary. All bundled binaries
are rejected from Revora.

## Architectural decision

The owner source becomes the primary gameplay-behavior donor, subordinate to the
exact approved 5165 client for wire behavior. Revora retains authentication,
AccountId/Character UID identity, PostgreSQL, cryptography, framing, network
limits, logging, service lifecycle, launcher, and signed configuration.

For the first slice, Revora adapted the donor's previous/current-position model,
eight direction deltas, DMap accessibility, 18-cell screen, symmetric view
membership, spawn on view entry, movement-before-removal on view exit, and action
135 removal. Exact IDs and layouts remain independently derived from client SHA
`902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`.
The exact action dispatcher has a distinct action-135 branch at `0x54edb9` that
looks up the packet UID, resolving the earlier cross-source despawn conflict for
this client.
