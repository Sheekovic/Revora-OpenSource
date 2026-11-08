# AMP Live Validation Record

Date: 2026-08-28

Result: blocked before template discovery; narrow rollback completed. No Revora AMP instance was created.

## Preconditions

- Repository baseline: `346f2920fe10c5429f503a5c5b180c9faa296e76` on `feature/amp-integration`, clean.
- Artifact SHA-256: `261e02fb279af44e327bc68a65132f0e8adc4efe0a2620a69a07f2c1a22e8205`.
- Deployment validation and all existing tests passed.
- AMP/ADS version: 2.8.0.4.
- ADS01 was the sole AMP instance, running at `127.0.0.1:8080`.
- TCP ports 9958 and 9959 were unused.
- `LOCALRevora-main` did not exist.

## Attempt and hard stop

The seven reviewed files were installed byte-identically at `/home/amp/.ampdata/instances/ADS01/Plugins/ADSModule/DeploymentTemplates/LOCALRevora-main`, owned by `amp:amp`; directories were mode `0750` and files `0640`. No secret was installed.

AMP's installed Manage Templates Refresh implementation calls the authenticated ADS API. A request without a session was rejected with an authorization response, as expected. No authenticated ADS UI session was available to this automation. Using management credentials obtained from process data, placing panel credentials on a command line, invoking an undocumented refresh command, or restarting ADS would violate the approved security boundary. Execution therefore stopped before discovery and instance creation.

## Rollback and verification

The exact reviewed directory was archived as `/home/amp/revora-install-backups/LOCALRevora-main.unloaded.tar.gz`, owned by `root:amp` mode `0640`, SHA-256 `01d029a7ea0101c68869eb96d7a26079bfc25c1da7da87efb8a24d0bb867bb1e`. The directory itself was moved to `/home/amp/revora-install-backups/LOCALRevora-main.unloaded`; it is no longer in the live template discovery path.

Post-rollback verification confirmed:

- `LOCALRevora-main` is absent from the live DeploymentTemplates directory.
- ADS01 remains the sole instance and remains running.
- AMP Instance Manager and nginx remain active and were not restarted.
- Ports 9958 and 9959 remain unused.
- No RevoraDevelopment instance or datastore exists.
- No Revora process ran under AMP.
- No listener, firewall rule, nginx configuration, AMP global configuration, existing instance, or public binding was changed.

## Required operator action

An authorized operator must use an already authenticated Hostinger Game Panel session to open AMP template management and invoke Refresh. The reviewed `LOCALRevora-main` directory must first be restored to the exact live path and reverified. If Refresh asks for an ADS restart, stop. Once Revora is discovered, the remaining stopped-instance workflow in `AMP_INSTALLATION_READINESS.md` may resume.
