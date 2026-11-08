# Revora public-release readiness — 2026-09-01

## Verdict

`NOT_READY_FOR_PUBLIC_RELEASE`

This verdict is evidence-based. Revora has a production-proven account/world/movement/economy/monster core and a
stable signed launcher, but large patch-5165 gameplay families remain unwired, City Guard cannot safely render a
non-account equipped player-like entity, the approved client still contains visible legacy branding, and no new
Windows client acceptance was performed during this continuation. Compilation or schema presence is not counted
as completion.

## Git and production baseline

The requested commit `b9282cafef990d6a052ad430ab34db1b0e89a327` was an ancestor of the actual named-branch
tip `af558764ce3e7cfd84fe24a84fce328652d4eb3d`. Production ran the V045 lineage from `5823bc6`; the
proficiency fix `c1cda6f` was on a divergent branch. Both were reconciled as `a238957`. The first release-readiness
runtime commit is `93c2547` and the launcher root-safety correction is `6babe83`.

Before this work, `revora-game` was enabled/active with PID 1200309, zero automatic restarts, installed and loaded
inode 2391189, and SHA-256 `cabd37bbfc8cff9f76945cf806c4ecfe84a5d43de6ead63e9f2efb52ce41f6aa`.
The database was contiguous through V045. Web, Admin, nginx, PostgreSQL, AMP, and Game were active. After the
controlled deployment, V046 is active with checksum
`5b000c77491ec8239c1db4818456504bc53435f9532479520634a421cfb697f3`. The installed and loaded Host share
inode 2391212, size 40,447,063, and SHA-256
`5f354d11d453e8394061ffbd573b0c9b0ff4bc612140998e49b4857ac7a647f1`.

Baseline production data digests (count plus MD5 aggregate, not secrets) were:

| State | Baseline |
|---|---|
| characters | `3 / ee1bbc9ca2800ceb6db370898ec602c1` |
| items | `56 / b95b1e6b60ce42bd96e0dfe9da5bdc09` |
| item locations | `22 / 3fa1c33d06e7d283758a97736a503b44` |
| wallets | `5 / f02dcce3d095b4d26fff930a5f084a5c` |

## Owner Source adoption matrix

Each row uses exactly the required classification vocabulary. “Tests” means the strongest currently located
evidence, not an assertion that lower-level tests replace genuine-client proof.

| System | Classification | Revora evidence | Owner Source / remaining boundary |
|---|---|---|---|
| Account login/authentication | `COMPLETE_AND_VERIFIED` | `Genuine5165RuntimeComponent`, `AccountCipher`, PostgreSQL handoff; genuine owner logins | Donor auth/network intentionally excluded |
| Account-to-character ownership | `COMPLETE_AND_VERIFIED` | immutable AccountId/UID constraints V001–V010; PublicIdentity 9/9 | Donor identity model rejected |
| Character creation/selection | `COMPLETE_AND_VERIFIED` | exact 1001/1006 flow; Playable suite | Owner creation behavior adapted |
| World entry/reconnect | `COMPLETE_AND_VERIFIED` | 1006/10010/1110, persistence; owner acceptance | Same-session and existing-character paths |
| Walking/running/jumping | `COMPLETE_AND_VERIFIED` | `GameplayWorld5165`; owner acceptance and soak | DMap-authoritative |
| Maps/DMaps | `PARTIALLY_IMPLEMENTED` | 192 accepted DMaps, reviewed hash catalog | 9 missing DMaps; not every donor map active |
| Portals/map transitions | `COMPLETE_AND_VERIFIED` | 163 live references; owner 1002↔1004 acceptance | 158 staged from 222 donor rows; unavailable maps gated |
| Nearby-player synchronization | `PARTIALLY_IMPLEMENTED` | movement/view lifecycle is owner-proven; equipped peer appearance failed the 2026-08-31 genuine two-client test | corrected canary player-UID presentation requires a repeat genuine test |
| NPC placement/visibility | `COMPLETE_AND_VERIFIED` | 1,377 live NPC rows, exact 2030 path | two donor rows map-incompatible |
| NPC basic dialogs/router | `COMPLETE_AND_VERIFIED` | 2031/2032, guide/shop/warehouse precedence; owner accepted | safe fixed/declarative subset only |
| Donor NPC scripts/actions | `PARTIALLY_IMPLEMENTED` | bounded reviewed services only | 221 executable handlers not adopted; dynamic execution excluded |
| Item definitions | `COMPLETE_AND_VERIFIED` | 8,956 active rows, signed release closure | 8,408 core plus sparse Ninja additions |
| Inventory/item instances | `COMPLETE_AND_VERIFIED` | immutable UID/location constraints and encrypted Host path | owner accepted |
| Equipment/calculated stats | `COMPLETE_AND_VERIFIED` | equip/unequip, proficiency enforcement, owner accepted | peer visuals await two-client acceptance |
| Equipment peer appearance | `IMPLEMENTED_PENDING_GENUINE_CLIENT_VALIDATION` | exact 10014 offsets plus the post-failure player-UID/canary correction | genuine test failed on `17a54c7`; corrected build requires retest |
| Item use | `PARTIALLY_IMPLEMENTED` | V045 reviewed recovery consumables | general donor item handlers absent |
| Repair/composition/quality/sockets | `FOUNDATION_ONLY` | V019 catalog/schema and domain types | no exact Host mutation dispatcher |
| Shops/buy/sell | `COMPLETE_AND_VERIFIED` | 27 shops/1,094 rows; owner accepted arithmetic | 7 shops and 417 custom refs quarantined |
| Warehouse | `COMPLETE_AND_VERIFIED` | open/list/deposit/withdraw; owner persistence accepted | money banking intentionally inactive |
| Currency/economy | `COMPLETE_AND_VERIFIED` | transactional ledger, safe caps, replay receipts | CP/Silver owner-observed |
| Monsters/templates/spawns | `COMPLETE_AND_VERIFIED` | 189 types, 989 generators, 49,362 UID assignments | event/missing-map subsets gated |
| Monster AI/physical/ranged combat | `COMPLETE_AND_VERIFIED` | bounded scheduler, production attacks/deaths | genuine owner combat evidence exists |
| Monster magic presentation | `IMPLEMENTED_PENDING_GENUINE_CLIENT_VALIDATION` | runtime/encrypted fixtures | broad genuine effect coverage incomplete |
| Combat/death/revive | `COMPLETE_AND_VERIFIED` | exact 1022, V038–V045, owner acceptance | supported normal/Ninja subset |
| Drops/ground pickup | `COMPLETE_AND_VERIFIED` | 1101 durable ground path, receipts, owner observations | not every donor drop category activated |
| Skills/spells/proficiencies | `PARTIALLY_IMPLEMENTED` | seven Ninja skills, weapon proficiency persistence | 759-row catalog is not a general skill runtime |
| Natural leveling/stats | `PARTIALLY_IMPLEMENTED` | combat EXP and paid progression | complete class-wide natural reward logic incomplete |
| Class promotion | `PARTIALLY_IMPLEMENTED` | Ninja stages and paid service paths | all class NPC promotions not complete |
| First/second rebirth | `IMPLEMENTED_PENDING_GENUINE_CLIENT_VALIDATION` | V033/V036 transactional service | full generic lineage/client acceptance incomplete |
| Teams | `FOUNDATION_ONLY` | V023 plus in-memory aggregate | no codec/Host/PostgreSQL coordinator |
| Friends/enemies | `FOUNDATION_ONLY` | V023 tables/models | no live packet path |
| Trade/personal shops | `FOUNDATION_ONLY` | V024 tables/models | no runtime settlement/codec |
| Guilds/marriage | `FOUNDATION_ONLY` | V025 tables/models | no live runtime |
| Quests/tasks | `FOUNDATION_ONLY` | V026; packet 1134 only audited | exact client behavior and safe declarative engine missing |
| Events/tournaments/arena/rankings | `FOUNDATION_ONLY` | V027 aggregates/tables | no Host wiring; guard arena also incomplete |
| Housing/furniture/mining/weather | `FOUNDATION_ONLY` | V028 schemas/gates | no Host wiring |
| PK/crime | `PARTIALLY_IMPLEMENTED` | combat PK gate and deterministic guard rules | complete name-color/jail/crime lifecycle absent |
| Chat | `PARTIALLY_IMPLEMENTED` | 1004 parsed; bounded system/GM paths | normal chat/social channels largely dropped after audit |
| GM commands | `PARTIALLY_IMPLEMENTED` | role-bound `@givemeall`, `@clearinv`, receipts/audit | broad donor commands intentionally not enabled |
| RevoraMaster 349,359 | `IMPLEMENTED_PENDING_GENUINE_CLIENT_VALIDATION` | UID 900000001, four paid atomic services | max level/loadout observed; rebirth/full campaign incomplete |
| City Guard | `FOUNDATION_ONLY` | V041 rules/store, 12 tests, coordinate 455,378 | no player-like guard entity/runtime/AI/client proof |
| Launcher/self-update/repair | `COMPLETE_AND_VERIFIED` | 1.0.7 owner accepted; signed manifests; 43 core tests | no Authenticode; proprietary-client legal boundary remains |
| Client branding | `PARTIALLY_IMPLEMENTED` | Revora launcher/Web identity | visible TQ/Conquer art/text/links remain in managed client |
| Admin/metrics/operations | `PARTIALLY_IMPLEMENTED` | protected Admin, audit, service metrics | no City Guard admin views; late subsystem coverage incomplete |

The V023–V029 tables contain no production rows and have no Host packet paths. They are therefore not described as
transferred. The scan found no gameplay `TODO`/`FIXME` markers masking implementations; the important gaps are
architectural dead paths, especially packet 1134 being audited and ignored at
`src/Revora.Host/Hosting/Genuine5165RuntimeComponent.cs` and advanced aggregates without persistence/dispatch.

## City Guard evidence and current boundary

The approved client executable SHA is
`902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`. Read-only disassembly confirms the
packet-10014 handler at `0x589c9d`. Owner Source `ReVora/Main/Packets.cs::SpawnEntity(Character)` corroborates:

| Field | Offset |
|---|---:|
| status | 16 |
| headwear | 24 |
| garment | 28 |
| armor | 32 |
| left hand | 36 |
| right hand | 40 |
| steed | 44 |
| armor / left-hand / head colors | 92 / 94 / 96 |

Commit `93c2547` added `GamePlayerAppearance5165`, authoritative item-to-appearance projection, session-safe peer
refresh and byte-offset tests. It did **not** prove the authenticated-player path. The genuine two-client test on
the deployed `17a54c7` build failed: both characters were equipped, each local character rendered its own gear,
and each remote character rendered with no gear. Production audit showed SHEEKOVIC legacy UID 2 had the immutable
canary enabled while sheeko1 legacy UID 3 did not. The approved client handler at `0x589c9d` supplies the second,
independent cause: a 10014 entity UID below 1,000,000 follows the non-player branch. Revora emitted immutable
database UIDs 2 and 3 directly, so even SHEEKOVIC's nonzero equipment fields were not consumed as a player
appearance. The correction retains those immutable database UIDs and uses a collision-bounded, display-only
player UID namespace only for a peer pair containing the SHEEKOVIC canary; it also hydrates the ordinary peer's
authoritative equipment and keeps peer movement/removal identity consistent. This remains
`IMPLEMENTED_PENDING_GENUINE_CLIENT_VALIDATION`, not success.

Failure evidence conditions: genuine approved patch-5165 clients, simultaneous SHEEKOVIC and sheeko1 sessions,
map 1002, within the 18-cell screen, both characters fully equipped. The side-by-side screenshot bytes were not
made available as a workspace file, so its SHA-256 is `Unknown`; no hash has been invented. The image must be
added to the private evidence store before its checksum can be recorded.

Production evidence at the failed session boundary:

| Evidence | SHEEKOVIC -> sheeko1 | sheeko1 -> SHEEKOVIC |
|---|---|---|
| immutable legacy UID | 2 | 3 |
| gate audit | `enabled=True` at `2026-08-31T09:39:46.2556809Z` | `enabled=False` at `2026-08-31T09:39:55.8372855Z` |
| authoritative visual item types | head 118109, armor 130109, left 420339, right 410339 | head 123109, armor 135109, left/right 601301 |
| failed 10014 result | nonzero fields carried under UID 2, which the client classifies as non-player | rollout supplied a zero appearance under UID 3 |

The old Host did not record full outgoing 10014 plaintext, so historical coordinates cannot be reconstructed as
a captured wire claim. Using the deterministic old encoder and the post-test persisted positions gives bounded
reproduction SHA-256 values `65d93ad487f2a5f707b163c274a6e909e91764603bbb5d63ae760df480c43efa`
(SHEEKOVIC, 147 bytes) and `885e86dce99842105474a9e118d9d462a4b38b4405fa5f60d1b01e6bf4c40c4b`
(sheeko1, 145 bytes); these are explicitly reconstructions, not falsely labelled historical captures. The corrected
Host records the complete canary plaintext bytes, decoded fields, source/recipient immutable UIDs, reason and wire
UID for the final genuine retest. Expected pairwise wire UIDs are 1,000,002 and 1,000,003.

No `PlayerLikeGuardEntity`, fake account, monster/NPC substitute, or guessed packet was introduced. Commander
remains map 1002 `(455,378)` and all `guards.*` gates remain disabled.

## Service NPC and GM command

`ProgressionService5165` and `PostgreSqlProgressionRuntime` implement four 1,000,000-CP services with explicit
confirmation, serializable charge+mutation, replay receipts, capacity checks, and audit. Production contains 27
service receipts and genuine maximum-level/loadout activity. First/second rebirth remain pending a complete owner
campaign, so the NPC is not globally released.

`@givemeall` is accepted only on normal Talk through `Genuine5165RuntimeComponent`; the serializable persistence
transaction revalidates active, non-revoked `gm` assignment for exact AccountId and CharacterId. Caps are
2,000,000,000 Silver and CP, not an additive credit. It does not authorize by name.

## Client and launcher audit

Stable launcher 1.0.7 remains immutable:

* launcher ZIP: 4,257,804 bytes, SHA-256
  `4a18d669204838b94d935f0448e1e62d4e90fd8f0aaeb0720cb3d6d25abfc460`;
* launcher manifest: 4,145 bytes, SHA-256
  `ed70f1f0a3e2f84b793819ae33d1c748aa3a6b3d9428b2e16de6588789a50b29`;
* client manifest: 6,933,146 bytes, SHA-256
  `b0237870474713c9f911fd891919f56f4ed458c28aa627041541969f44cf346c`;
* full client: 504,860,912 bytes, SHA-256
  `eb45995b817a153e2664f80f871c54ae24bf6993456887c498098d56decfcb0e`;
* desired state: 12,831 files / 1,450,174,454 bytes, 19 critical files.

The signed manifest still exposes internal build wording `manual-playable-vps-owner-test`; it must be replaced in a
new release, never edited in place. Static scanning of the exact public full-client archive found visible legacy
branding: 5 files/7 TQDigital occurrences, 9 files/21 `91.com`-family links, and 83 files/85 explicit Conquer phrases.
Confirmed visible examples include `data/AutoPatch_pic/{1.jpg,2.jpg,3.JPG}` and
`Help/images/index_01.jpg`. `Assist.dll` contains legacy URLs and must not be binary-patched speculatively.
`Flash.ocx` is excluded, but 128 proprietary SWF files remain. Legal/provenance review is required before claiming
lawful rebranding or redistribution. No client file was changed or published in this continuation.

The launcher root guard had a cross-platform normalization defect: trimming `/` produced an empty value. Commit
`6babe83` preserves volume roots and makes the 43/43 launcher suite pass. The real signed-client campaign damaged
`version.dat` and `ini/3DEffect.dbc` in a disposable copy, repaired exactly both, verified all 12,831 managed files,
preserved unmanaged files, and reused 12,812 passive cache entries.

## Automated validation

| Gate | Result |
|---|---|
| Release solution build | pass, 0 warnings / 0 errors |
| Gameplay | 56 passed |
| Host | 15 passed |
| Unit | 10 passed |
| Feature gates | 5 passed |
| Protocol | exact 5165 fixtures passed |
| Native Login | 59/59 passed |
| Web | 53/53 passed |
| City Guard rules | 12 passed |
| City Guard DMap coordinate | pass; 455,378 standable |
| Playable | 17/17 passed using dynamic ports and isolated DB |
| Gameplay PostgreSQL | pass against isolated restored production DB plus V046 |
| Admin | 6/6 under isolated peer-authenticated `revora-admin` identity |
| Public Identity | 9/9 under isolated peer-authenticated database identity |
| Launcher Core | 43/43 passed after `6babe83` |
| Client Doctor | 24/24 passed |
| Signed 12,831-file repair campaign | passed; exact two-file repair and cache reuse |
| Production-shaped PostgreSql5165 Host | passed against isolated restored V046 DB and complete reviewed map tree |
| Windows WPF / genuine client during this continuation | not run; cannot be called passed |

The initial from-scratch migration replay failed at V035 because catalog data is intentionally imported between
migrations; it is not a standalone SQL-only sequence. The valid workflow—protected production dump, isolated
restore, V046 apply twice, checksum marker, full PostgreSQL tests—passed. This distinction is preserved rather than
reporting the failed workflow as green.

## Security, release, and blockers

Critical/high blockers:

1. **City Guard entity wire identity:** exact equipment fields are now implemented for authenticated players, but
   a non-account player-like UID and full lifecycle lack genuine 5165 proof. Resolver: one controlled two-client
   appearance capture followed by a non-account entity capture only if client evidence supports it.
2. **Incomplete core gameplay families:** team/social/trade/guild/quests/events/housing are foundations without
   Host paths. This is engineering work, not an external blocker.
3. **General skills/items:** only evidenced subsets are active. Broad activation would invent semantics.
4. **Client public branding/provenance:** visible legacy marks and internal build text remain; redistribution and
   replacement rights must be confirmed. No public client update can be safely claimed complete.
5. **Genuine Windows validation:** the original equipment-aware peer path failed. The player-UID and pairwise
   hydration correction is deterministic/server-tested only until the same two genuine clients pass.

Known limitations: launcher executables are not Authenticode-signed; no new Malwarebytes/Defender exact-package
scan occurred because no package was built; no new manifest or public artifact was published.

## Deployment and rollback

V046 is forward-only and idempotent. It grants `revora_game` read-only access to five immutable City Guard catalog
tables and defines the independent equipment-visibility canary. Rollback is operational: remove only the exact
canary row and set its rollout to disabled; keep the migration ledger and grants. Host rollback restores the
timestamped pre-deployment executable/config copy and restarts only `revora-game`. No Web, launcher, manifest,
nginx, DNS, firewall, AMP, or PostgreSQL networking change is required.

Deployment backup: `/root/revora-game-backups/20260831T024332Z-release-readiness-v046`. Its prior Host hash is
`cabd37bbfc8cff9f76945cf806c4ecfe84a5d43de6ead63e9f2efb52ce41f6aa`; protected custom database dump hash is
`08debd78fd3b19babba092e1c8fa2065971e980d0ce7eb095f704a715fb9748c`. The dump restored successfully in
isolation, V046 applied twice without data duplication, and its character/item identity digests matched production.
The immediate on-host rollback directory is
`/srv/revora-game/app.previous-release-readiness-v046-20260831T024332Z`.

Post-deployment health: `revora-game` enabled/active, PID 1226392 at verification, exactly one READY, zero automatic
restarts, ports 9958/9959 owned by that Host, public Web HTTP 200, protected Admin redirect 302, PostgreSQL ready,
nginx active, and AMP ADS loopback HTTP 200. All thirteen `guards.*` gates are disabled. The new
`world.player.equipment.visibility` gate is enabled with canary rollout and exactly one immutable SHEEKOVIC
character assignment. Character and item identity digests match the protected pre-V046 restore.

The exact deployed tree was built twice and the Host bytes were identical. ClamAV 1.4.3 with signature set 28106
(2026-08-28) scanned nine deployment files / 41.61 MB with zero detections. A bounded string scan found no private
key marker, password assignment, research root, or development path. No Defender or Malwarebytes claim is made for
this Linux Host artifact.

## Minimal genuine owner action

Only one external action is needed for the next evidence level: log in simultaneously with SHEEKOVIC and sheeko1,
place both within 18 cells on map 1002, verify both are fully equipped locally, and capture both clients showing
the other character. Unequip and re-equip one visible item on each character while both remain in view, then
capture both clients again. The canary audit records the complete plaintext 10014 bytes and projected fields; the
owner does not inspect packet bytes. Stop on disconnect, wrong slot/art, duplicate entity, frozen peer movement,
or a missing refresh. Player-like Guard work remains blocked until both clients visibly pass.

## Final conclusion

What became complete in the original continuation: production/source lineage reconciliation, fixed-field 10014
serialization, late feature-gate registry parity, V046 guard read permissions, isolated PostgreSQL/Playable/Admin/
PublicIdentity execution, and cross-platform launcher root safety. Genuine testing disproved the stronger peer-
appearance claim. The corrected authenticated-player presentation is implemented but awaits retest. City Guard,
general gameplay systems listed as foundation/partial, full client branding, a new client release, and public-
release acceptance remain incomplete. The single safest next step is the bounded two-client SHEEKOVIC appearance
retest above; it is prerequisite evidence, not a claim that City Guard is done.

## 2026-08-31 peer-equipment canary correction deployment

Commit `46408846a2829bccb3e4e945124a9c80fe7307b9` was pushed to
`origin/feature/intelligent-city-guard-5165` and deployed as a Host-only canary correction. The deterministic
Linux x64 Host was published twice with identical bytes: 40,450,226 bytes, SHA-256
`24f662c78529adf498ea59cad4d0c484e784a8ddbff10c179094dbe2836260fe`. The installed file and the loaded
`/proc/1297952/exe` both have that hash, size, and inode `2391260`.

The protected rollback is `/srv/revora-game/rollback-peer-equipment-20260831T104114Z`; its previous Host SHA-256
is `5f354d11d453e8394061ffbd573b0c9b0ff4bc612140998e49b4857ac7a647f1`. The secrets file remained owned by
`revora-game:revora-game` with mode `0600`. No database migration, catalog, identity, Web, launcher, nginx, DNS,
firewall, or AMP change was made.

Validation passed: Release build (zero warnings/errors), Gameplay 56, Host 15, Unit 10, Feature Gates 5, Native
Login 59/59, Protocol fixtures, the complete 17/17 Playable campaign using dynamic ports and an isolated restored
PostgreSQL database, and the full PostgreSQL gameplay/concurrency/rollback suite. ClamAV 1.4.3/signature set 28106
reported no detection for the exact Host; the bounded secrets/path scan was clean. The first database test attempt
used a stale disposable schema missing `game.consumed_item_instances` and was not counted as passing; the protected
pre-V046 restore workflow passed fully.

Post-deployment state: `revora-game` is enabled and active with exactly one READY and zero automatic restarts;
ports 9958/9959 belong to the loaded Host. Public Web, registration and login return HTTP 200; protected Admin
returns its expected HTTP 302; PostgreSQL, nginx, and AMP are healthy. All thirteen `guards.*` feature gates remain
disabled. The presentation change applies only when the immutable SHEEKOVIC canary is one endpoint of the visible
peer relationship. No existing character UID, item row, wallet, equipment location, or guard state changed.

Genuine status remains `IMPLEMENTED_PENDING_GENUINE_CLIENT_VALIDATION`. The prior failed screenshot conditions
are preserved above, but its bytes were not available in the workspace, so its SHA-256 remains `Unknown` rather
than inferred. The old Host did not audit complete outgoing 10014 plaintext; the documented old packets are
deterministic reconstructions, not captures. The deployed Host now audits complete sanitized canary 10014
plaintext and authoritative projections for the one bounded retest.
