# Dis City treasury and issued victory keys

2026-09-15, baseline `d34599b`. This completes the treasury earning/opening loop
for the current Revora combat edition. The later [exchange checkpoint](DIS_CITY_EXCHANGES.md)
adds Star Sword earning and dress rewards. Difficulty variants, Dark Horn,
next-day rank prizes and genuine client acceptance remain open.

## Ordinary-player route

Complete the four trials and claim victory from Solar Saint. The final claim now
includes one bound Pluto's Key (3006982), alongside the existing Sacred Refinery
Pack and Yin Jade. Key issuance, weekly completion, achievement, all reward items
and mirror consumption commit together. The key receipt has a deferred foreign
key to that completed run; one run cannot issue multiple keys.

Solar Saint in Twin City (327,250) offers passage when the player carries an
unspent issued key. The private room uses the installed treasure map profile
3954 and the reviewed 120-by-120 cached floor. Admission is free, consumes no key,
and works outside combat admission days. It shares the sixteen-owner instance
limit with the dungeon. Another character cannot enter the owned room.

Visit the Hell Treasury chest and choose the offered key, or right-click an issued
key beside the chest. The chest consumes one owned, bound, unlocked, non-expiring,
single-unit key in the bag. Unrelated same-type items, transferred ownership,
expired room access, distant requests and replayed spent UIDs cannot open it.

The [official guide](https://co.99.com/guide/quests/monstercity.shtml) describes
key-based random openings and a fifth-opening prize. The precise key frequency,
weights and quantities below are **Revora tuning**, not recovered original values:

| Opening | Prize, always bound |
| --- | --- |
| Each regular opening | 50% one Super Refinery Pack; 35% one Sacred Refinery Pack; 15% one Yin Jade |
| Every fifth opening | Two Sacred Refinery Packs and one Yin Jade, with no random draw |

The count persists across weeks and visits. One key is earned per weekly victory;
keys can be saved. The fifth prize requires two empty bag spaces because its key
is replaced by three items. Insufficient space preserves both key and entitlement.
A regular prize replaces its key even in a full bag. Packs and jades retain their
existing separately tested use handlers.

## Transactions and recovery

The character inventory lock and a locked treasury row serialize openings. Key
consumption, marking the issued key used, reward insertion, claim receipt and
opening count form one database transaction. Inventory updates occur after commit.
An NPC offer is bound to an exact key UID, room epoch and one-minute lifetime.
Repeated clicks cannot silently choose the player's next key. Explicit right-clicks
are tied to the selected item UID and canonical inventory object.

The room expires after two hours and contains no monsters. Solar Saint provides
the exit. Departure, death, disconnect and expiry use the shared dungeon cleanup.
The last treasury map ID is persisted before travel, separately from combat
progress, so login can recover after runtime maps are lost. Visiting the treasury
does not advance or erase an unfinished combat run.

`items.days` is a legacy text column. Eligibility compares its stored `'0'` value;
the fixture clones the actual production table structure. A read-only column
inspection identified this before completion of the treasury batch.

## Content evidence and private patch

Installed HellCrystal/HellTreasury profiles replace the generic crystal appearance
and supply the chest. Their meshes are 37480 and 37490. The new room uses existing
map 3954 as a documented Revora choice; its name and walkable coordinates do not
prove that it was the original Dis City treasury. Map/profile hashes are recorded
in [protocol research](PROTOCOL_RESEARCH.md).

The combat floor table in [restoration evidence](DIS_CITY_RESTORATION.md) now
reports walkable counts correctly; earlier figures were blocked counts. Terrain
hashes and the production traversal behavior did not change.

The private overlay generator now includes the usable key route, opening odds and
updated final reward text. Stage: `obj/chi-refill-validation/dis-city-treasury-overlay`.
It supersedes the earlier combat-only stage, preserves Summer/Labyrinth changes,
and retains the same input hash gates and separate-directory safeguards.

| Output file | SHA-256 |
| --- | --- |
| `ini/itemtype.dat` | `19d6824c2e2c61533d3606abf5946202a1de7f14a17865372b2ec038a87fa445` |
| `ini/Questinfo.ini` | `d1b37e1f40b8001e9b17090303baccb0f281baff7f23629edd265371e5c43af2` |

No installed client, public download, production record or live binary was changed.
The stage is unsigned. Native room rendering, chest/crystal appearance, quest text,
inventory delivery and item use remain acceptance gates.

## Validation scope

The route fixture earns its first key by completing all four combat stages through
production handlers. Additional keys represent seeded historical weekly victories,
using the same issuer and foreign-key-backed completion records; this is not a
claim that six separate full combat runs were performed. Checks cover actual
treasure-map construction, production player jumps, NPC requests and item use,
key deserialization, unissued keys, failed final issuance, failed opening writes,
duplicate clicks, offer expiry, locked keys, prize boundaries, fifth-opening space,
six openings, cleanup, expired access and lost-map recovery. All 119 Dis City
checks pass, together with 248 related regressions (refinery 108, jades 57, mail
32, quest packets nine and Labyrinth 42). Twelve overlay checks and three producer
audit checks pass. The source inventory still has 81/337 reviewed producers;
this reward loop does not invent additional achievement identities.

The isolated Debug/x86 build passed. Tested assembly SHA-256:
`8EED19BC646AEAFD07D8E13D1F0EE7714E3E1B78B8E3E03699561659FBD5A3C2`.

## Fresh earned key and reward opening (2026-10-03)

- [x] Continue FreshRoadmap1 from its genuine four-stage Dis City completion,
  using the saved earned key3006982. Direct key use leaves state unchanged;
  Solar Saint admits the ordinary owner into the treasury based on map3954.
- [x] An unoffered chest request keeps the key. After obtaining the offer,
  twelve concurrent ordinary NPC requests consume the key once and record one
  opening. Reusing the consumed item or repeating the request adds nothing.
- [x] Return to town; attempted reentry without the key is refused. Open all
  currently owned3008057/3008058 refinery packs, including the earned ranking
  prizes, through ordinary item handlers. Each consumed pack maps to exactly
  one owned bound output in its saved receipt; consumed-pack replay is unchanged.
- [x] A separate game/database restart preserves exact keys, treasury opening,
  pack outputs, Dis City run/claims, ranking and Horn receipts, possessions,
  profile and nested prior career.

Fixture `tools/CheckCandidateFreshTreasury.cs` passes
`career-fresh-treasury-{earn,reload}` on the first run; checkpoint
`revora_fresh_treasury_probe`, done=true. Private game SHA-256:
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
The same batch performs the documented Dark Horn and next-day ranking claims.
Proximity and clock are controlled; native navigation/display and live acceptance
remain open. This covers one genuinely earned key, not later multi-key tiers.
No production rebuild, client patch, live restart or deployment occurred.
