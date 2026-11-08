# Sword Master owned encounter

## Retained earned-Honor journey (2026-10-01)

The retained ordinary career completes the Arena-to-Sword-Master route across
four additional processes: `career-master-buy`, `career-master-win`,
`career-master-loot` and `career-master-reload`. All four passed on their first
execution. These follow the documented [Arena earning checkpoint](SWORD_MASTER_RESTORATION.md#retained-arena-earning-prerequisite-2026-10-01).

An ordinary mutual trade transfers one existing unbound accessory to the retained
peer, making room without deleting possessions. The real Honor shop then spends
exactly 8,000 of the earned 1,000,000 Honor, leaving 992,000 in both mirrors and
one saved Sword Soul. A repeated Buy request adds no charge or item. The purchase
receipt and exact item survive abrupt process exit and cold loading.

The owner enters its already-earned level-five house through the ordinary NPC,
uses that exact Soul and submits sealed 2032 confirmation. Duplicate confirmation
creates no second charge or boss. Ninety-four normal ranged attacks defeat the
15,000,000-HP actor; no item, damage amount, kill result or loot draw is supplied.
Victory grants 20 Study Points and saves three uncollected spoils before another
abrupt exit. The next process restores the house and all three drops. Ordinary
sealed floor pickups, each replayed, deliver exactly three items with one bound
P7 soul. A further cold load compares the complete snapshot; stale confirmation
and reward polling produce no additional actor, drop, item or Study Points.

The saved career's bag finishes at 40 items. Encounter clocks, proximity and
attack cooldown are controlled. This proves the retained solo earning/reward
route, not balanced difficulty, all skill animations, native input/rendering or a
fresh party journey. The earlier focused party/skill tests remain separate
coverage. An active Sword Master fight is intentionally not restart-resumable;
this run interrupts only after purchase or committed victory/pickup.

No production source change or rebuild was needed for this batch. The existing
private candidate SHA-256 remains
`1109CC262F8FBAA65A50451A60BE104FC2FB4A1FD885A72B11E444FE0C19E91E`.
No live deployment, live restart or client release occurred. Native acceptance
and live Host restart/rollback gates remain open.

Candidate source integration, 2026-09-21. This service is connected to ordinary
item use, packet confirmation, combat, startup and the world timer. It has not
been deployed to the live server or accepted in the genuine 6711 client.

## Ordinary earning route

A level-100-or-higher player buys Sword Soul 720842 for the existing 8,000 Honor
price, builds/owns a registered house, gathers eligible teammates inside it,
and uses the item. A two-minute server-owned offer captures the exact item,
stack, binding and house. Sealed 2032 confirmation consumes one Soul with the
persisted once-per-Pacific-day attempt. Replays, changed/expired offers, foreign
houses and a busy encounter slot cannot charge another item. There is no GM or
account allowlist. One house can host one fight; the global actor limit is 128.

The challenge lasts thirty minutes. Owner departure/disconnect, expiry or loss
of every eligible living team member forfeits the attempt. These duration and
owner-session rules are explicit Revora policy. Startup forfeits interrupted
attempts, as described by the official guide. Repeated initialization within the
same process does not close live actors. A bounded orphan sweep closes committed
admissions that never published an actor. House upgrades, including final
installment construction, wait for active challenges and unexpired ground spoils.

The registered actor owns family 9916804 and the reviewed SwordMaster name/mesh
953. Damage, direct death callbacks, companions and named attacks require its
canonical map, house metadata, owner session and frozen party. Former teammates,
foreign players, stale actors and unregistered companions receive no credit.
A companion's owner must remain close enough to receive the reward. Owner and
killer locks, followed by the encounter gate, serialize victory and cleanup;
contended cross-player locks fail without blocking another player indefinitely.

## Combat policy

The original skill behaviors are described in the
[official guide](https://co.99.com/guide/monsters/sword_master.shtml). The selected
level-zero spell identities are present in the loaded catalog. Exact original
variant mapping and numerical parity are not established; see
[protocol evidence](PROTOCOL_RESEARCH.md#sword-master-candidate-combat-2026-09-21).

Revora uses 15,000,000 HP, level 140, raw melee attack 1,600-2,328, magic attack
3,000, defense 100 and magic defense 30. HP and the boss flag are applied to the
normal spawn/update representation. This verifies server values, not the
client's long-bar rendering.

Each skill has a two-second warning and a four-second recovery. The deterministic
cycle lets players learn the fight. Distances below are Euclidean tile radii;
normal class defenses and Warrior counter behavior remain in the damage path.

| Skill | Catalog ID | Revora behavior |
| --- | --- | --- |
| Slash | 10369 | Single target within 3 tiles of the cast origin |
| Sword Beam | 10504 | Single target within 12 tiles; double calculated melee damage |
| Immolation | 10500 | Magic damage within 2 tiles of the warned target location |
| Revenge Punch | 10505 | Melee damage within 3 tiles of the warned target location |
| Blade Vortex | 10506 | Melee damage within 6 tiles of the boss's cast origin |
| Blade Storm | 10503 | Magic damage in the ring from 4 through 10 tiles |
| Heal | 10374 | Restore up to 2% maximum HP, capped at full health |

## Victory, failure and persistence

The exact saved attempt moves to victory in the same transaction that records
20 Study Points for each eligible nearby frozen teammate, both original-category
loot rolls, the extra bound P7 event drop and the killer's owned Soul Ball bonus
of 1,000 spirits (capped at the bead requirement). Ordinary daily Soul Ball
completion still gives no P7. Registered companions credit their owner. Generic
monster loot is suppressed for this family.

Only after commit is the actor retired and its corpse processed. Study payout,
bound loot delivery and reconnect behavior use the existing
[durable reward participant](SWORD_MASTER_REWARDS.md). Failed writes roll back
victory and all entitlements; the session is fenced. A real disconnect still
forfeits an active challenge. The retry test clears only the fixture fence to
exercise the same live transaction again; it does not claim a reconnect-resume
policy that the service does not offer.

## Acceptance limits

The focused fixture buys the item through the actual Honor shop, builds the
house through its ordinary NPC, issues the bead through the envoy, confirms the
summon through packet dispatch and kills through the normal damage receiver.
A separate teammate route uses mutual friendship and an accepted native house
invitation. Item/character reload uses the production loaders. That earlier fixture starts
with Honor funds; the retained journey above now separately proves normal Arena
combat earning those funds.
Combat damage inputs and virtual clocks are controlled test inputs, not a recorded
native player combat session.

Still required before live availability claims: genuine 6711 item confirmation,
all seven animations, long HP bar, house/party movement and floor pickup;
native review of the [privately composed level-100 tooltip](SWORD_MASTER_CLIENT_STAGE.md);
and a live Host restart/rollback acceptance run. No client
files or production database rows were changed by these isolated tests.

## Validation results

Isolated x86 build succeeds. `Verify-PotencyHunt.ps1 -SwordMasterOnly` passes
79 checks; `-QuestPacketsOnly` passes 11; `-HousingOnly` passes 557 including
existing admission and reward participants. Counts include shared fixture setup
and are not independent feature totals. All three runs completed successfully.
Logs are ignored under `obj/chi-refill-validation/sword-encounter-tests.log`,
`sword-packet-regression.log` and `sword-housing-regression.log`.

The encounter run covers duplicate confirmations and kills, malformed dialogue
text, foreign/direct death attempts, all seven warning/execution phases, geometry,
invited-team combat, departure/disconnect/expiry, changed binding and stale offers,
registered versus unregistered/distant companion ownership, pending-loot upgrade
guards, orphan/startup recovery and suppressed reward/spirit/recovery writes.
Distance proofs use widened squared arithmetic so invalid large coordinates cannot
wrap the legacy distance calculation into a false nearby result.


## Fresh earned-Honor summon and durable victory (2026-10-03)

The fresh non-GM career now completes the same route using its independently
recorded Arena result and paid daily ranking, plus its earned five-level house.
The [Honor prerequisite](SWORD_MASTER_RESTORATION.md#fresh-earned-counterpart-and-arena-ranking-2026-10-03)
includes an ordinarily trained level70 counterpart and explicitly documented
fixture recovery; no Honor or summon material is supplied.

The real Honor shop spends8,000 of the earned1,000,000 Honor and saves exactly one
Sword Soul720842, leaving992,000 in both solo/team mirrors. Duplicate Buy requests
add nothing. Cold loading preserves that exact purchased item and receipt. The
owner enters its paid, furnished house through NPC30156, uses that Soul and sends
sealed2032 confirmation twice. Only one15,000,000-HP Sword Master appears, and the
Soul is consumed once.

759 calculated melee attacks defeat the owned boss with the player alive. The
production victory pays20 Study Points, records10528 and saves three uncollected
spoils. After restart, normal sealed floor-pickup packets collect all three; each
packet is replayed without another item. Exactly one drawn reward is a bound P7.
A further cold process preserves the full house/furniture, prior career, Honor,
reward UIDs and paid study/run/loot records. Repeated summon confirmation and
reward polling create neither an actor nor already-collected loot. The final
reload also explicitly checks10528, all three collected UIDs in the loaded bag,
and the single paid20-point study entitlement.

All four phases pass: `career-fresh-master-buy`, `career-fresh-master-win`,
`career-fresh-master-loot`, `career-fresh-master-reload`. The final reload is
repeated only to validate the added loaded-ownership/receipt assertions.
Evidence: `tools/CheckCandidateFreshMaster.cs`, `revora_fresh_master_probe` stage3,
and the corresponding private phase logs. Candidate SHA-256 remains
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.

Clock, NPC/combat proximity and cooldowns are controlled. This closes the fresh
server-side earned-Honor/summon/reward path, not native presentation, natural
fight timing, retaliation or class balance. Production source, client artifacts
and live deployment are unchanged. The post-Snow receipt audit is historical
and predates the documented housing, Honor and Sword Master earning.
