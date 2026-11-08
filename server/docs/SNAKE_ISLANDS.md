# Snake Islands restoration contract

Latest correction: [admission cleanup race](ISLAND_ADMISSION_RACE.md). A waiting
tick no longer cancels a ferry admission after arrival. The corrected build
passes the reproduced race, genuine departure and authenticated next-day claim
fixtures. The older untraced failure's exact cause remains unconfirmed.

## Earned victory and next-day process recovery (2026-09-28)

`tools/Verify-ArenaProcess.ps1 -SnakeOnly -ResetOwnedFixture` passes three actual
game launches with intervening PostgreSQL restarts, on private executable SHA256
`6CF84170A95EF52B68170194D25CE6EA613AA5DD6A6B2F90DFF7ED3D45019E4B`.
The helper compiles with warnings as errors; no gameplay code changed.

An ordinary level100 non-GM profile takes Green Snake's ferry through production
NPC dispatch. The saved admission precedes defeat of the real startup-spawned
Snake King3102 through the production damage/death handler. The fixture verifies
the exact boss UID in the victory, the saved rune reward flag, and one11018
achievement evidence row. The bag is still empty: no reward has been claimed.
After saving the character, the game exits abruptly without graceful shutdown.

The next game/database launch uses the following Pacific quest day. Before
recovery, the item, hunt, attempt and achievement snapshots match exactly.
Production login recovery returns the character to Green Snake on Bird Island;
his claim option pays the previous day's two bound Meteors and bound Tortoise
Breaker rune. All three exact item UIDs belong to the claimant and appear in
the saved reward array. Two repeated claims leave the snapshot unchanged.

A third process/database launch preserves the complete claimed snapshot and
the one11018 evidence row; repeated claims still grant nothing extra. This
verifies that a saved victory is claimable after its day and that claiming it
does not create a second day's victory. It does not test earning a new hunt on
the following day.

Controls: the clock, level100 profile, NPC/combat proximity and lethal damage
amount are supplied. The production receiver/death processing and normal NPC
admission/claim run against the startup world and full private schema. No hunt,
reward, boss, achievement or completion is seeded. Movement across the islands,
attack calculation and authenticated/native UI are outside this fixture; the
earlier isolated35-jump test retains its separate scope. Owned clone cleanup
and private PostgreSQL shutdown succeeded. Live deployment remains pending.

## Authenticated next-day claim and inventory (2026-09-28)

The three-phase Snake suite now authenticates over SRP/DH for the claim and
verification launches. The full suite passes on the same6CF84170 private game
hash, with both helpers compiled using warnings as errors. Normal game login
performs recovery; the helper no longer calls Snake OnLogin directly for those
phases. The shared island probe session setup also retains Sky's existing mode.

On the day after the earned victory, the ordinary account opens Green Snake's
dialogue and claims through game-socket NPC requests. The probe checks the
immediate inventory against all three earned item UIDs, item types and bound
flags. Two repeated claims leave the durable snapshot unchanged. Another
game/database restart, fresh authentication and two further claim requests
preserve those exact inventory items and the saved hunt/attempt/11018 evidence.

One intermediate run failed before authentication: the startup-spawned king was
dead but the expected victory row was absent. That run did not capture character
health or the final attempt reason, so its cause is unresolved. The fixture now
prints only post-hit health/death/map and saved hunt/attempt state to distinguish
combat/death races from persistence faults if it recurs. The unchanged-game retry
passed all phases; this does not prove the earlier failure repaired.

Victory earning still uses supplied damage and proximity in the offline first
phase. This closes authenticated claim, immediate inventory and fresh-login
replay/recovery coverage, not authenticated combat or crossing the islands.
Native-client acceptance, the unexplained earning observation and deployment
remain open. The owned clone was removed and private PostgreSQL stopped.

## Historical restoration checkpoint

2026-09-15 UTC; baseline `8549a0f`. Implementation and 28 isolated checks pass.
Live-client acceptance and deployment remain pending.

The [official Snake Islands guide](https://co.99.com/guide/quests/snakeislands.shtml)
requires level 90, Green Snake entry, traversal of dangerous islands and defeat of
Snake King near (87,67). Achievement 11018 in the reviewed 6711 catalog requires
completion of Snake Islands. Do not grant it for entering the map or killing an
unrelated snake.

The stored Bird Island position for Green Snake 30115, (678,966), is blocked on
the active map. Startup restores him at the official guide's valid (468,203)
position on map 1015. Green Snake 30125 remains in Snake Islands 1063 (87,44).
The installed monster families are Fire Snake
Spirit 3100, Tough Snake 3101, Snake King 3102 and Water Snake 3103. Snake King
spawns near (84,64), with the existing Water Snake guards. Existing combat stats
remain the challenge; no unsupported historical loot probabilities are invented.

The server map SHA-256 is
`a87744ff679f61c2fb69c4a8bce6dfc07e7a735cc41ed79e4f03d92ec3dfc349`.
A read-only graph search found a 35-jump route from the far inhabited island
near (417,367) to the boss at (84,64), using the current server's valid-landing and
floored-distance-at-most-20 rule. It reaches 3,245 of 3,249 valid tiles. This is
server terrain evidence; it is not live-client visual or movement acceptance.

Selected Revora edition: level-90 ordinary players take Green Snake's ferry to
the far island. A saved admission attempt owns the hunt. Only that canonical
player's actual nearby kill of the real world Snake King completes it and grants
11018. One completion per Pacific quest day earns two bound Meteors, claimed
atomically from either Green Snake. Saved victory rewards remain claimable after
reconnect and after their quest day. These fixed, bound, daily rewards are explicit
Revora choices from the guide's reward categories.

Death or leaving ends the active attempt; the player may retry an unfinished day.
A new day requires a fresh ferry admission. Capture attempt identity before death
processing so a late old kill cannot finish a new attempt. Gate the island's
server teleport entry through the ferry, preserving normal player jumps and exits.
Keep full-bag victory rewards unclaimed; reject fake, remote, foreign, duplicate,
wrong-family and unadmitted kills. Preserve admission, completion, reward and
achievement transactions across failures and reconnects.

The 28 isolated checks exercise ordinary NPC entry, the actual production jump
route, boss ownership, all four monster identities, death/retry, day boundaries,
full bags, database failures, concurrent claims, persistence, stale cleanup and
late attempt callbacks. No unconfirmed native mission ID is assigned.

The death/retry fixture exposed a shared revival defect: flag values are indices,
so combining Dead (5) and Ghost (10) removed BlackName (15) instead. Both normal
and Lotus revival now remove the two indices separately. The fixture verifies
the cleared flags and subsequent production jumps. Movement timing and native
visuals still require a genuine client session.

Demon/Summer fixture kill anchors were also moved onto reviewed walkable tiles
on Bird Island (240,208) and Mystic Castle (232,199). This strengthens terrain
validity of the existing death-callback tests; it does not certify native combat.

The isolated build and all 646 preceding quest/event/wardrobe regression checks
pass. Validated server SHA-256:
`BEDC17AEC199B4C44AD6A86C24C9A18BEF0DD6E2ED4960C26854ED2A96E672E3`.

## Daily reward correction (2026-09-21)

New runs now guarantee one bound level-1 Tortoise Breaker rune (4030101)
beside their Meteors. The earlier P7 upgrade was missed by the initial daily
reward audit; pending claims now ignore the obsolete P7 flag. Existing issued
items and completed receipts remain unchanged. New `rune_reward` flags default
false on old contracts; Snake ferry retries and Sky transitions preserve the
saved terms. This is a Revora reward choice, not an original-game reward claim.

Reward UIDs remain in the existing claim array, committed together with final
completion. Capacity includes the rune and, for Sky Pass, the consumed prize
pass's freed slot. Existing contracts keep their Meteor-only payout.

Isolated build and 30 route checks pass, covering ordinary earned travel,
old contracts with obsolete P7 flags, full bags, final-claim rollback, concurrent
claims, exact bound rune receipts, reload and no daily P7. Sky covers all 25
monster/guard associations; Snake covers the 35-jump crossing. Log:
`obj/chi-refill-validation/snake-rune-tests.log` (ignored).
Native client acceptance and deployment remain pending.


## Fresh career crossing and durable victory (2026-10-03)

The same ordinary level124 Warrior (UID1096322) continues from the paid Sky Pass
checkpoint with existing equipment,36 bag items and no supplied progress or
currency. Green Snake refuses an unearned claim, then admits the daily hunt.
The fixture finds safe landings and sends49 sealed jump packets through the
production Data handler at1.2-second intervals. Each accepted landing is checked.
Normal melee calculation defeats the existing Snake King in64 attacks; no direct
HP assignment or supplied death callback earns the victory.

The unpaid victory and11018 snake-islands-v1 evidence survive a private
process/database restart. Normal login recovery returns the character to the
ferryman. The ordinary claim adds exactly two bound Meteors and one bound
Tortoise Breaker rune, preserving every prior bag UID. Another cold load verifies
all three exact saved reward UIDs, the hunt/attempt ledgers and the entire nested
career snapshot. Repeat claim and same-day admission preserve that state and
leave the character on Bird Island with39 bag items. No daily P7 is issued.

`tools/CheckCandidateFreshSnake.cs` phases `career-fresh-snake-earn`,
`career-fresh-snake-claim` and `career-fresh-snake-reload` all pass on the first
run. Logs are under the ignored private candidate root with the corresponding
`arena-process-` phase names. Binary SHA-256 remains
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.

Scope: initial inter-map/NPC approach and final melee proximity/cooldowns are
controlled. Jumps use real terrain and handler checks; this is not native client
input or rendered acceptance. Player pet/screen timers remain omitted; normal
map respawn timers run. Retaliation/balance are not proved. Prerequisite terrain
is staged privately as in the previous career batch. No game-source change,
live restart, deployment or client patch is included.
