# Rune material purchases

2026-09-26; source validation only, not deployed or native-client accepted.

The existing atomic Exemption Token purchase handler now also handles item IDs
730001 through 730008. It requires actual CP-shop stock and catalog pricing;
this does not add missing stones to any shop. The loaded global mall stocks +8
stones at 26,244 CPs each. Every stone is a separate item with its corresponding
enhancement value persisted. Bound-CP purchases retain binding.

Payment, items and receipt commit in one transaction. The existing historical
`revora_exemption_purchases` table already records item type; no schema change is
required. Its one-second identical-purchase guard survives reconnect. This is
bounded duplicate suppression, not a guarantee that arbitrarily delayed packets
represent the same intention: the request has no verified unique purchase nonce.

585 combined token/shop checks pass in `obj/rune-stone-shop-tests.log`, including
13 new stone checks: concurrent regular/bound purchases, reconnect replay,
suppressed item/payment/receipt writes, persisted enhancement across stocked
grades, and rejection of an unstocked +7 stone. The existing token admission and
rite tests remain included. These totals overlap other fixture runs.

The continuing Prestige character buys one five-rune set from Vesper for 3,250
earned RVPs. Twelve +8 stones max its blue rune; four max each of three distinct
yellow runes. Its red rune needs no upgrades. Total material cost is 629,856 CPs
for 24 stones. Normal shop, rune-bag transfer, composition and equip handlers
provide the route. See [career evidence](PRESTIGE_EARNING.md).

Packet builders are existing server fixture evidence, not verified native 6711
captures. Native acceptance remains pending. Validation compiles committed
production sources plus this change while preserving unrelated local work.

## Bound-only shop integration correction (2026-10-04)

The full worktree integration run found that the earlier specialized material
handler intercepted +Stones before the general CP-shop handler, then silently
refused shops whose currency was BoundCps. It now accepts both CP shop types and
forces binding and bound-CP payment for a bound-only shop. The final transaction
check requires the shop currency to remain unchanged. Ordinary CP shops retain
their existing selectable payment behavior, catalog prices and receipt table.

The initial combined starter/purchase run reproduced the missing +1 Stone. After
the correction,953 checks pass on the integrated binary. Seven added cases cover
Exemption Token and +1 Stone purchases with selectors0/1/2, concurrent requests,
production inventory reload, repeated request suppression and refusal to spend
ordinary CPs when the bound balance is empty. Run the existing
`Verify-PotencyHunt.ps1 -BuildDirectory <isolated-build> -RevoraStarterOnly` with
the required disposable connection. These are ordinary fixture players; this
does not claim a fresh game-process or native-client purchase test.

The separate legacy `--second-token-shop-only` run initially failed before its
purchase cases: its sparse schema could not be dropped with current dependencies;
an exploratory full-schema variant then exposed missing Enlightenment startup
in the Soul fixture. The normal server initializes that dependency through
`Thread` and `WayOfHeroesClaims`. The corrected token/Soul suite setup uses the
full disposable character schema and explicit Enlightenment initialization.
The standard token suite now passes593 checks, including actual earned quest
CPs funding the token and the completed second-rebirth receipt surviving reload.
The earlier953 starter checks remain separate and overlap this suite.
See [integration evidence](research/WORKTREE_INTEGRATION.json). No live deployment.
