# Way of Heroes: Super set and Moon Box promotion use

## Evidence and scope (2026-10-02)

Installed StageGoal section `[4-2]` specifies stage402, TaskType5, six Super-quality
pieces (Require9), opening level101 and one bound MoonBox721052. The existing
approved full-set predicate now accepts this fourth quality tier; all lower tiers
retain their requirements. Core slots plus an equipped non-arrow offhand must
qualify in the selected bank. No new slot interpretation is introduced.

The [official Moon Box guide](https://co.99.com/guide/quests/moonbox.shtml), reviewed
2026-10-02, identifies the box as required for level110 promotion and lists possible
opening outcomes such as equipment, silver and Meteors. It does not provide exact
opening odds or a complete variant-specific pool. Local ClassPromotion explicitly
accepts721052 as a Moon Box material, consumes it atomically and preserves binding
in its promotion reward. Thus the milestone prize has a concrete verified use.

Random right-click opening remains an independent unfinished behavior. This batch
does not invent its odds, claim an opening implementation, or change the installed
client description. The restored claim is specifically validated through the
existing promotion use; native/release gates remain pending.

## Ordinary Super-set earning and promotion chain

`tools/CheckCandidateSuperClaim.cs` adds `career-super-claim-earn/reload`.
One ordinary non-GM Warrior has supplied level110, class24, no rebirth, attributes,
proficiency, 50,000 CPs and six low-level Elite pieces. The Elite set cannot claim
stage402. Six actual CP-shop DragonBalls, purchased at loaded prices, are consumed
through normal quality upgrades to Super. Rolls are controlled successes with
two-second test-clock spacing. With the sixth Super piece still in the bag, the
claim remains refused. Normal equipment completes the set.

Eight concurrent requests then award exactly one bound plus0 single Moon Box721052.
Normal Warrior trainer dialogue consumes that earned box and promotes class24 to25,
recording the trainer achievement. The resulting DragonBall remains bound. Replaying
the confirmation cannot re-promote or issue another reward. A supplied spare normal
pair of boots consumes that earned DragonBall through the normal quality handler,
becoming Refined; replay changes nothing. No qualifying Super item, milestone,
promotion or upgrade receipt is supplied by fixture SQL.

After normal EntitySave and game/PostgreSQL stop/start, exact scoped purse/items,
Chi/Way, upgrade, class/experience and trainer-evidence snapshots match. All six
core pieces remain equipped Super, class25 persists, one stage402 claim and seven
successful quality upgrades remain, and the only bag item is the upgraded spare.
Replaying the milestone changes nothing.

The isolated committed-source-overlay build passed with existing legacy warnings;
the harness compiled with warnings as errors. Earn/reload both passed first run.
Private executable SHA-256:
`F735477E54D3FF5AE70C8308DAFD19370A7D55B9713AFC9202E28A47735FEF88`.
Logs: `arena-process-career-super-claim-{earn,reload}.log`.
Both game closes completed and PostgreSQL stopped. Old executable/manifest
preserved; unrelated owner WIP excluded from the build.

Native/authenticated presentation, natural starting qualification/CP/Elite gear,
other trainer variants, offhand/alternate/rental process variants, random Moon Box
opening and interrupted-commit injection remain unverified. Supplied inputs and
controlled rolls/clock are explicit limits. No live deployment occurred and the
retained main-career checkpoint is unchanged.
