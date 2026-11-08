# Database Selection — Initial Comparison

Status: proposed; selection should be confirmed before persistence implementation.

| Criterion | PostgreSQL | MariaDB |
|---|---|---|
| Data integrity | Strong constraint/type system and rich transactional semantics | Solid InnoDB transactions; compatibility settings require care |
| Concurrency | Mature MVCC, locking controls, and useful concurrency diagnostics | Mature InnoDB MVCC; behavior is familiar to legacy CO schemas |
| .NET provider | Npgsql is mature and actively maintained | MySqlConnector is the preferred modern community provider |
| Migration from legacy references | Requires deliberate schema/type translation | Lowest-friction translation from legacy MySQL schemas |
| Clean-domain modeling | Excellent fit for explicit constraints, enums/checks, JSONB where justified, and transactional invariants | Good fit, but legacy compatibility can tempt direct schema copying |
| Operations on Windows | Supported; local tooling and containers available | Supported; familiar MySQL-compatible tooling |
| Licensing consideration | PostgreSQL License; provider license must be recorded when selected | GPL server distribution considerations depend on deployment; connector license must be recorded when selected |

Recommendation: choose PostgreSQL for Revora. The project is intentionally not importing a legacy schema, and PostgreSQL's integrity/concurrency features better support explicit session, character-name, and world-state invariants. MariaDB remains a valid choice if operational familiarity or migration tooling becomes the dominant constraint.

Before adoption, record an ADR with the exact server/provider versions and licenses, review provider provenance, design least-privilege runtime/migration roles, and add parameterization/concurrency integration tests. No provider package is added in this phase.
