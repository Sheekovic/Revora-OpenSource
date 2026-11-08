# Licensed Legacy Gameplay Source Matrix

Reviewed 2026-08-29. The exact approved patch-5165 client, SHA-256
`902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`, is the
wire-format authority. A legacy server is never an authentication, persistence,
networking, launcher, or security dependency.

## Pinned sources and reuse decision

| Source | Pin and deterministic source hash | License/provenance | Revora decision |
|---|---|---|---|
| Owner ReVora source | Owner-supplied `ReVora.zip`, SHA-256 `060df1cf72e5be6701b354c6879a68d06c03e941f54deb5d5dfc91051774e9a2`; static intake 2026-08-29 | Owner-authorized original material; no archive-wide license file; bundled third-party components remain separately restricted | Primary gameplay-behavior donor. Adapt reviewed source through Revora interfaces. Never execute or vendor bundled binaries. |
| COPS v7 Emulator | `https://gitlab.com/conquer-online/servers/cops-v7-emulator`, commit `84919e25e79eded39314ea50bfd787427eadd59f`, `git archive` SHA-256 `11b17a893109e7d4e851c5101e6bd0cabd561a01005fd7602054a70d36815052` | BSD-4-Clause-style grant, CptSky; bundled Lua/MIME/LZ4 notices apply only if those components are used | Primary permissively licensed gameplay donor. Port only reviewed rules behind Revora interfaces. Preserve the required CptSky acknowledgement. No third-party dependency was imported. |
| Redux 5065 | `https://github.com/conquer-online/redux`, commit `383c0f136e80d38475aa757b2f8a1fc2c24c6831`, archive SHA-256 `5600334d9144bc966ae8dd9bddcfdb903e46b86788d69c1817a95db6a103f74b` | No root or file-level license grant found; imported history is incomplete | Functional reference only. No code, data, or expressive structure may be copied. |
| HeroesCoServer | `https://github.com/DuneGameStudio/HeroesCoServer`, commit `008b0e997d63778204bb6bb6c91b88908d2e4892`, archive SHA-256 `271031ea4d63d1f810b709dd81a4ea8f6e24d9f2689b747e1b1eb437b232b92a` | No license grant; bundled binaries/archives and unclear provenance | Claimed-5165 compatibility reference only. Never built, executed, deployed, or copied. |
| Impulse 5165 | Historical archive reference `[2010-05-03] Impulse's Source [5165].rar`, 4.19 MB, linked from `https://www.elitepvpers.com/forum/co2-private-server/1669139-request-impulses-5165-source.html` | No authoritative VCS revision, stable archive hash, or reusable license could be established | Pinned only as a named historical reference. It is ineligible as a donor and was not downloaded or executed. Packet claims require exact-client confirmation. |
| Approved 5165 client | SHA-256 above; preserved disassembly generated from the approved forensic copy | Proprietary evidence; no redistribution | Final wire authority. Static instructions only; the executable is neither modified nor incorporated into server artifacts. |

## Feature/source selection

| Gameplay area | COPS v7 reusable design | Compatibility/reference evidence | Revora adoption boundary |
|---|---|---|---|
| Movement and jumping | `src/Network/msgwalk.*`, `msgaction.*`, `Entity/player.cpp`: authoritative UID, direction deltas, maximum jump distance, echo/broadcast | Exact client `0x58198f–0x581a31` (20/10005 builder), `0x581a57–0x581d5c` (handler); Heroes claims later ID 10005/action 137; Redux describes older 1005 | Implemented with exact 5165 codecs, AccountId/Character UID binding, bounded rates, PostgreSQL optimistic persistence. COPS rules were independently adapted. |
| Screen/entity synchronization | Owner `World.Spawns`, `WalkRun`, `Jump`: 18-cell view, symmetric membership, spawn/remove lifecycle; COPS independently corroborates the model | Exact client 10014 handler `0x589c9d`; action-135 branch `0x54edb9`; older sources conflict | Dynamic enter-range spawn, in-range movement/jump, leave-range action-135 removal, and symmetric membership are implemented and deterministic-tested. |
| Map collision and portals | `Map/mapdata.*`, `gamemap.*`: DMap cells, accessibility, altitude, passages | Exact client `GameMap.dat` maps documents 1002/1004 to `newplain.DMap`/`forum.DMap`; exact action 85 request and action 86 transition constructors; owner portal rows | **Implemented for the reviewed 1002↔1004 slice.** Both DMaps are hash-pinned, source proximity and destination accessibility are authoritative, and position persistence is atomic. Proprietary map data stays outside Git and is server-private. |
| NPCs and shops | COPS `Entity/npc.*`, `Network/msgnpc*`, `msgshop*`, database tables | Exact client constructors/handlers for 28/2030, 16/2031 and variable 2032; owner placement data and fixed-dialog behavior | **NPC placement, visibility and a fixed safe dialog are implemented for five reviewed map-1004 NPCs.** No script, reflection, dynamic compilation, raw SQL, shop or legacy runtime was imported. Shops remain deferred. |
| Inventory/equipment | COPS item entities, `msgitem*`, `msguserattrib*` | Older-patch Redux and claimed-5165 references conflict on offsets | Not imported. Revora schema/transactions and exact 5165 codecs required first. |
| Monsters, AI, combat, skills, drops | COPS monster/task/magic subsystems provide separable concepts | References contain extensive global state and patch-specific constants | No code imported. Plan is deterministic map actors, server-authoritative combat, and reviewed drop transactions. |
| Chat, teams, friends, trade, guilds | COPS provides bounded message/object separation | Redux/Heroes mix commands, globals, and weak authorization | Not imported. Revora authorization and moderation interfaces remain mandatory. |
| Quests and events | COPS task/Lua architecture shows feature boundaries | Bundled script engines and legacy content have separate licenses/provenance | No scripts/content imported. Revora-owned deterministic event interfaces required. |

## First vertical slice: imported versus rewritten

No repository was vendored and no legacy service was run. Revora independently wrote:

- span-based exact 20/10005 movement, genuine `0xffffffff`-tail 32/10010 action-137 jump, action-108 correction, and 10014 player-spawn codecs;
- the concurrent world-session registry and serialized per-client send boundary;
- AccountId-authenticated/Character-UID-authoritative movement and jump validation;
- PostgreSQL optimistic position persistence and reconnect restoration;
- per-session movement/jump rate limits and malformed/foreign-UID rejection.

The DMap cell structure, checksum expression, accessibility rule, 210-altitude rule,
and 17-cell jump limit remain adapted from licensed COPS and carry its notice. The
owner source is now the primary donor for the previous/current position and
symmetric 18-cell view lifecycle. Exact packet IDs/offsets and action-135 acceptance
come from the approved client, not from either donor's packet assumptions.

## Portals/maps/NPC vertical slice

The owner-authorized database export contributes only five portal placements and
five NPC placements for maps 1002 and 1004. Their canonical allowlisted JSON digest
is `16630c8952354e3bf1521e471c990275e83842e8d32ed1cb8de4fb930cec753b`.
V009 defines the provenance-bound catalog and V010 performs the idempotent import.
Legacy accounts, passwords, network history, payments, scripts, and player state are
excluded.

The exact-client authority is:

- portal request: general action `85`, with packed X/Y coordinates; server transition
  uses action `86` plus the destination map packet;
- NPC spawn: declared `28/2030`, UID at 4, X/Y at 8/10, type at 12, flags at 14,
  sort at 16;
- NPC activation: declared `16/2031`, UID at 4, data at 8, event at 12, type at 14;
- NPC dialog: `2032`, option/index at 10, action at 11, and the string pack at 12.

Owner behavior is used for the 18-cell visibility/proximity rule and the portal row
semantics. COPS corroborates the DMap accessibility and passage model. Revora owns
the state machine, PostgreSQL transactions, validation, fixed dialog content, and
bounded session lifecycle.

The private canonical `forum.DMap` uses the exact eight-byte `DMAP101\0` header,
whereas `newplain.DMap` uses the numeric 1004 header. Both keep dimensions at offset
268 and the same checked cell rows. The owner donor reads both by skipping the first
268 bytes. Revora accepts only these two headers and still requires the exact
per-map length and SHA-256 before parsing.
