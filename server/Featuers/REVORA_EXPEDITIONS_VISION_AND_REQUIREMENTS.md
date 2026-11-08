# Revora Expeditions — Risk and Reward Looting

> A concept and player-experience requirements document for Codex. Implement the appropriate outcome for the current source; architecture, protocol, and integration choices are delegated to Codex.
>
> Status: requested design, not a report of an implemented or deployed feature.

## 1. Concept

A competitive event inspired by extraction games: a player enters a dangerous area, gathers loot from monsters, containers, and encounters, then decides whether to leave with the current haul or continue for a greater reward. Loot becomes permanent property only after successful extraction.

The central decision is: **"Keep going or get out?"** A more valuable haul creates greater tension when risking it. Winning does not require eliminating every participant; several players can succeed by extracting at different times.

## 2. Agreed First-Version Rules

| Element | Rule |
|---|---|
| Schedule | Every 3 hours: 8 scheduled events in a normal day |
| Participation | Solo only; no teams |
| Capacity | A limited number of seats per event |
| Registration | Open for exactly 5 minutes before the expedition starts |
| Late entry | Unavailable after registration closes |
| Expedition duration | 20 minutes from the event start, not from each player's arrival |
| Extraction availability | Opens after the first 5 minutes of the expedition |
| Attempts | One entry per character per event; death or extraction does not permit another entry |
| Original equipment | Protected; not lost through death or event failure |
| Loot ownership | Temporary during the expedition, permanent only after successful extraction |
| Audience | All eligible players under published rules, without GM or owner-account requirements |

**Initial balancing proposal:** 16 players in one map per event. This is a proposed test population based on map size and encounter density, not an established final limit. The proposed approach is not to create additional instances automatically to bypass the event's seat cap; capacity should remain genuinely limited.

## 3. Schedule and Registration

Codex should establish a fixed server-time schedule and display the next event time with a clear countdown. The schedule must not become three hours after each server restart or three hours after the previous expedition finishes.

Relative timeline for one event:

- From T−5 minutes until immediately before T: registration is open.
- At T: registration closes, the participant list is finalized, and the expedition starts.
- At T+5 minutes: extraction points become available.
- At T+20 minutes: the expedition ends and unresolved participants receive their final outcome.
- The next event begins at T+3 hours.

Reject registration outside the window or after capacity is reached. Repeated requests must not reserve multiple seats, and concurrent requests for the last seat must not exceed capacity.

Seat allocation was not settled in the discussion. Codex should choose and document a fair, published policy, such as first-come-first-served. Also define pre-start cancellation, registered no-shows, and the minimum participant count needed to start. Do not fill missing seats with fake players or accounts, or convert Solo into a team mode.

## 4. Player Journey

1. Visit the event NPC or interface and read the rules, schedule, and available capacity.
2. Register within the five-minute window and receive a seat confirmation or a clear rejection reason.
3. Enter at the event start at a reasonably safe, fairly distributed spawn location.
4. Gather expedition loot through PvE and optionally fight other participants through PvP.
5. Decide whether to continue or move toward an available exit.
6. Begin a timed extraction while remaining exposed to danger under the published rules.
7. On successful extraction, settle the loot once and return to the ordinary world.

Events must not interfere with other event instances or cycles. Spectators and unregistered players must not reach the expedition or its loot through external travel routes.

## 5. Map, Risk, and Reward

The map should contain areas with different risk and value: reasonable starting regions and richer locations that encourage players to cross paths and compete. Monsters and containers should offer worthwhile opportunities even when a player encounters no human opponent.

A high-value objective or expedition boss may be added when its supporting systems are ready. It should create an optional risk decision rather than becoming the only way to obtain any reward.

Map design should account for:

- Spawn distribution that avoids immediate spawn killing or one location overlooking every spawn.
- Multiple approaches to important areas and exits where practical.
- Density appropriate to the limited population: neither an empty map nor an overcrowded arena with no escape opportunities.
- No major advantage determined solely by receiving a particular spawn.
- Enemies and rewards appropriate to the expedition duration, without turning the mode into safe, uninterrupted farming.

## 6. Solo Combat and Fairness

Each participant competes individually. Team membership outside the event must not grant extra visibility, shared loot or rewards, or targeting exemptions inside it.

PvP is opt-in through registration. Fighting another participant inside the expedition does not generate PK Points or Wanted status, and does not summon City Guards merely for that combat. This exception is confined to the event; it must not erase earlier crimes or grant immunity outside the expedition.

Define assistance and abilities affecting other players in a way consistent with Solo. Arranged alliances and feeding loot between cooperating accounts should not become the best way to win. Two players temporarily choosing not to fight is not, by itself, proof of teaming or cheating.

**Power equalization is an important unresolved balancing choice:** standardized equipment, power limits, or another model suitable for the source. Codex should choose a clear model that makes skill, movement, and decision-making matter and prevents the strongest permanent equipment from guaranteeing rewards. If temporary equipment is used, it must not replace ownership of or permanently alter original equipment.

## 7. Loot and Protection of Original Property

Clearly distinguish property owned before the event from loot earned in this expedition. Temporary loot cannot be moved into an external warehouse, shop, or another character before extraction. Original possessions cannot be converted into expedition loot available for others to steal.

On death, the player loses the expedition loot exposed to risk. A documented policy must specify what portion becomes lootable by other participants and what portion disappears. Lootability percentages and reward values were not fixed in advance.

Loot taken from another participant remains temporary expedition loot for its new carrier. Permanent ownership is granted only through correct settlement. Changing carriers, retrying requests, or disconnecting must not duplicate an item or reward.

Crafting materials, trade goods, cosmetics, and specialized rewards are suitable design options. Currency and combat-power rewards should be balanced against Revora's economy. Review their relationship with RevoraMaster services so rewards retain meaningful value.

Protecting original equipment is a fixed rule. Potion consumption, durability loss, and possible entry fees were not agreed; do not introduce hidden losses. Codex must disclose the participation policy before enabling it. Loss of personal equipment and a Hardcore mode are outside the first version.

## 8. Extraction

Extraction points offer a way out, with a waiting period that creates contestable tension. Codex should determine extraction duration, movement or positioning requirements, and how damage or combat affects the countdown. Explain those rules before players rely on them.

Extraction becomes available at minute five of the event. Players cannot secure loot early through a scroll, teleport, logout, or another travel interface.

Avoid a single compulsory exit that can be monopolized without an alternative. Exit count, changing locations, and activation conditions are balancing decisions rather than fixed mechanisms prescribed here.

When extraction completes:

- End the player's exposure to expedition combat at one clearly defined resolution point.
- Convert eligible loot into permanent property exactly once.
- Handle a full inventory through a reliable mechanism appropriate to the source, without losing or duplicating rewards.
- Return the player to a safe location and display the outcome and loot summary.

If death, extraction completion, or the event deadline coincide, the server applies one deterministic, published resolution order. A player cannot receive both success and failure for the same expedition.

## 9. Death, Timeout, and Disconnection

Death ends participation in the current event. Returning to the ordinary world grants neither a second entry nor restoration of loot lost under the rules. A successful extractor or defeated participant may register for the next event.

The proposal consistent with the extraction concept is that a player who has not completed extraction before the twenty-minute deadline fails to secure temporary loot. Display the timer and final warning clearly and document the resolution rule before release. Do not automatically settle everyone's loot merely because the event ends.

Disconnection is not extraction. Codex should define reconnect timing and behavior that accommodates accidental outages without providing a combat escape button. An allowed reconnect within the same participation is not a new life or a second entry after death.

A server-wide failure differs from one client disconnecting. Provide controlled recovery or cancellation that preserves original property and cannot duplicate loot. Document any server-failure compensation policy; it must not become a free extraction that a participant can force.

## 10. Abuse Prevention

- Prevent exceeding capacity or registering the same character multiple times per event.
- Prevent turning Solo into shared reward distribution or arranged loot feeding to one account.
- Define a multi-account policy appropriate to current capabilities, without treating a shared IP alone as sufficient evidence of abuse.
- Prevent temporary loot smuggling through warehouses, trading, vending, mail, or any existing transfer channel.
- Prevent duplicate extraction rewards and simultaneous settlement to a previous carrier and the player who looted them.
- Derive event eligibility, life state, and loot ownership from server authority rather than client claims.
- Isolate the event from the ordinary world, including movement, damage, PK, Wanted, and reward behavior.

These requirements do not prescribe a particular technical system. Achieve the correct behavior using the implementation appropriate to the source.

## 11. Player Interface

Players need to know:

- The next event's server-time schedule and time remaining before registration.
- Maximum capacity and the status of their reserved seat.
- Solo rules, equipment protection, loss conditions, and extraction rules.
- Remaining expedition time and when exits become available.
- Current expedition loot, clearly distinguished from original possessions.
- Available exits, completion conditions, and why extraction stopped, if applicable.
- The final outcome: extracted, died, timed out, or event cancelled due to a failure.

Codex should choose presentation methods appropriate to the current client. A complex new interface is not required if existing capabilities can explain the experience clearly. Player-facing messages must not expose internal implementation details.

## 12. Decisions Delegated to Codex

Inspect and reuse suitable existing scheduling, maps, combat, loot, persistence, NPC, and notification systems, completing missing prerequisites. This document does not claim those systems are already complete.

Codex chooses implementation and work order and documents initial decisions for proposed capacity, minimum participants, seat allocation, power equalization, death loot, extraction points, disconnection, and compensation. Keep these choices distinct from the agreed rules in Section 2.

Do not change Solo, the three-hour schedule, the registration window, or equipment protection for implementation convenience. If a fundamental product rule needs to change, identify it as a proposed change. Continue independent workstreams without blocking the project on a partial dependency.

## 13. Completion and Acceptance Criteria

| Scenario | Expected result |
|---|---|
| Registration attempted early or after closing | Clear rejection without reserving a seat |
| Concurrent requests for the final seat | Capacity is not exceeded |
| Repeated character registration | One seat and one participation only |
| Five-minute registration window ends | Registration closes and the participant list is finalized |
| A team member registers | Individual participation without team privileges inside the event |
| Late entry or return after death | No second participation is created |
| Extraction before minute five | Not allowed |
| Loot obtained from a monster or player | Remains temporary until successful extraction |
| A carrier dies and another participant loots them | No duplication; original equipment remains protected |
| Successful extraction with repeated result requests | Reward is granted once |
| Full inventory at successful extraction | Entitlements are preserved without loss or duplication |
| Timeout or death coincides with extraction | One outcome according to the resolution order |
| Client disconnects during danger | No automatic extraction or new life |
| Server restarts | Fair recovery or cancellation without duplicate rewards or lost original property |
| PvP within the event | No ordinary-world crime is added and no prior Wanted status is erased |
| Event ends | Clean up its state without leaking content into the next cycle |

## 14. Required Delivery

1. One complete experience: limited registration → Solo entry → looting and risk → death or extraction → correct settlement.
2. A concise record of the schedule, rules, and values selected by Codex.
3. Ordinary-player test instructions covering failure, success, concurrency, and disconnection.
4. Separate implemented, connected, deployed, and client-confirmed status. Server tests are not visual acceptance.
5. Useful tracing of outcome reasons, loot origin, and settlement without disrupting gameplay or bothering players.

**Success criterion:** a limited-capacity Solo event every three hours, giving players a real decision between leaving with a relatively secure gain and continuing under risk. Loss is limited to expedition loot, and permanent rewards require successful extraction.
