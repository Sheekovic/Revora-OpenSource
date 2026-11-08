# Class promotion earning paths

M6.51, reviewed 2026-09-16 UTC against baseline `138d926`. Server implementation
and ordinary-player transaction fixtures are complete. Native 6711 acceptance
and deployment remain open. No client files or live database rows were changed.

## Evidence and edition boundaries

The installed `6711/ini/Achievement.dat` SHA-256 is
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.
The already-reviewed catalog transform was evaluated independently in memory;
decrypt/encrypt reproduced the original bytes. Thirty-five entries encode the
profession directly in their symbolic identity. The other fifteen identify
Pirate, Dragon Warrior and Windwalker ranks by their category and rank names;
these were matched to the existing server trainer transitions. These are strong
catalog/source associations, not a claim that native achievement rendering was
observed. Each entry is worth ten achievement points.

| School | Installed trainer in map 1004 | Destination professions | Achievement IDs in rank order |
| --- | --- | --- | --- |
| Trojan | 10022 | 11-15 | 10819-10823 |
| Warrior | 10001 | 21-25 | 10813-10817 |
| Archer | 400 | 41-45 | 10903-10907 |
| Ninja | 4720 | 51-55 | 10908-10912 |
| Monk/Saint | 4271 | 61-65 | 10913-10917 |
| Pirate | 4272 | 71-75 | 11205-11209 |
| Dragon Warrior | 5061 | 81-85 | 11216-11220 |
| Water Taoist | 10000 | 101, 132-135 | 10829, 10830, 10831, 10832, 10901 |
| Fire Taoist | 10000 | 101, 142-145 | 10824-10828 |
| Windwalker | 51580 | 161-165 | 11304-11308 |

Client records 50200 and 50300 both explicitly require promotion to profession
101 and carry the identical `Achievement_101AccessionTaoist` identity. That
shared promotion records both IDs. The level-40 choice then records only its
selected Water or Fire rank. Later specialization achievements are not granted
for the shared first promotion.

The official [older promotion guide](https://co.99.com/facebook/guides/promotion.shtml)
supports level gates 15/40/70/100/110, five Euxenite Ores for the Archer's
level-40 promotion, and Emerald/Meteor/Moon Box offerings for the last three
ranks. It also distinguishes the level-40 Water/Fire choice. The
[Monk launch rules](https://co.99.com/newclass/monk/) corroborate those material
and level gates. These historical editions differ from the
[current promotion guide](https://co.99.com/guide/guides/promotion.shtml), which
removes some materials and uses rebirth-dependent equipment rewards, and the
[newer promotion expansion](https://co.99.com/guide/event/improved_job_promotion.shtml),
which adds ranks and attributes. They are not interchangeable 6711 specifications.

Revora keeps its installed `GameState.Promote*` costs and rewards: no item for
the first rank; five ores and item 500067 for the Archer's second rank; one
Emerald for rank three; one Meteor for rank four with reward 700031; one Moon
Box for rank five with reward 1088000. Other second ranks have no material or
item reward. Exact historical reward-edition reconciliation is still open;
this change does not claim the existing reward list equals every official
edition. Reward binding now inherits bound offerings, preventing bound material
from becoming an unbound prize. Unbound offerings retain unbound rewards.

The installed server item catalog identifies 32 Moon Box variants. The explicit
allowlist includes the Moon Box quest's 721090 and legacy singleton 721020;
other similarly named consumables are not accepted by a broad name search.
All nine NPC identities and their map/coordinates were read from the existing
NPC database into the disposable fixture. No trainer placement was fabricated.

## Behavior and persistence

`ClassPromotion` replaces only promotion options in the existing trainers;
skill teaching and other menus retain their current routes. Ordinary players
must be alive, fully loaded, canonical owners, near their trainer in Job Center,
and outside a trade or booth. A two-minute offer belongs to the current class,
rebirth and trainer. Confirmation consumes that offer; cancellation, a different
NPC or reopening the conversation invalidates it. Repeated confirmation packets
cannot advance a second rank or repeat a charge. Every promotion checks its level,
including the formerly unchecked Moon Box and early Taoist branches.

The owner lock covers validation, database commit and client projection. The
existing `CharacterProgression` saved preimage protects the whole profile while
preserving legitimate pending EXP/attribute gains. The transaction saves the
new profession, verifies owned bag capacity, debits exact material rows/stacks,
inserts the reward and records the achievement. Missing or suppressed writes
roll everything back and fence the stale session. A receipt conflict is accepted
only if the receipt actually exists. Permanent achievement uniqueness does not
prevent legitimate later-life promotions from repeating their ordinary reward.

Materials must be unlocked, permanent and unsuspicious bag items; persisted
owner/type/position/warehouse/binding/stack/lock state is verified before debit.
Partial stacks retain their remainder; legacy zero-stack singleton consumables
are accepted as one. A fully consumed offering can free the reward's bag slot.
Only after commit do inventory, class, existing rune profile effects and
achievement/wardrobe eligibility refresh. No arbitrary class setter, paid class
switch, rebirth, login or historical backfill grants these promotion receipts.
No new packet layout, table migration or client patch is introduced.

## Validation and remaining work

Build SHA-256:
`8E5422A3AD9929C67C3713B143A017AE2979FE5BB530603C45C94B9E6704FED6`.

`Verify-PotencyHunt.ps1 -PromotionsOnly` passes **266** checks using ordinary
non-GM players and the real NPC dispatcher. Coverage includes all fifty catalog
entries, every below/exact level gate, both Taoist choices, existing rewards,
concurrent confirmation replay, class/profile changes, expiry/cancellation,
wrong trainer/request/map, replaced or unloaded sessions, trade/death/fake
rejection, missing/locked/suspicious materials, split stacks, inherited binding,
full bags, legacy singleton boxes, stale database ownership and suppressed
profile/item/receipt writes. Character, item and normal title-login reload retain
the earned class, items and achievements. Reborn players and later-life repeat
promotions preserve lifetime receipt uniqueness.

Two additional integrated routes within those 266 checks complete all six Moon
Box tactics, earn the terminal quest reward through actual NPC/monster handlers,
and spend that earned Moon Box or Meteor at the trainer. Tactic selection and
loot rolls are deterministic, and movement/combat is scripted. This verifies
quest ownership and reward integration, not native play difficulty or duration.
The other material cases use fixture-supplied offerings; Emerald/mining supply
and the complete level-one-to-master character journey remain separate audits.

The same build also passes EXP/progression **452**, progression evidence **74**,
titles **91**, rune loadouts **185** and Moon Box quest **31** checks: **1,099
gameplay checks** in this batch, plus **3** achievement inventory audit tests.
Logs remain ignored under `obj/chi-refill-validation/` (`promotions.log` and
`promotion-*.log`). Shared disposable database suites ran sequentially.

The producer inventory is now **217/337**, leaving **120** missing identities.
At least **103** more producer identities are required even to reach the 320
source-count threshold. Multiple class routes do not prove that one character
can earn them all; Rising Star remains partial. Wardrobe classification totals
stay 10 code-validated, 26 partial and 36 without an implemented source.

Pending acceptance: view each trainer's short dialog, cancel/reopen, complete a
promotion at the threshold, verify inventory/class/achievement display and
reconnect in genuine 6711. Reconcile original promotion reward editions and audit
remaining material acquisition before claiming a complete original-game career.
Build artifacts have not replaced the running server and no public patch was
published.


## HillMonster Emerald acquisition (2026-09-26)

The installed HillMonster family 15 on Desert City map 1000 already has Emerald
1080001 in its database-driven own-item drop. Source-only searches missed that
route; the unreachable NPC 338111 and removed starter packs are not required.
The source is also described in player-authored guides hosted by the official
site: [Warrior leveling](https://co.99.com/content/2008-02-06/20080206005226220.shtml)
and [Archer leveling](https://co.99.com/content/2009-02-19/20090219191925412.shtml).
These corroborate the source, not original odds.

HillMonster Emeralds now use the existing owned hunting-drop pipeline: one roll
per death, protected floor ownership, atomic item/receipt persistence, and replay
and stale-origin rejection. The generic legacy own-item branch is suppressed only
for this managed family/map/item, preventing a second drop roll. The existing
`AdjustedItemDropRate(OwnItemRate)` is retained: **90% in the installed fixture**.
It is independent of daily quest material chance. No new 10% policy was retained.

Build passes with existing legacy warnings; **501 combined hunting-loot checks
pass** (`obj/emerald-build.log`, `obj/emerald-hunt-tests.log`). The Emerald route
uses combat damage, owned floor pickup, full reload, normal Trojan trainer
confirmation, consumption and another class/item reload. Ordinary account access,
installed spawn, exact 89/90 roll boundary, corpse replay and saved pickup origin
are checked. Shared pickup failure/ownership regressions also pass; counts overlap
other suites. Monster HP/damage and RNG are controlled. The promotion test starts
with a supplied rank-two character; it does not prove the complete career.

Remaining: connect Emerald and Moon Box acquisition to both lives of the Honored
Hero career, remove supplied initial level/class prerequisites, and complete
Host/native acceptance. Nothing was deployed or patched into the live client.


## Actual ordinary promotion and reload (2026-09-27)

The ordinary earned-career character entered the school through its real portal
and earned Trojan ranks11 and12 through normal NPC dialogs. Immediate updates,
zero required payment and consumed-offer replay refusal passed. Class12 and its
two achievement receipts survived actual restarts; the next rank correctly
refused a missing Emerald. [Evidence](ROADMAP_STARTUP_ACCEPTANCE.md#ordinary-trojan-promotion-prerequisites-2026-09-27).

A follow-up promotion dialog timed out after each successful rank and worked
after reconnect. This remains unresolved; neither consecutive in-session
promotion nor the final three ranks are certified by these runs. Native and
production gates remain open. No synthetic profile or achievement writes.


### Follow-up timeout resolved (2026-09-27)

The preceding timeout classification is superseded: the unpaced socket probe
sent a fourth2032 selection inside the server's500ms, three-packet allowance.
The issue reproduced without changing rank. Waiting250ms between selections
made the same replay/reopen sequence pass in two full cycles on the original
candidate; server limits and promotion logic were preserved.
[Evidence](ROADMAP_STARTUP_ACCEPTANCE.md#promotion-follow-up-timeout-resolved-in-the-probe-2026-09-27).
Ranks13-15, remaining materials and native acceptance are still outstanding.


### Ordinary material ranks13/14 (2026-09-27)

Earned CPs purchased the active mall's Emerald(1 CP) and Meteor(3 CPs). Both
trainer promotions completed consecutively with correct material consumption,
no extra currency charge and one unbound normal Rainbow Gem reward. Class14,
all four achievement receipts and the gem persisted after real restart; final
promotion refused a missing Moon Box. [Detailed evidence](ROADMAP_STARTUP_ACCEPTANCE.md#earned-fund-trojan-material-promotions-2026-09-27).
This supersedes the earlier pending rank13/14 state. Rank15, Moon Box, rebirth,
native acceptance and deployment remain outstanding.


### Ordinary final rank from the earned Moon Box (2026-09-27)

The same character finished all six Moon Box tactics through ordinary combat,
exchanged its materials and claimed the quest box. The trainer consumed that
exact box UID, promoted class 14 to 15 and granted an unbound DragonBall without
charging currency. Replay and highest-rank refusal passed. The final rank,
DragonBall, five promotion receipts and Moon Box achievement persisted across
actual game/database restart. [Evidence](ROADMAP_STARTUP_ACCEPTANCE.md#earned-moon-box-completion-and-final-trojan-promotion-2026-09-27).
This closes the first-life Trojan trainer journey for this ordinary character;
rebirth, other class careers and native acceptance are not thereby certified.


## Retained-career planning constraint (2026-10-01)

The current retained receipt snapshot has38 of50 promotion identities present.
Its12 absent promotion identities are10813,10819,10824,10825,10829,10830,10903,
10908,10913,11205,11216,11304. The reviewed paid reincarnation route cannot itself
perform these trainer transitions: `RebirthRules.Classes` selects11,21,41,51,61,
71,81,132,142,161, and `ReincarnationRules.Profile` requires one of those targets.
`ClassPromotion.Record` records actual promoted ranks; a class change is not a
substitute for the omitted early trainer transition.

This is not a proof that no other ordinary route exists. It does mean a planner
cannot count all50 promotion identities merely because all producers exist.
The union of these12 route gaps and four currently unavailable identities leaves
321 candidate identities from337, only one above the320 target. Other prerequisites
may reduce compatibility further. Existing receipts have mixed provenance.

`python tools/audit_retained_compatibility.py` regenerates the hash-tracked
[planning report](research/RETAINED_COMPATIBILITY_PLAN.json) from current checked-in
receipt/gap evidence and records the reviewed rule-source hashes. It neither grants
receipts nor changes the game. Source availability, compatible earning and native
acceptance remain distinct requirements.


### Ordinary entry-point audit (2026-10-01)

A bounded source review traced the active first/second-rebirth NPC offers and
sealed1066 reincarnation packet to their target rules. `FirstRebirth` and
`SecondRebirth` choose from `RebirthRules.Classes`. The packet dispatcher calls
`PaidReincarnation.Process`, whose profile calculation uses
`ReincarnationRules.Profile` and the same target list. Those targets begin at
non-Taoist rank1 or Taoist rank2; they do not return the existing character to the
unpromoted ranks required for the missing trainer transitions.

The review also inspected direct class assignments in EntityTable, GameState,
RevoraMasterSystem, the legacy Reincarnation helpers and NPC code. Character
creation supplies unpromoted ranks for a **new identity**, not this saved career.
The only repository caller found for the old GameState.Reborn method was the chat
command path. The legacy direct-assignment constructors and RevoraMaster's
PromoteClass helper supplied no verified ordinary-player entry point in this
bounded review. Their presence is not evidence that a player can legitimately
use them for the missing promotions.

No classes, receipts, fees or target lists were changed. This narrows the reviewed
route evidence; it is not a global call-graph proof, a native input audit or a
reason to grant historical credit. A fresh character's chosen first school may
have a different attainable history, but cannot be substituted for retained
character4294907001 while claiming its existing achievements coexist.

The compatibility report now records these entry-point findings and hashes the
reviewed active rule/packet sources. Further work can pursue other compatible
ordinary earning paths without silently counting these12 as completed.
