# Vesper purchase safeguards

## Bound P7 souls and ordinary admission (2026-09-25)

Vesper's non-rune transaction already committed the item and RVP debit together,
but created all merchandise unbound. Weapon and armor/accessory P7 soul choices
now create bound items, matching the owner's P7 binding rule. Existing catalog
prices are unchanged: 12,000 RVPs for weapon souls and 10,000 for armor/accessory
souls. Stones, garments and mount armor keep their existing unbound behavior.
Previously issued items are not retroactively modified.

The NPC handler now requires a canonical, fully loaded ordinary session near
the actual Vesper actor in Twin City, with no trade or booth. This applies to
all menus and purchases, including runes. The non-rune transaction rechecks that
eligibility under the inventory lock and again before commit. A buyer who loses
eligibility before commit cannot retain a debit or item. There is no GM, VIP or
character-name allowlist. Purchases retain the existing inventory capacity and
saved-purse preimage checks; postcommit installation failure still fences for reload.

The isolated build passes. `-PurseReviewOnly` completes with zero failures:
406 first-rebirth prerequisite checks plus the purse review aggregate produce
the runner's printed count of 407; the log contains 670 individual PASS lines.
The added nine checks cover bound weapon and armor souls, exact unchanged prices,
full save/production reload, and remote/fake/trading buyer refusal without debit.
Existing Vesper stone purchases, external purse collisions and forced item-write
failure still pass. The old direct-call fixture now places the real NPC and its
buyer together rather than bypassing location requirements.

This batch does not establish complete acquisition of every garment/mount design,
native NPC presentation, a real Host restart or live deployment. The build uses
the existing worktree with owner WIP preserved. No live binary, database or client
patch was replaced. Rollback is the source commit reversal; newly issued bound
items should retain their saved binding.
