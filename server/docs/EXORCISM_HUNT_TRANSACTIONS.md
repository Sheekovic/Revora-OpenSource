# Owned Exorcism daily hunt

2026-09-21 reward update: new daily contracts include one bound level-1 Tortoise
Breaker rune alongside the existing difficulty-based gifts. Existing/imported
contracts retain their original rewards. Build and 633 Exorcism checks including
shared prerequisites pass, covering all three earned tiers, exact bound-rune
receipt/reload, suppressed rune-insert rollback, paid claims and full bags.
See [current policy](DAILY_REWARD_POLICY.md). Log:
`obj/chi-refill-validation/exorcism-rune-tests.log` (ignored).
Native acceptance and deployment remain pending.

Implemented against `cf20e43` on 2026-09-16. This is an ordinary-player server
route, with isolated automated validation; it has not been deployed or accepted
in the genuine 6711 client.

## Playable route

Heavenly Master (12333, Market 243,151) accepts a level-90 daily commission.
Choose one of three Spooks, gather up to five companions beside the NPC, then
enter. Earth Master inside the private instance summons its registered Spook
and provides a return to Market. Nearby admitted teammates with matching daily
commissions earn a victory together. Heavenly Master pays each player once.
Already-completed players can help but receive no second daily entitlement or
Spook spirit bonus. Joining a team alone no longer allocates a quest map.

The fixed roster, admissions, original one-hour deadline and victory are durable.
The summoner enters first. Rejoining after a disconnect reconstructs the same
hunt within its original deadline; an active boss returns at full HP. Boss damage
is not persisted. Explicit departure closes the summoner's hunt or removes that
teammate. Team changes and timeout eject participants. At most 16 instances are
resident. Cleanup is scoped to its own run: a completed instance cannot close a
newer hunt that its former member joined.

Rewards preserve Revora's configured daily economy. Defaults are 2,500 CPs,
15 RVPs, one EXP Ball of experience, one Chi Token and 1/2/3 Exorcism Tokens.
Study Points are 25/50/100. These items retain the legacy unbound setting.
A separately reviewed, two-minute, single-use offer finishes the strongest tier
for the configured 5,000 CP fee. Accepted terms are frozen. Earned victory can be
claimed after midnight, consuming the claim day's allowance. Completed-day and
clock-rollback guards prevent repeat rewards. Existing same-day legacy completion
is imported conservatively; the old price flag is not proof of an unpaid victory.

The newly credited killer also earns 600/800/1,000 spirits for a valid issued Soul
Ball, capped at that bead's requirement. A completed helper cannot farm this bonus.
Anniversary beads remain excluded. Three earned tokens can be exchanged and the
resulting pack opened through the atomic services implemented in `cf20e43`.

## Ownership and transactions

`revora_exorcism_daily`, `revora_exorcism_hunts` and
`revora_exorcism_members` hold commissions, runs and admissions. Unique active
owner/member constraints and explicit run identity protect replays and recovery.
Only the registered live boss, its owned map and canonical admitted player or
registered companion can finish a hunt. Player combat against teammates or
outsiders is denied inside the instance. Ordinary boss melee remains enabled.

Victory, nearby daily entitlements, admission closure and the killer's Soul Ball
progress/source receipt commit together. A failed required write leaves the boss
alive and all entitlements unpaid. Claims atomically persist currency, full
experience/progression effects, Study Points, exact owned gifts and their receipt.
Inventory and wallet preimages, ordinary eligibility, mentor/recovery effects,
capacity, overflow and replay guards precede cache and client updates. Persistence
failures fence the session for production reload and retry.

Private map allocation reserves loaded and unloaded public IDs. Construction
failure releases partially registered maps. NPCs are actual instance-owned clones;
public NPC 8798 is not borrowed as Earth Master. UTC deadlines round-trip through
PostgreSQL without relabeling local time. No public map, client file, production
data or live server process was changed by this validation.

## Evidence and limits

The [official Exorcism guide](https://co.99.com/guide/quest/2012/Exorcism.shtml)
supports level 90, daily grades and the strongest tier's three tokens/100 Study
Points. Its 27-CP direct finish differs from Revora's retained configured economy.
The [older Spook guide](https://co.99.com/guide/monsters/thrilling_spook.shtml)
describes a one-hour team hunt and three named attacks. The
[Spirit Bead guide](https://co.99.com/guide/quest/spirit-bead.shtml) supplies the
600/800/1,000 bonuses. These gameplay references do not verify a packet layout.

The private hunt uses installed Tiger Cave terrain 1013, not a verified original
Black Burrow. Its cached floor is hash-gated to
`E689BAA83C58952C16352A10E4C5DF2D2E8BC86FFC3BE728C4F74574183B4868`.
The 104-by-104 instance clears inherited portals; entry, NPC and boss cells are
validated. Monster presentation derives from installed family 4212, mesh 952.
Custom tiers have 10/32/55 million HP and configured 20,000-30,000 raw attack.
Later inspection found that the legacy name-based attack branch still bypassed
normal damage calculation in this baseline. The [named combat repair](EXORCISM_COMBAT.md)
replaces it and verifies defense handling; the original damage claim here was
too strong. Named skills were not implemented by this initial batch. The existing Alchemist Dan dialogue is present; its shop purchase
route was not separately validated here.

NPC 2032 framing reuses existing bounded continuations. Unverified packet 2400
remains disabled for everyone. Synthetic spawn/damage checks prove full-width
boss-health values at full, half, one and zero HP. They do not prove native HUD
rendering. Original terrain/combat parity, genuine-client travel/dialogue/HUD and
restart acceptance remain open; deployment is a separate step.

## Validation

The isolated x86 build passed. Final focused Exorcism hunt suite: **557 checks**,
including shared progression prerequisites. Ordinary non-GM fixtures exercise all
three earned routes, paid cancellation/expiry/concurrency, team/helper admission,
reconnect and expiry, midnight/rollback, counterfeit actors, capacity, partial map
construction, cross-instance cleanup, suppressed database writes, production
reload and retry. An earned strongest-tier hunt completes both its daily bundle
and a Super Soul Ball; another route exchanges earned tokens and opens its pack.

Related suites passed before the final narrowly scoped departure correction:
752 Exorcism reward checks, 559 Soul Ball checks, 262 Grotto/Dragon Slayer/Terato
checks, 511 Price Hunt checks and nine quest-packet checks. Together with the
final focused suite: **2,650 gameplay checks** (shared prerequisites repeat).
Three producer-audit tests pass; inventory remains 219 present and 118 absent.
No additional achievement producer is claimed.

Final focused harness SHA-256:
`2A4415F46CB2C406C71DD3A89AA0FEA57B20D4BCA989B8CE8E951DEFDDAD9CB5`.
Ignored evidence logs: `obj/chi-refill-validation/exorcism-quest-final.log`
and `obj/chi-refill-validation/spook-regression-*.log`.

## Valuable daily completion reward (2026-09-21)

New daily contracts freeze `valuable_reward=true` and add one unbound catalog
P7 soul at completion, for all three difficulties and the existing paid finish.
Old saved/imported contracts default false and retain their gifts. The added
soul UID joins `reward_uids` in the same completion transaction as CPs, RVPs,
Study Points, EXP, Chi Token and grade-dependent Exorcism Tokens. Capacity
includes the extra soul; NPC text advertises it. Existing token-to-pack exchanges
and randomized pack-opening contents remain unchanged.

Isolated build and 619 Exorcism checks pass. All three actual earned hunt routes
verify exactly one unbound P7 soul, durable receipt membership and reload/replay.
The suite also exercises claim rollback, full bags, paid completion, team/hunt
ownership and the daily allowance. An ordinary-character fixture applies the
migration-default false marker to an accepted contract, earns victory through
combat, and verifies original gifts, reconnect persistence and replay rejection.
This simulates the old contract marker; it does not run an older server binary.
Log: `obj/chi-refill-validation/exorcism-p7.log` (ignored).
Live Host and genuine-client acceptance remain pending.

## Retained ordinary hunt and cold reward recovery (2026-10-01)

Current daily contracts award a bound Tortoise Breaker rune instead of a P7
soul. The September 21 P7 section above records superseded reward behavior;
this batch verifies the current daily-P7 policy without reopening old claims.

The retained level-140 Archer uses ordinary trade and warehouse packets to
prepare capacity: transfer owned pack 60197956 and three unbound stored weapons
59990052/59960395/59970176 to the existing peer, then deposit three bound earned
EXP Balls into the vacated warehouse slots. Bag count becomes 36, warehouse 8
remains at 60, and both owners' belongings survive a full restart. Nothing is
deleted, rebound or granted to make room; capped EXP Balls are preserved.

The installed Market portal and Heavenly Master dialogue admit the character
to the first-grade private hunt. Earth Master summons the full 10,000,000-HP
Spook. Twenty-one normal ranged attack-handler calls defeat it and earn 10529;
no supplied damage or direct death call is used. The hunt and daily contract
record victory. Earth Master returns the player to Market, where character save
and abrupt process exit leave the victory unpaid.

After game/database restart, the exact saved snapshot matches. Heavenly Master's
ordinary claim delivers 2,500 CPs, 15 RVPs, 25 Study Points, Chi Token 729304,
Exorcism Token 711211 and one bound rune 4030101. No P7 is present and no skip
fee is charged. The three reward UIDs belong to the saved receipt and remain
owned; all earlier bag UIDs survive. Level-cap experience remains unchanged.
Repeated claim/accept/claim requests grant nothing. A further full restart
preserves the nested career state, purses, items, victory and achievement.

Five phases pass: `career-exorcism-prepare`, `career-exorcism-prepared`,
`career-spook-hunt`, `career-spook-claim`, `career-spook-reload`.
Latest checkpoint: `revora_career_spook_probe` done=true / `CareerSpookSnapshot`.
Candidate SHA-256:
`B558BF74A339C92158604D48BD6ED9AB8F079055CF44545DCE13137CF8F9DF70`.

Approach coordinates and attack cooldown are controlled; existing maxed combat
stats remain unchanged. This is solo first-grade server-handler evidence, not
representative combat balance, authenticated attack decoding, native rendering,
mid-fight crash recovery, natural timing or the higher-grade/team journeys.
No production code, live restart, deployment or client release changed.

## Fresh production-created Warrior earning (2026-10-03)

The fresh career accepts grade1 through Heavenly Master, summons through Earth
Master and defeats the10M-HP Spook with263 calculated melee attacks using its
existing earned equipment. Unclaimed victory/10529 and exact non-P7 daily rewards
survive separate processes; same-session and cold replay preserve all prior
possessions and receipts. No clock or lethal-damage override is used. Controlled
proximity/cooldown and timer-free retaliation limits remain; this does not extend
native, balance or live-release acceptance.
[Evidence and current checkpoint](CAREER_STARTING_BOUNDARY.md#fresh-first-grade-exorcism-and-unpaid-victory-recovery-2026-10-03).
