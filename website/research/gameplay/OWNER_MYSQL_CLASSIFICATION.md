# Owner MySQL export classification and PostgreSQL plan

The quarantined dump has 49 tables. It uses 40 `AUTO_INCREMENT` declarations, 89
unsigned integer declarations, mixed `utf8`/`latin1` collations, and legacy zero
dates. No reusable ENUM, SET, binary/blob column, foreign key, trigger, or stored
procedure was identified in the static DDL inventory. Absence of relational
constraints in the donor is not carried into PostgreSQL.

## Data classes

| Class | Examples | Decision |
|---|---|---|
| Credentials, identity, recovery | accounts, reset-password data | **Never import**; contains historical plaintext/recoverable secrets and personal data |
| Payments/network/audit/session history | payment/transaction tables, vote/IP history, online/log tables | **Never import**; sensitive and not gameplay authority |
| Player state | characters, stats, skills/proficiencies, guild membership, furniture/houses, rankings | **Do not import**; must not overwrite AccountIds, UIDs, characters, ownership, or audit history |
| Curatable reference data | portals, NPC placement, custom shop catalog, item upgrade rules | **Eligible for reviewed staging**, not automatic production activation |
| Executable content/configuration | dialogs, buttons, quest/NPC source, event configuration | **Manual code/content review required** before any row is eligible |
| Operational aggregates | drop statistics, online counters, server configuration | **Reject as historical state** |

## Explicit type conversion

- `AUTO_INCREMENT` becomes an identity/sequence only for new Revora-owned rows;
  legacy reference IDs remain source-scoped keys.
- Unsigned MySQL values use checked PostgreSQL `integer`/`bigint` domains with
  non-negative constraints; no wraparound conversion is permitted.
- Case-insensitive player identity remains Revora's normalized AccountId model.
  Reference identifiers use ordinal numeric keys; future display strings require
  explicit collation/normalization decisions.
- MySQL zero dates are rejected or mapped to nullable timestamps only by a
  table-specific reviewed transform. They are never silently accepted.
- ENUM/SET would become checked lookup values; none is currently eligible.
- Blob/binary values require an explicit format parser and size limit; none is
  currently eligible.
- Donor indexes are reviewed semantically. Revora migrations add primary keys,
  checks, and foreign keys rather than reproducing Navicat output verbatim.

## Tooling and rollout

`V009__owner_gameplay_reference_catalog.sql` creates source-scoped, additive
reference tables without touching production accounts or characters. It grants
the game runtime read-only access. `Revora.OwnerGameplayImport` accepts only the
exact reviewed SQL hash and extracts only four numeric allowlisted reference
tables (`portals`, `npcs`, `customshop`, `itemadd`) into a new empty directory.
It never parses or emits identity/history rows. Database insertion remains a
separate restricted-migrator review step, so extraction cannot activate gameplay.

Rollout order is: isolated restore/test of V009; deterministic extraction; human
review and exact-client/data cross-check; parameterized import under
`revora_migrator`; row-count/hash validation; then a feature-specific Host change.
V009 is not required by the movement slice and must not be applied merely because
the migration exists.
