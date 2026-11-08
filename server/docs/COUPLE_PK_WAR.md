# Couple PK: Revora NPC-ready edition

2026-09-15, baseline `2749534`. Code and automated validation complete;
deployment and genuine-client acceptance pending.

The [official guide](https://co.99.com/guide/quests/couplepk.shtml) establishes
Friday 21:00-22:00, enrollment before 21:09, married two-person teams and joint
entry. It uses holding hands or hugging at the guide. The existing server paired
action handlers do not retain a trustworthy request/consent history and their
numeric pose meanings have not been verified for this client. This restoration
uses a brief, explicit confirmation from each spouse at the guide. Native paired
animation verification remains separate; arbitrary interaction fields do not
authorize tournament entry.

The husband leads a team containing exactly the two spouses. Both ordinary live
characters must stand near the guide, have reciprocal saved spouse/name records,
and have no other tournament reservation. His invitation lasts ninety seconds;
only that exact live spouse session can confirm it. Both owner locks are acquired
in UID order before the shared tournament gate. The pair becomes admitted only
after both arena transfers and its durable entry succeed. Forged confirmation,
stale sessions, changed teams/marriages and partial transfers cannot create a
competitive field or participation evidence.

Restore guide 2319, mesh 2910, near Twin City (444,290). Awarder 23140, mesh 2920,
serves only the owned arena. The installed 1090 floor is 192 by 192 with 9,523
valid tiles; SHA-256
`dffa4c67b899154ef0cc436a7318f2b10d55faca7fe43129e91de981cd26c20f`.
Use a private copy of that floor, bounded to thirty-two admitted/reserved pairs.
Old map-alive-count purse handlers stay intercepted, including old locations.

The selected Revora rules use Pacific time and UTC persistence. Combat begins
after a pair's ten-second preparation. The earliest victory is 21:10, after all
admissions close. A pair loses when both spouses are defeated; a downed spouse
returns to Twin City and waits online for the outcome. A surviving spouse may
win for the pair. Leaving voluntarily, disconnecting, breaking the marriage or
changing the two-person team forfeits the pair. A defeated member cannot return
to the field or attack it. Allies cannot damage each other. A champion requires
at least two genuine admitted pairs and positive accepted damage to opponents.
An uncontested pair or unresolved 22:00 deadline produces no champion.

Both admitted spouses earn 11115 only after a genuine competitive field exists.
Both members of the victorious pair earn 11121 with the saved result. Each gets
a once-only purse of 50,000 CPs, 500 RVPs, 500,000 gold and two bound EXP Balls,
plus seven days of the native spouse halo (status effect 51). Currency amounts,
NPC confirmation, retreat/forfeit rules and the earliest result time are explicit
Revora choices. Do not substitute Couples vs. Singles Heart wings or another
event's wardrobe rewards.

Purse claims remain saved across full bags and reconnects. Result-time expiry
cannot be extended by claiming later. Interrupted runs cancel on restart while
completed results survive. Native effect 51 is present in the reviewed client
status-effect catalog; compact legacy database key 8 is not a title-list grant.
Live combat, effects and genuine-client NPC acceptance remain pending separately.

Validation: the isolated .NET Framework build and 42 Couple PK checks pass.
They exercise ordinary spouse sessions through the NPC dispatcher, expiring and
stale consent, concurrent confirmation, partial import/reconnect, real jump and
damage paths, ally/outsider rejection, defeated-spouse reservation, team/marriage
forfeits, deadline closure, atomic result/claim rollback, concurrent individual
purses, expiry and restart cancellation/replay. A failed result write retains
captured standings and result time even if a winner departs before the retry.
The 888 existing quest/event/progression/wardrobe regression checks passed before
the final standings-snapshot change; the 42 Couple PK checks passed again after it.
Five catalog/prerequisite audit checks also pass. Final staged server SHA-256:
`ECBDD8B5D872752F6EE1B5476A90524EC9A1879345B34436F71A5405F96D9F68`.
Tests use disposable persistence and do not install or deploy the staged executable.

## Bound P7 reward for each champion (2026-09-21)

Every spouse in the winning pair of a newly created tournament receives one
bound catalog P7 soul beside their existing two bound EXP Balls, 50,000 CPs,
500 RVPs and 500,000 silver. Each spouse has an independent `(run,uid)` claim.
The reward belongs to the saved winning pair, including a spouse defeated earlier
in the event; participation alone still earns no champion purse.

The saved run's `valuable_reward` flag defaults false for older tournaments.
New `Open` inserts set true; conflicts do not replace old contracts. Claim
capacity requires three free slots for new runs or two for old ones. The soul
UID shares the existing reward array. Currency, both EXP Balls, the soul and
checked claim insertion commit together. Historic paid claims remain closed.

The isolated build and 46 Couple PK checks pass, including ordinary spouse
invitations, owned combat, concurrent individual claims, exact capacity, purse
and receipt rollback, saved soul binding and coordinator recovery. Both actually
earned new purses are then loaded through fresh production character/item
loaders; every UID and currency survives and claim replay pays nothing extra.
Full character profiles are saved before claims, not afterward. A separately
earned older pair result survives restart and repeated claims with its original
two-ball purse for each spouse. No champion/result rows are fabricated for these
reward routes; the old contract flag is fixture setup before enrollment.

Log: `obj/chi-refill-validation/couple-pk-p7.log` (ignored). NPC wording announces
the new reward. Genuine-client presentation and live release remain pending;
the fresh-character fixture is not a native socket login test.

## Earned marriage through Couple PK (2026-09-25)

All Couple PK spouse fixtures now begin single and establish their links through
the actual marriage request/accept handler, earning achievement 10129. The two
initial competing couples save their complete character profiles before marriage,
then reload spouse identities and achievements before event enrollment. The
fixture invokes the normal CalculateStatBonus/CalculateHPBonus methods after loading;
the shared character-loader helper alone does not perform the login stat phase.
Without that phase MaxHitpoints was zero, which correctly prevented continued
participation after transfer. No event eligibility rule was weakened to pass it.

The existing NPC admission, invitation replay/expiry, contested combat, pair result,
individual claim, bound-P7 contract and reward-reload checks now run from those
earned marriages. All 60 Couple PK checks pass against the existing isolated
server assembly. Exact soul/item UIDs and both champion purses survive production
loaders and claim replay. No prize/result rows are supplied for the winning route.
The fixtures still provide the two-person team, qualifying character stats,
controlled event time and combat outcomes. This is server-handler integration,
not native socket/UI acceptance or validation of team creation itself.

No gameplay code, reward values, live database, deployment or client patch changed
in this batch. Genuine-client presentation and live release remain pending.

## Saved couple champions through full process restart (2026-10-01)

Four ordinary level-100 fixtures (4294908701-4294908704) form two couples through
the actual marriage request/accept handler. Both teams use normal create,
invite and accept packets; spouse invitations use the Guide's issued consent
codes. Initial profiles, sexes, approach coordinates and event time are controlled.
This is not native character creation, natural calendar observation or a socket
marriage/team test.

The first controlled October2 window correctly records no champion: the fixture
sent its damage during the ten-second protection period, and saved pair damage
remained zero. Those completed/no-prize rows remain intact. The same saved
marriages continue into the October9 window, using normal revival where needed.
After protection expires, accepted damage plus controlled death callbacks defeat
both opponents. Finalization saves the champion pair and participation/win
receipts, returns all four players to town and leaves both prizes unclaimed.
No result, marriage or payout row is injected/reset.

Abrupt process exit and game/database restart preserve the complete saved result,
marriages, belongings and earlier Ancient Devil/event snapshots. Each champion
independently claims through CouplePKGuide2319: 50,000 CPs, 500 RVPs, 500,000 gold,
two bound EXP Balls722136 and one bound catalog P7 soul. Exactly two claim records
are written. Winner/loser repeat claims do not change any saved purse, item UID,
result or achievement. A further full restart preserves that exact snapshot.

Passing phases: `career-couple-win`, `career-couple-claim`,
`career-couple-reload`, for run `cpk-20261009`.
Checkpoint: `revora_couple_process_probe` done=true / `CoupleProcessSnapshot`.
Candidate SHA-256:
`F5683ADB48B1D7015F0B998AA48F569431955CAA298FD506FE0BA78F35F93B96`.

Combat uses a controlled one-point ReceiveAttack followed by ordinary Entity.Die
callbacks. This verifies contested eligibility/result delivery, not damage
calculation or fair class balance. Mid-fight crash, interruption between spouses'
claims, native UX, natural calendar and deployment remain outside this batch.
No gameplay source, live restart or release changed.

## Interrupted-fight recovery gap (2026-10-01)

Source review after checkpoint38e37e9a identifies a separate unfinished lifecycle
requirement. The saved-champion and prize checks above remain valid; they do not
prove recovery of players still fighting when the coordinator is lost.

- `Game/Features/CouplePkEvent.cs`: `Fighter.Mode` and `members` hold the original
  PK mode and session ownership only in memory. `Confirm` reserves the pair,
  then `Admit` transfers both spouses before committing admission. No durable
  return reservation is written before either transfer.
- `Game/Features/CouplePkResults.cs`: `Initialize` cancels open runs, but its
  schema stores neither original PK modes nor pending return destinations.
  Cancelling the result prevents an invented champion; it does not recover the
  interrupted players' original modes.
- `CouplePkEvent.OnLogin` restores a mode only from the current `members`
  dictionary, and recognizes an arena location only by the current arena ID.
  `Game/Map.cs` allocates dynamic IDs from current runtime state; equality with
  the old arena is not a durable ownership contract.
- `Database/EntityTable.cs` does send an unknown saved map to Twin City
  (300,278). This reduces the location risk; it does not restore the missing
  original mode or identify a still-existing/reused dynamic ID. `Entity.PKMode`
  is an in-memory property, not restored by this character loader.
- `tests/PotencyHunt/CouplePkTests.cs` creates an `interrupted` pair and replaces
  the coordinator, but checks cancellation and previously paid champions rather
  than calling login recovery for that interrupted pair. Thus its passing
  restart assertion does not cover this defect.

Repair acceptance checklist (updated below):

- [x] Save each spouse's original mode and arena ownership before any transfer;
  retries must preserve the original mode and reject unrelated pending sessions.
- [x] Recover each spouse independently after coordinator/process loss, even
  if the loader already used its unknown-map fallback or dynamic IDs changed.
- [x] Acknowledge return only after the destination is durably saved; retain a
  retryable record on transfer/save/acknowledgement failure.
- [x] Test admission failure between transfers, two distinct original modes,
  cancelled-round login, repeated recovery and no unearned reward. Preserve
  same-window unadmitted reservation retries and previously paid claims.
- [x] Verify a real process interruption and subsequent cold reload privately,
  then update this checklist from observed evidence.

`LocalPkResults.PrepareReturn/CompleteReturn/RecoverReturn` is an existing
location-recovery reference, not a drop-in replacement: Couple PK must preserve
its two-spouse admission, early defeated-spouse return and reservation retry
semantics. This finding is source evidence, not a newly reproduced crash or a
shipped fix. No gameplay, database, runtime or release changed in this review.

## Durable interrupted returns (2026-10-01)

The subsequent implementation adds `revora_cpk_returns`, linked to actual
per-player entries. Both spouses reserve their arena and original mode before
either transfer. Pending retries retain the original mode; a returned record
can be rearmed only for an unadmitted reservation. Another pending Couple PK run
prevents admission. Location-only writes lock the owning entity row under the
existing tournament gate, without taking the inventory persistence lock.

Normal departure and defeated-spouse retreat acknowledge the return only when
the database location matches the current location outside the saved arena.
Login recovers pending records independently of dynamic map identity, saves the
town destination and then acknowledges it. Failed acknowledgement leaves the
record available for retry. Completed returns do not teleport players again.
Existing claims and prize contracts are unchanged. Historical sessions without
return records cannot have their missing original modes reconstructed.

Private process phases `career-cpk-return-enter`, `career-cpk-return-recover`
and `career-cpk-return-reload` use the existing ordinary married champions
4294908701/4294908702 in a new October16 window. Normal team/NPC consent admits
both with controlled Capture/Team starting modes. Abrupt exit while admitted
leaves two pending returns. The next process cancels the unfinished run; even
after an explicit unknown-map fallback simulation to town(300,278), login
restores both modes and town(408,330). Repeat recovery and prize requests add
no reward. All earlier BP/Couple/Ancient nested reward snapshots remain exact.

`career-cpk-return-fault` repeats admission in the October23 window and rejects
the return acknowledgement using a named disposable CHECK constraint. The first
spouse reaches a saved town destination with the correct mode, but both return
records remain pending. Removing only that fault allows ordinary login retry to
return both spouses, clear their callbacks and acknowledge both records without
any prize or changes to their prior rewards.
A further full game/database restart after this fault/retry also passes the
cold location, prior-reward and no-duplicate-prize checks.

The first cold fixture incorrectly required the landing coordinates after its
own prize-NPC approach had changed and saved them. The correction asserts that
completed-return replay preserves the loaded coordinates, while first recovery
still requires exactly(408,330). The corrected cold check passes. A compile-only
ambiguous `KnownPersons` reference was qualified before the first admission.

Isolated production build passed; candidate SHA-256:
`8424C9F7FB1FAB516906EA053C7A54329CC029FC1367C1F214F40FC626846A08`.
The between-spouse transfer fault and same-window reservation rearm regression
were still pending at this checkpoint and now pass in the follow-up below.
Controlled clocks/approach, server handlers and
private processes do not establish native UX, natural calendar, live deployment
or general PK-mode persistence outside this recovery path. No live restart or
client release occurred.

## Second-spouse transfer failure and reservation retry (2026-10-01)

`career-cpk-retry` and `career-cpk-retry-reload` pass on the same candidate
8424C9F7FB1FAB516906EA053C7A54329CC029FC1367C1F214F40FC626846A08.
The existing married ordinary players enter the controlled October30 window.
Normal team packets and Guide invitations establish consent; the first Confirm
call is invoked directly so the fixture can inspect the exact injected database
exception without the NPC dispatcher's persistence fence.

A named disposable CHECK constraint rejects only the wife's arena map write.
The husband has already reached the arena; the wife remains in town. Both
pending return records exist and the single reserved pair remains unadmitted.
After removing only that fault, production login recovery returns the husband,
cleans both callbacks, restores Capture/Team modes and acknowledges both returns.
No event or prize row is fabricated or deleted.

The players select Revenge/Guild and obtain a new Guide invitation in the same
window. This confirmation uses the ordinary NPC dispatcher. It admits the same
saved pair ID, keeps exactly two entry records, rearms exactly two return records
with the newly chosen modes, and sets both fighters to PK mode in the arena.
At ten minutes the uncontested round completes without a champion, returns both
players with Revenge/Guild and acknowledges their records. Repeated clerk claims
pay nothing. A full game/database restart preserves the completed no-winner run,
both acknowledged returns and exact earlier BP/Couple/Ancient reward snapshots.

Both phases passed on their first execution; no production rebuild was needed.
This closes the two pending admission regressions above. It does not expand the
native, natural-calendar, general-mode-persistence or deployment claims.

## Retained career victory and cold prize collection (2026-10-02)

The retained ordinary character4294907001 enters with its saved spouse4294908901,
continuing the paid gender rite, flowers and marriage checkpoint. Existing married
characters4294908703/4294908704 supply the opposing pair; their assets are retained.
Normal team packets, Guide2319 invitations and spouse confirmation admit both pairs.
The controlled Friday21:00 Pacific window is2035-01-19 (`cpk-20350119`).

The retained bow deals calculated ranged damage through `Attack.HandleAttack`:
two shots defeat each opponent. No hitpoint assignment, injected damage or direct
Die call produces this victory. The production result closes after ten minutes,
returns all four players to town and persists an unclaimed champion entitlement.
Actual game/database restart precedes both spouses' Guide option3 claims. Each
receives exactly50,000 CPs,500 RVPs,500,000 silver, two bound722136 items and one
bound P7 soul. Exact item UIDs, currencies, run, pairs, returns, claims, marriage,
Magnolia rewards and prior career snapshots survive another complete restart.
Repeated winner claims and losing-pair requests change nothing. Participation11115
and victory11121 coexist on the retained main character.

`career-retained-couple-{win,claim,reload}` pass on unchanged candidate SHA-256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
Checkpoint `revora_retained_couple_probe` is step2. The final cleaned fixture also
passes reload after removal of its one-time recovery branch.

The initial fixture omitted its phase allowlist and failed before startup. After
that correction, both pairs entered the January12 window, but offline players
remained unattackable because their ordinary timers are disabled. No victory was
fabricated. Full snapshot comparison found only two new participation receipts
(main and spouse), with all earlier assets unchanged. Production startup cancelled
the interrupted run; a one-time exact-hash guard verified this state and normal
`CouplePkResults.RecoverReturn` restored its entrants. The cancelled run and its
participation evidence remain saved with no prize. The corrected next-week fixture
accounts for elapsed combat protection using Attackable/CantAttack controls;
calendar, approach and attack cooldowns are also controlled. Opponent equipment
and prior career funding retain their documented supplied/mixed provenance.
This proves the server earning and persistence route, not competitive balance,
authenticated socket/native UX, natural calendar timing or live deployment.

## Fresh earned couples and cold champion prizes (2026-10-03)

The fresh career supplies all four ordinary participants without new level,
stat, gear or currency assignments. The main level 133 Warrior and female
level 130 Warrior retain their earned marriage. The existing level 70 Archer
uses Sereth's paid gender-change options 40/41/42 with earned CPs, persists
body 2001 through a full restart, and marries the existing level 130 Trojan
through normal proposal/acceptance. Immediate repeat gender payment and
repeated marriage acceptance are refused without a second mutation.

The current Couple PK rules do not impose a level bracket. Both two-person
teams form through ordinary create/invite/accept packets and enter through
Guide 2319's invitation and spouse confirmation. The controlled window is
Friday October 11, 2041 at 21:00 Pacific (`cpk-20411011`). Three calculated melee
hits from the main Warrior defeat the two opposing players. No supplied
damage, hitpoint assignment, direct death call or seeded result creates the win.
The coordinator returns all four to town; ordinary revival restores the losers.

Separate game/database restart precedes prize collection. Each winning spouse
receives exactly 50,000 CPs, 500 RVPs, 500,000 silver, two permanent bound 722136
EXP Ball items and one permanent bound P7 soul. Both winners receive 11115
participation and 11121 victory evidence. The final reload checks all six exact
reward UIDs, types, permanent binding, currencies, result/pair/claim/return
receipts, four saved profiles and nested previous career state. All four players
attempt the claim twice without additional payout.

All five gender/marriage/win/claim/reload phases passed on the first execution.
Evidence: CheckCandidateFreshCouple.cs, private candidate SHA256
D48A6C00F6BF84889581ADFEABEB924C4C7448C5DC3C6D17F8674129C43665B6.
Calendar, proximity, invitation/stage waits, attack cooldown and revival delays
are controlled; the opponents do not retaliate and the second winning spouse
is an admitted teammate. This proves the fresh server earning/recovery route,
not competitive balance, natural scheduling, native UI or live deployment.
