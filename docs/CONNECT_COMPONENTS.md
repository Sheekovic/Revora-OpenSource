# Connect your own Revora installation

The static frontend is independent of the server language. Its optional account
link can point to an operator-controlled HTML/JavaScript account application,
a Node backend, or another explicitly reviewed API/service. ASP.NET is not
required for the public frontend.

```text
Browser / GitHub Pages        Public HTML, CSS, JavaScript and artwork only
          |
          | explicit HTTPS account-page link, when you configure one
          v
Your account backend         Validation, password hashing, rate limits, sessions
          |
          | parameterized database operations, private network/loopback
          v
PostgreSQL                   Separate web identity and 6711 game data
          ^
          | restricted runtime login
6711 Game server             Native Account TCP 9958, Game TCP 9959
          ^
          | reviewed 6711 profile and exact file/signature checks
Windows launcher + client    Client installed lawfully by the operator/player
```

## Website and domains

- Deploy only `website/site/` to GitHub Pages or any static host.
- Edit `website/site/config.js` for your repository and optional HTTPS account
  **page** URL. It is public configuration. Do not place tokens, connection
  strings, database passwords, private API keys or signing keys in it.
- GitHub Pages cannot run PostgreSQL, the game server or a Node/ASP.NET account
  API. Host these on your own Windows/VPS infrastructure.
- Put public HTTP account services behind valid HTTPS and a reverse proxy. Keep
  database ports private. DNS, firewall, public ports and TLS certificates are
  explicit operator changes; local setup does not make them for you.

## Database boundaries

The managed defaults are `127.0.0.1:25432`, game database `revora6711_3d`, and
optional web database `revora_web_local`. Use separate role credentials from
private `.local/secrets/`; do not copy their values into documentation or chat.

The modern web schema uses `identity.game_accounts`. The 6711 runtime reads
`public.accounts`, `public.entities` and `revora_account_srp`. These are different
contracts. Do not point both services at one database and assume registration
or password changes are integrated. Website migrations do not create the
complete 6711 game schema.

A compatible account service must implement the 6711 identity contract using
`server/Database/AccountTable.cs` and `server/Network/AuthPackets/Srp6711.cs` as
the current source of truth. Preserve the server's SRP marker, salt/verifier
format, normalization/uniqueness rules and atomic credential updates. Use
synthetic compatibility vectors and a real 6711 login test. Never introduce
plaintext password storage or copy PBKDF2/Argon2 hashes into SRP fields.

If you choose the preserved legacy ASP.NET service, explicitly configure its
HTTPS `RevoraWeb:GameIdentityRegistrationUrl`, database settings and private
keys; registration has no default endpoint. Its redirects are disabled. It
still needs the corresponding operator-owned game identity adapter and account
lifecycle tests before registration can be enabled.

## Launcher and native runtime

The server targets genuine 6711 DX9. Some imported launcher and legacy tools
target 7630 and reference the original project's domains. Review those values
for your installation before distributing anything. Use an approved matching
profile, lawful client distribution, exact hashes and signed manifests.
Changing a filename or turning off hash/signature checks is not a compatibility
solution. Keep release private keys offline and out of every frontend bundle.

## Minimum end-to-end evidence

1. Database health, migrations and least-privilege checks pass in a fresh setup.
2. Website/Pages loads on desktop and mobile with no missing relative assets.
3. Your account service creates and authenticates a disposable synthetic account;
   wrong passwords, replay/abuse, reset and credential changes behave correctly.
4. The exact approved 6711 client reaches the intended server, creates/loads a
   character, moves, saves, disconnects and logs in again.
5. Stop/start preserves test state and keys. Re-running setup never resets data.

Until these are proven, keep registration disabled and label the installation's
remaining limits. The public Pages preview contains no connected account/game
service and no invented online-player count.
