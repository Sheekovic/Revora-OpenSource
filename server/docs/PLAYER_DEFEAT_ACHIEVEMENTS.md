# Player defeat achievement earning

## Local evidence

The approved client Achievement.dat SHA-256
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`
identifies four distinct conditions: 10605 blue-name defeat, 10606 red-name
defeat, 10607 black-name defeat, and 10611 defeat of a personal enemy.
This is local catalog evidence, not verification of native visual precedence
when several flags coexist.

`Entity.DieCore` snapshots the victim's existing FlashingName, RedName and
BlackName flags and the killer's enemy membership before death effects. Each flag must also be unexpired at the captured local
server time, using the same deadline semantics as the flag cleanup loop. It
records only after the core accepts death, assigns the killer and zeros HP.
Early-return survival such as Absolution does not reach that receipt hook.
Guard/adventurer special paths return before it. Capture rejects simulated or
companion bodies, self kills, dead participants, noncanonical sessions and
players in different map instances. Matching conditions can coexist: revenge
against a flashing attacker earns both corresponding achievements.

This placement precedes `ClearFlags`, crime penalties, black-name handling and
death callbacks that may teleport the victim. Enemy membership is directional:
the defeated player must already be in the killer's list. The current death's
later AddEnemys call adds the killer to the victim's list, not the reverse.
`Database.KnownPersons.LoadEnemy` loads saved relations at login.

## Persistence and limits

`revora_player_defeat_credit` retains at most four rows per character, keyed by
character and achievement kind, including first victim, map, unique receipt and
completion time. Replayed notifications cannot produce repeated awards. Receipt
insertion commits first; all pending achievements and completion markers then
commit together. Progression reads/login recover a committed receipt if evidence
projection failed. A suppressed initial receipt write fences the killer and
cannot be reconstructed on reconnect. Death and its first receipt are separate
operations; this does not claim durable recovery of every possible database outage.

The service serializes its writes on the killer's existing inventory lock. It
does not redesign the legacy combat scheduler or make all player death side
effects atomic. Native display, simultaneous lethal combat, overlapping name
flag presentation, every event-specific death route and live deployment remain
unverified. No new currency or items are awarded by these achievements.

## Validation

Build and 41 focused checks pass using `-PlayerDefeatOnly`. Actual lethal damage
covers each condition; nonlethal hits grant nothing. The revenge journey begins
with a real defeat that creates the enemy relation, reloads it through the
production social loader, revives the player and defeats that enemy. Checks
cover entity/achievement reload, concurrent replay of accepted notifications,
invalid participant snapshots, receipt failure and evidence recovery. All three
name flags are tested immediately before and at their exact expiry; actual
lethal hits reject expired flags still waiting for cleanup. They do
not simulate a native client or an entire leveling journey. The existing
34-check revival suite also passes after the death hook change.


## Enemy-guild member defeat (10227)

The same hash-bound local client catalog identifies 10227 as defeating a member
of an enemy guild. This is separate from personal revenge (10611). The accepted
player-death path now captures both canonical guilds and validates their member
objects. At receipt time both guilds and current memberships must still match; a single SQL statement checks both saved
character memberships and the killer guild's directed `guildenemy` relation.
Reverse-only rivalry, unsaved rivalry and stale membership do not qualify.
No GM, VIP, owner identity or donation threshold is required.

`revora_enemy_guild_defeat` stores one first-kill receipt per killer, with victim,
map and both guild IDs. Achievement 10227 is projected in the second transaction;
a pending receipt recovers on login even if the guild rivalry has since ended.
Repeated callbacks cannot issue a second receipt. These are achievement points
only; this adds no currency or item rewards. Initial receipt failure still
fences the killer and cannot reconstruct the already accepted death on login.

The saved relationship check uses one database statement snapshot, not row locks
on both fighters. This avoids taking a victim row lock after the killer lock in
reciprocal combat. It does not make legacy guild membership/rivalry changes and
all death effects one atomic operation, nor guarantee credit through every
simultaneous relationship change. Native presentation and live deployment remain
pending.

The focused player-defeat suite now passes 87 checks, including actual nonlethal
and lethal ordinary-player combat, production rivalry reload, distinct personal
revenge, stale runtime membership, saved membership mismatch, reverse rivalry,
rivalry removed after capture, concurrent callback replay, ordinary login,
suppressed initial receipt and failed evidence projection followed by recovery.

## Retained crime-status progression (2026-10-02)

`career-retained-crime-earn` and `career-retained-crime-reload` pass on the
same retained ordinary character 4294907001. Two existing level-100 ordinary
Ancient Devil donors participate with empty inventories. The offender first
lands a nonlethal calculated melee attack on the retained character, acquiring
the flashing-name flag through the attack handler. The retained character
then defeats it with calculated ranged attacks and earns 10605.

Subsequent calculated kills of the innocent participant raise the offender's
PK points through the normal death handler: first to at least 30, then to at
least 100. Defeating that offender at each threshold earns 10606 and 10607.
The fixture never assigns PK points, crime flags, lethal damage or achievement
rows. It uses the ordinary revive handler between fights. Each phase saves
only its explicitly loaded participants.

The cold process reload compares the complete saved career, credit receipts,
opponent profiles and enemy relationships. Each of the three achievements has
exactly one `player-defeat-v1` evidence row and its outgoing achievement packet
bit set. Both phases passed on their first run against private game SHA-256
`34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`.

Public-map placement, attack/revive cooldowns and attackability are controlled;
opponent starting class/level and prior funding remain fixture inputs. This is
private handler/persistence evidence, not native client acceptance, a complete
leveling journey or a live deployment. Personal-enemy 10611 and guild-enemy
10227 still require their own retained-character checks.

## Retained guild rivalry (2026-10-02)

All three `career-retained-rival-*` phases pass. The retained CareerBanner
leader declares RvpBanner an enemy using a sealed `GuildCommand.Enemied`
request. Declaring the rivalry alone does not grant 10227. A fresh process
loads the directed relationship and both existing memberships; calculated
ranged combat then defeats ordinary RvpBanner leader 4294908801 on map1005.
Another cold process compares the exact saved career, rivalry, opponent items
and guild-defeat receipt, and checks one `enemy-guild-defeat-v1` evidence row
plus the 10227 achievement packet bit.

The fixture controls arena placement, attackability and attack/revive cooldowns.
The opponent's level/class and original guild funding were supplied in earlier
tests. Arena admission and native client input are not established by this
combat fixture. No guild IDs, memberships, rivalry rows, lethal damage or
achievement rows are assigned directly in these new phases. The private game
binary remains `34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`.

## Retained personal revenge (2026-10-02)

The `career-retained-revenge-*` phases establish personal-enemy credit 10611
separately from the guild rivalry. On public map1000, the ordinary RvpBanner
leader defeats the retained character with calculated melee attacks. The
normal death handler creates the personal enemy row. Every owned item UID
remains present; ordinary equipment durability wear is retained. The character
has fewer than30 PK points before that defeat; no item protection is injected.

A cold process loads the personal enemy record, revives through the ordinary
handler, and wins the rematch with calculated ranged attacks. Another cold
process compares the complete saved career and relationships and verifies one
completed 10611 receipt, one `player-defeat-v1` evidence row and its packet bit.
The prior guild-enemy receipt also remains present through this rematch.

The first fight hit its45-second fixture bound after5085 hits without a defeat.
A read-only full snapshot comparison found only durability changes on five
items; a temporary exact-hash checkpoint recovery preserved that wear:

- Prior: `2e35a4a4caa5b53a76070c48ad63f6d0e07b3caf16e9b756f7c8ab4bf2adf4f8`.
- Current: `34f8a78ddb6d9fe2cb494b6ce3b8cd455dc8425ee7cb5168e4a24da023cdb719`.

With a270-second bounded fight, the weak opponent won after27040 calculated
hits. Enemy creation, cold rematch and cold reload passed. The temporary
recovery branch was removed, and the final fixture passed another cold reload.
This was a recovered timeout, not an uninterrupted first-pass success.

The fixture controls public-map placement, attackability and cooldown elapsed
time. It supplies neither lethal damage nor enemy/achievement records. Donor
eligibility and prior character funding remain fixture inputs; full native
input, representative balance and live deployment remain pending. This closes
the five retained PvP receipt checks, not the complete compatible320 career.
