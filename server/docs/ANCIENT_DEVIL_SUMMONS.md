# Ancient Devil summon payment contract

Current source status (reviewed 2026-10-01): the historical staging and
"still required" notes below describe this page's original batch. They are
superseded for NPC admission, summon/claim dispatch, startup/tick integration
and collection achievement 11020 by [lifecycle evidence](ANCIENT_DEVIL_LIFECYCLE.md)
and [collection evidence](ANCIENT_DEVIL_COLLECTION.md). Do not reimplement those
connected components. Composed-process collection and saved-victory reward
recovery now pass in the linked lifecycle record. Mid-fight interruption,
genuine-client acceptance and deployment remain unclosed gates.

Staged source, 2026-09-20. This transaction participant has no production
initializer or NPC dispatch. It creates durable paid encounter records; it does
not spawn the Ancient Devil, grant victory rewards or award achievement 11020.

## Payment and daily policy

The caller must hold the canonical character's item lock and an open database
transaction. The summoner must be alive, level 50+, outside trade/booth/poker,
and beside switch identity 300017 at Ancient Island (218,208). That switch remains
unplaced in production; the fixture supplies it. Saved character state must agree.

A new payment selects exactly nine physical amulet UIDs of distinct classes.
Every item must be an owned, unbound, unlocked, non-suspicious, permanent,
single-item inventory entry. Current item state is locked/revalidated in the
database and joined to its original completed class-trial exchange receipt.
Ordinary trade can change its owner without erasing that earning provenance.
An item with the right catalog ID but no earning receipt cannot fund the summon.

One transaction records the summon and all nine offering receipts and consumes
the exact items. Item UIDs and originating trial receipts cannot be reused by
another summon. The optional tenth amulet is untouched. There is no added CP,
RVP or silver price. The caller commits before installing the inventory removals;
production reload recovers committed payment if the process is lost before that
installation.

Explicit Revora policy is one paid summon per character per Pacific quest day
and a thirty-minute encounter window. The official guide publishes a once-daily
limit but not these accounting/retry details. An unexpired pending summon resumes
its original identity without another payment, even when its previous offering
UIDs are no longer in the bag. A resumed result contains no removal instructions.
An outstanding victory must be claimed before another payment. Expiry closes
the pending attempt and does not restore that day's paid entry. A later day still
requires a fresh valid nine-amulet offering. No expiry or helper accounting is
inferred from mere island/trial admission.

The future world coordinator must validate/prepare the encounter before payment,
bind the spawned actors to this durable identity, preserve its deadline across
restart and distinguish a technical resume from a fresh paid attempt. The future
NPC must review the exact selected items before calling this internal method.
Those integrations remain unfinished.

## Validation

The isolated server build and the following fixture pass:

```powershell
& tests/PotencyHunt/Verify-PotencyHunt.ps1 -AncientSummonOnly
```

It reports 180 checks, including the 155-check full ten-class earning/trade route.
Payment uses those actually earned and traded amulets. It tests invalid lengths,
duplicate classes/UIDs, foreign or restricted items, stale saved state, trade,
range, a counterfeit item without provenance and a suppressed final offering
receipt. Failure rolls back earlier consumption and partial summon history.

Concurrent first confirmations create one payment; concurrent retries return
that same identity. A simulated loss after commit and before inventory install
recovers through production reload with only the unselected tenth amulet left.
All nine consumed items still join their original earning receipts. Pacific day,
exact deadline, same-day exhaustion and next-day material requirements are checked.
The fixture controls time and character setup; no real-time encounter duration,
boss combat, native switch action or live migration is claimed.

## Client boss profile follow-up

The installed encrypted `ini/monster.dat` contains exactly one `AncientDevil`
presentation profile: level 120, MaxLife 65,535, TypeID 2407, Boss 0 and zoom 150.
The server's `database/Monsters.txt` has the same profile values. The updated
read-only Ancient Devil audit verifies both hashes, those fields and a complete
codec round trip. Nine synthetic parser/topology/profile tests pass.

- Client monster.dat SHA-256:
  `99fa2c8f14c3851d797a0b20fbd325053f9acee35c7dab4c5e73edc2f5792585`.
- Server Monsters.txt SHA-256:
  `99d5f7d6b3ece68d9d17cb31d3170ab77c8f2501d80af15075cad467696cac9b`.

A read-only database query returned no monster family 2407. Family 7006 remains
Guild Beast and must not stand in for this boss. A client TypeID alone does not
prove a server family identity or spawn mesh. The combat profile, guarded
encounter, native HP presentation, completion reward and achievement producer
still need implementation/validation. No client profile was modified or staged.
