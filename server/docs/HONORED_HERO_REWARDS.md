# Honored Hero ? Revora career award

Owner-approved Revora rule (2026-09-22), not a claim about retail acquisition:
complete Soul Trial, Courier Loop, Path Hunt, first rebirth and second rebirth.
Each distinct route needs its durable final receipt. Repeated lives or stages,
legacy quest flags, purchases alone and current rebirth count do not substitute
for missing receipts. Existing recorded completions count.

The retained 6711 catalog maps Honored Hero to `20572057` (type/id 2057/2057),
worth 30 title points. `ProgressionEvidence.Read` now reconciles the five receipt
tables into one permanent `honored-hero-revora-v1` / `career-v1` award. The existing
ordinary achievement/wardrobe refresh publishes ownership, points and equip
notifications after persistence commits, including on login. No GM privilege,
allowlist, payment or new NPC is required. Normal online reconciliation uses the
existing achievement cache/wardrobe tick; this is not a synchronous quest-popup.

All five receipts must belong to the character and not be future dated. An
insert failure rolls back the evidence read. Concurrent refreshes preserve one
award; database reload restores ownership and equipped selection. Explicit
revocation remains effective, and recovery cannot clear it. The award has no
expiry. Once granted, it is an earned permanent award, not a continuously
recomputed rebirth-state bonus.

## Validation and remaining gates

Build passes. The title suite passes 105 checks, including 14 new career cases:
catalog identity, no receipt, each incomplete stage, repeated route, future
receipt, suppressed grant, full receipt set, concurrency, ordinary equip,
database reload and revocation. The shared progression suite passes 122 checks.
Logs are `obj/chi-refill-validation/honored-hero-build.log`,
`honored-hero-titles.log` and `honored-hero-progression.log`.

The original title fixture seeds receipts and validates the award consumer.
A subsequent combined fixture in `PathHuntTests.VerifyEarnedCareer` now earns
all five final receipts on the same ordinary character: registered monster
deaths and NPC claims for Path Hunt, Soul Trial and Courier, followed by both
NPC rebirth transactions. No final receipts are inserted by this fixture.
It verifies no early title after three/four routes, one permanent award after
the fifth, and ownership/equipped selection after full character/item reload.
The follow-up also obtains Clean Water from an owned Waterlord death and floor
pickup, crafts the Heavenly Stone through Sereth, and purchases an Exemption
Token through the native shop handler using earned Courier CPs. Purchase and
item state are reloaded before the second rite. The affected Path Hunt batch
passes 602 checks; evidence is
`obj/chi-refill-validation/earned-career-materials.log`.

The follow-up mines all seven unbound normal Heavenly Stone gems on that same
character: earned quest silver buys and equips the installed Pickaxe, the
Conductress fare enters Plain Mine, and Forest/Canyon assistants admit the
later pools. Controlled ticks select each installed pool entry. Rebirth
level/class remain fixture prerequisites. The Waterlord drop roll is still
fixed, and kills remain registered deaths rather than native combat.

It does not prove complete prerequisite acquisition or an actual Host restart.
Those acceptance gaps keep the reward matrix at `predicate-only`. Native
acceptance stays pending by owner choice. No private runtime refresh,
production deployment or client patch occurred.


## Combat route and shared-variable recovery (2026-09-26)

Path Hunt, Soul Trial and Courier earning fixtures now pass damage through
`Handle.ReceiveAttack` for their registered hunting monsters, rather than calling
`Entity.Die` directly. The same career character also kills Waterlord through
that combat path, picks up Clean Water, mines/crafts the Heavenly Stone, buys
its Exemption Token with earned CPs and completes both rites. Explicit duplicate
kill callbacks remain to check that corpse replay adds no progress. Monster HP,
killing damage and drop selection are controlled; this is not a native attack
packet, balance or real-time grind claim.

The combined run exposed a production regression: reward transactions wrote
quest variables directly, then cleared `Changed` without updating the vault's
acknowledged save baseline. A later ordinary variable save could fail with a
conflict. All three reward producers now lock/read the stored row, merge unrelated
local/stored changes, reject a newer quest admission or conflicting edit, persist
only the resulting image and install that exact image after transaction commit.
Currency, items and receipt changes roll back with a rejected merge. The vault
object/lock identity is preserved and its save baseline advances only on success.

**614 combined Path Hunt/career checks pass**, including independent local and
durable variables preserved through a reward and later save, changed admission
rejection, conflicting unrelated edits and reload of the surviving stored value.
The full same-character five-receipt route, ordinary title equip, full reload and
single permanent award pass. Build succeeds with existing legacy warnings;
pre-existing owner work was present. Evidence: `obj/career-vault-build.log` and
`obj/career-combat-tests.log`.

Rebirth level/class are still supplied prerequisites; complete prerequisite
acquisition, actual Host restart, native acceptance and deployment remain open.
Honored Hero remains predicate-only in the matrix. This supersedes the earlier
registered-death fixture limitation only, not the remaining acceptance gates.

Separate affected-producer regression suites also pass: **513 Soul Trial** and
**534 Courier Loop** checks, covering their ordinary claims, failure/replay and
saved-state cases through the updated merge/install path. These suites overlap
with the shared prerequisites and must not be added into a unique-check total.
Logs: `obj/career-soul-tests.log`, `obj/career-courier-tests.log`.


## Earned rebirth levels (2026-09-26)

The same-character career fixture now earns level 120 from Path Hunt, Soul Trial
and Courier in each of its first two lives. It no longer supplies level 130 before
either rite. It verifies the first rite's persisted level-15 reset, repeats the
three per-life routes through combat/NPC claims, reloads the earned level 120,
returns to Sereth, and completes the second rite. Final reload retains two receipts
for each hunt, both rite receipts, the equipped title and exactly one permanent
award. **649 combined checks pass** (`obj/career-level-tests.log`); totals overlap
prior suites. The existing production build was reused; only tests/docs changed.

The reconnect helper now performs login's gem/stat/HP recalculation. Omitting it
left fixture maximum HP at zero and caused a natural level-up to kill the test
character; no production HP workaround was needed. Monster damage remains
controlled. Initial level 50 and class ranks are still supplied: this is not a
new-character-to-title proof. Class promotion/material acquisition, actual Host
restart, native acceptance and deployment remain open. Honored Hero remains
predicate-only in the reward matrix.


## Integrated promotion career (2026-09-26)

**693 combined Path Hunt checks pass** (`obj/career-promotion-tests.log`). The
career now starts with a level-one, rank-zero ordinary Trojan fixture and earns
level 120 through the three quests. It visits the installed Job Center trainer
for every promotion; hunts/picks up its Emerald from HillMonster; completes the
six-tactic Moon quest for Meteors and again for a Moon Box; consumes those earned
materials; and reloads the master rank. After the first rite resets it to level
15/rank one, it repeats the quest leveling and promotion/material routes before
buying its Exemption Token with earned CPs and completing the second rite.

No master-class or pre-rite level override remains. The fixture also mines the
seven Heavenly Stone gems, obtains Waterlord Clean Water and crafts the stone.
Mining checks identify newly acquired item UIDs, so a normal gem awarded by
promotion cannot masquerade as mining output. Ordinary warehouse deposit packets
store earned souls/potions before the six-token hunt instead of bypassing bag
capacity. Both lives retain their quest receipts; the five lifetime promotion
receipts remain unique. The final full character/item reload retains both rites,
the permanent equipped title and exactly one award.

The existing production build was reused; this batch changes tests/docs only.
It uses synthetic ordinary sessions, controlled monster HP/damage/drop rolls,
scripted travel and production handlers. It does not exercise account/character
creation, native attack packets, a real-time grind or an actual Host restart.
Server-side prerequisite earning is now integrated; actual Host/native acceptance
and deployment remain open, and the conservative matrix classification remains
predicate-only. This supersedes earlier supplied-level/class/material limitations,
not the remaining runtime/client gates.


## Fresh-process earned-career recovery (2026-09-27)

The existing ordinary earning career now launches a separate test executable only
after the same character has earned both lives, all five prerequisites and its
permanent equipped title. Credentials travel through redirected stdin, not command
arguments or persisted output. The child accepts only the disposable fixture schema,
starts with an empty session pool, reloads character state/items/skills/variables
through the production loaders and runs production title login/reconciliation.
Catalog definitions come from the installed catalogs; only the read-only refinery
catalog load temporarily uses the public search path. Progression remains in the
fixture schema.

Two consecutive final runs pass **695 parent checks plus 5 child checks**. The
child proves second rebirth, all five earned prerequisite receipt types, permanent
equipped Honored Hero, exact saved item UID/type/binding/stack fingerprint, silver,
CPs and duplicate-free repeated reconciliation. The parent verifies the committed
item fingerprint and single permanent award after child exit. It relinquishes and
reacquires its isolated activity lease around the child. These totals include the
prior 693 checks; they are not separate full-game coverage.

**Historical intermittent assertion (clock correction below):** two earlier parent runs completed the second rite
but failed the immediate title assertion; a captured failure had no persistence
fence, rebirth2 and one second-rite receipt, but no projected Honored Hero. The
cause is not established. The final two passes do not close that defect. Failure
context now includes the latest receipt/client-clock difference and award count
for follow-up. Keep immediate earning reliability open; do not infer that a
fresh-process recovery pass fixes it.

Logs: `obj/career-process-tests.log`, `obj/career-process-repeat.log`. The fixture
retains its existing controlled travel, combat, mining/drop inputs. This is an
actual fresh **component-test process**, not an authenticated game session, a Host
crash or a database restart. Native/Host acceptance, the intermittent immediate
projection issue and deployment remain open. Wardrobe classification is unchanged.


## Rebirth receipt clock correction (2026-09-27)

The two rebirth producers used PostgreSQL's default `now()` for `created_at`,
while Honored Hero evaluates every prerequisite against the game process's UTC
clock. The three quest producers already pass their game UTC timestamps.
A bounded read-only probe on the isolated Windows/PostgreSQL instance found the
database timestamp ahead of the client clock **after** the read in99/100 samples,
by up to **15.445ms**. Thus a committed rite could be temporarily classified as
future evidence, matching the intermittent immediate-title failure mechanism.
This measurement concerns this environment, not a universal PostgreSQL delay.

First and second rebirth now explicitly pass their existing authoritative `Clock()`
as the receipt's `created_at` parameter in the same transaction. The existing UTC
validation, ownership/payment checks, transaction rollback and future-evidence
filters remain intact. No timestamps are backdated, no tolerance interval is
introduced and historical receipts are untouched. No schema migration is needed.

A new deterministic receipt-clock assertion fails against the previous binary
(`obj/rite-clock-before.log`) and passes after the change. The earned career freezes
both the final rite and title consumer at one exact microsecond-aligned game UTC
instant. Its title is available immediately at that instant and remains unavailable
one microsecond before it. The final title and award recover in the existing fresh
child process without duplicate grants or changed item/currency state.

Validation: isolated build; **698 parent career +5 child checks** and **1846
second-rebirth checks** pass. The suites share prerequisites; do not sum them.
Both rite clocks are checked explicitly; the second-rebirth suite includes class
transitions, rollback, stale preimages, failed commits and concurrent recovery.
Logs: `obj/rite-clock-build.log`, `obj/rite-clock-career-tests.log`,
`obj/rite-clock-rebirth-tests.log`. The diagnostic-only clock probe remains under
ignored `obj/`. The temporary career failure print was removed in favor of exact
boundary assertions. This closes the demonstrated receipt-clock defect; native
and authenticated Host acceptance and live deployment remain pending.


## Same-character actual socket journey (2026-09-27)

Ordinary RoadmapProbe has now earned Courier Loop, Soul Trial and Path Hunt
through actual Account/Game TCP navigation, normal NPC dialogs and real combat,
without seeding their counters or final receipts. All three earned reward sets
and receipts survive actual game/database restarts; duplicate claims are refused.
[Latest earning evidence](ROADMAP_STARTUP_ACCEPTANCE.md#path-hunt-full-ordinary-earning-and-reward-recovery-2026-09-27).

First and second rebirth still remain for this character. This closes the three
quest portions of its native-packet journey, not the five-receipt career/title
acceptance gate. No wardrobe matrix upgrade or native DX9 completion is claimed.


## Fourth receipt earned over actual sockets (2026-09-27)

The same ordinary character has now earned Courier, Soul Trial, Path Hunt and
first rebirth through actual Account/Game TCP and retained all four final
receipts across real game/database restarts. Its first-life promotions and Moon
Box quest were also earned normally. First rebirth used earned CPs at the
existing stone shop; original gem/water crafting acceptance remains open.
[Evidence](ROADMAP_STARTUP_ACCEPTANCE.md#first-rebirth-earned-through-the-ordinary-npc-2026-09-27).
Second-life leveling/promotion and second rebirth still prevent claiming the
complete actual-socket Honored Hero journey. Native acceptance remains pending.


## Second-life leveling earned (2026-09-27)

The ordinary reborn career character has now earned level120 again: normal EXP
Ball purchases/use, player attribute allocation and all 20 Bandit kills plus
Courier turn-in. A distinct life1 receipt and bound permanent reward persist;
the exact 387-point budget and all earlier receipts/rewards survive restart.
[Evidence](ROADMAP_STARTUP_ACCEPTANCE.md#second-life-courier-leveling-and-receipt-isolation-2026-09-27).
Second-life promotion and the second-rebirth receipt still remain before the
actual-socket Honored Hero award can be claimed as verified.


## Actual ordinary career and restart completed (2026-09-27)

The same non-GM character now earned all five prerequisite receipts through real
Account/Game TCP, including first-life combat/quests, both life-leveling and
trainer-promotion paths, and both rebirth NPC transactions. The first Moon Box
was combat-earned; the second-life offering came from the existing30-CP pack.
The Heavenly Stone and Exemption Token came from normal shops with earned CPs.
The token's loaded `ItemtypeSub.txt` override is888CPs, superseding the base
catalog's999 for this candidate.

Before the fifth receipt, the title could not be equipped. After second rebirth,
ownership and normal equip notifications arrived. Actual game/database restart
restored the equipped title, one permanent/unrevoked award, unchanged old
receipts and owned rewards. [Exact evidence and limitations](ROADMAP_STARTUP_ACCEPTANCE.md#honored-hero-earned-and-equipped-through-actual-sockets-2026-09-27).

The wardrobe matrix now marks Honored Hero `code-validated`. Earlier statements
that actual Host acceptance keeps it predicate-only are superseded. Native
rendering and deployment remain pending; mining/crafting and Abyss alternative
routes are separate outstanding acceptance tasks.
