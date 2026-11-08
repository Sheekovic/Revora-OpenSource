# Class PK War restoration contract

2026-09-15, baseline `6625504`. Implemented: isolated build, 116 Class PK checks
and 386 local PK/Power Arena/Elite PK/wardrobe/progression regressions pass.
Live-client acceptance and deployment remain pending.

The [official class guide](https://co.99.com/guide/quests/propkwar.shtml) supports
separate classes, separate Water/Fire Taoists, four level divisions, one survivor,
and Monday 19:30-20:30 with fifteen-minute advance registration. Revora selects
this edition: entry 19:15-19:30 Pacific, battle 19:30-20:30. Older weekday and
multiple-round editions are not combined with it.

The installed database contains the genuine envoy 705, mesh 1360, parked on 5001,
and escorts 706-745, mesh 1370, on aliases 160-199 of map 1730. The old handler
70500 has no matching installed NPC. Existing 705/706-745 handlers sort by nobility
and directly pay enormous prizes from map-alive counts; those branches must be
intercepted, including requests made at old locations.

Restore envoy 705 near Twin City (452,291). Each class/level division gets a
private 1730 arena and escort 706. The actual 96-by-96 floor has 3,208 valid tiles;
SHA-256 `c773a3a1135be5745a64f1b45ccb67f5c9eb70cc3dac1e65e75122052a5670d0`.
This preserves the installed arena terrain while separating it from Power Arena.

Supported source class families: Trojan 10-15, Warrior 20-25, Archer 40-45,
Ninja 50-55, Monk/Saint 60-65, Pirate 70-75, Dragon Warrior 80-85, Water 130-135,
Fire 140-145 and Wind Walker 160-165. Four divisions per family use levels
1-99, 100-119, 120-129 and 130+. An unspecialized Taoist must choose its school
before entering; unused client-only professions do not establish server classes.
Changing family or level division forfeits the frozen admission. Class promotions
within the same family remain eligible.

Reuse the owned local-PK coordinator, shared Elite PK combat gate, canonical
sessions, maximum 64 reservations per division, restart cancellation, safe deaths,
no reentry after admission, and atomic saved result/claim rules. Registration is
peaceful. One surviving competitor can win only after the battle begins, in a
field of at least two real entrants, after dealing positive accepted damage.
An idle sole entrant and unresolved deadline award no champion.

Only a genuine Class PK victory awards achievement 11131. No matching class
participation identity has been confirmed. Selected Revora champion purse:
50,000 CPs, 500 RVPs, 500,000 gold and five bound EXP Balls; seven-day class halo
from the result timestamp. These currency amounts are explicit Revora choices.
Old nobility ranks and map-alive counters grant no entitlement.

Native halos use status effects, not the unrelated title-list IDs. Installed
`statuseffect.ini`, SHA-256
`05FB159E8BD4AA30DD88A9F41D774A80D05B1DE79893D969ABFF0CAD756A3BDF`,
supports the source class effect indices: Trojan 39, Warrior 38, Archer 40,
Ninja 43, Monk 114, Pirate 122, Dragon Warrior 154, Water 41, Fire 42,
Wind Walker 203. Compact legacy database keys must be archived and excluded from
native title-list projection. Real-client halo/combat acceptance remains pending.

Validated fixtures: every family and promotion boundary, same-family promotions,
class/division changes, peaceful registration, cross-family attacks, separate
Water/Fire arenas, ordinary envoy/escort movement, Wind Walker eligibility,
simultaneous results, interrupted writes, saved purse recovery, honor expiry,
legacy handler blocking, and weekly/monthly/Elite regression coverage. Four
level boundaries reuse the already-tested weekly/monthly division function.

`LocalPkRules` reserves kinds 10-19 for the ten class families. The migration
preserves existing weekly/monthly history, adds frozen class identity to entries,
and archives old class status rows. The shared coordinator has at most forty
class arena copies, allocated only for admitted fields. Empty divisions produce
no world-chat announcement. Failed results retain their original decision time
and retry without allowing more combat or extending the honor.

Halo display prefers an earned current-class halo, then monthly, weekly, and a
still-valid former-class halo. Only one managed local-PK effect is projected at a
time. This is an explicit Revora display rule, separate from reward ownership.
No new native title-list identity or packet layout is introduced.

Validation executable SHA-256:
`6AFA6CE9EB9B9E02334EAFCC81A1E2A235F17CB6AA323E15EFB27333D436E864`.
The fixture covers genuine non-GM NPC dispatch, production jumps to all ten
escorts, normal attack/spell eligibility, concurrent battles, forced result and
prize rollback, replay, expiry and restart recovery. No live executable, client
configuration, public patch or website changed.

## Bound P7 champion reward (2026-09-21)

New Weekly, Monthly and Class PK tournaments add one bound catalog P7 soul to
the champion's existing purse and EXP Balls. Eligibility, winner resolution,
currencies and honors are unchanged. BP PK uses its separate claim path and is
not upgraded by this batch. Solo attendance and non-winning participation do
not become prize claims.

The saved tournament `valuable_reward` flag defaults false for existing rows;
new `Open` inserts set true without replacing an existing run. The soul UID
joins the original claim's reward array. Currency, all items and the checked
claim insert commit together; capacity includes the extra soul. Previously
completed claims are never reopened.

Build, 65 Local PK checks and 120 Class PK checks pass. Ordinary contested
combat earns Weekly, Monthly and class champion results. Coverage includes
concurrent claims, purse/receipt rollback, overflow, exact four-item Weekly
capacity, saved bound-soul receipts, exact item UIDs through production item
reload, and coordinator restart. An older Warrior class contract is marked
before enrollment, won through combat, reloaded after coordinator restart and
claimed twice for its original five-ball bundle only.

Logs: `obj/chi-refill-validation/local-pk-p7.log` and
`obj/chi-refill-validation/class-pk-p7.log` (ignored). This is item reload and
coordinator recovery evidence. A follow-up uses the complete isolated entity and
login schemas. Ordinary Weekly, Monthly and Class champions are loaded through
fresh production character/item loaders, then attempt the claim again. All
currencies and exact bound-soul/EXP-Ball UIDs survive without extra payment.
Character profiles are saved before claims, never afterward to backfill rewards.
Follow-up logs: `local-pk-reconnect.log` and `class-pk-reconnect.log` in the same
ignored validation directory. Genuine-client presentation and live deployment
remain open; these tests do not claim a native socket login.

## Trojan bracket champion through full process restart (2026-10-01)

Retained ordinary level100 Trojan participants4294908403/4294908404 continue
from their Weekly/Monthly matches with unchanged class, levels and equipment.
The real Class PK Envoy705 admits them during a controlled November2 Monday
19:15 Pacific window. The production arena uses installed map1730. After the
registration/preparation window, a controlled one-point normal ReceiveAttack
and the loser's ordinary death permit the coordinator to save a contested
Trojan champion with positive accepted damage. No result or item is seeded.

career-classpk-win/claim/reload all pass. The winner's unclaimed result survives
abrupt exit and database/game restart. The ordinary envoy delivers50,000 CPs,
500 RVPs,500,000 silver, five bound EXP Ball packs722136 and one bound P7.
All previous bag UIDs remain; exact items, balances, result, entries and nested
prior event/quest state survive another restart. Winner and loser each repeat
the claim twice without any change.

Evidence: OtherPkProcess/OtherPkSnapshot(true), private candidate SHA256
CE1B425A68EB9082F15EF2C39FD2F0B0EC6D21980360CB1B5EA3FFB708522F10.
This is the tested Trojan100-119 bracket, not all ten class brackets. Controlled
calendar/approach/damage/death and direct server handlers do not prove native or
authenticated fighting, natural scheduling, lost COMMIT replies or live release.
No gameplay rebuild or live deployment was required.

## Retained Windwalker champion (2026-10-01)

The retained ordinary level-140 Windwalker enters through Class PK Envoy 705,
option 1, and fights another ordinary Windwalker in the level-130+ bracket.
Both admissions are saved under `pk-19-20280605-3`; normal melee combat produces
the win and achievement 11131. No winner, damage value or death is injected.

The envoy's ordinary claim (option 3) after a process restart delivers exactly
50,000 CPs, 500 RVPs, 500,000 silver, five bound 722136 EXP Ball items and one
bound P7 soul. Existing inventory UIDs remain, reaching 40 used bag slots. The
repeat claim and another restart compare the full retained career, prior Arena,
Weekly and Monthly evidence, and Class result/entries/claim/returns. No duplicate
reward is allowed. Thus the successful repeat also runs with a full inventory.

Opponent class/level prerequisites are supplied, and it does not retaliate.
Calendar, admission/stage waits, proximity and attack cooldown are controlled.
This covers Windwalker at level 130+; it does not claim all ten class brackets,
PvP balance, native-client acceptance or production deployment.

Validation: `career-retained-class-win`, `career-retained-class-claim` and
`career-retained-class-reload` passed against candidate SHA256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
No production code, live deployment or client patch changed.

## Fresh Warrior opponent earned from character creation (2026-10-03)

The fresh career previously had only one Warrior. A second ordinary account
creates female Warrior FreshWar1 (UID1096331), opens RevoraStarterPack once,
and receives 20,000 earned CPs from the main character through an ordinary
mutually accepted trade. No level, class, equipment, stat or purse is seeded.
The starter supplies its documented 50,000 bound CPs and 1,100,000 silver.

Five bounded training batches buy and consume 452 EXP Balls through shop 2888
and the production item handler, paying 12,204 CPs. The peer reaches level 130
in class 20 without changing the main character's level 133 Warrior 25 profile.
The normal ten-ball daily allowance is preserved across 46 controlled dates;
45 next-ball attempts against the prior full day are refused without consuming
the item or changing experience. Reusing each consumed item changes no saved
career state. The previous five-character career is preserved in nested snapshots.

Evidence: CheckCandidateFreshWarriorPeer.cs and the private
career-fresh-warrior-peer-create/train/reload phases, candidate SHA256
D48A6C00F6BF84889581ADFEABEB924C4C7448C5DC3C6D17F8674129C43665B6.
Calendar advancement is controlled and does not prove 46 elapsed real days.
This is a Class PK prerequisite, not a Class PK victory or release claim.

## Fresh Warrior Class PK and cold rewards (2026-10-03)

The earned level 133 twice-reborn Warrior 25 and the newly earned level 130
Warrior 20 enter through Class PK Envoy 705 option 1 during the controlled
September 30, 2041 Monday 19:15 Pacific registration. Both enter the installed
1730 arena in the same 130+ Warrior bracket. After the 15-minute window, one
calculated melee hit defeats the new opponent and the coordinator records
`pk-11-20410930-3`, positive accepted damage, two competitive admissions and
achievement 11131 (`class-pk-v1`). Both return to town; the loser revives through
the ordinary revival handler. No damage, death, winner or result is supplied.

The unpaid result survives process/database restart. Envoy 705 option 3 grants
exactly 50,000 CPs, 500 RVPs, 500,000 silver, five permanent bound 722136 EXP Ball
items and one permanent bound P7 soul. The next restart loads those exact six
UIDs, types and binding, balances, admissions, claim and return receipts, with
the previous career unchanged. Winner and loser each repeat the claim twice
without additional rewards. All three phases passed on the first execution.

Validation: `career-fresh-class-earn`, `career-fresh-class-claim`,
`career-fresh-class-reload`, in CheckCandidateFreshClass.cs against the same
D48A6C00F6BF84889581ADFEABEB924C4C7448C5DC3C6D17F8674129C43665B6 candidate.
The preparation registration script needed a corrected source-list anchor before
execution; no gameplay or reward recovery was necessary.

Calendar, proximity, attack cooldown and revival delay are controlled. The
opponent does not retaliate. This proves the fresh Warrior 130+ earning and
recovery path, not all class brackets, competitive balance, natural scheduling
or genuine-client acceptance. No live deployment or client patch was made.
