# Owner Test 4 Deployment Record

Date: 2026-08-28 UTC

## Deployed private components

- Authenticated route: `/owner-downloads/client-native-bootstrap-test-4`
- ZIP: 152,847,415 bytes; SHA-256 `e96e5a133ce3dbd0f537e5af37fcdbd12b66c5eb232df5706733142259bc47ee`
- First-party launcher: 139,847,307 bytes; SHA-256 `c5e4bd65d67f8fc3c1a0c711e10d3249651ad8dc19f71f19c4688e5fd4dd98d0`
- Signed testing profile: SHA-256 `d938e3de20c2e2a0c301657546ae73a943be7b279c1d6bb13825dd79b2fe0b19`
- Combined patch metadata: SHA-256 `1dcb6731bd8d7a3516e3c662771de0151c8e834fab0545ecf3920c7ea6fbb69a`

The archive contains only first-party tools, signed profiles, first-party testing configuration, asserted patch metadata and instructions. It contains no proprietary client executable or asset. ClamAV 1.4.3 definitions 28106 reported no detection on the launcher, package inputs or ZIP.

## Database and Admin

- Production migration: version 4, `launcher_tickets`.
- Migration SHA-256: `e0ec45c4741b843e06213588632623aea89b16e227065192b069219d0d6ca347`.
- Table contains digest-only, expiring, realm/session/build/launch-bound tickets. Raw tickets are never stored.
- Pre-migration consistent dump: `/var/backups/revora-postgresql/revora-20260828T194428Z.dump`; its adjacent checksum verified successfully. This operational path is intentionally not in Git.
- Pre-deployment Admin backup: `/srv/revora-admin/backups/automatic-bootstrap-v4-20260828T194514Z`; intentionally not in Git.
- Admin remains `revora-admin` on `127.0.0.1:5081`. Malformed launcher authentication returns generic HTTP 400.

## Validation

- Complete Release build: zero warnings and errors.
- Native Login: 19/19.
- Launcher: 14/14.
- Admin: 6/6 as the least-privilege service identity.
- Existing synthetic durable/playable suite: 11/11; explicitly not genuine 5165 evidence.
- Test ticket SQL: first atomic consumption updated one row; replay updated zero rows.
- Launcher publish was byte-identical across two clean output directories.
- `playrevora.com` and `panel.playrevora.com` remained HTTP 200 after the Admin-only restart.
- Ports 9958 and 9959 remained unused and no firewall, AMP, ADS, DNS, nginx or Web change was made.

## Rollback

1. Stop only `revora-admin.service`.
2. Restore the two exact Admin application files from the recorded Admin backup.
3. Start only `revora-admin.service`; confirm `127.0.0.1:5081` and the prior dashboard.
4. Remove only the Test 4 ZIP after confirming its exact path and SHA-256 if private download withdrawal is required.
5. If and only if no ticket has ever been issued, apply the V004 rollback described in `AUTOMATIC_ACCOUNT_BOOTSTRAP.md`. Otherwise preserve ticket/audit evidence and use forward recovery.

No nginx change is required for this route. Public client downloads and public game ports remain disabled.
