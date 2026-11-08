# Owner source / Revora gameplay feature matrix

| Area | Current Revora | Owner source | Decision |
|---|---|---|---|
| Walking/running | Exact 20/10005 parser, UID/rate validation, authoritative persistence | `WalkRun`, direction movement, collision, peer view lifecycle | **Adapted now** behind `GameplayWorld5165`; modes remain exact-client bounded rather than trusting donor bytes |
| Jumping | Exact 32/10010 action 137 including the genuine final `0xffffffff` sentinel; distance/cell/rate checks and action-108 correction | `Jump`, packed target coordinates, DMap and screen lifecycle | **Adapted now**; exact-client wire layout is authoritative and unsafe special-map exceptions remain rejected |
| Map collision | Hash-pinned map-1002 and map-1004 DMap readers and elevation checks | DMap/GameMap readers plus special cases | **Adapted now**; only reviewed, hash-pinned map data is active |
| Player screen synchronization | Initial 10014 spawn and movement broadcast | Symmetric screen sets, view enter/leave, spawn/action-135 removal | **Adapted now** with concurrent session ownership and deterministic tests |
| Position persistence | PostgreSQL optimistic row version by immutable character ID | Periodic MySQL character save | **Revora retained**; donor SQL is rejected |
| Portals | Exact action-85 parser, source/map/proximity validation, atomic transition and action-86/map response for 1002↔1004 | Portal table and proximity transitions | **Adapted now** from five allowlisted rows; remote/forged use and blocked destinations fail safely |
| NPCs/dialogs | Exact 2030/2031/2032 codecs, stable UIDs, range/map validation and fixed safe dialog | Large NPC/dialog catalog and handlers | **Adapted selectively now** from five allowlisted placements; scripts and dynamic dispatch are rejected |
| Shops/warehouses | Not enabled | Custom shops, NPC shops, warehouse handlers | **Rewrite transactions**, adapt reviewed price/catalog data |
| Inventory/equipment | Minimal character appearance only | Items, equipment, composition, upgrades, gems | **Adapt rules; rewrite persistence/authorization** |
| Monsters/spawns/AI | Not enabled | Mob definitions, spawn loops and AI | **Adapt behavior behind Revora actors**; reject global loops and broad catches |
| Combat/skills/drops | Not enabled | Attack, skills, damage, drops, rebirth | **Adapt in vertical slices** with server authority and exact codecs |
| Chat/social | Initialization responses only | Chat, team, friend/enemy, trade, marriage, guild | **Rewrite security/state; adapt semantics** |
| Quests/events | Not enabled | NPC quests and event systems | **Manual content review then adapt**; no arbitrary scripts/plugins |
| Authentication/networking | Genuine Account/Game, DH/Blowfish, tickets, limits | Legacy socket/auth workers and recoverable credential assumptions | **Rejected**; Revora remains authoritative |
| Database | PostgreSQL, immutable AccountId/Character UID | MySQL/Navicat schema and mixed live/history data | **Rejected as architecture**; curated reference rows only through V009 tooling |
| Launcher/operations | Signed config, exact client checks, supervised unprivileged service | Legacy executables/config and firewall interop | **Rejected** |

No complete donor directory or third-party assembly is vendored. “Adapt” means a
reviewed Revora implementation behind Revora interfaces, with attribution where a
separately licensed dependency requires it.
