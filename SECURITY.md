# Security

Do not post credentials, account data, signing material, raw authentication
packets, or memory dumps in public issues or pull requests.

Report vulnerabilities using this repository's GitHub **Security → Report a
vulnerability** feature when available. If private reporting is unavailable,
open an issue requesting a private contact without including exploit details
or sensitive evidence.

Local credentials and runtime settings belong in environment variables or
ignored operator configuration. Signing keys stay outside the checkout.
The guided setup keeps credentials, database state references, runtime copies
and logs in ignored `.local/`. Public browser configuration contains URLs only.
Do not publish `.local/`, Docker-volume backups or runtime credential files.
CI uses a redacted Gitleaks scan of published history; findings require review,
and a leaked real credential must be rotated rather than merely hidden in a
later commit. Scans reduce risk and cannot establish that every secret is absent.
Do not run legacy probes, installers, or deployment tools against production
without a separately authorized operator procedure.
