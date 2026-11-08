# Market Errand daily completion

The ordinary PurseClerk (NPC 900000015, Twin City 418,330) offers one Market
Errand completion per UTC day. Accept, enter the real Market map1036 through
server-accepted travel, and return to the clerk. Dialogue remains brief.

## Reward and persistence

Completion grants the existing configured CP, silver and RVP plus one bound
level-one Tortoise Breaker rune (4030101). It grants no P7 soul. Acceptance,
recorded visit and completion use the existing durable variable vault:
`market-errand` stages 1 (accepted), 3 (visited), 2 (completed), with the existing
`market-errand-day` UTC stamp. Completed historical days remain completed.
Previously accepted errands require a new accepted Market arrival; an old
PreviousMapID no longer qualifies as a visit.

The accepted-arrival callback records a visit only for the canonical loaded
player on the actual map1036 instance and valid accepted coordinates. Walking,
jumping, teleporting or loading into that map after acceptance can record it.
The visit survives reconnect and travel through other maps before returning.

Acceptance and completion require proximity to the real clerk. Session, item and
vault ownership protect updates. Completion checks saved purse preimages and
live/saved bag capacity, then commits all currencies, the bound rune and daily
completion together. The existing vault merge preserves unrelated quest changes.
Post-commit installation updates live balances and inventory. Failure retains
the saved visited state and fences the character for reconnect. A new UTC day
allows another acceptance, which requires another visit before payment.

## Validation and remaining gates

Staged source build and **441 focused checks** pass: 406 fixture prerequisites
and 35 Market Errand assertions. Ordinary managed characters accept at the clerk,
reject a prior Market visit, travel via production Teleport/accepted-arrival,
reconnect with the visit, complete, autosave and reload the exact bound rune.
Checks cover daily replay, next-day fresh visit, failed item insert, saved-only
full inventory, external RVP conflicts and recovery without partial payment.

Tracked `VerifyMarketErrand` is also registered in the purse review suite.
Ignored evidence: `obj/market-errand-build.log`, `obj/market-errand-tests.log`.
Only the focused suite was run. The fixture supplies installed terrain and the
clerk; it does not verify native NPC/portal presentation, rune equip/use or the
complete genuine-client walk. Native acceptance and deployment remain pending.
No production restart, schema migration or client patch was performed.


## Installed Market route and backward-clock repair (2026-10-01)

The installed Twin City portal312,344 leads to Market1036 (landing211,196).
Portal475,293 instead leads to promotion hall1004. The prior service and focused
fixture incorrectly treated1004 as Market, so a player following the real Market
route could not earn visit credit. Both the clerk directions and accepted-arrival
map/base checks now use the installed Market route. Promotion hall visits do not
advance the errand. Existing completed rewards and saved legacy visit flags are
preserved; this change does not revoke or repay historical claims.

Evidence: private installed `database/Portals.ini`, SHA-256
`4A9A681103915CA232BE7C98DEFD69CF08AE5639205822967EE678985EA0AAA9`;
loaded-map portal assertions and normal UsePortal dispatch. The separate native
promotion-school probe also identifies475,293 as entry to1004. The proprietary
portal file itself is not added to Git.

The old date test rejected only an exactly matching completed day. Going backward
could accept a new contract over a later completed stamp. Menu eligibility now
recognizes later completed dates, and mutation refuses any date earlier than the
saved stamp. Normal next-day acceptance still requires a fresh visit. The existing
UTC calendar, prices and rewards are unchanged.

The composed-process route uses retained ordinary character4294908703:

- Accept at the loaded clerk; premature claim pays nothing. Enter promotion hall
  through its actual portal and return: state remains accepted. Enter the actual
  Market through312,344: the accepted-arrival callback saves visited state.
- Exit the game abruptly. Fresh game/database startup preserves visit, inventory,
  currencies and prior event records. Market NPC45 returns the player to Twin City;
  the clerk grants exactly800CPs,8RVPs,15,000silver and one bound level1 rune.
- Repeat acceptance/claim at the same date and a controlled previous date. Neither
  changes the paid vault, items or purses. Cold claimed-state reload repeats those
  checks against the complete saved snapshot. No daily P7 is added.

The initial diagnostic used the old1004 contract on character4294908702 and failed
at the missing Market return NPC; it issued no reward. That unpaid historical
fixture remains untouched. The corrected route uses a distinct ordinary character
and separate probe, rather than converting the wrong-map visit into evidence for
Market arrival. The focused source fixture was also corrected to1036; its wider
purse suite was not rerun in this batch.

Build and all three process phases pass, with existing build warnings.
`ErrandCareer` owns the three
`career-errand-visit/claim/reload` phases in `tools/CheckCandidateCareerRites.cs`.
Calendar and approach coordinates are controlled; native presentation, continuous
walking and deployment remain unverified. No live migration/restart occurred.
Private executable SHA-256:
`B08AF6399258E9F2A07BC8557018FEBA5F7EF3D1A78208D4C67738BB12C5FCA6`.
