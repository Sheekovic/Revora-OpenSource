# Path Hunt reward and earning

2026-09-21. The server implementation and isolated earning/persistence checks are
complete. Native 6711 interaction, navigation and live release remain pending.
This restores the existing Revora quest; it does not assert retail parity.

Rowan remains at Twin City 1002 (319,299). Each configured rebirth stage permits
one completed hunt: by default 20 Pheasants, 20 Robins, 20 Turtledoves and 20
Apparitions. His four existing travel options lead to the hunting areas. The
fixture confirms installed spawn rows for each family, then drives registered
monsters through `Entity.Die`, with mid-hunt reconnects after ten of each family.

New acceptance saves `path-hunt-valuable=1` alongside the existing life/count
variables. Completion guarantees one **bound random P7 soul** from the loaded
P7 catalog. Older accepted hunts, which have no reward marker, retain their
original reward; the marker survives reconnect. Old paid completion bits are not
reopened. The existing rewards remain 25,000 silver, 100 RVPs and three unbound
EXP Potions, plus the configured level puff (80/95/105). Players already at the
puff receive eight more potions. The original stat/attribute behavior is retained.

## Persistence and ownership

NPC operations require the nearby registered Rowan, the canonical living player
and no trade/booth. The inventory owner lock serializes claims and autosaves.
The variable lock covers the complete progress image through commit. A single
transaction writes all reward items, purse, progression, completed variables and
`revora_path_hunt_claims`, keyed by character and capped rebirth stage. Receipts
record the reward version, nullable soul UID, full item UIDs and paid values.

Every expected row write is checked. Silver, RVP and progression use their durable
preimages. Capacity includes the P7 and the correct potion alternative; overflow
is refused before payment. Failed/ambiguous writes require reconnect and do not
leave partial rewards. A receipt repairs stale completion variables at Rowan.

Only nearby registered wilderness monsters with matching installed metadata count.
Each monster life credits once; normal respawn clears its weak life marker.
Each credited kill saves progress. Acceptance and rebirth reset save the reward
marker and counters together. Fake families, removed bodies, guards, companions,
wrong maps/owners and remote monsters cannot earn credit.

## Validation

The isolated server build and `Verify-PotencyHunt.ps1 -PathHuntOnly` pass **552
checks**, including the existing 403-check first-rebirth prerequisites. Six actual
80-death routes cover new and legacy accepted contracts at all three configured
rebirth stages. Tests cover exact item UID/binding, purse/profile/progress reload,
concurrent claims, a separate autosave race, six suppressed-write rollback cases,
retry, stale preimages, capacity, overflow, receipt repair, old paid runs, remote
NPCs, invalid kills, corpse replay, revival and persisted rebirth reset.

The earned-route reconnect assertions have no concurrent or subsequent autosave
after reward claim. The autosave race deliberately uses a separate prepared
completion fixture so it cannot mask a missing transactional reward write. The
same separation was applied to Courier's fixture; its revised suite passes
**531 checks**. These are configured rebirth profiles, not a new proof of crossing
both rebirth rites on a single character. Failure/race fixtures prepare completed
counters separately from the actual earned routes.

The composed private journal v2 adds Path Hunt's new bound-soul reward and
old-contract notice, preserving the earlier starter/Monk/Warrior edits. Seventeen
journal tests and hash-gated preparation pass; see [review](STARTER_JOURNAL_REVIEW.md).
The active client and live server are unchanged. Logs remain ignored under
`obj/chi-refill-validation/path-hunt-atomic.log` and `courier-reload-separation.log`.


## Actual ordinary earning and restart (2026-09-27)

RoadmapProbe completed all80 kills through Game TCP, claimed through Rowan and
received100 RVPs,25000 silver,11 unbound EXP Potions and a bound permanent P7
LionHeavyRing823061. The exact life0 valuable receipt and all12 reward items
survived two further game/database restarts; same-session and later duplicate
claims were refused. The same character retains its earned Courier/Soul Trial
receipts and rewards. [Detailed scope](ROADMAP_STARTUP_ACCEPTANCE.md#path-hunt-full-ordinary-earning-and-reward-recovery-2026-09-27).
Later lives, abrupt commit faults, native acceptance and deployment remain open.
