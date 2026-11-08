# Inner Power unlock and training persistence

Implemented 2026-09-26. This closes the transaction gaps identified in
[the Prestige prerequisite review](PRESTIGE_CULTIVATION_GAPS.md); it does not
complete the 600,000-Prestige earning route.

Unlock now consumes one owned book and writes progression in one PostgreSQL
transaction. An already unlocked gong returns without consuming anything.
Training commits one legal level with its exact cumulative potency cost. Both
paths check the current player session, admission, item ownership where relevant,
award checkpoints and progression revision. Conflicts or failed postcommit client
updates fence the session for reconnect. No GM or owner allowlist is used.

`revora_inner_progress` stores each migrated character's gong state, revision and
cumulative spending. Existing `InnerPower.txt` remains the pre-migration baseline;
its new trailing `AppliedTrainingPoints` field defaults to zero for old files.
Recovery applies both award ledgers before subtracting spending not yet reflected
in that file. It restores progression from the database even when the last file
save predates the unlock. Startup discovers ledger-only characters and recovers
them before ranking; only migrated characters incur these new startup lookups.

The existing installed costs and admission are retained. Universal Concept A
requires second rebirth and level 15, with five levels costing 1,500 potency each.
Training requests have no verified nonce: repeated accepted requests buy successive
levels, each charged once, until the cap. Only unlock replay is idempotent; this
is not a claim that identical training packets represent one intent.

## Validation

Run the existing fixture runner with `-InnerProgressOnly` after building its
configured validation output. This run used the tracked entry point and fixtures
with the existing `obj/mrms2017-validation/bin` output override. Build succeeded
with legacy warnings. Logs: `obj/inner-progress-build.log` and
`obj/inner-progress-tracked-tests.log`. **429 combined checks pass**, including
406 reused prerequisite checks and 23 checks in this batch. Existing unrelated
owner work was present in the validation build and remains outside this commit.

The isolated database checks cover concurrent/repeated unlock, malformed requests,
second-rebirth admission, exact pack/NPC awards and training costs, insufficient
potency, maximum level, item/progression write rollback, changed ownership, stale
award and progression preimages, interruption after commit, inventory reconnect,
cold award/spend recovery, new and legacy file loading, startup discovery and
offline Inner Power Prestige parity. Eligibility, initial books and the NPC
purchase purse are supplied fixture inputs. No end-to-end book supplier, earned
second rite on the continuing Prestige character, real crash process or native
client acceptance is claimed.

## Deployment boundary

No live deployment or schema change was performed. On a future approved start,
initialization creates the additive table and existing characters migrate on
their first committed unlock or training operation. Preserve a consistent backup
of PostgreSQL and `InnerPower.txt` before that deployment. Once new progression
has committed, do not downgrade to code that ignores its spending ledger: rollback
requires a coordinated database/file restore or a reviewed state export. Ordinary
file-only rollback can lose unlocks or re-credit spent potency.

## Ordinary earning checkpoint

The continuing second-reborn Prestige character now earns potency through the
public hunt, buys all 17 books from the installed CP shop, completes 111 training
levels and passes cold recovery plus offline score parity. **1,116 combined checks
pass**, including 27 new earning checks. [Exact costs, controlled outcomes and
remaining gates](PRESTIGE_EARNING.md#earned-full-inner-power-2026-09-26).
This extends the earlier transaction-only fixture scope. The legacy RVP merchant
on map 5001 is not certified as an ordinary accessible service by these tests.
