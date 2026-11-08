# Dis City restoration evidence

2026-09-15, source baseline `96af481`. Stage identities resolved from additional
installed client evidence. The [combat route checkpoint](DIS_CITY_DUNGEON.md)
now implements the four stage earning paths. The [treasury checkpoint](DIS_CITY_TREASURY.md)
adds issued weekly keys and private openings; remaining exchange/rank rewards,
difficulty variants and native acceptance remain pending.

The earlier audit extracted each achievement's visible description but omitted
its icon identifier. The four descriptions only number the quest; the identifiers
also name the stages. Reading the bounded original records resolves the missing
content association:

| Achievement | Icon identifier | Matching mission | Terminal stage |
| --- | --- | --- | --- |
| 11002 | `Achievement_PassTheNetWoDoor1` | 6472 | Hell Gate |
| 11003 | `Achievement_PassTheNetWoHall2` | 6473 | Hell Hall |
| 11004 | `Achievement_PassTheNetWoClois3` | 6474 | Hell Cloister |
| 11005 | `Achievement_PasTheNeWoMagicFr4` | 6475 | Battle Formation |

Evidence: installed `Achievement.dat`, SHA-256
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`,
and `Questinfo.ini`, SHA-256
`f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14`.
Confidence: strong client-content association, corroborated by matching ordered
mission names and objectives. This is not an observed native achievement trigger
or a new packet-layout claim. Record each stage only after its own genuine final
objective; never grant all four from a single boss death or generic map visit.

The [current official Dis City guide](https://co.99.com/guide/quests/monstercity.shtml)
matches the installed four-stage edition: five Soul Stones, class-dependent
Slayer Points, nine ordered guarded crystals, then token fragments and a summoned
Pluto. It also describes a treasury and weekly completion limit. Its NPC location
wording conflicts internally; installed mission location links include stale
coordinates. Validate actual landings rather than copying those links.

## Server terrain and content

Read-only database inspection found Solar Saint 3215, mesh 1310, at Twin City
(327,250). Its current dialogue explicitly closes an old Perfection adaptation.
That adaptation's retired schedules and purse handlers must not be reactivated.

The stored map metadata requires care: IDs 2021-2025 have house mapdoc aliases
1500/1501, while the Dis City adaptation uses 4021-4025 with mapdocs 2021-2024.
The `.map` filenames below contain the real dungeon terrain. A new owned private
map must bind the reviewed floor and intended native mapdoc explicitly; do not
change shared house metadata or assume the existing map ID implies its display.
The dynamic `Map` constructor prefers a matching base-ID `.map` file over its
fallback path. Its NPC/monster loaders use the dynamic ID, so owned actors must
be installed explicitly.

| Floor file | Dimensions | Walkable tiles | SHA-256 |
| --- | --- | ---: | --- |
| 2021.map | 550 x 550 | 104655 | `e9bb085858ac547b70136f843f94449b97bbe9a547a4b9ada6e512df294e5baf` |
| 2022.map | 424 x 424 | 46445 | `0e47937c0537fad3c67497d0a017af88ef145aa159e0702aa4cbfcd9e7c893b9` |
| 2023.map | 676 x 676 | 43858 | `398115067c61112ae6ff11427a73a68e6c46f9490e6ede42d1311bb5dab34443` |
| 2024.map | 302 x 302 | 13238 | `eee895cf2a93c8b1c3f12a04461556ba46b0d3bf9e7117b0dd8e77cf8cc0ce56` |

The walkable column was corrected at `d34599b`: the earlier numbers counted
blocked bytes. `Map` treats byte 1 as blocked. File hashes and the production
floor traversal tests are unchanged by this documentation correction.

Existing monster identities include Wraith 5055, Syren 5058 and the renamed
PlutoCentro 5059. Existing spawns use 4021 for gate creatures, 4023/4024 for
wraiths, and 4025 for Syren. Names alone do not certify the renamed boss's full
stat/loot policy. Current 4022 has no matching spawn rows in the bounded query.
Private owned actors and tested travel are required for a complete restored route.

## Next implementation contract

Use distinct durable stage receipts and owned monster identities. Freeze the
class-dependent Hall quota when admitting the run. Seal the nine-crystal order,
prevent foreign kills and stale requests from advancing it, and allow only a
genuine issued summon to produce the final victory. Bound concurrent instances
and clean up abandoned actors. Preserve earned claims across storage failures
and reconnects; define a dated weekly entitlement and terminal replay policy.

Review final item identities, package use and treasury rewards before selecting
the exact Revora reward edition. Test normal non-GM enrollment, production jumps,
all four stage objectives, class changes, death/leave/restart recovery, full bags,
foreign items/kills, duplicate terminal claims and transaction rollback. Genuine
6711 tracker, terrain, NPC and boss-bar acceptance remain separate pending work.

## Reward handler audit at baseline `05668be`, 2026-09-15

Historical prerequisite finding; the refinery and jade portions are superseded
by the implementation checkpoints below.

Installed and server item catalogs agree on 3008057 SuperRefineryPack,
3008058 SacredRefineryPack, 3008059 YinJade and 3008060 YangJade. The active item
handler has no use branches for these four items. Do not issue them as completed
usable rewards yet. The official guide and installed descriptions agree on
random seven-day Super/Sacred materials and 300 Chi for a single jade or 1,200
for a paired use, with a below-8,000 eligibility condition. The precise behavior
when an award crosses 8,000 needs an explicit tested policy.

The existing refinery catalog distinguishes level-five Super and level-six
Sacred material families. Active `ItemHandler.PurifyRefinery` applies timed
`itemadding` effects, ordinarily seven days, extended by the existing VIP rule;
this is distinct from expiring the loose material through `items.days`.
That application path currently removes the previous effect, creates the new
effect and consumes the material through separate writes. It also needs owned
atomic application/binding validation before certifying a Dis City reward route.
Do not infer that adding a seven-day bag expiry repairs refinery application.

Jade use needs an atomic item/Chi transition against `chi.points`, exact cached
balance checks, paired UID ownership and reconnect/replay coverage. Existing
legacy Chi-pill branches change inventory and the in-memory balance separately.
`ChiTable.Save` persists the wallet later, so it is not an adequate transaction
boundary for new dungeon rewards. No reward handler or four-stage dungeon has
been marked complete by this investigation.

## Refinery prerequisite implementation

The [refinery checkpoint](REFINERY_PROGRESS.md) restores owned application,
correct six-tier stabilization costs, retained credit, permanent fallback,
production loading and online expiry. Dis City packs 3008057/3008058 now open
atomically into their selected installed Super/Sacred material pools. The 108
direct checks pass. This does not complete Yin/Yang jade use or any of the four
Dis City stage earning paths. Those remain the next dungeon prerequisites and
implementation work. Mailbox transfer preservation has since been restored;
auction restoration, native acceptance and deployment are still pending.

## Jade prerequisite implementation

The [jade and Chi wallet checkpoint](DIS_CITY_JADES.md) restores single/paired
Yin/Yang use, full rewards below the 8,000 eligibility boundary, atomic item/Chi
consumption, durable Chi refills and wallet save coordination. Its 57 direct
checks pass. The four-stage dungeon and newer Labyrinth jade earning path remain
pending; this prerequisite does not count as a completed stage achievement.

## Four-stage combat implementation

The [four-stage route](DIS_CITY_DUNGEON.md) now has genuine owned objectives,
distinct achievement/reward transactions, run-bound quest items, private terrain,
weekly final entitlement and reconnect/instance recovery. Its 75 direct checks
pass. Earlier prerequisite findings above are historical: the combat earning paths
are now present. Treasury, difficulty variants, exchanges, rankings, coherent
client text and native acceptance remained unfinished at that checkpoint. The
later [treasury batch](DIS_CITY_TREASURY.md) completes keys/openings and matching
private content; difficulty variants, exchanges, rankings and native acceptance
remain open.
