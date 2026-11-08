# Ancient Devil battle and reward persistence

Current source status (reviewed 2026-10-01): the historical staging and
"still required" notes below describe this page's original batch. They are
superseded for NPC admission, summon/claim dispatch, startup/tick integration
and collection achievement 11020 by [lifecycle evidence](ANCIENT_DEVIL_LIFECYCLE.md)
and [collection evidence](ANCIENT_DEVIL_COLLECTION.md). Do not reimplement those
connected components. Composed-process collection and saved-victory reward
recovery now pass in the linked lifecycle record. Mid-fight interruption,
genuine-client acceptance and deployment remain unclosed gates.

Staged source, 2026-09-20. This internal transaction participant is not a playable
boss encounter. No production initializer, boss spawn, switch dispatch or
achievement 11020 producer is enabled by this batch.

An owned, unexpired paid summon binds five unique actor identities to a private
map and a fresh generation. Four guard deaths must commit before the boss death
can commit. Rebinding preserves defeated guards and the original deadline while
invalidating callbacks from the old generation. These methods require the
canonical player's item lock and saved-state validation. The future world
coordinator must additionally authenticate the actual monster objects and deaths;
identifiers supplied by client packets must never call this store directly.

Boss victory atomically records the final death, completion state and reward
recipe. Explicit Revora economy policy is 20% for one Dragon Ball and 80% for six
Meteors. This is not a claim about official odds, and is separate from Magnolia's
owner-selected 20% bonus chance. Production draws use rejection-sampled random
bytes. A committed victory never redraws on duplicate callbacks or claim retries.

The summoner claims the saved, unbound reward beside the island seal. Claims may
survive the encounter deadline and reconnect. Saved and runtime inventory counts
must agree, and the bag needs one or six slots. Item inserts, exact reward UIDs,
claim timestamp and summon state commit together. The caller installs items only
after commit while retaining the item lock. Concurrent confirmations cannot
deliver the reward twice; incomplete writes roll back all reward items.

## Validation

The isolated server build succeeded. Run:

```powershell
& tests/PotencyHunt/Verify-PotencyHunt.ps1 -AncientBattleOnly
```

306 combined checks passed. All three paid encounters use actual class-trial amulets
earned through the staged NPC/combat route and transferred by ordinary player
trades. Battle death callbacks themselves are synthetic internal inputs: this
fixture proves persistence, not actual boss combat or client presentation.

Coverage includes invalid identities and slots, guard order, generation rotation,
duplicate deaths, invalid reward draws, suppressed final writes, concurrent
claims, production reload, both reward outcomes and insufficient inventory space.
Suppressing the sixth Meteor insert rolls back all five earlier inserts. Fixture
inventory filler is created only to exercise capacity and rollback. Controlled
time advances between quest days; no real-time duration test is claimed.

## Restart recovery

`RecoverStartup` is a staged startup-only entry point, to run after table
initialization and before player admission or publication of dynamic scenes.
It rejects an affected owner still present in the session registry. In one
transaction it locks active paid summons and their characters, restores saved
private encounter locations to the public island, and rotates actor generations.
Offering receipts, defeated guards, original deadlines and victory recipes are
retained. Pending attempts expire at their original deadline; victories do not.
The future coordinator must bind new actual actors before resuming combat.

The fixture tests admitted-owner refusal, failed return-write rollback, pending
payment-free recovery, rejection of pre-restart callbacks, preserved guard masks,
victory recovery after deadline and exact-deadline expiry. Repeated expiry
recovery is harmless. All three offerings are earned; this remains a controlled
store/reload test rather than a launched Host crash or actual boss-world test.

Remaining work includes switch selection/review, canonical boss and guard world
ownership, combat balance, technical recovery/expiry, startup activation, earned
achievement evidence and genuine-6711 acceptance. Nothing was deployed.

## Valuable victory reward (2026-09-20)

New summons freeze `valuable_reward=true`. After the four guards and Ancient
Devil are defeated, the owned victory claim adds one uniformly selected unbound
catalog P7 soul to the existing Dragon Ball/six-Meteor recipe. Saved older
summons default false, including recovery and resumed attempts. The original
recipe stays in `claimed_items`; `soul_uid` records the extra item in the same
transaction as all inserts, claim time and terminal summon state. Capacity
includes both the original recipe and the additional soul. A failed receipt
leaves neither a soul nor a partial original reward.

Isolated build and 327 battle-store checks pass. They include concurrent claims,
old-contract Meteor delivery, rollback without a saved soul and production
reload with exact P7 receipt identity. Full encounter validation is tracked in
`obj/chi-refill-validation/ancient-p7-world.log`; store log is
`obj/chi-refill-validation/ancient-p7-battle.log` (both ignored).
Live Host and native-client acceptance remain pending.

Full boss-world integration also passes 278 checks, including real owned actor
combat, resumed guard progress, victory cleanup and reward reload/replay.
