# WKConquer V308 identity boundary audit

Status: static research only  
Reviewed: 2026-08-31  
Source: sibling workspace `C:\Users\Administrator\Desktop\WKConquer-Revora`
(see its `PROVENANCE.md`)

## Decision

WKConquer V308 must not replace Revora Identity. Revora keeps PostgreSQL as the
authoritative store and keeps these identities distinct:

- `identity.game_accounts.account_id` (UUID): immutable account authority.
- `game.characters.character_id` (UUID): immutable character authority.
- `game.characters.legacy_uid` (integer): client-facing wire identifier only.
- A random, short-lived, single-use handoff credential: proof that the Account
  service authorized one connection to one realm.

The reviewed source may inform independently implemented gameplay behavior only.
Its account schema, password handling, login trust model, and INI persistence are
not migration targets.

## Static findings

| Severity | Finding | Evidence | Revora disposition |
|---|---|---|---|
| Critical | The game login handler accepts a client-supplied 32-bit `Key`, assigns it to `ConnectionUID`, and loads `Users/<Key>.ini`. Possession or guessing of a character UID is therefore treated as authorization. | `Source/Game/MsgServer/MsgLoginClient.cs`; `Source/Database/ServerDatabase.cs::LoadCharacter` | Never accept a UID as proof. Atomically consume a random handoff credential, obtain its server-side `AccountId`, then resolve the owned character. |
| Critical | Character creation also makes `Player.UID` equal the connection/account key, coupling account authority, session routing, character ownership, and filenames. | `Source/Game/MsgServer/MsgNewRole.cs`; `Source/Database/ServerDatabase.cs::CreateCharacte` | Preserve UUID account/character keys and allocate a distinct constrained numeric wire UID. |
| Critical | The legacy SQL dump stores passwords in a `varchar(16)` field and includes plaintext example credentials. | `cq.sql`, `accounts.Password` and seed rows | Do not import passwords. Require account recreation or a controlled password reset into Revora's versioned password hash format. |
| High | Account and password operations are exposed from in-game NPC handlers. | `Source/Game/MsgNpc/NpcHandler.cs` account inserts, lookups, and password updates | Registration and credential changes remain in Revora Web/Identity with rate limits, verification, session revocation, and audit events. |
| High | SQL is constructed by concatenating identifiers in multiple paths. | `Source/Client/GameClient.cs::AccountName2` and `AccountName` | PostgreSQL access remains parameterized through Revora persistence boundaries. |
| High | The database connection string and privileged database password are hardcoded. | `Source/FilesMt/DonateSystem.cs` | Secrets remain outside Git in access-controlled secret files; services receive least-privilege roles. |
| High | Duplicate-login handling disconnects an existing socket and mutates a global client pool without an authoritative transactional session claim. | `Source/Game/MsgServer/MsgLoginClient.cs` | Claim a durable game session atomically; reject or explicitly replace sessions according to one documented policy. |
| Medium | Character existence is inferred from an INI filename and mutable player state is spread over files. | `Source/Database/ServerDatabase.cs::AllowCreate`, `LoadCharacter`, and `CreateCharacte` | Character ownership and creation remain transactional PostgreSQL operations with unique constraints. |
| Medium | Login exceptions are broadly caught after substantial shared-state mutation. | `Source/Game/MsgServer/MsgLoginClient.cs` | Fail closed, log a redacted structured reason, and clean up through one connection-owned cancellation path. |

## Required authentication flow

1. Account service verifies the normalized username and versioned password hash,
   applies account status/rate-limit policy, and selects a realm.
2. Account service generates a cryptographically random handoff credential and
   stores only its digest with `AccountId`, realm, and a short expiry.
3. The client presents the protocol-compatible representation to the game
   service. The wire representation is not an account or character identifier.
4. The game service atomically consumes the credential exactly once, verifies
   realm, expiry, account state, and bans, and claims the live session.
5. The game service resolves `game.characters` by the authenticated `AccountId`.
   Only then may it send or accept the character's numeric `legacy_uid`.
6. Disconnect closes the owned session through one bounded cleanup path.

Any 7630 client-packet adaptation needs separate packet evidence. The V308 code
alone is research evidence and does not verify a layout against a clean client.

## Data migration rule

Legacy account rows are untrusted staging input. A migration may retain a legacy
numeric ID only as audited mapping metadata; it must never become an
authentication credential or PostgreSQL primary key. Plaintext/short legacy
passwords, security questions, machine identifiers, IP addresses, and example
accounts are not imported. Character state is accepted only through versioned,
field-by-field validators with explicit ownership mapping and rejection of
ambiguous or duplicate rows.

## First implementation gate

Before any newer class or gameplay feature is enabled, tests must prove that:

- guessed, expired, replayed, wrong-realm, and concurrently consumed handoffs fail;
- a valid handoff resolves only the character owned by its `AccountId`;
- a numeric wire UID cannot authenticate or select another character;
- character creation is atomic under duplicate-name and duplicate-account races;
- failed login and disconnect paths leave no live session or global client entry.
