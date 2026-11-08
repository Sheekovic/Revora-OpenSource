# Owner-defined valuable quest rewards

Owner clarification (2026-09-21): **remove direct daily quest P7 rewards only**.
One-time quests, event rewards and both Magnolia custom packs keep their bound
P7 souls. Daily Hunt, Rare Materials, Soul Ball, Release Souls, Magnolia's base
harvest, Exorcism and Worship retain their other completion rewards. Custom
Magnolia packs give one bound P7 soul, 5,000 CPs and 1,000 RVPs. Existing items
and completed receipts are preserved. See [current policy](DAILY_REWARD_POLICY.md).


**Owner update (2026-09-21): P7 souls awarded by quests are bound.** This
supersedes earlier unbound-soul descriptions, including the two Magnolia custom
packs. New grants/openings use bound items; already-issued items are unchanged.
Weapons, garments and runes retain their separately specified binding rules.


Owner direction, 2026-09-20: every quest must give valuable rewards such as
class-appropriate maximum-level weapons with +12 enhancement, -7 damage reduction,
two sockets and no binding; P7 Dragon Souls; five-star garments or mounts; or
runes. This instruction supersedes earlier assumptions that original retail
EXP, Study Points or low-value materials alone complete Revora's reward design.

## Required implementation

- Audit each actual quest completion path and its frequency, including daily and
  weekly quests. Intermediate objectives are not separate completed quests.
- Give each completed quest a meaningful reward from the owner's categories.
  A chance at a valuable bonus does not by itself prove the base quest meets this
  standard. Scale the category and quantity to difficulty and repeat frequency.
- Select weapons for the character's class from verified loaded catalog families.
  Verify the highest supported level, +12, -7 and two actual sockets; weapon
  rewards must be unbound. Do not confuse a quest token named Rune with an
  equippable combat rune, or a mount/garment name with verified five-star rarity.
- Preserve owned completion, inventory capacity, replay/concurrency protection,
  exact currency updates, receipts and reconnect behavior. Commit reward items
  with the completion; do not grant them through a second untracked callback.
- Freeze reward versions for newly admitted runs. Preserve existing earned
  entitlements and claim receipts; do not retroactively duplicate paid rewards.
- Match NPC text, item tips and client rarity display to the actual committed
  rewards. Keep native acceptance and live release gates explicit.

## Current delivery and remaining scope

Use the [coverage inventory](QUEST_REWARD_COVERAGE.md) for current reward terms
and the [daily policy](DAILY_REWARD_POLICY.md) for daily exclusions. In particular,
new Sky Pass, Snake Islands, Daily Hunt, Rare Materials, Soul Ball, Release Souls,
Magnolia base harvest, Exorcism and Ancestor Worship contracts now give their
documented combat rune instead of a direct P7 soul. Obsolete P7 flags cannot
restore a removed daily reward. Magnolia's two custom bonus packs retain bound P7.

Skill Team PK is implemented in staged source and is included in the current
coverage inventory: its top-eight leader purses contain bound P7 and ranked CPs.
Authenticated champion earning and restart recovery are recorded in the
[feature evidence](SKILL_TEAM_PK_RESTORATION.md#tcp-earned-rewards-after-restart-2026-09-27).
Native acceptance and deployment remain open.

## Historical implementation checkpoints

The following paragraphs preserve earlier implementation and test scopes, not
current reward contracts. Their unbound-soul and daily-P7 descriptions were
superseded by the owner clarifications and subsequent rune additions above.
Their family counts are historical; they are not current coverage totals.

Path Hunt now guarantees a bound P7 soul for newly accepted hunts alongside its
original purse/potions/level alternative. Older acceptance retains its promise.
Build and 552 checks pass ([evidence](PATH_HUNT_TRANSACTIONS.md)). Daily Envoy is
not double-counted. Worship the Ancestors now adds a bound P7 in its owned daily
transaction; 520 checks pass ([evidence](ANCESTOR_WORSHIP_TRANSACTIONS.md)).

Courier Loop now commits its bound P7 soul with the original purse, level and
VIP entitlement. The 531-check suite covers earned routes, complete rollback,
VIP conflicts and fresh reload; native acceptance remains pending
([evidence](COURIER_LOOP_TRANSACTIONS.md)).

Soul Trial now awards its bound class-oriented P7 soul through an owned completion
transaction with the original potions, silver/RVP and level alternative. The
510-check suite covers earned stages, rollback and fresh reload; native use and
presentation remain pending ([evidence](SOUL_TRIAL_TRANSACTIONS.md)).

New Elite PK top-eight Envoy claims add one bound P7 soul beside the existing
bundle/CP/EXP. Old tournaments and independent outer packs retain their contents.
Build and the 637-check suite pass (including prerequisites); actual old/new
brackets, fresh reload, capacity and atomic faults are covered. Test scope and
remaining native/release gates are recorded in [Elite evidence](ELITE_PRIZE_CLAIMS.md).

New BP PK tournaments grant a bound P7 soul beside the original CP/Chi/token
purse. A separate BP reward flag preserves older tournaments. All 104 BP checks
and 185 shared PK regressions pass, including earned rewards, fresh character
reload and token expiry/exchanges. Native honor/display and release remain open
([evidence](BP_PK_IMPLEMENTATION.md)).

New Couple PK champion pairs award one bound P7 soul to each spouse beside
their original individual purses and two EXP Balls. Build and 46 checks pass,
including ordinary pair combat, atomic claims, both spouses' fresh character
reload and old contracts. Native/live acceptance remains open
([evidence](COUPLE_PK_WAR.md)).

New Weekly, Monthly and Class PK tournaments add one bound P7 soul to the
champion purse and ranked EXP Balls. Older tournament payouts stay frozen.
Build, 65 Local PK and 120 Class PK checks pass for ordinary combat earning,
atomic claims, rollback/replay and fresh character/item reload. BP PK is excluded
from this shared claim path; native/live acceptance remains open ([evidence](LOCAL_PK_WAR.md)).

New Power Arena sessions guarantee permanent unbound five-star Somersault Cloud
mount armor 200449 at seventh-floor completion, beside either selected floor
gift. All 106 checks pass, including ordinary earning, equipment and wardrobe/
title ownership through fresh character reload. Old contracts retain their gifts;
native/live acceptance remains open ([evidence](POWER_ARENA.md)).

New Revora NPC Quiz Shows award one bound P7 soul to every entrant who answers
five questions correctly, alongside the existing ranked EXP Balls. Old shows
retain their frozen payout. All 36 Quiz checks pass, covering ordinary earning,
transaction failures, replay and exact item reload; native/live acceptance is
pending. See [Quiz evidence](QUIZ_SHOW.md).

New Summer daily hunts guarantee one unbound level-one Tortoise Breaker combat
rune (4030101) beside fragments. The 142-check earning/crafting fixture validates
atomic receipts, ordinary rune-bag admission and eligible equipment/reconnect.
Native acceptance and live deployment remain open. This is the first combat-rune assignment in the quest
reward upgrade inventory, not completion of all rune or seasonal work.


Implemented in source: owner-defined Magnolia Meteor Pack 721269 and Dragon Soul
Pack 720957 each open for one uniformly selected catalog P7 soul, 1,000 RVPs and
5,000 CPs. The soul is always bound regardless of pack binding. New version-four Magnolia contracts
add these packs to Elite/Super bonus pools; the overall bonus chance stays 20%.
This completes the two custom packs, **not** the all-quest reward upgrade.

New Elimination and Flame Lit contracts now guarantee one unbound P7 soul at
full completion, alongside their existing rewards. Elimination requires the
complete rescue/cure/den; Flame Lit requires all ten stones and the Dragon Rune.
Early partial Flame claims are not full quest completions and retain EXP only.
Old admitted contracts retain their frozen rewards. Build, 195 Elimination and
93 Flame checks pass, including rollback/replay/reload.


New Daily Hunt and Rare Materials contracts also guarantee one unbound catalog
P7 soul alongside their existing Chi Token, EXP, CPs, RVPs and Study Points.
The same frozen reward applies to their existing paid-completion option, once
per Pacific quest day; fees and purchased Rare Materials stars are unchanged.
Existing saved/imported contracts keep their original rewards. Completion,
source consumption, currencies and both item UIDs commit together. Capacity
accounts for actually removed stacks, and NPC reward text includes the soul.
Build, 547 Daily Hunt checks and 559 Rare Materials checks pass, including
ordinary earned routes, paid/concurrent claims, rollback, full bags, legacy
contracts, migration reinitialization and production-loader reconnects.
These upgrades are source-tested; live/native acceptance remains open.

New Sky Pass and Snake Islands runs now also add one guaranteed unbound P7
soul to their existing four/two bound Meteors. Old runs retain their saved
contract. The item UID joins the completion receipt in the same transaction.
Build, 40 Sky and 28 Snake route checks pass, including old contracts, concurrent
claims, full bags, rollback and reload. Native acceptance remains pending.


New Release Souls and Moon Box runs also guarantee one unbound P7 soul. Release
Souls retains its daily limit and paid option; Moon Box retains its repeatable
six-token route and adds the soul to either successful ending, not refusal.
The selected soul UID is recorded in the existing completion reward array.
Old contracts keep their original gifts. Build, 504 Release Souls checks and
34 Moon Box checks pass, including rollback, full bags, concurrent claims,
ordinary reload and old contracts. Live/native acceptance remains pending.

New Soul Ball contracts now also guarantee one unbound P7 soul for every grade
and the existing paid finish. The reward UID commits with all other completion
writes, while old imported/saved contracts keep their original gifts. Build and
610 Soul Ball checks pass, including Terato earning, rollback, bag capacity and
reload. Native/live acceptance remains pending.

The [coverage inventory](QUEST_REWARD_COVERAGE.md) now records nine upgraded
quest families, confirmed remaining reward gaps and the next implementation
seams. It explicitly lists unenumerated legacy/event paths.

Anniversary new contracts now add a guaranteed unbound P7 soul to the existing
cards and currencies. The reward array and completion commit together; the daily
exclusion with Soul Ball remains unchanged. Old beads retain their prior gifts.
See [transaction evidence](ANNIVERSARY_TRANSACTIONS.md).

New Magnolia harvests now guarantee an unbound P7 soul independently of the
unchanged 20% bonus roll. Existing contracts retain their original terms. Build
and 853 checks pass, including no-bonus receipt identity and the custom packs'
additional soul. See [bonus and harvest evidence](MAGNOLIA_BONUSES.md).

Ancient Devil new summons now add a guaranteed unbound P7 soul to the saved
final victory recipe. Build, 327 battle-store and 278 boss-world checks pass,
including owned combat, recovery, old contracts and reward rollback/reload.
See [claim evidence](ANCIENT_DEVIL_BATTLE_STORE.md); live acceptance stays open.

New Dis City expeditions add one guaranteed unbound P7 soul at final victory
turn-in. Build and 179 integration checks pass, including final claim rollback,
receipt identity and weekly replay. The old-contract fixture also earns all four stages after applying the migration
default and verifies original gifts through reconnect, item reload and replay.
See [dungeon evidence](DIS_CITY_DUNGEON.md).

Labyrinth's newly confirmed Cloud Diamond exchange now gives an unbound P7 soul
beside the Ancestor Box. Old receipts and existing box-opening recipes remain
unchanged. Build and 46 checks pass. [Exchange evidence](LABYRINTH.md) explains
its immediate reviewed exchange contract and the intermediate exchanges.

New daily Exorcism hunts now guarantee an unbound P7 soul, including the existing
paid finish. Build and 619 checks pass, with durable soul receipts on all three
earned difficulties. Pack exchange/opening remains separate. See
[hunt evidence](EXORCISM_HUNT_TRANSACTIONS.md); live acceptance remains pending.

Remaining: inventory the full completion-path set and implement the reward
standard in coherent batches. Other existing quest paths need individual audits
before being marked compliant. Neither of these source upgrades has live-client
acceptance or deployment approval/results.
Warrior epic now awards a pair of unbound level-140 EvilSlayer fists with +12,
-7 and two empty sockets on new contracts. Build and 458 checks pass, including
atomic enhancement persistence and equipment/reconnect. Historical contracts
retain their old pair. See [Warrior evidence](WARRIOR_EPIC_QUEST.md).
Other classes, five-star cosmetic and rune assignments remain incomplete.

Validation of the custom packs: 850 Magnolia checks, 1,341 gift-service checks,
41-item identity audit, four private tooltip tests. Nothing in this decision
constitutes a live deployment, signed client patch or native display acceptance.

Monk and Ninja final awakenings now award an unbound P7 soul atomically alongside
their existing equipment conversions. They preserve owned equipment metadata
and binding; they do not promise maximum enhanced weapons. Full bags and failed
soul writes preserve the material and conversion entitlement. Historic completed
operations do not receive retroactive rewards. The coverage inventory now has
18 source-upgraded families; native/live acceptance and remaining quests stay open.

New second-rebirth confirmations now grant an unbound P7 soul on the earned
Abyss and Exemption Token routes. Its dedicated receipt commits with the entire
rite and survives reload; historical completed rites are not reopened. This is
the nineteenth source-upgraded family. First rebirth and remaining legacy/event
completion paths still need reward audits; live/native acceptance remains open.

First rebirth now grants permanent unbound five-star Armor of Holy Light 187405
alongside its original gifts. Build and 401 first-rebirth checks pass, including
equipment/reconnect, exact capacity and garment-insert rollback. Catalog stars
match server and client. Twenty source-upgraded completion families are now
listed; wardrobe/title acceptance and native/live release remain open.
