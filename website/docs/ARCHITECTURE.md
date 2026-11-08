# Revora Architecture

## Boundaries and dependency direction

Revora uses a modular clean architecture. Dependencies point inward:

```text
AccountServer ----> Application <---- GameServer
     |                  |                |
     v                  v                v
Networking       Protocol5165       Networking
     |                  |                |
     +----------> Application <-----------+
                        |
                        v
                      Domain

Persistence implements Application ports and may depend on Domain.
Protocol5165 contains wire codecs and depends only on Domain where shared value
types are necessary. Domain depends on no other Revora project.
```

- `Revora.Domain`: identities, immutable value objects, and domain invariants.
- `Revora.Application`: use cases, ports, authorization policies, and session contracts.
- `Revora.Protocol5165`: verified patch-5165 framing, codecs, constants, and packet models.
- `Revora.Networking`: protocol-neutral TCP framing, connection lifecycle, limits, and bounded transport queues.
- `Revora.Persistence`: database adapters and migrations. The database provider remains undecided pending a documented PostgreSQL/MariaDB comparison.
- `Revora.AccountServer`: loopback-default account/login process and composition root.
- `Revora.GameServer`: loopback-default world process and composition root.
- `Revora.Host`: Linux/AMP composition root that validates configuration, coordinates persistence and the two server runtimes, owns process signals/stdin, reports layered health, and performs bounded startup/shutdown. It contains no gameplay or packet behavior.
- Test projects mirror unit, integration, and protocol-fixture concerns.

Server hosts are composition roots and must not contain domain rules. Networking must not know packet semantics. Protocol codecs must not access persistence or global state.

For AMP deployments, `Revora.Host` is the sole operating-system process. Account and Game become hosted runtime components rather than independently supervised child processes. This avoids shell wrappers and child-process ambiguity while preserving project boundaries through explicit component interfaces.

```text
AMP Generic Module
       |
       v stdin/signals/stdout
  Revora.Host
   |    |    |
   v    v    v
Persistence Account Game
 adapter     runtime runtime
```

READY is a conjunction: configuration and permissions valid, persistence healthy, Account listener ready, Game listener ready, and internal dependencies healthy. Any failed prerequisite prevents the marker.

The explicitly selected `InMemory` Development/Test adapter provides isolated volatile persistence and two protocol-neutral loopback TCP listeners. It accepts bounded connections, discards received bytes without interpretation, reports listener/connection health, stops acceptance and closes its stateless raw connections as its bounded drain operation, and surfaces unexpected completion to the coordinator. The default `Disabled` adapter remains fail-closed. This harness is lifecycle evidence only and is not a 5165 protocol implementation.

## Account and login flow

The intended flow, pending verified 5165 wire evidence, is:

1. A bounded account connection is accepted on an explicitly configured loopback endpoint.
2. Framing and length limits are applied before packet parsing.
3. Authentication traffic is rate-limited. Credentials are handed to an Application authentication port and are never logged.
4. A password verifier checks a modern, salted password hash. Plaintext is never persisted.
5. On success, the account server issues a short-lived, single-use, cryptographically random handoff token bound to the selected realm/account.
6. The response supplies the configured game endpoint and opaque handoff data in the verified 5165 format.

The protocol-independent handoff implementation stores only a SHA-256 lookup digest of each random 256-bit token, consumes a token on every validation attempt, enforces realm binding and expiry, and redacts its string representation. The Development in-memory adapter is non-durable; a future database adapter must preserve the same atomic consume semantics.

The exact client password transformation and packet layouts remain protocol-research questions. No guessed implementation is allowed.

## Game connection flow

1. The game server validates framing and rate limits before decoding a verified connect packet.
2. It atomically consumes the account-server handoff token.
3. The connection is attached to one owned game session.
4. The session performs character selection/creation, entry, map snapshot, movement validation, and visibility updates through Application use cases.
5. Disconnect cancellation stops producers, drains or rejects bounded work, releases presence ownership, and disposes transport resources.

## Session ownership

- Each accepted socket has one connection object, one cancellation source, one bounded inbound queue, and one bounded outbound queue.
- A session registry has a single explicit owner and exposes atomic claim/release operations. Duplicate login policy is an Application decision.
- Map/zone actors own mutable spatial state. Cross-zone work is message-based; callers do not mutate another zone's collections.
- Connection and session lifetimes are distinct: a token is consumed before a connection may claim a session, and a failed claim cannot leave partial presence.
- Shutdown cancels acceptance, then connections, then owned workers, with observable completion and no fire-and-forget tasks.
