# Earned Elite PK prize claims

Date: 2026-09-16. M6.56c2 server implementation; not deployed.

Every eligible ordinary player can review and collect an earned top-eight prize
from the restored Elite PK Envoy in Twin City, currently 1002 (438,270), subject
to its existing safe-tile placement. The dialogue stays brief: review the prize,
leave enough bag space, and collect. No GM identity or allowlist is required.

## Reward contract and evidence

The [official Elite PK guide](https://co.99.com/guide/prize_upgrade/elite_pk.shtml),
read 2026-09-16, specifies top-eight Envoy claims, an EXP bonus of 100/50/30/15%
for first/second/third/fourth-through-eighth, a matching signup level for that
bonus, and collection before the following tournament. Confidence is high for
these published rules. Its newer rebirth-group prose conflicts with its level
schedule; this implementation retains the separately reviewed 6711 brackets.

The claim reads graph-verified [placement entitlements](ELITE_PLACEMENT_ENTITLEMENTS.md),
never inferring a prize from wings, an old pack, or an incomplete run. The saved
Pacific deadline applies, including daylight-saving changes. A completed run
with no eligible champion may still have earned runner-up/bronze prizes.

All four brackets receive the documented percentage of their current level's
EXP curve when current level equals signup level. A changed level forfeits the
bonus while preserving the item/CP prize. The configured cap gives zero EXP.
Integer rounding follows existing prize arithmetic. Normal curse restrictions
block a nonzero EXP award. A zero-EXP claim does not trigger rebirth recovery.

The Envoy delivers the full existing bundle contents directly. This avoids
paying the same EXP again through an outer legacy pack. Independently owned
720714..720729 packs keep their existing behavior. Nested 720049/720731/729626
packs still open through the [validated nested reward service](ELITE_NESTED_PACK_TRANSACTIONS.md).

Existing Revora item quantities and forced binding are reused. Highest-bracket
CP awards remain 5,000,000 for first through third and 3,000,000 for top four
to eight. These are custom economy amounts, not official parity. That bracket's
legacy top-eight bundle retains one Endurance Book; the official page lists
five. Exact historical economy reconciliation remains separate work. Neither
per-duel EXP/honor awards nor native bracket/prize UI are added here.

## One committed claim

Options 6/7 review and collect. A review is bound to the canonical session,
placement and current level for two minutes. A level change requires another
review. A preview never reserves, consumes or extends a prize.

The owner inventory lock precedes the mentor lock; the service runs outside the
tournament combat gate. Each claim locks its character row and placement, checks
a completed run and its earned/deadline window, then commits these together:

- The entire bundle, with fresh item UIDs, catalog durability and binding.
- Exact CP preimage and overflow-safe balance.
- Managed character EXP/level/attributes and their durable preimage.
- Required level skills, Arena admission, mentor credits and rebirth recovery.
- One `revora_epk_prize_claims` receipt keyed by run and player, referencing the
  saved placement, with signup/claim levels, prize tier, EXP, CPs and item UIDs.

The bag needs the complete number of free slots (2..23); there is no partial
claim or assumption that rewards will merge. Its durable UID set must match
memory. Every required mutation checks its affected row count. Session,
proximity and deadlines are checked again before commit. Item/currency/EXP
client updates follow commit under the same locks. A failed or uncertain
projection fences the session for production reload; the durable receipt
prevents a second payout.

Envoy 2032 continuations use the existing bounded NPC layout: body 20..120,
exact eight-byte trailer, bounded input length, optional matching echoed NPC
UID. No new native packet family or captured-client verification is claimed.

## Validation and release boundary

The isolated claim suite passes 575 checks, including first-rebirth prerequisites.
It runs a complete eight-player bracket in each of four level groups using
ordinary players, real NPC enrollment, accepted attack/death paths, bronze
bouts and production result persistence. All 32 players claim their exact
placement prize through 2032 dispatch, race duplicate requests with autosave,
reload and attempt replay. The highest champion opens all eleven nested packs,
reloads, refines an owned weapon using a resulting Super material and reloads
again. Restarted tournament lifecycle cannot reopen that prize.

Fault coverage includes suppressed profile, CP, late item, receipt, skill,
mentor, Arena and recovery writes; full rollback followed by production reload
and successful retry; exact/full capacity, overflow, changed levels, cap, curse,
expired review/deadline, future result, cancelled/running result, changed
placement, distant/dead/fake/trading/replaced sessions, foreign NPC echo,
stale profile/purse/bag and malformed packet lengths/trailer/input. No live
player rows are used; the disposable validation schema is removed afterwards.

Final validation: isolated Debug x86 build; claims 575, tournament 124 and
owned/nested packs 1,638 (2,337 gameplay checks), plus three achievement audits.
The producer inventory remains 219 present / 118 absent, not per-player proof.
Ignored logs are `obj/chi-refill-validation/elite-claims-fifth.log`,
`elite-claims-tournament-regression.log` and `elite-claims-packs-regression.log`.
Server SHA-256: `B819FC299A6336A36DA49D466B4B19B3EE04C099DBFD9F5939A81D2CDA6FAA2A`.
Harness SHA-256: `FA0FB8F70DFF6FA8F61B7F5D3E3CDF38D87E859EFA015AB0AD7906FFEF940C44`.
No live binary/schema deployment, server restart, client patch or website
publication is included. Genuine 6711 dialogue, immediate reward/EXP display,
nested item use, nearby appearance and reconnect acceptance remain open.
Rollback retains claim/placement receipts; never delete them to enable replay.

## Guaranteed bound P7 soul (2026-09-21)

New Elite PK tournaments add one bound catalog P7 soul to every earned top-eight
Envoy claim. The full original bundle, bracket CPs, signup-level EXP rules and
claim deadline remain. Independently owned outer prize packs keep their original
contents; this addition belongs to the earned placement transaction.

`revora_epk_runs.valuable_reward` defaults false for older tournaments and is
set true only by new run insertion. A preview records the flag and the final
locked read must agree. Existing completed claims remain closed. The soul joins
the same saved reward-UID array as the bundle and commits with CP/EXP and all
progression effects. Capacity is original bundle size plus one for new contracts
(up to 24 free slots); NPC review announces the soul and exact space requirement.

Build and the 637-check Elite prize suite pass, including its prerequisite
first-rebirth checks. Four real eight-player brackets earn all 32 placements:
the level-99 bracket is marked as an older admitted contract before duels and
retains original rewards, while levels 119/129/135 receive the added bound soul.
Every place passes exact bundle/CP/EXP checks, concurrent claim/autosave and
fresh production character/item reload with identical physical item UIDs.
The champion's eleven nested gifts and refinery route still work with the soul
retained. New fault entitlements cover silent soul-insert suppression, receipt
and progression failures; none can commit a partial prize. Those fault fixtures
are distinguished from the actual combat-earned placement journey.

Log: `obj/chi-refill-validation/elite-prizes-p7.log` (ignored). A second actual eight-player tournament now exercises new lowest-bracket
contracts without editing their reward flags. All eight places earn their bound
P7, original bundle and exact EXP; fresh character/item reload preserves every
UID and rejects claim replay. This closes the new-contract lowest-bracket earning
gap while retaining the separate old-contract bracket test. The shared combat
fixture drives both tournaments through admission, damage and ordinary deaths.
Follow-up log: `obj/chi-refill-validation/elite-prizes-low-bracket.log` (ignored).
Native prize presentation and live release remain pending. No client file changed.


## Retained calculated champion and cold prize (2026-10-03)

The existing retained ordinary character4294907001 continues after the10/10
Way of Heroes checkpoint. Its current equipment and progression are loaded;
no weapon, skill, damage value, death or winner is supplied. Two ordinary
players register through envoy448582. The controlled opponent has supplied
level140/two-rebirth eligibility and does not retaliate. With approach, attack
cooldown, attackability and event clock controlled, `Attack.HandleAttack`
calculates a single melee hit that defeats the opponent from11,559 HP. The
saved match has positive damage and outcome `defeat`; the run completes with
the retained character as champion. This proves the calculated route, not
competitive balance or authenticated TCP/native combat.

`career-retained-elite-combat-win`, `...-claim` and `...-reload` all pass in
separate game processes with intervening database restarts. The saved victory
is initially unclaimed. In the next process, ordinary NPC offer/confirmation
pays exactly5,000,000 CPs and24 items, including one permanent bound catalog-P7
soul. The actual24 item UIDs equal the claim receipt. The full nested career,
event/placement and prize snapshot survives another restart unchanged;
repeated confirmation and fresh offer/confirmation attempts grant nothing.
The earlier11123 receipt is preserved, not deleted/reissued to manufacture
new achievement credit. No achievement or reward-family total increases.

The first attempt stopped before registration because the fixture addressed
the envoy while still on its saved warehouse map. Three completed ordinary,
zero-currency trades had already freed bag space. Recovery checks those exact
receipts and no player entry in the abandoned calendar slot, preserves the
transfers, explicitly approaches Twin City, and uses the following weekly slot.
It does not roll back possessions or replay the trades. The final snapshot is
`revora_retained_elite_combat_probe`, step2; older career snapshots are historical
and must not be rerun as the current preimage.

Source: `tools/CheckCandidateRetainedEliteCombat.cs`. Private executable SHA256
`943AA1FBE2BF19433045F4CAF5CDA0FE4BCBEF2EC1EFAE9CD703995346FC6435` is unchanged.
Logs: `C:/Revora/DisposablePostgres/moon-box-candidate/arena-process-career-retained-elite-combat-{win,claim,reload}.log`.
Unrelated private vote-pulse PostgresException messages remain outside this check.
No gameplay implementation, client patch or live deployment changed. Earlier
funding/qualification provenance, natural-calendar/native acceptance and the
complete compatible320-achievement career remain separate open requirements.

## Fresh earned champion and runner-up (2026-10-03)

`career-fresh-elite-prepare/earn/claim/reload` all pass on their first executions.
The ordinary level132 Warrior1096322 and earned level130 Trojan1096323 enter the
Envoy448582 through option3 in run `epk-20410705-3`. Preparation deposits24 owned
items through Desert City warehouse10011, preserving the Monthly PK checkpoint.
Two calculated melee attacks produce a saved defeat and champion; no levels,
damage, deaths, placements or rewards are supplied. Both players return alive
to Twin City with their original Peace mode before the unpaid-victory restart.

After a separate game/database restart, each player requests the ordinary
prize offer6 and confirms7. Champion rewards are5,000,000 CPs and24 items,
including exactly one bound P7;77,302,211,292 EXP advances the main to133.
Runner-up rewards are5,000,000 CPs and12 items, including exactly one bound P7;
4,294,567,294 EXP retains level130. Both EXP amounts equal their installed
signup-level curve allowance (full/half respectively). Prior bag UIDs remain,
and each new reward UID matches the durable claim receipt.

Repeat confirmation and repeat offer/confirmation pay nothing. A final separate
cold process preserves exact nested career/roster, results, claims and all reward
UIDs, compares each of the five loaded bags with database UIDs and verifies the
main's11123 evidence. The authoritative checkpoint is
`revora_fresh_elite_probe`, stage2; previous probes are historical. Future phases
must continue loading Desert City alongside the other three used warehouses.

Dates, deadlines, attack positions and cooldowns are controlled; the synthetic
sessions do not prove natural retaliation, full-bracket competitive balance,
wardrobe rendering or native login. This batch validates earning and prize
persistence, not native title/wing acceptance. Live deployment remains pending.
No production code/build changed. Candidate SHA256 remains
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
Redacted private logs are named for the four phases above.
