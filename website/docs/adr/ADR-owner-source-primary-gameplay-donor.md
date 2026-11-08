# ADR: owner source as primary gameplay donor

Status: Accepted, 2026-08-29

Revora adopts the authorized owner source as its primary gameplay-behavior and
reference-data donor. The exact patch-5165 client remains final wire authority.
Revora's identity model, PostgreSQL persistence, secure credentials, Account/Game
authentication, DH/Blowfish, rate limits, signed launcher configuration, service
supervision, and audit boundaries remain the production foundation.

The donor is not vendored or executed. Reviewed subsystems are adapted behind
Revora interfaces in vertical slices. Its MySQL access, global mutable world,
broad exception suppression, plaintext/recoverable credentials, socket/auth
workers, firewall manipulation, compiled outputs, and third-party binaries are
rejected. Original archives and database rows remain ignored and quarantined.

The first gate is movement, jumping, map validation, player-screen lifecycle,
PostgreSQL position persistence, and reconnect restoration on map 1002. Later
gates proceed in this order: portals/map transitions; NPC visibility/dialogs;
shops/warehouse/inventory; equipment/item progression; monsters/spawns; combat
and drops; skills/progression; social systems; quests/events. Each gate requires
proven 5165 codecs, provenance review, bounded parsers, database isolation,
multi-client tests, production rollback, and genuine-client acceptance.
