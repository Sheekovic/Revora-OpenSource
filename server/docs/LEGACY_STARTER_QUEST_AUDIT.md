# Legacy starter quest reward audit

2026-09-21. Initial source/binding audit, followed by catalog and persistence
work. Soul Trial now has [owned earning/completion validation](SOUL_TRIAL_TRANSACTIONS.md).
Courier Loop also now has [owned earning/completion validation](COURIER_LOOP_TRANSACTIONS.md). Neither path has live release approval.

Courier Loop and Soul Trial select class-oriented soul IDs in their own
`PickClassSoul` methods, outside `QuestSoulReward.Create`. Their final claims
called `Inventory.Add(soul, 0, 1)`, creating an unbound item. The initial correction used
`Inventory.AddBoundItem(soul, 0, 1)`, which explicitly sets `item.Bound=true`.
Both now insert their bound item in an owned completion transaction. NPC reward wording and the feature descriptions
now say bound. Existing issued items are not migrated.

The generic four-argument `Inventory.Add(uint, byte, byte, bool)` currently
ignores its `bound` argument. Merely passing true would not fix these grants.
This batch uses the existing explicit bound-item method and does not alter every
caller of the generic inventory API. Isolated server build passes; no new
end-to-end earning, failure or live-client test is claimed for these old quests.

## Initial transaction gaps (subsequent source fixes below)

- Courier Loop still sets in-memory completion variables, grants items,
  update currencies/level/VIP, and save variables/entity state separately.
  Its catch path reopens progress after a failure. An earlier granted item or
  currency can therefore outlive a later failure; this needs one owned atomic
  completion transaction and a durable receipt before it can join source coverage.
- Preserve once-per-rebirth eligibility, class-oriented rewards, progress,
  level-puff/EXP-potion alternatives, Courier VIP terms, exact purse preimages,
  inventory capacity and saved completion. Integrate existing owned progression
  services rather than repeating nontransactional level writes.
- Verify accepted ordinary routes through actual monster deaths and NPC stages,
  then fresh character reload, duplicate requests, full inventory, stale balances
  and late transaction failures. Check immediate client item/currency projection.
- Catalog identity/grade verification now passes for the 29 candidate IDs in
  both quests (see below). Validate actual purification compatibility for each
  class; catalog class fields alone do not prove class-exclusive use.
- Audit Path Hunt and Daily Envoy completion rewards under the owner standard.
  They are outside the completed reward coverage inventory.
- Reconcile Courier travel text: current source uses Phoenix Castle map 1011;
  the older FEATURES page describes Bandits on Bird Island 1015. Validate the
  active monster family/route before treating either prose description as proof.

The initial narrow corrections did not change the then-29-family inventory.
Soul Trial and Courier's subsequent owned completions bring source coverage to 31 families. Genuine
client acceptance and live deployment remain pending for the legacy paths.

## Candidate catalog verification

`tools/audit_starter_souls.py` reads both selectors and the loaded server and
installed encrypted client item catalogs. It requires equal, nonempty candidate
sets, rejects duplicate catalog identities, verifies the decoder round trip and
checks P7 grade (field 55) plus the first six identity/name/eligibility fields.
All 29 candidates pass. Only derived IDs, decisions and source/catalog hashes
are recorded in `docs/research/STARTER_SOUL_CANDIDATES.json`; no decoded client
catalog is written. Run with `--output` to regenerate the evidence.

Both legacy `Offer` methods now require loaded `PurificationLevel==7`, so a
missing item or later catalog downgrade cannot silently become a non-P7 reward.
The selector mapping is otherwise unchanged. The fixture temporarily downgrades
one loaded candidate, checks rejection in both quests, restores the catalog in
`finally`, and checks P7 availability for all eleven listed class families,
including novice Taoist. It does not establish class exclusivity or use the
quest names as evidence of actual purification compatibility.

Atomic quest completion and actual earning/reconnect tests remain separate work.

Validation: isolated build and 1,374 gift-suite checks pass, including 24 new
selector checks and the existing suite prerequisites/regressions. Log:
`obj/chi-refill-validation/starter-soul-catalog.log` (ignored).

## Shared progress-save prerequisite

`VariableVault` now serializes a stable image under its own lock. Both
`EntityVariableTable.Save` paths retain that lock through the database update
and clear `Changed` only after exactly one owner row was saved. A database
exception, suppressed write or missing row leaves the progress pending for retry.
Concurrent setters cannot be acknowledged by an earlier save, and dictionary
insertion cannot change the entry count during serialization.

The isolated gift fixture verifies both save entry points with a suppressed
update, a database exception, successful retry/readback, concurrent writes/saves
and a missing owner row. It also checks concurrent snapshot serialization.
These are 13 new checks, not proof of an earned Soul Trial completion.

This is a prerequisite only: the legacy reward/level/quest-flag operations still
need one owned completion transaction and actual earning/reconnect coverage.
The lock does not turn a multi-call read/increment/write into an atomic counter,
nor does it coordinate separate session objects for the same database owner.
No live database migration, deployment or client validation occurred.

Validation: isolated server build and 1,387 gift-suite checks pass, including the
13 new progress-save checks. Log:
`obj/chi-refill-validation/quest-variable-durability.log` (ignored).

## Soul Trial completion follow-up

Soul Trial's separate reward, profile and flag writes are replaced by one checked
transaction and a durable life receipt. Its accepted route now validates owned
wilderness deaths, saves each credit and rejects corpse replay. Build and 510
checks pass. See [the full scope](SOUL_TRIAL_TRANSACTIONS.md), including failure
injection, fresh reload and limitations. The earlier sections record the initial
audit and prerequisite stages; their former Soul Trial transaction gap is closed
at source/test level. Courier completion and native acceptance remain open.

## Courier Loop completion follow-up

Courier's split claim is now one transaction covering the bound soul, purse,
profile, VIP, progress and receipt. Its owned Phoenix Castle hunt persists each
kill and requires the Commander/Clerk sequence. The historical transaction and
route-documentation gaps above are closed at source/test level: build and 529
checks pass. See [evidence and limits](COURIER_LOOP_TRANSACTIONS.md). Path Hunt,
Daily Envoy, native purification compatibility and live acceptance remain open.

## Path Hunt and Envoy follow-up

[Path Hunt](PATH_HUNT_TRANSACTIONS.md) now has owned earning and atomic valuable
rewards for new acceptance; 552 checks pass. The [Envoy audit](DAILY_ENVOY_REWARD_AUDIT.md)
identifies Worship the Ancestors as the next visible reward gap and avoids
counting the hub again. Current source coverage is 32 families. Historical gap
lists above record earlier audit stages, not the current completion status.
