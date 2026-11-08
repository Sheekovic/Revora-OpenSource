# Holy Grail and guild-season research (M7.1a)

The installed 6711 catalog resolves the item identities needed for this route.
[M7.1b](GUILD_SEASON_BATTLES.md) now owns weekly combat results and the season calendar.
[M7.2a](GUILD_SEASON_CLAIMS.md) implements leader claims;
[M7.2b](GUILD_SEASON_PRIZES.md) implements earned token exchanges.
[M7.2c](GUILD_PRIZE_ROLLOVER.md) implements earned Gold rollover.
[M7.2d](GUILD_CUP_STATS.md) corrects the installed cup bonuses.
[M7.3](HOLY_GRAIL_CRAFTING.md) implements the earned Grail craft and both
permanent wardrobe awards. Native full-route acceptance remains open.

## Installed evidence

Read-only input: `C:/Users/Administrator/Desktop/Revora/6711/ini/itemtype.dat`,
SHA-256 `D49F0658BF01387B99C284DDEC81713838F2EAE3ADB8E63666E5C906F63CF87A`.
The existing Revora decoder was used in memory; no client file was modified.
The corresponding server `bin/Debug/database/itemtype.txt` contains these IDs.

| ID | Installed name | Relevant meaning |
| --- | --- | --- |
| 723467 | LordToken | Exchange at Guild Chief for prizes and CPs |
| 2100055 | BronzePrize | One winning round; 900 HP/MP |
| 2100065 | SilverPrize | Two winning rounds; 1,200 HP/MP |
| 2100075 | GoldPrize | Gold seasonal prize; 1,500 HP/MP |
| 2100085 | GoldTrophy | Twelve trophies lead to the special cup |
| 2100095 | HolyGrail | Twelve Conquest Cups; 1,600 HP/MP |

The last two descriptions use different English names for the material/result
chain. The mapping of Conquest Cup to Gold Trophy is a strong inference supported
by both installed descriptions and the official conversion rules below; there
is no separate installed item named ConquestCup in the reviewed catalog.
The unrelated Grail 728377 belongs to the Longevity Spring quest. Euro trophy
variants also have different identities and must not enter this recipe.

Installed `Storage.ini` SHA-256:
`00490ADA2256A196DBD0FFB645FD0CE7B9BE51069AAD57D1346AEC9A44792DB9`.
Entries 32 and 33 tie both rewards to the twelve-cup Grail crafting requirement:
Invincible title key 20342034 and Supreme wings key 60116011. The client names
crafting as the requirement; merely possessing or equipping a Grail is not proof.
No timed duration appears in those requirement entries. Permanent crafted awards
are the intended implementation; genuine-client display acceptance remains open.

## Official edition evidence

The [2017 seasonal guide](https://co.99.com/guide/event/2017/sgw_16.shtml)
describes five weekly wars, one Lord Token per winning guild, and exchanges of
one/two/three tokens for Bronze/Silver/Gold plus 3,000/6,000/20,000 CPs. Tokens
expire with their season. Unclaimed rewards follow a transferred guild leadership;
claims run Monday 01:30 through Thursday 23:59. At the next contest, the former
Gold Prize holder receives a permanent Gold Trophy. The page contains mixed old
headings and historical material, so its edition labels are not reliable alone.
The [dated 2017 announcement](https://co.99.com/news/2017-02-13/the_16th_super_guild_war_will_start_on_march_4th_.shtml)
independently confirms the five rounds and token exchange amounts.

The [2019 rules](https://co.99.com/news/2019-05-21/22nd_super_guild_war_on_05/25-06/22.shtml)
explicitly connect twelve Gold Trophies to a Holy Grail and retain the leadership
transfer and Gold Prize replacement rules. Its Grail base statistics match the
reviewed installed row. This corroborates the item chain without importing the
2019 dates or NPC coordinates into Revora.

The installed Gold Prize description says more than three rounds, whereas both
official exchange tables price it at three tokens. Use the explicit three-token
exchange for the implementation and record the description discrepancy.

## Current server gaps and provenance

Reviewed server baseline: `d8b388a`.

- `GuildWar` uses map 1038, pole 810 and gates 516074/516075, scheduled Sunday
  15:00-16:00 server local time. The separate `SuperGuildWar` uses map 1234,
  pole 811 and Wednesday 20:00-21:00. These are existing Revora schedules, not
  official 2017 times. Both keep mutable scores and pole ownership; neither
  supplies a durable five-week seasonal result ledger.
- The existing war reward loops distribute Lord Tokens using local rank-based
  payouts. Those loose tokens have no season or finalized-round provenance.
- NPC 900046 exchanges loose tokens for unrelated rewards. It also grants a
  four-day Gold Trophy from FirstGuildPack and a timed Grail from credit points.
- Other legacy NPC paths sell Gold Trophy for RVPs or grant a one-day Grail for
  Easter materials. Item pack 3100017 also yields Gold Trophy. The commented
  twelve-trophy handler would return Gold Prize 2100075, not Holy Grail 2100095.
- Existing item ownership, timed rewards and ordinary shop services must not be
  reclassified as earned seasonal progress. They remain untouched in this audit.
  Existing cups may still be usable equipment, but they cannot establish a
  historical season result or a verified Grail crafting award.

`ProgressionEvidence.Grant` already supports transactional award receipts. Its
unique key is `(uid, source, source_key)`, so a future craft must use distinct
source keys for the title and wings and verify both inserts before commit.
Adding only an equipment-observation predicate would bypass the quest requirement.

## Implementation sequence

1. Own the regular Sunday war lifecycle and its canonical pole/attacker validation.
   Freeze one final winner per scheduled battle; record real participation,
   guild ownership and replay-safe results. Recover or cancel interrupted rounds
   explicitly. Do not adopt a stale prior pole keeper as a new winner.
2. Group five consecutive scheduled Sunday battles into a durable Revora season.
   Preserve the existing local hour as the working schedule; this calendar is a
   Revora adaptation. Record season boundaries once, including daylight-saving
   handling. The independent Wednesday war must not accidentally double credit.
3. Add leader claims backed by winning-guild receipts and the documented claim
   window. Before claim, authority follows the current genuine guild leader;
   afterward the item belongs to the claimant. Keep claimed tokens tied to their
   season and exact item UID. Leadership transfer cannot replay a claimed round.
4. Implement the one/two/three-token exchanges atomically with their CP payouts,
   bound prize and consumed token receipts. At the next season replace only
   recorded Gold Prizes, including equipped/warehouse/offline ownership, with
   permanent recorded Gold Trophies. Never remove unrelated same-type items.
5. Craft a bound Holy Grail from twelve owned permanent trophies with valid
   earned provenance. Consume exact inventory preimages and provenance, insert
   the Grail and both permanent wardrobe receipts in one transaction. Reject
   timed, traded-away, already consumed or substituted materials. Capacity checks must account for the consumed slots; a
   suppressed write must leave all trophies and grants unchanged.
6. Verify ordinary guild members/leaders, transfers, disconnects, expired tokens,
   missed rounds, concurrent claims, rollover recovery, item positions, final
   crafting and immediate/reloaded wardrobe visibility. Native presentation and
   deployment remain separate deliverables.

The calendar origin and battle lifecycle are frozen by M7.1b. M7.2a implements [weekly claims and leadership transfers](GUILD_SEASON_CLAIMS.md).
M7.2b adds [earned-token prize exchanges](GUILD_SEASON_PRIZES.md).
M7.2c adds [owned Gold Prize rollover](GUILD_PRIZE_ROLLOVER.md).
M7.2d corrects [cup statistics](GUILD_CUP_STATS.md), and M7.3 completes the
[server Grail earning/crafting route](HOLY_GRAIL_CRAFTING.md). Legacy purse migration
and native acceptance remain open; this does not close the M7 umbrella.
