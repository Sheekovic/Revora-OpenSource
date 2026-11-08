# Wardrobe quests and events: 6711 research

Research date: 14 September 2026, America/Los_Angeles (15 September UTC).
Server baseline: `23c520b`, Revora-6711. Scope: research and implementation planning.

This report preserves that research baseline. See the [current milestone checklist](../QUEST_EVENT_ROADMAP.md)
for subsequent implementation progress; baseline findings below are not updated retroactively.

**Recommendation:** first make achievement completion and event awards authoritative, restore local CTF and Elite PK, and build a small set of genuine classic quests. Follow with a summer crafting season for the five currently locked summer wings. Cross-server prizes require a separate tournament infrastructure project. Several historical rewards still lack enough evidence to reproduce their exact eligibility.

The companion [72-reward matrix](WARDROBE_REWARD_MATRIX.csv) accounts for every installed wardrobe entry and is maintained as a current-state inventory; see its [latest earning audit](../WARDROBE_AVAILABILITY.md). The [implementation plan](WARDROBE_IMPLEMENTATION_PLAN.md) turns these findings into deliverables, dependencies, acceptance checks, and outstanding product decisions. No gameplay feature was implemented or activated during this research.

## 1. What is actually missing

| Finding | Evidence | Implication |
| --- | --- | --- |
| The installed wardrobe has **72** reward entries. | Client `Storage.ini`; server `StorageManager.Points`. [L1, L2] | Audit the installed catalog rather than a current CO reward list. |
| **29** entries have eligibility expressions; **43** have none. | Distinct keys added by `TitleRequirements.Eligible` and `AddPrestige`. [L2] | An expression is not proof that players can acquire its prerequisites. |
| Victor and Infernal Beast wings have reward checks, but their scheduled local events are parked. | Early return in `WorldTournaments`; disabled CTF dispatcher and schedule. [L4] | Restore event lifecycles, not just reward ownership. |
| Rising Star counts client-reported bits. | `Achievement.Process`, action 2, calls `CreateFlaID` from a received ID. [L3] | A client claim currently substitutes for verified completion. |
| There are **337** distinct positive achievement IDs in this client, versus **416** physical bits. | Decoded catalog and thirteen 32-bit server words. [L1, L3] | Seventy-nine undefined positions must not count toward Rising Star. |
| Periodic wardrobe reconciliation rebuilds ownership exclusively from eligibility. | `Reconcile` replaces the entire set on login and periodically online. [L2] | Directly inserting a newly earned event title into the saved blob will not preserve it. |

The 43 locked rewards break down as follows. These are reward counts, not a claim that fourteen fully specified events can immediately be built.

| Reward family | Count | Missing dependency | Research readiness |
| --- | ---: | --- | --- |
| Cross-server CTF | 2 | Genuine inter-server guild competition and results | Reward identity clear; event edition needs selection |
| 2016 Mr./Ms. Conquer, including Fairy Wings | 7 | Flower-category election and historical rank rules | Partial; Beauty/Wise unresolved |
| Returning-player campaign | 4 | Historical activity eligibility and campaign claims | VIP mapping clear; exact campaign cutoff unresolved |
| Euro 2016 Planet wings | 1 | Original event reward condition | Event association only |
| Holy Grail | 2 | Cup acquisition, seasonal Guild War, crafting | Recipe clear; item/edition mapping unresolved |
| Cross-server Elite PK | 4 | Inter-server solo tournament | Placements clear; 6711-era duration unresolved |
| 2017 Mr./Ms. Conquer | 4 | Local and all-server election stages | Historical guide available |
| Romance wings | 1 | Versioned Valentine/election award | At least one documented historical award route |
| Fashion Icon Competition | 3 | Competition scoring and results | Placements clear; scoring/duration unresolved |
| Honored Hero | 1 | Original grant source | Unknown |
| Poker Cash Tournament | 4 | Tournament coordinator and verified standings | Placements clear; complete version contract needed |
| Cross-server Team PK | 4 | Inter-server teams, roster and reward delivery | Dated 2018 rules available |
| Horn of Summer wings | 5 | Material sources and crafting claims | Recipes documented; local item integration pending |
| Couples vs. Singles Heart wings | 1 | Faction contribution campaign | Association clear; threshold/duration unresolved |
| **Total locked** | **43** | | |

## 2. Evidence rules and limits

The installed 6711 catalog establishes reward identity and the requirement shown to this client. Official historical CO guides corroborate gameplay rules. Server source establishes current behavior. None of these alone proves a successful ordinary-player journey in the live server.

Use these confidence labels throughout implementation:

- **Confirmed locally:** directly read from the installed client or current source. This does not imply a verified packet capture or live playtest.
- **Historically corroborated:** an official dated event page supports the specific edition. Do not silently import a later edition's requirements.
- **Partial:** names or portions of a flow exist, but the complete playable or reward path is unproven.
- **Proposed Revora rule:** a design recommendation, explicitly distinct from historical fact.
- **Unknown:** the evidence does not establish the grant condition. Preserve that uncertainty rather than inventing an achievement.

CO's current site mixes editions. Examples include an Elite PK page with inconsistent bracket descriptions, a summer page whose lower bracket differs between prose and table, and undated quest guides whose coordinates or mechanics have changed. The 2018 Team PK article's URL says June 21, while its displayed publication date is June 28. Use displayed dates and retain the exact source URL. Mutable news index pages are weaker evidence than dated articles.

This is a catalog and source audit, not an exhaustive completion audit of all 337 achievements. No live player records or private account data were needed. Map placement, individual item acquisition routes, and packet behavior still require the implementation-stage checks described in the plan.

## 3. Restore existing local competitions first

### Local Capture the Flag

The client ties **Victor** to the winning local guild's leader. It separately ties **Conqueror** and **Radiant Wings** to the cross-server winner. The original wardrobe guide supports this distinction.[^wardrobe]

Revora already has CTF maps, bases, flags, scoring code, and a durable `RecordCtfWinner` hook. However, the tournament scheduler returns before its legacy section, the CTF schedule is commented, and its packet handler is commented. Construction of the CTF object during startup does not make the event playable. [L4]

Restore registration, authorized guild membership, flag pickup/drop/delivery, scoring, closing, and durable finalization under one event owner. A completed result should update the current winning guild once. The title should then follow that guild's current leader, matching the existing eligibility policy. Define tie handling, insufficient attendance, disbanded guilds, and disconnects before launch. Local CTF must not issue its two cross-server counterparts.

### Local Elite PK

The reward expression for **Wings of Infernal Beast** follows the unexpired highest-bracket champion status; the existing claim route grants that status for seven days. Tournament classes and signup/claim NPC branches exist, but the scheduled registration path is below the same early return. [L2, L4]

The official guide establishes Friday registration and knockout/final matches, but its bracket prose and table conflict. Select the client-compatible level brackets through a targeted local audit; do not substitute modern rebirth or Battle Power divisions merely because they appear on a current page.[^elite]

Implementation needs durable brackets, match ownership, legitimate wins and byes, no-show handling, spectator isolation, and a recoverable result claim. A pre-existing champion row or a GM test grant does not demonstrate that an ordinary player can win a fresh tournament.

## 4. Rising Star needs real quests and trusted completion

**Rising Star requires 320 completed achievements, not 320 achievement points.** The installed requirement is explicit. The 2016 wardrobe guide also says 320, despite mentioning a smaller achievement catalog from that period.[^wardrobe]

The installed `Achievement.dat` contains 337 distinct positive IDs across thirteen groups:

| ID group | Entries | ID group | Entries | ID group | Entries |
| --- | ---: | --- | ---: | --- | ---: |
| 101 | 30 | 106 | 17 | 111 | 29 |
| 102 | 13 | 107 | 22 | 112 | 32 |
| 103 | 32 | 108 | 30 | 113 | 23 |
| 104 | 31 | 109 | 31 | | |
| 105 | 29 | 110 | 18 | **Total** | **337** |

Only seventeen catalog entries can remain incomplete if a player is to reach 320. Class-specific, rebirth, historical event, and potentially mutually exclusive requirements therefore need a **per-character reachability calculation**. The presence of 337 IDs does not prove that any one character can earn 320. Class changes and reincarnation may matter; their effect must be checked rather than assumed.

Current `CompletedCount` counts set bits without restricting them to the known catalog. The code search found client ingestion as the normal `CreateFlaID` path, plus a separate administrative flag command. It did not establish a centralized server completion bridge from quest and event results. [L3]

Introduce a server-owned achievement catalog and completion service. Validated gameplay events should create completion records; requests from the client may open/query the UI but must not independently prove achievement. Retain legacy records for audit and distinguish verified, recoverable-from-durable-evidence, and unsupported historical completions. Do not fabricate completion or silently destroy historical evidence during migration.

### Classic quest backlog

The table lists useful initial targets and actual client achievement IDs. IDs are achievement identifiers, **not proposed quest packet IDs**. A missing completion hook and a missing entire quest are different problems.

| Quest/event | Achievement IDs or role | Current finding | Proposed work |
| --- | --- | --- | --- |
| Demon Exterminators | 11001 | No complete named quest path established in this audit | Implement verified multi-stage hunt and completion claim. |
| Dis City | 11002–11005 | Minimal `DisCity` state stub; disabled scheduling | Reconstruct the selected edition and prove what each of the four achievements means. |
| Power Arena | 11013, 11014 | Seven-stage NPC code exists; legacy schedule is parked; custom CP/RVP rewards differ | Restore lifecycle and verified floor/badge progression. |
| Moon Box | 11022 | Item consumption exists in promotion code; genuine quest completion unproven | Implement the tactics/token chain and reward from its final NPC. |
| Sky Pass | 11019 | Complete reachable chain unproven | Build and test floor transitions, tokens and final claim. |
| Snake Islands | 11018 | Complete route and achievement attribution unproven | Validate islands, jumpable route, Snake King ownership and completion semantics. |
| Labyrinth | 11027 | Simon dialogue and entry code exist; initial information option is commented; advertised rewards differ from Ancestor Box requirement | Audit all four floors and implement the correct terminal exchange. |
| Daily soul-release route | 11012 | Daily Quest Envoy has an existing soul-release branch | Trace the actual completion transaction and connect the matching achievement. |
| Elimination / Dragon Slayer | 11010 / 10932 | Full completion paths unproven | Identify the exact quests and soul reward source before mapping IDs. |
| Newbie / equipment bonus chains | 10930 / 10931 | Aggregate achievements require many underlying quests | Inventory every prerequisite, including class alternatives and already-promoted characters. |
| First / second rebirth quests | 11030 / 11031 | Character rebirth alone does not establish completion of the named quest | Separate quest-earned completion from shortcut services unless the exact rule supports both. |
| Quiz / CTF / arena / Couple PK | 10120; 10230, 10301; 11032; 11115, 11121 | Some underlying legacy systems exist | Award from answer, objective, participation, and final-result records respectively. |
| Flame Lit / Ancient Devil | 11016 / 11020 | Exact progression and grants not established | Research and restore their item/objective chains after the first batch. |

Historical mechanics that are sufficiently documented to guide a first implementation:

- **Demon Exterminators:** the official guide describes a level-120 daily quest through Cloud Saint, four hunts of 600, 1,200, 1,500 and 1,800 monsters, team kill sharing and exclusions for guard kills. That is 5,100 kills, not a generic single bounty. A shortened Revora edition would be a deliberate adjustment.[^demon]
- **Moon Box:** collect six distinct tactic tokens, exchange them for Soul Jade, then complete the Life Tactic route for the final NPC reward. Buying a Moon Box is not evidence of this achievement. Paid shortcuts and escape routes require separate eligibility checks.[^moon]
- **Power Arena:** seven stages, guardian requirements, Agate transitions, defeat penalties and Refinery Badge exchanges form the original loop. The documented pack costs are 2, 6 and 12 badges. Existing CP/RVP payouts do not by themselves reproduce it.[^power]
- **Sky Pass:** the official route spans five floors with combat/token decisions and a final reward limited to once daily. Its undated page has inconsistent level presentation, so level gates and rewards need client-era reconciliation.[^sky]
- **Snake Islands:** the guide gives a level-90 entry, island traversal and a Snake King objective. Preserve the movement challenge and verify traversable coordinates against Revora's maps.[^snake]
- **Dis City and Labyrinth:** official pages provide multi-stage dungeon structures, but the current Dis City guide has conflicting location text and the Labyrinth guide describes an updated edition. They are research leads, not proof that the four Dis City IDs mean four floors or that an arbitrary Labyrinth reward satisfies Ancestor Box.[^dis][^lab]

Reuse the existing quest-list integration where it is proven: `QuestListSystem` currently registers Path Hunt, Courier Loop, Soul Trial and Daily Envoy. `DailyQuestEnvoySystem` already exposes hunts, Spirit Bead and other routes. A new quest should have persistent acceptance, objectives and final claim, not merely a teleport NPC and a completion bit. [L5]

## 5. Best first cosmetic season: Horn of Summer

The installed client links **Emerald, Orange, Flame and Bright Glow Wings**, plus **Siren Song**, to Horn of Summer. The official July 2017 summer exchange documents permanent bound crafting: **30 Glow fragments** produce a random color; **99 Siren fragments** produce Siren Song. It also documents a ten-fragment rainbow wand and wing unbinding materials. Material sources included daily-quest tokens, racing, hunting and tournament participation.[^summer]

**Proposed Revora edition:** retain the documented wing recipes, offer bounded daily material quests, and show exact progress at one summer artisan. Allow a PvE route and optional PvP progress so this collection is not restricted to tournament champions. Use a guaranteed daily contribution alongside optional random bonuses; publish all caps and odds. Final fragment earning rates, event duration, dyes and tradability belong in the content contract before implementation.

The historical event also gave material cards when opening an Outstanding Exploit Pack.[^summer] Revora's Potency hunt intentionally has a high-volume source of that item. Carrying over the old conversion automatically would create an uncontrolled wing-material faucet. Keep existing Potency packs separate unless a bounded conversion is explicitly designed and measured. [L6]

This season can unlock five currently locked entries without building a cross-server service. It also provides a reusable crafting and durable cosmetic award path. Existing client wing entries are present, but fragment IDs, item-use behavior, acquisition UI and visual verification still need an exact client/server audit.

## 6. Other historical reward families

### Holy Grail: a long-term guild objective

The client requires twelve-cup Holy Grail crafting for **Invincible** and
**Supreme**. The subsequent [installed catalog and seasonal research](../HOLY_GRAIL_RESEARCH.md)
resolves the item chain with documented confidence limits, and the installed
6711 statistics corroborate the official reward values.[^grail]
[M7.3](../HOLY_GRAIL_CRAFTING.md) now implements the earned twelve-trophy recipe
and both permanent wardrobe receipts. Its ordinary-player fixture earns all
materials through scheduled battles, exchanges and rollovers before crafting.
Legacy loose trophies remain ineligible. [M7.2e](../GUILD_WAR_PURSES.md) now
provides atomic regular Sunday rank purses. Native full-route presentation remains open.

### Returning heroes

The installed four titles distinguish returnees by VIP tier: Earth Knight 0, Sky Knight 1–4, Glory Knight 5–6, and Paladin 7. An official June 2016 mailing describes returning-player titles and a Guide/Traveler campaign.[^return]

VIP alone is insufficient. Record eligible historical absence, first return in the campaign and a once-only claim. Capture eligibility before updating last-login timestamps. [M8.1a-b](../HOMECOMING_RESEARCH.md) recover the separate May campaign's dated eligibility notice and repair activity persistence. The June campaign's exact absence cutoff and title duration remain unverified. A proposed rolling inactivity period would be a Revora rule, not an official fact. An active VIP must not qualify merely by logging in again.

### Mr./Ms. Conquer and Romance wings

The 2016 final used separate flower categories; the Tulip winners received permanent wings while the Orchid category awarded permanent titles.[^mr2016] That supports **Fairy Wings** and an election system, not the current source's Mr./Ms. map-alive counters. Client placements establish Fairy/Goddess and Scholar/Handsome, but Beauty and Wise lack a usable threshold.

The 2017 guide describes registration, local and all-server stages, flower voting, and a real-photo review process. It documents year-scale titles and tier-dependent Romance-wing durations, including a permanent first-place award.[^mr2017] This is a separate edition from 2016; shared-looking rewards must retain source and duration.

For Revora, an in-world portrait/character contest could avoid operating a real-photo competition, but that is an explicit adaptation. It needs a published scoring contract, vote transactions, eligibility, deterministic ties and finalized standings. Keep public NPC dialogue brief; put detailed rules on an optional page. Do not grant the all-server champion titles from a single local election without clearly deciding and documenting changed semantics.

### Cross-server CTF, Elite PK and Team PK

Together these account for **ten locked entries**. The installed requirements explicitly identify inter-server competitions. The official CTF guide contains multiple event descriptions with differing admission limits; freeze one edition rather than combining the easiest rules.[^csctf]

The October 2016 Elite PK Q&A confirms cross-server level divisions and rewards, but describes a fourteen-day **halo**. It does not establish the lifetime of the four separate wardrobe titles in this client.[^cselite] Their placements are confirmed locally; duration remains open.

The dated 2018 Team PK event specifies five-on-five matches, level divisions, entry tickets and fourteen-day King/Dominator/Power/Elite Team titles in the highest division. Its packs were delivered to team leaders for distribution.[^csteam] Revora should preserve each eligible roster member's entitlement; direct claims would be an explicitly documented delivery improvement rather than a rule taken from that guide.

A real implementation needs at least distinct origin realms, a tournament coordinator, collision-free character/guild identities, authenticated admission, controlled character state, durable results and recoverable return/claim flows. Two maps inside one realm, or adversaries supplied by adventurer AI, do not establish a cross-server win. This audit found commented cross-client infrastructure, not a functioning inter-realm service. [L4]

### Poker, fashion, Euro and Couples vs. Singles

**Poker:** the client specifies first, second, third and Top 30 placements. The 2017 official Cash Tournament used a dedicated cross-server tournament entry, an in-game silver fee and mailed rewards.[^poker] Existing table handling is not evidence of a coordinator, elimination standings and durable tournament claims. Keep this later than quest/CTF work. A silver-only Revora competition would need an explicit edition contract; reproducing the historical real-money promotion is not part of this proposal.

**Fashion Icon:** first/second/third are locally confirmed, but this research did not establish authoritative scoring, season duration or title expiry. Generic garment-making festivals are not evidence for Fashion Icon Competition. Audit the exact event before awarding these three titles.

**Euro 2016:** Big Fan and Euro Collector already have collection expressions; their items' ordinary-player acquisition routes still need verification. Planet wings have only an event association locally. Official 2016 promotional material confirms the football campaign and Fan Points, but not the exact Planet reward threshold.[^return] Do not assume owning the six jerseys grants Planet wings.

**Couples vs. Singles:** the March 2018 announcement describes choosing a faction and earning Heart wings through support.[^heart] It is distinct from Couple PK. Exact contribution thresholds and duration remain unresolved. The existing couple arena cannot be used as a substitute proof.

**Honored Hero:** its installed tooltip repeats the name and does not identify a requirement. This research did not confirm the correct source. A matching phrase in a newer CO Origin promotion would not establish the identity of reward `20572057`.

## 7. What the 29 existing expressions still need

Progression thresholds, current Emperor status and collection ownership are already evaluated. The matrix labels these **implemented expression; acquisition not end-to-end verified**, rather than saying all are earnable. Inspect the normal-player supply of the Euro designs, permanent monkey variants, Celestial Fox variants, basketball uniforms and romance collection items. A catalog item with no legal gameplay source is still a content gap.

Preserve distinctions already handled by the code: unique designs versus duplicate instances, temporary versus permanent monkeys, expired items, one equipped rune set, and current guild/kingdom leadership. Keep the two identically named Scholar entries separate: prestige Scholar is `20272027`; Mr. Conquer Scholar is `20232023`.

New rank titles, permanent crafted unlocks and current-condition titles have different lifetimes. The current five-byte title record holds identity and equipped state, not event provenance or expiry. Add durable award evidence and teach eligibility to read it; preserve the existing storage compatibility and backups. [L2]

## 8. Player experience and scheduling recommendations

Use one concise quest/event directory with available activities, next opening and personal progress. Individual NPCs should give a short in-world invitation and actions such as **Enter**, **My progress**, and **Claim**. Example original copy: “The dead stir beneath the city. Gather your companions; I will open the way.” The quest tracker can carry objectives without a long introductory speech.

Do not add every researched event to the hourly calendar. Current source already schedules LMS on even hours, Relic Rush on selected odd hours, alternating half-hour PvE trials, hourly Boss Rush, daily races/duels, Extraction and four daily world bosses. Most listed events use host-local `DateTime.Now`; Extraction uses UTC. [L6]

Proposed sequence: normalize scheduling to explicit server time with UTC persistence, protect one major weekly tournament window, then run one cosmetic season alongside a small permanent quest selection. Before choosing displayed times, account for registration and fight duration, daily bosses' two-hour lifetimes, and daylight-saving changes. Existing maps and timers should be reused only after checking their ownership and concurrent-instance behavior.

Favor cosmetic progress and bound quest materials in the first season. Tune XP, CPs, RVPs and tradeable rewards separately against current Revora supply. The historical shop price of a cosmetic or a late-edition reward table does not determine a fair rate for this server.

## 9. Remaining evidence and decision gates

| Gate | Evidence or decision needed | Safe planning outcome |
| --- | --- | --- |
| Rising Star reachability | Per-character route through 320 of 337 legitimate IDs, including class changes and historical exclusions | Do not promise completion until this is calculated. |
| Dis City achievements | Meaning of each of 11002–11005 in the matching client edition | Do not assign all four to one boss or assume four floors. |
| Holy Grail | Exact Cup/Grail IDs, acquisition edition and ownership behavior | Keep two grants tied to verified crafting. |
| Historical campaigns | Beauty/Wise criteria; return cutoff/expiry; Fashion scoring; Planet and Heart thresholds; Honored Hero source | Keep uncertainty visible and avoid speculative grants. |
| Cross-server rollout | Distinct origin realms and desired event edition | Separate infrastructure milestone from local event restoration. |
| Seasonal tuning | Duration, caps, fragment rates, binding and dye policy | Publish an explicit Revora edition if changed. |
| Existing collection items | Actual ordinary-player sources and permanent item variants | Fill acquisition gaps without bypassing ownership conditions. |
| Existing player migration | Recoverable completion evidence and policy for unsupported old flags | Preserve original records and report eligibility changes before rollout. |

These gates do not prevent the plan or foundational implementation. They prevent presenting unverified historical rules as completed authentic features.

## Local evidence register

Paths below are relative to this repository unless identified as client evidence. Line numbers refer to baseline `23c520b` and can move later.

| ID | Source | What it establishes |
| --- | --- | --- |
| L1 | Installed client `6711/ini/Storage.ini`, `title_type.dat`, `Achievement.dat` | Wardrobe identities, visible requirements and achievement catalog. |
| L2 | [TitleRequirements.cs](../../Sistemas/WardRobe/TitleRequirements.cs), [StorageManager.cs](../../Sistemas/WardRobe/StorageManager.cs), [existing wardrobe notes](../WARDROBE_REQUIREMENTS.md) | 29 expressions, 72 keys, reconciliation, persistence and existing validation scope. |
| L3 | [Achievement.cs](../../Network/GamePackets/Achievement.cs), especially `CompletedCount`, `CreateFlaID`, `Process`; admin path in `Network/GamePackets/Chat.cs` | Current bit counting and ingestion behavior. |
| L4 | [Thread.cs](../../Thread.cs) at 3478 onward and 2169; [PacketHandler.cs](../../Network/PacketHandler.cs) at 2422; `Game/Features/Tournaments/CaptureTheFlag.cs`; `Game/ElitePKTournament.cs`; `Npcs.cs` around 19168; `Game/Features/Tournaments/ClassPK.cs`; `Game/ConquerStructures/CheckAlive.cs`; `Sistemas/CrossClient/CrossClient.cs` | Parked event lifecycle and partial existing competition code. |
| L5 | [QuestListSystem.cs](../../Game/Features/QuestListSystem.cs), [DailyQuestEnvoySystem.cs](../../Game/Features/DailyQuestEnvoySystem.cs), [DisCity.cs](../../Game/Features/Event/DisCity.cs); `Npcs.cs` around 2522 and 18183 | Existing quest integration, partial dungeon routes and disabled options. |
| L6 | [Potency hunt](../POTENCY_HUNT.md), [daily bosses](../DAILY_WORLD_BOSSES.md), [event improvements](../EVENT_IMPROVEMENTS.md), [daily quest ideas](../DAILY_QUEST_IDEAS.md); current `Game/Features/*System.cs` and `ExpeditionEvent.cs` | Revora economy context, reusable activities and schedule conflicts. Current source takes precedence over older summaries. |

Client fingerprints (SHA-256):

| Evidence | SHA-256 |
| --- | --- |
| `Storage.ini` | `00490ada2256a196dbd0ffb645fd0ce7b9be51069aad57d1346aec9a44792db9` |
| `Achievement.dat`, 33,345 bytes | `df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc` |
| Decoded achievement evidence | `3eccec105fdba5e740ef488de137ad9894f8bfabf58cebc536373d88a3cbfcb1` |

The prior wardrobe protocol notes record the `title_type.dat` evidence hash; its decoding was not repeated for this task.

Achievement decoding reused the transform from Revora's existing `tools/ClientPatches/prepare_shop_buy_hotfix.py`, with seed `0x2537`; only that transform was evaluated. Re-encrypting the decoded bytes reproduced the original input exactly. The private decoded evidence remains under the ignored parent `research/isolated/wardrobe-quests-6711/`. No client file, raw decrypted catalog, proprietary binary, database dump or player record accompanies this report.

Research validation: the CSV's 72 unique keys match both installed `Storage.ini` and server `StorageManager` exactly; 29 have expressions and 43 do not. All 337 achievement IDs map to distinct valid positions in the thirteen-word format. The three recorded file hashes and decryption round-trip match. Local document links and all 19 source footnote definitions were checked. This documentation-only task did not run a server build, change gameplay, or perform a live acquisition test.

## Official sources

All links were researched for this task on 14–15 September 2026. Dates describe historical editions, not current Revora settings. Undated pages are explicitly marked. Sources establish gameplay facts only; implementation recommendations are this report's proposals.

[^wardrobe]: [Wardrobe guide](https://co.99.com/guide/quest/2016/wardrobe.shtml), 2016 route, publication date not printed. Core collection, achievement and tournament reward associations; old achievement-count note is not the installed catalog size.
[^elite]: [Elite PK Tournament](https://co.99.com/guide/quests/elite_pk.shtml), undated and mixed bracket descriptions. Use with version reconciliation.
[^demon]: [Demon Exterminators](https://co.99.com/guide/quest/demonexterminators.shtml), undated historical guide. Daily hunt structure and counts.
[^moon]: [Moon Box](https://co.99.com/guide/quests/moonbox.shtml), undated guide. Tactics, token chain and terminal claim.
[^power]: [Power Arena](https://co.99.com/guide/quests/powerarena.shtml), undated 2.0-server guide. Seven stages and badge exchange.
[^sky]: [Sky Pass](https://co.99.com/guide/quests/skypass.shtml), undated guide with mixed introductory text. Floor route and reward limit.
[^snake]: [Snake Islands](https://co.99.com/guide/quests/snakeislands.shtml), undated guide. Entry and traversal objective.
[^dis]: [Dis City](https://co.99.com/guide/quests/monstercity.shtml), undated/currently available edition. Version and coordinate conflicts remain.
[^lab]: [Labyrinth](https://co.99.com/guide/quests/labyrinth.shtml), undated updated dungeon guide. Not sufficient alone to map Ancestor Box.
[^summer]: [July 2017 Cross-Server Team PK and summer exchanges](https://co.99.com/guide/event/2017/july/cross-server_team_pk_tournament/index.shtml), exchange window July 20–August 2, 2017. Wing recipes and material ecosystem.
[^grail]: [22nd Super Guild War](https://co.99.com/news/2019-05-21/22nd_super_guild_war_on_05/25-06/22.shtml), displayed May 23, 2019. Later guild-season and Grail corroboration; not a verified 6711 item mapping.
[^return]: [Official June 2016 mail archive](https://co.99.com/comail/list_304_3.html), entries June 6 and June 13–14, 2016. Surviving Homecoming and Euro summaries; the linked Homecoming landing page currently redirects to an error. Treat this as partial evidence.
[^mr2016]: [2016 Mr./Ms. Conquer final round](https://co.99.com/news/2016-03-14/mr_ms_co_third_round.shtml), March 14, 2016. Flower categories and permanent final rewards.
[^mr2017]: [Mr. & Ms. Conquer 2017 guide](https://co.99.com/guide/event/mr____ms__conquer_2017_guide.shtml), February 16–March 3, 2017 edition. Election stages and time-limited versus permanent prizes.
[^csctf]: [Cross-server CTF guide](https://co.99.com/guide/event/2013/cross-server-ctf.shtml), 2013 route with multiple rule descriptions. Admission limits require edition selection.
[^cselite]: [Q&A about Cross-Server Elite PK](https://co.99.com/news/2016-10-13/q_a_about_cross-server_elite_pk.shtml), October 13, 2016. Halos are not interchangeable evidence for wardrobe-title expiry.
[^csteam]: [Cross-Server Team PK, June–July 2018](https://co.99.com/news/2018-06-21/cross-server_team_pk_tournaments_on_06/21-07/19.shtml), displayed June 28, 2018. Team size, brackets, ticketing and fourteen-day titles.
[^poker]: [2017 Poker Cash Tournament](https://co.99.com/guide/event/2017/tournament/cash.html), September 22, 2017 event. Tournament entry and reward delivery, not proof of a working Revora implementation.
[^heart]: [The War Between Single and CP](https://co.99.com/news/2018-03-01/the_war_between_single_and_cp_will_start_.shtml), March 1, 2018. Faction campaign association with Heart wings; exact eligibility incomplete.
