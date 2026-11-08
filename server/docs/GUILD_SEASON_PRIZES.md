# Earned seasonal prize exchanges (M7.2b)

GuildDirector 10003 now offers staged ordinary-player exchanges from the
**Guild War honors** menu. The quantities and CP amounts follow the
[official exchange table](https://co.99.com/guide/event/2017/sgw_16.shtml):

| Earned Lord Tokens | Bound prize | CPs |
| --- | --- | --- |
| 1 | Bronze Prize 2100055 | 3,000 |
| 2 | Silver Prize 2100065 | 6,000 |
| 3 | Gold Prize 2100075 | 20,000 |

These are replacements for the consumed tokens, not cumulative upgrades. A
recipient who later transfers guild leadership may still exchange their own
claimed tokens. No GM status or owner allowlist is required.

## Ownership and atomic delivery

Selection admits only bound, single, unlocked inventory tokens with exact item
UIDs recorded by the seasonal claim service. Their saved claimant, season end
and unspent receipts must agree. Legacy/shop tokens, even with matching type,
binding and duration, cannot substitute. Tokens from an expired season fail.

A two-minute confirmation binds the selected token objects, UIDs and duration
preimages to one player session. Confirmation rechecks ownership and proximity.
Consumption frees inventory slots before delivery, so a full bag may exchange
one existing token for one prize. A CP balance that would overflow is rejected.

One transaction marks each token spent, deletes its exact saved item preimage,
inserts the prize, records its token ancestry and updates CPs against the prior
balance. Every required write must affect one row. Post-commit client updates
use the existing inventory and CP serializers. A failed or uncertain persistence
operation fences the session for reload; it cannot replay consumed receipts.

Prize receipts retain the source season, tier, CP payout, exact item UID and
season boundary for the subsequent Gold Trophy conversion. The item is not
given an automatic deletion timer: rollover must replace recorded Gold Prizes
atomically instead of allowing the generic expiry loop to destroy the evidence.

## Validation

Staged build SHA-256:
`C5827E6DD085B13F7B35AC8051FB21BC3163B15BC63CB19D29EDD724A05FAF74`.

The focused guild-season suite passes **104 checks**, including 27 prize checks
on top of the previous 77 battlefield/claim/transfer checks. Bronze uses an
actual claimed token after leadership transfer. Gold and Silver run respectively
three and two further actual Sunday battles, claim each result and redeem through
the NPC dispatcher. The fixtures use ordinary non-GM characters.

Fault tests suppress material deletion, prize insertion, spent-token marking,
prize receipt insertion, material receipt insertion and CP update. They verify
complete rollback. Other cases cover insufficient tokens, locked/changed items,
remote and expired confirmation, stale saved balances/materials, CP overflow,
full bags, concurrent confirmation, legacy-token substitution and season expiry.
Normal character save, character reload and production item loading preserve
the CP balance and prize while the consumed token remains unavailable.

Related suites: progression 74, quest packets 9 and Potency/daily bosses/Extraction
216. Total: **403 checks**. All database mutations stayed in a disposable schema.

## Remaining acceptance

[M7.2c](GUILD_PRIZE_ROLLOVER.md) now converts earned Gold Prizes, including
offline/equipped/warehouse ownership. [M7.2d](GUILD_CUP_STATS.md) corrects cup
statistics. [M7.3](HOLY_GRAIL_CRAFTING.md) adds Grail crafting and both wardrobe
grants. [M7.2e](GUILD_WAR_PURSES.md) adds atomic regular Sunday rank purses. This staged exchange implementation has not been deployed; genuine
6711 dialogue, item display and CP refresh acceptance remain open.
