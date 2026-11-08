# Tomboy's Marriage implementation

Current status (2026-10-01): this chain is included in the completed private
[nineteen-chain earning/restart validation](EQUIPMENT_QUEST_AGGREGATE.md).
The approved Revora10931 evaluator is implemented and tested; older statements
below that it is disabled or that no process restart was tested are historical.
Native/authenticated-client acceptance and live deployment remain pending.

## Evidence

- Installed 6711 journal: missions 1795?1799, type 4, minimum level 78,
  client maximum 98, no class restriction, five ordered stages.
- Chang 8796 (Revora 900071727) at 1002,473,300; Farmer Lynn 4718 at
  1000,461,661; Carolyn 4719 at 1000,460,667.
- Required items: ten Marble 710581; RuneJade 710582, MoonJade 710583,
  SunJade 710584. LeafStone 721951 + MapleStone 721952 yields RuneJade when
  Leaf is clicked; Maple + OceanStone 721953 yields MoonJade when Maple is
  clicked; Ocean + Leaf yields SunJade when Ocean is clicked.
- Journal stage payments: three payments of 100 silver, then 3,000 silver
  and half an EXP ball of direct experience; final pack 728241.
- [Original guide](https://co.99.com/guide/quests/tomboymarriage.shtml), consulted
  2026-09-26: Rock Monster gathering, level-78 variant stones, jade fusion,
  Carolyn's question, level-82 Elite Necklace/Bag and EXP Ball(E), repeat after
  rebirth. Original drop odds, fusion odds and the exact question are absent.
- `tools/audit_tomboy.py` checks the installed journal and item catalog hashes,
  unique sections/fields, ordered endpoints, quantities and reward identities.
  Derived metadata: `docs/research/TOMBOY_CONTRACT.json`.

## Implemented

- [x] Ordinary transactional pack 728241 opening. Known physical classes get
  BasaltNecklace 120158; Taoist classes get JadeBag 121158. Both are catalog
  level 82 Elite, permanent +0, preserving source binding. Unknown classes keep
  the unopened pack. Saved output UIDs and consumption commit atomically.
- [x] Build and **435 focused checks pass** (406 shared + 29 pack checks):
  rollback/retry, duplicate opening, source binding, ordinary class selection,
  reload persistence, unsupported class. Logs: `obj/tomboy-pack-build.log` and
  `obj/tomboy-pack-tests.log`.
- [x] Earned acceptance, tracked monster materials, jade fusion and recovery store.
- [x] Carolyn's question and atomic final reward store including the owner's bound
  one-time P7 bonus. No daily-quest P7 is introduced.
- [x] NPC/item/journal dispatch and ordinary server complete-route validation.
- [ ] Native/client/Host acceptance, deployment and signed publication.

The installed pack tooltip says bracelet/necklace and includes EXP Ball(E),
whereas the guide says Necklace/Bag. This implementation uses the documented
Necklace/Bag and catalog class requirements. The earning flow will issue the
bound EXP Ball(E) separately with the final pack, matching the existing quest
reward convention. A matching private tooltip correction is staged below;
this is not a claim that the original pack script was recovered.

## Remaining gates

Native NPC/hunt/fusion/tooltip/journal and inter-city travel acceptance, actual
Host restart, live deployment and signed publication remain pending. Server
integration is tested below; achievement 10931 remains unproven.

## Gathering, fusion and reward store

- [x] Catalog checks bind gathering to existing Desert City family 16
  RockMonster (level 77) and family 75 RockMonsterL78 (level 78). Both already
  have Desert City spawns; no additional spawn population was created.
  Exact catalog metadata and NPC model candidates 424/425 are recorded in
  `docs/research/TOMBOY_ACTORS.json`. The journal NPC IDs are absent from the
  current public NPC catalog; their coordinator still needs to create them.
- [x] Saved acceptance freezes the bound P7 reward per character/rebirth.
  Lynn's referral, ten-Marble hand-in and three-jade hand-in each pay 100 silver.
  Hand-ins require earned tracked UIDs; donated same-type items do not qualify.
- [x] One attempt per valid monster corpse, owner/distance/map/family checks,
  full-bag protection and caps of ten active Marbles or two of each raw stone.
  Banked or missing-but-unresolved tracked items still count toward those caps.
- [x] Atomic three-recipe fusion consumes the exact clicked stone and a tracked
  matching partner. Success creates one bound jade and receipt; a committed
  miss consumes the pair. SQL failures roll everything back. Existing active
  jade entitlement prevents another fusion of that type.
- [x] Missing Marbles/raw stones are retired from tracking so they can be
  rehunted; earned lost jades recover from successful fusion proof. Nothing is
  replaced while its old UID exists anywhere. Each recovery saves a receipt.
- [x] Carolyn's correct answer grants 3,000 silver and half an EXP ball once.
  Final claim grants bound 728241 pack, bound 723911 EXP Ball(E), and frozen
  bound P7, with three saved output UIDs. Total quest silver is 3,300.
- [x] Ordinary NPC/item/death/revival hooks, saved journal and server full-route checks.
- [ ] Native/Host acceptance, matching tooltip, deployment and publication.

Explicit Revora rules, not recovered original odds: eligible deaths have a
50% drop chance. Either family can give Marbles; only family 75 gives stones,
with uniform selection among the three types and no reroll when a type is
capped. Fusion has 50% success and consumes both stones on a committed failure.
Gathering caps and recovery policies above are Revora inventory controls.
Carolyn asks which stone must be clicked alongside Maple to create Rune Jade:
Ocean, Leaf or Maple; Leaf is correct. Wrong answers cost nothing and may be
retried. Her original question is unverified. Claims repeat once per rebirth.

Store methods remain staged and require the caller's session/owner lock and
proximity context. Tests use ordinary characters but invoke these methods
explicitly; this does not yet prove the public NPC/item earning route.

Validation: build and **524 focused checks pass** (406 shared + 29 pack + 89
store/reward checks). Coverage spans ordinary rebirth 0/1/2, old/wrong corpses,
miss/full-bag replay, unearned materials, per-type caps, stored/lost materials,
failed item insertion, committed fusion misses, concurrent successful fusion,
suppressed hand-in/answer writes, level-capped EXP, full final bag, failed P7
insertion, concurrent final claims and reload persistence. Logs:
`obj/tomboy-store-build.log`, `obj/tomboy-store-tests.log`. Catalog inspection
was read-only (`obj/tomboy-catalog.log`). No live schema or Host changes.

## Ordinary server integration

- [x] Chang options 130/131/132 and ordinary Farmer Lynn/Carolyn dialogs expose
  the earning path without GM/name allowlists. Startup creates the checked NPCs.
- [x] Actual monster death/revival hooks earn tracked resources. Normal item
  dispatch fuses stones and reports success/failure. Ordinary NPC recovery,
  Carolyn answer and final claim use the tested store transactions.
- [x] Five saved mission states project to the journal; forged Begin/action
  packets refresh authoritative state rather than granting progress. Login
  refreshes the saved quest view.
- [x] Build and **481 focused ordinary-route checks pass** (406 shared + 75
  route checks), covering level/proximity/order rejection, all three rebirths,
  monster/item/NPC dispatch, failed fusion, lost jade, paid stages, final claim
  rollback/session fencing, journal, reloads and normal necklace-pack opening.
  Earlier 524 store checks cover transaction and concurrency cases separately.
- [x] Static routes from Lynn's stand to Carolyn and both journal hunting
  points pass in server/native terrain with both NPC footprints. This does not
  validate other live actors, inter-city travel or native movement/rendering.
- [x] Private composed journal and pack-tooltip corrections staged, with exact
  input hashes, idempotent edits and original installed inputs rechecked.
- [ ] Native/Host acceptance and live deployment/publication.

Evidence: `obj/tomboy-quest-build.log`, `obj/tomboy-quest-tests.log`,
`tools/audit_tomboy_terrain.py`, `docs/research/TOMBOY_TERRAIN.json`, and
`tools/ClientPatches/prepare_tomboy_journal.py`.
Private staged artifacts are in `obj/chi-refill-validation/tomboy-journal`:

- Journal: `6fcd7b896089388e4a0778e2a2ab1f444e6fc4bd325eb371b49c17c5f1606600`.
- Item text: `961bae394dc908663e080560aba5e20ba5f46417e6c1eeb156d8687b7686abd5`.
- Item DAT: `a7e2752ee6c7cf0b215423c4f090d077c68ce5674b087bf6680f2f32efa77712`.

Only Tomboy's five journal sections and pack 728241's tooltip are changed;
TXT and DAT inputs are checked independently because the installed files differ.
The tooltip now describes Necklace/Taoist Bag and separately awarded quest EXP
Ball. These private overlays require composition with any other staged client
changes before a release. They are not installed, signed or published. Nothing
deployed; no live schema or Host changes. Next independent root: Desert Blood (1800).

## Earned final claim across actual process restart (2026-10-01)

`career-tomboy-earn`, `career-tomboy-claim` and `career-tomboy-reload` pass on
private full-server SHA256
17976314CE4445B573179FF4AC744F2581196BA7E242D2A6BE2A09C31AF713A6.
An ordinary non-GM level100, rebirth0 character with retained event rewards
accepts through Chang and visits the installed Lynn/Carolyn actors. Ten loaded
RockMonster deaths earn ten tracked Marbles. Six loaded RockMonsterL78 deaths
earn the required stone pairs. ItemHandler.UseItem performs the three ordered
jade recipes, and Lynn consumes the earned jades. Carolyn rejects the wrong
answer, accepts Leaf, and saves stage5, the silver and experience reward.
The three intermediate100-silver awards plus the answer's3000 total3300.

The fixture saves and exits the actual game process abruptly before final claim.
PostgreSQL and game restart preserve exact progress, materials/fusion receipts,
level/experience, purses and prior item rows. Chang then grants exactly three
bound items: pack728241, EXP Ball723911 and the saved P7 soul. The stage becomes6.
Repeated claim requests pay nothing. A second full restart preserves exact item
UIDs/rows, saved soul, receipts and currency/experience values and rejects replay.
`revora_tomboy_process_probe` stores the completed fingerprint.

This is an earned-material/recipe/server-state route. Map transfers, NPC/monster
approaches, lethal damage and successful drop/fusion draws are controlled; it
proves neither normal travel/combat nor statistical odds or native presentation.
The equipment pack remains unopened in this journey, so class-specific pack
opening retains its separate focused evidence. No source production code or
live deployment changed. The separate retained320 career is unchanged.


### Retained career verification (2026-10-02)

- [x] Ordinary retained character 4294907001, life2, accepts through Chang,
  travels by portal to Desert City and visits Lynn. Ten calculated RockMonster
  kills earn ten tracked marbles, then six RockMonsterL78 kills earn two of
  each stone. Item use combines those stones into the three distinct jades.
- [x] A separate process reloads all three jades, hands them in, answers the
  documented Revora question and returns through the portal to Chang. Rewards
  are three bound items (728241, 723911 and one P7 soul), with exactly 3,300
  silver across the quest.
- [x] A third process preserves exact saved career/quest state and all reward
  UIDs. Repeating the final claim leaves the snapshot unchanged.

Evidence: `career-retained-tomboy-hunt`, `career-retained-tomboy-claim`, and
`career-retained-tomboy-reload` all passed on private game SHA-256
`34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`.
The fixture controls drop/fusion rolls and positioning; it does not inject kill
damage or quest receipts. This does not prove natural odds or native rendering.
No deployment. Retained aggregate10/19; achievement10931 remains incomplete.
