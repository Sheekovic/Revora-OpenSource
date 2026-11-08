# Revora Living World

## Initial concept: Revora Adventurers

Status: product concept for discussion and implementation planning, not a claim of existing functionality.

Revora Living World introduces recognizable, server-controlled adventurers who make cities and hunting areas feel inhabited. They travel, hunt, rest, react to danger and form lightweight relationships with real players. Their value is believable activity and memorable encounters, rather than an inflated population counter.

This document defines the intended experience and initial boundaries. Codex should choose the architecture, integration points, implementation sequence and operating parameters after reviewing the current source. Numerical limits, prices and reward values remain design decisions unless explicitly stated here.

## 1. Product goals

- Make quiet regions feel active without crowding out real players.
- Give the world recurring characters with names, personalities and understandable goals.
- Support optional PvE companionship and practice without replacing human cooperation.
- Connect naturally with Revora City Guard and future world encounters.
- Keep ordinary features available to eligible players, without owner-account or GM-only gameplay restrictions.
- Make every important behavior traceable so mistakes can be reproduced and corrected.

Adventurers must be recognizable as world characters through a consistent label or equivalent clear presentation. They must not impersonate real users or increase the displayed count of human players online. If population figures include them, show the categories separately.

## 2. Character identity and personality

Each persistent adventurer has a stable identity, class, appearance, equipment presentation, home region and personality. Repeated encounters should feel like meeting the same character.

Suggested personalities include a cautious hunter who retreats early, a brave protector, a traveling merchant and an experienced explorer who shares useful information. Personality affects choices, not permission to bypass gameplay rules.

Use varied but coherent behavior. A cautious character should not repeatedly charge into impossible fights, and a wounded explorer should not instantly forget an injury to begin a new activity. Appearance must use verified client-compatible representations and assets.

## 3. Daily activity and routines

An example routine is: leave town, travel to a suitable hunting area, fight a few monsters, return to repair equipment, then rest. Different adventurers can follow different schedules and respond to interruptions.

Cities can contain travelers, resting fighters and visitors moving between existing services. Hunting areas can contain small numbers of adventurers pursuing bounded objectives. Ambient visits to shops or trainers do not require fake player transactions or fake market demand.

Characters should use valid paths, respect collision and occupancy, avoid blocking NPC access or portals, and recover when a route becomes unavailable. Visible arrival and departure should look natural rather than appearing or disappearing beside the player.

## 4. Population that adapts to players

Adventurers fill quiet spaces and yield space when real players arrive. Manage density per region rather than using only a global online count.

Keep regional and global caps configurable. Increase or reduce activity gradually to avoid constant spawning when population fluctuates. Active encounters and hired companions should finish or conclude safely instead of vanishing because a density threshold changed.

Busy areas should receive fewer ambient hunters. Real players retain priority over hunting opportunities, rare monsters, routes and interaction space. Distant activity may use a cheaper simulation, provided visible behavior and persistent outcomes remain consistent.

## 5. Interaction and lightweight memory

Players can greet recognizable adventurers, help them and encounter them again later. A character may remember a rescue, acknowledge earlier assistance or offer a related conversation.

Store bounded gameplay facts such as a completed rescue or relationship tier. Do not build unnecessary records of private conversations. Memory and reward eligibility must remain separate: remembering a player does not create unlimited repeat rewards.

Dialogue should fit the character and current world state. Information about active events or dangerous routes must come from actual server state. Keep ambient speech local, infrequent and easy to ignore. Generative AI is not required for this concept; authored dialogue and deterministic decisions are a valid starting point.

## 6. Small world encounters

Suggested encounters:

- An explorer surrounded by ordinary monsters.
- A caravan needing protection along a reviewed route.
- A wounded traveler asking for assistance.
- A hunting party retreating toward a city gate.

Participation is optional. Define what happens if nobody intervenes, if a participant disconnects, or if the encounter expires. Encounters must not obstruct mandatory travel or punish uninvolved beginners.

Rewards, if added, require explicit budgets, eligibility and repeat limits. Repeated staged rescues, intentional damage and cooperating accounts must not create an unlimited reward loop. The initial ambient version can operate without valuable rewards.

## 7. Optional PvE companions

A later extension allows players to hire or recruit a limited companion for eligible PvE activities. Recruitment method, duration and any fee are undecided.

Simple commands should cover Follow, Wait, Protect, Heal and Retreat. A companion should not pull additional monster groups or initiate avoidable fights without permission. Its strength and capabilities should leave human cooperation more valuable.

Resolve kill credit, assistance, drops and experience explicitly before enabling companion combat. Preserve the player's established loot rights; do not silently create extra rewards or an independent farming character.

Companions must not enable unattended farming. Define behavior when their owner becomes inactive, dies, disconnects or enters an unsupported map. They cannot autonomously trade, transfer resources or continue rewarding progression while the owner is offline.

## 8. City Guard integration

Living World characters can react to danger while City Guard retains responsibility for crime and enforcement. Civilians may flee, traders may suspend activity, and guards may respond according to the actual jurisdiction and crime rules.

Share compatible foundations where useful: entity identity, equipment presentation, navigation, perception and diagnostics. Keep civilian, adventurer and guard roles distinct so a generic bot does not accidentally acquire enforcement powers.

Attacking or killing an adventurer must not automatically count as killing an innocent human player. Define protection, warning, retaliation and penalties per role before enabling those interactions. Where a rule is not defined, prevent the unsupported interaction rather than inventing a criminal consequence.

Existing Wanted status must not disappear through companion recruitment, bot combat or encounter travel. Guard and adventurer rewards must not allow arranged bounty or Honor farming.

## 9. Practice opponents

A later dedicated practice arena can offer clearly labeled bot opponents. Training objectives may include spacing, avoiding a skill, timing healing and managing resources.

Difficulty should primarily change reaction time, decisions and ability use within legitimate limits. Avoid hidden omniscience, impossible movement and guaranteed hits. Explain any deliberate training stat adjustments.

Practice opponents do not enter human competitive rankings or reward-bearing competitive events. Revora Expeditions, the solo extraction event, remains reserved for real player participants; this concept does not authorize bot entrants or companions there.

## 10. Economy and fairness boundaries

- Ambient adventurers must not generate tradable items or CP for themselves, trade with players, or manipulate player-market prices.
- Decorative equipment and simulated wealth are not collectible economic assets.
- Ambient hunting must not steal player-engaged monsters, existing drops or scarce boss opportunities. Avoid resource competition by design.
- Bot death does not produce valuable loot by default.
- Any future reward comes from an explicit server-controlled encounter or companion policy with an auditable budget.
- Bots must respect applicable range, collision, cooldown, targeting and combat rules. Exceptions must be deliberate, documented and visible where relevant.
- Player interaction is optional; ambient speech must not flood local or global chat.

## 11. Server operation and diagnostics

Prefer server-owned world entities without launching a game client or creating a fake authenticated player account for every bot. Codex must verify how the active 6711 client can display and update the chosen entity type, including equipment, movement, effects, removal and reconnect visibility.

Reuse authoritative gameplay services where they fit. Do not duplicate an independent damage or inventory model that gradually diverges from ordinary gameplay. Combat-dependent features should wait for their required equipment and damage calculations; city routines and navigation can progress independently.

Provide an operator view that explains each adventurer's current activity, goal, target, path state and last failure. Record meaningful transitions and rejection reasons with entity, encounter and session correlation where appropriate. Avoid unbounded per-tick logs or private-message logging.

Required controls include per-feature and per-region enablement, population limits, activity budgets, stuck detection, bounded retries and safe retirement. Disabling a feature must settle active player interactions and rewards without loss or duplication. Restart recovery must preserve important identities and outcomes without restoring duplicate entities or paying a reward twice.

The exact scheduler, persistence schema, diagnostic interface, deployment mechanism and performance budgets are implementation decisions for Codex.

## 12. Suggested delivery phases

### Phase 1: believable ambient life

Start with one city and one hunting region selected from currently working maps. Introduce a small configurable population with stable identities, clear bot labeling, coherent travel/rest routines, collision-safe movement and operator diagnostics. Reward-bearing combat is not required for this phase.

### Phase 2: encounters and recognition

Add greetings, bounded relationship memory, rescue or escort encounters, population adaptation and reactions to City Guard activity. Enable rewards only after their eligibility and anti-farming rules are complete.

### Phase 3: controlled PvE assistance

Add limited companion recruitment, explicit commands, validated combat and healing, and fully defined loot/experience behavior. Verify inactivity, death, disconnect and map-transfer behavior before wider availability.

### Phase 4: practice and wider coverage

Add practice opponents, more personalities, reviewed regions and richer event participation. Expand based on player experience and measured server load rather than a target bot count alone.

These phases are a suggested order, not a request to stop unrelated server development. Independent work may progress alongside feature integration.

## 13. Initial acceptance criteria

The first release should demonstrate that:

- A normal player can encounter clearly identified adventurers without privileged access.
- The same adventurer can complete a coherent routine and be recognized later.
- Navigation does not block service access, violate collision or leave recurring stuck actors.
- Regional density responds gradually to human activity without disrupting active interactions.
- Ambient activity cannot mint player wealth, steal protected loot or inflate human population figures.
- The operator can explain why an adventurer selected an activity or failed to progress.
- Feature disablement and server restart do not duplicate actors or lose committed player outcomes.
- Actual client observation verifies the relevant appearance, movement and removal behavior; server tests are reported separately.
- Performance remains within explicitly measured budgets at the intended population.

For later combat and reward phases, also verify authoritative damage, companion eligibility, resource accounting, concurrency and replay behavior through the real integrated paths.

## 14. Decisions left to Codex

Inspect the current source and document the chosen entity representation, first maps, initial population, scheduling approach, memory scope and diagnostic tools. Propose companion costs, reward budgets, inactivity rules and practice difficulty only when those phases become relevant.

Report progress separately as designed, implemented, integrated, deployed and observed in the genuine client. Do not count a moving demonstration as completion of companions, encounters or economy integration.

The defining experience is simple: a player should remember an adventurer they met, recognize what that character is doing, and enjoy crossing paths again.
