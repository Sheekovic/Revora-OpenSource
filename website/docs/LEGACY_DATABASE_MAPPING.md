# Legacy Database Mapping

Legacy schemas are untrusted evidence, not migration scripts. No legacy SQL is executed and no legacy table is imported directly into Production.

| Legacy concept | Revora destination | Required transformation | Status |
| --- | --- | --- | --- |
| Account row | Game account compatibility record | Deduplicate identifiers; discard plaintext/reversible credentials; require secure credential reset or a reviewed adapter | Blocked by 5165 authentication evidence |
| Character row | Character aggregate | Validate realm ownership, names, ranges, map and coordinates | Research mapping only |
| Item/equipment | Future inventory aggregates | Validate identifiers and ownership; reject orphaned/duplicated rows | Out of current milestone |
| Guild/social rows | Future bounded social aggregates | Resolve membership conflicts and permissions | Out of current milestone |
| Server configuration | Reviewed deployment configuration | Never import passwords, paths, GM flags or public binds | Manual review only |

Every future migration has a provenance record, source hash, repeatable dry run, rejected-row report, reconciliation counts and rollback. Unknown columns are not silently preserved. Legacy numeric primary keys are stored only as quarantined import references, not authoritative identifiers. SQL dumps, research databases and extracted credentials remain ignored and outside tracked paths.
