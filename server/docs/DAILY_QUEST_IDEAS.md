# Daily adventures for Revora

Design proposal — these quests are not enabled by this document. Names,
objectives and rewards below are suggested additions, not claims about
currently available services.

The daily board should offer three different adventures, rather than a long
list of mandatory kills. Each should take roughly 5–15 minutes, tell a small
story, and send players somewhere worth exploring. Completing any three
finishes the daily bonus; players may swap one unwanted assignment for free.

## A rotating quest pool

| Quest | What the player does | A moment that makes it fun |
| --- | --- | --- |
| The Ferryman's Due | Extract with three relics from the Expedition. | Escape with a small haul or risk a guarded cache. |
| The Last Boat | Extract through the south boat after minute 12. | A late crossing with other hunters closing in. |
| A Guardian's Hoard | Defeat one cache guardian and successfully extract its spoils. | Winning the fight is only half the job. |
| Tracks in the Dust | Follow three tracks in random order to find an ambush. | The final track reveals which monster followed you. |
| The Missing Courier | Find a wounded courier, recover a stolen satchel, and escort them to safety. | Choose the shorter ambush road or the longer safe road. |
| Lanterns on the Pass | Light three distant signal lanterns, then defend the final one for 45 seconds. | Friends can help defend without stealing quest credit. |
| A Relic That Bites | Carry the Relic Rush relic for 30 cumulative seconds. | A comeback is possible during the double-point final minute. |
| Hold the Broken Line | Clear four Wave Defense waves with at least one post standing. | Recover from a bad opening instead of abandoning the run. |
| Three Posts, One Promise | Finish Wave Defense with all three posts standing. | An optional harder team challenge, outside the required daily three. |
| The Beastkeeper's Wager | Reach wave four in Beast Trial without dying. | Short, skillful play counts; perfect gear is unnecessary. |
| Spoils of the Serpent | Earn and open one Exploit Pack from the Potency hunting field. | A useful introduction to where Potency comes from. |
| A Name Worth Hunting | Participate meaningfully in one daily world-boss hunt. | Credit rewards contribution, not only the final blow. |
| The Blacksmith's Errand | Gather three marked fragments from different enemy families. | Explore several regions instead of camping one spawn. |
| An Unquiet Shrine | Defeat a shrine guardian using a rotating encounter mechanic. | One day dodge marked ground; another interrupt a channel. |
| Lost Along the Coast | Find three buried chests using short location clues. | Clues change daily, and no single coordinate solves them all. |
| A Hunter's Mercy | Rescue two trapped travelers while fighting off raiders. | The rescue matters as much as the kill count. |
| Rival's Respect | Finish a fair event match against another active player. | A close loss still makes progress; repeated arranged opponents do not. |
| The Wandering Trader | Locate a trader who moves between three announced camps. | A brief world encounter with a small story and a useful reward. |

## Suggested reward bands

Use existing rewards first. These are starting balance values for testing:

| Difficulty | Suggested reward | Daily limit |
| --- | --- | --- |
| Short exploration or rescue | 1,000 CPs + 50 RVPs | One claim per assigned quest |
| Standard combat or event objective | 2,500 CPs + 100 RVPs | One claim per assigned quest |
| Optional difficult challenge | 5,000 CPs + 250 RVPs | One optional challenge |
| Complete any three assignments | 5,000 CPs + 200 RVPs | Once per server day |

Three standard quests plus the completion bonus give 12,500 CPs and 500
RVPs. That is a useful steady reward without approaching the daily boss's
1,000,000 CP / 50,000 RVP payout. Keep guaranteed P7 souls out of routine
dailies. A cosmetic title after a week of varied completions would be a
better long-term prize than endlessly increasing currency payouts.

Potency rewards should initially come from the actual Exploit Packs already
earned by the hunting quest. Avoid paying the same pack's Potency twice.
New currencies, fragments and collectibles need item/catalog work before
they can be advertised as rewards.

## Rotation and access

- Offer one exploration/rescue task, one combat task and one event task.
- Provide a combat alternative when an event will not run during the player's
  available time. The three mandatory tasks must never require a rare spawn.
- Keep the daily world-boss quest optional. Choose contribution thresholds
  after measuring damage, healing and support participation on this server.
- Scale ordinary quest encounters to player progression; reserve difficult
  variants for optional challenges. No GM status or owner allowlist.
- Show progress in the quest tracker and announce the next concrete step.
  Dialogue should sound like a person asking for help, not a configuration page.

## Short dialogue examples

**The Missing Courier**

“Our courier never reached the pass. Find him before the raiders do.”

Options: “I'll look for him.” / “Another time.”

**The Ferryman's Due**

“Three relics from the coast will settle your debt. Make sure you reach my boat.”

Options: “Save me a seat.” / “Not today.”

**Hold the Broken Line**

“Those posts are all that stand between us and the beasts. Hold them.”

Options: “We're ready.” / “We need more hands.”

## Implementation order

1. Ship a small board with the existing Extraction, Potency, Relic Rush and
   Wave Defense objectives. Reuse confirmed NPC and progress packets.
2. Add durable per-character/day assignments, progress and atomic claims.
   Persist the chosen rotation so a restart never rerolls or resets it.
3. Add rescue, tracking and lantern encounters after their movement and
   objective interactions work for ordinary players on real client terrain.
4. Add optional weekly variety and cosmetics after observing completion
   times and the currency entering the economy.

Credit each kill, rescue or event completion once by its durable event ID.
Reject remote interactions, repeat claims and contributions from fake world
actors. Full inventory must preserve the claim. Item consumption, reward
creation and claim completion must commit together. Tests should include
reconnects, restarts, duplicate packets, day rollover, full bags, failure at
commit and two players competing for the same objective.
