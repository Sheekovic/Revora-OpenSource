# Web Production Deployment Record

## Live state

The public-only Revora website was deployed on 2026-08-28 without AMP or a database. The deterministic self-contained artifact is `Revora.Web-linux-x64-v11.tar.gz`, SHA-256 `ac466b069a191214921403869edfdbd8b82e979c75bcc45c3aada61342ce81ae`. Artifacts and machine data remain outside Git.

`revora-web.service` runs the application directly as the non-login `revora-web` account from `/srv/revora-web/app`. It listens only on `127.0.0.1:5080`. The immutable app is owned by `root:revora-web`; application directories and executable are mode `0550`, ordinary files are `0440`, and the private config directory is `0750`. Writable data and key directories are owned by `revora-web:revora-web` mode `0700`; generated data-protection keys are mode `0600`. `/srv/revora-web/backups` is `root:root` mode `0700`.

Production configuration uses `https://playrevora.com`, enables indexing only for public content, and keeps maintenance disabled. Public registration, website authentication, account security and no-email recovery use the authoritative local PostgreSQL identity store through the restricted `revora-web` role. Client downloads remain disabled and Admin protection remains separate.

## Validation evidence

- The service ran as `revora-web`, emitted one READY marker per start and one STOPPED marker per graceful SIGTERM, and recovered through repeated stop/start and post-cutover restart cycles.
- `/health/live` and `/health/ready` passed on loopback; nginx intentionally returns 404 for public `/health/` paths.
- English LTR and Arabic RTL pages passed Chromium review at mobile, tablet, desktop, ultrawide, 4K, 200% scale, and reduced-motion settings. Review files remain outside Git under `/tmp/revora-web-visual-20260828`.
- Registration, login and recovery forms are functional and noindex. Account and Admin routes reject anonymous access. Registration activates Player accounts immediately without email; downloads remain unavailable.
- TLS hostname verification passed for apex and www. `www` preserves the request URI in a permanent redirect to the apex.
- Canonical, reciprocal hreflang, x-default, Open Graph, valid JSON-LD, robots, sitemap, CSP, and security-header checks passed.
- `panel.playrevora.com` and `srv1936586.hstgr.cloud` remained HTTP 200. ADS, AMP instances, DNS, firewall rules, and unrelated nginx sites were not changed.

## nginx and rollback

Only `/etc/nginx/sites-available/playrevora.com` was replaced, from the reviewed `deploy/nginx/playrevora.com.conf`. The enabled symlink was preserved. The exact pre-cutover nginx file and Coming Soon page are backed up at `/root/nginx-cutover-backups/20260828T124500Z-revora-web-public`; its `SHA256SUMS` file verifies both copies. TLS private keys are not included.

Rollback triggers are failed readiness, repeated service failure, a public port 5080 bind, TLS or redirect regression, protected-route exposure, broken Arabic/English rendering, material asset failure, or AMP regression. To roll back:

1. Stop only `revora-web.service`.
2. Install the backed-up `playrevora.com` file over `/etc/nginx/sites-available/playrevora.com` with owner `root:root` and mode `0644`.
3. Run `nginx -t`; do not reload on failure.
4. Gracefully reload nginx with `systemctl reload nginx`.
5. Verify the original noindex Coming Soon apex, both AMP hostnames, nginx activity, and absence of a listener on port 5080.

Do not modify DNS, firewall, ADS, AMP, panel configuration, certificates, or another nginx site during rollback.
