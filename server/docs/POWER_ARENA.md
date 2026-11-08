# Power Arena restoration

M4.3 restores ordinary-player admission, seven floors, saved defeat penalties,
badge exchanges and the two confirmed achievements. Code and isolated checks
are complete; deployment and genuine 6711 client validation remain pending.

## Ordinary attack and population budget (2026-10-04)

The selected320 review reuses the earned Archer loadout and the existing
365-kill/claim/reload journey. A read-only private catalog check found populations
30/30/30/4/30/30/30: `Map.LoadMonsters` uses `max_per_gen`, not `maxnpc`.
The40-second database respawn becomes45 seconds in the loader. Including one
second for the500ms timer's strict elapsed-time check, fourteen full-wave waits
cost644 seconds. Actual guardian configuration and the production ranged damage
calculator give one hit per guardian for the conservative earned profile.

At one attack per second,365 attack seconds plus644 waiting seconds leave2591
seconds of the one-hour window. A stated900-second movement/claim/healing
allowance gives a1909-second budget. This is an unopposed arithmetic budget,
not a measured survival or navigation run; other hunters share the population.
No spawn rate, quota or reward changes were made. The selected320 planner reads
[the budget and limitations](research/POWER_ARENA_WINDOW_BUDGET.json) so this
specific population question need not trigger another full event replay.

## Five-star final reward (2026-09-21)

Newly admitted arena sessions award permanent unbound **Somersault Cloud mount
armor 200449**, verified five-star in the loaded server and installed client
catalogs, when the seventh floor is cleared. Either the Badge or EXP Ball choice
includes the mount. Floors one through six remain intermediate stages. The
normal full route defeats 365 guardians; retreat/death may require more.

The mount is limited to the final floor's first claim per character/session.
There are two sessions daily. Existing runs migrate with `valuable_reward=false`
and keep their original gifts for that window, including after restart or
same-window re-entry. Admission in the next window sets the new contract flag.
Historical claim receipts are not reopened.

`revora_power_claims.mount_uid` is nullable and unique. The floor item, mount,
claim receipt, badge credit, completion achievement and checked run update commit
together. Both item slots must be available; receipt insert and run update must
each affect one row. Failure rolls back both rewards.

Build and **106 Power Arena checks** pass: ordinary seven-floor earning, both
reward choices, concurrent final claims, exact capacity, failed mount insert,
late achievement rollback, old-contract completion and next-window migration.
The actually earned mount equips in slot 17 and survives production item reload
with the same UID/binding. A separate earned mount enters the wardrobe, tolerates
a repeated request, saves `inwardrobe=1`, and qualifies for five-star mount title
key 60040023. The equipment fixture supplies vitality for its initial character;
no mount or completed floor is injected for these reward checks.

Evidence: [catalog audit](FIVE_STAR_QUEST_REWARDS.md),
`obj/chi-refill-validation/power-mount.log` (ignored). This proves source earning,
equipment/item reload and wardrobe eligibility. The follow-up uses complete
isolated character/login tables and fresh production character/item loaders for
both actually earned mounts. Equipped UID/binding, wardrobe ownership and title
60040023 survive reload; neither final-floor choice can replay the mount claim.
Both characters remain ordinary players. This closes the server reconnect gate,
not a live socket/native-client test. Follow-up log:
`obj/chi-refill-validation/power-mount-reconnect.log` (ignored).
Genuine-client rendering/riding/title display and live release remain open.

## Player route and explicit rules

[The official guide](https://co.99.com/guide/quests/powerarena.shtml) establishes
the two daily sessions, seven floors, Agates, defeat/re-entry behavior and
2/6/12-badge exchange prices. Installed client achievement data defines 11013
as clearing floor seven and 11014 as earning twelve Arena badges.

Arena Manager Wang (500010) is restored near Twin City 449,293. Admission is
open from **13:00 to 14:00** and **20:00 to 21:00 America/Los_Angeles**, including
daylight-saving changes. Closing times are exclusive. There is no level, class,
GM or account-name gate. Active competing-match ownership prevents entry.

Each floor uses its installed map (1730–1736), Agate (3956–3962) and guardian
family (2133–2139). The following are explicit Revora v1 choices where the
official guide does not supply numeric rules:

| Floor | Required guardians | Guardian HP | Attack range | Level | Defense |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1 | 30 | 15,000 | 1,125–1,504 | 100 | 800 |
| 2 | 45 | 30,000 | 1,425–1,904 | 105 | 900 |
| 3 | 60 | 45,000 | 1,725–2,304 | 110 | 1,000 |
| 4 | 35 | 60,000 | 2,025–2,704 | 115 | 1,100 |
| 5 | 45 | 75,000 | 2,325–3,104 | 120 | 1,200 |
| 6 | 60 | 90,000 | 2,625–3,504 | 125 | 1,300 |
| 7 | 90 | 105,000 | 2,925–3,904 | 130 | 1,400 |

Counts follow the installed Agate dialogue/route, resolving the contradictory
first-floor 30/60 requirement in favor of its displayed 30. Runtime guardian
configuration replaces the old inverted HP/level progression. Attack values are
base monster stats, not guaranteed damage against a player's defenses.

An Agate offers **one bound Refinery Badge (720148) or one bound EXP Ball (722136)**
after its quota. Each floor grants one chosen gift per daily session. The EXP
choice is item delivery and follows existing EXP Ball use restrictions. The
unverified historical direct-XP amount is not invented. The retired Christmas
CP/RVP and gift payouts are removed from these NPC paths.

The player must clear the current floor before advancing one floor. Retreating
resets that floor's current hunt; already claimed rewards remain claimed.
Re-clearing an earlier floor permits advancement without issuing another gift.
Quitting permits re-entry during the same opening, retaining the session's claim
mask. A later opening supplies a fresh seven-floor entitlement.

A defeat on floors 2–6 returns the player to the preceding floor; floor 1
restarts floor 1. Defeat on floor 7 ejects the player and imposes a saved
five-minute cooldown. Revival restores health/mana. These maps use existing
PK-free rules. External teleports cannot enter or bypass the Arena's owned
travel path. Session closure returns remaining participants to town.

Clearing floor 7 records 11013 in the same transaction as its claim. The twelfth
badge **earned from Agates**, accumulated across sessions, records 11014. Bought
badges, legacy kill counters, imported rewards and client requests never supply
these achievements. Badge spending does not reduce earned achievement progress.

## Badge shop and usable packs

Wang exchanges physical badges at any time, including stacked or previously
acquired badges. Primary, Medium and Superior cost 2, 6 and 12 respectively.
His two-page menu covers all sixteen equipment/stat combinations in the guide:
weapon Critical Strike and Breakthrough, Backsword Skill Critical Strike,
Armor Immunity/Counteraction/Detoxication, Headgear Intensification/Detoxication/
Block/Penetration, Shield Block and Bracelet Penetration.

The 48 packs use installed item identities and produce bound materials. A
two-minute server-owned confirmation is consumed once, preventing duplicated
Buy confirmations from charging twice. Stack decrements, pack creation and the
exchange receipt commit together. Full bags and insufficient badges preserve
payment. Stale ownership fences the player for reload.

Opening one of these pack families verifies the canonical inventory object and
database ownership, then atomically replaces the pack with one material and a
saved result. It preserves the existing pack-to-material mapping, bound state
and nominal 63%/27%/10% material-offset roll. These odds are inherited server
behavior, not a claim about official rates. Neither reconnect nor duplicate use
can re-roll a committed pack result. The former nontransactional switch paths
for these families are intercepted by this validated implementation.

## Persistence, evidence and checks

- `revora_power_runs`: one current session per character, stage, capped kills,
  claim mask, clear/entry state, UTC cooldown and pending defeat transfer.
- `revora_power_claims`: one immutable floor claim per character/session/floor.
- `revora_power_totals`: capped progress toward twelve genuinely earned badges.
- `revora_power_exchanges` and `revora_power_pack_results`: durable payment and
  random delivery receipts, keyed to distinct item identities.

The driver returns plain `DateTime` timestamps in local time. These routes read
`DateTimeOffset.UtcDateTime` explicitly and write typed UTC parameters. NPC/item
transactions hold the player's item lock and database rows. Death penalties
commit before the callback returns without acquiring the victim's item lock
from a killer's stack; revival is queued to the world timer. The pending transfer
flag restores an interrupted defeat once after reconnect. Failed writes fence
further mutations until ordinary inventory/event reload.

Private client evidence was read-only. `Achievement.dat` SHA-256 is
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`;
`itemtype.dat` SHA-256 is
`d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a`.
No proprietary catalogs or client files are committed. Existing NPC, map, item
and achievement packets are reused; native tracker and live visuals are unverified.

The isolated build passes **95 Arena checks and 355 regressions**: 31 Moon Box,
44 Demon, 78 Elite PK, 48 CTF, 63 progression and 91 wardrobe, **450 total**.
The Arena fixture exercises two daily sessions through the twelve-badge
threshold, ordinary NPC and item dispatch, all 48 purchase/opening combinations,
actual monster/PvP deaths, duplicate requests, failure injection, saved defeat
recovery, full/stacked inventory, malformed ownership and summer/winter boundaries.

Run `tests/PotencyHunt/Verify-PotencyHunt.ps1 -PowerOnly` and existing regression
modes sequentially. They use the disposable `revora_potency_validation` schema,
removed after each run. Validated staged executable SHA-256:
`f440b323d57941198bafffb942857e7b7218f73331196011b39f88e11a7cf3cf`.

No live server replacement, database migration, restart or client release was
performed. Before activation, preserve the current binary/database, review M1's
achievement migration, and verify NPC travel, floor combat, defeats, claims,
badge stacks, all pack categories and reconnect on genuine 6711 clients.
Rollback restores the prior binary while retaining earned item/receipt data.

## Retained career: seven floors and twelve badges (2026-10-01)

`career-power-first`, `career-power-second` and `career-power-reload` pass on
private full-server SHA256
17976314CE4445B573179FF4AC744F2581196BA7E242D2A6BE2A09C31AF713A6.
The existing ordinary Archer career continues from its nobility checkpoint.
A normal decoded trade transfers eight spare unbound weapons to its existing
partner, preserving them and freeing eight bag slots. Equipment is unchanged.

At a controlled13:00 Pacific window, Wang admits the character normally.
Production ranged attacks defeat the required365 loaded guardians through all
seven floors; Agate progression and first-claim checks award seven badges and
one permanent unbound five-star Somersault Cloud200449. The character exits and
exchanges six earned badges through Wang's confirmed medium-pack option. The
same-window duplicate claim and repeated exchange confirmation pay nothing.

After game/database restart, a controlled20:00 window admits the same character.
Five more floors require215 further guardians, bringing lifetime earned badges
to12 and recording11014 beside first-run11013. The character deliberately exits
on floor five; no second final-floor mount is claimed. A second six-badge exchange
leaves two bound medium refinery packs, one new mount and35 bag items overall.
The next cold restart preserves exact career state, item UIDs, balances, claims,
badge totals, trades and exchanges. Expired confirmation replay remains harmless.
`revora_career_power_probe` and CareerPowerSnapshot are the latest retained-career
checkpoint, superseding the nobility checkpoint for subsequent work.

Two fixture corrections were necessary. The first attempt omitted Screen.Add,
so ordinary ranged attacks could not resolve their target; no kill was credited.
The next stopped after floor one and30 floor-two kills because initial floor
populations were exhausted. Recovery retained that admission and all claimed
items. The test now calls the actual map respawn callback with a controlled
future deadline when needed, rather than manufacturing monsters or kill counts.
Recovery queries use DateTimeOffset to match the saved UTC event window.
No character, reward or progress row was reset. The final first-session process
completed the remaining305 kills; the second completed215.

Scope: real combat calculation and death credit with controlled target approach,
attack cooldowns, visibility setup, respawn deadlines and event calendar. This is
not natural-time balance, packet-authenticated combat, native riding/appearance,
or deployment. Stored achievement identities rise from208 to210 (127 absent);
mixed provenance remains, so210 is not an independently earned or overall total.

## Fresh two-session earning and reward recovery (2026-10-03)

- [x] Continue ordinary FreshRoadmap1 from its paid Treasury/Horn checkpoint.
  Use existing earned equipment and preserve possessions through ordinary
  warehouse storage if needed. Wang admits the player during a controlled
  Pacific13:00 window on2041-03-13.
- [x] Complete all seven floors:30/45/60/35/45/60/90 guardian kills,365 total,
  through1,856 calculated melee attacks. Premature clear requests pay nothing;
  repeated successful floor claims do not change the saved state.
- [x] Earn seven badges, achievement11013 and one unbound Somersault Cloud200449.
  Exchange six earned badges through Wang for one medium refinery pack723655.
  A separate process preserves the exact claims, mount, pack and prior career.
- [x] At the controlled20:00 opening, earn five more badges through215 kills
  and612 melee attacks. The cumulative twelve badges earn11014. Exchange the
  six remaining owned badges for a second medium pack; duplicate confirmation
  pays nothing. A further cold restart preserves the exact nested state.
- [x] Open both earned packs through ordinary item use with unforced result
  rolls. Both yield bound724406 in this run, each linked to its exact consumed
  pack UID. Consumed-pack replay, another full restart and duplicate exchange
  confirmation grant nothing further.

Fixture `tools/CheckCandidateFreshArenaPower.cs` passes all five phases on their
first runs: `career-fresh-arena-power-{first,second,reload}` and
`career-fresh-arena-packs-{open,reload}`. The final checkpoint is
`revora_fresh_arena_packs_probe`, wrapping `revora_fresh_arena_power_probe`.
Private game SHA-256:
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.

Event clock, target proximity, screen registration, attack cooldown and map
respawn deadlines are controlled. Monster retaliation, natural timing/balance,
native mount presentation and live acceptance remain open. No gameplay state
was seeded, no production build changed, and no live restart or deployment
occurred. The older post-Beast achievement export remains historical.
