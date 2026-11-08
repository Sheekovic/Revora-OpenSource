# Wardrobe deposit persistence

2026-09-25. Source validated; production deployment and genuine-6711 acceptance
remain pending.

The Addtowardrobe branch previously added a bag item to the live collection and
set its wardrobe flag before checking the database write. Its update matched only
the item UID and ignored the affected-row count. A suppressed update or stale bag
projection could therefore grant a live collection entry that would not survive
reload. That branch also lacked a garment/mount category check before mutation.

The initial deposit now requires the canonical, loaded, alive ordinary player,
with no active trade or booth. It checks the requested identity against the owned
item and accepts only known garment or mount-armor item types. A zero ItemId is
still accepted for compatibility; a supplied nonzero identity must match.

Under the existing item lock, the transaction locks the character and compares
the persisted item owner, type, bag location, stack size, wardrobe flag, binding
and rental term against the live item. Exactly one saved row must change before
the bag/collection projection changes. Expired rentals are rejected. A database
failure or stale location fences the session for reload, preserving the original
bag projection when the write did not commit. An interruption after commit is
recovered through the existing production item loader.

Equipment selection follows the deposit as a separate operation. Subsequent
withdrawal, takeoff and equipment-switch fixes are described below. A deposited
item remains safely stored if the later equipment selection fails; the combined
deposit-and-equip request is not one transaction. No new achievement producer is
enabled by these persistence changes.

## Validation

The isolated x86 build succeeded with existing legacy warnings. The build includes
the owner's unrelated worktree changes; it is not a clean release candidate.
`CollectionAcquisitionOnly` passes **86 checks** in the disposable validation
schema, including seven new deposit checks:

- Reject a mismatched requested design, a non-cosmetic bag item and active trade.
- Suppress the deposit update and prove no live wardrobe entry or bag removal;
  production reload restores the original item and a subsequent request succeeds.
- Change the saved item location while retaining a stale bag projection; reject
  the deposit, fence the session and recover through production reload after the
  fixture restores the saved bag location.

The existing earning route still kills a scheduled boss, spends its RVP bounty
on eleven designs, deposits them, checks duplicate requests, reloads the earned
titles and exercises three actual Fashion placements. Euro jersey and Big Fan
purchase/persistence checks also pass. Logs are local ignored artifacts:
`obj/wardrobe-deposit-build.log` and `obj/wardrobe-deposit-tests.log`.

No live Host restart, client patch or native acceptance was performed.

## Withdrawal follow-up

The Retrieve branch also changed the live collection before saving its wardrobe
flag and location in separate writes. It now commits both `inwardrobe=0` and
`position=0` in one owner-locked transaction before removing the equipped/stored
projection. The predicate verifies the saved item owner, identity, binding,
location and rental term. The saved bag UID set must match the live bag and leave
room within the normal forty-item capacity. An equipped withdrawal additionally
requires that exact item instance in its canonical cosmetic slot.

After commit, equipped items are removed through the existing no-bag-write
equipment projection, inserted into the live bag without another database write,
and sent to the client. Equipped stats and appearance are recalculated. Failed
writes fence the session and leave the original collection/equipment unchanged;
duplicate requests after a successful withdrawal cannot insert another item.

The expanded collection fixture passes **103 checks**, including stored and
equipped garments, active-trade rejection, supplied full-bag capacity, suppressed
updates, production character/item/wardrobe login recovery, successful withdrawal
reload, duplicate requests, loss of current Fashionist eligibility and restoration
on redeposit without payment. The fixture now explicitly runs `CoatStorage.Login`
when checking equipped recovery; item loading alone does not populate wardrobe
equipment slots. The full-bag objects are test-supplied, not claimed earned items.

Logs: `obj/wardrobe-withdrawal-build.log` and
`obj/wardrobe-withdrawal-tests.log`. The isolated build succeeds with the same
legacy-warning and owner-WIP limitations. Native presentation, post-commit process
crash injection and auto-equip atomicity remain unverified; neither this result nor
the earlier deposit checks claim those gates complete.

## Takeoff follow-up

Takeoff previously sent a cosmetic-hide packet without clearing the equipment
slot in memory or the saved item position. It could also add a bag item to the
live collection without saving wardrobe ownership. The handler now accepts only
the canonical owner's already-stored, actually equipped garment or mount armor,
and rejects trade/booth contexts, mismatched identities and garment changes in
Quarantine. It commits the saved position change under the character lock before
clearing the equipment projection, recalculating stats and sending appearance
updates. The item stays in the wardrobe and never requires a free bag slot.

A shared, item-lock-required cosmetic unequip projection performs no database
writes and checks the expected equipment instance. Withdrawal also uses it after
its own transaction. This avoids the legacy generic removal method's bag-capacity
requirement for an operation that does not itself add a bag item.

The suite now passes **115 checks**. Twelve additional checks cover both slots:
trade rejection, suppressed-write preservation, production login recovery,
successful takeoff with forty supplied bag items and duplicate requests,
unequipped persistence without losing collection titles/currency, and subsequent
re-equip. The mount test supplies riding-subclass qualification explicitly; it
does not claim to earn that prerequisite. Both cosmetic items still come from
the earlier boss-funded shop purchases.

The isolated build succeeds with existing warnings and unrelated owner WIP.
Evidence logs are `obj/wardrobe-takeoff-build.log` and
`obj/wardrobe-takeoff-tests.log`. Native appearance acceptance and atomic equipment
switching remain open. This batch does not add collection achievements or deploy
the live server.

## Equipment-switch follow-up

Equipment switching now locks the character, verifies the saved occupant of the
cosmetic slot, clears that occupant and sets the selected wardrobe item's slot
in one transaction. Suppression of either write rolls back both. The replacement
must match its saved owner, type, binding, rental term, wardrobe status and stored
location. Quarantine and riding-subclass restrictions remain in effect.

After commit, an old wardrobe cosmetic remains stored. A cosmetic equipped through
the ordinary bag path returns to the bag; that path checks saved/live bag identities
and capacity before commit. Switching two wardrobe items requires no bag space.
Both item projections and appearance are updated after commit, with a reload fence
if installation fails. Repeated selection of the same equipped item is idempotent.

The collection suite now passes **131 checks**. It suppresses the displaced-item
and selected-item writes separately for both garment and mount-armor slots, checks
rollback and full production login recovery, switches with forty supplied bag items,
replays the request, and reloads both saved identities. An additional ordinary
`ItemHandler.EquipItem` path equips an earned garment outside the wardrobe and
proves that a subsequent wardrobe selection returns it to its saved bag; reconnect
and redeposit preserve it without another purchase. Riding qualification and bag
fillers remain explicit fixture prerequisites rather than claimed earning routes.

All collection-fixture reloads now run `CoatStorage.Login` to restore equipped
wardrobe items before further interaction. The stricter saved-slot comparison
correctly rejected the earlier incomplete fixture login sequence.

Latest logs: `obj/wardrobe-switch-build.log` and `obj/wardrobe-switch-tests.log`.
The isolated build succeeds with existing warnings and unrelated owner WIP.
Older sections' equipment-switch deferrals are superseded by this source checkpoint;
native appearance, post-commit process-crash injection and the broader achievement
earning rules remain pending. No live executable or client files were changed.
