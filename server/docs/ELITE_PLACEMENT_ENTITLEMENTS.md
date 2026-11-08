# Earned Elite PK placements

Date: 2026-09-16. M6.56c1 server prerequisite; not deployed.
The Envoy prize payout remains M6.56c2. A saved entitlement is not yet a usable
prize service, and this milestone does not mark the full earning journey done.

## Owned third-place fight

A semifinal loser from a contested defeat/timeout stays in the waiting hall,
heals and receives a brief invitation to fight for third place. When the final
begins, the two eligible semifinal losers fight a bronze bout concurrently in
a separate existing arena slot. It uses the same ordinary-player admission,
preparation, damage, spectator exclusion, timeout, forfeit and cleanup rules.
The cached arena count is unchanged. Bronze winners never advance into the
championship final. All participants return home when their match is exported.

In a three-player bracket there is one real semifinal loser, who takes the
bronze bye after having fought. A bronze forfeit gives the present opponent
third place. If both bronze opponents remain idle, neither receives third;
both retain the same fourth-through-eighth reward tier. These attendance and
bronze-resolution details are explicit Revora rules, not asserted historical
6711 timing or native bracket behavior.

## Verified result transaction

`revora_epk_matches.kind` separates elimination and bronze bouts. Saved pair
replay checks include this role. `ElitePkPlacements.Record` validates the entire
completed bracket within the existing locked finalization transaction:

- Registered entrants have valid level/bracket identities and the run has at
  most 32 entrants.
- Every bout is resolved, has distinct registered fighters, nonnegative bounded
  damage, a valid outcome and a winner consistent with its participants.
- Each elimination round follows the previous round's winners exactly once.
  Pair count, slot sequence, byes and contiguous round numbers must agree.
- Timeout winners agree with saved damage, signup BP/level and seed tiebreaks.
- Bronze fighters are genuine losers of contested semifinals; a finalist or
  unrelated entrant cannot receive a bronze placement.
- A final exists and leads to one remaining winner. At most eight eligible
  entrants may receive placement records.

A player must have participated in a contested elimination bout to receive a
prize entitlement. Byes or absence alone cannot create one. The champion also
must have won a contested bout before receiving first prize or champion honors.
If an untested finalist wins by forfeit, other players still retain placements
earned through their own combat. Such a run can be `complete/uncontested` with
valid non-champion placement rows; a future claim must use those rows rather
than assume every eligible run has `outcome=champion`.

`revora_epk_placements` stores run, player, bracket, signup level, placement,
existing prize-bundle type, earned time and deadline. Places 1/2/3 distinguish
champion, runner-up and bronze. Place 4 covers defeated/unresolved bronze
qualifiers; place 5 denotes the tied fifth-through-eighth elimination tier.
All fourth-through-eighth prizes use the same existing bundle for their bracket.

Every placement insert, champion honor and final run-state update commits
together. Silent failed writes throw and roll back the whole finalization. The
frozen result retries without another fight. A replay of completed finalization
cannot add records or extend deadlines. Restart preserves completed placement
rows while cancelling interrupted runs. Historical completed runs are not
silently backfilled from titles or assumed ranks.

## Evidence and claim deadline

The [official Elite prize guide](https://co.99.com/guide/prize_upgrade/elite_pk.shtml)
distinguishes the top three and fourth-through-eighth rewards, describes a
signup-level condition for bonus EXP, and gives players until the following
weekly tournament start to claim. Its newer rebirth-group prose conflicts with
its level-based schedule; this change preserves Revora's reviewed 6711 level
brackets rather than silently changing admission rules.

Each saved deadline is the next Friday start for that same bracket in Pacific
time. Conversion uses the timezone rules; crossing daylight saving produces
167 or 169 elapsed hours where appropriate, not an assumed 168 hours.

Native bracket, ranking and wager families remain disabled. Existing NPC,
movement and combat paths are reused; no new packet layout is claimed.

## Validation

The isolated x86 build passed 124 Elite PK checks and three producer audits.
The ordinary-player fixtures exercise 3-, 4-, 5-, 8-, 16- and 32-player combat
brackets, a lower-level final, bronze byes/forfeits/idle outcomes, an unearned
champion with earned runner-up/bronze placements, ordinary return and existing
champion wardrobe behavior. Fighters and lethal damage are fixture supplied;
accepted damage/death dispatch and actual tournament scheduling do the work.

Failure fixtures cover invalid fighter/round/role/timeout/bronze records,
suppressed placement and final-state writes, result retry, duplicate finish and
restart. Both Pacific daylight-saving transitions are exercised. Tests use only
the disposable validation schema. No production account, item or map data was
changed. The producer audit remains 219 present / 118 absent, not per-player
completion proof.

Final server SHA-256:
`B9DA093B976C1C54EEB19165890BBCF3C7BC8736507ACFE2F61B4F125300B221`.
Final harness SHA-256:
`72DFB01926DE307A16EEF0AC917C2C3DA76D990EE744753C53FC2BB33F6F82DC`.
Ignored log: `obj/chi-refill-validation/elite-placements-final.log`.

Next: add a physical Envoy claim with the complete configured prize bundle,
correct signup-level EXP eligibility, atomic inventory/progression/mentor writes,
one-time recovery and ordinary battle-to-nested-pack-to-reward validation.
Delivering the actual bundle at claim can avoid duplicating EXP through an outer
legacy pack; existing independently owned legacy packs must retain their rewards.
Full native 6711 journey and live deployment/restart acceptance remain open.

2026-09-16 claim follow-up: The Envoy payout and full ordinary battle-to-nested-reward journey are now implemented and isolated-tested in [earned prize claims](ELITE_PRIZE_CLAIMS.md). The original prerequisite results above remain historical; live deployment/native acceptance are still pending.
