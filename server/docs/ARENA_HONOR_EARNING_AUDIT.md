# Arena Honor earning audit and implementation sequence

Audit of source at `32bf8f5`, 2026-09-21. This is a verified gap analysis and
implementation plan, not a completed earning path or live economy change.
The existing summon-item fixtures seed Honor and therefore do not prove that
ordinary matches earn a spendable, durable balance.

## Original evidence and Revora differences

The official [PC Qualifier guide](https://co.99.com/guide/quests/arena.shtml)
specifies 500 Honor and an EXP Ball at twenty daily matches, plus another such
award at nine daily wins. It describes daily ranking Honor, seasonal ranking
awards and a three-percent transfer of the loser's Arena points. A withdrawal or
disconnection is a forfeit. The guide does not publish the ranking award table,
integer rounding or a complete restart/transaction contract. Do not infer those
from the mobile or Champion's Arena guides, which are different systems.

Current Revora `Qualifier.Win` instead applies a random 30?49 Arena-point
transfer. It increments winner TodayWin/TotalWin and loser TodayBattles/TotalLose,
but never winner TodayBattles. TodayBattles therefore ceases to represent all
matches; the unsigned `TodayBattles - TodayWin` calculation used by ranking/reset
can underflow for a winning player. Those are concrete source defects.

The active Honor rewards are custom fifty-loss and hundred-win branches, gated
by configuration/VIP. The loss branch even checks the winner's VIP. The hundred-
win branch also grants configured CPs/RVPs/gold and several items through separate
mutable writes. None of these writes joins an immutable result receipt. They
must not be treated as a verified original daily 20/9 contract or silently removed
without accounting for the intended Revora custom reward policy.

`ArenaTable.Reset` grants `(1001 - rank) * 1000` Honor through mutable statistics.
It does not enforce an upper rank bound before unsigned subtraction, does not
record an immutable settlement receipt and applies only one snapshot per call.
`Qualifier.Reset` sets DayPassed after the first different day but resets that
flag only when the clock returns to the original day-of-year. Normal successive
days therefore cannot reliably run another settlement. The day-of-year comparison
also ignores the year. Source inspection establishes these defects; no production
balances were queried or changed to demonstrate them.

## Proven integration boundary

`QualifierGroup.Import` creates a dynamic map, verifies both transfers and marks
`fightStarted`; `ArenaParticipation.RecordStart` already requires the registered
owned match, canonical sessions, accepted invitations and Fight state. Its saved
receipt is only the player's **first** participation achievement, not a ledger of
every match, and cannot be used to count daily matches or wins.

`QualifierGroup.End` currently selects Winner/Loser and marks Done. `CheckGroups`
waits four seconds, calls Export, then invokes Win. Export unregisters the group,
disposes its map and clears both players' group references. Consequently Win runs
after the evidence needed to prove a registered owned result has been destroyed.
DoQuit and invitation timeouts also invoke Win without an imported fight. Adding
Honor directly to Win would reward unplayed invitations and retain replay races.

Honor Store spending already uses the owner item lock, durable player-row lock,
exact inventory insert, matching solo/team Honor balances and a purchase receipt.
Its preparatory Arena save intentionally flushes legacy in-memory earnings. New
result/settlement writes must serialize with that path and with both Arena save
methods, or a later cache save can overwrite an earned or spent balance.

## Required implementation sequence

1. Introduce a durable match lifecycle keyed by the existing unique match GUID.
   Register both participants and canonical map at successful import. Freeze the
   authoritative terminal result before Export destroys map/group ownership.
   Count only accepted imported matches; cancellation before import earns nothing.
   Represent kill, time-limit and forfeit outcomes explicitly, including durable
   handling when a participant disconnects. Neither a client score nor a public
   helper call is sufficient proof of victory.
2. Commit both participants' match counts, wins/losses and Arena-point changes
   with the immutable result. Lock player identities in stable UID order; preserve
   canonical-session checks and bound retry/cleanup. Choose and document rounding
   for the evidenced three-percent rule; prevent underflow/overflow. Publish
   client/statistic changes only after commit, then export exactly once.
3. Derive the 20-match and nine-win daily entitlements from accepted result rows,
   with unique per-player/day/milestone receipts. Preserve a chosen explicit
   timezone across restarts, midnight and clock rollback. Award the 500 Honor and
   verified EXP Ball together; retain delivery when the bag is full. Never import
   legacy RAM counters as already-verified earned results.
4. Migrate the custom hundred-win reward and any retained fifty-loss policy into
   separate atomic entitlements. Preserve intended configured economy values and
   avoid duplicate grant alongside old code. Normal earning cannot require GM,
   account names or owner allowlists.
5. Replace mutable ranking payout with a durable daily settlement snapshot and
   one award per player/day. Verify the intended Revora ranking table separately;
   the original guide does not supply its exact amounts. Handle offline players,
   rank bounds, consecutive days, year boundaries and restart replay. Keep this
   separate from the daily twenty/nine match milestones.
6. Prove the earning-to-spend route with ordinary non-GM participants: real signup,
   acceptance, import and server combat/forfeit/timeout; midnight and restart;
   duplicate callbacks, failed writes and concurrent saves/purchases; reload and
   Honor-shop acquisition of Sword Soul/Banshee Spirit. The test may accelerate
   its clock and simulate transport, but must not seed earned Honor or victory
   counts when claiming end-to-end earning validation.

This migration must replace the old writers when activated, rather than adding
an independent reward ledger beside them. Existing participation achievements,
loaded-player level/Arena-point transactions, purchase serialization, house boss
rewards and client protocol guards need regression coverage. Native played Arena,
ranking UI, immediate Honor refresh and live restart acceptance remain separate
from server fixtures. No implementation milestone is checked complete by this audit.

## Custom prize dependency found during migration

Source defaults are 5,000 CPs, 250,000 silver, 50 RVPs and minimum VIP 5 for
the 100-win prize; its additional 500 Honor and six item units remain part of
the old writer. The 50-loss branch is disabled by default. The prize includes
P7 scraps whose resulting packs formerly produced unbound souls. Their two
opening paths are now atomic and bound ([fix](P7_SOUL_PACK_TRANSACTIONS.md));
scrap conversion, the custom entitlement and its atomic payout still need work.

The four prize material recipes are now transactional and preserve binding
([conversion scope](ARENA_PRIZE_CONVERSIONS.md)). Custom reward earning and
Sacred Refinery Pack/star-item use are still unfinished; supplied materials in
conversion fixtures are not evidence of earned Arena prizes.

Custom milestone decisions and atomic payout are now implemented as an inactive
participant ([scope](ARENA_CUSTOM_REWARDS.md)). The recipient VIP, economy and
optional loss policy are frozen per milestone. Legacy writer replacement,
pending delivery recovery and the complete played earning path remain open.
