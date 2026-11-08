# Vote reward delivery

Server implementation. Not deployed. Native acceptance remains pending.
Parent [M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open. This checkpoint
does not close that inventory.

The shared-purse integration is **not release-ready**. Reviewed source
checkpoints can be published under owner authorization while the unfinished
release gates below remain open. Review of
`374f173c2b9baf35ea01a1c4f832c87ebdd439fb` reproduced six additional failures:
CP and RVP setters adopted coincident external totals, explicit CP and RVP
saves overwrote external awards, overlapping compound rewards lost a credit,
and opposing transfers entered under owner locks deadlocked. The earlier
four compatibility checks passed, but did not cover these cases.

Supervisor fixes make CP assignment and both explicit online purse saves
require the saved preimage. Transfers no longer use a global gate, acquire
database row locks in character-id order, and decline contention when called
while already holding one owner lock. No debit occurs on that rejection.
Both purses remain fenced if projection fails after the transaction commits.

`77034ce` adds explicit committed RVP projection and removes the unsafe
equal-total adoption. Its two-helper concurrency check demonstrates that
`AddConquerPoints` retains both credits; it is not a production-caller audit.
Supervisor review found further failures in callers: RevoraMaster reported
successful payment after a rejected debit, GenderChange mutated appearance
after rejected payment, generic CP/RVP writes bypassed saved preimages, and
database exceptions escaped ordinary purse mutations without fencing.
The bounded fixes reject those payments before dependent effects, disallow
generic purse writes (as already done for silver), and fence database failures,
including ambiguous commits. Unmanaged load/bootstrap writes remain private
to the explicit purse-save APIs.

Boss Rush stores the claim clearance with its exact CP, Revora Point, gold,
and Perfection Stone award in one transaction. The saved claim is compared
before either purse changes. A purse conflict, a changed claim, or a statement
error leaves that claim unpaid and does not report success. Vesper stores the
Revora Point payment and the purchased item in one transaction. A rejected or
ambiguous purchase is not described as untouched or successful. A player-booth
Conquer Point sale locks the two owners in character-id order, then stores both
purses and the item owner together. Insufficient funds and seller overflow
return before any write. An external purse change or an item-ownership miss
rolls the sale back and asks both characters to reload. Silver booth sales and
the stale second booth list still move money and the item in separate writes.
Summer packages are still refused by both booth paths. Summon Fox, Crane,
Panda, and Voltaic Warg, Inner Power, guild creation, union creation and rank,
the windwalker branch, merchant application, and the warden's bail do not grant
the spell, points, guild, rank, or release when payment does not commit. Guild
creation still writes the guild after the payment and refunds that payment when
the guild save fails. Other NPC menus were not all reclassified and can still
continue after a failed spend. `CpGrantDelivery` still marks the shop order
delivered before the points are stored. A direct unlocked read followed by an
absolute purse assignment can still drop one of two rewards.
`--purse-review-only` exercises these release gates separately from the older
`--purse-compatibility-only` fixture.

Supervisor review of `f9c8435` restored unbound Boss Rush victory stones and
keeps the entire claim pending if its configured stone is missing from the
catalog. Vesper and CP booth purchases send the committed item to the client;
installing with `ItemUse.None` alone suppresses that packet. CP booth settlement
also rechecks the canonical sessions and live item restrictions under both
owner locks, and compares stored bound/lock/suspicion/stack fields before
transferring the item. Claim and shop error messages require reconnect without
asserting whether an uncertain commit paid. Native client packet acceptance is
still pending; source review of these send calls is not native acceptance.

Boss Rush win and failure store the claim under the player and vault locks
before the instance is marked finished. A failed claim write retains the chosen
outcome in memory so the next tick retries it before advancing combat or applying
a later deadline. Issuance and terminal marking share the claim owner's lock.
Victory is claim 1 and a
failure on boss 3 or later is claim 2. The configured amounts are unchanged.
Inner Power NPC 900019 charges exactly 500 Revora Points for 2000 potency or
4000 Revora Points for 16000 potency in one purse and `revora_inner_purchases`
transaction. It does not call the chat command. That ledger is separate from
`AppliedPackPoints`, so a pack award and an NPC award cannot project each
other twice. Login reads the file and then applies only the unpaid gap. A
projection failure after the database commit asks the player to reconnect and
does not describe the purchase as unpaid.

Supervisor review of `158f195` reproduced a potency-lock/global-file-save
inversion and a failed victory write becoming consolation after the deadline.
The purchase now releases its potency lock before saving or handling failure.
Tests exercise that lock boundary and the actual file save/cold loader,
as well as both directions of Boss Rush outcome changes during write retries.
Issuance verifies or stores a saved claim before marking an instance terminal.
A value in live memory alone is not proof of a saved prize.

Boss Rush issuance and consumption merge the locked vault row with this
session's baseline. A key changed only in memory keeps the memory value. A key
changed only in storage keeps the stored value. Conflicting changes to a key
on both sides fail the write; identical changes agree. The same rule is used by a normal variable save. The live
vault object and its lock stay in place. Each run has an id. A prize already
stored for another run is left in place, and the new outcome is parked until
that prize is claimed or a login finds the parked outcome with no active
prize. Active boss and life checkpoints are stored, while arena contents
remain in memory. Claim and shop error messages still require reconnect without
asserting whether an uncertain commit paid.

Supervisor review of `0420f4f` reproduced two run-identity defects. Claim
consumption now checks the saved run id as well as the prize kind. Issuance
rejects a different outcome already parked for the same run, including when
no active prize exists. Tests cover both checks, preservation of unrelated
live and stored progress during consumption, and an issuance projection
failure followed by retry of the committed run.

Run ids come from `revora_boss_rush_run_seq`; allocation failure refuses
entry. Admission records boss 1 before teleport or announcement. A failed
admission checkpoint does not open the instance. Each later boss records its
number before the spawn, and a nonterminal death records the remaining lives
before the revive. Checkpoint success now requires reading back the requested
boss, lives and phase in the same transaction. A preserved newer/terminal row
is not confirmation of a conflicting request. Finish waits for a confirmed
terminal checkpoint before issuing its outcome; identical retries remain valid.
Login recovery pays one consolation when the last confirmed boss is 3 or
later, including a row whose phase is still active because the finishing
writes were rejected. A saved boss number whose wave was never confirmed
does not count as that boss. A fight whose current session still owns the registry entry
is not settled. A replacement login retires the previous instance during
that recovery and settles it then. If the registry is busy or a recovery
transaction throws, world ticks retry by character id on the current session.
The FIFO deduplicates characters and processes at most 16 per tick; offline
characters fall back to login recovery. An existing claim, pending prize or
ledger-only prize does not skip queuing other reward-eligible abandoned runs.
Queuing all eligible outcomes precedes projection of a single prize. A
rejected nonterminal death stays pending on the instance and the next tick
retries that life checkpoint before it advances or finishes the fight.
The confirmed retry also revives the player if the fight is still active and
the player remains in its arena. Recovery rejects an already stale caller,
checks current session ownership again during registry inspection, and freezes
recovered active progress as failed in the transaction that issues consolation.
That prevents a later checkpoint from choosing victory for the recovered run.
A checkpoint holds that character's session lease from before its database
work until commit or rollback returns. Deferred constraints run before the
ownership decision, so a replacement that lands during that wait is visible
before the checkpoint becomes durable. Login publishes through the same lease:
it disconnects the previous session, then stores the new one. The disconnect
callback defers Boss Rush metadata work if the registry gate is busy. A checkpoint that already holds the
lease finishes or rolls back before publication continues. A conflicting
checkpoint rolls back the entire request, including partially merged boss,
life and phase changes. Missing, fake and replaced sessions cannot checkpoint;
an offline session cannot settle its own abandoned progress. An uncertain one is the database not answering
and is the life-drop retry. A replaced session's tick does not delete the
registry entry; it asks the current session's next tick to recover it.
Retiring a registered fight before boss 3 marks that run failed and pays
nothing. Registration first writes an admitting checkpoint owned by that session
and generation. Recovery leaves the admission alone only while that same
session is still current. Replacement before registry publication does not
teleport or register the old session, and the admitting row then closes
with no prize. The registry gate only checks capacity and publishes or
removes fight metadata. Map preparation happens outside it. Entry holds
the session lease across teleport guards, so installation waits until that
call returns and cannot publish during the guard. A fight that is published
but not yet entered is ignored by combat ticks, death, and rush hits.
Raising the saved boss records the next wave before the monster is inserted.
That row's `spawned` flag stays false until a second checkpoint confirms the
insertion. Existing rows default to confirmed, so a legacy boss number stays
eligible. An unconfirmed row keeps the previous boss as its reward progress:
recovery pays only when that confirmed boss is 3 or later, or the phase is
already won. A spawn that throws before insertion leaves the new wave unpaid.
While this process still has the map, an inserted monster is confirmed before
the fight can advance, spawn another boss, or take a new terminal outcome. A
crash after insertion and before that confirmation cannot be distinguished
from a missed spawn, so only the previous confirmed boss remains eligible. An
empty arena with an unresolved wave retries that same wave. Insertion must
retain the exact monster in the arena; a silent UID collision stays pending.
Life loss is checkpointed even while wave confirmation is delayed. A terminal
callback retains its first outcome while waiting, then computes the prize
from reconciled progress. Confirmation reads back `spawned=true` before it
accepts the wave. Recovery prepares the schema before taking the vault row,
without repeating schema DDL inside the settlement transaction. A
disconnect callback does not wait on that gate. Login and character creation
capture the generation of that publication on the session object. The session
lease stays held through early loading and creation gifts, and again through
account presence, answer, thread registration, online status and recovery.
Replacement through `SessionInstall` waits for those phases. Disconnect
callbacks, presence closure, shutdown saves and pool removal wait for the same
lease and then remove only this object. A caller that already holds the
character item lock defers that work instead of taking the lease under the
item lock. A disconnect callback that throws still reaches socket shutdown and
retirement. A deferred Arena shutdown keeps its actor and blocks replacement
until teardown can proceed. Obsolete generations only dispose their own
transport, timers and presence token; they do not run world callbacks or
write character state. An exception escaping login
loading retires that session before releasing the phase lease. Character
creation stores the character row, account pointer, server identity counter
and starter-gift receipt in one transaction. That transaction locks the
account before the name, so two creations for one account cannot both commit,
including when they propose different character ids. Persistence retries of the
same id revalidate the saved owner and name; a missing or ambiguous server counter
rolls back creation. Packet-level retry/relogin is still an acceptance gap.
The two starter packs and their separate delivery bit commit in one transaction
under the session and item locks. Missing catalogs or capacity keep delivery
pending without granting either pack. Consuming a delivered pack never makes it
owed again. Failure after commit retires the login projection so a fresh item
load recovers the saved packs. Previously completed legacy starter receipts are
honored; this does not repair a historical partial grant.
Class gear and its starter receipt commit in the same transaction, after the
pack receipt and without removing equipment the player already owns. Occupied
target slots leave a new kit pending instead of creating duplicate equipped rows.
A failed projection after commit retires the session for a fresh item load.
A missing catalog leaves the kit pending and writes nothing. A partial kit is not marked
complete and is not rebuilt. Once the receipt is set, consuming or moving the
kit does not grant it again. A complete kit already stored only repairs the
receipt. The raw creation packet is exercised by the creation handler; transport
remains unauthenticated and entity loading may still use a fixture hook.
Disposable REVIEW-302 checks cover occupied slots, suppressed item/receipt writes,
postcommit interruption, fresh loading through DoLogin.LoadData, and moved gear.
The fake cleanup failure fixture proves that other cleanup attempts continue;
it does not establish draining every pending callback or removing every world-view
reference when a disposer itself fails. Native acceptance remains pending.
The ownership helper drops its publication after a failed attempt. A
disconnected or retired attempt cannot republish. Failure after publication
ends the attempt instead of allocating another id. The creation success
packet and login run under the creation lease. Fake loaders hold the id's
lease while initializing and publish only after loading. A real login disposes
that fake's timers and world registration without removing or changing the
successor. Initialization failure removes only that fake and disposes its
local timers.
City guards, living-world actors and wave allies install only through the fake
pool path, which will not replace or remove a real session or overwrite a
different fake actor. Rejected fake loaders dispose their timers and do not
register for world updates.
Guild disband does not remove a pool entry
by guild id. An admitting row without a live owner, and active progress before
boss 3 with no live instance, become failed and pay nothing. Vacant pool
entries are dropped only when
the snapshot is still current. Partial login retirement uses the account id when
the placeholder entity has UID zero. A busy disconnect drain retains every
unprocessed member of its batch.
A consumed outcome is not rebuilt. Early failures with no prize do not hide
later eligible progress. The first terminal phase stays immutable. Recovery
defers active settlement if it cannot immediately inspect registry ownership;
it never waits for the registry while holding player and database locks.
Tick treats a player outside the instance map as a failure before considering
boss progression or victory. Map position and monsters remain in memory.
Boss advancement holds the session lease through the checkpoint and live spawn,
and rejects a duplicate number after waiting. A production-method fixture with
two ticks at the same cleared boss and a database row barrier requires one new
monster. Calls holding the registry gate cannot bypass persistence or spawn.
Missing monster catalogs are checked before advancing durable boss progress,
using the captured template for the spawn. This prevents a missing boss-3
template from creating consolation eligibility on reconnect. The durable
checkpoint still precedes map insertion; crash/exception recovery across that
boundary remains unfinished.
Recovery holds the session lease through its transaction, live projection and
nonblocking metadata removal, in session -> player -> vault -> database order.
The production vault-row barrier fixture requires installation to wait for
recovery, then verifies that the successor sees the same single prize.
Review of `fc093cc` reproduced replacement publication during both immediate
death revival and a pending-life retry, after their life checkpoints committed.
Death and Tick now retain the session lease through decisions, persistence,
revival and live application. A pending death is retried by Tick before a new
death is accepted. Both production-method revive-guard probes require the
replacement to wait; the entry probe also directly requires waiting and uses
the real Boss Rush disconnect callback before replacement publication.
These are synthetic production-method interleavings, not authenticated login
or native-client acceptance.
Admission still releases its lease between reservation and arena entry. These
fixes do not establish complete serialization of admission, death, terminal
selection and login.

Supervisor review of `ed631d8` reproduced arena shutdown resumption bypassing
the session lease and disconnected/retired creation attempts republishing.
Resumption now uses the same lease wrapper as disconnect, including deferral
when the caller already holds the item lock. Further probes reproduced stale
shutdown marking a successor offline and disconnect dropping an actor retained
for pending Arena entry. Both now have regressions, including refusal of a
replacement until retention releases. These fixtures do not establish atomic
character creation or complete cleanup when a real login takes over a fake.

Supervisor review of `9f27eec` reproduced an inconsistent creation retry being
accepted, creation committing without a counter row, and a missing catalog
marking starter delivery complete with only one pack. The corrections have
disposable database regressions, including pack-transaction rollback, consumed
pack replay, interruption after commit and fresh item reload. The creation
fixtures use a separate Homecoming advisory identity and exercise account
presence instead of skipping it. They still substitute the entity-loading hook;
raw creation packets, the complete authenticated login and native acceptance
remain unverified. Fake takeover evidence covers screen/pet timer disposal and
thread unregistration, not every in-flight callback or world-view reference.

Durable lifecycle ownership remains an unfinished release gate. The contended-login
and existing-claim regressions from review of `8ae9d16` now pass. Review of
`282c075` additionally reproduced skipped outcomes behind pending/ledger-only
prizes, partial queuing of multiple progress rows, dropped database-error retries
and retries targeting an obsolete session. Those cases now pass production-path
fixtures. Review of `7c17747` reproduced replacement during a deferred commit.
The checkpoint now applies those constraints before it decides, while holding
the session lease, and rolls back when the published session is no longer the
caller. Installation through that lease waits until the in-flight checkpoint
releases it. A registered fight
retired before boss 3 is marked failed and pays nothing. An in-flight
admission stays admitting. Cold early progress without a live instance becomes
failed and pays nothing. A replaced session's tick leaves the registry
entry and schedules recovery on the current session. Recovery now inspects
registry entries without removing them, commits the terminal progress and queue,
then removes the retired instances after releasing database and player locks.
Rejected retirement therefore retains its retry evidence, including early
runs with no prize. Suppressed writes and vault merge conflicts schedule retries.
Tracked terminal rows are revisited even with an existing ledger receipt, so
durable retirement can complete metadata cleanup without replaying a consumed
prize. Checkpoint results distinguish confirmed, conflicting and uncertain
writes. Confirmed is the only result that advances a life or a boss. An
uncertain life drop is retried once the fight is still current. A conflicting
write is rolled back. Recovery uses a nonblocking registry metadata check.
Admission performs map creation, teleport and spawn outside that global gate.
Its admitting checkpoint and arena entry do not yet share one session lease.
The in-memory admission mark identifies a session and generation. Entry itself
now holds the lease through teleport and initial spawn; reservation-to-entry
ordering and expected-generation login effects remain unfinished.
Map position and monsters remain in memory. These production probes do not
prove every concurrent interleaving.

Recovery and claim now both lock the vault before ledger rows. Issuance
rechecks the consumed receipt under that same ordering before projecting
the claim, including when a replacement session claims between the earlier
ledger commit and projection. Regression tests cover the lock boundary and
that interleaving. They do not establish every concurrent outcome or complete
login ownership; checkpoint-to-map insertion recovery remains unfinished.

The release gate remains open. Silver booth sales and the second booth list
still move money and the item separately. Guild creation still pays and then
writes the guild. Summon refunds and spell-soul upgrades still commit the
purse before the spell row. Other NPC menus were not all reclassified. Online
and offline credit admission races are still unverified. `CpGrantDelivery`
still marks the shop order delivered before the points are stored. These remain
separate roadmap work; they are not prerequisites for implementing the public
vote-count fallback. The next priority is that fallback and its direct vote
eligibility/delivery prerequisites. M6.56b remains open.

`identity.xtremetop_votes` rows with status `voted` and no `delivered_at`
are the only payable rows. Pending, expired, missing and already delivered
rows are not credited. Stored `cp_amount`, `rvp_amount` and `gold_amount`
are paid exactly. The website catalog is 2500 CPs, 50 Revora Points and
50000 gold. No new amount is invented.

The character, the three purses and the delivered marker commit in one
transaction. The vote row is locked and read again, so a stale list cannot
choose the amount. The username and `accounts.entityid` must name that same
character. Overflow of any purse leaves the whole vote undelivered. There is
no clipped payment and no success message.

`ApplyFor` runs from character load and returns without payment unless the
caller is already the ordinary current session: not fake, fully loaded, not
fenced, action 2, and the canonical pool entry. `Pulse` walks online
sessions instead of stopping at the oldest offline votes. Offline and
ineligible players keep the voted row.

After commit, live Conquer Points, Revora Points and silver are projected.
`PurseBalance` records the saved CP and Revora Point image at load, including
a null column. Live memory uses zero for that empty balance. A full save
flushes the live values only when the stored image, null included, still
matches the row. An unloaded session can save only when its memory still
equals that row. Unsaved monster or session gains are included in the vote
payment. An external change to the durable row does not match the image, so
the vote stays unpaid and the session reloads. Silver uses
`SilverBalance.ApplyCommitted`. A projection failure fences the session.
`SaveEntity` then refuses to write. Reload shows exactly one award.

Monster kill credits use owner-locked `AddConquerPoints` and
`AddRevoraPoints`. Those helpers and `TrySpendConquerPoints` /
`TrySpendRevoraPoints` compare the saved image and write the new total
immediately. The ordinary Revora Point setter does the same and does not
treat a database total that merely equals the requested amount as its own
commit. After a reward transaction has stored Revora Points,
`ApplyCommittedRevoraPoints` updates the live balance and only that saved
image. A CP bag or custom gift compares the saved image and adds its reward
to the live balance. A JiangHu kill moves 1,500,000 Conquer Points in one
transaction, lower character id first, and only after both balances fit.

Active `ConquerPoints +=` / `-=` and `RevoraPoint +=` / `-=` reward and spend
lines now call those helpers, including NPC, event, guild war, boss, and shop
callers. The earlier search of 597 lines in 48 files counted comments and
retired branches. Those commented lines were left as comments. A direct
`ConquerPoints = before + n` after an unlocked read can still keep only one
of two rewards; `CpGrantDelivery` retains that pattern. Vesper's shop
does not hand over the item unless the Revora Point payment commits. Some
older NPC menus still grant spells, items or progression if a spend returns false.
`UpdateCps(uid, value)` credits an online character through
`AddConquerPoints` and otherwise adds with a checked upper bound so the
stored value cannot wrap. This does not prove ownership across concurrent
login/logout; a pool lookup alone does not serialize session admission.

`CpGrantDelivery` still marks the shop order delivered before it assigns
Conquer Points. The assignment now stores the points immediately, which is
the previous setter contract. The order and the purse are still two writes.

Silver validates its saved preimage and includes ordinary unflushed gains or
spending in the committed balance. The account-to-character mapping is held
under a shared row lock through commit.

Tests use a synthetic table in the disposable schema. They do not write
production `identity.xtremetop_votes`.

## Still outside this checkpoint

The public XtremeTop vote-count fallback lives in the parent Web source, not
in this game payer. That Web source admits a callback and a public-count match
through one eligibility transaction. `VoteGrantDelivery` still pays only a
`voted` row with no `delivered_at`, including a public-count match that has no
voter address, and it pays that row once. Reload does not repeat it. The
parent source is not published or deployed by this game checkpoint. A
disposable test can confirm one row through the website store and pay that
same row here once; reload does not repeat it. Provider callback setup is
still unverified. Local tests are not provider confirmation. Ordinary players
on the live server are not paid by a source commit alone.

The handoff suite requires `REVORA_WEB_TESTS` to point to the reviewed parent
`tests/Revora.WebTests/Revora.WebTests.csproj`. Build and run that project in
Release first, then run `Verify-PotencyHunt.ps1 -VoteGrantsOnly`. The child uses
that existing build, with a two-minute deadline. The player reloads before the
first delivery and again before replay verification; no temporary checkout path
is assumed. Use the same disposable-test database configuration for both suites.

Review 307 corrected local parent Web source at
`0a99976528dd1e403327251d161b5f31f43ea817`: canonical listing parsing,
streaming byte/deadline bounds, whole-observation consumption, request ordering,
and callbacks through the existing trusted nginx loopback hop. The following
vote checkpoint adds snapshot age, contested locks, and the migration preflight
in that parent tree. `VerifyWebConfirmedHandoff` asks that website store to
confirm one disposable row and then pays that same row once. The earlier
fixture still inserts its own row and only proves the payer. Neither test
deploys the website or shows a live ordinary player being paid. Unrelated
gameplay audits do not block this voting work.

The parent stores the original HTTP Date separately from the Age-adjusted
time. A later request whose Date is older does not reset the cursor. The same
Date with a different total is ignored. A legacy cursor is adopted with no
credit. `VerifyPublicCountHandoff` feeds synthetic dated HTTP listings through
the website fetcher, parser and store to confirm one disposable row from a
public-count increase and then pays that row once after
a fresh reload: 2,500 Conquer Points, 50 Revora Points, and 50,000 gold.
`confirmation_source` is `public-count` and the voter address is empty.
Reload does not pay it again. The callback handoff remains. Both rows are in
a private schema. The poller stays off. This does not deploy the website,
configure the provider callback, or pay a live player.
