# Approved Revora rules — 2026-10-01

The owner accepted all five recommendations. These are Revora editions, not
claims about original Conquer rules. Native client acceptance stays pending.

- [x] Restore ordinary travel to maps1360–1364 through a documented Revora
  expedition with encounters, entry and a safe return. Verify earned travel
  receipts and restart recovery before marking the routes complete.
- [x] Add an earnable event reward exchange for otherwise unsupported permanent
  cosmetic collection milestones. Lower-star collection designs may supplement
  valuable event rewards. Do not count rentals or duplicate designs, change
  catalog star ratings, or claim 200 mounts from the compatible199-design catalog.
- [x] Retain tested direct Perfection progression to54. Existing atomic
  payment/material, ownership, binding, cooldown and replay rules remain.
  Original110/Super admission and shared phase restrictions are not requirements
  of this edition. Five existing private earning routes are code-validated;
  native acceptance remains open.
- [x] Disable Poker, including legacy table loading and client packet handling.
  Its four wardrobe rewards remain unavailable by owner choice; no Poker
  tournament implementation is planned. Entrance NPCs explain the closure;
  players already in its rooms retain their town return. Roulette is separate.
- [x] Skill PK refuses unsupported equipped Hossu619301 with an explanation to
  unequip it. It does not delete, downgrade or silently unequip the item. Gear
  validation remains authoritative before reservation/import.

Validation for the Poker/Skill batch: private build
`9170FDED5EABFA3CFD68B01E148FEC7BBB83BC538E91FAA083820AC81016952D`;
267 Skill combat/catalog checks pass, including Hossu explanation, item
preservation and eligibility after removal. Poker checks verify refusal before
packet parsing and disabled table loading without consulting a table file.
The build retains existing warnings. Native NPC/message presentation and live
deployment were not performed. A test-fixture class assignment initially tried
to persist without a database; supplying the profile before FullyLoaded
resolved the fixture error without production changes.

Exchange implementation and bounded validation: [Planet exchange](PLANET_COLLECTION_EXCHANGE.md). The [combined 200-garment / 100-mount journey](COLLECTION_TWO_HUNDRED_AND_HUNDRED.md) now passes ordinary server-handler earning, game/PostgreSQL restart recovery and duplicate-claim checks on one non-GM character. Controlled eligibility, calendar, combat and reward rolls are documented; native acceptance and a compatible 320-achievement career remain pending.

[Forgotten Realms](FORGOTTEN_REALMS_EXPEDITION.md) completes the expedition code and bounded ordinary-route/recovery checks. The1363 terrain/index release and all native gates remain pending. All five accepted implementation decisions are now addressed.

## Additional owner decisions (2026-10-01)

The owner's latest "use all recommendations" accepts these previously presented
Revora definitions. Approval does not mean implementation or native acceptance.

- [x] Champion11123: a contested local Elite PK winner earns the achievement.
  Atomic failure/retry, retained ordinary career and cold reload pass privately;
  native/live acceptance remains pending. See CAREER_REBIRTH_JOURNEY.md.
- [x] Occupation10302: eligible members of the winning guild earn it at regular
  Guild War finalization, with durable eligibility and result receipts.
  Ordinary member/absent-member, rollback/retry and restart checks pass privately;
  native/live gates remain open. See GUILD_WAR_OCCUPATION.md.
- [x] Newcomer10930: the installed four-step Twin City introduction, once per
  character, with one bound4030101 rune at final completion. Preserve normal
  warehouse/shop services and atomic progress/reward claims. Private earning,
  full-bag/failure/replay and cold recovery pass; native/live remain pending.
  See NEWCOMER_ACHIEVEMENT_PROPOSAL.md.

These are documented Revora rules, not verified original Conquer definitions.
Implementation, ordinary-player validation, persistence and replay protection
remain required before checking these entries. See NEWCOMER_ACHIEVEMENT_PROPOSAL.md.
