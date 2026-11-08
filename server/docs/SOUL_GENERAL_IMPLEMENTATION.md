# Soul of the General implementation

Current status (2026-10-01): this chain is included in the completed private
[nineteen-chain earning/restart validation](EQUIPMENT_QUEST_AGGREGATE.md).
The approved Revora10931 evaluator is implemented and tested; older statements
below that it is disabled or that no process restart was tested are historical.
Native/authenticated-client acceptance and live deployment remain pending.

## Evidence and intended route

The installed, hash-checked 6711 journal defines missions 1751?1757, level
55?75, any class. Derived metadata is in
[SOUL_GENERAL_CONTRACT.json](research/SOUL_GENERAL_CONTRACT.json).
`tools/audit_soul_general.py` rejects changed eligibility, predecessors,
NPC endpoints, objectives, reward identity, missing stages and duplicate fields.
The original quest resets after rebirth. Revora will use level 55 minimum with
no upper cap, once per rebirth 0/1/2, matching the preceding equipment quests.

Sources reviewed 2026-09-26:
- https://co.99.com/guide/quests/soulofthegeneral.shtml
- https://co.99.com/guide/npcs/bonusnpc.shtml

The official guide confirms the three wishes, illness investigation and phantom
encounter. Its Amber location differs from 6711; NPC placement will follow the
client only after terrain and collision validation. Guide and NPC index also
conflict on Zhao/Guard coordinates. Do not silently reuse those older positions.

| Role | Client ID | Map and position |
|---|---:|---|
| Chang | 8796 (Revora uses 900071727) | 1002 473,300 |
| General Amber | 4487 | 1020 865,540 |
| General Zhao | 4488 | 1002 368,251 |
| Guard Li | 4490 | 1002 372,251 |
| Painter Feng Kang | ID/model still to verify | 1020 556,571 |

Planned persisted steps: Chang referral; Zhao hair 721703; Amber hair exchange
and SilkPack 721704; collect scent counters 1 SnakeMan, 10 RockMonsterL42,
10 GiantApeL53; Amber hand-in; painter Portrait 721705 and Amber hand-in;
Zhao illness conversation before Guard Li; owned Jade Phantom encounter;
PaperFigure 721706 to Zhao; Chang final claim. All quest material exchanges
must track exact owned UIDs and recover lost items without copying stored items.

The journal specifies 4,500 silver total and two-thirds of an EXP ball, plus
pack 728244. Intended Revora final claim also includes one bound P7 soul under
the approved one-time-quest reward policy. No daily reward is added.
Official scent drop odds, phantom population and combat attributes are not
published. Any chosen odds/profile must be explicitly documented as Revora
rules; monster family IDs, terrain and NPC models still need local verification.

## Completed prerequisite, 2026-09-26

- [x] Seven-stage installed-client contract and five quest/pack identities audited.
- [x] Pack 728244 opens through ordinary item use into permanent level-60 Elite
  CrocodileBoots 160118, +0, preserving source binding. No class restriction.
  Client catalog confirms identity/level; runtime validates loaded catalog too.
- [x] Build and 434 checks pass (406 shared prerequisites plus 28 pack checks).
  Tests cover ten class choices, ordinary dispatch, replay, exact UID/receipt
  persistence, concurrent stack use, both binding states, full bags, database
  failure rollback and retry after reload. Three contract tests also pass.
- [ ] NPC/material/counter persistence and actual earning route.
- [ ] Owned phantom encounter, final rewards and journal integration.
- [ ] Native/Host acceptance, deployment and signed client journal publication.

Evidence: `obj/soul-general-pack-build.log`, `obj/soul-general-pack-tests.log`.
Pack support does not establish quest completion. Achievement 10931 stays disabled.
Nothing deployed; installed client files unchanged.

## Investigation persistence checkpoint, 2026-09-26

- [x] Persisted acceptance through Guard Li's referral, keyed by character and
  rebirth, with frozen P7 selection and five one-time 100-silver payments.
- [x] Exact bound hair/silk/portrait exchanges, storage-aware lost-item recovery
  receipts, owner row locking, context rechecks and atomic item/payment updates.
- [x] Saved scent counters, capped at 1/10/10. Loaded server families verified:
  Snakeman 13 (1020), RockMonsterL42 106 (1011), GiantApeL53 70 (1020).
  Credit requires a genuine registered nearby death, killer ownership and the
  exact tracked silk pack. Replayed corpses cannot add scent or become eligible
  after an earlier rejection. Revival explicitly resets that runtime marker.
- [x] Build and 516 checks pass (406 shared prerequisites + 110 investigation).
  Cases include rebirth 0/1/2, ordered steps, payment replay, missing materials,
  full bags, warehouse refusal, silk-insert failure rollback, recovery, wrong
  monster/map, pre-stage deaths, capped counters and persistence after reload.
- [x] Conservative shared cardinal paths pass for Chang, Zhao, Guard, Painter,
  Amber, four nearby candidate phantom cells and the two Ape City hunt landmarks.
  This proves terrain connectivity only, not native navigation or occupancy.
- [ ] NPC dispatch, global death/revival hooks and saved journal wiring.
- [ ] Owned Jade Phantom encounter, paper hand-in and final reward claim.

Explicit Revora rule: each qualifying scent kill gives one credit (100%); the
original guide does not publish its odds. Targets/counts follow the 6711 journal.
Tests call the persistence component with owner locks and synthetic valid NPC
context, using actual combat deaths for scent fixtures. This is not yet an
ordinary NPC earning path. The future coordinator must own session leases,
exact NPC context and post-commit reload fencing.

JadePhantom is present in the local client monster metadata but absent from the
loaded server monster catalog. A dedicated owned encounter profile is still
required; do not claim it exists from client metadata alone.

Evidence: `obj/soul-general-store-build.log`, `obj/soul-general-store-tests.log`,
`tools/audit_soul_general_terrain.py`, `research/SOUL_GENERAL_TERRAIN.json`.
Nothing deployed. Native/Host gates and achievement 10931 remain unchanged.

## Owned phantom and rewards checkpoint, 2026-09-26

- [x] Bounded owned outdoor Jade Phantom runtime (family 9917510), with ordinary
  attack/damage/death guards and stale/foreign actor rejection. It uses four
  audited cells around Amber, rejects occupied cells and retries population.
- [x] Durable attempt/victory receipt before bound PaperFigure delivery. A full
  bag retains victory for later recovery; an abandoned attempt retains cooldown.
- [x] Exact paper recovery/hand-in, 4,000 silver and two-thirds EXP-ball progression
  in one transaction. Final stage 12 grants bound boots pack 728244 plus the
  bound P7 frozen at acceptance. Total quest silver is 4,500.
- [x] Build and 563 checks pass (516 investigation/prerequisites + 47 encounter
  and reward checks). They cover rebirth 0/1/2, remote/duplicate starts, departure,
  cooldown, stale/foreign combat, normal owner kill, full-bag saved victory,
  paper insertion failure/retry, lost-paper recovery, replayed hand-ins,
  P7 insertion rollback and exact bound rewards surviving reload.
- [ ] Ordinary NPC admission, scheduled/login coordination and journal wiring.
- [ ] Native model/attack animation/AI/companion and actual Host restart acceptance.

Explicit Revora encounter rules: one phantom per owner, at most 32 owners,
60-second reuse cooldown, five-minute lifetime; attack 400?600, defence 150,
melee range 2 and no inherited normal loot. Level 60 and 13,805 HP match the
installed client JadePhantom profile (TypeID 2149). That TypeID is not verified
as a mesh. The current model candidate is General Amber's simple object 271,
matching its disguise role; original phantom mesh and native combat animation
are unverified. This candidate must pass native acceptance before deployment.
NPC appearance candidates and exact metadata hashes are recorded in
[SOUL_GENERAL_APPEARANCE.json](research/SOUL_GENERAL_APPEARANCE.json).

Tests invoke the runtime directly and pulse cleanup explicitly; reward methods
use synthetic valid NPC context. They do not prove NPC access, automatic
login/tick recovery, companion fighting, five-minute expiry or Host interruption.
These remain integration/acceptance work, not completed gameplay claims.
Evidence: `obj/soul-phantom-build.log`, `obj/soul-phantom-tests.log`.
Nothing deployed; achievement 10931 stays disabled.

## Ordinary route integration checkpoint, 2026-09-26

The source now connects ordinary Chang/Amber/Zhao/Painter/Guard dialogue,
normal scent-death and revival hooks, scheduled phantom cleanup, login recovery
and persisted journal missions 1751?1757. No GM/account allowlist is required.
Painter is assigned Revora NPC ID 4489; his 6711 model is 672. Brief NPC dialogue
shows the saved scent counters; original per-objective client counters are not
claimed, and private journal directions replace them with the persisted route.

The integrated fixture found that `Map.AddNpc` blocks Amber's eight neighboring
cells. The prior four adjacent phantom candidates could not spawn once Amber
existed. The encounter now uses (863,538), (863,542), (867,542), beyond that
footprint. The fourth proposed replacement (867,538) was rejected by the shared
client/server terrain audit. Tests explicitly check all three remaining cells
after spawning Amber. This supersedes the earlier four-cell candidate layout.

- [x] Ordinary NPC earning, actual combat callbacks and journal request protection.
- [x] Scheduled five-minute expiry, replacement-session/login recovery and saved
  full-bag victory recovery through ordinary NPC dialogue.
- [x] Private journal overlay composed over Frantic Monkeys; idempotence and
  unrelated-section byte preservation verified; installed inputs unchanged.
- [ ] Native NPC/model/animation/AI/companion/journal acceptance, actual Host
  restart, deployment and signed client publication. Achievement 10931 disabled.

Private overlay: `obj/chi-refill-validation/soul-general-journal/manifest.json`.
SHA-256: `c89ba95366e9a78c14f6cf98c0364cbe5a8e83078c242b40b13603e67e600105`.
The phantom model remains the documented Amber disguise candidate, with native
combat animation unverified. Nothing deployed.
Next independent roadmap root: The Mad Man (1781).

Build and **487 checks pass** (406 shared prerequisites + 81 ordinary-route
checks). Route cases cover rebirth 0/1/2, remote/forged requests, referral order,
actual scent combat/replay, reload, correct NPC-footprint spawn cells, scheduled
expiry, reconnect cleanup/retry, full-bag victory, final-claim rollback/fencing,
level-cap handling, 4,500 total silver, bound pack/P7 persistence and normal
boots-pack use. Logs: `obj/soul-quest-build.log`, `obj/soul-quest-tests.log`.


## Actual startup catalog prerequisite (2026-09-27)

The current isolated game revealed a catalog mismatch outside the ordinary route
fixture: existing Amber4487 used map1020 (879,562), mesh6700. The quest correctly
requires the reviewed 6711 position (865,540). The startup guard remains intact.
A parameterized private-candidate-only correction now accepts that exact old
preimage or the already-correct row; two actual game/database cycles pass after
it. Production remains unchanged. Deployment must review the destination catalog
and stage the matching correction; fixture success alone was insufficient to
prove startup compatibility. [Evidence](ROADMAP_STARTUP_ACCEPTANCE.md#current-committed-gameplay-startup-2026-09-27).

## Phoenix population and earned process route (2026-09-30)

The same-character equipment journey stopped at stage4 with snake=1, rock=0,
ape=0. Read-only inspection of the copied database found family106
RockMonsterL42 only in three map1025 spawn rows; no map1011 row existed. The
quest requires that family in Phoenix Castle, so prior synthetic-monster tests
had not established that ordinary players could finish this step.

Map loading now supplies twenty ordinary family106 monsters on map1011 when
that family is absent. This is a documented Revora population repair, not an
original spawn-count claim. Placement scans open monster cells in the bounded
901..945 / 541..585 region, retaining the catalog's combat/drop profile and the
normal map monster lifecycle with a thirty-second respawn setting. Existing
family106 populations are preserved, including dead bodies, and map1025 is
untouched. The helper checks the expected level42 non-boss family and terrain
SHA-256 `7E8F7A8262F8AE93A314699AF6D276F0324FFF6C32BCC6061BFFC2F336F1E817`.

The private eight-chain process fixture verifies twenty loaded monsters and
idempotent population setup, earns ten rock kills alongside the snake and ten
apes, follows ordinary NPC referrals, defeats the owned phantom and claims
the final bound pack/P7. Exact quest receipts, currencies and all accumulated
items survive game/database restart and repeated final NPC claims. Combat uses
supplied lethal damage through production handlers; phantom cleanup time is
controlled. Natural respawn timing and native-client appearance are not claimed
by this fixture. See [aggregate validation](EQUIPMENT_QUEST_AGGREGATE.md).

Build and both process phases passed with executable SHA-256
`E9607BE43A45ECAE234D49C2D74C54993B94908BCBF4F423B0023E87A277CCA2`.
No live deployment was performed.

## Retained Soul General journey (2026-10-02)

The retained Water Taoist follows Chang option101, Amber4487, Zhao4488, Painter4489
and Guard4490 through ordinary NPC handlers and the Twin City/Phoenix/Ape portals.
Calculated attacks defeat one family13 monster, ten family106 RockMonsters and
ten family70 monsters before the portrait and illness referrals. The existing
Phoenix population is verified as twenty RockMonsters; ensuring it again adds
none. The owned Phantom9917510 is defeated through calculated combat and normal
quest cleanup delivers paper721706. The five interim payments total500 silver.

Full game/database restart preserves the paper, completed Phantom ledger and
nested career. Zhao consumes the paper and pays4,000 silver; Chang delivers
bound728244 plus a bound P7 soul. A second restart preserves both exact reward
UIDs,4,500 total silver and stage12. Repeated final claim changes nothing.
`career-retained-soulgeneral-{hunt,claim,reload}` all pass on candidate SHA-256
`34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`;
probe `revora_retained_soulgeneral_probe` finishes at step2. Fresh read-only
aggregate evaluation confirms7/19 final receipts, twelve remaining.

Starting qualification/funding retain earlier provenance. Proximity, attack
cooldowns, respawn timing and Phantom cleanup clock are controlled; damage,
materials, quest stages and receipts are not directly assigned. Native acceptance
remains pending. No production build, live deployment or client release changed.
