# Task brief for your AI agent

Copy the following into an agent working in your own clone. Replace the paths
with private local paths and provide authority only for systems you control.
Do not paste passwords, signing keys, real accounts or production dumps.

> Set up this Revora clone end to end on my own development machine. Read the
> root/component AGENTS.md files, README.md, docs/QUICKSTART.md,
> docs/SERVER_BUILD.md and docs/CONNECT_COMPONENTS.md first.
>
> Use website/site/ as the primary website. Keep it plain HTML/CSS/JavaScript
> with no .NET or frontend build requirement. Start with
> scripts/Start-Revora.ps1 -Mode Check, then start the local static preview.
> Verify English/Arabic, keyboard navigation, mobile layout, links and dialogs.
>
> Prepare isolated PostgreSQL using scripts/Start-Revora.ps1 -WithDatabase.
> Inspect prerequisites before installing anything. Preserve the generated
> credentials, existing volumes and keys. Re-run setup to prove idempotence.
> Never stop or reconfigure unrelated processes, containers or databases.
>
> I will supply a lawfully possessed, reviewed 6711 server runtime/schema and
> an independently checked executable SHA-256 through local paths. Inventory
> all missing assembly/API dependencies and their provenance. Do not invent
> a game schema or download arbitrary replacement DLLs. Stage the runtime with
> -GameRuntimeDirectory, -GameExecutableSha256 and an optional schema-only
> -GameSchemaSql into the private managed area. Keep every real account,
> database dump, client artifact and runtime credential out of Git.
>
> Connect my own HTTPS account service to the 6711 contract. The modern web
> identity tables and legacy 6711 accounts/SRP tables are not interchangeable.
> Keep registration disabled until synthetic interoperability tests prove
> creation, login and credential changes. Never store plaintext passwords,
> log request credentials, disable TLS checks, or send test/user passwords to
> the original Revora production domains. Browser configuration may contain
> only public URLs; use private environment/configuration for backend secrets.
>
> Audit the launcher's imported 7630 profiles and original-domain references.
> Configure a real 6711 profile for my own deployment with exact hashes and
> signed manifests. Preserve integrity checks and offline signing keys.
> Read docs/CLIENT_ENDPOINT_TOOL.md. For an approved 6711 DX9 hash, use the
> ClientEndpoint CLI on a private client copy to set my account IPv4/port,
> inspect it, verify the backup and exact restore, then configure the endpoint
> again. For server-list editing, use dat-inspect, dat-apply and dat-restore on
> the matching private executable/DAT copy. Select the actual server row ID,
> set my name/account IPv4/port, verify signature decoding and paired restore,
> then apply the final settings. The editor rotates the local RSA trust anchor
> and switches to signed-list routing; do not layer an endpoint override on it.
> Keep .revora-endpoint/, especially keys/server-list-private.pem, private and
> back it up locally. Re-hash both files and re-sign the operator-owned launcher
> manifest after saving; never disable integrity gates or publish private keys.
>
> Build the relevant components, run existing tests, the static-server tests,
> the repository checks and a redacted Gitleaks scan. Verify game Account/Game
> listeners, then a genuine 6711 native login, character entry, movement, save
> and relogin. Listener or HTTP readiness alone is not full gameplay evidence.
>
> Publish only website/site/ to my GitHub Pages repository when authorized.
> Never upload .local/, private mirrors, runtime binaries, data, raw logs,
> credentials or production captures. Do not rewrite/delete existing remote
> history, change public firewall/DNS/TLS, or deploy to production without my
> explicit authority for those exact changes.
>
> Work through routine fixes autonomously. Report what passed, what remains
> unverified, and any owner-only input needed. Do not claim a complete playable
> server while required dependencies or client tests remain missing.

Use an agent with filesystem, shell/build tools and GitHub access. Browser
testing and a real Windows/6711 client require suitable local access; a cloud
agent without those capabilities cannot verify the final gameplay experience.
