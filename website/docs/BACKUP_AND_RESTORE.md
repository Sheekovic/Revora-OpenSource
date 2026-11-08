# Backup and Restore

## Scope and layers

Hostinger weekly VPS backups are infrastructure-level recovery points; AMP instance backups are application-instance archives. Their exact Hostinger coverage/retention was not discoverable from local files and must be confirmed in the Hostinger control panel. Neither is assumed to replace a consistent database backup.

A Revora instance backup should include non-secret `config/revora.json`, persistent application data, migration history, required non-reproducible state, a release/version manifest, and SHA-256 checksums. Include bounded operational logs only when needed for recovery/audit.

Exclude secret files when credentials are recoverable from the approved secret manager; otherwise protect them as a separate encrypted recovery item. Always exclude build caches, source/research repositories, temporary/update staging, disposable logs, private keys, Codex state, GitHub credentials, and raw database files.

AMP template rollback material is narrower than an instance backup. If a pre-existing local Revora template ever needs replacement, archive only `/home/amp/.ampdata/instances/ADS01/Plugins/ADSModule/DeploymentTemplates/LOCALRevora-main` into the protected `root:amp` mode `0750` directory `/home/amp/revora-install-backups`; archive files are mode `0640`. When that target is absent, record absence rather than creating a meaningless backup. See `AMP_INSTALLATION_READINESS.md` for the exact guarded plan.

## Database consistency

Do not copy live PostgreSQL/MariaDB storage files. The safe sequence is maintenance mode, application `save`, a provider-supported logical dump using a least-privilege backup identity, checksum, and then AMP file backup. Exact dump tooling cannot be selected until the database decision/provider is finalized.

AMP alone cannot guarantee external database transactional consistency. No privileged hook or shell workaround is added in this phase. Backup automation remains a documented limitation and a future approval point if privileged scheduling is required.

## Targets

- Development RPO: 24 hours initially; 15 minutes after tested incremental/application persistence exists.
- Development RTO: 4 hours.
- Proposed retention: 7 daily, 4 weekly, and 3 monthly verified sets, subject to storage review.

Staging/Production targets require a separate capacity and business-impact decision.

## Restore procedure

1. Obtain approval and stop only the target Revora instance.
2. Verify backup manifest, provenance, version compatibility, and SHA-256.
3. Restore into an isolated directory and separate database/schema first.
4. Apply least-privilege ownership: directories `0750`, secret file `0600`.
5. Restore the database with provider-supported tooling; never overwrite live DB files.
6. Start on alternate loopback ports in maintenance mode.
7. Verify migrations, health, record counts/invariants, save, shutdown, restart, and READY.
8. Cut over only after explicit approval; preserve the prior environment for rollback.

No destructive restore is performed in this phase.
