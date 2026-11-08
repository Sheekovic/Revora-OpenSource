# Hero's Wings implementation

Current status (2026-10-01): this chain is included in the completed private
[nineteen-chain earning/restart validation](EQUIPMENT_QUEST_AGGREGATE.md).
The approved Revora10931 evaluator is implemented and tested; older statements
below that it is disabled or that no process restart was tested are historical.
Native/authenticated-client acceptance and live deployment remain pending.

## Contract and evidence

- [x] Installed missions 1808-1818, ordered predecessors, level 90-110 client
  eligibility and unrestricted class. Each section has exactly two identical
  ActivityType=0 keys; audit rejects other duplicate fields.
- [x] Poor Xiao 4700 at 1015,725,541 and Doctor Know-it-All 4701 at
  1015,701,595. Chang uses the established Revora replacement identity.
- [x] Meteor 1088001 x1, BirdBlood 721906 x10, BitterGinseng 721908 x1,
  HawkClaw 721910 x5, Gallbladderr 721909, PeelPill 721911, GinsengMap 721912
  and SuperHeadgearPack 728247 verified in the installed catalog.
- [x] `tools/audit_hero_wings.py` passes against hash-bound installed journal
  and catalog; derived metadata in `docs/research/HERO_WINGS_CONTRACT.json`.

[Original guide](https://co.99.com/guide/quests/herowings.shtml), consulted
2026-09-26, supports per-rebirth eligibility, timed pairs of Hawks L93 and five
Ginseng locations. It does not publish drop odds or a complete Ginseng selection
rule. Local journal hunting hints differ from the guide; actual spawn/terrain
checks remain required. Monster identity strings in the contract are descriptive
candidates, not verified runtime family IDs.

## Reward pack

- [x] Ordinary transactional opening of 728247 produces a class-specific Super
  headgear: level 92 Archer hat; level 97 for other supported classes. Permanent
  +0, source binding preserved. Unsupported classes retain their pack.
- [x] Ten class selections, rollback/retry, duplicate use, binding and reload
  covered by 435 checks (406 shared + 29 pack); build passes.
  Logs: `obj/hero-pack-build.log` and `obj/hero-pack-tests.log`.
- [ ] Earned referrals, paid Meteor, tracked monster materials, timed kills,
  Ginseng search, pill delivery and persistent final rewards.
- [ ] Ordinary NPC/item/death/journal integration and complete route validation.
- [ ] Client tooltip/journal corrections, native/Host acceptance and deployment.

Class headgear is the Revora selection from the guide's headgear/earring range;
no original per-class pack script was recovered. The one-time quest will award
its bound EXP Ball and owner-approved bound P7 separately, following the earlier
quest policy. The installed pack tooltip still mentions the EXP Ball: its private
correction must accompany the earning route. Pack opening alone is not a completed
quest. No production schema initialization, deployment or client changes occurred.

## Runtime and terrain evidence

- [x] Read-only catalog probe confirms Birdman 18 (87), BirdmanL88 77
  (88), HawKing 19 (92) and HawkL93 78 (93). Their Bird Island spawn rectangles
  are recorded in `docs/research/HERO_WINGS_RUNTIME.json`. Similar names such
  as SilverHawk and BirdKing are not interchangeable with these families.
- [x] NPCs 4700 and 4701 are absent from the queried public NPC catalog; the
  quest coordinator must create them with validated client models.
- [x] Hash-bound shared-terrain routes pass for the two NPCs, five Ginseng
  locations and sampled hunt hints. Doctor's west approach 699,595 is blocked;
  use connected south approach 701,597 in route tests.
- [x] Client Hawk hint 409,568 is walkable but outside both recorded HawkL93
  spawn rectangles. The 451,676 region has family 78; final journal text must
  direct players to that actual spawn region.

Reproduce with `tools/audit_hero_wings_terrain.py --client ../../6711 --output
 docs/research/HERO_WINGS_TERRAIN.json` (run with Python as one line). The audit
checks immutable terrain hashes and bindings. It does not verify live monster
movement, every spawn cell, inter-city arrival or native rendering. Runtime
probe output is retained privately in `obj/hero-catalog.log`.

Next: persistent ordered referrals and paid Meteor, tracked blood/gallbladder/
Ginseng/pill ownership, durable one-minute paired-kill timer and atomic rewards.
The timed-pair window must expire across logout/restart, reject corpse replay,
and consume disjoint pairs so one kill cannot count toward multiple claws.

## Persistent store

- [x] Ordered per-rebirth progression, frozen bound P7 and Ginseng site.
- [x] Meteor payment supports bound/unbound stacks with an atomic stage change;
  locked, stale or otherwise unusable items are rejected.
- [x] Tracked bound Blood, Gallbladder, Map, Ginseng, Claws and Pill. Stored
  belongings cannot be copied; lost raw materials require renewed hunting,
  while earned prepared items/documents can be restored from tracked ownership.
- [x] HawKing family 19 is flagged Boss=true in the loaded catalog. A narrow
  validator permits that exact quest target while checking public map identity,
  ownership, template, killer and corpse registration; other bosses gain no exception.
- [x] Durable disjoint HawkL93 kill pairs, corpse replay protection, inclusive
  60-second boundary and expiry across reload. UTC conversion accommodates this
  Npgsql version's local-time reads and PostgreSQL microsecond precision.
- [x] Ordered hand-ins, total 6,800 silver, one direct EXP ball, then bound
  Super Headgear Pack, EXP Ball(E) and frozen P7 are transactional.
- [x] Build and 521 focused checks pass (406 shared + 29 pack + 86 store).
  Three rebirth states, real monster deaths, Meteor rollback, material ownership,
  recovery, timer reload/expiry/boundary, reward rollback and concurrent claim
  persistence are covered. Logs: `obj/hero-store-build.log`, `obj/hero-store-tests.log`.
- [ ] Ordinary NPC/item/death/journal coordinator and full-route checks.
- [ ] Additional full-bag, raw-loss and timer-write fault coverage.

Explicit Revora rules: Blood and Gallbladder drop chance 50%; one of the five
verified sites is chosen uniformly and saved at acceptance. Using an earned
Gallbladder within two cells of that site with the earned Map deterministically
creates Ginseng. Wrong sites consume nothing. The Map is consumed at the Ginseng
hand-in. These rules are custom because original odds/site selection are unpublished.
A full bag does not grant a material or advance a timed pair. A backwards clock
starts a new pair; completed pairs never reuse either kill. Store tests supply
trusted interaction contexts and do not prove public NPC access. Nothing deployed.

## Ordinary server route

- [x] Poor Xiao and Doctor Know-it-All use installed NPC types 421/422,
  SimpleObjID 16/273, at their confirmed coordinates. Chang offers the quest.
- [x] Ordered ordinary NPC interactions, Meteor payment, material recovery,
  Ginseng Map/location text and Gallbladder use are connected.
- [x] Production monster-death hook, revival replay reset, startup initialization
  and login journal refresh are connected. The timed pair remains durable.
- [x] Eleven journal missions reflect saved progression; both quest-start packet
  paths reject forged client progress.
- [x] Build and 500 ordinary-route checks pass (406 shared + 94 route).
  Three rebirth states cover actual NPC/item/death dispatch, varying saved sites,
  timer expiry after reload, final bound rewards and pack opening after reload.
  The test also confirms an explicit replay after production death changes
  neither inventory nor timer, rather than using replay to grant missing credit.
  Logs: `obj/hero-quest-build.log`, `obj/hero-quest-tests.log`.
- [ ] Extra inventory/timer-write fault checks and private journal/tooltip overlay.
- [ ] Native/Host acceptance and deployment.

The full route is source-implemented and tested with ordinary non-GM players in
an isolated database. Real-client NPC rendering and arrival/navigation remain
unaccepted. No live server/client deployment occurred.

## Fault coverage and private client overlay

- [x] Full inventory rejects Blood and timed-pair initiation without granting
  materials or allowing later reuse of the same corpse.
- [x] Lost raw Gallbladder and Claw require fresh hunting; a replacement Claw
  still requires two distinct kills.
- [x] A failed Claw insert rolls back timer completion and material tracking.
  The failed corpse cannot be replayed; a fresh kill can finish the saved pair.
- [x] 531 focused store checks pass (406 shared + 29 pack + 96 store), including
  ten new fault checks. Log: `obj/hero-fault-tests.log`. Source code unchanged
  since the prior successful build; ordinary route coverage remains 500 checks.
- [x] `tools/ClientPatches/prepare_hero_wings_journal.py` stages eleven journal
  sections and pack 728247's corrected tooltip on the Desert Blood bundle.
  Unrelated quest/catalog data remains intact, with hash-bound preimages,
  transform round-trip and rewrite-idempotence checks.
- [x] Directions use the Doctor's south approach, actual Hawk spawn region,
  saved Ginseng Map location and documented paired-kill rules. EXP Ball(E) is
  described as a separate quest reward rather than pack contents.

Private staged journal SHA256:
`6be1c0c45d4e6c5c76f24b541d9cc4d3e6793fb28e15c0f65ba80c2b1893d536`.
Files: `obj/chi-refill-validation/hero-wings-journal/staged/ini/`.
Unsigned, uninstalled, unpublished; native UI/rendering and actual Host acceptance
remain pending. The next independent quest root is Family Feud (1819).


### Retained career verification (2026-10-02)

- [x] Ordinary retained character 4294907001, life2, deposits spare belongings
  through the owned house box, buys a Meteor at the configured CP catalog price
  and pays exactly one through the quest NPC. Existing funds have mixed prior
  provenance; this check does not claim they were all earned by this quest.
- [x] Public portals lead to the quest NPCs. Calculated Birdman and HawKing
  combat earns the map and digging material; the selected site yields the next
  material. Five timed Hawk pairs earn five tracked materials at stage7.
- [x] A separate process reloads those materials, completes ordered hand-ins
  and receives three bound rewards (728247, 723911 and one P7 soul), with
  exactly 6,800 silver across the quest. Another process preserves exact
  career/quest state and reward UIDs; repeat final claim changes nothing.

Evidence: `career-retained-hero-hunt`, `career-retained-hero-claim`, and
`career-retained-hero-reload` all passed on private game SHA-256
`34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`.
The fixture controls drop rolls, clock, positioning and return from home; it
uses calculated attacks rather than injected damage. Native playability,
rendering and deployment remain pending. Retained aggregate12/19, seven remain.
