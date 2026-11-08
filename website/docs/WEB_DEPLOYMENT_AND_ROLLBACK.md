# Web Deployment and Rollback

## Deployment gate

1. Build/test and scan tracked content for secrets, keys, dumps, clients, research and generated artifacts.
2. Publish self-contained `linux-x64`; validate archive paths/content and record SHA-256.
3. Recheck `127.0.0.1:5080` availability. Deploy the reviewed artifact under `/srv/revora-web/app` without using AMP.
4. Run as the dedicated `revora-web` system account; verify Production fail-closed behavior, private data-protection keys and loopback binding.
5. Complete two Start/READY/health/Stop cycles; verify graceful SIGTERM, no secret leakage and port release.
6. Test Arabic/English routes, noindex, private-route authorization, CSP and canonical metadata through the loopback endpoint.
7. Back up only the affected nginx site and current Coming Soon document; record hashes and rollback commands.
8. Change the apex upstream atomically, run `nginx -t`, then gracefully reload. Never restart nginx.
9. Validate external TLS/hostname, localized pages, headers, status codes and AMP hostnames.

## Rollback

Rollback triggers include failed readiness, redirect loops, wrong host content, authentication exposure, missing private-route noindex, security-header regression, AMP regression, public application binding or repeated application failure. Restore the exact backed-up Coming Soon nginx site, run `nginx -t`, gracefully reload, stop only `revora-web.service`, and verify the apex fallback plus both AMP hostnames. Do not change DNS, firewall, ADS, nginx service state or another service as a recovery shortcut.

The current rollback asset is `/var/www/revora-coming-soon/index.html`; operational backups are stored outside Git under a new timestamped narrow directory.

Current deployment state: the public-only platform is live behind nginx as a hardened systemd service. `deploy/amp-web/` remains unused, `RevoraWebDevelopment` was not created, and ADS was not changed. The Coming Soon document remains available as the rollback asset. Operational evidence and exact rollback steps are recorded in `WEB_PRODUCTION_DEPLOYMENT.md`.
