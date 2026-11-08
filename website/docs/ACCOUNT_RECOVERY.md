# No-email account recovery

Revora does not request email and has no SMTP dependency.

At registration, ten independent 128-bit recovery codes are displayed once and may be downloaded locally. Only keyed SHA-256 digests are persisted. Consumption is an atomic conditional update; replay and concurrent reuse fail. A valid code creates a 15-minute single-use reset authorization stored by digest, with the raw value confined to a Secure HttpOnly cookie.

Optional recovery questions require all three slow-hashed answers plus a recognized device or familiar network signal. A qualifying request has a 15-minute cooldown. Insufficient-confidence requests remain Pending for protected Admin review. Admin sees only request time and a sanitized support category; approval never reveals or chooses the new password.

Recognized devices use a random 256-bit cookie token and server-side keyed digest, expire after 180 days, and can be revoked. Familiar networks use a versioned keyed HMAC of an IPv4 /24 or IPv6 /56 prefix and expire after 30 days. Raw full IP addresses are not retained by this subsystem. Neither IP nor a device is sufficient alone.

Public responses are generic. No reset secret appears in a query string. Successful reset revokes sessions, devices, reset authorizations, handoffs, and launch tickets while preserving AccountId, character UIDs, ownership, and game state.
