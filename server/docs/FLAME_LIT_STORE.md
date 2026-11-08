# Flame Lit owned progression store

M6.67b2 adds transaction participants for rune issuance and the ten ordered
lighting steps; M6.67b3 adds atomic EXP claims. M6.67b connects ordinary NPC/item
dispatch, startup placement and achievement 11016. Source integration is complete;
the live Host has not been deployed and full-client acceptance remains open.
See [research and rules](FLAME_LIT_RESEARCH.md) for the installed identities,
locations, timing evidence and limits of retail parity.

## Authority and persistence

`GuildSeasonWar.AcquireFlame` supplies a disposable handle to the actual active
battle. It validates the canonical pole and gates and the current battle window.
A loose war flag or calendar time alone cannot authorize progression. Acquisition
does not wait on a busy battlefield lock. Callers hold the player item lock,
acquire the handle before opening a transaction, and retain both through commit
and inventory installation. The store also checks the durable active battle row.

`FlameLitStore.Begin` requires the ordinary player's saved identity and location,
the actual Taoist at Twin City (353,325), interaction range and a free bag slot.
The weekly run is unique per character and battle day. A replacement attempt is
allowed only when the exact current rune UID no longer exists in saved items;
storage or transfer does not count as loss. Restart increments the attempt
generation and returns to Jade Rune without deleting the earlier step receipts.

`Advance` validates the owned bound rune, saved item restrictions, current run,
expected stone, range and relative Guild War timing. Consuming the old rune,
creating its successor, recording the step and advancing the run commit in one
transaction. Replacement also works with forty occupied bag slots. The final
stone opens only in the last half-hour, and progression closes at the war's end.
Replay of an earlier UID cannot advance the current attempt.

The store does not debit currency. Existing EXP-unit rules remain unchanged.

## Post-war claims

`Claim` requires the actual Taoist, the exact current owned rune and a persisted
run with at least one lit stone. The original battle's scheduled end must have
passed. An unlit Jade Rune gives no reward and is retained. Earned partial or
complete rewards remain claimable after reconnect, in a later week or following
an interrupted/cancelled war. This is explicit Revora recovery policy. Claims
do not depend on a future battle becoming active or erase a newer week's run.

The transaction consumes the rune, persists character progression and level-up
effects, consumes any applicable recovery entitlement, creates a unique claim
receipt and marks that run claimed. One through nine stones yield that many
EXP-ball units; ten yield twelve. Each unit uses the level reached by the prior
unit, preserving the existing sequential calculation. One final reward plan
persists the combined result and applies its effects after commit. No daily
consumable EXP-ball allowance is charged. Cursed characters retain the rune;
characters at the configured level cap may complete without extra EXP.

Callers hold both the player item lock and `MentorExperience.Sync` through
transaction commit and `ApplyReward`. No active-battle handle is needed for this
post-war path. A failed or uncertain commit must use the standard quest failure
and reconnect path rather than install a speculative reward.

## Validation

The isolated server build passed. `Verify-PotencyHunt.ps1 -FlameStoreOnly`
passed 92 checks; the preceding gate-isolation batch's `-GuildSeasonOnly` passed 347
regression checks. Fixtures run
sequentially in the disposable validation schema, which the harness removes.

The Flame fixture starts the real scheduled Guild War service and tests an
ordinary non-GM character. It covers invalid/disposed/busy battle handles,
cancelled durable battles, range and trade restrictions, full bags, concurrent
issuance and replacement, saved-item mismatch, foreign ownership, suppressed
receipt rollback, ordered transitions, exact final-window boundaries and
production reload of the rune inventory. Loss is simulated by a committed item
removal. The low-level store cases assign fixture positions. The separate full
dialogue route now uses packet-driven walking, as described below; genuine-client
movement and interaction behavior remain unverified.

Claim coverage includes premature and replayed claims, concurrent final claims,
suppressed-receipt rollback of rune/EXP/completion, stale saved rune restrictions,
delayed partial claims after cancellation, full inventory, unchanged currencies
and production reload. Final rewards are compared with twelve actual legacy
`IncreaseExperience` calls across multiple levels. Partial rewards cross the
Arena admission level and are compared with the existing reward path. Level-cap
and unlit-rune cases are also checked. The mentor and legacy reincarnation cases
below exercise additional shared reward behavior specifically through Flame Lit.

## NPC, item and achievement integration

`FlameLitQuest.Initialize` runs in tournament initialization, after the Guild War
coordinator, maps and progression storage, before listeners open. It validates
actor meshes/maps and local approaches, removes the old runtime actors and places
the Taoist plus ten stones at the researched positions. It accepts the known
parked map 5001 identities, but rejects unrelated map/mesh collisions. Catalog
rows and client files are not edited. Initialization is idempotent in the fixture.

The old Npcs.cs separate-write Flame Lit block is removed. Its identities are
intercepted by the new handler even while disabled, preventing legacy fallback.
Brief dialogue offers expire after sixty seconds and are bound to the character,
class and NPC. Requests revalidate location and inventory; claim offers identify
the exact pending rune. Stone menus select the current persisted run's rune,
avoiding unrelated or old copies. Right-click uses the exact owned item instance.

Achievement 11016 commits with the tenth transition and Dragon Rune creation;
it is not awarded by EXP claims. Post-commit refresh makes it visible immediately.
A suppressed achievement write rolls back the final rune and stage for retry.
The ordinary-player fixture uses sealed 2032 dialogue choices and ItemHandler
for a complete ten-step route, reward claim and reload. It also covers unsolicited
options, stale item references, claim replay and immediate/persisted achievement.
The full dialogue route uses packet-driven walking between stones and ordinary
travel services between maps. It does not assign positions between rune steps.

Source achievement coverage is now 239/337 (98 lack a trusted producer). This
inventory does not establish playable-route or client acceptance for those entries.

## Guild gate isolation prerequisite

Reviewing the real movement path found that `Calculations.IsBreaking`, called
after both ground movement and jumps, checked regular and Super Guild War gates
regardless of the player's map. The regular Guild War jump precheck also combined
both sets of gates. A closed Super gate could reject a crossing after the regular
gate was destroyed; missing unrelated structures could cause a null reference.

The crossing guard now selects map 1038's regular gates or map 1234's Super gates.
The regular jump precheck uses only regular gates. Existing inward/outward
boundary policy and rejection behavior remain unchanged; absent gates on the
player's own battlefield fail closed. No quest privilege bypasses a gate.

Eight additional checks cover both battlefields with opposite open/closed states,
both gate directions, outward movement, unrelated maps and missing structures.
The 347 Guild War regression checks also pass. These are crossing-predicate and
service tests. The default `AdventureWalk` helper calls Entity.Move directly and
does not exercise the post-movement IsBreaking check. Its optional packet mode
now dispatches sealed GroundMovement frames through PacketHandler, checks every
resulting position and excludes currently closed gate crossings during route
planning. Flame Lit's full route opts into that mode.

## Packet-driven server journey

Follow-up (2026-10-04): the separate calculated gate budget now uses the earned
Archer's25571 minimum attack and180% Dragon gems, without fan or temporary buffs.
Production `Calculate.Rango(Entity,SobNpcSpawn,...)` returns44748 damage; the
reviewed candidate catalog has10M HP per gate, requiring224 hits. Two breaks at
one shot/second,20 minutes of travel/dialogue allowance and waiting for stone10
give a31-minute budget in the one-hour war. This does not change the journey's
supplied-damage provenance below. Enemy interference and repeated pole captures
are excluded; repair and replenish arrows before the encounter. Native timing
and live catalog equivalence remain unverified. See
[the recorded budget](research/FLAME_GATE_BUDGET.json).

The ordinary player begins beside Flame Taoist, obtains Jade Rune through sealed
dialogue and lights the nearby first stone through ItemHandler. The fixture then
walks to the installed Guild Controller and enters through its normal Guild War
dialogue. It walks to stones two and three outside the enclosure, approaches the
left gate from outside and destroys it through the normal SobNpc attack dispatcher.
Only then does it walk to the interior stones. The gate mesh is not edited to
permit passage, and no teleport to an interior flame position is used.

After stone eight, a normal Twin City scroll returns the player for stone nine.
Walking and Guild Controller dialogue take the player back to stone ten. After
earning Dragon Rune, a second scroll and packet-driven walk return the player to
Taoist for the post-war reward. EXP, consumed rune and achievement survive reload.
The path covers over a thousand individual accepted movement packets; Guild
Controller's ordinary randomized landing changes the exact step count.

The fixture supplies an ordinary guild membership, the two scrolls and controlled
gate damage. It proves route access and dispatcher behavior, not earning those
scrolls, guild creation, realistic gate kill time or combat balance. It uses a
controlled scheduled-war clock, no competing players, and the existing isolated
server assembly; it is not a running Host or genuine-client test. Eighty-five
checks passed in the initial combined store, route and reward batch.

## Mentor and legacy reincarnation reward validation

The final twelve-unit claimant now has a persisted mentor relationship loaded
through KnownPersons. Suppressing the Flame claim receipt rolls back the level
reward and both durable mentor counters, without updating either player's visible
credit. Concurrent successful claims add exactly one credit per newly earned
level. The mentor's production relationship reload preserves the resulting value.

A separate ordinary player earns a nine-stone partial rune and has a fixture-set
legacy reincarnation snapshot (former level 139 and 5,000,000,000 EXP, current
level 109 just below the restoration threshold). A failed claim preserves that
saved snapshot, the runtime recovery entry and the owned rune. Concurrent retry
consumes recovery once, and its final level, EXP and attribute points match nine
ordinary sequential EXP awards on an equivalent reference character. Production
character reload retains the reward and rejects rune replay.

The combined fixture now passes 92 checks. This validates consumption of a legacy
recovery entitlement; it does not prove earning that entitlement. Dedicated Flame
cases for first-rebirth, second-rebirth and paid-reincarnation receipts remain
open, as do genuine-client mentor display and native recovery presentation.

## Remaining work

Contested PvP/gate behavior, native walk/jump presentation and real-Host acceptance
remain separate roadmap gates. The additional rebirth receipt families noted
above still need Flame-specific validation. No live Host, production database schema or active
client was changed by this source batch.


## Owner reward upgrade: guaranteed P7 at full completion

The owner's valuable-quest-reward direction is implemented for newly admitted
runs. Admission freezes `valuable_reward=true`; migration defaults existing runs
to false so old entitlements are not silently rewritten. A full eligible claim
creates one bound P7 soul from loaded catalog IDs 800000?899999 with
PurificationLevel 7, alongside the existing reward. The selected UID is retained
in the final claim receipt. Item insertion, progression/currency effects and
completion commit together; the item is projected only after commit.

Elimination needs one free bag slot and defers the entire claim when full.
Flame Lit exchanges its consumed Dragon Rune for the soul, so no extra slot is
needed; partial claims do not grant a full-completion soul. There is no extra
CP/RVP bonus on these quests: the specified 5,000 CPs/1,000 RVPs belong to the
separately approved Magnolia pack contract.

Validation: isolated build, 195 Elimination checks and 93 Flame checks pass.
Coverage includes normal full-route claims, soul binding/catalog class, receipt
ownership, full-bag deferral, late-write rollback, concurrent/replayed claims,
saved item reload, old-contract preservation and partial-claim exclusion.
No live Host, database or active client was changed. The wider all-quest reward
upgrade remains incomplete; see [owner standard](QUEST_REWARD_STANDARD.md).

## Retained ten-stone route and interrupted-war claim (2026-10-01)

The ordinary retained Archer earns11016 through the actual active Guild War
coordinator and all ten Flame Stone NPC exchanges. The first eight stones pass;
the ninth returns to Twin City, and the tenth refuses before the last half-hour
then accepts at its exact opening. Reusing every consumed rune leaves the saved
state unchanged. The Taoist refuses the full reward before the scheduled end.

The route uses the installed Guild Transporter for admission to1038 and two
normally purchased Town Scrolls for return, costing400 silver total. NPC approach
coordinates are controlled, so this is not evidence of walking past contested
closed gates. Two owned unbound spare items (epic weapon671023 and the Power Arena
Somersault Cloud200449) move to the existing ordinary peer by decoded trade,
freeing bag space without discarding items. The bag ends at39.

The game exits abruptly after saving the earned Dragon Rune, before claiming.
Cold startup cancels the interrupted Guild War, preserves the ten-step contract,
and the Taoist still accepts the owned Dragon Rune after its original scheduled
end. The atomic claim consumes it and grants exactly one bound catalog P7 soul.
The character is already at level140: the12 EXP units produce zero extra EXP,
without changing level or existing experience. Repeated confirmations and another
full game/database restart preserve the exact soul UID, claim, achievement, prior
career and peer items. The loaded inventory contains the saved soul; a further
ordinary NPC claim changes nothing. No daily P7 source was added.

`career-flame-earn`, `career-flame-claim` and `career-flame-reload` pass against
private game SHA256
`17976314CE4445B573179FF4AC744F2581196BA7E242D2A6BE2A09C31AF713A6`.
Checkpoint: `revora_career_flame_probe` done=true / `CareerFlameSnapshot`.
The export has212 stored identities and125 absent identities, still mixed
provenance rather than a verified320-achievement career.

Fixture recovery: the first trade was correctly refused because the retained
peer had zero saved HP. Loading its stats and using normal revival allowed trade.
The initially selected Guild War date already had a cancelled durable round, so
it refused reopening and no Flame contract was issued. Continuation preserved the
completed trade and purchased scroll, using an unused Sunday (2026-11-01) rather
than deleting battle history or repeating purchases. Subsequent three phases pass.
Calendar input and NPC positions are controlled. Contested combat, natural timing,
sub-cap EXP/rebirth recovery, in-COMMIT interruption and genuine-client acceptance
remain outside this process batch. No production source/build, deployment, live
restart or client release was performed.

## Fresh ten-step earning and below-cap payout (2026-10-03)

- [x] Continue the same ordinary fresh career after its saved siege-bomb event.
  During a controlled2041-03-24 Guild War, receive the initial rune from NPC4452
  and complete all ten ordered NPC transitions. Use ordinary gatekeeper travel
  and owned town scrolls, purchasing a replacement through the silver shop when
  needed. Every transition consumes its predecessor; consumed-rune replay is inert.
- [x] The final step refuses before15:30, then succeeds after the controlled
  deadline, saving11016. Return to the issuer; an early reward request changes
  nothing. Save the fully earned Dragon Rune and exit before claiming.
- [x] After full process restart, the interrupted war is cancelled, but the
  earned rune remains redeemable after the reward time. The claim consumes it,
  saves twelve reward units and15,565,779,652 EXP, raises level130 to131 and
  delivers one bound catalog-P7 soul. Repeated claims grant nothing.
- [x] Another cold process retains the exact soul UID, resulting profile,
  run/step/claim receipts and nested prior career. Repeated requests preserve it.

Fixture `tools/CheckCandidateFreshFlame.cs` passes
`career-fresh-flame-{earn,claim,reload}` on the first run. Checkpoint:
`revora_fresh_flame_probe`, done=true. Private game SHA-256:
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
This adds a below-cap earning/payout path beyond the earlier retained max-level
check; it does not establish every rebirth-recovery variant or independently
verify original6711 EXP numerics. Clock, NPC approach and screenless sessions
are controlled. Natural travel, contested combat, native display and deployment
remain open. No production rebuild, client patch or live restart occurred.
