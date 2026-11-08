# Banshee restoration evidence and item-loss correction

Current follow-up: the [Purple Banshee encounter candidate](PURPLE_BANSHEE_ENCOUNTER.md)
now connects these participants to owned item confirmation, combat and victory.
Earlier dormant/source-integration statements below describe their historical
batches; native/live acceptance and catalog repair remain open.

The retained Arena-funded [Purple house hunt](PURPLE_BANSHEE_ENCOUNTER.md#retained-arena-funded-house-hunt-2026-10-01)
and [public Snow fight](SNOW_BANSHEE_ENCOUNTER.md#retained-fight-checkpoint-and-reward-process-recovery-2026-10-01)
now have ordinary earning and cold reward evidence. Golden War remains a separate
route. Native/live acceptance and catalog installation remain pending.

## Historical defect and prerequisite corrections

The following sections record the earlier closure and reconstruction. Their
dormant-service descriptions do not describe current candidate source.

## Confirmed defect and current protection

Item 3004259 (`BansheeSpirit`) had an empty switch label followed by commented-out
summoning code and no active `break`. It therefore entered Promotion Pack 728204's
handler: with space available, right-clicking consumed the Banshee Spirit and
created an Emerald, a Moon Box and a Meteor. This was neither a valid summon nor
a deliberately priced exchange.

The separately reachable 2032 continuation checked only item possession and the
RAM `BansheeSpirit` daily flag. It spawned family 7565 at the player's current map
and coordinates, then consumed an item, without house ownership, a server offer,
exact-item matching or an atomic persisted attempt.

Both paths now preserve the item and give a brief dormant-rite message. Stale
confirmations clear `ActiveNpc` and do not spawn, consume, set the daily flag or
create Promotion Pack gifts. Its dialogue family also uses the existing strict
frame check. There is no GM/owner exception and no claim that the house boss is
restored. The existing public Ice Queen NPC and Golden War service are unchanged.

The isolated build and all 15 quest-packet checks pass. Added checks use ordinary
item dispatch and sealed confirmation packets, repeat both operations, preserve
both bound and unbound two-item stacks, compare the whole inventory and world
actor count, and reload saved items through the production loader. No live
server, installed client or production player rows were changed.

## Distinct identities: do not alias their credit blindly

Read-only catalog inspection on 2026-09-21 returned:

| Family | Local name | Mesh | Level | HP | Raw attack | Defense |
| --- | --- | --- | --- | --- | --- | --- |
| 4171 | IceQueen | 951 | 138 | 100,000,000 | 10,000 | 40,000 |
| 4172 | SnowBansheeSoul | 951 | 138 | 60,000,000 | 10,000 | 40,000 |
| 7565 | PurpleBanshee | 951 | 138 | 54,000,000 | 10,000 | 40,000 |

These are local stored values, not verified original retail combat formulas.
Inspector: Revora-owned x64 helper, SHA-256
`13E88C0DD57AD147B25EC7411C6B6638766E8F3A0B96F3D68EC2524AB26FE227`.
Source/binary remain ignored under `obj/chi-refill-validation`; its transaction
was read-only and selected only these monster catalog rows.

Loaded HonorShop.ini stocks Banshee Spirit for 35,000 Honor. Its local item tooltip
specifies a once-daily Purple Banshee summon in the owner's house. The unsafe
callback selected 7565; this does not establish that Purple Banshee should receive
SnowBansheeSoul's original spirit bonus or original wilderness reward pool.

The [official Snow Banshee guide](https://co.99.com/guide/monsters/snow_banshee.shtml)
places a recurring public boss at Frozen Grotto 2 (341,362), refreshing every half
hour. It awards 100 Study Points per teammate and two randomized drops: 10% Dragon
Ball, 50% P4-or-higher artifact, 10% accessory, 30% +2 Stone. Seven named behaviors
include single-target and fan attacks, a rotating hail attack, short freezes and
healing. Exact statistics, accessory identities and the meaning of half-hour
refresh versus time-after-death still need explicit local policy/evidence.

Official 2014 [Call of the Gold](https://co.99.com/guide/quest/2014/call_of_the_gold.shtml)
mentions a house Banshee Spirit summon; the 2015
[Fiery Dragon event](https://co.99.com/guide/event/2015/fierydragon/) explicitly names
Purple Banshee kills in houses. Those seasonal reward references corroborate a
separate house route, not its complete permanent reward contract. They do not
authorize automatically reopening those old seasonal campaigns.

Legacy NPC 899990 offers family 4171 in map 1927 around (502,425), gated by a
mutable global flag and an hourly minute window. Another path uses the same
family in Golden War map 2072. The old NPC 2999 path is commented out. These are
not proof of the official recurring Grotto route or a registered spirit-credit
source. Previous read-only spawn research found no normal spawn rows for
4171/4172; revalidate that state before installing a replacement schedule.

## Next earning-path work

The public site and inactive persistent lifecycle prerequisite now pass the
isolated build, 33 storage checks and 106 travel checks. The guide's exact tile is
blocked locally; the reachable candidate clearing is (370,365). Durable actor
generations, health checkpoints and a thirty-minute post-defeat timer are ready
for coordinator integration ([evidence and remaining scope](SNOW_BANSHEE_PERSISTENCE.md)).
This does not enable the public encounter or its rewards.

The separate [reward participant](SNOW_BANSHEE_REWARDS.md) now passes 28 focused
checks and 79 Sword Master regression checks: 100 team Study Points, two weighted
drops and the extra bound P7, with atomic pickup and recovery. Owned combat and
spirit integration remain required before activating this earning route.

Prioritize the more fully documented public Frozen Grotto encounter: validate its
actual terrain and normal travel, establish a persistent single-boss respawn
contract, register actor generations and damage/death ownership, implement the
seven evidenced behaviors using reviewed local spell identities, and commit
killer/team rewards and the owned 1,000-spirit bonus with the victory. All P7
souls must remain bound; no daily completion P7 is added. Replace only the
corresponding legacy Grotto admission once its owned replacement is validated;
keep Golden War's separate contract explicit.

The house Purple Banshee requires its own reviewed acquisition/admission/reward
contract and cross-boss house occupancy protection before item use can reopen.
Reuse proven Sword Master transaction/lifecycle primitives where appropriate,
but do not equate distinct catalog families to make an easier test pass. Both
routes require ordinary persistence/concurrency checks and genuine client/Host
acceptance before a live availability claim.
