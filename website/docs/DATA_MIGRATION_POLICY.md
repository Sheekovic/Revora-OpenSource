# Data Migration Policy

1. Acquire only a legally authorized source snapshot and hash it before inspection.
2. Work in an isolated, access-restricted staging environment; never point migration tooling at Production first.
3. Inventory licenses, encodings, time zones, credential formats, privileged accounts and referential defects.
4. Define a versioned field mapping and deterministic validators. Reject rather than guess ambiguous identity, currency, privilege or ownership data.
5. Remove plaintext, reversible and weak legacy credentials. Users must establish a modern website credential through a verified recovery flow.
6. Dry-run into an empty staging database using a least-privilege importer. Produce counts and safe error summaries without personal or secret values.
7. Reconcile aggregates and security invariants, then obtain approval for a maintenance-window import.
8. Take and verify a transactionally consistent destination backup. Run a single reviewed migration version with automatic production migrations disabled.
9. Verify application readiness, reconciliation counts and audit events. Roll back on unexplained loss, duplicates, ownership conflicts or authorization drift.

Raw imports are retained only for the approved migration window and deleted through an approved data-retention procedure. They never enter Git, AMP templates, logs or release archives.
