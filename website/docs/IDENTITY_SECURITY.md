# Identity security

`AccountId` is the immutable account authority. Normalized username is a unique login identifier, not a relational key. `game.characters.account_id` references AccountId; the numeric legacy UID identifies a character on the 5165 wire. Character names are case-insensitively unique display properties.

Web sessions contain an encrypted-cookie copy of a random session token while PostgreSQL stores only its keyed digest. Every authenticated request validates the session, security stamp, account state, expiry, and revocation state. Login rotates the session. Password changes rotate the security stamp, revoke other Web sessions, reset authorizations, recognized devices, legacy handoffs, and launch tickets.

Cookies are Secure, HttpOnly, and SameSite Lax or Strict as appropriate. State-changing browser routes validate antiforgery. Production is HTTPS-only behind the explicitly trusted loopback nginx proxy. Client-supplied forwarding headers from other sources are ignored.

Authentication is limited to 5 attempts/minute per observed source and accounts lock for 15 minutes after eight recorded failures. Registration is 5/hour and recovery is 6/15 minutes. These are configurable bounded windows and do not permanently ban shared networks.

Security questions are a weaker supporting factor. Answers are normalized and independently salted with the slow password hasher. All three must match, and questions alone never authorize an immediate reset.
