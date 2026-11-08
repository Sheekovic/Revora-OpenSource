# Way of Heroes main-stage prizes

## Installed catalog and status

All79 installed substages and ten main-stage claim handlers are enabled in source.
All ten aggregates now pass retained-career earning, claim, consumption and cold
persistence checks. Native/live acceptance remains pending; the retained career
contains earlier documented fixture prerequisites, so this is not a new-player
zero-to-completion proof.
The installed `stagegoal.txt` maps the ten main stages to these packs; installed
`itemtype-new.txt` explicitly describes their amounts as CPs(B):

| Stage | Required substages | Pack | Bound CPs |
| --- | --- | --- | --- |
| 1 | 5 | 3005107 MartialNovicePack | 20 |
| 2 | 7 | 3005108 MartialWarriorPack | 30 |
| 3 | 6 | 3005109 MartialStandoutPack | 50 |
| 4 | 9 | 3005110 MartialFamousPack | 80 |
| 5 | 7 | 3005111 MartialHeroPack | 100 |
| 6 | 9 | 3005112 SuperHeroPack | 200 |
| 7 | 11 | 3005113 MartialTalentPack | 300 |
| 8 | 8 | 3005114 GreatTalentPack | 500 |
| 9 | 9 | 3005115 GrandmasterPack | 800 |
| 10 | 8 | 3005116 MartialMightyPack | 1,000 |

The existing Way of Heroes bag consumer now handles these ten packs. Its ledger
migration preserves old uses with zero bound CPs. Item consumption, bound-CP
compare-and-set and use receipt share one transaction. The committed balance
updates the current client through `ApplyCommittedBoundCps`. Overflow refuses
without consuming the pack; a failed transaction leaves both unchanged and
fences the session for a normal reload. No packet layout or dispatcher change.

## Private consumer validation (2026-10-03)

Executable SHA256: `E7CBA8C5199135392F13B946E3ADB68D27E0211577A6617E0832367F6140249E`.
`career-main-packs-earn` and `career-main-packs-reload` passed. Ordinary non-GM
character4294917901 received ten explicitly supplied fixture packs; this tests
consumption, not milestone earning.

- Eight concurrent ordinary item-use requests per pack grant its amount once.
- All ten uses total3,080 bound CPs, with no unbound CP or silver change.
- At a controlled near-maximum bound-CP balance, the first pack remains intact.
- A scoped receipt trigger returns no inserted row. The transaction rolls back
  its pack debit and balance write, then fences the session. Reload preserves the
  exact preimage; normal retry succeeds. The trigger/function are removed.
- Cold process reload preserves exact items, balances and ten receipts; a save
  does not resurrect consumed packs or overwrite the new balance.

The initial fresh-player adapter attempted a save before loading normal durable
state and was refused. The retained test resumed by loading the actual character
and its already-saved packs; no items/receipts were reset or duplicated.

## Aggregate contract (implementation status below)

- Parse and validate all ten main-stage GoalNum/prize/open-level rules against
  the installed substage set before enabling claims.
- Bound mode2 page IDs to1-10 before multiplication and reject main IDs presented
  as mode1 substage claims. The existing dispatcher recognizes modes0/1/2; native
  mode2 presentation/acceptance still requires its separate client gate.
- Require every expected saved substage completion, not only a count or client
  progress. Decide/document legacy Done handling consistently with existing
  replay-safe substage migration.
- Save main-stage Done and its unique payout receipt atomically with pack delivery;
  distinguish it from substage entries when restoring/merging progress.
- Validate partial/malformed/foreign-owner refusal, concurrent single claim,
  postcommit recovery, actual earned pack consumption and cold persistence.
- Audit the hardcoded `SendDone` summary separately before claiming accurate
  native UI progress. No native evidence or live deployment is claimed here.

## Aggregate handling validation (2026-10-03)

Executable SHA256: `3577CBB7822FC92381011621C2CECC15297172BEE5E549684F46B80E38998F3B`.
`career-main-claims-earn` and `career-main-claims-reload` passed. Despite the phase
name, these are claim/migration tests with supplied saved completion fixtures,
not proof of earning79 goals through gameplay.

Implementation validates all ten GoalNum/prize/open-level/flag tuples and their
expected installed substage counts at startup. Main mode2 pages must be1-10
before multiplying; mode1 cannot address a main-stage ID. The payout requires
each specific substage's saved Done flag. Extra, duplicate or unrelated entries
cannot substitute for missing requirements. Existing trusted legacy substage
completion is accepted consistently with substage migration; client fields and
unsaved session progress are never eligibility evidence.

Main receipts use stage_uid100/200/.../1000 in the existing unique claim ledger.
Restore marks the page's Done without inventing a substage100/200/etc. All ten
packs use Revora's existing bound-prize policy, including where the catalog's
outer-pack flag is0; their contents remain the exact catalog bound-CP amounts.
Inventory insertion, ledger receipt and saved Done commit together. The existing
2831 page response is sent after successful installation. A legacy main Done
flag is conservatively treated as already paid and migrated to a legacy receipt
without awarding another pack.

Private non-GM fixtures4294918001/8002 verify:

- No saved completion means no main payout on any of the ten pages.
- Each of79 individual missing Done flags rejects its page, and replacing a
  required member with an unrelated done entry rejects each of ten pages.
- Invalid pages0/11/UINT_MAX and arithmetic-wrap alias1073741825 are refused;
  mode1 rejects all ten main IDs without changing saved state.
- Eight concurrent requests per page award one pack. All ten packs open once
  for3,080 bound CPs; cold reload preserves exact receipts/balances/consumption.
- An injected BeforeInstall exception occurs after page1 commits. Session fencing
  then ordinary reload restores one pack and main Done; replay cannot pay twice.
- Already-paid legacy main Done creates one legacy receipt and no items, remains
  separate from substages, and survives repeated claims/cold reload.
- Serialized2831 byte8 reflects saved main Done. This is not native rendering
  acceptance, and the hardcoded2832 summary still requires audit.

The C# fixture initially lacked its SafeDictionary namespace import; compilation
was corrected before process validation. No live deployment or client patch.

Remaining: actual compatible-career aggregate earning; native main-stage claim
and UI acceptance; coherent release after those gates. Previous consumer and
individual-substage evidence is retained but does not replace these checks.

## Retained Novice and Warrior earning (2026-10-03)

No gameplay rebuild was needed: executable SHA256 remains
`3577CBB7822FC92381011621C2CECC15297172BEE5E549684F46B80E38998F3B`.
`career-main-career-earn/reload` and `career-main-second-earn/reload` passed.
Character4294907001 remains an ordinary non-GM career with accumulated gameplay
and durable evidence from earlier batches, not a fresh signup fixture.

For Novice, saved level140, valid equipped quality, completed introduction and
promotion evidence already qualified. Its XP-kill ledger was empty. The character
used its Monk trainer's ordinary route for Oblivion as needed, killed actual
spawned ordinary monsters to fill the XP gauge, and ran the normal buffer callback
to activate the XP list. Actual10390 activation then earned20 distinct XP-kill
receipts through melee kills. No kill receipt, substage Done or main Done was
inserted by the test. Attack cooldown timestamps and the offline outgoing socket
adapter are controlled test mechanics; this is server earning, not native control.

All five substage claims were made through2830. Main-stage requests before the
last saved substage claim were refused, even after the underlying XP goal was
satisfied. Eight concurrent main claims paid one bound3005107; actual use added
20 bound CPs once. Separate cold reload preserved the exact career/XP/promotion/
introduction receipts, inventories, balances and main/substage claim state.

Warrior's remaining requirements were203 team creation and206 hour-potion use.
Normal team-create/dismiss packets saved the team receipt. The203 reward supplied
a bound hour potion; ordinary use consumed it and saved3,600 seconds. All seven
substages then claimed normally. Main-stage requests remained refused while the
last substage receipt was absent. Eight concurrent main requests paid one bound
3005108; actual use added30 bound CPs. A separate cold reload preserved the exact
career/team/potion/claim state and balances, including the active potion timer.

Bag-space preparation uses ordinary trades with retained character4294917901,
with no item deletion or earning-record reset. Its earlier supplied-pack snapshot
is now historical because it holds transferred goods. The first stage2 adapter
attempt asked the generic trade helper to make more room than needed; it exhausted
that helper's supported bound-material choices after reaching29 slots. The fixture
resumed from those committed trades using the actual31-slot maximum needed for
this reward batch. No production storage rule was changed. Stage1's snapshot is
likewise historical after stage2 advances the same character.

The private process also printed an existing vote-pulse PostgresException; no
vote service was changed or validated in this batch. The scoped aggregate phases
and cold snapshots passed independently.

Current aggregate earning coverage: **2/10**. A read-only current-predicate audit
identified these remaining gaps on this career (saved claims remain authoritative
when a transient predicate later changes):

| Main stage | Currently unmet substages at inspection |
| --- | --- |
| 3 | 304,305 |
| 4 | 403,406,407 |
| 5 | 505,507 |
| 6 | 604,605,606,607 |
| 7 | 702,703 |
| 8 | 804,805,807 |
| 9 | 904,905,906,908 |
| 10 | 1003,1004,1007,1008 |

Stage3 is next: five confirmed mutual friendships and the first Team Arena win.
Native client rendering/control and live release remain outside this evidence.

## Retained Standout earning (2026-10-03)

`career-main-third-earn/reload` passed against the unchanged private executable
SHA256 `3577CBB7822FC92381011621C2CECC15297172BEE5E549684F46B80E38998F3B`.
Retained ordinary character4294907001 advanced from its stage2 checkpoint.

Five new ordinary peer adapters4294918101-8105 supply counterpart players. Their
normal level100 profiles are fixture prerequisites; none is a GM or a fake actor.
Five friendship request/accept sequences commit both reciprocal rows and consent
receipts. Team creation and accepted invitations then form two teams of two.
Ordinary Team Arena signup, matchmaking and acceptance install a shared match.
The retained character defeats both scripted opponents through actual melee
handlers, using its loaded combat stats and their loaded HP/defence. No result,
win counter, friendship or milestone receipt is directly manufactured.

Warm-up/return timestamps are advanced in the private adapter to bound test time;
opponents are scripted, so this proves earning/persistence rather than PvP
balance or native-player control. Before entry and before the match result,
substage305 refuses to pay. Aggregate requests are refused while a substage
receipt remains missing. The six ordinary substage claims then qualify stage3;
eight concurrent main requests pay one bound3005109. Actual use adds50 bound CPs
once, and cold reload preserves exact inventory, friend consent, arena statistics,
substage/main receipts and balances. Repeated substage/main claims change nothing.

Storage preparation retains old goods through normal withdrawals, trades and
deposits with peer8105. The generalized fixture helper excludes runes and known
non-storable quest materials. Its first draft incorrectly applied inventory-only
trade eligibility to warehouse-held goods; correction selects a portable unbound
warehouse candidate and lets the real transfer/trade handlers enforce ownership.
A subsequent invitation attempt exposed missing partner/mentor collections in
the offline adapters; normal relation loaders fixed that setup. Earlier committed
friendships and trades were preserved on retry. No gameplay rule was changed.

The private vote pulse still reports its pre-existing PostgresException; voting
is not part of these claims and has not been repaired or validated here.

Aggregate earning coverage is now **3/10**. Stage4 still needs403 equipment
composition,406 Team PK participation and407 Skill Team PK participation. The
source already supports these individually; the next batch must combine their
ordinary earning on this retained career. Native/live gates remain pending.


## Retained stage4 earning (2026-10-03)

- [x] `career-main-fourth-earn` and separate `career-main-fourth-reload` pass.
- [x] Retained ordinary career4294907001 earned composition with a bound +1 stone
  previously awarded by stage304 and existing compatible bound equipment.
- [x] Ordinary create/invite/accept packets formed eight two-person teams.
  NPCs417171/801717 admitted them to Team PK and Skill Team PK respectively.
  Registration alone refused406/407 and the aggregate; starting each eight-team
  bracket wrote its actual participation evidence. No participation receipt was
  directly supplied. Fifteen new non-GM profiles4294918201-8215 supplied level130
  counterpart prerequisites, not retained-career progress.
- [x] All nine saved substages qualified one bound3005110 under eight concurrent
  main requests. Actual consumption added80 bound CPs once. Exact cold snapshots
  cover inventory, earlier career state, composition, participation, claim/use
  receipts, balance and counterpart items. Repeated claims changed nothing.

Executable SHA256 remains
`3577CBB7822FC92381011621C2CECC15297172BEE5E549684F46B80E38998F3B`.
This batch changes the private adapter and evidence only. Calendar/warm-up/match
and cleanup deadlines are controlled to finish bounded scripted brackets; this
is participation evidence, not competitive combat, balance or native UI proof.

The first composition attempt selected an unbound item and was correctly refused
with the bound stone. The adapter now selects compatible owned bound equipment.
An attempted team dismissal/recreation between tournaments failed because the
existing Dismiss path removes the leader rather than dissolving every membership;
the same-team scenario does not require dismissal. That team-command behavior
remains a separate follow-up, not a repaired feature in this batch. Retried runs
preserved committed trades, composition and first-tournament participation; the
second tournament completed after a process restart. This evidence does not yet
prove both tournament entries in one uninterrupted client session.

Room for rewards came from real warehouse/trade operations with peer8215; old
goods were retained. Earlier exact snapshots remain historical checkpoints.
The unrelated private vote pulse still reports PostgresException. Aggregate
coverage is **4/10**, not a whole-roadmap percentage. Next is stage5, particularly
505 Champion Arena participation and507 twenty saved Team Arena wins. Native acceptance and
live deployment remain pending; no live process was restarted.


## Retained stage5 earning (2026-10-03)

- [x] `career-main-fifth-earn` and separate `career-main-fifth-reload` pass.
- [x] Retained non-GM career4294907001 entered Champion Arena through registrar
  signup, matchmaking and mutual acceptance with existing peer4294918202.
  Signup alone refused505. Actual paired entry committed participation; the
  character then withdrew through the arena steward. This is participation
  evidence, not a Champion combat-win claim.
- [x] Normal team creation/invitation and Team Arena signup/acceptance packets
  formed matches with existing ordinary peers8201-8203. Actual melee handlers,
  loaded character equipment/stats and loaded opponent HP/defence produced the
  remaining19 wins, bringing the career to20. The fixture never supplies wins,
  replaces combat stats or directly creates earning receipts. Saved progress
  below20, including19, refused507 and the main claim.
- [x] Seven saved substage claims qualified main5. Eight concurrent main requests
  paid one bound3005111; actual consumption added100 bound CPs once. Cold reload
  preserved exact career inventory, claims/use receipts, arena results/admissions,
  Champion result state, balances and transferred counterpart items. Repeat
  substage/main requests changed nothing.

Executable SHA256 remains
`3577CBB7822FC92381011621C2CECC15297172BEE5E549684F46B80E38998F3B`.
Only private adapters and documentation changed; no production build/deployment.
Counterpart profiles and goods were reused from stage4. Ordinary warehouse/trade
operations made room for rewards without deleting previous possessions.

The first adapter attempt approached the registrar while still on a warehouse
map; explicit travel to Twin City corrected it. A later assertion expected
immediate return after the steward withdrawal, but the actual handler records
completion before the normal export phase. Paired entry and withdrawal survived
the interrupted test and startup recovery; the adapter now runs the export phase.
The final pass resumed that committed participation rather than replacing it.
Consequently this batch does not prove uninterrupted Champion-to-Team-Arena play.

The Team Arena loop initially reopened admission only once. Normal time-of-day
maintenance closed it after the first match because the private test ran outside
11:00-13:00/19:00-21:00. Diagnostics confirmed both leaders remained eligible and
queued. Reapplying the controlled admission window per match corrected the test;
no schedule, ownership or combat rule was weakened. Three previously committed
wins were preserved on retry, followed by wins5-20 in the final earning process.
Match warm-up, attack spacing and return timestamps are advanced to bound runtime;
scripted opponents do not prove competitive balance or native controls.

Aggregate earning coverage is **5/10**. Stage6 next needs604 purification,
605 Chi score,606 enlightenment and607 Guild PK on this same career. All79
individual substages remain separately covered. Native-client acceptance and
live deployment remain pending. The unrelated private vote pulse continues to
report PostgresException and was not repaired or validated by this batch.


## Retained stage6 Chi and enlightenment (2026-10-03)

- [x] `career-main-sixth-growth-earn/reload` passed on the unchanged private
  executable3577CBB7822FC92381011621C2CECC15297172BEE5E549684F46B80E38998F3B.
- [x] Retained career4294907001 spent its existing earned Chi points to unlock
  Dragon, study it above300, then unlock/study Tiger. Normal random rolls reached
  total597. The adapter keeps attributes scoring at least75 using normal lock
  bits; it neither overrides randomness nor writes Chi blobs/scores. Every roll
  checks the exact point debit. Any needed refill uses the ordinary paid action.
- [x] Substage605 paid one bound3005126 under eight concurrent claims. Actual use
  credited500 Chi points once. Existing progress below500 refused the claim.
- [x] The same career spent100 existing enlightenment points on ordinary
  level100 peer4294918104 through the real enlightenment packet. A nonlegacy donor
  receipt and the recipient's allowance/wait state committed; replay changed
  nothing. Substage606 paid exactly three bound Meteors under concurrent claims.
  The Meteors remain owned for later use; this batch does not claim their
  consumption or completion of the recipient's remaining timed EXP payouts.
- [x] Cold reload preserved the exact combined career/recipient snapshots,
  pending enlightenment award, balances, inventory, claims and prior progress.
  Repeated605/606/main6 requests changed nothing. Main6 correctly remains unpaid.
- [ ] 604 compatible purification on the retained career.
- [ ] 607 Guild PK participation on the retained career, then the remaining
  stage6 claims and main reward.

The private audit confirmed four current unmet stage6 predicates before this
batch:604/605/606/607. Displayed permanent P7 effects on the old low-level fixture
gear do not satisfy the saved catalog compatibility check. They were not rewritten
or counted as earning; valid compatible equipment plus actual soul application
remains required. No claim or eligibility safeguard was relaxed.

Existing characters8104 and8214 supplied the recipient and storage counterpart.
Normal warehouse/trade handlers retained prior goods while making inventory room.
An initial reflection call omitted RefreshDay's optional second argument; the
adapter was corrected, preserving the already committed Chi work on retry.

Aggregate coverage remains **5/10**, with two of stage6's four previously unmet
requirements now earned. No live process, client patch or game binary changed.
The existing private vote PostgresException is outside this evidence. Native
acceptance and live deployment remain pending.


## Retained stage6 purification (2026-10-03)

- [x] `career-main-sixth-soul-earn/reload` passed against the unchanged private
  executable3577CBB7822FC92381011621C2CECC15297172BEE5E549684F46B80E38998F3B.
- [x] Retained career4294907001 withdrew an existing earned bound P7 boots soul
  from Twin City's warehouse. The adapter preserves the selected item identities
  in its probe metadata rather than supplying new equipment or souls.
- [x] Its existing bound boots advanced through normal Meteor upgrades to
  level129. Normal randomness was used; each attempted upgrade consumed its
  selected Meteor, and failed attempts required ordinary paid repairs. Existing
  Meteors were used first, with additional Meteors purchased for catalog CPs.
  Only the anti-repeat clock was advanced between attempts to bound test time.
- [x] Actual purification consumed the selected soul and exactly the catalog's
  Meteor requirement, including Meteor-scroll packing/change. Replay paid nothing
  again. The unequipped purified item refused604; equipping it qualified one bound
  small Permanent Stone723694 under eight concurrent claims. The stone remains
  owned; stabilization and permanent-P7 milestone1008 are not claimed here.
- [x] Separate cold reload preserved the saved compatible P7 effect, equipment,
  prior career/recipient state, balances, receipts and reward inventory. Repeated
  substage/main6 requests changed nothing. Main6 remains unpaid pending607.

The old supplied low-level equipment's displayed permanent P7 metadata was not
accepted as valid earning. This test upgraded the actual item and replaced its
effect through the paid application service; neither the compatibility predicate
nor the saved effect was forged. The new effect has the ordinary finite lifetime,
so later milestones must use it while valid or stabilize it through the service.
Previous possessions were retained through normal trades with existing peer8213.

Aggregate coverage remains **5/10**. Three of stage6's four previously unmet
requirements are now earned; Guild PK participation is next. This evidence does
not validate native controls, competitive balance, or live deployment. No live
process or game binary changed. The unrelated private vote PostgresException
remains outside this batch's validation.


## Retained stage6 Guild PK and main prize (2026-10-03)

- [x] `career-main-sixth-guild-earn/reload` passed against unchanged executable
  SHA2563577CBB7822FC92381011621C2CECC15297172BEE5E549684F46B80E38998F3B.
- [x] Retained ordinary career4294907001 and existing ordinary rival4294917701
  entered Guild Contest through its NPC. Actual melee handlers defeated two
  hunt monsters for the retained guild and one for the rival. Real frozen scores
  seeded the two guilds; the scheduler generated their final. No seed, kill,
  match or participation row was supplied directly.
- [x] Both players entered through the Guild PK NPC during its admission window.
  Qualifier registration and pre-start PK admission refused607. Once the match
  started with both sides present, the normal participation pulse committed
  evidence. Its normal unscored deadline produced a draw and returned both
  characters. This proves participation, not a combat victory or treasury prize.
- [x] All nine saved substages qualified main6. Eight concurrent main requests
  paid one bound3005112; actual use added200 bound CPs once. Separate cold reload
  preserved exact career/reward state, participation, frozen seeds and kills,
  match and entry records. Repeated substage/main claims changed nothing.

The retained character predates durable guild-admission tracking. Its unknown
join date was correctly refused by the seven-day rule, even though it had a
valid guild membership and older join achievement. Ordinary leadership transfer,
leave, invitation, rejoin and leadership restoration established a new admission
without deleting the guild or backdating tenure. Storage peer8212 temporarily
held leadership; the original rank was restored through the normal command.
The private calendar then supplied elapsed time. This does not establish a
legacy admission migration or waive the seven-day rule for live players.

Counterpart equipment is an explicit prerequisite: the existing rival received
one supplied bound +12 low-level sword when it lacked a weapon, equipped through
the normal service. Its guild points still came from actual kills with loaded
stats. The retained character's gear/attack values and all monster HP remained
unchanged by this fixture. Existing profile7701's earlier independent checkpoints
are historical now; its equipment is no longer their original empty state.

The first draft queried the final before the next scheduler pulse. Another
attempt correctly refused unknown tenure; its interrupted arena was cancelled
by startup recovery. The adapter preserves those historical records and qualifies
again in the next controlled week instead of reopening a cancelled match. The
final earning process completed the fresh hunt, scheduled PK participation,
deadline cleanup and all claims. Only fixture calendars/attack spacing were
advanced; no live clock, schedule, gameplay rule or executable was changed.

Aggregate earning coverage is **6/10**. Next: stage7's1300 Chi score and10000
JiangHu score. Native-client acceptance and live deployment remain pending. The
unrelated private vote pulse still reports PostgresException; it is not covered
or repaired by these results.


## Retained stages7 and8 earning (2026-10-03)

- [x] `career-main-seventh-earn/reload` passed.
- [x] `career-main-eighth-earn/reload` passed.
- [x] Retained ordinary career4294907001 advanced Chi through actual unlock/study
  packets, starting with its earned597 score. Each paid roll checked its exact
  point debit; necessary refills used the ordinary CP-priced action. The adapter
  retained attributes using normal lock bits, with no RNG override or score/blob
  insertion. Stage7 reached1379 across four powers; stage8 reached1407.
- [x] Actual JiangHu creation and paid2702 training advanced the same character
  above10000, then20000 saved strength. Every training checked catalog CP cost
  and20 Study Points credit. Normal random rolls and legal active stages were
  used; no star/strength state or currency was supplied. Below-threshold claims
  refused payment.
- [x] Stage702 awarded one bound Senior Training Pill and804 awarded three.
  Normal item use armed them, and actual training consumed their saved blessing.
  Subsequent progression therefore uses earned rewards in the same career.
- [x] Stage7's eleven saved substages qualified one bound3005113 under concurrent
  main requests. Actual use added300 bound CPs once. Stage8's eight substages
  qualified one bound3005114 and actual use added500 bound CPs once.
- [x] Independent cold reloads preserved exact combined career snapshots,
  JiangHu stars/strength, Chi, pill receipts, inventories, currencies, prior
  progression and main/substage/use receipts. Repeated claims changed nothing.

Executable SHA256 remained
`3577CBB7822FC92381011621C2CECC15297172BEE5E549684F46B80E38998F3B`.
The private adapter's Chi keeper threshold became a parameter so later stages
can retain stronger attributes without changing production probabilities.
JiangHu fills available stars, then retrains its weakest available star. These
are bounded scripted progression choices, not native-client control evidence.

Existing fixtures8211/8210 received goods through ordinary warehouse/trade
operations to make reward space; possessions were not deleted. Earlier exact
snapshots are chronological checkpoints, not resettable seeds. The valid P7
application from stage6 qualified807 while its normal duration remained active;
no new soul or purification evidence was manufactured for stage8.

Aggregate coverage is **8/10**. Stage9 next needs904 equipped sockets,905 Chi1450
and906 JiangHu30000;908 must still pass actual equipped-soul eligibility. Stage10,
native-client acceptance and live deployment remain outstanding. No live process
or game binary changed. The recurring private vote PostgresException remains
outside this batch's evidence.


## Retained stage9 earning (2026-10-03)

- [x] The same ordinary Monk career bought two class-compatible basic swords
  with silver, equipped them through normal item requests and opened both
  sockets on each through actual sealed1009 socket requests. Twelve purchased,
  unbound DragonBalls were consumed: one for each first socket and five for each
  second socket. No equipment, socket values, class changes or RNG outcomes
  were supplied. Existing equipment and possessions were retained through
  ordinary warehouse/trade operations with existing peers8209/8208.
- [x] Substage904 refused below18 equipped sockets, including at17, then paid
  exactly three bound728919 Superior Refinery Packs under concurrent requests.
  Empty sockets count. The packs remain owned; this batch does not claim their
  consumption (the independent socket fixture covers that separately).
- [x] A purchased composition stone upgraded the new main-hand sword to at
  least+8 through the ordinary composition handler. Replay consumed nothing
  further. Existing equipment supplied the remaining total-bonus prerequisites.
- [x] Normal paid Chi rolls, with attributes retained at a95% keeper threshold,
  raised the saved total to1467. No production probabilities changed. Substage905
  paid five bound Senior Training Pills; ordinary use and subsequent training
  consumed available pills through their durable use receipts.
- [x] Normal paid JiangHu rolls retrained the weakest available stars until the
  saved strength exceeded30000. CP and Study Point changes were checked on every
  roll. No synthetic star/strength values or controlled RNG were installed.
- [x] All nine saved substages qualified main9 once under concurrent claims.
  Actual use of its bound3005115 added800 bound CPs. Existing career evidence
  qualified the other substages, including the compatible, still-active P7
  boots applied in stage6; those prerequisites were not earned again here.
- [x] Socket earning and completed main9 each passed a separate cold process.
  Exact combined snapshots preserved owned goods, currencies, sockets,
  composition, Chi/JiangHu, reward/use receipts and previous career progress.
  Repeated substage/main claims made no further changes.

The harness's initial immediate second silver purchase was correctly rejected
by the shop's one-second duplicate guard. The adapter now spaces those requests;
continued execution used saved first-sword progress rather than resetting it.
A missing phase allowlist entry was corrected before gameplay began.

Aggregate earning coverage is **9/10**. Stage10 remains next: Chi1580,
JiangHu50000, total equipment bonus120 and permanent compatible P7 purification,
with all other conditions rechecked through the ordinary claim handler. Native
client presentation (including the2832 summary audit) and live release gates
remain pending. Candidate executable SHA256 stayed
`3577CBB7822FC92381011621C2CECC15297172BEE5E549684F46B80E38998F3B`;
no production executable or live process changed. The recurring private vote
PostgresException remains outside this batch's validation.


## Retained stage10 and all-stage closure (2026-10-03)

- [x] Consumed the retained bound Small Permanent Stone plus two normally
  purchased Large Permanent Stones to stabilize the stage6-compatible P7 boots.
  Substage1008 refused before stabilization; replay consumed nothing further.
- [x] Bought ordinary mall composition stones and used actual2036 composition
  requests to raise both stage9 swords to+12. Paid Super gems filled their empty
  sockets through normal embedding requests. No equipment stats or materials
  were granted. Substages1001/1006/1007/1008 claimed once: Battle Power350,
  sixteen Super gems, total equipment bonus120 and permanent compatible P7.
- [x] Normal paid Chi rolls with a99% attribute keeper reached1584 total score.
  Substage1003 granted exactly ten bound Senior Training Pills under concurrent
  claims. Ordinary item use armed their effects for subsequent training.
- [x] JiangHu progressed from30140 with4740 Study Points to at least50000 using
  normal paid rolls. The adapter selected stars by possible quality/alignment
  gain, then used the normal paid restoration request when a roll did not
  improve total strength. Each roll/restoration checked exact CP/Study debits
  or gains, restored star identity and resulting strength. No star, score,
  probability or RNG result was supplied; this tests server progression, not
  native automated play. All progression remains bounded.
- [x] The remaining saved substages qualified main10 under concurrent claims.
  Its bound3005116 was actually consumed for exactly1000 bound CPs once.
- [x] Equipment and full main10 each passed independent cold processes.
  The final all-stage audit proves ten Done main stages,79 saved substages,
  exactly89 non-legacy claim receipts, ten consumed main packs and3080 total
  bound CPs from those main packs. Replaying all79 substage requests and ten
  main requests preserves the exact combined career snapshot.
- [x] Constructed all ten existing2831 page replies from restored state: each
  carries Done=1 and its actual saved substage count. This is serializer evidence,
  not client-rendering acceptance.

Existing prerequisite qualifications retain their historical provenance; this
closes the retained aggregate workstream, not every full-career achievement or
an entirely unsupplied new character journey. Owned goods were preserved through
ordinary storage/trades with existing peers8207/8206. Equipment prizes remain
owned unless explicitly consumed above. Earlier snapshots are chronological
checkpoints and must not be reset/replayed after advancing this career.

The additional all-stage audit passed in both an earn-labelled resumed process
and a cold reload after the progression run; it did not reroll or repay saved
completion. The harness's first compile found an unavailable packet ToArray
method; the restoration request now uses the existing mode10 frame layout.
This affected only the private adapter, before any new gameplay ran.

Executable SHA256 remained
`3577CBB7822FC92381011621C2CECC15297172BEE5E549684F46B80E38998F3B`.
No production executable, client patch or live process changed. The unrelated
private vote PostgresException remains outside this batch's evidence.

### Progress-summary source audit

The current C# tree has no static call site for `WayofHeroes.SendDone` and no
other2832 sender; the method still contains a fabricated four-completed count
and zero claim indicator. It must not be enabled as an accurate summary.
The actual2830 page-query path returns2831 using saved `StageInfo`, which the
private serializer checks above cover. Client2832 semantics and whether the
native interface needs that summary remain unverified; retain native acceptance
rather than inventing a wire contract or connecting this dormant sender.

Retained main-stage earning coverage is **10/10**. Native presentation and
coordinated release remain separate open gates. Continue the roadmap's retained
career and ordinary-access work; this is not full roadmap completion.
