# ADR: Licensed legacy-gameplay adoption behind Revora interfaces

Status: Accepted, 2026-08-29

## Decision

Revora keeps its production identity, PostgreSQL persistence, Account/Game
authentication, DH/Blowfish transport, signed launcher configuration, service
supervision, and security boundaries. Gameplay will advance as reviewed vertical
slices by adapting permissively licensed COPS v7 concepts behind Revora-owned
interfaces and verifying every wire format against the exact patch-5165 client.

Redux, Impulse 5165, and HeroesCoServer are behavior/compatibility references only
unless an explicit reusable license and provenance are established. No complete
legacy repository, authentication system, database, socket stack, launcher, plugin
model, binary, or data archive becomes a Revora dependency.

## Why

Packet-by-packet invention produced working authentication and world entry but did
not scale to gameplay breadth. COPS offers coherent, permissively licensed map,
entity, visibility, movement, and gameplay boundaries. Its patch differs, so its
wire IDs cannot be copied; its tested gameplay concepts can be adapted while
Revora retains stronger identity, persistence, framing, cryptography, lifecycle,
and abuse controls.

## Adoption gates

Each subsystem requires license/provenance review, exact-client wire evidence,
bounded parsers, PostgreSQL transactions, multi-client deterministic tests,
malformed/replay/rate tests, attribution, and a genuine acceptance gate. Global
mutable worlds, plaintext credentials, MySQL/file persistence, legacy socket
framing, embedded admin commands, native crypto DLLs, arbitrary scripts/plugins,
and broad exception swallowing are explicitly rejected.
