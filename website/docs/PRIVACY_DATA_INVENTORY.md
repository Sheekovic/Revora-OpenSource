# Privacy data inventory

| Data | Purpose | Stored form | Retention |
|---|---|---|---|
| Username | Login and display | Plaintext plus normalized unique value | Account lifetime |
| Password | Authentication | PBKDF2-SHA512 salted hash only | Replaced on change/reset |
| Recovery code | Primary recovery | Keyed digest only | Until consumed/regenerated |
| Security answer | Supporting recovery | Independently salted slow hash | Until replaced/account removal |
| Device token | Risk signal | Raw only in Secure HttpOnly cookie; keyed digest in DB | 180 days or revocation |
| Network history | Risk signal | Keyed HMAC of /24 or /56 prefix | 30 days |
| Web session | Authentication | Raw token only inside protected cookie; keyed digest in DB | 8 hours plus cleanup |
| Reset authorization | Password reset | Keyed digest | 15 minutes or consumption |
| Pending recovery | Admin review | Opaque digest and sanitized category | 24 hours plus audit policy |
| Character | Game persistence | Numeric UID, display name, AccountId FK | Character/account policy |

Passwords, security answers, recovery codes, raw device/session/reset tokens, raw handoff tickets, and password-equivalent packets are excluded from logs, reports, audits, and Admin views.
