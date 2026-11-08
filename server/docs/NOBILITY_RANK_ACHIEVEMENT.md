# Paid nobility rank achievement

Achievement 10318 identifies receiving a nobility rank in the installed 6711
catalog (hash recorded in NOBILITY_ACHIEVEMENT_CHECKPOINT.md). Revora's existing
SortCore gives every donor with at least 30,000,000 silver-value donation a rank
of Knight or higher. The shared MinimumRankDonation constant now expresses that
existing threshold for both sorting and achievement eligibility.

NobilityDonations.Pay records 10318 in the same transaction as the checked purse
debit, donation row and payment receipt when the resulting donation reaches that
threshold. Cumulative donations qualify; silver, CP and bound-CP rates are
unchanged. Only the canonical ordinary-player payment path can create this
evidence. A rank field supplied by a client is not evidence. A suppressed evidence
write rolls back payment, donation and receipt. Post-commit refresh exposes the
achievement through the existing progression projection.

The achievement is historical: weekly reset removes the current donation/rank,
but does not erase its earning record. Existing high donations qualify on their
next successful payment; there is no backfill based on an unverified old balance
or paid Rey flag. This does not award 11120 (King title) or 11123 (Champion).

Validation: isolated Debug/x86 build and full purse review, with sixteen new
assertions covering all three currencies, just-below-threshold rejection,
cumulative/concurrent payment, immediate achievement projection, full character
reload with the production login achievement Send step, and suppressed evidence
or payment-receipt writes. The eighteen reset assertions now also check the
earned achievement after reset and cold reload. The runner's 407 prerequisite
counter excludes these review assertions. Five source-inventory audit tests
pass; producer inventory becomes 278/337 with 59 missing.

Test funds are fixtures. Ordinary earned funds and actual NPC-dialog routes
remain separate follow-up checks; direct service fixtures do not certify them.
Unrelated owner edits were present in the isolated build. No live deployment
or native-client acceptance is claimed.

Follow-up: NOBILITY_EARNED_ROUTE.md verifies daily-boss-earned CPs through the
installed donor NPC, safe confirmation and full reload. The bound-CP NPC earned
route and native/live gates remain pending.
