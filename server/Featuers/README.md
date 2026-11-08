# Feature implementation plans for the working 6711 server

These plans cover the three owner-authored concepts currently in `Features/`.
Original concept files remain untouched. A plan is not an enabled feature.

| Feature | Implementation note | Delivery order |
|---|---|---|
| City Guard | [Guard implementation](6711_CITY_GUARD_IMPLEMENTATION.md) | Correct existing crime/bounty rules, then server-owned actors, pursuit, Arena and settlement integration |
| Expeditions | [Expedition implementation](6711_EXPEDITIONS_IMPLEMENTATION.md) | Fixed schedule and registration, then durable participation, instance isolation, temporary loot and extraction |
| Living World | [Living World implementation](6711_LIVING_WORLD_IMPLEMENTATION.md) | Bounded, clearly labeled ambient actors first; encounters and companions only after their dependencies |

All features are for ordinary eligible players. The current target for these
plans is genuine6711; none silently switches the working server to7630.
Each note separates existing code, missing integration, proposed balancing,
server tests, deployment and genuine-client acceptance.

## Resource-conscious execution

Use one bounded batch at a time and at most three independent workers. Reuse the
existing workspace and disposable PostgreSQL installation. Run affected tests
first, then one consolidated Release build if source changes warrant it. Avoid
new client copies, broad binary scans and duplicate publish directories. Deploy
only actual connected gameplay changes, with backup and graceful drain; documents
and disconnected foundations do not justify interrupting players.

This tree now contains a first playable version (ordinary players, no GM gate):

- City Guard: player-shaped officers in the cities, pest hunt, PK still first.
  Commander talker NPC 91001 remains in Twin City.
- Expeditions: Clerk NPC 91002, 3-hour cycle, 5-minute registration, 16 seats, 20-minute
  solo instance, temporary loot, extraction after minute 5.
- Living World: labeled `[Adv]` wanderers in six cities, walk/sit/greet, wing + Chi +
  Jiang Hu, pest fights, whisper hire/follow/wait/dismiss (no AI model).

See [docs/FEATURES.md](../docs/FEATURES.md) and [docs/WORKLOG.md](../docs/WORKLOG.md)
for the list of what actually shipped after these plans.

These are server-side features in `Game/Features/`. They are not the modern Host
runtime.
