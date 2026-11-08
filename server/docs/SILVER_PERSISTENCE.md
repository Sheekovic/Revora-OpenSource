# Durable silver ownership

Baseline `5299818`, 2026-09-15. M6.28 prevents a delayed character save or
reward settlement from replacing a newer durable silver balance. It also
replaces the offline auction credit's separate 32-bit read and write with an
atomic, range-checked database increment.

## Saved balance contract

`SilverBalance` remembers the exact nullable `entities.money` value read at
login, separately from pending in-memory earnings or spending. The image belongs
to that `GameState` and entity instance. Loading a replacement entity clears it;
negative or out-of-range stored values fail closed. Null legacy purses retain
their null preimage until their first successful save.

Full character saves compare both the existing progression image and this silver
preimage in the same SQL update. A mismatch changes no character fields and
fences the session for reload. Successful saves advance both images only after
the statement succeeds. Initial saves without a loaded image may bootstrap only
when the cached purse equals the actual stored value, with that value added to
the update predicate. They cannot silently bootstrap over an offline credit.

Explicit silver saves use the character owner lock and a transaction that locks
the entity row, checks the saved balance, writes the complete pending balance,
then advances the image after commit. Generic `UpdateData(...,"Money",...)`
writes are rejected so callers cannot bypass this contract.

This is an exact balance preimage, **not a monotonic revision column** or a
complete currency ledger. An external sequence that returns the balance to its
original value is indistinguishable from no change. The preimage protects the
net durable balance; it does not audit the history of unrelated writers.

## Reward and purchase integration

Book purchases, gold mail claims, atomic player trades, daily world-boss bounties,
and local/couple-PK champion prizes check the saved silver image under their
existing owner locks and database transactions. Their final committed balances
advance the image before later saves. Trade and mail also require their purse
updates to affect exactly one row. Receipt, item or purse failures roll back the
transaction and preserve the prior image.

Book purchases and mail retain their existing deferral while ordinary pending
silver awaits a save. Other integrated payouts can include those pending gains
in their committed balance. A genuine external balance conflict requires reload;
it is not resolved by guessing how to merge unrelated spending.

## Offline credits and login races

`EntityTable.UpdateMoney(uid, amount)` now credits a fully loaded canonical online
character under its owner lock, preserving pending silver and sending the
existing immediate balance update after commit. Disconnected or still-loading
characters receive one parameterized database increment, including balances
above `UInt32.MaxValue`. The increment rejects missing accounts, negative
purses, and overflow beyond `Int64.MaxValue`.

If login read its snapshot before an offline credit committed, the old snapshot
cannot later save over that credit. The session is fenced when it next attempts
a conflicting save or integrated settlement. Reconnection loads the durable
balance. This deliberately does not promise seamless merging of unsaved changes
across a competing login or external database writer.

M6.28 alone did not make the older auction transactional.
[M6.29/M6.30](AUCTION_INTEGRATION.md) subsequently replace auction settlement
with receipts and durable deliveries, so its refunds/proceeds no longer use
unreceipted offline increments. Native acceptance and legacy recovery gates
remain pending; other callers of an unreceipted increment still need their own
operation-level replay protection.

## Validation and limits

The isolated x86 build passes. Ordinary non-GM fixtures cover pending gains,
repeated saves, stale full and explicit saves, rollback and committed projection,
concurrent offline credits beyond 32 bits, online credit with pending gains,
full purses, null/negative stored values, login snapshot races, and rejected
generic-write bypasses. All sixteen book payments are followed by production
character saves. Mail, trade and boss tests verify subsequent purse saves;
mail and trade also preserve newer external credits and their unpaid rewards.

The final build passed 452 EXP/skill/book/silver/shop checks, 1,793 checks
across all 28 quest/event regression modes, 39 existing CP-shop checks and 16
source/producer/overlay audit tests: **2,300 checks**. This includes 31 added
focused checks and six added event integration checks. Failures injected during
validation affect only disposable fixture schemas; no public player rows are
used.
Validated assembly SHA-256:
`8F3F5B5A44BD2D80FDA7A3C9054F6A1A538AFF57E36FA5C17757FF94CF632288`.

Ordinary deltas still use the existing periodic save policy; a crash can lose
unsaved rewards. Older NPC, auction, bank, booth and poker operations still need
their own payment/delivery recovery where earlier side effects are separate.
No schema or packet layout changes are required. Native 6711 balance display,
reconnection and gameplay acceptance remain pending. No live deployment,
server restart, client installation or public patch is performed in this batch.
