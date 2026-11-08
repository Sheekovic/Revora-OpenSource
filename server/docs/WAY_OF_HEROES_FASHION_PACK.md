# Fashion Suit pack: atomic seven-day reward

## Evidence and Revora reward rule (2026-10-02)

Installed private `database/itemtype-new.txt` identifies3001264 as FashionSuitPack
and describes a random seven-day bound garment. The legacy ItemHandler removed
the pack before adding a random item from181305-194300 and omitted the expiry.
That could lose the pack on failure and incorrectly create a permanent garment.

The existing owned `GemDragonSoulPacks` interception now also handles3001264,
covering both sealed item-use dispatch and direct UseItem without editing the
owner's unrelated ItemHandler/ItemUsage WIP. It selects uniformly from the
intersection of the historical numeric range, audited compatible garment designs,
loaded item catalog/garment category and loaded wardrobe type1/five-star rows.
The five-star restriction implements the owner's valuable cosmetic reward policy;
it is a documented Revora choice, not an assertion about original pack odds.
Empty or unavailable pools preserve the pack and refuse with an explanation.

The output is bound, plus0, stack1 and Days7, with its start timestamp set at use.
Pack debit, reward metadata and `revora_fashion_suit_pack_uses` receipt commit in
one owner-locked transaction before inventory projection. Existing owner, lock,
expiry, capacity, malformed-frame and session guards remain. Other gem/Dragon
Soul pack pools and their receipts remain separate. Existing permanent garments
are not retroactively changed. Seven-day rewards do not satisfy the collection
achievements' saved Days0 requirement; that existing filter was source-reviewed.

## Rollback, use and restart verification

`tools/CheckCandidateFashionPack.cs` adds `career-fashion-pack-earn/reload` for
one ordinary non-GM character with supplied level100 and a bound single pack.
This is consumer validation, not earned stage302 validation.

An invalid random index leaves the exact snapshot untouched. A private database
trigger then injects failure when the new consumption receipt is inserted, after
the attempted debit/output writes but before commit. The handler marks the session
for reload and the exact original pack/player snapshot remains unchanged. The
trigger/function are removed in finally; their removal is also verified.

After ordinary character reload, eight concurrent use requests produce exactly
one audited bound five-star garment, with Days7 and one receipt. The original
pack is gone; replay leaves the snapshot unchanged. Normal equipment into slot9
succeeds. Ordinary save and game/PostgreSQL stop/start preserve exact item expiry,
binding, ownership/equipment and the scoped player/receipt snapshot. The garment
loads equipped with its seven-day duration. No receipt or output is fabricated.

The isolated committed-source-overlay build passed with existing legacy warnings;
the harness compiled with warnings as errors. Both phases passed first run,
including the deliberate transactional failure and recovery. Private game SHA-256:
`0DA85764703FA539E4394B4FC361A7F228A5ECC4D03636BB2ADFC3806F09763E`.
Logs: `arena-process-career-fashion-pack-{earn,reload}.log`.
Both game closes completed and PostgreSQL stopped. Old executable/manifest
preserved; unrelated owner changes excluded from the build.

Native/authenticated display, every garment/body appearance, actual seven-day
elapsed removal, full-inventory/stacked-pack variants and live deployment remain
pending. The controlled draw and supplied input pack/level are explicit limits.
No new milestone is enabled: equipment-quality earning predicates and their
ordinary end-to-end reward chain still need implementation. The retained
main-career checkpoint is unchanged.


The subsequent [quality-set batch](WAY_OF_HEROES_QUALITY.md) enables stage302 and
verifies earning this pack through eighteen ordinary paid quality upgrades, then
opening and equipping its reward. The consumer-only limits above remain scoped
to this original prerequisite batch.
