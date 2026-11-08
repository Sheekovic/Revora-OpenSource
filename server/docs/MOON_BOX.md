# Moon Box quest restoration

M4.2 implements the ordinary-player route from Fortune Teller through the six
tactics, Soul Jade and the Life Tactic reward. Code and isolated automated
checks are complete. Deployment and genuine-client validation remain pending.

## Rules and evidence

The [official Moon Box guide](https://co.99.com/guide/quests/moonbox.shtml)
establishes level 70 entry, random tactics, six distinct tokens, a Soul Jade
exchange and the final choice of a Moon Box or three Meteors. Refusing the
spirit consumes the jade without a reward. The installed server and 6711 client
agree on the maps, NPCs and material identities used here.

Fortune Teller (600050) stands near Twin City 489,622, matching the installed
client mission. Maggie (600003) opens paths from map 1042. ToughHorn families
6000–6005 supply tokens 721010–721015 in maps 1043–1048. The Ghost in Death
Tactic (1049) exchanges all six for Soul Jade 721072, then sends the player to
Life Tactic (1050). One of its 25 installed ghosts is selected and saved for
each run. Only that ghost can accept the jade. Accepted rewards are Moon Box
721090 or three Meteors 1088001. **Only receiving the Moon Box records 11022**,
matching the installed achievement predicate. The Meteor ending completes the
run without that achievement; the player can repeat the quest for the box.

Explicit Revora v1 choices:

- Each eligible solo kill has a 20% token chance, guaranteed by the twentieth
  committed kill in that tactic. A full bag preserves the guarantee until a
  later kill with space. Tokens arrive bound in the killer's inventory.
- Each character owns one repeatable, persistent run. No daily limit or expiry
  applies. Buying a box or collecting unrelated tokens never proves this quest.
- Maggie chooses among six tactics and Death until all six tokens are earned;
  a full set then guarantees Death. After a defeat with a jade, she restores Life.
- A defeat returns the player alive to Maggie, preserving earned materials.
  The portal forbids PK; hostile tactics and Life use the existing PK-free rules.
- Death prevents external escape while alive. Its confirmed surrender option
  consumes the run's earned tokens, ends the run and returns to town without a
  reward. The historical paid escape/completion shortcuts are not enabled.
- Box and Meteor rewards are unbound. The existing Moon Box promotion material
  use remains available; this change does not implement a new box-opening loot table.

## Persistence and ownership

`revora_moon_runs` stores the current run UUID, phase, destination, six token
UIDs, six capped kill counts, jade UID and selected Life ghost. Completed/refused
claims have immutable `revora_moon_results` rows. Accepting a later run replaces
the current row while preserving results and permanent achievement evidence.

Every exchange checks the physical inventory and database ownership of this
run's exact materials. Consumption, reward creation, saved transition and final
achievement share one transaction. Item cache/client updates follow commit;
failure fences further mutations until reconnect. Ordinary inventory loading
recovers committed materials and rewards. Tokens/jade cannot be dropped, sold,
traded or newly deposited into storage. Existing stored legacy materials remain
loadable but cannot substitute for a run's tracked items.

Native monster death processing suppresses duplicate notifications. Progress
requires the canonical ordinary player, matching map/family, and nearby death.
NPC actions require proximity to their installed actor. Per-player item locks
and database row locks serialize exchanges. PvP death transfers are queued to
the world timer so a killer never acquires the victim's item lock.

## Client evidence and validation

The private catalogs were inspected read-only; no client asset is committed.
SHA-256 evidence:

- `ini/itemtype.dat`: `d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a`
- `ini/Questinfo.ini`: `f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14`
- `ini/GameMapEx.ini`: `64ce044b2eb7ad15e980d91a5834f19d5ccc5dee2d3022cacb72e7a99833af7f`
- `ini/Achievement.dat`: `df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`

Mission 2378 describes the quest but has expired historical activity dates.
NPC dialogue and inventory supply the current interface. Native tracker
visibility and actual client rendering are not claimed as verified.

The isolated build passed **31 Moon Box checks and 324 regressions**: 44 Demon,
78 Elite PK, 48 CTF, 63 progression and 91 wardrobe checks. Tests exercise actual
ordinary-player NPC dispatch, server monster/PvP deaths, twenty-kill guarantee,
forged materials, database rollback at all exchanges, full bags, duplicate
claims, item-loader recovery, surrender and all three terminal choices.

Run `tests/PotencyHunt/Verify-PotencyHunt.ps1` with `-MoonOnly`; run the existing
five regression modes sequentially because they share a disposable schema.
Validated staged executable SHA-256:
`6486e64df8c973d8ba6f0db6e6d21b7535ff1986c9b03945a3505519ecfa14c7`.

The later catalog audit narrowed 11022 to the box choice before deployment.
The isolated rebuild and all 31 Moon checks passed with that correction,
including rejection of the achievement on the Meteor ending. Corrected staged
SHA-256: `a95912afaddc9562a8de622cf37c26dec7358d16984272a5d63fb9dc700fc266`.

No live server replacement, restart, database migration or client patch was
performed. Before activation, preserve the current binary/database, review M1's
achievement migration and verify travel, combat, materials, final choices and
reconnect on genuine 6711 clients. Rollback restores the previous executable
while retaining earned item and progression data.

## Owner reward upgrade (2026-09-20)

New accepted runs freeze `valuable_reward=true` and guarantee an additional
bound catalog P7 soul at successful completion. Existing runs default false
and retain their prior rewards. This is a Revora policy, not a retail drop claim.
Release Souls includes its existing paid option without the earned achievement;
Moon Box includes both the Moon Box and three-Meteor endings, never refusal.
Existing repeat limits and other gifts remain unchanged. Capacity includes the
extra item. Completion, exact soul UID, other rewards and any consumption or
payment are committed together; NPC text reflects the upgraded contract.

Isolated build and 34 checks pass, covering old contracts, full bags,
failed claims, concurrent claims and item reload. Release Souls additionally
checks a suppressed soul insert and the complete walked route through using its
EXP Ball and reconnecting. Moon Box checks both successful endings, refusal and
zero saved P7 items after final-claim failure.
Log: `obj/chi-refill-validation/moon-p7-reward.log` (ignored).
Live deployment and genuine-client acceptance remain pending.
