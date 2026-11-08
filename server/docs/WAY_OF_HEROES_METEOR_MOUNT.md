# Way of Heroes: Meteor upgrade and steed

## Installed rules (2026-10-02)

Private `database/Slayer/stagegoal.txt` sections `[2-5]` and `[2-7]` specify:

| Stage | Task | Opening | Requirement | Bound reward |
| --- | --- | --- | --- | --- |
| 205 | 45 | level 31 | Upgrade equipment with a Meteor once | Steed 300000 |
| 207 | 44 | level 31 | Equip a steed | Meteor 1088001 |

Stage 205 now reads the character's successful `revora_equipment_upgrades`
operation-1 receipt with a changed item type. Failed attempts or current equipment
level alone do not prove this requirement. The existing upgrade service owns the
atomic material debit/result/receipt transaction; historical successful records
qualify without keeping the target item.

Stage 207 checks the saved permanent single item in shared slot12, its catalog
steed category and matching runtime owner/item identity. Duplicate saved slots
fail closed. Bag, warehouse, suspicious and wardrobe items do not qualify. The
shared steed slot works while alternate equipment is active. The normal item
service still enforces equip eligibility; receiving a steed does not waive it.

Both stages retain installed opening gates, bound plus-0 single rewards,
capacity/session validation and the atomic unique-claim transaction. No new
protocol or storage schema is introduced. Unimplemented/main-stage prizes stay
refused.

## Ordinary reward chain and cold restart

`tools/CheckCandidateMountClaims.cs` adds `career-mount-claims-earn/reload`.
One ordinary non-GM character has supplied level100, no rebirth, 50,000 CPs and
low-level bound boots. Both claims initially refuse. The actual CP shop sells a
Meteor at its installed price; the normal sealed item-use handler consumes it in
a successful level upgrade. Success rolls are controlled and separate attempts
advance the upgrade test clock by two seconds to respect duplicate protection.

Eight concurrent stage-205 requests issue exactly one bound plus-0 single steed.
The steed in the bag cannot claim stage207. Normal equipment of that earned
steed into slot12, followed by the ordinary alternate-equipment switch, permits
one bound plus-0 Meteor across eight concurrent requests. Normal upgrade consumes
this reward for a second successful level increase. Replaying either Meteor use
changes nothing. No milestone result/receipt, steed or qualifying equipped item
is supplied by fixture SQL.

After normal EntitySave and game/PostgreSQL stop/start, exact scoped purse,
inventory, Chi/Way and equipment upgrade snapshots match. The earned steed remains
equipped and bound, both successful upgrades and both unique claims persist, and
replaying the claims changes nothing. The bag holds only the upgraded boots.

The initial compile caught an enum/byte slot comparison and was corrected before
execution. The isolated committed-source-overlay build then passed with existing
legacy warnings; the harness compiled with warnings as errors. Earn/reload both
passed on their first gameplay runs. Private game SHA-256:
`2BBC1C00BDFAA809440EA33E879BBD50071972D7F5CC8DF6BBD5FE0EB8756797`.
Logs: `arena-process-career-mount-claims-{earn,reload}.log`.
Both game closes completed and PostgreSQL stopped. Prior executable/manifest
preserved; unrelated owner WIP excluded from the build.

Native/authenticated presentation, natural starting level/CP/boot acquisition,
failed-upgrade process variants, every mount identity, the lowest possible equip
level and interrupted-commit injection remain unverified. Controlled rolls/clock
and supplied inputs are explicit limits. No live deployment occurred and the
retained main-career checkpoint is unchanged.
