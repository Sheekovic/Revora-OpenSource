# Username capitalization during game login

2026-09-16; baseline `1f6f906`.

Read-only inspection confirmed that an active newly registered account with mixed
capitalization existed in `public.accounts`. The existing `AccountTable`
constructor found its registered spelling and returned `exists=false` for the
lowercase spelling. Recent authentication logs also recorded unknown-account
rejections, but contain only username lengths, so those individual attempts
cannot be conclusively attributed to the reported player.

The website's `UsernamePolicy.Normalize` and legacy account lookup use lowercase
identity matching. The game used case-sensitive PostgreSQL equality. This can
reject a valid registration before SRP password verification begins.

`AccountTable` now uses a parameterized case-insensitive lookup, preserves the
stored spelling for subsequent saves, and rejects more than one matching legacy
row. Password handling and the SRP transcript remain unchanged: the latter still
uses the client's supplied username. No packet layout or native-client login is
newly verified by this change.

Validation: isolated server build and 20 disposable-database checks covering
ordinary accounts, capitalization, canonical spelling, exact password retention,
unknown/empty/injection input, ban-state retention, and ambiguous legacy rows.
The fixture owns `revora_login_validation`, refuses an existing schema, and
removes only the schema it successfully created. No player credentials or live
account rows were changed. The running server remains unchanged; deployment and
restart are still required for the permanent fix. Using the registered spelling
is the immediate workaround on the existing server.
