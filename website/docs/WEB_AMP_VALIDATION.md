# Revora Web AMP Validation

Date: 2026-08-28 UTC. Scope: offline and local lifecycle preparation only; no live AMP template or instance change.

## Artifact

- Path: `/root/Revora/artifacts/Revora.Web-linux-x64-v6.tar.gz`
- SHA-256: `30714f542475a06f975c18a630f2b3f114ffaa4732103706deebfeeac6b490bb`
- Runtime: self-contained Debian 13 `linux-x64`, prebuilt with no runtime restore/build.
- Application: `Revora.Web` plus required ASP.NET static-web metadata, safe appsettings and original static assets. No source, secrets, keys, client, database, research, logs or Git metadata.

## Lifecycle evidence

The extracted artifact ran as `amp` from `/tmp/revora web release.4a3tbF/app`, with application directories `0750`, content `0440`, executable `0550`, configuration `0640`, data/logs `0750`, and data-protection key directory `0700`. The exact temporary path is disposable test evidence, not an intended AMP path.

- Listener: only `127.0.0.1:5080`.
- stdin cycle: one READY, `status`, graceful `stop`, one STOPPED, clean completion.
- signal cycle: one READY, SIGTERM delivered to the `amp` process, one STOPPED, clean completion.
- occupied port: safe `FatalStartupFailure` without raw exception/stack, exit 1, no READY.
- dashboard/Admin requests: anonymous requests challenged (302 to localized login); content was not rendered.
- unknown console input containing shell punctuation was rejected as one unknown command.
- secret canary was supplied to lifecycle processes and did not appear in observed output.
- key isolation: generated key files stayed below the isolated instance `data/keys`, directory `0700`, file `0600`. A first test revealed and then removed one exact key created in the shared AMP profile; the code was corrected before the final artifact.

The filesystem key ring is acceptable only for isolated Development validation. Production requires reviewed at-rest key protection and a durable, backed-up instance scope.

## AMP state

`deploy/amp-web/` passes repository JSON and cross-reference checks against the existing AMP 2.8.0.4 Generic template conventions. Live discovery remains unproven: `LOCALRevoraWeb-main` was not installed, Manage Templates Refresh was not invoked, and `RevoraWebDevelopment` was not created. Updates and automatic retries are disabled.

nginx still serves `/var/www/revora-coming-soon/index.html`. No nginx, DNS, certificate, firewall, ADS, AMP instance or public listener changed during W5.
