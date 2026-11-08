# Progression shop dispatch integration

2026-10-04. The integrated worktree's general CP handler ran before the existing
subclass, Inner Power and P6 native-shop handler. With a registered ordinary
session, the general handler accepted bound CPs instead of preserving the
specialized shops' regular-CP-only policy. The regression fails on A9B83286.

Dispatch now selects these three specialized shops before the general shop
handlers. Their existing NPC type, map, range, stock, currency, quantity and
transaction checks remain authoritative. No prices or item binding policy
changed. General mall bound-CP purchases and the earlier specialized material
handler remain available under their own rules.

The integrated corrected build passes959 starter/purchase checks, including six
new assertions across shop91008,91009 and91010. Each registered non-GM fixture
rejects bound CPs without a debit or item, then buys with regular CPs and verifies
the exact price and unbound item through production entity/inventory reload.
Fixtures supply shop stock/proximity; this is not native display or map testing.
The P6 fixture calls the production catalog initializer, which sets its price.

Two fixture prerequisites also needed correction after the full candidate startup
migrated the disposable database's public schema. `CREATE TABLE AS` loses column
defaults, even when later `ADD COLUMN IF NOT EXISTS` sees the migrated column.
The full starter fixture now preserves Champion table defaults and sets the
progression bonus default on its own empty entities table. This avoids invalid
null revision/bonus images without weakening production validation. An initial
test without a registered session was discarded as fixture evidence; the recorded
before-fix failure uses a registered session and is the bound-payment assertion.

[Build/log evidence](research/PROGRESSION_SHOP_DISPATCH.json).
This build includes existing uncommitted owner integration. Only the dispatch
ordering, targeted tests and fixture corrections belong to this batch; unrelated
owner edits remain in the worktree. The private full-game candidate remains on
A9B83286. No live deployment, restart, schema or client changes occurred.
