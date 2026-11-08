# Leftover HP/MP/LifeFruit medicine

M6.56b9r / M6.56b9s, 2026-09-23. Server implementation. Not deployed.

Ordinary 1009/action-4 and direct `UseItem` of leftover HP/MP/LifeFruit
medicine go through `HealingMedicine`. Those leftover cases fail closed.

## Leftover terms (preserved)

HP IDs 1000000/10/20/30/40, 1002000/10/20/50 and 725065 add catalog `ItemHP`
capped at MaxHitpoints. MP IDs 1001000/10/20/30/40, 1002030/40 and 725066 add
catalog `ItemMP` capped at MaxMana. LifeFruit **723726** adds ItemHP and sets
Mana to MaxMana. Maps 1707/1801 refuse with leftover texts; HP/LifeFruit also
refuse inside Wave Defense. Active leftover `NoDrugsTime` refuses without
consume; an expired leftover cooldown is cleared to 0 then the heal proceeds.
Leftover does not start a new cooldown after a successful heal.

Ordinary Welcome pack **723753** issues 1000000 and 1001000.

Ordinary positive `NoDrugsTime` comes from leftover Poison Star **6002**.
`Handle` routes a successful hit through `HealingMedicine.TryApplyPoisonStar`,
which persists `revora_no_drugs_state` under the target `ItemSyncRoot` before
projecting leftover Time/flag/UI. The durable row stores `poison_star` so a
positive remaining that is not a Poison Star grant cannot resurrect the flag
on reload.

Serenity **10400** and both Tranquility **10425** branches persist a durable
clear (`TryClearNoDrugs`) before leftover PoisonStar flag removal. Dead
Tranquility targets are allowed. Solo Qualifier `Win` clears durable Time
without leftover PoisonStar flag removal; reload of remaining 0 cannot
resurrect the flag. Team Qualifier `RestoreFighter` persists the leftover
one-second reset with `poison_star=false` and drops the live PoisonStar flag
so reload cannot resurrect it. Unused leftover `TeamQualifier1.cs` is not in
the project and stays unmodified.

A durable write failure fences the target and does not apply live Time or
flag. Medicine use, Poison Star and cleanse/reset share that lock. Leftover
duration/boundary/UI stay; leftover does not start a cooldown after a
successful heal. Other leftover Serenity/Tranquility/Qualifier debuff clears
stay split-writes.

## Owned contract

One owned unit is consumed with stack remainder kept. Durable entities
Hitpoints/Mana CAS and `revora_no_drugs_state` cooldown remaining commit with
receipt `revora_healing_medicine_uses`. Failed source, HP/MP, cooldown or
receipt writes roll back together. Direct daily P7 is unchanged.

## Remaining work

VIP-day tokens are catalog + unreachable UID 6001 shop / leftover consumer
([VIP_DAY.md](VIP_DAY.md)). DisguiseAmulet 723724 is catalog + leftover consumer
([DISGUISE_AMULET.md](DISGUISE_AMULET.md)). HonorShop leftover GetNextRefineryItem
packs are owned ([HONOR_REFINERY_PACKS.md](HONOR_REFINERY_PACKS.md)). Catalog-only/other leftover packs, Host
restart, native acceptance and deployment remain open. Parent
[M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.


## Unsaved combat health correction (2026-09-27)

The authenticated Pirate flower hunt reproduced a real rejection:8352 live HP
versus10000 saved HP caused a potion to throw `Durable HP/MP changed; reload required.`
Ordinary combat changes live health before autosave, so comparing the current
live values directly to the database was incorrect.

`HealthBalance` now retains the last committed HP/MP image from entity load.
Medicine writes its new health against that image inside the same transaction
as item consumption, cooldown state and receipt. Live combat damage or mana
expenditure may differ from the saved image. An unexpected database change
still rejects the transaction and fences the session; removing the stale-writer
check was not the fix. The saved image advances only after successful commit.

Character saves use the same optimistic preimage and update it after saving.
The existing Desert Expedition pill debit and Mad Man healing outcome use the
shared health writer so they cannot leave its baseline stale. Loaded players
participate in the existing ordered health-mutation lease; saves and Mana writes
also use the health gate. This preserves the lock order of owner before health.
No new per-hit database write or database table is introduced.

Validation on private gameplay SHA256
`02480BEB0990A0D88D95855927D68E2F2A28A87A5D8F27896FA27960E7CDB583`:
`obj/health-balance-build.log` passed with existing legacy warnings;
`tools/Verify-ArenaProcess.ps1 -HealthPersistenceOnly` passed in
`obj/health-persistence-probe.log`. The focused probe covers unsaved HP and MP,
eight concurrent uses of one potion with one receipt, rollback after a forced
receipt failure, reload/retry, external HP mutation rejection without consumption,
and stale autosave rejection without overwriting the external value. Expected
rollback/conflict diagnostics in that log are asserted test outcomes.

The first socket run on this fix healed repeatedly during nine actual monster
kills/pickups, then failed on a different monster family's lethal attack. That
run is evidence for the medicine correction, not flower quest completion. Full
flower acceptance and the PvP smoke check are recorded separately when complete.
Native acceptance and live deployment remain pending.


The complete12-flower combat/pickup/offering/reconnect run subsequently passed
on02480...B583 in `obj/pirate-flower-healing-fixed.log`; see the linked Pirate
restoration scope. Final review replaced remove/reinsert of the health snapshot
with one volatile reference assignment inside a stable holder, avoiding a brief
unmanaged-state gap visible to concurrent combat.

Final executable SHA256:
`DD61E4AD95D583179236E66A11FD820D2A0A39275CB175404C3F51D802C407CB`.
The focused health persistence suite passed again in
`obj/health-persistence-final.log` on this final build. Prior flower evidence
retains its original executable hash. Neither result establishes native client
acceptance or authorizes a production deployment.

The final build also passed `-ClanWireOnly` in `obj/health-clan-wire.log`:
actual opposing-player TCP combat settled the winning clan, excluded the loser,
and delivered immediate bound soul/CP/RVP rewards. This checks the shared
health lease in PvP as well as medicine's focused transaction cases.


## Bundle entry extension (2026-10-03)

The service now also owns720010-720017 unpacking. Those IDs use a separate
inventory transaction and journal; they neither heal nor enter the healing
mutation lease. Existing pill use retains its original path. Eight exact
outputs, capacity/refusal, concurrency, failed-write recovery and cold persistence
pass privately. [Evidence](MEDICINE_BUNDLES.md#atomic-bundle-delivery-2026-10-03).
No issuer or live deployment was added.
