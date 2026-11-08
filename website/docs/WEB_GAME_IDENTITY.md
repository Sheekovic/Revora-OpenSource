# Web-to-game identity bridge

Status: implemented for loopback development and integration testing; disabled by
default and not approved for public login.

## Routes

- `POST /api/v1/game-handoffs` requires an authenticated web session and a valid
  antiforgery token. It issues a 60-second opaque `uint32` handle bound to the
  authenticated account, its existing character, the configured server, and the
  request IP address. PostgreSQL stores only the handle's SHA-256 digest.
- `POST /internal/v1/game-handoffs/consume` accepts `handle` and
  `remoteAddress`. It atomically consumes the matching row, derives account and
  character numeric identities from authoritative owned records, and returns the
  HMAC-SHA-256 signed response documented by the WKConquer adapter.
- `POST /internal/v1/game-servers/status` records a bounded heartbeat.
- `GET /api/status` exposes only server name, online state, player count, and
  uptime. A heartbeat becomes offline after 45 seconds.

The internal routes return 404 unless integration is enabled, the direct ASP.NET
peer was loopback before forwarded-header processing, and nginx supplies the
configured client-certificate SHA-256 fingerprint. Nginx must validate the game
server client certificate and overwrite (never append or pass through)
`X-Revora-Client-Certificate-Sha256` before proxying to ASP.NET.

## Development configuration

Apply migration `V047__wkconquer_identity_handoff.sql` to a disposable database.
Supply settings through protected environment/configuration providers; do not put
keys or passwords in source-controlled JSON:

```text
RevoraWeb__RuntimeMode=Development
RevoraWeb__RegistrationEnabled=true
RevoraWeb__DatabaseConnectionString=<restricted revora_web connection>
RevoraWeb__SecurityHmacKeyBase64=<development-only 32-byte key in base64>
RevoraWeb__InternalGameIntegrationEnabled=true
RevoraWeb__IdentityServerId=<non-zero WKConquer server ID>
RevoraWeb__IdentityVerificationKeyBase64=<same development-only HMAC key as game server>
RevoraWeb__IdentityClientCertificateSha256=<64 hexadecimal characters>
```

Bind ASP.NET only to loopback. Registration and recovery are rejected by startup
validation outside the Development environment. Production settings remain
disabled until the release gates in `AGENTS.md` are satisfied.

## Identity mapping

Migration V047 adds a durable unsigned-32-bit-compatible `legacy_uid` to web
accounts. The character identity comes only from `game.characters.legacy_uid`
joined through `account_id`; a caller cannot select either value. Accounts without
an existing character cannot obtain a handoff and receive
`character_identity_unavailable`.
