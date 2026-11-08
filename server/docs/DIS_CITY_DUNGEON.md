# Dis City four-stage combat route

2026-09-15, baseline `818b618`. The four stage earning paths are implemented and
automatically tested. The [treasury checkpoint](DIS_CITY_TREASURY.md) subsequently
adds issued weekly keys and private chest rewards. The [exchange checkpoint](DIS_CITY_EXCHANGES.md)
adds Star Sword earning and dress choices. Difficulty variants, Dark Horn
exchanges, next-day rankings and native client content/acceptance remain open.
This is a Revora reward edition, not a claim that every original reward weight
or presentation detail has been reproduced.

## Entry and objectives

Solar Saint 3215 stands in Twin City at (327,250). Ordinary authenticated players
of level 110 or above can begin on Monday, Wednesday or Friday, Pacific time.
An unfinished run can resume on other days. Enrollment freezes the Hall quota
and a permutation of the nine crystal identities. One final reward can be claimed
per Pacific Monday-based week. Progress continues across deaths, voluntary exits,
instance expiry, disconnects and server restarts.

The [official guide](https://co.99.com/guide/quests/monstercity.shtml) and
[installed stage evidence](DIS_CITY_RESTORATION.md) support these objectives:

| Stage | Required progress | Durable achievement | Revora stage reward |
| --- | --- | --- | --- |
| Hell Gate | Earn and submit five issued Soul Stones | 11002 | One bound Super Refinery Pack |
| Hell Hall | Defeat 400 owned demons; Archer 600, Taoist 300, fixed at entry | 11003 | One bound Super Refinery Pack |
| Hell Cloister | Defeat each appointed Hell Wraith and activate its crystal, following the sealed nine-crystal order | 11004 | One bound Sacred Refinery Pack |
| Battle Formation | Earn four issued Syren scraps, combine the token, use it to summon Pluto, defeat that owned boss and claim victory | 11005 | One bound Sacred Refinery Pack, one bound Yin Jade, one bound Pluto's Key; 20% chance of a bound Star Sword |

Revora chooses a 25% Soul Stone/scrap chance per eligible kill. These weights and
stage rewards are explicit local tuning, not recovered original distributions.
Refinery packs and jades use their separately tested handlers. The treasury
checkpoint adds the usable key route; the exchange checkpoint adds Star Sword's
saved boss outcome and usable dress reward. Dark Horn is not issued yet.

## Ownership, space and recovery

Each player has a private instance of the current stage. Sixteen players can
occupy the dungeon concurrently; a stage transition briefly holds the departing
map until arrival succeeds. Each ordinary field maintains at most 24 live
monsters; the Cloister exposes one appointed guardian at a time. Pluto has
5,000,000 HP and uses the existing full boss HP projection. Combat values are
Revora tuning; ordinary monsters have 100,000 HP and guardians 500,000 HP.

Only the canonical player's actual kill of an owned encounter advances a run.
The durable death identity includes the run, instance epoch and monster UID, so
recreated maps cannot collide with earlier death receipts. Owned encounter IDs
are excluded from generic loot and CP rewards, even after their runtime object
is retired. The normal death projection follows the committed outcome.

The Mirror of Hell, Soul Stones, scraps and Pluto Token carry issued UIDs tied
to the run. Foreign items and unrelated same-type tokens do not prove progress.
Full bags preserve earned but uncollected stones/scraps; Solar Saint delivers
them when space is available. Stage receipts, submitted item consumption, reward
insertion and that stage's achievement commit together. The final claim consumes
the still-owned mirror, if present. Failed writes preserve the prior state and
require reconnecting before further mutations.

The mirror returns its owner to the current stage's guide and explains the next
objective. Summoning announces Pluto's coordinates. Leaving or dying releases
the private field without resetting earned progress; an uncompleted boss returns
at full HP on a rebuilt field. Instances expire after two hours. The last private
map identity is stored before travel, allowing login to recover a character whose
runtime map no longer exists. Temporary actors and map metadata are released.
Elapsed completion time includes pauses; ranking prizes are not implemented yet.

Private maps use the reviewed 2021-2024 terrain hashes and explicit dungeon map
profiles. Shared house aliases are preserved. The four-argument teleport now uses
the existing current-map status projection, matching other teleport overloads.
The shared identifier counter serializes allocation, preserving its old wrap
semantics while preventing duplicate IDs during concurrent admissions/rewards.

## Validation and remaining completion gates

`Verify-PotencyHunt.ps1 -DisCityOnly` passes 75 checks. The fixture uses actual
private-map construction and reviewed floor files, production NPC handlers,
player jumps to all guides/crystals, issued item use, and monster death handlers.
The final boss is killed through the production attack receiver. Coverage includes
all four objectives, frozen class quota, foreign entry/kills/items, full bags,
stage and death write failures, concurrent claims, weekly reset, expired instances,
lost runtime maps, login recovery, sixteen unique admissions, capacity rejection,
cleanup, and concurrent identifier allocation. The 25% draw is deterministic in
the fixture so it can complete the real 400-kill Hall objective without flakiness.

The producer audit now finds 81 of 337 identities, leaving 256 without reviewed
producers. Five audit tests pass. This is still an upper bound of 81/320 for
Rising Star, not a complete per-character route.

The isolated Debug/x86 build passed, followed by all 1,408 existing regression
checks and the 75 Dis City checks (1,483 total). The tested assembly SHA-256 is
`5725D57E6E1779D2DDEBE38D3B5BEEC506E110109D6C18CCE7E7FCF0F64223EB`.

The [private client checkpoint](DIS_CITY_CLIENT_STAGE.md) now corrects the following
known content issues in a separate staged overlay: installed missions
6472-6475 contain stale Solar Saint coordinates, treasury reward text and expired
historical activity dates. Counter text must reflect each durable objective rather
than implying completion on the first kill; the server now projects objective
readiness separately from raw progress. Existing task packets are projections
only; quest-begin requests cannot grant achievements. Native pathfinding links,
terrain, crystal appearance, combat and boss-bar acceptance remain pending.

Before closing the whole Dis City milestone, finish reviewed exchange rewards,
difficulty choices and next-day ranking receipts, then
stage coherent client text and validate it on genuine 6711. No production records,
installed client files, live server binaries or running services were changed.

## Valuable final victory reward (2026-09-20)

New expeditions freeze `valuable_reward=true`; existing saved runs default false.
The final stage-four turn-in adds one unbound uniformly selected catalog P7 soul
to the existing packs, key, horn and optional sword. Intermediate stages do not
receive this completion reward. The soul UID is included in the existing stage
claim's `rewards` array, in the transaction consuming the mirror and saving the
weekly completion. Capacity includes the extra item. Weekly limits, optional
Star Sword odds, treasury, ranking and old-run reward terms remain unchanged.

Isolated build and 179 Dis City checks pass. The full earning route reaches the
new final reward; failed key issuance leaves no saved soul, concurrent claims
retain one soul and its saved receipt, and weekly replay stays closed. This
suite also covers the existing treasury, relic, horn and ranking flows. An additional old-contract fixture applies the migration's false default at
admission, resumes the run, earns all four stages, and confirms original rewards
through item reload and weekly replay. It uses a separate unsettled ranking day.
Log: `obj/chi-refill-validation/dis-city-p7.log` (ignored).
Native-client and live Host acceptance remain pending.

## Fresh Warrior full earning and cold recovery (2026-10-03)

- [x] Continue ordinary FreshRoadmap1 from its completed Boss Rush checkpoint.
  Preserve earned equipment and store20 owned items through the Ape City
  warehouse packet before admission through Solar Saint.
- [x] Complete all four stages with production randomness:15 stage-one kills
  obtain five stones;400 stage-two kills meet the Warrior quota; nine guardians
  and the drawn crystal order clear stage three;30 further kills collect the
  scraps and defeat summoned Pluto. Total454 kills and5,447 calculated melee
  attacks. No item, drop result, damage value, or death callback is supplied.
- [x] Claim all four stage rewards through normal NPC/item handlers. Save one
  completion and achievements11002-11005. Every reward UID in all four claim
  arrays remains owned; the final claim contains one bound soul. The historical
  September20 unbound wording above is superseded by the current bound-P7 policy.
- [x] Cold process recovery preserves exact quest, four claims, completion,
  possessions and nested career. Repeated weekly admission gives no reward and
  leaves the completed snapshot unchanged.

Fixture `tools/CheckCandidateFreshDis.cs`; phases `career-fresh-dis-{earn,reload}`.
Checkpoint `revora_fresh_dis_probe`, done=true. Private executable SHA-256:
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.

The first earning attempt completed and saved all four stages, then its replay
assertion failed: it compared the automatic town-return position against the
subsequent NPC approach. The fixture now approaches before taking the replay
baseline. A first resume correctly refused completed-week admission but still
expected an active dungeon; the resume branch now detects the saved complete
run and checks its rewards without replaying combat. The corrected earning
verification and separate cold reload pass. A result-marker naming mismatch was
also corrected before those final phases. No gameplay rows were reset or patched.

Admission clock, proximity, three-second event ticks and attack cooldown are
controlled. Retaliation, natural difficulty/timing, native travel and live
acceptance remain unverified. Treasury, Dark Horn and ranking follow-ups are
not completed by this batch. No production rebuild, live restart or deployment
occurred; the post-Beast receipt export retains its earlier scope.
