# Rare Materials: durable daily commissions

2026-09-21 update: new contracts include one bound level-1 Tortoise Breaker rune.
Existing and imported legacy contracts retain their original rewards. See the
[saved contract, capacity and transaction rules](DAILY_REWARD_POLICY.md).
Native acceptance and deployment remain pending.

Implemented from `e33f438` on 2026-09-16. The existing Revora daily service now
commits its gem requirement, RVP rerolls, paid stars, CP completion and whole
reward through `DailyRareQuest`. Eligible ordinary players can earn all four
normal-gem offerings through the existing mines and complete the service.

## Evidence and edition boundaries

The [official Rare Materials guide](https://co.99.com/guide/quests/rare_materials.shtml)
confirms a daily level-75 Envoy service with three star grades and material
turn-in. It lists 30/50/70 study points and 120/240/360 minutes of EXP by star,
plus one Chi Token, and a one-CP change fee. These differ from Revora's custom
economy. This implementation repairs that existing economy's persistence; it
does not silently substitute the original prices or rewards.

| Rule | Retained Revora default |
| --- | --- |
| Minimum level | 75 |
| Requirement | Three normal Phoenix, Dragon, Fury or Rainbow Gems |
| Reroll | 25 RVPs, maximum four changes |
| Star upgrade | 10 RVPs, up to three stars |
| Paid completion | 5,000 CPs |
| Reward | 2,500 CPs, 15 RVPs, one EXP Ball of direct experience and one Chi Token |
| Study reward | 15, 25 or 35 points at one, two or three stars |
| Reset | Pacific midnight, with daylight-saving handling |

The previous exclusive random bound selected only the first three of four
implemented requirements. Its default is now 5, enabling the existing Rainbow
Gem commission. Plain Mine 1028 supplies Phoenix/Dragon/Fury Gems. Forest Mine
1025 supplies Rainbow Gems. These catalog identities and earning sources were
already implemented in [Mining Gem Earning](MINING_GEM_EARNING.md); the new
fixture joins them to the complete commission. The NPC briefly names the
appropriate mine and asks the player to take a pickaxe.

## Persistence and payment behavior

`revora_rare_runs` owns each character/day's assignment, quantity, star, reroll
count, frozen tariffs/rewards, paid RVP/CP totals and final reward receipt.
Character inventory and database owner locks serialize mutations. Experience
claims use the shared mentor lock and progression transaction as well.

- Acceptance creates a durable one-star commission. Old RAM flags do not
  authorize payments or rewards.
- A reroll atomically deducts RVPs and saves a different gem requirement while
  preserving the purchased star. No unbounded random retry loop remains.
- A star purchase needs its own reviewed two-minute continuation. The RVP
  deduction and new star commit together. Repeated clicks cannot charge twice.
- Completion consumes exact owned gem quantities or the skip CP fee, then
  commits all currencies, study, Chi, progression, skill/Arena effects, mentor
  credit, recorded rebirth recovery and daily completion in one transaction.
- Bound gem inputs keep the Chi Token bound. Full bags may use a fully consumed
  stack's freed slot; a partial stack cannot consume gems without reward space.
- Cache updates and existing inventory/point packets follow commit. A failed
  write rolls back the whole exchange and requires reconnect before retry.

The latest durable day prevents clock rollback and legacy mirror resets from
repeating the reward. Login and the Envoy menu refresh the legacy display
fields from the ledger. Starting a new day resets the commission to one star.
Maximum-level players receive non-EXP rewards without altering saved EXP.

The old `MaterialsStar` field was never saved. A completed or valid active
legacy assignment is imported only when its persisted reset date matches the
current legacy server date; a historical paid star cannot be reconstructed
from that record. It starts at one star rather than inventing a prior payment.
New star payments survive production character reload. No retroactive
compensation or guessed star migration is performed.

## Validation and deployment

The isolated server build passes. The focused suite contains 523 checks: 392
shared first-rebirth prerequisites and 131 Rare Materials checks. For each of
the four requirements, an ordinary character accepts through the real NPC,
purchases any selected star upgrades, reconnects, buys and equips a Pickaxe,
walks to the existing mine entrance, enters through the normal NPC/portal
route, mines the full offering, walks back and completes the exchange. A
second production reconnect verifies the reward and purchased star.

Mining ticks and random choices are controlled. This is a server earning-path
fixture, not a real-client mining-speed or difficulty assessment. The suite
also checks payment replay, caps, daily rollover, old-state adoption, frozen
configuration, wrong grades, stale persisted items and currencies, bags,
binding, nullable balances, suppressed writes, mentor credit and first-life
recovery followed by ordinary reload/retry.

Final validation is **2,485 gameplay checks**: Rare Materials 523, Price Hunt
511, Magnolia 466, mining 94, travel 105, experience 452, progression 74,
Release Souls 35, quest packets 9 and Potency/daily bosses/Extraction 216.
The three achievement-audit tests pass. The source upper bound stays 219/337
catalog entries, not a proven per-character route to 320 achievements.
Harness SHA-256:
`7AEF26840D4DF06F86EE86FC47AD9EEC905C06CF30D33E11BF7EC094D4AC178B`.
Logs are ignored under `obj/chi-refill-validation/rare-*.log`. Tests write only
the disposable `revora_potency_validation` schema and read the installed
catalogs/NPCs/terrain as evidence. No achievement is invented for this quest.

Soul Ball, exact original numerical parity and genuine 6711 NPC/inventory/star
acceptance remain open. The optional old paid reset still applies only to
legacy daily state and cannot reset this new ledger. This batch has not
restarted, patched or deployed the live server.

## Valuable reward upgrade (2026-09-20)

New contracts freeze `valuable_reward=true`; existing and imported contracts
remain false. A new completion, including the existing paid option, adds one
unbound P7 soul selected uniformly from the loaded P7 catalog. `soul_uid` is
saved with the completion, Chi Token, material consumption, currencies and EXP
in the same transaction. It requires two reward slots after actual removals.
Old contracts retain their previous one-item reward. NPC reward text follows
the frozen contract. Daily limits, fees and other rewards remain unchanged.

Isolated build and 559 checks pass. Coverage includes full earned routes,
concurrent paid claims, rollback with no saved soul, reload/receipt identity,
full-bag retry, old contracts and repeated initialization after claims.
Log: `obj/chi-refill-validation/rare-p7-reward.log` (ignored).
Live Host/native-client acceptance has not been performed for this upgrade.


## Retained mining and cold commission recovery (2026-10-01)

Retained ordinary character4294908702 continues from the completed Price hunt;
this is not an increment to the main character's320-achievement tally.

The new route accepts the Normal Dragon Gem commission and buys one star upgrade
through the ordinary Envoy. Repeated confirmation spends only10RVPs and retains
star2. The character buys a1,000-silver Pickaxe through a sealed shop request,
equips it and pays the existing100-silver Conductress fare into Plain Mine1028.
Sealed mining intent and production mining yield the first gem before abrupt exit.

A fresh game/database process retains the exact equipped tool, first gem, paid
star, currency and unfinished commission. Two further mining yields complete the
three-gem offering, followed by another abrupt exit. The next process returns
through the installed physical portal and turns in through the Envoy: all three
gems are consumed for2,500CPs,15RVPs,25Study Points, one Chi Token and one bound
level1 Tortoise Breaker rune. No P7 or paid-completion charge is added.

The fourth phase reloads the claimed state and repeats free completion, paid
completion and star confirmation requests. The checkpoint compares complete saved
inventory/equipment rows, purses/position/EXP, Study Points, the original Price
receipts, Rare commission and mining provenance; replay preserves it exactly.

`RareCareer` in `tools/CheckCandidateCareerRites.cs` supplies the four
`career-rare-start`, `career-rare-mine`, `career-rare-claim`, `career-rare-reload`
phases. Calendar, mining tick time, selected draw and approach positions are
controlled; gem items and payments are not injected. This does not establish
natural mining odds/speed, all four assignments' composed-process behavior or
native acceptance. Existing four-route handler tests retain their scope.

The first fixture purchase omitted the mandatory packet seal and was correctly
refused before payment/delivery. The corrected route resumed the existing stage0
commission only after verifying the saved star fee and unchanged CP/silver against
the prior Price checkpoint. It did not repay or recreate the accepted commission.
This is recorded as a fixture correction, not a server purchase defect.

Executable SHA-256:
`61DB4C2F5EFE59F1F4B061AACC9D6AB9ED983EC8F05F64EB5AA4ABA59E715D46`.
All four phases pass after the documented fixture correction. Private logs remain
outside Git. No production source, live database,
installed client or deployed Host changes are part of this validation batch.

## Fresh career with unforced mining loot (2026-10-03)

The production-created ordinary character receives an unforced three-Phoenix-Gem
assignment, buys its pickaxe and mines the gems using normal loot RNG.2144
attempts use an accelerated mining clock;534 newly mined non-gem items are sold
through ordinary Storekeeper packets with exact price assertions. Prior items
and mined gems are protected; the original weapon/shield are restored. Paid
mine passages, earned offering, exact non-P7 payout and all prior possessions
survive separate unclaimed/paid processes; duplicate claims pay nothing.
This extends the earlier forced-roll fixture with unforced acquisition, not
natural-time or native acceptance.
[Evidence and seven-route scope](CAREER_STARTING_BOUNDARY.md#fresh-mined-rare-materials-and-seven-daily-routes-2026-10-03).
