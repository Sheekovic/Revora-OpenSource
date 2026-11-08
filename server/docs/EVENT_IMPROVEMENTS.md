# Extraction and event improvements

## Extraction / Expedition

Replaces the old 96×96 arena with a separate 1024×1024 Bird Island instance
(base map 1015). A connected 460×460 hunting region supplies terrain-checked
entry points, encounters and objectives; players can explore the larger map.
Normal Bird Island players and spawns are unaffected. The instance removes
copied outward portals. Sixteen starting positions are at least 35 tiles
apart, and entry provides ten seconds before combat.

The existing schedule remains every three UTC hours, with five minutes of
registration and a 20-minute hunt. Registration is solo. The owner selected
PvP/PvE, so participants enter PK mode and their previous mode is restored
when they leave. Normal equipment remains protected; unextracted spoils are
lost on death, departure, disconnect or expiry.

- 72 AlienSerpent scouts roam the hunting field and replenish after 45 seconds.
  Each defeated scout uses the existing 20% temporary-loot roll.
- Three separate SilverHawk guardians protect three contested caches. A
  cache grants its first claimant three temporary items and a 250-RVP bonus.
- West, east and south ferrymen open at minutes 5, 8 and 12. Boarding requires
  being within five tiles and holding still for ten seconds. Damage or
  movement cancels boarding. The HUD shows each boat's coordinates/opening
  time and the three camp coordinates.
- At minute 10, HorrifyingSpecter appears near the old trail. Its killer
  receives up to three temporary items and a 500-RVP bonus, payable only on
  successful extraction. The existing corrected 5M-HP client profile is reused.
- The satchel holds twelve items. A cache requires room for all three rewards.
  Scout and boss loot fill remaining space without exceeding the cap.
- Successful extraction grants 100 RVPs + 20 per item + earned cache/boss
  bonuses. The existing item pool is Meteor, MeteorTear, +1 Stone and DragonBall
  (configured item IDs remain authoritative). No daily world-boss purse is
  awarded inside this event.

Scouts preserve AlienSerpent's 35,050 HP; guardians preserve SilverHawk's
1,543,200 HP. Base hits are 1,200–2,000, 2,500–3,500 and 4,000–5,500 for
scouts, guardians and the Specter respectively, before later combat effects.
Names and HP match installed client profiles, so no extra patch beyond
1.0.12 is needed for this event update.

Extraction claims, inventory rows and the resulting RVP balance commit in
one PostgreSQL transaction (`revora_expedition_claims`). Replayed completion
cannot award twice. Full bags leave the satchel intact. Ambiguous database
failures fence the character for reload. Temporary round participation is
in memory and is forfeited on a server restart; completed rewards persist.
Operators should avoid restarting during a live hunt.

Status is written to `database/extraction-status.json` on initialization,
during a hunt and at completion. It records instance/base map, dimensions,
reachable placement count, objectives and encounter count, without player data.

## Relic Rush

The HUD now gives remaining time and the relic/carrier's coordinates. Hold
points double in the final minute, with an arena announcement. Score messages
use points rather than seconds. A departed or disconnected carrier leaves a
visible relic at the last recorded position; previously the holder could be
cleared without restoring the hidden relic. Existing prizes are unchanged.

## Wave Defense

Clearing waves 1–7 starts an eight-second regroup period. Surviving posts
recover 10% of maximum HP and living participating players recover 15%,
once per cleared wave. Fallen posts and dead players are not revived.
New attackers target only surviving posts. The HUD shows each post's HP
percentage and the next-wave countdown. Existing lives, prizes and final
victory conditions are unchanged.

## Verification

The combined fixture suite uses a disposable PostgreSQL schema and the
installed terrain. It exercises ordinary-player entry, separated starts,
all encounter placements, guarded/cache competition, each extraction gate,
cancelled/early/replayed extraction, full bags, atomic saved rewards, database
rollback, late-boss rewards, expiry, PK restoration, wave regrouping/targeting,
and Relic Rush scoring/departure recovery. It retains the earlier daily-boss,
boss-HP, Potency and retired-reward regressions. Native visual playtesting of
the expanded event remains separate from these server fixtures.

Future quest proposals are in [DAILY_QUEST_IDEAS.md](DAILY_QUEST_IDEAS.md).
