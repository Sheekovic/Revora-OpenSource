# Snow Banshee victory rewards and recovery

Current2026-10-01: the retained ordinary character's normal combat, saved partial
HP recovery, earned victory, bound-P7 pickup and exact cold reward replay pass
across four game/database processes. [Scope and controls](SNOW_BANSHEE_ENCOUNTER.md#retained-fight-checkpoint-and-reward-process-recovery-2026-10-01).
Native and deployment gates remain open.

## Historical participant-only checkpoint

Integration follow-up: [the owned public encounter](SNOW_BANSHEE_ENCOUNTER.md)
now supplies registered combat victories and startup/tick recovery. The inactive
integration status below describes the earlier reward-participant batch;
native/live acceptance remains open.

The reward participant is implemented and fixture-validated. **The public boss
is not activated yet**: the fixture supplies the victory through SnowBansheeStore,
and does not prove combat ownership, party eligibility or spirit credit. No
production initialization or reward polling hook is installed in this batch.

## Reward contract

The [official Snow Banshee guide](https://co.99.com/guide/monsters/snow_banshee.shtml)
specifies 100 Study Points per teammate and two independent random drops:

| Chance per roll | Candidate item |
| --- | --- |
| 10% | Dragon Ball, 1088000 |
| 50% | Random loaded P4-P7 soul |
| 10% | Random accessory from 350001, 360001, 370001, 380001 |
| 30% | +2 Stone, 730002, with persisted Plus=2 |

The exact catalog pool and seven-day accessory lifetime are explicit Revora
choices; the guide does not identify those item IDs or their relative odds within
each category. All loaded P4-P7 souls are eligible, with uniform selection within
that pool. Accessories start their seven-day lifetime when collected.

Revora additionally supplies **one guaranteed bound P7 soul** per event victory,
consistent with the owner's valuable-event reward policy. Every P7 produced by
either weighted roll is also bound. Other drops are unbound. This is an event
reward, not a new P7 grant for daily quest completion. One-time quest rewards and
the two Magnolia custom packs retain their separate bound-P7 policy.

## Persistence and ordinary pickup

Record requires the canonical killer session, its inventory lock, the public
Grotto world and an existing victory within the supplied transaction. Its frozen
recipient list must match exactly; omission or addition of a teammate fails.
Study and all three loot entitlements commit in the same transaction as victory
and respawn cooldown. The future combat coordinator must first prove the actor,
killer and eligible nearby party; calling this participant does not establish
those facts.

Study receipts are paid once, with the saved balance checked against memory.
Overflow preserves the pending 100-point entitlement instead of truncating it.
Normal subclass packets and achievement evidence update after the committed
credit. Recovery polls online recipients in bounded batches.

Uncollected ground drops reconstruct in canonical public map 1927 near the saved
victory, within the reviewed site leash. They retain the existing owner/team
protection interval and expire two minutes after victory. Private map aliases or
changed world ownership do not receive recovered loot. Persisted type/binding,
object identity, location, ownership and item attributes are checked on pickup.
The +2 Stone explicitly receives and saves Plus=2; creating its item ID alone
would leave the default Plus=0.

The existing sealed floor-pickup handler and alternate item-pickup entry route
now recognize only registered Snow Banshee drops. Inventory insertion and its
collector receipt are atomic; full bags, stale or repeated pickups and failed
writes cannot consume the entitlement or create duplicate items. Previously
collected and expired drops do not return during recovery.

## Validation

The isolated build and **28 Snow Banshee reward checks** pass, plus **79 Sword
Master encounter checks** covering the shared pickup path. The reward fixture
enumerates all 100 weighted branches and exercises non-GM sessions, frozen-team
matching, duplicate claims, protected loot, malformed seals, full bags, concurrent
native pickups, production character/item reload, Plus=2, timed accessories,
binding on both P7 sources, tampered binding, suppressed inserts, Study overflow,
loot recovery and expiry. Failed Study/loot entitlement inserts roll back the
entire victory and preserve the active boss for retry.

Ignored logs: `obj/chi-refill-validation/snow-banshee-reward-tests.log` and
`snow-banshee-sword-regression.log`. Tests write only a disposable schema. No
installed client, production player state or live server was changed.

Remaining: owned actor/damage/death integration, seven combat behaviors, the
registered 1,000-spirit bonus, coherent startup/tick integration, replacement of
the legacy Grotto summon, and genuine client/live Host acceptance. The roadmap's
full Snow Banshee earning route remains unchecked.
