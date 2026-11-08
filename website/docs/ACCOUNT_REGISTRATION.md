# Public account registration

`Revora.Web` creates an immediately active Player account in `identity.game_accounts`, the same authoritative table read by the patch-5165 Account service. Registration accepts only a username, password, and password confirmation; email, role, status, realm, IDs, and administrative fields are not browser inputs.

Usernames are 4–15 ASCII alphanumeric characters and are normalized with invariant lowercase. `normalized_username` has a unique database index, so concurrent case variants cannot both commit. New public rows are marked `public_registration` and constrained to this policy. Existing legacy rows remain compatible.

Passwords are 8–15 printable ASCII characters for patch-5165 compatibility. They are never truncated. They use the reviewed PBKDF2-SHA512 adaptive hasher (600,000 iterations, independent 256-bit salt). This restriction remains until launcher-only authentication can keep stronger credentials outside the legacy packet.

Registration atomically creates the account, Player role, security record, and ten recovery-code digests. The account is Active immediately and the same credentials work in Web and the genuine 276/1086 verification path.

Abuse controls include antiforgery, a 5/hour network-prefix registration limit, minimum form-completion time, honeypot, bounded forms, database uniqueness, generic errors, and a configuration circuit breaker. No external CAPTCHA is required.
