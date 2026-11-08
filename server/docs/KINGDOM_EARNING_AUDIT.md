# Kingdom wardrobe earning audit

Source checkpoint: `a19bd63d`, reviewed 2026-09-26. This is a bounded source
review, not native protocol verification, a runtime exploit demonstration or
proof of an ordinary earning route. No live state or production rules changed.

## Blocking findings

1. **Payment does not select the payer's union.** `Npcs.cs:1503` (option99 under
   NPC10003) checks200,000 RVPs and3,500,000,000 union funds, spends RVPs, subtracts
   union funds in memory and calls `UpGradeUnion` three times. The selector at
   `Sistemas/Union/UnionPacket.cs:205` independently chooses the highest GoldBricks
   union. A successful payment can therefore announce promotion to a payer whose
   union is not selected. The failure text quotes350,000,000 instead of the checked
   3,500,000,000. This is not a tested or approved economy adjustment.
2. **The promotion is not one durable transaction.** The RVP debit, union fund
   mutation and kingdom change are separate operations. `UpGradeUnion` writes
   `Kernel.ServerKingdom` before broadcasting. In the reviewed production C# paths,
   the only other direct assignment is the configuration read at `Server.cs:909`;
   no matching kingdom configuration write was found. A restart-safe title claim
   cannot be inferred from this in-memory change.
3. **Multiple triggers disagree about admission.** Login calls the global selector
   after the logging-in union has50,000 bricks and150,000 funds
   (`Network/GamePackets/GeneralData.cs:1845`). Paid promotion uses different funds
   and no equivalent brick test. `PlanderWar.Close` also calls the selector, but
   its scheduled start is commented out at `Thread.cs:3605`. The selector itself
   checks none of these prerequisites, has no explicit tie rule and dereferences
   a null winner when the union list is empty.
4. **Ordinary brick earning is not yet durable evidence.** Plander collection uses
   a progress callback and transient `PuntosUnion`. `Data.cs:1119` adds it to union
   bricks and clears it without a transaction/receipt. GoldBrick item3003498 at
   `ItemHandler.cs:4221` increments the union then removes the item separately.
   Neither is sufficient proof of an atomic earning/payment path. Supplier and
   event access still need validation; no brick source has been invented here.
5. **Succession needs transaction and session validation.** Union action14 at
   `Sistemas/Union/Union.cs:509` modifies official/harem/guard state, changes the
   leader and saves separate fields. The current title fixture supplies a union
   object and `Kernel.ServerKingdom`; it tests eligibility only, not creation,
   election, succession, old-ruler revocation or restart recovery.

`TitleRequirements.Eligible` awards Overlord11000 and Solar Dragon40011000 to the
leader whose union matches `Kernel.ServerKingdom`. Both matrix rows remain
predicate-only. Granting them from a seeded union would hide the issues above.

## Decision and implementation sequence

Owner decision requested: competitive event selection, paid promotion of the
paying union, or retaining locks pending original-rule research. Existing code
contains conflicting semantics, so choosing one silently would change the product.

After that decision:

- Define a single authoritative selection trigger, eligibility and deterministic
  tie/expiry rules; preserve the approved price if a paid route is selected.
- Commit the result, any payment, union changes and a unique operation/event receipt
  before updating caches or broadcasting. Use expected preimages and owner/session
  locks; reject stale leaders and duplicate requests without additional charges.
- Recover the same result on startup and reconcile both old and new ruler wardrobe
  state immediately. Make succession use that same durable authority.
- Prove ordinary guild/union creation and brick/fund acquisition, followed by
  selection, title/wing equip, reload, successor transfer and old-ruler revocation.
- Validate rollback and interrupted postcommit recovery in an isolated database;
  retain actual Host/native and deployment gates.

This audit does not claim any of those implementation steps complete.

## Owner decision (2026-09-30)

The owner selected the recommended **competitive event selection** edition.
Paid promotion is not the target earning path. This resolves the selection-model
question; it does not validate the conflicting legacy selectors or enable rewards.
The implementation sequence above remains open: one durable competitive result,
ordinary eligibility, deterministic ties, restart recovery and atomic succession,
followed by ordinary title/wing earning and revocation tests. Existing live code
has not been changed or deployed by this decision record.

## Loaded catalog and union recovery (2026-09-30)

A read-only query of the owned private catalog confirms map3998 exists with
mapdoc3998, but NPC900038 and all field NPCs on3998 are absent. GuildDirector10003
is installed at1002/317,321. The old event therefore cannot be called an ordinary
player route merely by uncommenting its timer. Field admission, harvest/deposit
actors and their reachable terrain must be implemented and tested.

A startup recovery prerequisite is fixed in `UnionTable.Load` and
`UnionClass.Restore`. The former loader invoked persistent setters for bricks,
funds, name, title, bulletin and declaration while reading database rows; these
writes were unnecessary and could fail startup or overwrite state. It also read
only six of eight saved core-officer positions. The new hydration path assigns
private fields without writes or notifications, restores all eight slots, accepts
legacy six-slot strings with two zero slots, validates roster widths and unsigned
values, and publishes the complete dictionary only after every row succeeds.
It rejects calls against an already populated union cache rather than replacing
live objects. Guild associations are rebuilt deterministically from loaded guilds.
Malformed data fails startup with no partially installed union snapshot; this does
not silently repair or delete database records.

- [x] Read-only startup hydration and complete office roster recovery.
- [x] Isolated x86 build and27 database assertions across two fresh probe processes
  with a PostgreSQL stop/start between them. Database triggers reject every
  INSERT/UPDATE/DELETE during loading. Exact saved-row fingerprints, funds,
  leader identity, all officer/harem/guard slots and legacy roster padding survive;
  malformed roster rows publish nothing and populated-cache replacement is refused.
- [ ] Competitive admissions/scoring/selection, atomic union creation and succession,
  title/wing transfer and revocation, full Host/player journey, native and deployment.

Reproduction probe: `tools/CheckUnionRecovery.cs`, compiled x86 against the isolated
Revora/Npgsql outputs with the existing binding-redirect configuration. Its
`capture` and `verify` phases require `REVORA_QUARANTINE_TEST_CONNECTION` for the
fixed private127.0.0.1:25439/revora_candidate_game database, clear that environment
value immediately, and operate only in the ownership-marked
`revora_kingdom_recovery_fixture` schema. The verified phase removes that schema.
Use the established DPAPI child-environment runner; never put the connection
string on a command line or in Git. Both phases passed with assembly SHA-256
`A8E69436C0078E0CE5F406B125C60E4574C6984BE6EEE1B0880BC4ADCAB721C3`.
Ignored logs: `obj/kingdom-recovery-build.log`,
`obj/kingdom-recovery-capture.log`, `obj/kingdom-recovery-verify.log`.
The isolated executable emitted its pre-existing missing GeoIP.dat notice; this
probe does not use geolocation. PostgreSQL stopped cleanly afterward. No live
configuration, game database, listener, client package or deployment changed.

This closes recovery of existing union records only. It is not evidence of an
earned Kingdom result, complete event implementation or ordinary ruler rewards.

## Competitive ledger follow-up (2026-09-30)

The [competitive edition ledger](KINGDOM_COMPETITION.md) now has tested durable
membership, harvest/deposit receipts and immutable scoring results.31 database
checks and isolated build pass, including concurrent replay, failed writes and
separate process/PostgreSQL restart. A subsequent private field adapter passes25
ordinary NPC/reload checks, including safe returns and corrected union identity
hydration. Combat/title integration and startup activation remain pending.
The original raw-byte geometry audit inverted blocked/walkable semantics; loaded
Floor checks now confirm connected field approaches. See the corrected terrain
evidence in the competitive edition document. The legacy combat branch still
allows city attacks rather than field attacks and must be replaced before
activation. No Kingdom wardrobe row becomes complete from these prerequisites.

## Field combat follow-up (2026-09-30)

The expanded private field journey passes47 checks including actual ordinary
player damage/death, revival, protected entry, allied and cross-map denial, exact
saved economy/item preservation and closed-window refusal. The legacy city-only
Union attack and death-point branches are removed; Grabber cannot bypass field
item protection. Union creation/succession, authoritative crown projection and
full Host/native acceptance remain incomplete. Normal activation remains off.
See [combat evidence](KINGDOM_COMPETITION.md#combat-validation-2026-09-30).

## Ordinary union formation follow-up (2026-09-30)

Creation action7 now saves union, founder office, guild link and allocator in one
transaction before cache/client projection.14 ordinary handler and failure checks
pass, including rollback, concurrent replay and postcommit reconnect recovery.
[Evidence and scope](KINGDOM_COMPETITION.md#atomic-union-formation-2026-09-30).
This resolves the formation write sequence, not leadership transfer or other legacy
membership/office mutations. Crown selection, succession and wardrobe revocation
remain open; no Kingdom wardrobe row is complete yet.

## Ordinary succession follow-up (2026-09-30)

The old action14 sequence of independent office-removal and leader writes is now
one checked transaction with postcommit projection.25 formation/succession checks
pass, using ordinary allegiance and appointment handlers before transfer, including
rollback, replay, old-leader authority loss and both-session reconnect recovery.
Legacy Union actions and direct membership/office updates share serialization with
transfer, but their own multi-write rollback remains separate unfinished work.
[Evidence](KINGDOM_COMPETITION.md#atomic-union-succession-2026-09-30).
Competitive crown projection, expiry, wardrobe earning/revocation, fresh Host/native
acceptance and deployment remain open; neither Kingdom reward row is complete.

## Competitive crown follow-up (2026-09-30)

The paid, login, configured-ID and lifetime-brick selectors described above are
retired in source. Crown authority now comes from the latest finalized contest,
fixed expiry and the winning union's current saved leader.40 ordinary private
journey checks cover actual earning/equip, succession with immediate successful
revocation, reconnect recovery, expiry, replacement, empty results, replay and
fail-closed authority reads.568 title checks and47 field checks pass.
[Evidence and activation limits](KINGDOM_COMPETITION.md#competitive-crown-and-wardrobe-authority-2026-09-30).
The two rewards now have an implemented private earning path, exceeding the original
predicate-only evidence, but remain staged: normal startup, cold full Host recovery,
remaining membership transaction acceptance, native acceptance and deployment are
not complete. No playable/deployed completion count is increased here.
