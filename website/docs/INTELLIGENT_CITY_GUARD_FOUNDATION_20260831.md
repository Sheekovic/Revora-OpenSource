# Intelligent City Guard foundation evidence — 2026-08-31

## Outcome and evidence boundary

This change delivers and production-deploys the fail-closed persistence and domain-rule foundation for
Wanted Heat, black-name bounty cycles, account Honor Points, Guard Arena sentences, guard configuration,
Commander placement, achievements, audit records, and staged feature gates. It does **not** claim that the
client-visible guard system is complete.

The blocking protocol fact is specific: Revora has genuine 5165 evidence for monster spawn/rendering in the
strict UID range `400001..599999`, and for authenticated player spawn packet `10014`; it has no verified packet
evidence for a fully equipped, player-like, server-controlled entity which is not an authenticated player.
Using a monster/NPC or a fake account would violate the requirement. Consequently all rendering, Commander
dialogue, AI enforcement, global announcements, earning, redemption, and Arena gameplay gates remain disabled.
No new packet layout, mesh, skill, item, map, portal, title, or effect was invented.

## Source and production baseline

- Starting local branch: `feature/genuine-combat-stamina-fix`.
- Starting commit: `ed6e7b68a391de6ac85834aecc0a0d2f811c7b91` (`Record genuine monster targeting deployment`).
- Starting tree: clean.
- Remote fact: `ed6e7b6` is `origin/feature/genuine-monster-targeting-fix`; no same-named remote existed for the
  starting local branch.
- Feature branch: `feature/intelligent-city-guard-5165`.
- Production migration at start: V040, checksum
  `cc688d562e917c5d0f0d98bfa3de696ad4521e571b6f03b91d7fecb81c1fbdeb`.
- Deployed binary hashes at start: Host
  `0140e0ec1a0d42939069ef188c42c22547a0721cdfa47dc8e2ca8b1c17f78e9d`, Admin
  `cf08cd6d43a73046d33a91b90ec220fe890cd4400f15443cc136cf77de3e71c6`, Web
  `019612f8293e4f7b64c08340df15688a9137faf4e96b27ff970469319a83e14c`.
- Host/Admin/Web/nginx/PostgreSQL were active. Host PID `1109735` began 2026-08-30 23:49:01 UTC.
- Production schema after deployment: V041, checksum
  `4803ca11bcf1a322ba7e4096eb14d2ee907eae80d3376e5b52cdc0ca2d16fec1`.
- Pre-migration backup: `/root/revora-game-backups/20260831T001811Z-city-guard/pre-v041.dump`, 1,736,216 bytes,
  SHA-256 `785d66cfb84601912e9b857d02ccafd6545ec5ef54694c2fbec46d6bd1bee503`, mode `0600`.

Concurrent unrelated edits appeared after the clean baseline in existing combat/stamina/playable files. They
were preserved, were not staged as part of this feature, and explain any final non-clean working tree.

## Sources inspected

- `AGENTS.md`, `docs/ARCHITECTURE.md`, `docs/PROTOCOL_RESEARCH.md`.
- `docs/evidence/OWNER_GAMEPLAY_ADOPTION_AUDIT_20260829.md`.
- `docs/LIVE_COMBAT_NATIVE_NINJA_5165.md`, `docs/OWNER_MONSTER_STAMINA_5165.md` and newer production evidence.
- Authorized Owner archive `research/isolated/owner-source-20260829/owner-source.download`, SHA-256 previously
  recorded as `060df1cf72e5be6701b354c6879a68d06c03e941f54deb5d5dfc91051774e9a2`, inspected as text only.
- Owner `Game/Character.cs`: white `0..29`, red `30..99`, black `100+`; blue is an independent timed effect;
  PK decay is one point per three minutes; criminal kills add evidence-dependent 3/5/10 values.
- Owner `Game/Mob.cs`: `HuntBlueNames`, bounded target range, chase/attack delay, death and respawn concepts.
- Owner `Features/Arena.cs`: isolated dynamic duel concepts and sanctioned-PvP evidence. Its implementation was
  not copied.
- Active reviewed Twin City DMap via `/srv/revora-game/data/maps/reviewed-owner-maps.json`.

## Implemented architecture

`CityGuardRules` is a dependency-free deterministic rules boundary. It implements original PK name thresholds;
sanctioned-PvP, self-defense, criminal-victim, and environmental exclusions; Wanted escalation; repeated-victim
and protected-beginner multipliers; guard/service-guard distinction; LOS observation; whole-minute hidden decay;
logout freezing; location forgetting on expiry; black-cycle transition detection; bounty schedule; UTC daily cap;
same-pair 24-hour exclusion; meaningful 20% player contribution; linked-self-farm rejection; reciprocal-farm hold;
spawn-protection termination; bounded tribute; 20% Elite recovery; fallback release; and legal, prioritized,
instance-isolated guard target selection capped at six.

`PostgreSqlCityGuardStore` supplies serializable, retried, parameterized, idempotent Honor boundaries. A bounty
claim, cycle close, balance update, and immutable ledger insert commit together. Replay is keyed at the database
boundary. Status reward redemption locks an enabled catalog row, prevents negative balance, grants authoritative
status progress, inserts its receipt and immutable debit ledger row in one transaction. No catalog reward is
enabled, so production redemption fails closed.

V041 adds:

- `city_guard.character_wanted`, `bounty_cycles`, partial unique active-cycle index.
- Account `account_honor_balances`; append-only-by-permission `honor_ledger`; bounty decision/audit evidence.
- Empty, disabled `reward_catalog` and idempotent `reward_redemptions`.
- Persistent isolated `arena_sentences` and `arena_events` with one active sentence per character.
- `achievement_progress`, `jurisdictions`, `guard_power_tiers`, `guard_definitions`, `patrol_waypoints`, and
  bounded `audit_events` indexes.
- Foreign keys use immutable account/character IDs; correlation keys and active-cycle/sentence indexes enforce
  idempotency. Runtime receives no DELETE grant. Hot combat/movement code performs no new database reads.

The schema is additive. Ledger rollback after real transactions would risk audit integrity, so the safe strategy
is forward-fix plus kill switches; the pre-V041 dump exists for disaster recovery before any ledger activity.

## Exact behavior delivered

- PK Points and Wanted Heat are separate. No existing PK row is reset, rewritten, or deleted.
- Heat is `0..5`, timestamped with PostgreSQL `timestamptz`/`DateTimeOffset` UTC instants.
- Each complete hidden minute lowers Heat by one. Observation resets hidden time; logout freezes after advancing
  to logout time; login resumes from its stored instant. Expiry clears observed map/time only.
- Guard attacks add two Heat; repeated victim and protected beginner each add one additional level, capped at five.
- A service guard is excluded from attack/Arena triggers.
- Bounty awards are 1/2/3/4/5 for PK bands 100–149/150–249/250–399/400–599/600+.
- Account UTC-day cap is 10; same killer/wanted account pair is blocked for 24 hours; linked self-farm is rejected;
  suspicious reciprocal patterns are held; player contribution below 20% is rejected.
- Arena tribute is `min(10,000 + 2,000*level + 100,000*rebirths, 5% of Silver)` clamped to
  10,000..1,000,000. Three defeats or ten minutes enables fallback. A defeat restores 20% of Elite maximum HP.
- Target selection rejects dead, disconnected, protected, wrong-map and wrong-instance players; prioritizes black,
  then current violence, then Heat; caller cap is clamped to six.

Blue-warning incident coordination, combat attribution across direct/skill/DoT/summon/group/simultaneous cases,
PK mutation, pursuit scheduling, LOS/pathfinding, AI actions, arrest, Arena teleport/reconnect, announcements,
Commander dialogue, bounty history UI, live earning/redemption calls, achievements hooks, metrics, and Admin views
are not wired into the running Host. They are therefore **not implemented**, not merely disabled.

## Commander, jurisdiction, guards, and catalog

The requested `1002:456,378` tile is walkable but occupied by production NPC UID `87468`. Hash-pinned DMap
validation found all 25 tiles in radius two walkable; reference-table checks found no other NPC, portal, or enabled
monster there. Final coordinate is adjacent `1002:455,378`. The Commander has stable UUID
`51650041-0000-4000-8000-000000000002`, no guessed client UID, is `service_guard=true`, `attackable=false`, and
`enabled=false`.

A disabled initial Twin City polygon and two data-driven power tiers (`city-veteran`, `black-elite`) exist. Their
numeric values are deterministic balance-test inputs, not active claims of final balance. No patrol is populated;
no Warrior/Trojan/Archer/Fire/Water/Ninja skill set is populated because exact player-like rendering and complete
class skill evidence have not cleared the activation dependency. The reward catalog is intentionally empty.

## Feature flags and deployed state

All are `gate_state=disabled`, `rollout_scope=disabled`: `guards.system`, `guards.player.rendering`,
`guards.wanted.heat`, `guards.blue.warning`, `guards.red.pursuit`, `guards.black.announcements`,
`guards.honor.earning`, `guards.honor.redemption`, `guards.commander`, `guards.arena`, `guards.twin.city`,
`guards.ai`, `guards.emergency.disable`. Dependencies prevent partial activation. No canary/global behavior is on.

## Validation results

- `dotnet build src/Revora.Persistence/... -c Release --no-restore -m:1`: pass, 0 warnings/errors.
- `dotnet build src/Revora.Host/... -c Release --no-restore -m:1`: pass, 0 warnings/errors.
- `Revora.CityGuardTests`: 12/12 pass.
- `Revora.CityGuardMapTests`: `456,378` and `455,378` standable, 25 nearby safe cells, map 972x972.
- V041 applied from scratch to a fresh production V040 snapshot: commit, 13 gates/rollouts, 11 dependencies,
  two tiers, one jurisdiction, one Commander; structural validator V012–V041 passed.
- Existing: ClientDoctor 24/24; Host 15/15; Integration pass; Launcher 43/43; NativeLogin 59/59;
  Protocol pass; Unit 10/10; Web 53/53; FeatureGate 5/5; Gameplay 56/56.
- Admin: 5/6; database-role test could not run as root because peer auth correctly rejected `revora-admin`.
- GameplayPostgreSql: not run; peer auth correctly rejected absent matching `revora-test` OS identity.
- Playable, PublicIdentity, LauncherCampaign, and WPF suites require dedicated arguments/Windows artifacts and were
  not rerun. No assertions were weakened.
- No new pathfinding or active AI exists, so there is no legitimate new load/pathfinding result. Existing post-
  migration Host metric remained healthy: active monsters 49,334, ticks 1,136,190, maximum delay 1,343 ms.

## Deployment and observation

V041 and the clean committed Host build were deployed. The build came from a detached worktree at implementation
commit `74df835`; it therefore excluded the concurrent unrelated edits. Deployed/published Host SHA-256 is
`3bade860257d1ffe04430edba479f4265886131f247bede7d2a1f08cacf86ae1`. The previous app tree is retained under
the timestamped backup directory above. Shutdown drained listeners, flushed persistence, emitted `STOPPED`, and
startup connected PostgreSQL, loaded 189 monster templates/968 generators/49,334 enabled population, brought both
listeners ready, and emitted `READY`. `NRestarts=0`. All five services remained active.

No genuine Windows 5165 client validation was performed for this feature. Because nothing client-visible was
enabled, the furthest safe stage is schema/state foundation with every feature disabled. There is no owner test
procedure yet: a procedure would be misleading until the missing entity representation is statically proven.

## Kill switch, rollback, and next evidence

Do not enable any `guards.*` row. If schema activity is suspected, keep `guards.system` and
`guards.emergency.disable` disabled and inspect `city_guard.audit_events`/ledger. V041 is additive and inert;
forward-fix it rather than dropping immutable ledgers. Catastrophic pre-use recovery may restore the hash-pinned
pre-V041 dump into an isolated database and compare before cutover; never overwrite production blindly.

Highest risk (critical): no verified player-like non-account 5165 representation. Next action: static exact-client
analysis and untouched-client capture of a server-controlled equipped entity, proving UID domain, spawn/update,
movement/jump, status/equipment, attack/skill, removal, and NPC-dialogue compatibility. If impossible, the product
requirement must remain blocked rather than substituted.

High risks: attribution is not connected to combat/death; no persistence coordinator for Wanted/Arena; no LOS or
bounded pathfinding; no live Honor call; no admin/Commander views. Each must be implemented and tested after the
representation boundary, while keeping earning/redemption disabled until concurrent PostgreSQL tests pass under
the proper peer-authenticated harness.

## Requirement matrix

| Requirement area | Status | Evidence / precise gap |
|---|---|---|
| Discovery/source discipline | Implemented and validated | Baseline, deployed hashes, V040, architecture, Owner/client evidence recorded; no legacy executable run. |
| PK white/red/black semantics | Implemented and validated (rules) | 0–29/30–99/100+ tests; no live PK mutation/decay wiring. |
| Criminal kill/self-defense/events | Implemented but not integrated | Deterministic exclusions pass; multi-source combat attribution not wired. |
| Blue warning | Not implemented | Needs incident coordinator, observed LOS, verified chat integration. |
| Wanted Heat | Implemented but not integrated | Domain and schema validated; persistence/runtime coordinator absent. |
| Jurisdiction/perception/pathfinding | Not implemented | Disabled data row only; no LOS/search/pathfinding scheduler. |
| Red/black pursuit and bounty announcements | Not implemented | Blocked downstream of player-like entity protocol and runtime coordination. |
| Bounty cycle and Honor rules | Implemented but not integrated | Schema, rules and serializable store compile; death handler not wired. |
| Honor ledger/redemption | Implemented but awaiting DB harness validation | Atomic code and constraints; empty disabled catalog; no live call. |
| Guard Commander | Implemented but awaiting genuine-client validation (data only) | Final safe coordinate 455,378; service record disabled; dialogue/rendering absent. |
| Guard composition/AI/combat | Blocked with concrete evidence | No verified player-like non-account 5165 entity; no substitution made. |
| Beginner/spawn protection | Implemented but not integrated | Domain expiry/hostile cancellation only. |
| Guard Arena | Implemented but not integrated (state rules/schema) | Sentence/recovery/tribute/fallback persisted design; teleport/combat/reconnect absent. |
| Achievements | Implemented but not integrated (persistence) | Safe keys/schema only; Commander view/hook absent. |
| Persistence/schema | Implemented and validated | V041 snapshot-tested and production-deployed disabled. |
| Flags/canary/kill switch | Implemented and validated | 13 disabled gates/rollouts and dependencies verified in production. |
| Automated/regression tests | Partially validated | Results above; argument/peer/Windows suites outstanding. |
| Observability/Admin | Not implemented | Audit schema only; no emitted metrics/events or Admin views. |
| Canary/client/global deployment | Blocked with concrete evidence | No genuine representation evidence; production remains fully disabled. |

Statuses deliberately distinguish source scaffolding, server integration, production deployment, genuine-client
proof, and activation. This report does not summarize the requested complete system as complete.

## 2026-09-01 release-readiness continuation

The requested starting commit `b9282cafef990d6a052ad430ab34db1b0e89a327` was an ancestor, not the tip of
the named branch. The branch tip was `af558764`; production was running the later V045 recovery-item lineage
from `5823bc6`, while proficiency enforcement existed only on the divergent `c1cda6f` lineage. The release branch
was therefore reconciled first. Commit `a238957` contains both production V045 behavior and proficiency
enforcement; no deployed behavior was discarded.

Static evidence was rechecked against the approved `Conquer.exe` SHA-256
`902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`. The exact packet-10014 receive
handler remains at RVA/VA evidence point `0x589c9d`. The retained Owner Source
`ReVora/Main/Packets.cs::SpawnEntity(Character)` independently writes headwear at `+24`, garment `+28`, armor
`+32`, left hand `+36`, right hand `+40`, steed `+44`, and the evidenced colors at `+92/+94/+96`.

Commit `93c2547` closes the corresponding authenticated-player omission without claiming that it proves a
non-account guard:

* `GamePlayerAppearance5165` is an immutable server presentation value, populated only from authoritative
  equipped item rows.
* `GameplaySession5165` owns that value independently of its socket sender and broadcasts an updated 10014 only
  to peers already in the exact 18-cell screen.
* Equip/unequip actions refresh the snapshot only after the transactional item command and client response
  complete.
* Golden-offset protocol tests and a deterministic two-session world test cover the exact fields and suppress
  duplicate refreshes.
* V046 adds only the missing `revora_game` SELECT privileges for immutable City Guard catalogs. It enables no
  `guards.*` gate.

The continuation after `93c2547` placed authenticated-player equipment presentation behind the independent
`world.player.equipment.visibility` immutable-character canary. The genuine two-client test failed: SHEEKOVIC and
sheeko1 were both fully equipped, but each client rendered the remote character without equipment. Production
audit proved the rollout was asymmetric (SHEEKOVIC legacy UID 2 enabled; sheeko1 legacy UID 3 disabled), while
the approved client handler at `0x589c9d` proved both emitted UIDs were below its 1,000,000 player-entity boundary.
Thus the earlier direct serializer test was insufficient and no authenticated-player equipment success is claimed.

The correction keeps immutable database UIDs unchanged, hydrates both authoritative equipment snapshots, and
uses a pairwise canary-only player presentation UID for 10014 plus matching peer movement/removal updates. It adds
full fixed-region packet tracing and two-equipped-character / equip / unequip peer assertions. This remains
pending the same genuine two-client retest and is **not** evidence that combat or a non-account guard lifecycle is
valid. All thirteen `guards.*` gates remain disabled.

Failure evidence conditions: two genuine approved patch-5165 clients, SHEEKOVIC and sheeko1 on map 1002 within
18 cells, both fully equipped simultaneously. The screenshot itself was not exposed as a local workspace file;
its SHA-256 is therefore `Unknown` until the attachment bytes are supplied to the evidence store.

The relevant production audits are `enabled=True`, character UID 2 at
`2026-08-31T09:39:46.2556809Z` and `enabled=False`, character UID 3 at
`2026-08-31T09:39:55.8372855Z`. Authoritative equipped rows proved both characters had headwear, armor and both
hands equipped; this was not an inventory-state ambiguity. The approved-client function branches at UID
1,000,000, so the emitted immutable values 2 and 3 were invalid player-presentation identities even though they
remain valid Revora database identities. The corrected canary packet trace will preserve the exact bytes on the
next test; the old build did not log them, and deterministic reconstructions are not described as captures.

The remaining City Guard blocker is therefore narrower but unchanged: capture and classify the genuine 5165
client behavior for this equipment-aware peer path, then prove a collision-free non-account entity UID and full
lifecycle before constructing `PlayerLikeGuardEntity`. No fake account, monster/NPC substitution, or guessed
packet was introduced.

V046 and Host SHA `5f354d11d453e8394061ffbd573b0c9b0ff4bc612140998e49b4857ac7a647f1` were deployed after an isolated
restore, production-shaped startup, deterministic publish, and malware scan. The equipment-visibility rollout has
exactly one immutable SHEEKOVIC character canary. This did not change any `guards.*` row: all thirteen remain
disabled, and no guard entity is registered or visible in production.

The peer-equipment correction in commit `46408846a2829bccb3e4e945124a9c80fe7307b9` is now deployed Host-only.
Installed and running Host SHA-256 is
`24f662c78529adf498ea59cad4d0c484e784a8ddbff10c179094dbe2836260fe`; both paths resolve to inode `2391260`.
Rollback is `/srv/revora-game/rollback-peer-equipment-20260831T104114Z`. Release, protocol, database, concurrency,
and complete 17/17 Playable validation passed; the exact Host passed ClamAV. Service verification showed one READY,
zero automatic restarts, and healthy Web/Admin/PostgreSQL/nginx/AMP. This is still
`IMPLEMENTED_PENDING_GENUINE_CLIENT_VALIDATION`: it does not authorize `PlayerLikeGuardEntity`, and every
`guards.*` gate remains disabled until both real clients render the other's authoritative equipment and live
equip/unequip refresh correctly.
