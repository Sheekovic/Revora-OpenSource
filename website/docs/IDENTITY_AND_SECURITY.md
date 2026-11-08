# Identity and Security

## Threat model

Primary threats are credential stuffing, account enumeration, CSRF, session theft/fixation, open redirects, reset-token leakage, privilege escalation, malicious content, SQL injection, patch-manifest substitution, secret disclosure and denial of service. nginx and application limits form independent layers; neither is treated as sufficient alone.

## Authentication policy

- Passwords use a reviewed, adaptive one-way password hasher through an abstraction; plaintext and reversible encryption are prohibited.
- Authentication responses are deliberately non-enumerating. Login, registration and recovery have separate rate limits.
- Cookies are HttpOnly, Secure in Production, use an explicit SameSite policy and rotate on authentication/security changes.
- State-changing browser requests require antiforgery validation. Return URLs must be local and normalized.
- Recovery codes and reset authorizations are random, expiring or single-use, and stored only as hashes. Reset cookies and tokens are never logged or placed in URLs.
- Sessions have opaque identifiers, revocation, credential-version checks and logout-all support.
- Authentication events record a correlation identifier and safe outcome, never credentials or raw tokens.

Public registration is active. It creates immediately active Player accounts in the same authoritative AccountId store used by the genuine patch-5165 Account service. Email is neither requested nor synthesized; recovery codes are primary, while questions, recognized devices, and familiar-network digests are supporting risk signals only.

## Authorization

Dashboard routes require an authenticated-user policy. Admin routes require both authentication and explicit role/policy grants. Player chat, AMP console and website administration are separate trust surfaces. UI hiding never replaces server-side authorization.

Future administrative mutations append: actor, action, target type/id, UTC timestamp, reason, correlation id and a redacted before/after summary. Passwords, hashes, tokens, connection strings and private keys are never viewable.

## Secrets and response security

Production Web secrets live outside Git and the served application tree in access-controlled files owned by the dedicated `revora-web` identity. They are absent from arguments, templates, environment dumps, client assets and logs. Responses apply HSTS, a tested CSP, `nosniff`, restrictive referrer and permissions policies, frame protection through CSP, and private/no-store caching for authenticated pages. Developer exception pages are never enabled in Production.
