# The Mad Man implementation

Current status (2026-10-01): this chain is included in the completed private
[nineteen-chain earning/restart validation](EQUIPMENT_QUEST_AGGREGATE.md).
The approved Revora10931 evaluator is implemented and tested; older statements
below that it is disabled or that no process restart was tested are historical.
Native/authenticated-client acceptance and live deployment remain pending.

## Evidence and intended route

Installed 6711 journal missions 1781?1786 require level 60?80, any class.
Derived metadata: [MAD_MAN_CONTRACT.json](research/MAD_MAN_CONTRACT.json).
`tools/audit_mad_man.py` validates all six stages, predecessors, NPC endpoints,
seven colour counters, fang/powder checks and thirteen item identities.
All six source sections contain two identical `ActivityType=0` keys; this exact
exception is accepted, while changed/conflicting/additional duplicates fail.

Official source reviewed 2026-09-26:
https://co.99.com/guide/quests/themadman.shtml

The guide confirms an ordered toxin recipe: Blue then Green enters Kun Lun.
It also describes five alternative effects, seven stones collected within five
minutes, and combining their powders before returning to Du San. Expiry returns
the player to Ape City. These timed/recipe requirements must not be reduced to
an immediate reward button. Exact map, terrain, stone models and effect seams
still require local evidence. The source does not publish fang drop odds.

| Role | Client ID | Map and position |
|---|---:|---|
| Chang | 8796 (Revora 900071727) | 1002 473,300 |
| Du San | 4468 | 1020 552,601 |
| General Judd | 1611 | 1002 345,251 |
| Pharmacist Dong | 4470 | 1002 431,331 |

Planned route: Chang, Du San, Judd, hunt VenomousFang 729978, Judd inspection,
Dong extraction, ordered use of Red/Green/Blue Toxin 729979?729981, bounded owned
Kun Lun trip, seven distinct powders 729983?729989, combine RainbowPowder 729982,
Du San hand-in, Chang reward. Quest materials must be bound, tracked by exact
UID, with storage-aware recovery and durable prevention of repeated rewards.

Revora uses level 60 minimum without an upper cap, once per rebirth 0/1/2.
Journal reward totals: 5,400 silver and five-sixths EXP ball, plus pack 728245.
The official guide additionally lists one EXP Ball (E); plan one bound EXP ball
and the approved bound P7 one-time-quest bonus at the final claim. The extra EXP
item comes from the guide, not the journal. Reward transaction evidence is below.

## Completed prerequisite, 2026-09-26

- [x] Six-stage installed-client contract and thirteen item identities verified.
- [x] Pack 728245 uses the ordinary transactional fixed-pack handler: permanent
  Elite +0 headgear for the opening class, preserving source binding.
- [x] Class mapping audited against installed items. Most outputs are level 67;
  Archer receives level-62 LeopardHat 113038 because no level-67 Archer hat is
  present (the next is level 72). This is an explicit Revora selection, not a
  recovered original pack script. See [mapping](research/MAD_MAN_HEADGEAR.json).
- [x] Build and 435 checks pass (406 shared prerequisites + 29 pack checks), plus
  three contract tests. Coverage includes ten classes, unsupported-class refusal,
  ordinary use, concurrent/replayed use, binding, exact UID/receipt persistence,
  full bags, database failure rollback and retry after reload.
- [ ] Fang/referral/material persistence and NPC integration.
- [ ] Ordered toxins, all documented effects, timed Kun Lun/stone collection,
  powder combination, cleanup/recovery and final rewards.
- [ ] Saved journal, ordinary full-route verification and native/Host acceptance.
- [ ] Deployment and signed client publication. Achievement 10931 stays disabled.

Evidence: `obj/mad-pack-build.log`, `obj/mad-pack-tests.log`.
Nothing deployed; pack support is not quest completion.

## Investigation persistence checkpoint, 2026-09-26

- [x] Acceptance/referrals, Judd's fang inspection and Dong extraction persist
  per character/rebirth. Four one-time payments total 400 silver before Kun Lun.
- [x] Nearby registered Snakeman family 13 deaths in map 1020 can grant one
  bound tracked VenomousFang. Explicit Revora chance: 50%; original odds unknown.
  Pre-stage/full-bag/failed-roll corpses cannot be replayed for another chance.
- [x] Dong exchanges the exact fang for three bound toxins and their tracking
  rows atomically. Two net free bag slots are required. Failure of any toxin
  insert rolls back the fang, all outputs, tracking, payment and stage.
- [x] Lost fangs require hunting again without repeating inspection payment.
  Missing unused toxins can be replaced once per missing UID with durable
  receipts; stored items cannot be copied. Consumed/selected toxin states are
  excluded from this initial unused-item recovery path.
- [x] Build and 444 checks pass (406 shared prerequisites + 38 investigation).
  Coverage: rebirth 0/1/2, ordered/refused/replayed steps, real combat deaths,
  chance boundary/replay, full bags, storage, lost materials, partial-insert
  rollback and extraction/replacement/frozen-reward persistence after reload.
- [ ] NPC/death/revival integration, ordered toxins/effects and Kun Lun earning.

Evidence: `obj/mad-store-build.log`, `obj/mad-store-tests.log`.
Tests call the persistence methods with locked owners and synthetic valid NPC
context. They do not prove ordinary NPC access or the complete quest route.

[Location candidates](research/MAD_MAN_LOCATION_CANDIDATES.json) record identical
client/server bindings for named Kunlun maps 1712?1720 and available server
terrain hashes. These names do not prove the historical quest map. Terrain,
entry/return and seven-stone route acceptance remain pending. Installed NPC
models are Du San 552, Dong 554, Judd 699 and seven stones 555?561. Native model
rendering remains unverified. Nothing deployed; achievement 10931 disabled.

## Private collection scene checkpoint, 2026-09-26

The nine named Kunlun candidates have neither client archive/DMap files nor
server terrain in the installed trees. Their index entries do not supply usable
maps and do not prove original quest identity. Revora therefore uses a private
copy of verified map-1020 canyon terrain for this trial. This is an explicit
Revora layout, not a claim to reproduce the original Kun Lun appearance.

- [x] Hash-gated shared client/server terrain audit finds a 410-step circuit
  visiting seven distinct stones and returning to entry, with each NPC's 3x3
  collision footprint included. Initial blocked/island/detour candidates were
  rejected. Each chosen stone has open space around its footprint.
- [x] Bounded private-scene allocation: at most eight owners, no inherited
  monsters/NPCs/portals, seven exact stone actors using client models 555?561,
  owner metadata and a five-minute deadline reserved at creation.
- [x] Cleanup requires no occupants or combat actors and refuses replacement
  metadata/stone ownership. Releasing an already released scene is harmless.
- [x] Build and 450 checks pass (406 shared prerequisites + 44 scene checks),
  covering stone models/approaches, owner isolation, duplicate reservation,
  evacuation requirement, replaced-stone rejection, capacity and leak-free release.
- [ ] Ordered toxin consumption/effects, durable visit/collection and actual travel.
- [ ] Deadline enforcement, timeout/login return, collection/combine and final claim.
- [ ] Native visuals/timing/Host acceptance and deployment.

Entry: 553,584. Stones: red 555,561; orange 578,560; yellow 600,602;
green 570,630; blue 549,624; indigo 577,600; purple 600,575.
A 410-step static path does not prove completion speed on a real client.
The scene factory is a prerequisite only and is not yet ordinary quest admission.

Evidence: `tools/audit_mad_terrain.py`,
[terrain and route](research/MAD_MAN_TERRAIN.json),
`obj/mad-scenes-build.log`, `obj/mad-scenes-tests.log`.
No active client, live database or deployed Host changed.

## Ordered recipe and collection ledger checkpoint, 2026-09-26

- [x] First toxin selection persists without consumption; repeating the same
  item does nothing. Selection may be cancelled without destroying or copying
  an item. Stored/locked/unowned selected items cannot be mixed.
- [x] All six distinct ordered pairs map to their documented outcomes. Consuming
  a valid pair records both exact UIDs and a durable pending result atomically.
  Blue then Green additionally reserves the exact owned scene/visit/map and its
  five-minute deadline, entering stage 6. Missing/foreign scenes consume nothing.
- [x] Seven distinct stone credits and exact bound powder UIDs persist per attempt.
  Collection requires the entered owned scene, original stone object, nearby
  active NPC interaction and unexpired saved time. Duplicate clicks cannot earn
  another colour. A missing powder can be replaced within the same active trip;
  an existing item anywhere in storage cannot be copied.
- [x] Using a tracked powder with all seven colours consumes all seven exact
  materials and creates one bound RainbowPowder 729982 in the same transaction
  as stage 7 and attempt completion. Exact five-minute expiry rejects combining.
- [x] Build and 534 checks pass (444 investigation/prerequisites + 90 recipe and
  collection checks). Cases cover rebirth 0/1/2, six-pair classification,
  persisted/cancelled selection, storage, foreign scene, receipt failure rollback,
  duplicate use, full bags, replaced stone, seven colours, missing powder,
  exact expiry, Rainbow insert rollback and saved completion/receipts.
- [ ] Apply/acknowledge poison/heal/strength/float/zombie effects and actual travel.
- [ ] Replenish spent toxins, timed return/abort recovery and lost Rainbow recovery.
- [ ] Ordinary NPC/item/journal paths, final hand-in/rewards and native/Host gates.

The result receipts deliberately remain pending (`applied=false`); classifying
an outcome is not applying it. No visual/status effect or teleport is claimed.
Collection fixtures bind the owner to its private map and mark entry explicitly;
they do not test real travel, scheduled timeout return or process interruption.
No ordinary entry path is enabled by this persistence-only checkpoint.
Evidence: `obj/mad-collection-build.log`, `obj/mad-collection-tests.log`.
Nothing deployed. Achievement 10931 remains disabled.

## Travel and interruption recovery checkpoint, 2026-09-26

- [x] Actual owned entry through Entity.Teleport, durable visit receipt and
  rejection of unrelated/naked admission. Repeated entry is idempotent.
- [x] Five-minute expiry and completed collection return to Ape City 552,599,
  verified clear in both terrain grids and outside Du San's collision footprint.
- [x] Offline saved-location repair; replacement-session recovery preserves the
  entity loader's safe Twin City fallback and retires the old private visit.
- [x] After evacuation and scene release, interrupted attempts atomically remove
  owned partial powders and replace the exact spent Blue/Green pair once.
  Full bags delay refunds without trapping the player; stored/restricted powders
  must be retrieved before refund. Replay cannot duplicate toxins.
- [x] Build and 520 focused checks pass (444 shared/investigation + 76 travel/
  collection). Rebirth 0/1/2, expiry, offline/replaced sessions, full bag deferral,
  successful Rainbow return and durable refund replay are covered.
- [ ] Other five toxin effects, spent wrong-recipe recovery, lost Rainbow recovery.
- [ ] Ordinary NPC/item/journal coordinator and startup/login/tick wiring,
  final rewards, native acceptance, actual Host restart and deployment.

The travel methods are tested directly with ordinary player fixtures. They are
not yet scheduled or exposed by the ordinary quest coordinator. A real process
restart and real-client playability are not claimed. Blue/Green receipts are
acknowledged on travel/recovery; other effect receipts remain pending.
Evidence: `obj/mad-travel-build.log`, `obj/mad-travel-tests.log` and
`docs/research/MAD_MAN_TERRAIN.json`. Nothing deployed.

## Reward transaction checkpoint, 2026-09-26

- [x] Stage 7 Rainbow hand-in requires the exact owned bound powder, a completed
  seven-colour trial and a retired/released visit. It atomically consumes the
  powder, pays 5,000 silver and grants five-sixths EXP ball through the existing
  progression/mentor/reincarnation transaction, then advances to stage 8.
- [x] Lost earned Rainbow can be replaced using a durable exact-UID receipt.
  Existing stored items are not duplicated; full bags preserve entitlement.
- [x] Stage 8 final claim atomically grants bound headgear pack 728245, bound
  EXP Ball (E) 723911 and the bound P7 selected at acceptance. Three free slots
  are required. Stage 9 and all three output UIDs commit with the items.
- [x] Build and 551 focused checks pass (520 travel/prerequisite checks + 31
  reward checks). Tests earn collection before hand-in across rebirth 0/1/2;
  verify storage, loss/reload, full bags, exact XP including capped characters,
  suppressed hand-in rollback, failed P7 insertion rollback, concurrent/replayed
  claims and persisted bound outputs/frozen soul.
- [ ] Other five toxin effects and replenishment after wrong mixtures.
- [ ] Ordinary NPC/item/journal coordinator, startup/login/tick wiring and
  full-route dispatch validation. Native/Host acceptance and deployment remain open.

The reward store is tested with locked ordinary owners and supplied context;
this is not proof of NPC availability. No active client/live database/Host changed.
Evidence: `obj/mad-rewards-build.log`, `obj/mad-rewards-tests.log`.

## Toxin effect and replenishment checkpoint, 2026-09-26

The [official guide](https://co.99.com/guide/quests/themadman.shtml) confirms the
five non-travel outcomes, but supplies no duration/intensity or exact zombie
model. The following are explicit Revora choices, not recovered original rules:

| Ordered pair | Revora effect |
|---|---|
| Blue, Red | Existing Poisoned/ToxicFog path, 2% current HP per ordinary two-second tick; ten ticks including the final clearing tick |
| Red, Blue | Restore current maximum HP once, saved with the receipt |
| Red, Green | Existing Stigma strength flag, configured multiplier 1.15 |
| Green, Blue | Existing Fly flag; no overwrite of riding, flight or transformation |
| Green, Red | Owned cosmetic GreenFaceZombie mesh 4352; no combat transformation |

- [x] All effect receipts have a 20-second window from mixing. Pending receipts
  expire while offline; expired ones are retired without applying a late effect.
  Existing poison/strength/flight are preserved. Healing cannot be replayed on
  later damage, and its durable HP update rolls back if receipt acknowledgement fails.
- [x] Temporary status projection occurs after acknowledgement; reconnects do
  not replay an acknowledged effect. A crash between commit and projection may
  cancel that temporary effect. No exactly-once client display is claimed.
- [x] Zombie uses the shared bounded appearance owner and rolls back provisional
  appearance if its transaction fails. It clears on expiry, leaving Twin City,
  lost eligibility or session replacement. Normal body/class remain unchanged.
- [x] After a handled wrong mixture's window ends, hunting another Snakeman can
  earn a fresh fang at the documented 50% chance. Dong's store operation consumes
  that exact fang and replaces only spent toxins atomically. Unspent/selected
  toxins remain owned; no repeated referral payment or free buff refill.
- [x] Build and 579 focused checks pass (444 investigation/prerequisites + 135
  effect/replenishment checks). All five outcomes are exercised across rebirth
  0/1/2, with replay/reload, existing strength, healing rollback, expired receipt,
  appearance restoration, actual hunt, full bags and refill insertion rollback.
- [ ] Ordinary NPC/item/journal coordinator, startup/login/tick wiring and full
  route dispatch validation remain unfinished.
- [ ] Native zombie rendering, actual timed status/combat behaviour on the client,
  Host restart and deployment remain pending. Tests inspect installed server
  status state; they do not simulate the complete generic status scheduler.

The installed monster catalog identifies GreenFaceZombie TypeID 4352. This is
an explicit Revora candidate, not proof of the original quest's model or native
rendering. [Hashes, rules and source seams](research/MAD_MAN_EFFECTS.json).
Evidence: `obj/mad-effects-build.log`, `obj/mad-effects-tests.log`.
Nothing deployed. Achievement 10931 stays disabled.

## Ordinary server route checkpoint, 2026-09-26

- [x] Startup registers missions 1781-1786 and validates/spawns Du San 4468
  (1020 552,601; model 552), General Judd 1611 (1002 345,251; model 699), and
  Pharmacist Dong 4470 (1002 431,331; model 554). These model/name mappings are
  present in the installed `ini/npc.ini`. Chang offers the quest at level 60.
- [x] Ordinary NPC dispatch enforces proximity, ordered referrals, exact fang
  extraction, selection cancellation, lost-material recovery, spent-toxin
  replenishment, Rainbow hand-in and final reward claim. No GM/name allowlist.
- [x] Ordinary Snakeman combat-death/revival hooks award earned fangs once.
- [x] Item dispatch accepts earned toxins beside Dong, reserves bounded private
  travel for Blue then Green and applies the other outcomes. Unavailable capacity
  preserves materials. Pending effects may be handled at Dong/login; live effects
  are deferred outside Twin City and arena sessions, then expire normally.
- [x] Original owned stone actors grant each colour through ordinary NPC requests.
  Using one earned powder with all seven colours combines and returns immediately.
- [x] Startup recovery, login recovery and periodic travel cleanup are wired.
  Active healthy scene ticks no longer read the run from the database; the saved
  run is read when cleanup needs it. Successful composition marks its scene for
  cleanup before inventory projection, so a failed projection remains recoverable.
- [x] Saved journal states reflect the six missions, with forged quest-start and
  generic quest-action paths blocked. New private client directions are staged.
- [x] Build and 515 focused ordinary-route checks pass (406 shared prerequisites
  + 109 route checks). Coverage includes minimum level/remote requests, forged
  Begin, combat/replay, selection/cancellation, healing/refill, actual private
  admission, full-bag timeout, reconnect/refund, all seven stone interactions,
  combine/return, lost Rainbow, total 5,400 silver, failed final claim rollback,
  final bound rewards, persisted journal and ordinary pack opening after reload.
- [ ] Native NPC/stone/zombie rendering, terrain navigation, five-minute human
  playability, timed buff/combat/companion behaviour and journal display.
- [ ] Actual Host restart, deployment and signed client publication.

The complete ordinary server route is now implemented; it is not deployed.
Achievement 10931 remains disabled because the aggregate quest set is incomplete.
Next independent quest root: The Ghost City (1787).

Private overlay: `tools/ClientPatches/prepare_mad_man_journal.py`, composed over
Soul of the General. Approved DX9/installed/source hashes are checked;
idempotence and byte preservation outside the six Mad Man missions pass.
Staged Questinfo SHA-256:
`26a73fb04a1d2770b31cba6ea6c3094283ebaf0140c77b646a8a167ce26fb37e`.
Nothing installed, signed or published. Evidence: `obj/mad-quest-build.log`,
`obj/mad-quest-tests.log`, `obj/mad-journal-overlay.log`.


### Retained career verification (2026-10-02)

- [x] Ordinary retained character 4294907001, life2, follows Chang, Du, Judd
  and Dong dialogue, calculated Snakeman combat, toxin selection and the owned
  seven-colour trial. Each stone supplies one powder; combining them returns
  the character to Ape City with tracked Rainbow Powder at stage7.
- [x] A separate process reloads that material, hands it to Du, and claims
  three bound rewards (728245, 723911 and one P7 soul) from Chang. Total silver
  is exactly 5,400. The unused red toxin remains a material, not a reward.
- [x] A third process preserves exact saved career/quest state and all three
  reward UIDs; repeat final dialogue leaves the snapshot unchanged.

The fixture deposits spare items through the existing owned house-box service
before starting. It controls the hunt roll, initial placement and return from
home; public quest travel uses portals. This proves the server route and durable
rewards, not natural drop odds or native navigation/rendering. No live deployment.
Evidence: `career-retained-mad-hunt`, `career-retained-mad-claim`, and
`career-retained-mad-reload` all passed on private game SHA-256
`34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`.
The retained aggregate is now 8/19; achievement10931 is still incomplete.
