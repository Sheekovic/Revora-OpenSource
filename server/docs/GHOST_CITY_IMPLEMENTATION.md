# Ghost City implementation

Current status (2026-10-01): this chain is included in the completed private
[nineteen-chain earning/restart validation](EQUIPMENT_QUEST_AGGREGATE.md).
The approved Revora10931 evaluator is implemented and tested; older statements
below that it is disabled or that no process restart was tested are historical.
Native/authenticated-client acceptance and live deployment remain pending.

## Evidence and intended route

Installed 6711 journal missions 1787-1794 require level 75-95, any class.
Revora targets minimum level 75 without an upper cap, once per rebirth 0/1/2.
[Verified contract](research/GHOST_CITY_CONTRACT.json) includes all eight
predecessors/endpoints, three ordered firework counters and Powder/Bezoar checks.
Each source section has exactly two identical `ActivityType=0` fields; only that
known duplication is accepted. Conflicting/extra duplicates fail validation.

[Official guide](https://co.99.com/guide/quests/theghostcity.shtml), reviewed
2026-09-26, supplies the two-city rescue: find Mo Mo, investigate a pillar, then
return with Shine's help. The battle involves 100 ordinary foes followed by
Kid Hunter and Kid Seizer. Other children may receive the Bezoar once each for
one-sixth EXP ball before rescuing Mo Mo. The guide specifies a ten-minute
Mo Mo reappearance. Original private/shared scene ownership and encounter counts
per monster family still require reconciliation with local evidence.

| NPC | ID | Map and position |
|---|---:|---|
| Chang | 8796 (Revora 900071727) | 1002 473,300 |
| Aunt Zhang | 4503 | 1002 454,540 |
| Jerk Wang | 4504 | 1002 468,309 |
| Taoist Shine | 4505 | 1002 440,334 |
| Mo Mo | 4526 | 1780 145,134 |

The installed journal marks fireworks at **440,308; 443,344; 465,356**, in that
order, in Twin City. These differ from the guide's older positions. Revora uses
the installed 6711 positions, with a three-tile interaction radius. Terrain and
native navigation acceptance remain open; location-ledger tests are not evidence
of native path reachability.

Items: MagicFirework 721717, Powder 721712, Charm 721713, Bezoar 721714 and
Level87EliteArmorPack 728246. The journal totals 6,600 silver and one EXP ball
of direct experience. The guide additionally lists one EXP Ball (E), which is
planned as bound item 723911 at final claim, alongside the approved bound P7
one-time-quest bonus. Final earning and reward transaction are not implemented.

Map 1780 is referenced by the journal; 1779 and 1780 both bind to `slpk.7z` in
installed client/runtime indexes. Native DMap and server terrain are present,
320x320, with recorded hashes. [Presence evidence](research/GHOST_CITY_MAP_CANDIDATES.json)
is not completed layout, encounter or native acceptance.

## Investigation and armor-pack checkpoint, 2026-09-26

- [x] Strict eight-stage journal/five-item contract; three deterministic contract
  tests reject changed objectives, coordinates, eligibility and duplicates.
- [x] Ordinary fixed-pack handler opens 728246 into class-appropriate permanent
  level-87 Elite +0 armor, preserving the source pack's binding. All supported
  classes have exact level-87 catalog entries; unsupported classes retain the pack.
  [Class mapping](research/GHOST_CITY_ARMOR.json) is a documented Revora selection,
  not a recovered original pack script.
- [x] Durable acceptance freezes the P7 selection per character/rebirth.
  Aunt referral, Wang's tracked bound firework, three ordered location credits,
  report/firework consumption and Shine's tracked bound Charm persist atomically.
  These four referral payments total 400 silver, paid once.
- [x] Lost firework/Charm recovery checks exact UID across storage. Replacements
  preserve visited firework spots; stored items cannot be copied. Full bags and
  failed inserts retain items, entitlement, stage and unpaid silver.
- [x] Build and 486 focused checks pass (406 shared prerequisites + 29 pack + 51
  investigation checks), plus the three contract tests. Coverage includes ten
  classes, pack replay/concurrency, bound outputs, rebirth 0/1/2, ordered/refused
  locations, legacy-coordinate rejection, exact UID, storage, loss/reload,
  insert failure rollback and frozen reward/progress/payment persistence.
- [ ] Firework display and ordinary NPC/item/journal coordinator.
- [ ] Ghost/Dark City terrain routes, bounded owned visits, Mo Mo/pillar evidence,
  Powder progression, child rescue, combat stages and final rewards.
- [ ] Ordinary full-route checks, native/Host acceptance, deployment and signed
  client publication. Achievement 10931 remains disabled.

The store tests supply locked ordinary owners and synthetic valid NPC context;
only pack opening is wired to ordinary dispatch in this checkpoint. No live
schema, client, deployed Host or website changed.
Evidence: `obj/ghost-investigation-build.log`, `obj/ghost-investigation-tests.log`;
`python -m unittest discover -s tests -p test_ghost_city_contract.py`.

## First private visit checkpoint, 2026-09-26

- [x] Shared client/server terrain audit for map 1780: entry 150,150, Mo Mo
  145,134 and pillar 162,217. NPC 3x3 footprints included; walking legs are
  27/102/77 steps. The Twin City return tile 438,334 is clear in both grids and
  outside Taoist Shine's footprint. [Terrain evidence](research/GHOST_CITY_TERRAIN.json).
- [x] Bounded owned scenes, at most eight, with no inherited monsters/portals.
  Mo Mo uses catalog model 565; the pillar uses model 577 (FiendAltar), an explicit
  Revora candidate. Original quest rendering is unverified. Scene release checks
  actor/metadata identity and refuses occupied or foreign-owned contents.
- [x] Actual guarded teleport from any marked firework location requires the
  earned, owned bound Charm and stage 5. Naked/foreign entry and stored Charms
  cannot enter. Visit receipts retain history, with at most one active visit
  per character. Refused admission can release an unsaved reservation.
- [x] Original nearby Mo Mo must be met in this exact visit before the original
  nearby pillar can grant one bound tracked Powder. Prior-visit meeting credits
  cannot be reused. Powder, stage 6 and visit receipt commit together.
- [x] Successful powder collection marks the scene for return. Timeout, departure,
  death, disconnect and replacement-session cleanup use the owned lifecycle.
  A suppressed visit-retirement write preserves the unresolved scene and requires
  reconnect; it cannot silently release a still-active durable visit.
- [x] Missing Powder and Charm recover one exact UID at a time, without copying
  stored items. Shine's inspection grants 100 silver once (500 total so far),
  moves to stage 7 and retains both materials for the Dark City visit.
- [x] Build and 562 focused checks pass (486 prior + 76 visit checks). Coverage:
  ordinary eligible owners across rebirth 0/1/2, exact NPC ownership/proximity,
  visit ordering/retry, full bag, powder insert rollback, exact deadline,
  offline/replacement recovery, suppressed retirement recovery, earned return,
  stored/lost materials, inspection and saved second-city entitlement.
- [ ] Dark City combat/child rescue and final reward flow.
- [ ] Ordinary NPC/item/journal coordinator plus global startup/login/tick wiring.
- [ ] Native NPC/pillar/terrain/companion acceptance, actual Host restart,
  deployment and signed client publication.

Revora uses a private copy of the journal's city, a **20-minute visit limit**,
retains the Charm on interrupted trips, and returns automatically to Shine after
collecting Powder. These are explicit Revora lifecycle choices; the original
source does not establish those ownership/timeout/automatic-return rules.

Tests invoke the store/lifecycle methods with locked ordinary owners and supplied
context, including real Entity.Teleport. They do not prove ordinary quest-item or
NPC dispatch. No claim of native playability or actual process restart is made.
Evidence: `obj/ghost-visit-build.log`, `obj/ghost-visit-tests.log`,
`tools/audit_ghost_terrain.py`. Nothing deployed.

### Dark City encounter evidence

- [x] Installed 6711 `monster.dat` and runtime `Monsters.txt` agree on all
  four encounter display profiles. `KidCatcher`/`KidRipper` have 1,500 HP,
  `KidHunter` 5,000 and catalog-spelled `KidSiezer` 10,000; all are level 75.
  The last two use 200% zoom and StcType 10. All four have Boss=0; these
  profiles do not justify enabling a long boss HP bar.
- [x] Nine Revora spawn candidates are reachable from the existing entry in
  the intersection of server and native terrain, including current NPC
  footprints. Adding rescue children requires another footprint check.
- [ ] Owned combat, durable kill receipts, ordered boss progression and
  earned Bezoar delivery. Existing guide wording does not establish whether
  100 kills means combined or per family; the implementation must document
  its Revora interpretation. Attack/defence values are not in this evidence.

Evidence: `tools/audit_dark_city.py` and
`docs/research/DARK_CITY_ENCOUNTERS.json`. The audit ran against the installed
client and runtime and verified reversible catalog decoding, exact hashes,
matching selected profiles and all nine routes. These are static checks;
Dark City combat and native rendering remain incomplete. No Host or client
changes were deployed.

### Dark City combat and earned Bezoar

- [x] Owned Dark City admission requires the tracked Charm and inspected Powder;
  stage 8 return requires the earned Bezoar instead. Entry still uses the
  private 1780 terrain and the existing 20-minute visit limit.
- [x] At most nine ordinary foes, 100 combined KidCatcher/KidRipper kills,
  then one KidHunter and one KidSiezer. Owned damage/death guards reject
  unrelated players, stale bodies and unregistered monsters. The owner's
  registered companion is accepted by the guard; native companion acceptance
  remains pending.
- [x] Kill receipts keyed by visit and monster UID commit with progression.
  Counters persist per rebirth; interrupted visits remove owned actors without
  resetting earned kills. A failed save fences the session rather than granting
  unsaved progress. Monsters do not generate ordinary loot.
- [x] After both bosses, tracked Powder exchanges atomically for one bound
  Bezoar, including at full bag capacity. Item insert failure rolls back the
  exchange. Lost Bezoar recovery requires saved completion and absence of the
  old UID everywhere; stored items cannot be copied.
- [ ] Child rescue, Mo Mo feeding, final rewards and ordinary coordinator.
- [ ] Native/Host acceptance and deployment.

Explicit Revora rules: the guide's ambiguous 100-kill wording means **100
combined ordinary kills** here. Partial combat progress survives failed visits.
Attack ranges are 200?350, 300?500, 400?650 and 500?800 respectively; defence
is 150, attack interval 1.5 seconds, melee range 2 and view range 18. These are
Revora tuning, not verified original combat statistics. Mesh/HP candidates come
from the recorded 6711 profiles; native rendering remains unverified. Bezoar
replaces Powder through a durable claim rather than a public ground drop.

The feature is still staged: tests invoke the store/lifecycle with ordinary
non-GM characters and exercise actual ReceiveAttack and teleport paths. They do
not prove ordinary NPC/item dispatch or global startup/tick integration.

Validation: build and **623 focused checks pass** (562 prior + 61 Dark City
checks), including rebirth 0/1/2, actual owned combat, foreign/dead-body replay
rejection, ordered bosses, interruption persistence, failed counter-write
rollback/session fencing, failed Bezoar insertion, full-bag exchange and
stored/lost Bezoar recovery. Evidence: `obj/ghost-combat-build.log` and
`obj/ghost-combat-tests.log`. Nothing deployed.

### Child rescue and final rewards

- [x] Five owned Dark City children use verified catalog model candidates
  566?570. Their Revora positions and approaches pass the shared-grid audit,
  including all seven NPC footprints and all nine combat spawn candidates.
- [x] Optional child rescues require the earned tracked Bezoar and exact nearby
  owned NPC. Each credits one-sixth EXP ball once per rebirth, atomically with
  its receipt, respecting the level cap. The Bezoar is retained for Mo Mo.
- [x] Feeding owned Mo Mo consumes the earned Bezoar and Charm, saves stage 9
  and 100 silver, then flags the scene for safe return. Repeat feeding cannot
  pay twice. The scene is released after return.
- [x] Aunt's report saves stage 10, 6,000 silver and one EXP ball of direct
  experience together. Scene retirement must finish before reporting.
- [x] Final claim atomically issues bound 728246 armor pack, bound 723911
  EXP Ball(E), and the bound P7 soul selected at acceptance. Three free slots
  are required; concurrent requests produce one claim and three saved UIDs.
- [ ] Ordinary NPC/item/journal coordinator and global lifecycle integration.
- [ ] Native client, actual Host restart, deployment and signed publication.

These methods remain staged until the ordinary coordinator is wired. Context
callbacks will carry public-NPC proximity; private rescue methods additionally
verify the exact registered scene/NPC themselves. No owner/GM eligibility gates.

Revora lifecycle choices: child placements are custom and optional child credits
persist through visits. Feeding removes the owned scene instead of scheduling
Mo Mo's original-guide ten-minute reappearance. The private completed visit has
no remaining participants; this does not claim to reproduce an original shared
scene. Charm is consumed on successful rescue; Powder was already exchanged
for Bezoar. The one-time quest pays 6,600 silver overall, direct EXP as above,
the two original item rewards and the owner-approved bound P7 bonus.

Validation: build and **687 focused checks pass** (623 prior + 64 rescue/reward
checks). Coverage includes exact private NPC ownership, remote/invalid requests,
optional children, level-capped EXP, suppressed progression writes rolling back
child receipts, suppressed feeding/report writes, retained materials on failure,
full inventory, final P7 insertion rollback, concurrent final claims and reload
persistence across rebirth 0/1/2. Logs: `obj/ghost-rewards-build.log` and
`obj/ghost-rewards-tests.log`. `tools/audit_dark_city.py` also passes with child
footprints included. No live changes deployed.

### Ordinary server route and journal

- [x] Chang options 120/121/122 expose the quest to eligible ordinary characters.
  Aunt Zhang, Jerk Wang and Taoist Shine route earned referrals, recovery and
  final reports; private Mo Mo/pillar/child dialogs use exact owned actors.
- [x] Normal item dispatch handles ordered MagicFirework locations and owned
  Charm admission. Wrong gates release refused reservations. The final owned
  boss automatically exchanges Powder for Bezoar; entry can retry a saved claim.
- [x] Startup initializes stores/NPCs and recovers interrupted visits. Login and
  world ticks use the owned recovery lifecycle. Normal quest actions refresh the
  journal; missions 1787?1794 project saved stages and reject forged Begin/actions.
- [x] Build and **529 focused ordinary-route checks pass** (406 shared + 123
  route checks). Coverage includes ordinary rebirth 0/1/2, level/remote/order
  rejection, real Npcs.GetDialog/ItemHandler/ReceiveAttack paths, expiry, login
  cleanup, retained combat, automatic Bezoar, optional children, all payments,
  failed final insertion/session fencing, reloads and normal armor-pack opening.
- [x] Private composed journal overlay covers all eight missions, modern
  firework positions, twenty-minute visits, combined kill count, saved recovery
  and bound rewards. Exact input hashes, idempotence and unchanged unrelated
  sections are checked by `tools/ClientPatches/prepare_ghost_city_journal.py`.
- [ ] Native NPC/monster/terrain/firework/journal/companion acceptance.
- [ ] Actual Host restart, live deployment and signed client publication.

**Town travel limitation:** the shared-grid walking audit cannot connect Chang
at the audited stand 471,300 to Aunt's free northern stand 454,538. The western
stand is blocked in the native grid. Wang, Shine and all three firework spots
are connected. Server/native town dimensions differ (824 versus 852); the audit
searches only the common bounds. Jump travel and actual native movement are
unverified. The tests position ordinary characters beside NPCs; they do not
prove travel to Aunt. Keep the original placement pending native acceptance or
a separately validated placement/map correction; do not claim native usability.

Source evidence: `obj/ghost-quest-build.log`, `obj/ghost-quest-tests.log`,
`docs/research/GHOST_CITY_TERRAIN.json` and `DARK_CITY_ENCOUNTERS.json`.
Private journal SHA-256:
`c9d5f361b9edfa1946a8b7b3014d894b32679375eebded85da01d1235b77c3d5`.
The overlay is staged under `obj/chi-refill-validation/ghost-city-journal`,
not installed, signed or published. No live schema or Host changes were made.
Achievement 10931 remains disabled until aggregate earning evidence is complete.
Next independent quest root: Tomboy's Marriage (1795).


### Retained career verification (2026-10-02)

- [x] Ordinary retained character 4294907001, life2, follows Chang, Aunt Zhang,
  Wang and Shine; uses fireworks at all three spots, enters with the Charm,
  meets MoMo and brings back the pillar powder before the dark-city visit.
- [x] The owned encounter completes 102 calculated ranged combat kills, Bezoar
  collection, all five child rescues and feeding MoMo, returning to Twin City
  at stage9. The fixture advances the scene clock three seconds between kills.
- [x] A separate process reloads the rescue, reports to Aunt Zhang and claims
  three bound rewards (728246, 723911 and one P7 soul), with exactly 6,600
  silver across the quest. Another process preserves exact saved career/quest
  state and all reward UIDs; repeating Chang's claim changes nothing.

Evidence: `career-retained-ghost-hunt`, `career-retained-ghost-claim`, and
`career-retained-ghost-reload` all passed on private game SHA-256
`34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`.
The fixture controls positioning and time, not kill damage or quest receipts.
Native navigation, rendering and human timing remain pending; nothing deployed.
Retained aggregate9/19, with ten chains remaining; achievement10931 is incomplete.
