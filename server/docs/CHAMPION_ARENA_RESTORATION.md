# Champion's Arena restoration and milestone505

2026-10-02. Registration, owned entry, five-round scheduling, atomic results,
disconnect retention, captured-outcome restart recovery, equipment caps, direct
damage ownership and ordinary NPC controls have managed validation. Milestone505
is enabled after confirmed paired entry, with ordinary claim, consumption and
cold persistence validated. Later progression restrictions use documented Revora rules.
Native acceptance and live deployment remain pending.

## Historical evidence and Revora scope

The [2013 event guide](https://co.99.com/guide/event/2013/champions-arena.shtml)
requires level70+, weekday registration20:55-20:59, five rounds beginning21:00
at five-minute intervals,10 seconds of preparation and three-minute fights.
It describes same-grade pairing, seven equipment/stat tiers, points for winners
and losers, and no Apothecary subclass restriction.

The [2015 Kingdom War notice](https://co.99.com/guide/event/2015/conquer3.0/kingdomwar/guide/1/)
explicitly cancels Champion's Arena and moves Champion Point earning elsewhere.
An old client task or legacy packet serializer is therefore not evidence that
this was a normally available event in the later client. Revival must be clearly
documented as a Revora restoration, not claimed an unchanged6711-era service.

The installed stagegoal entry505 requires one participation
(task26), awards one bound EXP Ball723700 and belongs to the Famous stage.
The [Way of Hero guide](https://co.99.com/guide/guides/hero_system.shtml) also
lists participation and a bound EXP Ball. Registration alone will not qualify:
Revora must save confirmed arrival in a legitimate paired match.

## Historical code problems

These are the initial findings; the dated validation sections below record fixes.

- Fixed in the registration prerequisite below: DoSignup previously admitted
  weekends at any hour and omitted level70. Thread's legacy announcement still
  only targets Monday/Friday; full scheduler/control integration remains pending.
- Fixed in the owned-map prerequisite below: Import previously accepted equal
  final map IDs, including shared fallback maps. It now requires the assigned
  map object. Admission still has no durable reservation/receipt.
- Legacy timeout/refusal paths award through Win; several quit/cleanup paths
  refer to QualifierGroup instead of ChampionGroup. Awaiting/match registries,
  repeated callbacks, disconnects and restart require explicit ownership.
- Win mutates both players' points, streak and grade in memory; later saves are
  separate. The old Champion shop adds an item before subtracting points.
  These paths cannot establish atomic reward/payment or restart recovery.
- The daily reset uses DayOfYear and its same-day test can re-arm a reset in the
  same evening. Round count/time enforcement is incomplete.
- Handle reads the2604 action without a local frame/identity validation contract.
  The serializer is managed evidence only. A native control surface cannot be
  promised from that code; validated NPC controls are a possible Revora entry path.

## Completed stat prerequisite

Champion grades1-7 indexed the existing seven-row zero-based table directly.
Every lower grade therefore selected the next row and grade7 could go out of
bounds. A central ChampionLimits accessor now maps grade to grade-1 and uses
row1's restrictive policy for an invalid grade. All existing cap readers,
including Battle Power nobility contribution, use that accessor. Apothecary
retains its full subclass effect; other subclasses still use the selected cap.

`tools/CheckCandidateChampionCaps.cs` uses two ordinary supplied characters and
a synthetic group reference solely to activate the real stat-calculation path.
It is deliberately not an admission, match, victory or earning fixture.

- [x] Real equipped enchanted boots produce the expected HP cap at every grade:
 0/0/50/100/150/200/255. Grade7 no longer indexes past the table.
- [x] Calculated Battle Power clamps the nobility contribution correctly at
  every grade; leaving the synthetic group restores normal HP and Battle Power.
- [x] Supplied phase9 ChiMaster follows grade caps; supplied phase9 Apothecary
  retains72 detoxication at every grade. Invalid grades0/255 use restrictive
  limits without crashing while preserving the Apothecary exception.
- [x] The stat fixture cannot claim505. No earning receipt or enabled stage was
  added. The ordinary-player account is not a GM and no runtime allowlist exists.

The first harness compile used ushort for the int BattlePower property; that
fixture type was corrected before running the game. The final process passed on
`2BF0E12C7EBF9B4D61CE4B88CBF7E5071B40766D676673AF122622207C5D1969`.
The production overlay build retains existing legacy warnings; the composed
harness compiles with warnings as errors. Native stat/UI display is pending.

## Implementation sequence

1. Establish one weekday schedule and canonical admission state machine. Cover
   level, competing-event, booth, jail, disconnect, stale-session and invitation
   eligibility. Provide ordinary-player controls using confirmed interaction
   paths; do not enable an unconfirmed native packet family as a workaround.
2. Own each round, pair, participant lease and dynamic map. Persist pending and
   confirmed arrival with rollback/cleanup. Only confirmed paired arrival earns
   participation; refused invites, waiting queues and failed teleports do not.
3. Validate every published stat restriction through actual combat calculations,
   including Plus, item levels, gems, Chi, refinery/artifacts and subclasses.
   The lookup fix above does not prove full normalization or post-2013 systems'
   treatment. Document Revora policies for later gameplay additions.
4. Save each result and both players' grade/point/streak changes atomically.
   Snapshot rules for the round; define ties, disconnect, forfeits and restart
   recovery. Confirm threshold boundaries and the relationship between grade
   points and spendable points before replacing legacy counters.
5. Guard generic Champion saves and make its active shop purchases atomic with
   owned currency preimages and usable bound rewards. A safe event must not feed
   an unsafe consumer. Preserve/refuse unreconciled legacy history explicitly.
6. Validate ordinary registration, pairing, confirmed arrival, combat/result,
   cleanup and cold restart. Then enable505, consume its bound EXP Ball once,
   and verify duplicate claims and XP/points persist. Main-stage aggregate prizes
   and native/live gates remain separate.

This is available implementation work, not an external-infrastructure blocker.
The cancellation evidence changes the restoration's description and proof needed;
it does not mark the roadmap requirement complete or authorize a live release.

## Registration prerequisite (2026-10-02)

- [x] Signup now requires Monday-Friday20:55:00 through20:59:59 server-local
  time, level70+, canonical loaded living session, matching account/character,
  available arena statistics, no competing qualifier/group or pending opponent,
  no booth/trade, and no jail1081 or previously excluded event maps.
- [x] Signup serializes Champion queue transitions under QueueSync and uses TryAdd.
  Existing invitations/queue entries are refused, never removed or overwritten
  to make a new signup succeed. This is not yet a global tournament lease.
- [x] The retained ordinary-player Champion fixture rejects20:54:59,21:00,
  Saturday/Sunday, level69 and fake actors; preserves conflicting queue and
  invitation owners; eight concurrent valid calls produce one owned signup.
  Existing seven-grade stat checks and milestone505 refusal still pass.

Build and private candidate phase career-champion-caps passed with SHA256
`4A57997C610D0436AE832A20BE210F2E13E15C36A953E86DAE7E9F2E138D1757`.
No new packet family or event activation was added. These guards do not complete
step1: cross-event leases, safe queued cancellation, scheduler/announcement
alignment, invitation ownership and confirmed player controls remain unfinished.
Signup is transient and grants no participation or currency. Durable paired
arrival, atomic results/shop, recovery and native acceptance remain required.

## Pending invitation ownership (2026-10-02)

- [x] Signup, pairing, acceptance and pending cancellation share a short queue
  ownership lock. Same-grade pairing records reciprocal opponent references and
  exact session owners before issuing invitations. Teleport/stat work runs after
  releasing this lock to avoid nesting player locks inside the queue lock.
- [x] Pending quit/give-up removes only owned entries, clears signup, and never
  teleports or calls Win. Decline/expiry gives no points because no match began.
  An eligible reciprocal opponent returns to the queue. This is a documented
  Revora rule; historical withdrawal rewards are not used for unentered matches.
- [x] Accept requires an owned reciprocal invitation and current eligibility.
  Verification rechecks both players before allocating a group. Stale queue
  cleanup cannot evict a replacement owner. Unsolicited accept is inert.
- [x] Champion Clear no longer clears QualifierGroup/TeamQualifierGroup or their
  active ArenaState. Active-match quit leaves return teleport to Export. Export
  clears only its own ChampionGroup references. Win rejects null/nonreciprocal
  opponents; durable result settlement remains unfinished.
- [x] The ordinary retained candidate fixture covers signup/repeated quit without
  movement, pairing/accept/decline, expired invitation with unchanged points,
  replacement queue preservation, unsolicited acceptance, null reward input, and
  unrelated qualifier-reference/state preservation. Seven-grade stat checks and
  refusal of milestone505 also pass.

Final candidate SHA256:
`EF684610B2061CA3399490E7FEA1D19EE9CD19B82E42318FDC36EF6EA6BC09A2`.
The unrelated qualifier test supplies an uninitialized reference only to test
preservation; it does not claim another arena's admission or gameplay. Pending
registration/invitations are transient, with no durable reward changes to reload.

Full match leases, cross-event races, exact scheduled rounds, intended-map proof,
failed-import recovery, atomic result/shop persistence and restart recovery are
still open. The legacy active-match engine is not validated by these pending
queue tests. Milestone505 remains disabled; no native/live release is claimed.

## Owned-map entry and rollback prerequisite (2026-10-02)

- [x] Import takes a per-match lock plus ordered nonblocking session leases and
  character locks. It rechecks canonical living participants, reciprocal accepted
  invitation, account/stat identity, same grade and competing qualifiers. A busy
  import can retry on the event pulse; successful import cannot transfer twice.
- [x] Both players must occupy the exact canonical dynamic arena assigned to this
  registered group. Shared fallback maps do not count. Entry snapshots original
  maps, coordinates and PK modes; partial failure returns moved owners and gives
  no winner or points. A quit during transfer also aborts entry.
- [x] Export returns participants before disposing the map, preserves later group
  references, restores PK modes/stat calculations, and is idempotent. Damage only
  accepts this group's participants on its owned map. Pre-entry End has no winner.
- [x] Ordinary-player fixture follows signup, invitation, acceptance, real managed
  teleport, exact map ownership, duplicate Import, End/Export and duplicate Export.
  It verifies original coordinates/modes and map removal. Injected failure after
  first arrival, shared fallback after both transfers, and mid-transfer quit all
  leave no group/map leak or points. Existing caps/queue tests still pass.

The first fixture run used static reflection flags for an instance property and
failed after entry; corrected to instance flags, retaining the private database.
The final game build and fixture passed with SHA256
`D4A6B262A05580FAB9BE96ED20D92BEFADEA92F57BBB71FD6C09DD3B9C797988`.
Subsequent source changes only normalize indentation of lifecycle lock bodies.

EntryConfirmed is deliberately an in-memory condition, not a durable earning
receipt. The fixture manually exports the successful match and does not prove
result payout. Admission reservation, paired-arrival persistence, crash recovery,
reconnect/disconnect cleanup, atomic result/shop settlement and full combat caps
remain open. Failed rollback fences the owners and keeps the group/map for future
recovery rather than disposing an occupied map. Milestone505 remains disabled;
no native acceptance, deployment, or completed event claim is made.

## Durable entry and cold recovery (2026-10-02)

- [x] ChampionEntryLedger reserves one match and two owned participants before
  either teleport. Reservation takes entity row locks under session/character
  locks and refuses pending solo/team results. A partial unique index allows
  only one unreleased Champion entry per character.
- [x] Assigned map and original map/coordinates are saved with a match GUID.
  Both arrivals are confirmed in a separate transaction only after the managed
  ownership checks pass. Return/abort closes the entry and releases participants
  together; interrupted rows remain as audit evidence.
- [x] Cold startup, before listeners and character loading, returns characters
  still saved on the assigned arena to their recorded catalog map/coordinates.
  It preserves a different saved map. Unavailable/noncatalog destinations use
  Twin City400,400. Recovery marks entries interrupted and releases reservations
  transactionally without points or prizes; it refuses more than4096 pending
  participant rows rather than partially preparing startup.
- [x] Loaded Champion SignedUp is always false: old persisted signup cannot
  recreate missing transient invitations or prevent registration after restart.
- [x] Candidate career-champion-entry-partial exits the process without shutdown
  after the first real transfer. A new process verifies interrupted state,
  absent arrival timestamp, both released participants and original locations.
- [x] Candidate career-champion-entry-confirmed exits without shutdown after
  actual paired entry. Reload verifies the retained arrival timestamp, released
  participants and restored locations. Milestone505 still refuses a claim.
  The regular caps/signup/entry/rollback phase also passes on this build.

Validation game SHA256:
`F5FF3E961139264A3F3EAB18FAA77EBF427190DD8682FC72896A992B0A3126AA`.
The retained private database was not reset. The initial recovery implementation
incorrectly checked map objects, then terrain paths, before their startup load;
those attempts used safe fallback coordinates and failed exact-return assertions.
Recovery now checks MapsTable's already-loaded catalog. Both crash/reload cases
passed after correction; earlier interrupted receipts remain retained.

These records prove managed entry/recovery, not native arrival acknowledgement
or result payout. They do not yet enable505 or constitute cross-event admission
exclusion in both directions. Remaining work includes result/day settlement,
Champion shop payment ownership, live disconnect/reconnect cleanup, schedule,
full combat caps and ordinary confirmed controls. No live deployment occurred.

## Champion save ownership prerequisite (2026-10-02)

- [x] Champion rows now have a revision loaded with the point balance. Generic
  saves require both the loaded revision and nullable point preimage, increment
  the revision, and reject stale or invalid statistic objects. SQL values are
  parameters; save failures invalidate that object and fence its current owner.
- [x] Reconnect reloads invalid cache entries from the saved Champion row. A late
  save from the old object cannot fence the replacement session or overwrite it.
  Missing-row initialization uses INSERT ON CONFLICT DO NOTHING followed by read,
  so login cannot replace existing points with a new zero-balance statistic.
- [x] The duplicated UID-zero repair branch that inserted an Arena row has been
  replaced with this Champion loader. Daily load checks compare full dates.
- [x] Generic saves now include TotalMatches. Statistic reset and legacy match
  counter mutations synchronize with statistic saves. This is not atomic match
  reward settlement; the legacy shop still requires replacement.
- [x] Ordinary supplied fixture4294913801 saves47 points,9 matches and a2-win
  streak. A deliberate database revision/point change to57 makes the old save
  fail; reconnect obtains57, and another old-object save leaves the new session
  unfenced. Cold reload preserves57/9/2. These supplied points are not earning
  evidence. The existing caps/signup/entry/rollback fixture also passes.

Private phases career-champion-balance-earn, career-champion-balance-reload and
career-champion-caps passed with game SHA256
`9E9CE391E85206E03652C7D19624CBC30DB1FC84AA772057A028CE55E24D2532`.
No fixture/database reset was used. Remaining currency work is atomic paired
result settlement, date/round ownership, guarded shop debit/item creation, and
post-commit failure recovery. Milestone505 stays disabled and no live deployment
or native acceptance is claimed.

## Atomic Champion stone purchases (2026-10-02)

- [x] Champion shop6003 now routes before generic +Stone/Exemption shop handling.
  Sealed1009 action1, bounded frame length and one-item quantity are required.
  Normal loaded living owners can buy without GM/owner allowlists; trade, booth,
  pending Champion signup and active Champion matches prevent purchases.
- [x] The loaded server catalog sets point prices. +1/+2/+3 stones are bound with
  matching persisted Plus, durability and stack1. Point debit/revision, item row
  and receipt commit together under entity/session/statistic ownership checks.
  Bag UID sets must match persistence. The client balance and item install follow
  commit; failed installs invalidate stale stats and require reconnect.
- [x] Same-owner/type requests within one second are deduplicated by a durable
  receipt. This is a double-click guard, not a claim of unbounded packet replay
  identification. Longer-separated requests are legitimate new purchases.
- [x] Ordinary supplied fixtures4294913901-3904 buy all three stones through the
  normal ItemUsage.Process packet path and consume them through actual bound
  equipment composition. Eight concurrent requests per stone charge once.
  Precommit failure rolls back, reconnect/retry succeeds, and postcommit install
  failure cannot be overwritten by a stale save. Reload restores one item/debit.
  Insufficient points and unsupported packs make no economic changes.
- [x] Exact point/item/purchase snapshots survive a cold process; the prior balance
  recovery fixture still passes. Fixtures use supplied points, not match earnings.

The initial normal-path test exposed an earlier ExemptionTokenShop interceptor;
no points were charged. Routing was corrected in ItemUsage as well as ItemHandler
and the retained fixtures resumed without erasing their supplied equipment.
Final phases career-champion-shop-earn, career-champion-shop-reload and
career-champion-balance-reload passed with SHA256
`6F624B7069F71D5E3A750FE26EA070B8BF26B7DC7F329D0B30A74E51783CD158`.

Seven catalog packs still lack item-use consumers:3001046 and3001060-3001065.
They remain visible legacy stock but are refused without charge until their
reward paths are implemented. Therefore the whole Champion shop is not complete.
Match payouts/day/round settlement, these consumers, cross-event exclusion,
combat caps and native acceptance remain open. Milestone505 stays disabled;
owner work in the two packet files was preserved and no live deployment occurred.

## Champion steed pack consumers (2026-10-02)

Primary local evidence: installed6711 ini/itemtype.dat SHA256
`ab428fe7d3932d98ead2ece8cdc20c548ee909723792fb4de955df7dddfa7753`
identifies3001060-3001062 as +1 Maroon/White/Black Steed packs and3001063-3001065
as their +3 counterparts. All six descriptions specify a bound steed.
Installed ini/mounttype.dat SHA256
`2ca50bbbc750ffb1d42563536add1a61f0ce1af26b0f91bd288e3db10cb098b1`
provides color triples255/150/0,0/255/150 and150/0/255 respectively. The existing
local data transform was read and applied without executing its publishing
script; full inverse roundtrip matched. Decoded mount catalog SHA256 is
`1c33b9866b88fb878e3207fd677ad917c5b83dfe940275b2b4f41719d500bbfe`.
Decoded assets remain ignored local evidence and are not committed.

- [x] All six packs are purchasable through the atomic Champion shop at its
  existing loaded prices15/180 points. The same purchase guard protects them.
- [x] A sealed item-use request consumes an owned, unlocked permanent bag pack
  and creates one bound300000 steed at the specified Plus/color in one transaction.
  Horse effect, visible color and preview color persist together. Partial stacks
  require room for the new mount; a last pack can free its own bag slot.
- [x] A durable one-second opening guard limits duplicate clicks on stacked packs.
  Ownership preimages, exact bag membership, trade/session checks and postcommit
  fencing protect consumption and installation. Bound output is unconditional,
  including legacy unbound input packs, matching the installed descriptions.
- [x] Ordinary fixtures4294914001-4003 buy/open all six types through normal
  requests, check colors/Plus/binding, and test eight concurrent opening requests.
  Purchased +1 and +3 steeds perform real equipped-parent breeding: result +3,
  progress10, original color, bound; consumed parent cannot be reused.
- [x] Precommit failure preserves the pack; reconnect/retry opens it once.
  Postcommit installation failure reloads the single saved steed without its old
  pack. Exact purchase/opening/breeding/item snapshots survive a cold process.

The first breeding assertion mistakenly expected20 residual progress;120 minus
20 and90 is10. Server output was correct. The fixture was corrected and resumed
from its retained six opening receipts and one breeding receipt, without replaying
purchases or erasing records. career-champion-steed-earn, -reload and
career-champion-shop-reload passed with game SHA256
`2DA4916E55C57FFB10067760BADF1B6EC61FB657B36DA2102B8BF8CD65880AA9`.
Subsequent production-source edit only updates the remaining-stock comment.

Meditation Pack3001046 remains refused without charge pending its random reward
consumer. Nine of ten loaded Champion shop products now have managed usable
paths. Native steed rendering/preview acceptance remains pending; static catalog
and managed persistence tests do not substitute for it. Atomic match results,
schedule, full combat caps and other restoration gates still keep505 disabled.
No client asset was patched or released, and no live deployment occurred.

### Meditation prerequisite audit (2026-10-02)

Status: research complete for the advertised reward types; consumer and attribute
preservation are **not implemented or validated**. This does not enable505 or
increase the completed shop count beyond9/10.

Primary evidence:

- The [2013 Champion guide](https://co.99.com/guide/event/2013/champions-arena.shtml)
  advertises an800-point bound pack with one of three outcomes: Mystery Fruit,
  1,000 Chi, or3,000 Study. Fruit gives1-3 attribute points; Mystery Dew guarantees3.
  It does not publish outcome odds or explain allocation/preservation.
- The [2015 Mystery Fruit guide](https://co.99.com/guide/quest/2015/mystery_fruit_banquet.shtml)
  explicitly gives a900-point attribute maximum and the Dew guarantee. Its level80
  requirement is for the banquet, not verified eligibility for using a fruit.
- The [2018 fruit guide](https://co.99.com/news/2018-07-18/mystery_fruit_banquet_on_07/19-07/31.shtml)
  explicitly describes random allocation among Strength, Agility, Vitality and
  Spirit and repeats the900-point maximum. This is later primary evidence, not
  proof of the exact6711 implementation or its random distribution.
- The active disposable server catalog, `database/itemtype-new.txt`, identifies
  Fruit3001044, Dew3001045 and Meditation3001046 with matching reward descriptions.
  Catalog text alone does not establish executable behavior.

The code audit changes the implementation order:

1. `CharacterProgression` already includes all four attributes and unallocated
   points in its guarded durable profile. Fruit consumption must use its
   transaction/projection path, not an independent update of `entities.atributes`.
2. `ExperienceRewardPlan.AddSteps` replaces all four assigned attributes with
   class defaults on unreincarnated level-ups through120. A fruit directly added
   to an attribute would be lost at the next such level-up.
3. `FirstRebirth` and `SecondRebirth` call planners which reconstruct fixed
   attribute budgets without reading consumable bonuses. Both would erase fruit
   gains. `ReincarnationRules.Profile` instead carries the current total minus
   level-derived points; adding a separate bonus again there would duplicate it.
4. Consequently, enabling the pack first would sell a reward whose gains can
   disappear. Preserve attributed bonus ownership across these transitions before
   enabling sales. Do not infer a historical fruit ledger from arbitrary existing
   character totals or restrict the reward to second-reborn characters merely to
   avoid handling the transitions.

Implementation contract under the owner's documented-Revora-rules approval:

- Use explicit Revora odds where originals remain unpublished: equal probability
  for each pack outcome, and equal probability for1/2/3 points without Dew. Select
  one of the four attributes uniformly for the fruit award. These distributions
  are Revora rules, not claimed original odds.
- Interpret900 as the sum of four core attributes plus unallocated points for
  fruit eligibility. Never clamp an existing character down to900. Preflight room
  for the maximum three-point award before consumption, avoiding paid partial
  rewards and cap-dependent rerolls. This boundary policy is a Revora definition.
- Persist a bounded, owned consumable-bonus record with fruit consumption and
  progression in one transaction. Preserve its budget through normal leveling,
  first/second rebirth, stat reallocation and paid reincarnation exactly once.
  Rebirth may redistribute the bonus; it must not destroy or duplicate it.
- Offer normal use and explicit use with owned Dew; do not silently spend CPs or
  consume Dew. Dew and Fruit must commit together when the player chooses Dew.
- Pack opening must preflight every outcome (bag room and Chi/Study storage),
  select once, and atomically consume the pack and save the selected reward plus
  receipt. A reconnect must recover the committed outcome without rerolling it.
- Require ordinary-player tests of all outcomes, malformed/repeated/concurrent
  requests, full bag, cap/overflow refusal, precommit rollback, postcommit recovery,
  cold reload, and preservation across every attribute-changing transition above.

No production code, native protocol, public service or live data was changed by
this audit. The next implementation batch is bonus preservation plus Fruit/Dew
consumption, followed by Meditation opening and shop activation in the same
validated reward chain.

### Saved attribute-bonus foundation (2026-10-02)

Implemented a bounded `entities.revora_attribute_bonus` counter, defaulting to0.
It participates in the complete `CharacterProgression` saved-image predicate and
transactional projection. Existing characters receive no inferred bonus; existing
thirteen-field planner constructors explicitly default the added field to0.

First/second rebirth transactions now carry that counter into their new attribute
budgets exactly once. Paid reincarnation carries the counter but does not add the
points again because its existing total already includes them. Normal experience,
Soul Trial, Courier, Path Hunt and Master Peak plans preserve a recorded bonus
when replacing first-life default stats. Under this Revora rule, automatic default
allocation can move the extra points to the free allocation pool. It preserves
the total rather than guaranteeing the previously selected attribute distribution.

Validation: the legacy game build passed; the composed managed harness compiled
with warnings as errors. `career-attribute-bonus-earn`,
`career-attribute-bonus-reload` and `career-champion-shop-reload` passed with game
SHA256 `0E30BBA52893740E2E8AD8E70CBAD1A79936D45677EEDBC1E6F0CD2EE5197EFF`.
Ordinary fixture4294914101 received **supplied**, explicitly recorded three bonus
points. The fixture verifies a multi-level80-to82 plan, first/second rebirth and
paid reincarnation planners, duplicate bonus rejection, bounds, transaction
rollback, committed projection, ordinary save and exact cold-reload profile.
The initial harness compile attempted direct access to an internal stat helper;
the fixture was corrected to use reflection before any fixture was executed.

This is foundation coverage, **not** evidence of a fruit being earned or consumed,
nor a full NPC-driven rebirth test with consumed fruit. Remaining integration
includes Fruit/Dew receipts, explicit Dew choice, pack outcomes, stale-writer and
concurrency tests, and actual ordinary-player transitions. Legacy `GetStats`
callers (Welcome Guide, Revora Master, and old reincarnation paths) also require
reachability review before enabling fruit. Shop availability remains9/10 and505
remains disabled. No deployment or native acceptance was performed.

### Fruit and Dew consumer (2026-10-02)

- Normal1009 item use now handles Mystery Fruit3001044 and Mystery Dew3001045.
  Using Fruit consumes only Fruit and awards1-3 points to one randomly selected
  core attribute. Using Dew explicitly pairs it with the lowest-UID usable owned
  Fruit and awards3. No CP fallback or automatic Dew consumption is present.
- Both items, the guarded progression profile and a durable use receipt commit
  together under the owner lock/session lease. Unowned, locked, expiring or
  otherwise unusable items cannot fund the award. Stored bag membership and item
  preimages must match. The receipt records both item identities/stacks, selected
  attribute, amount and bonus-counter transition.
- The documented Revora maximum-reward preflight refuses consumption above897
  total core-plus-free points. A durable one-second guard prevents concurrent
  clicks consuming multiple units of the same Fruit stack. RNG is uniform by
  rejection sampling; the fixture injects deterministic outcomes.
- `DataHolder.GetStats`, including the Welcome Guide's older level-raising path,
  now uses the same budget-preserving base-stat assignment under the owner lock.
  The unused legacy Revora Master `RaiseLevel` helper also goes through this
  shared entry point. The active paid reincarnation/rebirth services already use
  guarded profiles; the old `Reincarnation.Handle.Hash` delegates recovery to
  `ReincarnationRecovery`. The legacy `GameState.Reborn` call found in Chat is an
  administrative path, not the normal rite restored by this roadmap.

Validation: game build passed and the composed harness compiled with warnings as
errors. `career-mystery-fruit-earn`, `career-mystery-fruit-reload` and
`career-attribute-bonus-reload` passed with game SHA256
`1FC99AF20F729D5A999BD0B328F2C9EF347C4B1010C4FE6DE6D1E4A1A8FEE8AA`.
Ordinary fixtures4294914201-4204 exercise the normal item-use dispatcher with
**supplied items**: all three Fruit amounts, leaving owned Dew untouched on plain
Fruit use, explicit Dew pairing, Dew without Fruit, consumed-UID replay, eight
concurrent stacked-Fruit clicks, legacy default-stat redistribution, cap refusal,
precommit rollback/retry and postcommit failure/reconnect. Fenced stale saves are
refused. Exact attribute/item/receipt snapshots survive cold restart.

Still pending: Meditation opening and activation, malformed/stale-profile edge
fixtures, full ordinary NPC-driven life transitions after fruit use, and native
client confirmation of item clicks/stat updates. Existing pure transition tests
are not a replacement for those integrated tests. No new quest or shop completion
is claimed by this consumer batch; the Champion shop remains9/10. No live server
or client files were deployed.

### Meditation activation and projected attribute cap (2026-10-02)

The earlier current-total-only Fruit preflight allowed a reincarnated character
to eat more Fruit at a low level, then regain ordinary level points above900.
That check is superseded: Fruit capacity now reserves ordinary points through the
configured level cap and, before second rebirth, the remaining possible rebirth
bonuses. For first-reborn characters the earned first-life bonus is taken from
their recorded history. Second-reborn characters use their actual current total
plus future level points, so missed historical bonuses can still be made up.
The projection is an explicit Revora anti-repeat rule, not a newly verified
original-client formula. Existing characters above the limit are not reduced.

- [x] Fruit cap fixtures4294914301-4304 cover a level15 second-reborn character
  already projected to900, first-life capacity at898, exactly897 followed by a
  three-point award, and unchanged projected capacity through first/second/paid
  rebirth planners. Malformed consumer frames leave items untouched. A changed
  saved attribute profile rolls back both Fruit/Dew consumption and fences the
  stale cache. Exact snapshots survive cold reload.
- [x] Meditation3001046 is purchasable for the existing800 Champion Points and
  produces one bound Fruit,1,000 Chi or3,000 Study Points with documented equal
  Revora odds. Purchase/opening require capacity for **every** possible outcome,
  including projected Fruit capacity, without rerolling an unavailable outcome.
  A last pack can free its own bag slot; a partial stack needs a free slot.
- [x] Pack consumption, selected reward, guarded progression/wallet preimages and
  opening receipt commit together. A durable one-second guard limits repeated
  clicks on the same stack. Postcommit failure requires reload; the outcome is
  never rolled again. Chi/Study outcomes send the existing client update packets.
- [x] Ordinary fixtures4294914401-4404 buy and open through normal1009 dispatch,
  exercise all three outcomes, consume the resulting bound Fruit, verify exact
  Champion costs, concurrent requests and consumed-UID replay. Precommit rollback
  leaves the purchased pack; postcommit failure reloads its single reward.
  Malformed frames and Study/Chi overflow refuse without consumption. A failed
  capacity check also leaves the Champion purchase balance unchanged.

Fruit-cap earn/reload and existing Fruit reload passed with game SHA256
`D3F1DABC0CBD6E302DDBE88B974964F3D7FA3A1E970D00CBE14C6B3CBFBEF943`.
After Meditation integration, `career-meditation-earn`,
`career-meditation-reload`, `career-fruit-cap-reload` and
`career-champion-shop-reload` passed with game SHA256
`8CA4E055112A0D23641629B6C60C06DF6EBBFEC3A39D8DC7282CCAC259208C70`.
The first Meditation fixture omitted the normal Chi loader; the purchase correctly
refused an unloaded wallet. The fixture added `ChiTable.Load` and resumed with the
uncharged retained character. No failed receipts or fixture rows were deleted.

All **10/10 loaded products now have managed purchase/use paths**. This is not
completion of the Champion event or native acceptance. Full NPC-driven rebirth
after earned Fruit, remaining integration edges, round timing/combat caps,
durable match settlement and client validation still remain;505 stays disabled.
No live deployment or client release occurred.

### Purchased Fruit through ordinary rebirth services (2026-10-02)

- [x] Ordinary fixture4294914501 purchases Meditation for800 Champion Points,
  opens its bound Fruit outcome, and consumes that Fruit with explicitly used
  Dew for3 points. It then uses Sereth's actual first-rebirth dialogue and payment,
  restores levels through `GameState.IncreaseExperience`, uses Sereth's actual
  Exemption-funded second rite, restores levels again, buys Oblivion Dew and pays
  for reincarnation through its normal NPC/1066 path.
- [x] The three bonus points persist exactly once: total attributes are130 at
  first-reborn level15,475 at level130,195 at second-reborn level15,540 at level130,
  and195 after paid reincarnation. The saved bonus counter stays3 throughout.
  Repeated completion/paid requests leave the saved result unchanged.
- [x] Each life phase starts in a separate server process and checks the previous
  exact reward/item/wallet/life/receipt snapshot. Final reload also matches. One
  Meditation opening and one Fruit consumption remain the sole source receipts.

`career-fruit-life-first`, `career-fruit-life-second`, `career-fruit-life-paid`
and `career-fruit-life-reload` passed against the unchanged game SHA256
`8CA4E055112A0D23641629B6C60C06DF6EBBFEC3A39D8DC7282CCAC259208C70`.
The harness compiled with warnings as errors; no new game build was necessary.
Champion funds, rite materials, XP and promotion eligibility were supplied fixture
inputs. This proves the ordinary reward/progression/service integration, not that
those inputs were earned through hunting, crafting or promotion quests in this
test. Native acceptance and live deployment remain pending. The next Champion
work is combat timing/ownership, round scheduling and durable settlement;505 is
still disabled.

### Combat preparation and pair ownership (2026-10-02)

- [x] Champion uses one injected monotonic combat clock for entry, preparation,
  combat deadline, completion and cleanup delay. Unsigned elapsed differences
  survive the signed `Time32` counter boundary. This does not fix unrelated
  queue or server timers.
- [x] Preparation lasts10 seconds; combat is open on `[10s,190s)` after confirmed
  arrival. Interpreting the three minutes as combat time after preparation is
  the documented Revora scheduling rule. The former12-second attack check and
  three-minute-from-entry timeout no longer disagree.
- [x] The common entity attack filter checks both owners against the same
  confirmed pair, canonical sessions and assigned map. Target-only participation,
  forged third-party membership, session replacement and detached participants
  refuse combat. Champion participants cannot attack noncombat NPC targets.
  Score updates outside the active combat window are ignored.
- [x] Fruit and Meditation cannot be consumed while a Champion group is attached;
  the ordinary `InQualifier()` helper covers solo/team groups only, so an explicit
  Champion guard was needed to prevent mid-match attribute changes.

The game build and composed harness compile passed. `career-champion-combat`,
`career-champion-caps` and `career-meditation-reload` passed with game SHA256
`318362696DE380201D2502A941CE5F4BC3A120A08017F51C8258C770D1A34B3A`.
Ordinary fixtures4294914601-4603 use real signup/acceptance/import and the common
attack filter. Checks cover0ms,9999ms,10000ms,189999ms and190000ms across the signed
counter boundary, score refusal/acceptance, participant ownership, item refusal,
timeout and map cleanup. The initial fixture left teleport invulnerability set;
it now simulates expiry of the independent five-second grace before checking
Champion protection. It also follows the actual paired player order rather than
assuming dictionary enumeration order. Failed entry records were retained.

This verifies attack eligibility and score timing, not every damage formula,
skill packet or pet behavior. Cleanup deliberately avoids the legacy `Win` call;
durable result settlement, tie policy, score-overflow handling and exact round
scheduling remain unfinished. No participation milestone or live deployment is
claimed;505 remains disabled.

### Paired durable match settlement (2026-10-02)

- [x] Export now commits the match receipt and both players' point/counter updates
  in one PostgreSQL transaction before returning players or removing the group.
  The receipt is unique by confirmed entry UUID and records both participant IDs
  and the winner (zero for a draw). Two award records retain admission grade,
  awarded points, wallet preimage/postimage, daily/lifetime totals, counters and
  revision. Existing revision guards protect against stale generic saves.
- [x] Failed transactions retain the ended group for retry. A repeated committed
  receipt never pays again. A failure/uncertain acknowledgement during commit or
  failure installing committed values fences the old cache for reload. A failure
  before commit leaves balances and the receipt unchanged.
- [x] Equal damage is a documented Revora draw: both receive their admission
  grade's loss/participation points and both streaks reset. Neither is declared
  winner. Existing win/loss packet layouts are unchanged; draws use ordinary
  system text plus refreshed statistics, pending native UI acceptance.
- [x] Authoritative damage uses saturating 64-bit counters; existing 32-bit score
  display values saturate without wrapping. Outcome comparison uses the wider
  counters. Wallet additions cap at304400; daily/lifetime uint totals and byte
  match/streak counters saturate. Win bonus uses the admission grade so crossing
  a grade threshold cannot underflow the displayed streak bonus.
- [x] The old public Win pair cannot mint rewards or bypass the owned match's
  transaction. Cleanup clears registration instead of automatically rematching
  every ten seconds. Correct five-round queue continuation is still pending.

Game build, warnings-as-errors harness compile, `career-champion-result-earn`,
`career-champion-result-reload` and `career-meditation-reload` passed with SHA256
`13194D3FD5C82BB86487A3DCC02025C964B29F70FD6F074062B44AF7FFE3F0CB`.
Ordinary supplied fixtures4294914701-4706 enter through actual registration,
acceptance and paired transfer. Three matches cover a winner beyond uint damage,
a draw with injected precommit rollback/retry, and injected postcommit cache
failure. Checks cover wallet/counter saturation, two award rows per match, stale
save refusal, repeated/concurrent cleanup, old Win replay and cold database/game
reload. Eligibility and near-limit point/counter values are supplied test setup.
This is managed-server evidence, not a native gameplay acceptance claim.

Remaining: persist an ending decision across a crash *before* its result commit;
finish disconnected/replaced-session settlement and recovery; exact five-round
schedule and continuation; broader normalized combat/pets; native client checks.
An interrupted entry without a committed result still earns nothing. This batch
neither enables505 nor claims Champion restoration complete or deploys live.

The existing `career-champion-caps` admission/rollback regression also passed
with this build; its successful-match check now expects durable settlement.

### Five scheduled rounds and continuation (2026-10-02)

- [x] Matchmaking now opens only during21:00,21:05,21:10,21:15 and21:20 on
  Monday-Friday, after the existing20:55-20:59 registration window. Each
  participant receives at most one offer per round. Registration alone cannot
  start a match before21:00, and continuous ten-second rematching is removed.
- [x] Revora's documented acceptance deadline is the end of the round's first
  minute. Late acceptance and transfer are refused. The latest admitted match
  has10 seconds preparation plus180 seconds combat and finishes before the next
  five-minute round. The legacy native countdown's presentation remains an
  acceptance check; no speculative packet field was added.
- [x] Successfully settled players requeue for the next round while eligible.
  A voluntary quit withdraws that player from later rounds. Round5 completion
  clears signup; remaining waiting queues expire at21:21. An unmatched player
  receives no invented bye reward. Invitation refusal/expiry still grants no
  points because no confirmed paired entry exists (a documented Revora policy).
- [x] The entry journal stores the server-local event date and round, with a
  unique `(uid,event_day,round)` index. Failed reserved entries consume that round
  as well; old journal rows remain without invented dates/rounds. Repeated or
  clock-rewound offers cannot authorize a second reservation for the same slot.
- [x] Daily reset uses the full date and no longer repeatedly clears results
  after21:25. Existing guarded saves log failures rather than silently swallowing
  them. Two same-day reset calls preserve completed rounds and daily points.
- [x] The existing registration announcement uses the same weekday policy and
  eligible-player checks, replacing its Monday/Friday-only filter. Its text now
  says registration is open. Native invitation controls remain unaccepted.

Game build, composed harness compilation and `career-champion-rounds-earn`,
`career-champion-rounds-reload`, `career-champion-caps` passed with SHA256
`2C9B3E54D27F09B1722053718C1DFA57B33F75874A47A64133EA41274066D5E3`.
The subsequent announcement-only integration rebuilt and cold round reload
passed with SHA256
`8093D33A7DBDABC32B9DD92B143A8321470ECAAA841C07C1A3210C1A66F5A2C3`.
Ordinary fixtures4294914801-4806 cover all five actual admission/combat-timeout/
settlement/continuation cycles,80 winner points and30 loser points, first-minute
boundaries/weekends, no early/repeated pairing, unique database round ownership,
withdrawal, expired acceptance, same-day reset preservation, queue expiry and
cold reload. Initial date binding correctly failed closed under the shared
UTC-timestamp guard; local calendar dates now use invariant date strings cast
by PostgreSQL instead of pretending to be UTC instants. Failed work was retained.

Restart/reconnect currently clears registration rather than silently resuming
remaining rounds. Durable precommit ending decisions and disconnected-session
settlement/return are still unfinished, as are full combat normalization and
ordinary native/NPC control acceptance. Stage505 remains disabled; no live
release or deployment is claimed.

### Champion shutdown retention (2026-10-02)

- [x] Paired admission attaches the existing `IArenaShutdownRetention` contract
  before reservation/transfer. A departure records the first departing owner,
  withdraws them from later rounds and defers shutdown while settlement or map
  return is pending. The existing disconnect-pool/session-publication barrier
  prevents a successor login from replacing that retained owner.
- [x] The normal group tick consumes the departure as a forfeit and preserves an
  outcome already determined before either player disconnects. Payment uses the
  existing paired transaction while both original owners remain available.
- [x] After cleanup, healthy peers release their retention and may continue to
  the next round. Requested shutdowns resume through the existing session lease;
  a failed resumption remains queued. Session replacement/retirement touches
  only the original actor. Fenced peers are disconnected for reload.
- [x] Retention has bounded capacity4096 groups and processes at most128 queued
  owners per tick. Busy owner/session locks return for retry, without holding
  a departing player's item lock while waiting for their opponent.
- [x] Champion admissions participate in the existing shutdown drain. New entry
  and requeueing stop once draining begins; pending invitations are withdrawn,
  unimported groups clean up, and shutdown cannot claim the arenas drained while
  Champion groups or retained owners remain. Active fights retain ownership and
  settle through their normal ending rather than inventing a shutdown winner.

Game build, warnings-as-errors harness compile, `career-champion-disconnect-earn`,
`career-champion-disconnect-reload` and admission/caps regression passed with
SHA256 `597C99D03FE0E3A525AD9C24534E390C75FE3FDB7E8145F822DDFAAF4594B191`.
Ordinary fixtures4294914901-4906 cover a busy opposing owner lock, injected
precommit rollback, retained canonical identity, blocked replacement, successful
payment/return/resumption, fresh session load, both players departing after an
already-determined result, departure during partial transfer, no failed-entry
reward, empty retention queue, and cold reload.

The fixture invokes the production shutdown gate and publication barrier, but
substitutes the final socket teardown callback with checked owner retirement.
It does not claim native socket teardown/reconnect acceptance. Durable ending
intent recovery across a full process crash before result commit remains next;
normal live retention cannot replace that journal. Stage505 and deployment
remain gated by that recovery plus combat/control acceptance.

### Durable ending capture and crash replay (2026-10-02)

- [x] Ending a confirmed match immediately attempts to save its winner/draw and
  both exact award plans before the cleanup delay. Busy ownership locks or a
  failed capture retry through the normal group tick. No result packet is sent
  before the separate payment transaction commits.
- [x] Capture reserves each Champion balance revision atomically with the two
  snapshots. The snapshots retain nullable wallet preimages, admission grade,
  award amount, bounded wallet/daily/lifetime counters, streak/match counters and
  daily reset metadata. They do not recalculate a different reward after restart.
- [x] Pending images decline generic statistic saves and point debits, preventing
  heartbeat/reset/shop writes from invalidating the reserved result. Loading a
  pending owner fails closed; the existing live retention blocks replacement
  until settlement. Committed installation clears the pending image.
- [x] Live settlement and cold recovery use the same guarded award application.
  Both awards and their unique result receipt commit together. Startup replays
  saved, unpaid endings before interrupted-map return and before loading player
  Champion caches or opening listeners. At most4096 pending matches are handled;
  unexpected balance/revision changes refuse replay rather than overwriting them.
- [x] Already-paid receipts are not paid again. An interrupted entry without a
  saved ending still earns no invented win, loss or participation points.

Game build and composed harness compilation passed with SHA256
`A38940EC2184C75AB0AE9B60B4AE03737331A39EF24B308FF2202A1AF06EF717`.
`career-champion-outcome-crash` actually terminates the candidate process after
saving two endings (win and draw), leaving a third entered match without an
ending and all three without payment/cleanup. Ordinary fixtures4294915001-5006
also verify that generic saves cannot change the reserved rows. Two separate
cold `career-champion-outcome-reload` processes verify exactly one award per
eligible player, zero for the unended match, saved point/lifetime totals and
returned/released entries without repeated payment.

`career-champion-caps` passed. A fresh4294915101-5106 cohort reuses the existing
live settlement checks via `career-champion-outcome-result-earn/reload`: overflow
boundaries, draw rollback/retry, postcommit cache failure, stale-save refusal,
repeated/concurrent cleanup and cold reload passed under the new journal.
Older fixtures and their evidence remain intact.

A crash before *any successful ending capture* still has no durable result to
replay; the server does not guess its outcome. Captured outcomes now survive a
crash before award commit. Native socket/control acceptance, remaining full
combat normalization and505 earning/claim validation are still pending. No live
deployment or complete Champion restoration is claimed.


## Equipment limits and block refresh ? validated prerequisite

- [x] Champion equipment calculation projects catalog level to the historical
  slot limit and caps Plus by grade without changing the owned item. It reuses
  the existing bounded same-family/quality/class/gender catalog projection used
  by Skill Team PK. Accuracy, item agility and magic-defence getters use the
  same Champion projection as the main equipment loader.
- [x] Battle Power now counts only the allowed Plus and socket contributions;
  previously it still counted both sockets and the item's actual Plus.
- [x] `ReloadBlock`, including its combat refresh callers, now respects the
  grade's refinery and artifact permissions. Previously it restored bonuses
  suppressed by the main equipment loader. Its shield null guard is corrected.
- [x] New `career-champion-equipment` uses ordinary fixtures4294915201-5202
  and real1009 equip/unequip handling. Across all seven grades it compares
  high-level +12/two-super-socket boots, blade, earrings and necklace against
  independently selected genuinely capped gear outside Champion. The comparison
  includes attack, HP, defence, dodge, accuracy, item getters and Battle Power.
  Original high items' complete stored rows remain unchanged after return to
  inventory. A second cold process repeats the comparisons using loaded items.
- [x] Block refresh is checked across all seven grades with catalog refinery
  and artifact effects supplied temporarily in memory and restored afterwards.
  This is a calculation test, not a claim that attachment/purification was tested.

Build and composed harness compilation passed with game SHA256
`BB9F4693EFEA308EF9D718599BD411A40AF90E34DC4061700503A98F38113C24`.
Two cold `career-champion-equipment` runs passed. The fixture activates Champion
stat calculation with a synthetic group; it does not claim native controls,
real damage exchange or milestone505 earning. The earlier caps fixture also
passed twice before the expanded checks were separated into this cohort.

Remaining: audit modern progression and pets in normalized combat, validate
ordinary controls and505 earning/claim, and retain native acceptance/deployment
as explicit gates. Gem quality is not newly restricted by this change; the
historical guide's numeric gem column alone does not establish a quality rule.
No complete Champion restoration or new Way of Heroes checkmark is claimed.


## Direct damage ownership ? validated prerequisite

- [x] Champion's direct `ReceiveAttack` path acquires the match lifecycle lock,
  ordered session leases and ordered player item locks before the existing
  health lease. It retains them through damage, score and terminal callbacks.
  Contention refuses the hit without waiting; partial acquisition is released.
- [x] Damage is allowed only during the imported pair's combat window. Both
  actors must be the canonical player bodies, not a pet/alternate entity merely
  pointing at a participant as its owner. The earlier `CanAttack` check enforces
  the same identity requirement. This is Revora's paired-player combat rule.
- [x] `career-champion-direct`, using ordinary fixtures4294915301-5303, exercises
  real `ReceiveAttack`: preparation/timeout refusal, outsider refusal, pet-body
  refusal in both directions, busy opponent-lock refusal, and a permitted
  nonlethal hit whose HP loss equals its score increase. It also reuses the
  paired entry, wraparound-clock, timeout winner and durable cleanup checks.
- [x] Two cold `career-champion-direct-reload` processes verify loaded points,
  daily/lifetime totals and match count against durable award receipts, with
  no unreleased entries or remaining Champion membership.

Build and composed harness compilation passed with game SHA256
`6C8FBCF77B6F72245DB076A2C280895465571EF551A5999F537035D8C5585CC3`.
The fixture retains an earlier setup-failure forfeit receipt; it verifies the
successful match's reward delta instead of deleting or resetting that history.
The pet is a synthetic entity body; pet AI/native summoning is not exercised.
The allowed direct hit is nonlethal; no new lethal-hit acceptance claim is made.

Modern progression normalization, ordinary entry controls,505 earning/claim,
and native acceptance remain unfinished. These checks do not establish that
all delayed skill effects or damage calculation entry points are normalized.
No live deployment or complete-event checkmark is added.


## Ordinary player controls ? validated prerequisite

- [x] A Champion Registrar703001 is placed in Twin City near310,288, using a
  bounded collision-free landing search and an existing clerk mesh. Placement
  is idempotent and rejects conflicting NPC identity. Every owned match receives
  a Champion Steward703002 near50,50 for withdrawal.
- [x] Ordinary2031/2032 NPC requests support registration, acceptance, declining,
  queue withdrawal and match forfeiture. Services require the current player,
  the right live NPC, proximity and current invitation/match ownership. Text is
  brief and uses server-time registration hours.
- [x] Optional standard message-box offers capture opponent, round and day;
  stale replies cannot accept or cancel another invitation. With AutoInvite
  disabled, registrar acceptance remains available. Players are told to stay
  near the registrar and the event announcement directs them there.
- [x] Unconfirmed2600-2604 input handlers are disabled globally, including direct
  legacy2604 calls. This does not claim new verification of output serializers.
  No role/name allowlist or native-only control is required for the NPC service.
- [x] `career-champion-controls-earn/reload` uses ordinary fixtures4294915401-5402
  through the actual NPC packet dispatcher. It checks malformed-length/input
  refusal, remote refusal, closed registration, duplicate signup, cancellation,
  generic popup acceptance, decline without rewards, stale callback refusal,
  popup-disabled NPC acceptance, paired entry and steward forfeit. Cold reload
  verifies saved reward/match totals and released entries.

The control run and cold reload passed with game SHA256
`2D10F222670C650A067A95003E73F6DEC7EBAEB553121AC8F08AE37C35A96421`.
After replacing the obsolete event alert with the registrar announcement, the
final build and another cold control reload passed with SHA256
`226765D2F5C9F990C50F19058D43552F1FD21356B4B70D19F18A186C61ED2417`.
Owner work in shared files is excluded from the validation overlay and commit.

This establishes managed ordinary-player controls, not native rendering or
full Champion restoration. Remaining: modern progression/delayed-effect combat
policy and acceptance,505 participation/claim, native client checks and approved
live deployment. Way of Heroes remains74/79.


## Lethal damage and protected possessions - validated prerequisite

- [x] Two ordinary players register and accept through the registrar, enter the
  owned match, and exchange a finishing hit through production `ReceiveAttack`
  and `Entity.Die`. The injected hit exceeds remaining HP; the score includes
  only actual remaining HP, the victim ends the match as loser, and another
  hit after defeat is refused.
- [x] Cleanup revives/returns both players and gives the winner10 and loser6
  grade-one points. Repeated cleanup leaves both result snapshots unchanged.
- [x] `career-champion-lethal-earn/reload` checks the above on fixtures4294915501-5502.
  A separate `career-champion-lethal-valuables-earn/reload` cohort4294915601-5602
  starts each player with123,456 silver,30,000 CPs and two unbound catalog items.
  Neither player's purse/items nor the killer's PK points change during the
  death. Cold reload verifies exact saved Champion/result snapshots plus the
  supplied purse and item rows; both characters are alive back in Twin City.

All four phases and composed harness compilation passed against game SHA256
`226765D2F5C9F990C50F19058D43552F1FD21356B4B70D19F18A186C61ED2417`.
No production code or deployment changed in this validation batch. The damage
amount is supplied by the harness; this is not class damage-formula validation,
full skill-packet acceptance, or delayed-effect acceptance.

The existing entry journal is the proposed505 evidence source: a successful
paired arrival atomically records `entered_at` in `ChampionEntryLedger.Confirm`.
Signup, declined offers and reserved/failed entries do not set it. No extra
participation table or client-claim-as-progress path is needed. Modern combat
policy/acceptance and505 claim integration remain pending; the milestone stays
disabled and Way of Heroes remains74/79.


## Tide Trap ownership and replay - validated prerequisite

- [x] Tide Trap must be the exact live floor object on its recorded map, with
  the original current player owner. Damage requires that owner still be on
  that map. The effect is consumed under owner/item locks and removed even when
  it has become ineligible; concurrent screen timers cannot fire it twice.
- [x] Both spell11110 Tide Trap producers now register the object with the map
  before sending its spawn. Previously they only populated client screens.
- [x] Each target receives a local copy of the trap's stored damage; processing
  one target cannot overwrite the stored value for later targets.
- [x] `career-champion-trap` uses ordinary fixtures4294915701-5702, actual
  registrar entry, a supplied14250 spell capability and production trap/damage
  handling. It verifies no hit before combat, no hit from a town trap after
  arena entry, absent-floor and wrong-owner refusal, exactly one123-HP hit and
  matching score under two concurrent callbacks, repeat refusal, and no hit
  after match completion. It also verifies removal from the owning floor map.

Build and composed harness compilation passed with game SHA256
`45EB05F26A8F73E697ABD07C167142557527367A08E52AFD5191FB8685749C15`.
The fixture supplies the trap and spell capability; it does not prove the full
spell11110/rune acquisition path or native rendering. No live deployment.

Remaining concrete combat findings from this audit:

- Twilight Dance's delayed callback captures one map but reads the caster's
  later map/screen when producing effects and damage. It needs an origin-bound
  callback check and a focused transition test.
- Spell11110's King-versus-Serf branch contains a123567 damage multiplier, while
  its neighboring King branches use1.2. This source finding needs correction
  with focused damage-path validation; no original rule supports the outlier.
- Modern progression policy/acceptance and505 participation/claim remain open.

These are pending findings, not completed checks. Way of Heroes stays74/79.


## Rank damage correction and real Blade Tempest casts (2026-10-02)

The grade-1 noble Battle Power cap did not stop separate rank-pair damage
multipliers. The attack handler contained 1,287 simple rank-pair damage blocks
(including 98 active `123567` multipliers), plus a nested `123456` multiplier.
The shared Melee, Magic and Magico calculators also applied a second rank table.
These damage-only layers are removed. Existing Battle Power calculations,
rank population rules, skill base multipliers and rank-based status-effect
eligibility (Soul Shackle and poison) are unchanged.

This is a documented Revora correction. The [official Battle Power guide](https://co.99.com/guide/guides/calculate.shtml)
describes nobility's Battle Power contribution; it does not establish these
extra legacy rank-pair multipliers. This change does not claim exact official
class damage formulas or complete class balance.

Validation uses `career-champion-blade`, ordinary UID cohort4294915801?5802,
a real registrar/paired owned match, an actually equipped catalog weapon and
a supplied learned11110 capability. After both characters enter screen range,
the production attack handler casts Blade Tempest for all49 attacker/target
rank combinations at grade1. Every cast deals2049 damage, leaves the target
alive, and preserves the active match. Melee, Rango, Magic and Magico return
positive, identical results across the49 rank pairs with fixed combat stats.
The fixture supplies stats/learned skill and controls clocks; it does not prove
skill acquisition, native packet decoding, native rendering or all classes.

Build and matrix fixture passed with private executable SHA256
`6D5C1CA1B0436F5D5BE0844FD0856896F8878A2A50A3E28517D136D98EFCBC67`.
A mechanical comparison against the prior commit verified that all other
non-region tokens in Handle.cs were preserved. Calculate.cs changes are limited
to its three nested Nobility Damage regions. No live deployment occurred.

Earlier zero-damage fixture failures were caused by missing casting setup:
a required weapon and screen registration attempted while the pair were still
out of range. Fixing the fixture required no relaxation of production checks.
Retained candidate history was preserved.

Twilight Dance callback ownership, modern progression acceptance and505 paired
participation/claim remain unfinished. Way of Heroes remains74/79.


## Twilight Dance callback ownership (2026-10-02)

- [x] Capture the original canonical body, owner, map instance, map-transition
  version, Champion group/opponent and fixed cast path. Reject dead, disconnected,
  replaced, moved or completed-match owners before emitting a floor effect.
- [x] Serialize each cast, permit at most three pulses and enforce400ms spacing
  using a monotonic clock. The existing scheduler's repeat2 means three total
  invocations; the correction preserves that count. Fresh target/attack/result
  objects prevent concurrent callbacks from sharing Handle's mutable fields.
- [x] Keep Champion lifecycle/session/item leases and per-target health ownership
  across validation, calculation and damage. MapID changes increment a transient
  version under the existing owner lock so leaving and returning cannot revive
  an old cast. Existing persistence-before-map-change ordering is preserved.

The extended `career-champion-blade` phase passed on private executable SHA256
`F3F86DB0A208CA4CABBBF1F70818567CF6A10E9F4A25CA4CF42DACB03ACA7DCC`.
Its49 Blade Tempest rank pairs still pass. For Twilight, it directly invokes the
production callback returned by CreateTwilightPulse with a supplied learned
skill and deterministic clock: two concurrent calls emit one pulse;399ms emits
none;400ms allows the next; exactly three pulses deal1760 each; further calls
are inert. Pre-combat, old-town, map-ID-away-and-back, completed-match and
post-return calls add no floor effects or damage. Ordinary retained player
cohort4294915801?5802 is reused without deleting its history.

The fixture tests the actual callback, not wall-clock scheduler delivery,
client skill acquisition or native animation. Its map round trip uses the
production MapID setter directly rather than a native teleport request.
No live deployment occurred. Modern progression acceptance and505 integration
remain open; Way of Heroes remains74/79.


## Supplemental VIP/nobility/Bloodshed stat restrictions (2026-10-02)

Champion now excludes Revora's extra flat VIP/nobility stats and the nine
Bloodshed boost stat additions at every grade. GM-only extra HP is also excluded
from the same calculation path; the ordinary-player fixture does not claim a GM
acceptance test. Nobility's separate capped Battle Power contribution remains.
These are documented Revora fair-combat restrictions, not additional columns
claimed from the [2013 guide](https://co.99.com/guide/event/2013/champions-arena.shtml).

The original boost flags, VIP entitlement and nobility rank are not cleared,
charged or rewritten. LoadItemStats suppresses only their derived stats while a
Champion group is attached; ordinary recalculation after leaving restores them.
This does not yet suppress JiangHu, Inner Power, Perfection, relic or rune bonuses.

The expanded `career-champion-equipment` fixture passed in two cold processes
on private executable SHA256
`CB7B12A398B8FFC6DA5D1E8619DD6C4C70DCD8F4AEF73EC2EB65A0164B45B666`.
It compares all seven grades with and without supplied VIP7, King rank and all
nine boost flags, verifies identical combat-stat vectors inside, higher stats
outside, and suppression again on re-entry. Flags and entitlements remain
present; the saved entity row is unchanged. Fixtures supply VIP through its
backing field and boosts in memory, so this proves stat handling rather than
purchase/entitlement earning. The existing owned-gear cap checks still pass;
their outside-arena reference now uses neutral VIP/rank to compare gear alone.

This is synthetic group/stat validation, not a new paired match or505 evidence.
The remaining later-system policy/acceptance and505 claim work stay unchecked.
No live deployment or native acceptance is claimed; Way of Heroes stays74/79.


## Later progression stat projection (2026-10-02)

Revora policy: JiangHu, Inner Power, Perfection and relic bonuses are inactive
at every Champion grade. Their owned progression remains intact; normal stats
return after leaving. These are Revora restoration rules for systems not covered
by the historical tier table, not newly asserted original earning/combat rules.

- [x] Suppress per-item and aggregate Perfection stats; reject Perfection combat
  procs when either actor belongs to Champion, without changing owned item levels.
- [x] Suppress JiangHu status application and direct increase/decrease refresh
  APIs during Champion. Inner Strength can still be recalculated accurately.
- [x] Suppress Inner Power's central stat application, including direct refreshes.
- [x] Suppress relic attributes and RelicPower contribution in the equipment
  calculation and direct attribute handler.

The extended `career-champion-equipment` phase passed in two cold processes with
private game SHA256
`AAF7F0948D096DD7AF13437DEE3952F32F52A9282EF5C656E4F1A93527518988`.
Each of four sources is tested separately at all seven grades: baseline and
supplied progression yield equal Champion stat vectors, positive differences
outside, and suppression again on re-entry. Direct JiangHu/Inner Power refreshes
cannot bypass the restriction. A deterministic supplied Perfection effect is
inactive in Champion and active outside; its temporary catalog replacement is
restored in finally. Existing equipment and supplemental-stat checks still pass.

Fixture scope: actual owned boots are equipped, while Perfection level54,
Inner Power bonus properties and a valid catalog JiangHu star are supplied in
memory and restored. Relic attributes are exercised through the production
private handler, not a new relic purchase/equip route. The owned boots database
row remains unchanged. These are stat/proc boundary tests with ordinary accounts
and synthetic Champion group references, not progression acquisition or a native
combat session. No new505 evidence is earned by this fixture.

- [ ] Finish rune skill/effect restrictions and paired combat acceptance, then505
  confirmed-entry/claim integration. Native acceptance and live deployment remain
  pending; Way of Heroes stays74/79.


## Rune restriction and ordinary paired acceptance (2026-10-02)

Revora rule: rune-derived skills and bonuses are inactive during Champion at all
grades. Active rune buffs are cancelled on entry; leaving restores usable skills
from equipped items, not paused buff durations. Owned items, levels, binding and
experience remain unchanged. The registrar now has a brief rules option covering
the documented gear/Chi limits and supplemental/later-system restrictions.

- [x] Derive an empty rune skill set while Champion is attached; normal refresh
  restores skills from the unchanged legal loadout after leaving. Skill saves
  continue excluding derived rune skills, preserving permanent learned skills.
- [x] Cancel rune flags, Infinity's associated flight, yellow effects and lingering
  shield/heal timers. Direct rune casting/effect dispatch is refused in Champion;
  effect dispatch shares owner/health locks with transitions.
- [x] Consume queued Tide Trap floor items without damage during Champion, even
  if a stale in-memory rune skill is present. Fine Rain team healing rejects
  Champion participants/other maps under health ownership; its delayed UI loop
  stops when the caster belongs to Champion.

`career-champion-runes` uses new ordinary cohort4294915901?5902 with supplied
level140/rebirth2 Pirate profiles. Three catalog runes are granted, moved and
equipped through real1009 handlers, then both players enter through the registrar.
Skills/active flags/yellow effects vanish in the real paired match, and unequip/
re-equip cannot restore them. Direct rune handlers and a queued trap are refused.
The item-row snapshot is unchanged after exit, yellow effects and derived skills
return, and permanent skill11110 survives saving inside/outside. A cold process
reuses the same saved loadout and repeats the route without recreating items.
These are ordinary loadout/entry/effect tests, not rune acquisition or native UI
acceptance. The earlier dedicated trap fixture's valid-Champion-hit expectation
is superseded by this no-runes policy; its source now expects no score/damage.
Historical candidate receipts were retained.

Final build and rune/Blade Tempest/Twilight checks passed with private SHA256
`29F3CA8621FA304B51EBED3A7A96DF3357198DD90B570F5E73191EB26A6F78DF`.
Native/client acceptance and live deployment remain pending. The next open work
is505 confirmed paired-entry evidence, ordinary claim/consumption and cold
persistence. Way of Heroes remains74/79 until that earning/claim path passes.


## Confirmed participation and milestone505 (2026-10-02)

- [x] Enable the catalog contract: task26, one participation, one bound EXP Ball
  723700. Registration alone does not count.
- [x] Require the durable paired-entry journal: exactly two players with matching
  weekday and round1-5, a confirmed entry timestamp, consistent timestamps and
  entered/returned/interrupted state. Reserved and aborted entries do not qualify.
- [x] Use the existing transactional claim route to persist progress, one reward
  and the unique claim receipt together.
- [x] Validate two ordinary characters through registrar signup, paired entry,
  return, eight concurrent claim requests per character, consumption and cold reload.
- [x] Reject no participation, signup alone, declined invitation, interrupted
  partial transfer, fabricated runtime match count, busy trade and duplicate claims.
- [ ] Native client display/control acceptance and live deployment remain pending.

`career-champion-claim-earn` and `career-champion-claim-reload` both passed against
private candidate SHA256
`8FFB2003FBE4EA86976C7BBE46D71EC81497B9594A5523727B9BDEDDE9AFCB18`.
The fixture supplies level120/rebirth1 profiles, but earns participation through
production NPC and match entry handlers. The real2830 claim gives one bound ball;
real item-use requests, including a duplicate, grant exactly one ball's XP.
Saved claim, progression, XP, daily usage and match snapshots survive a separate
process. The outsider remains ineligible. These are managed handler and database
checks, not a native client or live deployment result. Historical unfinished505
notes above describe earlier checkpoints and are superseded by this section.
