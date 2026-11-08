# Guild Beast daily hunt

Status: implemented in staged server source. The earned funding/kill/reward
journey now has actual game/database restart evidence below. Not deployed;
genuine6711 acceptance and an observed intermittent startup failure remain open.

## Ordinary player route

The Guild Director offers **Visit Beast Guard** to ordinary players, including
players without a guild. Travel uses a walkable tile within talking distance
of the installed BeastGuard: NPC 8150, map 1038, 56,56. Visits are refused while
Guild War is active, avoiding an entry shortcut during the war.

Before 21:00 server-local time, the latest eligible weekly winning guild's
current saved leader can pay 500,000 guild silver once for that day. Funding,
the fee and leader/result checks are transactional. Ordinary donations can fund
the purchase. No GM, owner-name, VIP or character allowlist is required.

At the first healthy world pulse from 21:00, one family-7006 Guild Beast appears
on legal terrain within the installed spawn bounds (86,78 plus 50,50). It keeps
the installed stats, including 65,535 HP. The old NPC's ten-second HollowBeast
4211 summon is removed. The static zero spawn count stays unchanged.

Everyone may hunt it until 22:00. The accepted killer earns achievement 10303
and returns to Beast Guard for one unbound Dragon Ball (1088000) plus one bound
permanent P7 soul under the [valuable kill reward policy](GUILD_BEAST_REWARDS.md). Generic random
loot is suppressed for this owned encounter. This is a documented Revora
inventory-delivery adaptation of the original dropped Dragon Ball: a full bag,
failed insert or reconnect does not lose the saved prize. A valid killer's
unclaimed reward remains available after midnight.

At 22:00 an undefeated beast is removed. The funded guild's current saved
leader or deputy may claim its one fallback Dragon Ball before midnight;
first committed claim wins. No kill achievement comes from fallback collection.
A funded day missed entirely during downtime expires without invented kills.

## Ownership and persistence

The world adapter registers the exact entity, monster information, map, UID and
saved life. Same-name/family copies, stale sessions and retired actors cannot
settle the hunt. A legitimate registered companion may credit its ordinary
owner; that source path has not received a dedicated companion fixture yet.
Runtime reinitialization rebinds a new life at full installed health; stale
callbacks cannot claim it. Generic map respawning is excluded.

Accepted kill state and achievement commit together before death projection.
Delivery inserts the item and seals its claim in the same transaction. Failed
kill evidence leaves the beast alive; failed reward insertion leaves the claim
pending. Reload can recover missing evidence from a committed killed result.
Timeout removes the runtime actor even if its database transition must retry.
The single Host is assumed; this is not a multi-Host encounter coordinator.

## Validation and limits

- Isolated Debug/x86 build passes.
- Full guild suite: 428 checks, including the ordinary director travel route,
  funded NPC purchase, real attack handler, nonlethal exclusion, wrong-template
  exclusion, fake/remote/stale sessions, copied/retired actors, busy ownership,
  runtime reinitialization, timeout/deputy fallback, repeat protection, failed
  evidence/item writes, full inventory and character/item reload.
- Player-defeat regression: 41 checks pass after the death hook change.
- Earlier funding/lifecycle primitive suite: 24 checks passed before world wiring.
- Logs: `obj/guild-beast-world-build.log`, `obj/guild-beast-world-tests.log`,
  `obj/guild-beast-defeat-regression.log`.

The native client, fresh-process crash acceptance, long database outages and
extended companion/combat timing races are not proven by these fixtures.
No live restart, database migration, client patch or deployment was performed.
Source producer inventory is 309/337, not a release-completion percentage or
proof that one character can earn all required 320 achievements.

That inventory figure and the fresh-process limitation above describe the
original component-test batch. The current roadmap inventory supersedes that
count; the following process journey extends its acceptance scope.

Original evidence, conflicting historical schedules and prerequisite findings
are in [Guild Beast research](GUILD_BEAST_RESEARCH.md). Revora explicitly uses
the installed dialogue's 21:00-22:00 window and installed NPC location.

## Earned funding and post-midnight process recovery (2026-09-27)

`tools/Verify-ArenaProcess.ps1 -GuildBeastOnly` passes five private game phases
against the production-schema disposable database. It first reuses the ordinary
Guild War capture and purse-delivery journey. The leader's saved victory and
earned purse fund the next step; no winning result or beast reward is inserted
to satisfy eligibility.

The third phase loads that leader, donates500,000 earned silver through the
ordinary owned donation service and visits Beast Guard via the Guild Director.
Two funding requests produce one funded day and one500,000 guild-silver debit.
A controlled21:00 tick creates the installed beast. A controlled attack through
the normal damage handler earns its killed result, frozen P7 type and exactly
one10303 receipt. Inventory/currencies do not change during the kill. The process
exits with the reward unclaimed, then PostgreSQL restarts.

The fourth phase advances the event clock past midnight and loads the same
ordinary leader. The Director/Guard route delivers exactly two new permanent
items: an unbound Dragon Ball and a bound catalog-P7 soul matching the saved
type and soul UID. Repeated claims add nothing. The fifth process loads and
claims again; item UID/type/binding/stack fingerprint, wallets, paid timestamp
and single achievement receipt are unchanged.

The complete rerun passes in `obj/guild-beast-process.log` (ignored), using game
SHA256 `9B72D1C220424D314A9C2E9879B98EED6C063C294C202F642CA2E1AAB1800B33`.
Guild affiliation, event clocks, movement to combat and damage are controlled;
war victory, purse funding, donation, beast kill and reward delivery use their
server earning paths. This is not authenticated TCP combat, native navigation,
natural elapsed time or live deployment.

**Unresolved startup observation:** the first sequence passed through reward
claiming but its fifth process failed during server startup with a
NullReferenceException. The runner's direct rethrow obscured the original frame.
It did not recur in the complete rerun. The initial socket-race hypothesis was
not established and no gameplay fix is claimed. The runner now preserves the
original exception stack and prints at most five method names without arguments,
paths or payloads. That diagnostic-only follow-up compiled with warnings as
errors; it has not reproduced or resolved the underlying startup failure.

Follow-up: [LivingWorld startup ownership](LIVING_WORLD_STARTUP_OWNERSHIP.md)
identifies and repairs a concurrent population race. Its focused regression
fails on the old build and passes after repair; all five phases above also pass
on the repaired private build. The lost original frame still prevents claiming
that this was conclusively the source of the earlier exception.


## Retained founded guild, war victory and Beast hunt (2026-10-01)

The `career-guild-hunt-*` phases extend the existing event tests to the retained
career's normally founded CareerBanner guild. Membership, war victory and Beast
eligibility are not supplied as fixture prerequisites in this journey.

- [x] Preparation trades one spare bag item and seven stored weapons through
  ordinary trade packets to the separate RVP test character, and deposits seven
  bound souls through warehouse packets. No earned item is deleted: the main bag
  has32 items, the Twin City warehouse remains60 and the recipient holds8 trades.
- [x] At the controlled Sunday window (2027-02-07), the Guild Controller's ordinary
  entry option admits the retained founder.3,297 normal ranged attacks capture
  the pole. The scheduled settlement records the guild's victory and an unclaimed
  leader purse. Damage, target HP and winner are not injected.
- [x] A fresh process restores the unclaimed purse. GuildGateKeeper7000 returns
  the founder to Twin City; Director10003 pays the exact saved CP/RVP/silver
  contract and six items, including a bound P7. Repeated claiming does not pay
  again. This extends the ordinary founding prerequisite, not just a supplied
  guild-affiliation fixture.
- [x] After another restart, the founder donates500,000 silver through the
  production donation service, travels through the Director to Beast Guard,
  and funds the hunt. Two requests debit500,000 guild silver once. At21:00 the
  installed beast spawns; one normal ranged attack from this maximum-equipment
  career defeats it and leaves its reward unclaimed. No balance claim is made.
- [x] A fresh process at23:00 claims exactly an unbound Dragon Ball and a bound
  catalog-P7 soul. Duplicate claims add nothing; the paid receipt stores the
  soul UID. The bag returns to40 while previously earned belongings remain owned
  or ordinarily traded/stored.

All six phases passed against private executable SHA-256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
The final reload passes both a backwards event clock (20:00) and a separate
same-23:00 cold replay, preserving the exact paid snapshot without another item.
Snapshots compare the guild row, saved items, currencies, counter, war/purse/
Beast rows, achievement receipts and the trade recipient's items across phases.

An initial2027-01-31 attempt stopped before combat when the background event
clock started the battle during its baseline comparison. A read-only diff found
only that active battle; startup later cancelled it. It remains as interrupted
history with no reward. The corrected fixture checks its baseline before15:00
and uses the next Sunday. A later travel assertion assumed a portal that is not
installed; resuming used the actual GuildGateKeeper NPC without repeating combat.
These were fixture corrections, not claimed gameplay fixes.

Clock, NPC/combat proximity and attack cooldown are controlled. Guild creation,
payment, entry, attack calculation, capture, settlement, donation, funding and
reward delivery use ordinary server paths. This is an unopposed synthetic
server journey, not native navigation, opposing-player combat or natural elapsed
time. No production build change, live restart or deployment was needed.

The refreshed retained inventory has231 stored identities and106 absent, including
four new explicit receipts:10228 (war purse),10232 (pole hit),10302 (occupation)
and10303 (Beast victory).10218/10219 also appeared as observed-state receipts;
those are recorded as such rather than counted as independently proven event
rewards. The conservative320-candidate plan now has230 present and91 absent
identities with mixed provenance. Full compatible-career/native acceptance stays
open.

## Fresh-founded guild war and Beast earning (2026-10-03)

The ordinary FreshRoadmap1 career continues from its five saved clan territories,
using its paid FreshBanner guild (51102). All six private phases passed on the
first run: `career-fresh-guild-war-war`, `career-fresh-guild-war-reload`, and
`career-fresh-guild-hunt-{purse,hunt,prize,reload}`.

- [x] Enter Guild War through the installed NPC on a controlled Sunday,
  2041-03-10. Capture the real pole in6,271 calculated melee attacks, then
  settle through the production event. The saved winner, contributor capture,
  unclaimed leader purse and10302 survive a separate process. Restarting or
  settling the same completed battle does not create another entitlement.
- [x] Store eight existing possessions through the ordinary Ape City warehouse
  packet, retaining ownership. Two Director claim requests pay the saved purse
  once:8,000 CPs,100 RVPs,2,000,000 silver and six items including one bound P7.
  Exact saved belongings and balances survive the next process.
- [x] Donate500,000 earned silver through the production donation service.
  Enter through the Director and ask Beast Guard twice to fund the hunt;
  guild silver decreases500,000 only once. Advance the private clock to21:00
  and defeat the spawned Guild Beast in65 calculated melee attacks. Its killed,
  unclaimed entitlement survives another process.
- [x] Claim at23:00 through Beast Guard: one unbound Dragon Ball and one bound
  catalog-P7 soul. Duplicate requests and final cold replay add nothing.
  Final bag40,351,905 CPs and929 RVPs are preserved with the nested prior career,
  guild, clan territories, purse, Beast row and item ownership.

Fixtures: `tools/CheckCandidateFreshGuildWar.cs` and
`tools/CheckCandidateFreshGuildHunt.cs`. The composed private game SHA-256 stays
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
No production source rebuild, live restart, deployment or client patch occurred.

Clock, NPC/combat proximity and attack cooldown are controlled. This proves an
unopposed handler-level earning and recovery path, not contested PvP, natural
calendar timing, native navigation, live client updates or combat balance.
The earlier dated compatibility audit remains historical; these results do not
establish the full compatible320 career. Other event earning and native gates
remain open.
