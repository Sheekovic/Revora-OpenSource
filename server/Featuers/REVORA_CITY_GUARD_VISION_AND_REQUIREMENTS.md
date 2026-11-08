# Revora City Guard — Vision and Gameplay Requirements

> A functional design document for Codex. It defines the intended player experience and leaves architecture, protocol integration, implementation, and deployment decisions to Codex, using the current source context.
>
> Audience: all eligible Revora players on the current client profile. This is not an owner-account or GM-only feature.

## 1. Concept and Purpose

An intelligent guard system inspired by GTA pursuit mechanics, adapted to Conquer. Cities and gates have guards that respond to player crimes and PK/Wanted status, pursue aggressors, and maintain reasonably safe spaces for beginners, trading, and social activity.

Players should understand the consequences of aggression and have meaningful opportunities to escape or fight. The system must not turn the game into permanent punishment or prohibit legitimate competition. Pursuits should be understandable gameplay events. Helping the community deal with wanted offenders should offer rewards through Honor or Tokens.

## 2. Agreed Core Rules

| Situation | Required behavior |
|---|---|
| Killing an innocent player | Record the crime even outside a city and update pursuit eligibility according to the system rules |
| Blue player | A warning when blue status is the only applicable trigger |
| Red player | Immediate pursuit when detected by guards |
| Black player | Wanted across the server, attacked on sight by guards, with an eligible kill reward under the reward rules |
| PK Points warranting guard intervention | Immediate attack when detected by a guard |
| Attacking a guard | Transfer the aggressor to an Arena encounter against a strong guard, without a warning phase |
| Leaving the guard Arena encounter | Defeat the guard or pay a ransom |
| Evading pursuit | Leave tracking range or remain hidden for a meaningful duration |
| Maximum continuous pursuit | Five minutes |
| Rewards | Honor Points or Tokens, redeemable at the Commander |

Rule precedence: attacking a guard invokes its specific Arena route. During ordinary pursuit, black status, red status, or qualifying PK Points take precedence over the warning associated with blue status alone. A warning must not prevent intervention when an independent immediate-intervention condition applies.

Codex should select and document exact PK thresholds and eligibility in a way consistent with current Revora rules. This discretion does not change the core behaviors above.

## 3. Guard Appearance and Identity

- A guard should appear as a human fighter wearing visible equipment and carrying a weapon, appropriate to Conquer's visual style and Revora's identity.
- Each guard has a recognizable name, identity, position, and role. Players can distinguish guards from ordinary players and regular monsters.
- The server controls guard behavior; no human-operated account is required to represent a guard.
- Equipment, movement, and state must appear consistently to every player observing the same guard.
- The Commander has a distinctive visual and functional identity as the main interaction point for the system and its rewards.

These appearance and behavior outcomes are the requirement. Codex chooses the appropriate technical representation in the current server and client.

## 4. Protected Areas and Crime Detection

Guard responsibilities begin with cities, gates, entrances, and player gathering areas. Codex should choose practical coverage based on the actual maps and player movement, providing useful protection without congestion or blocking travel.

Recording a crime and detecting an offender are separate concerns. Killing an innocent outside a city is recorded, but does not imply that a guard instantly appears beside every offender anywhere. A black-status offender is wanted across the server and becomes a target when detected by guards that can reach them.

Define an innocent player and unlawful aggression using the current PvP rules. Legitimate self-defense, fighting an eligible wanted offender, and combat in Arenas or competitions that authorize it must not automatically count as killing an innocent. Document each context to avoid punishing legitimate gameplay.

Attribute a crime to the actual aggressor. Account for area attacks, indirect damage, and player-controlled entities where those systems exist, without punishing bystanders or an entire team merely for membership.

## 5. Pursuit Lifecycle

The intended sequence is: detect an eligible offender → begin tracking → approach and engage → escape or resolution → return to duty.

- Guards use the target's authoritative position and state and move through traversable terrain.
- They must not attack through walls, hit from unreasonable distances, or teleport behind players to compensate for failed navigation.
- They stop targeting dead characters or characters that have left the world, resolving the pursuit without stuck guards or phantom targets.
- Guard coordination is bounded; an unlimited number of guards must not converge on one player.
- Losing a target creates a reasonable search opportunity followed by pursuit termination, rather than permanent knowledge of a hidden player's position.
- After pursuit ends, the guard returns to its assigned position or route and recovers readiness under documented rules.

A continuous pursuit cannot exceed five minutes. Changing the pursuing guard or crossing into another guard's range must not repeatedly reset the same incident's timer and create an endless pursuit.

Escaping ends the current encounter; it does not automatically erase the crime record or persistent Wanted status. Codex should document reacquisition timing and conditions that preserve a real escape opportunity while preventing the timer from becoming permanent immunity during continued aggression.

A new crime may create a new entitlement to pursue under the documented policy. Reprocessing the same event must not create another crime.

## 6. Attacking Guards and the Arena

Attacking a guard is a special offense that transfers the aggressor into an Arena encounter against a strong guard, without a prior warning. The encounter begins once per incident. Repeated hits or multiple guards must not create overlapping encounters for the same player.

The player must understand why they were transferred and the encounter objective. Two exit paths are available: defeat the encounter guard or pay the displayed ransom.

- Codex should balance the guard to be challenging and understandable, rather than using arbitrary numbers or making defeat inevitable for every character.
- Display the ransom before payment confirmation and charge only when the encounter is actually resolved by payment.
- Insufficient funds must not produce a broken state or negative balance.
- Victory or payment resolves the encounter once and returns the player to an appropriate safe location.
- Killing the encounter guard must not become a repeatable source of money or Honor farming.

Codex must define and test player death, disconnection, and server restart during the encounter. Reconnecting must not be a free way to cancel the consequence, and server failure must not permanently trap a character.

Leaving the Arena does not automatically clear Wanted or PK unless an explicit, consistent policy says so. The guard encounter and the crime record must not be conflated in a way that grants a free history reset.

## 7. Wanted Status and Black-Player Bounties

Wanted status must have a traceable reason. Distinguish an active pursuit from persistent status that may authorize a later pursuit.

Killing an eligible black-status offender can grant a reward to an eligible player. Define entitlement and recipient selection when several players or guards participate. Do not award the same bounty repeatedly or treat a client request as reward authority.

Repeatedly killing the same character must not generate unlimited rewards. Use a clear eligibility cycle tied to real crimes or qualifying conditions, with protections against arranged kills and cooperating accounts farming the system.

Codex chooses whether rewards use Honor, Tokens, or both, their amounts, and the conditions for renewed eligibility, according to the server economy. These values are not fixed by this document.

## 8. Commander and Honor

The Commander is the main player-facing interface and should provide:

- An explanation of guards, color rules, escape, and the Arena encounter.
- The player's relevant system status and Honor or Token balance.
- Available rewards and their eligibility requirements.
- Redemption of the selected reward after clear confirmation.
- An understandable reason for rejected redemption or ineligibility.

The previously selected Twin City location is map **1002 at 455,378**. Codex should verify it against the current world. If it conflicts with existing content, use the nearest suitable safe location and document the final coordinates. Do not create a duplicate Commander if one already exists.

Redemption is available to every eligible player. Reward inventory and prices are balancing decisions that must respect the economy and avoid turning protection into inflation or compulsory paid advantage.

## 9. Fairness and Abuse Prevention

These are essential experience requirements:

- Protect beginners against repeated killing and harassment without letting a beginner account provoke others into unfair punishment.
- Do not criminalize legitimate defense or combat permitted by its event or context.
- Prevent dragging guards into disruptive positions or using them to attack innocents.
- Prevent arranged or repeated kill rewards, duplicate redemption, and duplicate ransom charges.
- A shared network or IP address alone is not sufficient evidence of abuse.
- Normal gameplay rules must not have owner-name or GM-only exceptions; administration commands remain separate.
- Consequences should be noticeable and proportionate, without wiping characters, equipment, or progression.

Costs and material consequences must be clear to players. Do not add hidden fines or confiscation beyond the agreed rules.

## 10. Continuity and Player Experience

Important facts must survive reconnects and server restarts: eligible Wanted status, reward balances, completed redemptions, and Arena state requiring recovery.

Partial failure must not charge currency without the corresponding service or duplicate a reward. Removed guards must not remain as ghost entities. Pursuits, targets, damage, and results must not leak between players, maps, or separate Arena encounters.

Messages should be brief and useful: why a warning or pursuit started, Arena entry, successful escape, bounty eligibility, and ransom or redemption confirmation. The system must not flood chat.

Players should understand the system through the game, without needing packet, database, or diagnostic knowledge.

## 11. Decisions Delegated to Codex

Codex has the source context and should choose implementation details, integration, and work order. Reuse suitable existing Guard, Wanted, Honor, and Arena foundations rather than rebuilding them unnecessarily. Historical foundation work is not proof that a feature is connected to the current client path; verify the actual state during implementation.

Codex should also choose and document initial balancing values:

| Balancing decision | Functional constraint |
|---|---|
| Guard counts, positions, and ranges | Effective coverage without congestion or unbounded load |
| PK thresholds and offense classification | Consistent with color precedence and legitimate defense |
| Warning, hiding, and reacquisition timing | Meaningful escape without abuse; maximum five-minute continuous pursuit |
| Guard statistics and abilities | Understandable behavior and challenge |
| Ransom amount and currency | Disclosed, balanced, and not dependent on real-money spending |
| Honor/Token values and Commander rewards | Useful rewards without inflation or arranged farming |
| Wanted decay and bounty renewal | Clear rules that cannot be erased by disconnecting or create perpetual punishment |
| Arena death and disconnection | Fair recovery without free escape or permanent confinement |

Routine implementation decisions and initial balancing values should not stop independent work. Codex should make and explain suitable choices while preserving this document's product rules. A change to the core concept must be identified as a proposed change, not presented as part of the original agreement.

## 12. Acceptance Scenarios

The behavior is complete when these outcomes can be demonstrated for ordinary players:

| Scenario | Expected result |
|---|---|
| A peaceful player passes guards | No targeting; guards display correct equipment and state |
| Blue status without a stronger intervention trigger | Warning consistent with policy |
| Red status or qualifying PK Points | Guard intervention begins on detection |
| A black-status offender is detected away from the crime location | Pursuit eligibility remains available across the system |
| Crime outside a city followed by entry into guard coverage | The record persists and can be applied on detection |
| Escape or hiding meets the rules | Pursuit ends and the guard returns to duty |
| Long pursuit with changing guards | The continuous incident does not exceed five minutes |
| Legitimate defense or authorized competition | No false innocent-kill crime |
| Repeated attacks against a guard | One Arena encounter for the incident |
| Arena victory or ransom payment | One successful exit without duplicate charges |
| Disconnect or restart during the Arena | Fair recovery without free escape or permanent confinement |
| Several participants kill an eligible wanted offender | Reward entitlement follows policy without duplicate payout |
| Cooperating accounts exchange kills | No unlimited reward source |
| Repeated redemption or payment confirmation | No repeated transaction or lost balance |
| Pursuits on different maps or separate Arena encounters | No cross-contamination of targets, damage, or results |
| Two clients observe the same guard | Consistent identity, equipment, movement, and state |

Add edge cases revealed by the current source. Separate server logic and persistence validation from genuine-client visual confirmation. Definitions alone, or a visible guard without functioning behavior, do not complete a player feature.

## 13. Diagnostics and Deliverables

We must be able to trace why each intervention occurred and its outcome: triggering crime or state, pursuit transitions, termination reason, Arena entry/exit, and reward eligibility and settlement. Diagnostics must not freeze the game, disconnect players, or grow logs without bounds.

Required delivery:

1. A system usable by ordinary players that satisfies the scenarios above.
2. A concise record of the rules and balancing values selected by Codex.
3. Actual guard and Commander locations and protection coverage.
4. Separate implemented, connected, deployed, and client-confirmed status, with remaining work identified.
5. Practical ordinary-account test steps and specific blockers, without stopping independent workstreams.

## 14. Optional Extensions After the Core Works

A Commander wanted board, achievements for protecting players, cosmetic Honor ranks, and city-specific guard appearances are possible extensions. They do not replace pursuit, Arena, or reward functionality, and they are not part of the original completed scope until their details are approved.

---

**Success criterion:** peaceful players feel protected, aggressors understand the consequences of their actions, pursued players have a real chance to escape or fight, and legitimate assistance is rewarded without abuse or loss of player progression. Codex determines how to achieve this using the current source.
