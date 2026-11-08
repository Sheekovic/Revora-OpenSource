# Nobility earning prerequisites

Reviewed 2026-09-25. The hash-bound installed Achievement.dat identifies 10318
as receiving a nobility rank; 11120 concerns the King title and 11123 a Champion
title. These are distinct identities. None is inferred complete from a displayed
rank alone. Catalog SHA-256:
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.

The donation review found concrete payment defects:

- Nobility.Handle ignored TrySpendConquerPoints returning false and could call
  ApplyDonation after the durable CP preimage failed. ChargeDonation now returns
  the payment result. The CP donation NPC honors the result as well.
- CP ceiling arithmetic added 49,999 before division, overflowing for large
  unsigned inputs. It now uses quotient plus nonzero remainder; zero, unsupported
  money types, values exceeding signed database storage, and total-donation
  overflow are rejected before payment.
- The shared Donate entry used by NPC callers now refuses a reload-fenced player
  and out-of-range additions. The bound-CP NPC still uses its legacy setter;
  this guard does not certify its debit result or make that payment atomic.

Validation exercises ordinary loaded characters through Nobility.Handle with
request overflow, total overflow, unknown currency, insufficient CPs/silver and
an actual saved-CP mismatch. The mismatch fences the session; neither the handler
nor the subsequent public Donate call publishes donation/rank. Existing purse
review regressions also pass with zero failures. Isolated Debug/x86 build passes.
All six new guard cases pass; the runner also reports its existing 407 prerequisite
checks (that counter does not include the separate review assertions). Test setup
supplies purses; this is not an earned
nobility route. Source and isolated tests only; no live deployment or migration.

## Remaining implementation sequence

1. Replace ChargeDonation/ApplyDonation split writes with one owner-locked
   transaction containing silver/CP debit, donation preimage, checked insert/update
   and an idempotent request receipt. Publish Board/rank only after commit.
2. Route both existing NPC payment paths through that service. Preserve their
   distinct existing currency/rate rules until an explicit economy decision; do
   not treat public Donate as proof that payment occurred.
3. Coordinate weekly reset with donation writes. ResetWeekly currently projects
   zero donations before a database clear, catches a failure, then advances its
   reset marker and announces completion. Repair this before recording reliable
   rank evidence. Verify restart recovery, concurrent reset/donation and failed
   writes without lost payments or invented rank.
4. Derive 10318 from the resulting authoritative rank rules. Audit paid Rey versus
   earned ranked King before defining 11120; investigate Champion independently.
5. Validate ordinary earned funds, donation, rank, evidence and full character
   reload. Keep genuine-client title/icon and live acceptance separate.

The guard patch is a prerequisite repair, not completion of the nobility service
or its achievements. Source producer counts remain unchanged. The isolated build
contains unrelated owner edits and is not a clean release artifact.

## Donation transaction follow-up

NOBILITY_DONATION_TRANSACTION.md now records atomic debit/donation/receipt writes
and both NPC callers using that service. Thirty service assertions and the full
purse review pass. The sequence above preserves the initial audit.
NOBILITY_RESET_RECOVERY.md records the subsequent atomic weekly reset and
interrupted-projection recovery, with eighteen passing assertions. Rank evidence,
earned funds, NPC-route validation and native/live acceptance remain open.

## Rank evidence follow-up

NOBILITY_RANK_ACHIEVEMENT.md adds 10318 from the atomic paid-donation path at
the existing 30-million rank threshold. This closes the source producer for
10318; it does not settle 11120/11123, historical paid Rey, earned-funds/NPC
routes or native/live acceptance.

## King/Champion research follow-up

research/NOBILITY_KING_CHAMPION_EVIDENCE.md records the installed title identities,
official-versus-Revora rank-limit mismatch and remaining Rey command callers.
Official ranked limits are 3/15/50 rather than the current 4/16/150 cutoffs.
The owner subsequently chose five Kings, fifty Princes and unlimited qualifying
Dukes; see NOBILITY_REVORA_RANKS.md. Champion's specific grant-to-achievement mapping
remains unverified; neither 11120 nor 11123 was enabled by this research.
