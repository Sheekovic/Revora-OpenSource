# Sword Master owned admission transaction

Current integration: the [owned encounter](SWORD_MASTER_ENCOUNTER.md) now calls
this participant from ordinary item confirmation and owns combat/recovery.
The prerequisite-only availability statements below are historical. Native and
live Host acceptance remain unfinished.

Source prerequisite, 2026-09-21. `SwordMasterAdmission` records an exact daily
attempt and consumes its Sword Soul atomically. It is deliberately not initialized
by production or called by the player item/confirmation handlers yet. The complete
combat, rewards and startup recovery must be connected before charging players.
The legacy unowned summon remains disabled.

## Saved contract

`revora_sword_master_runs` records a UUID, owner, Pacific admission day, registered
house map/terrain/level, selected spawn tile, exact item UID, original stack and
binding, frozen party, state and timestamps. Unique owner/day and active-owner
constraints prevent overlapping attempts. An active older attempt also blocks a
new day until the lifecycle closes it. Failed attempts retain their day reservation;
there is no item refund or automatic reopening. Clock rollback cannot admit a day
before an existing attempt. Schema initialization preserves current attempts;
it does not simulate a restart or mark live encounters failed.

Admission requires the canonical eligible level-100+ owner, its inventory lock,
actual Housing ownership, exact registered map reference and metadata, no trade,
booth or poker activity, a usable owned item, an open monster tile clear of nearby
actors, and a coordinator authorization callback. The durable character level,
house row and map reservation are checked again under transaction locks. The
house-level check reuses Housing's validated terrain contract. The saved bag count
must match the live inventory before any item is spent.

One unit of the exact Sword Soul is removed, including bound and stacked items.
A one-unit/legacy-zero stack deletes its row; a larger stack decrements it. The
original ownership, stack, binding, lock, storage and expiry conditions remain in
the conditional write. Admission receipt insertion and consumption must each
succeed. The house/item/party/spawn context is rechecked before commit. Inventory
updates follow commit; a failed post-commit projection fences the session.

The party snapshot includes the owner and up to four eligible current teammates
already present in that house. Actual invited-team traversal and reward eligibility
must still be exercised by the upcoming encounter integration. A trusted failure
participant can close only the exact owner/run while active, once. It is not a
player command or a restart-recovery coordinator.

## Validation

Isolated build and all 464 Housing checks pass. The fixture constructs
and enters real owner houses through the existing NPC service; Sword Souls are
fixture-supplied. New tests cover authorization, level/lock refusal, concurrent
stack consumption, saved map/terrain/owner party, production reload, unchanged
active receipt after initialization, exact-owner failure, same-day/clock rollback,
next-day admission and foreign-house refusal. Fault injection suppresses the run
receipt or item deletion, or withdraws the callback before commit; each must retain
the item and release the daily reservation so a valid retry succeeds.

The context callback is fixture-provided; this is transaction acceptance, not a
native item-use/boss journey. No monster is spawned, no kill reward or spirit bonus
is granted and no native packet layout is introduced. Follow the remaining work in
[Sword Master restoration](SWORD_MASTER_RESTORATION.md). No live deployment.
