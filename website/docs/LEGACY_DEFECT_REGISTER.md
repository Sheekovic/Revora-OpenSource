# Legacy Defect Register

Static findings are risk indicators, not claims that a repository maintainer acted maliciously. Severity reflects the consequence if the pattern were inherited by Revora.

| ID | Source evidence | Defect/risk | Severity | Revora control |
|---|---|---|---|---|
| L-001 | Redux `Network/LoginServer.cs::Process_AuthAccountExPacket` | One duplicate LoginServer implementation creates a missing account with the submitted plaintext password and `GM` permission. | Critical | Registration disabled by default; separate use case; role assignment never implicit |
| L-002 | Heroes `Program.cs::Main`, SQL dump; Redux `DbAccount`, login handlers | Hardcoded `root/root`, plaintext/recoverable password comparison, privileged seed accounts. | Critical | Protected secrets; memory-hard password hashes; no seeded/default admin credentials |
| L-003 | Heroes `MySql/MySqlCommand.cs::Set/Where/And/Insert` | User strings are concatenated into SQL and executed without parameters. | Critical | Provider parameterization only; SQL construction tests and review gate |
| L-004 | Heroes `Connection/Connection.cs`, Redux `NetworkClient.Receive` | Receive callbacks pass each socket receive as a logical message; partial/coalesced stream state is incomplete or unsafe. | Critical | Existing tested accumulator, maximum lengths, per-connection framing state |
| L-005 | Heroes `AuthWorker.DataHandler` | Exact-length 276 check and raw fixed-offset reads do not establish general stream framing; unknown packets may be dumped. | High | Never log auth payloads; validate declared length/type before decode |
| L-006 | Heroes `AuthWorker.KeyedClients`; Redux counter/timestamp handoff | Predictable/shared tokens, non-atomic lookup/consume, unclear expiry cleanup. | Critical | Existing random 256-bit, digest-indexed, realm-bound, expiring, single-use abstraction; wire adapter later |
| L-007 | Redux `Managers.PlayerManager`; Heroes `Game.World` | Static mutable global players/maps/items and mixed synchronization create stale sessions and races. | High | Session registry and zone actors with explicit ownership |
| L-008 | Redux `Threading/*`; Heroes `System/MyThread.cs`, `Program.cs` | Busy/polling loops, `Thread.Sleep`, thread-per-subsystem scheduling, no cancellation/drain. | High | Async cancellation, bounded queues, deterministic scheduler, AMP lifecycle |
| L-009 | Redux repositories and socket code; Heroes throughout | Broad or empty catches swallow persistence/network/state failure. | High | Catch only at recovery boundaries; structured safe errors; component failure propagation |
| L-010 | Redux `NetworkServer.Prepare`; Heroes listener setup | Public/wildcard binding defaults. | High | Explicit loopback-only Development endpoints; public exposure gate |
| L-011 | Redux `GameServer.Process_TalkPacket`; Heroes chat/packet handlers | Player chat and administrative commands share parsing/authorization paths. | Critical | Console/admin capability plane never reachable from player packets |
| L-012 | Heroes `WalkRun.Handle` | Mode 9 path calls movement/spawn inside the branch and again afterward, apparently moving twice. | High | One validated movement transition per command; deterministic coordinate tests |
| L-013 | Redux `[1005] Walk.cs`, `[1010] GeneralData.cs`; Heroes builders | Allocated buffer size, declared size, and footer conventions are inconsistently expressed, inviting over-read/desync. | High | Codec-owned declared/wire length distinction and exact fixture tests |
| L-014 | Redux unsafe structs/MSVCRT; Heroes native OpenSSL/MSVCRT | Native pointer parsing and bundled obsolete crypto binaries increase memory-safety/supply-chain risk. | Critical | Managed span codecs; no native dependency without separate approval |
| L-015 | Heroes repository tree | Bundled EXE/DLL/RAR/ZIP, pre-build archive expansion, duplicate source copies, and unknown provenance. | Critical | Evidence-only bare quarantine; never extract/build/load |
| L-016 | Redux README; Dragon README/implementation | Loader injection, suspended-process APC injection, elevation, and antivirus exclusions. | Critical | No injection, AV exclusions, or elevation; client work postponed and separately reviewed |
| L-017 | Comet `Kernel` classes and `PacketProcessor`; RPC retry client | Global registries, unbounded processing, synchronous sends/retry sleep weaken isolation and backpressure. | Medium/High | Owned registries, bounded queues, async sends, cancellation/backoff |
| L-018 | Comet database schema/context | SHA-1-era password design and credentials in configuration patterns are obsolete. | Critical | Memory-hard password ADR and protected secret providers |
| L-019 | Redux `Repository` | Exceptions are printed, operations fail silently, and collection writes create one transaction per item. | High | Unit-of-work transactions, typed errors, batch invariants |
| L-020 | All server sources | Packet offsets are distributed through handlers/builders, with incomplete or contradictory length validation. | High | Central `Protocol5165` codecs generated only from confirmed evidence |
| L-021 | COPS README | Documentation derives partly from leaked binaries and only reaches patch 5066; accuracy does not imply 5165 identity. | Medium | Treat as licensed older-patch evidence; require authentic 5165 fixture |
| L-022 | Heroes/Redux feature breadth | Large apparent completeness can hide duplicated rules, hard-coded values, and untested conflicts. | High | Vertical milestones with acceptance tests; no feature-by-feature porting |

## Rejection rule

Any proposal that depends on a pattern above is rejected unless it independently specifies ownership, bounds, cancellation, validation, authorization, parameterization, secret handling, deterministic tests, and a compatible license. Passing a happy-path client test alone is insufficient.
