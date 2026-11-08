# Daily Envoy reward coverage audit

Owner clarification (2026-09-21): **remove direct daily quest P7 rewards only**.
One-time quests, event rewards and both Magnolia custom packs keep their bound
P7 souls. Daily Hunt, Rare Materials, Soul Ball, Release Souls, Magnolia's base
harvest, Exorcism and Worship retain their other completion rewards. Custom
Magnolia packs give one bound P7 soul, 5,000 CPs and 1,000 RVPs. Existing items
and completed receipts are preserved. See [current policy](DAILY_REWARD_POLICY.md).


2026-09-21. The Daily Envoy at 79882 is a hub, not another independent reward
family. Its 90004 journal state is projected by `DailyPriceQuest` from the owned
Everything Has a Price / Daily Hunt run. Do not add a second prize to that
journal completion or count the hub as an additional completed quest.

The visible menu routes Daily Hunt, Rare Materials and Soul Ball through their
owned handlers, and sends players to Release the Souls, Magnolia and Exorcism.
Those families already have valuable-reward coverage. This source mapping is not
a new claim of native acceptance for the whole hub.

Worship the Ancestors has a dedicated daily receipt and atomic reward
transaction for its CP/RVP, Chi Token and EXP. The owner's later daily-P7 policy
removed the direct soul reward. All six statue routes, failed writes, daily
rollover and fresh reload have coverage; current policy validation reports 515
combined checks. Current-day legacy paid state imports without an extra prize.
See [evidence](ANCESTOR_WORSHIP_TRANSACTIONS.md) and the policy above.

The Envoy now refreshes saved worship eligibility and directs players to the
owning Release Souls, Magnolia and Exorcism NPCs without the obsolete generic
reward bundle. Mission 90004 remains owned by Daily Hunt alone.

## Retired legacy paid reset

The reset is removed from the menu and option 18 rejects stale/replayed requests
without charging or mutating quest progress. The old `DailyEnvoyShowReset` and
`DailyEnvoyResetCp` fields remain compatibility settings with no effect on this
retired service. Treasure of Paradise travel remains disabled by
`DailyParadiseMap=0`.

The old reset only cleared legacy in-memory flags, then immediately refreshed
restored quests from their authoritative daily ledgers. Making its charge atomic
would still sell an ineffective reset. This batch retires that unsupported offer;
it does not implement paid daily replay, remove completed receipts or grant extra
attempts. A future paid replay design would need explicit eligibility, run
identity, reward-limit and economy rules across the owning quest systems.

Tests exercise the actual NPC request with both legacy toggle values and an
affordable fee, repeated packets, active and completed tasks, autosave and
production reload. No live configuration or deployment is changed.

Validation: isolated build and 559 combined Daily Hunt checks pass. Logs:
`obj/chi-refill-validation/envoy-reset-build.log` and
`obj/chi-refill-validation/envoy-reset-tests.log` (ignored). Native dialogue
acceptance remains part of the wider release gate.
