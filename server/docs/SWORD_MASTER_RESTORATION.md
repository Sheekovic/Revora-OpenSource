# Sword Master restoration

## Retained Arena earning prerequisite (2026-10-01)

The retained non-GM career now enters solo Arena through sealed 2205 signup and
acceptance requests, normal matchmaking and normal ranged combat against its
retained peer. No Honor, damage amount or winning result was supplied. Positions,
pre-fight/revive timers, attack cooldown and the December 2/3 test clock are
controlled; this is private handler/process acceptance, not native UI or balance
acceptance.

The journey exposed a real first-entry failure: the peer had 2,000 saved solo
points and zero team-mirror points. Legacy solo loading could also reset only
its cache before the durable admission transaction. Loading now preserves saved
points whenever durable rollover is initialized. First adoption may reconcile
an unmanaged character's point mirrors within the locked rollover transaction;
`team_points_before` records the previous mirror beside `points_before`.
Already-managed characters still reject mismatches, missing rows remain errors,
and Honor is not changed by this reconciliation.

The retained run includes three abandoned fights during fixture debugging and
two completed normal-combat wins. The fixture initially failed to load the peer's
combat stats, then advanced the wrong countdown, and finally selected an old
abandoned `active` row instead of the current group's exact participation key.
Those are fixture defects, not additional production fixes. The corrected lookup
uses the group's key. A narrowly guarded recovery phase repaired only probe
metadata after verifying the two durable wins; it did not replay a reward or
fabricate a result. Earlier admissions and abandoned records are preserved.

The ranking scheduler sealed the completed day. Rank 1 earned the existing
1,000,000-Honor award (no reward tuning in this change). Payment updated both
mirrors, repeated payment did nothing, and an abrupt process exit followed by
cold reload preserves the complete saved snapshot. The fixture permits startup
to add an unpaid ranking row between the combat and payment checkpoints; all
other state must match. Build succeeds with existing warnings. Private executable
SHA-256: `1109CC262F8FBAA65A50451A60BE104FC2FB4A1FD885A72B11E444FE0C19E91E`.

- [x] Ordinary Arena admission with first-use legacy mirror reconciliation.
- [x] Retained normal combat and durable outcomes.
- [x] Saved ranking Honor, duplicate refusal and cold persistence.
- [x] Spend that earned Honor on the 8,000-Honor Sword Soul and retain the exact item.
- [x] Complete this character's owned Sword Master summon/reward journey.

No live deployment, server restart or client release. Native acceptance remains
pending. The subsequent [four-process Sword Master journey](SWORD_MASTER_ENCOUNTER.md#retained-earned-honor-journey-2026-10-01) closes the two earning steps above; it does not certify native/live availability.

## Current integration

The owned encounter is now connected in candidate source: ordinary item use and
confirmation, seven named skill behaviors, house/party combat authorization,
atomic victory/rewards/1,000-spirit credit, failure cleanup and production
startup/timer wiring. See [current contract and acceptance limits](SWORD_MASTER_ENCOUNTER.md).
This supersedes the dormant/prerequisite-only descriptions below, which record
how the unsafe legacy route was closed and replaced. Native/client and live Host
acceptance remain open; there has been no deployment.

## Earlier research and prerequisite checkpoints (2026-09-21)

The [official Sword Master guide](https://co.99.com/guide/monsters/sword_master.shtml)
requires level 100 and the owner's house, permits one Sword Soul summon per day,
and names Honor/Horse Racing stores as acquisition sources. Victory awards 20
Study Points per teammate and two randomized drops: a 60% artifact (P1-P3) pool
and 40% weapon-accessory pool. It lists seven attacks, including healing, without
publishing numerical combat parameters. A server interruption forfeits the day's
attempt. The [Spirit Bead guide](https://co.99.com/guide/quest/spirit-bead.shtml)
assigns Sword Master 1,000 spirits. These describe original behavior; exact local
combat/loot implementation still needs validation.

Local source identifies Sword Soul as item 720842 and the boss catalog family as
4170. Loaded `database/shops/HonorShop.ini` stocks the item for 8,000 Honor;
`HonorShop.Load` and the current item-purchase handler establish that source.
The previous wilderness research correctly found no normal spawn, but missed an
active legacy summon continuation in packet 2032. That omission is corrected here.

Item use checked `(ushort)character UID` as the house map, which contradicts the
collision-safe `HouseMapRegistry` now used by Housing. It issued a confirmation
that only checked inventory possession and the RAM `SwordSoul` flag before
spawning family 4170 on the character's current map and removing an item. The
continuation did not revalidate house identity, ownership, level or the original
item UID, and lacked an atomic durable attempt/item transaction. The old Honor
purchase likewise grants an item before subtracting mutable Honor and separately
saving statistics; it is not yet evidence of crash-safe acquisition.

## Honor acquisition prerequisite implemented

The candidate Honor Store now commits both Honor mirrors, the item and a durable
purchase receipt together, protects concurrent saves and suppresses rapid duplicate
clicks. Sword Soul retains the real 8,000-Honor price. Build and 488 combined
checks pass ([transaction scope and limits](HONOR_PURCHASE_TRANSACTIONS.md)).
The earlier unsafe shop description above is historical. The retained journey above now covers actual match earning and the owned house
encounter in private processes; native/live acceptance remains pending.

## Owned admission prerequisite

A durable exact-item/house/day admission participant is implemented; the build and 464 Housing checks pass ([contract](SWORD_MASTER_ADMISSION.md)). It is not connected to player
item use or production startup. The complete encounter/reward coordinator must
own its authorization callback and recovery before the service may charge players.

## Durable rewards prerequisite

The reward transaction participant and physical floor delivery are implemented
([contract, Revora tuning and limits](SWORD_MASTER_REWARDS.md)). The isolated build
and 557 combined Housing checks pass. The fixture supplies a victory inside the
transaction; this does not prove owned combat, Soul Ball spirit credit or an
available player summon. The implementation sequence below remains open for those
integration and native acceptance requirements.

## Legacy bypass closed

The unsafe item menu and its continuation are disabled globally while the owned
replacement is built. Using an existing Sword Soul leaves it untouched and says
the rite is dormant. Old confirmation state is cleared without creating a monster,
consuming the item or changing the daily flag. There is no owner/GM exception.
This deliberately does not claim that Sword Master is restored or grant spirits
for arbitrary legacy family-4170 deaths.

The isolated build and all 11 quest-packet checks pass. Added ordinary-player
checks use actual item dispatch and sealed 2032 replies, repeat stale confirmation
requests, compare map actors/currency/daily flag and load the retained item through
the production item loader. The disposable fixture creates the item-addition table
required by that loader. No live server, database or client changed.

## Implementation sequence still required

1. Complete played Arena-to-shop acceptance over the implemented atomic Honor
   purchase. Shared solo/team saves, duplicate requests and recovery are tested;
   no free quest-only token grant replaces the loaded economy.
2. Admit an owned summon using the exact issued item UID, level eligibility,
   canonical house registry/metadata, reachable unoccupied terrain and a durable
   once-daily receipt. Freeze the team and ensure failure/restart handling cannot
   consume another player's item or reopen an attempt.
3. Add registered encounter ownership, attacks, full boss HP presentation and
   cleanup. Prevent a house upgrade/removal from invalidating a live encounter.
   Prefer existing tested combat/loot primitives; do not copy the unsafe callback.
4. Commit victory, eligible team Study Points, validated loot identities and the
   killer's owned Soul Ball spirit credit together. Add the owner-required valuable
   event reward with explicit Revora tuning; preserve the no-P7 daily-quest rule.
5. Verify acquisition through house admission, victory, reward collection, failure,
   concurrent callbacks, expiry and reload for ordinary accounts. Then integrate
   startup/timers, compose private client content and perform genuine DX9/Host
   acceptance before any live availability claim.

The seven original skill names alone do not prove packet layouts, damage formulas,
spell IDs or an accessory identity pool. Research those details before claiming
original parity. The broader quest/event goal remains active.


## Fresh earned counterpart and Arena ranking (2026-10-03)

`CheckCandidateFreshHonor.cs` continues ordinary UID1096322 from its paid house
and furniture checkpoint. The existing ordinary trainee1096323 retains its
creation, starter and Ancient-contributor history. A normal mutually accepted
trade moves1,000 earned CPs from the main character to the trainee; repeating
acceptance transfers nothing further. Twenty ordinary27-CP EXP Ball purchases
cost540 CPs and raise the trainee to70, qualifying it for the ordinary Arena-point
refill. The first ten balls did not reach70, so the initial bounded preparation
stopped at65 with every purchase/use saved. The corrected continuation proves
an eleventh same-day use is refused, then uses the next controlled calendar day
for the remaining ten. Exact two-character cold recovery passes. No balance,
level, equipment, points or result is supplied to either character.

On controlled day20410402, both characters use sealed signup/acceptance packets
and normal matchmaking. The level132 Warrior wins through two calculated melee
attacks; the exact group's participation key records the result. Its Honor stays
unchanged before daily settlement. The fixture controls approach, pre-fight and
attack timing and does not establish retaliation or class balance.

The first payout process stopped at snapshot comparison: the combat fixture had
exited before the arena's delayed return. Loading normalized the retired arena
map11020 to1002 for both characters, repeated in nested snapshots. A complete
JSON comparison found no item, currency or result differences. The scheduler
then independently sealed two unpaid rank rows (4030/rank1 and970/rank2). These
are the only additional differences in the second reviewed preimage.

Recovery accepts only these exact reviewed SHA-256 snapshots:

- Saved fight: `e055233d6e650e0fbccc5fe6bcad52e4aed02ee9f325e5099dcad3b1c29887d5`.
- Loaded map normalization: `40818b63ea9bb09aafef5c30b90a90d49e32a3d5ec79e650cfe9bd21bdc284e3`.
- Same state plus unpaid ranking: `0ad32c942a6291768da91d19dcf7e8b40a3e3c53ff47e75c084a1511df0f7988`.

It completes the controlled town return and saves the new checkpoint without
replaying combat, changing the result or granting Honor. The test now advances
the post-fight return countdown before saving future fight checkpoints; that
edit is not a claim that the interrupted run observed a natural return.

The next-day production ranking payout grants the main character exactly
1,000,000 Honor under the existing Revora rank table. Solo/team Honor mirrors
agree, repeated payment leaves the full snapshot unchanged, and a further cold
process preserves payment and both characters' careers. The peer's rank award
remains unpaid in this batch. The paid checkpoint is `revora_fresh_honor_probe`
stage3. Passing phases include preparation/reload, the recorded fight, reviewed
recovery, pay and final reload; the interrupted fixture attempts remain failures.
The case does not prove native login, natural-time competition or live release.
