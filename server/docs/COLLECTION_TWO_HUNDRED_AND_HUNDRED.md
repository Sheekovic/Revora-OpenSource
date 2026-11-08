# Combined 200-garment / 100-mount earning journey

2026-10-01. One ordinary non-GM character now earns **200 distinct permanent
garments and 100 distinct permanent mounts** through the reviewed suppliers.
The normal achievement view grants 11231 at 100 garments, 11301 at 200 garments and
11230 at 100 mounts. Both larger tiers remain absent below their thresholds.
Exact items, currencies, earning receipts and milestone receipts survive a
game process restart **and a PostgreSQL restart**.

## Actual route

The private character starts with no CP/RVP collection funding or cosmetics.
Its level, rebirth and VIP eligibility are supplied test inputs. It then:

1. Completes eight Rowan weekend hunts: 800 loaded Birdman kills, 24 owned
   sentries, eight recovered caches and deliveries. Deposits all 19 permanent
   bound Planet gifts through the wardrobe handler.
2. Kills eight scheduled daily bosses. Their saved purses fund ordinary Vesper
   purchases and CP mall purchases; no currency is inserted to bypass payment.
   Vesper costs 72,500 RVPs. The additional mall garments and 17 stocked mount packs
   cost 87,016 CPs in total; pack consumption uses the production handler.
3. Spends 156 of 160 earned expedition credits for the complete exchange stock.
   After all direct suppliers, the same wardrobe contains 198 garments / 92 mounts.
4. Pays for 70 ordinary sign-ins over five controlled months. Opens the actual
   issued daily, weekly and fortnightly packs on their earning dates. Ten
   issued Easter packs supply the final two garments and eight mounts using
   controlled valid reward rolls. No cosmetic, pack or completion is seeded.
5. Deposits the cosmetics and retains all 290 non-cosmetic gifts through normal
   warehouse packets, without deleting surplus rewards to make room.

The final wardrobe contains 300 items and 300 distinct designs. Warehouse counts
are 60 each at IDs 8, 44, 10011, 10012 and 50 at 10027. Loaded NPC 44 is in Twin City,
not the legacy Market map; the helper uses its actual location.

## Persistence and replay evidence

The earning process writes a checkpoint only after its ordinary operations.
The second process starts after private PostgreSQL has stopped and restarted,
runs the actual `Server.Run` initialization, reloads the character through
production character/item/wardrobe readers and compares its exact snapshot.
The comparison includes all item UIDs, types, binding, stacks, blessing,
storage locations, permanent duration, currencies, Planet runs, exchange
claims and the three collection milestone receipts. Memory also matches
the persisted inventory/warehouse/wardrobe rows.

All 156 exchange replay requests and a repeated Planet delivery preserve that
snapshot. Normal achievement packets retain the three expected bits. Final
read-only database inspection confirms:

- Eight Planet completions and eight paid boss victories.
- 70 sign-in claims, 85 sign-in pack openings, 10 Easter openings and 156 exchange
  claims; the helper separately verifies 17 mount-pack openings.
- 300 distinct wardrobe designs and 290 stored non-cosmetic gifts.
- 807,918,829 CPs, 187,500 RVPs, 4,080,100,000 silver and 2,500,000 bound CPs, including
  actual sign-in rewards and the earned Planet/monster baseline.
- Exactly one `wardrobe-permanent-v1` receipt for each of `mounts:100`,
  `garments:100` and `garments:200`.

The run resumed its retained earned state after correcting a malformed purchase
fixture, an unsupported driver return type for a catalog-presence query and
the warehouse 44 map assumption. It did not reset the character, replace earned
rewards or insert completion receipts. Before this character was created, its
reserved UID 4294903001 was verified absent. Existing Kingdom/Supervisor/Spun
fixtures at other UIDs remain retained.

## Reproduction and limits

`tools/CheckCandidateCombinedCollection.cs` adds phases
`collection-combined-earn` and `collection-combined-reload` to
`tools/Invoke-CandidateArenaLifecycle.ps1`. The first phase resumes checkpoints;
it refuses an existing character without its matching owned checkpoint. Use
only the documented loopback candidate 25439 / `revora_candidate_arena_process`.
The wrapper restores private configuration after each phase. PostgreSQL was
stopped after validation; the earned fixture is retained for subsequent work.

Helper compilation treats warnings as errors. Both lifecycle phases pass with
production assembly SHA-256
`102604CE650FAD38027D5FA07EEFAB57D1A0CFB887217AFF9F5D912F6748627D`.
No production code change or rebuild was required. Logs remain ignored under
`obj/collection-combined-*` and the private candidate's matching phase logs.

Controls include eligible character profile, calendars, valid Easter outcomes,
proximity, combat damage and normal monster timer advancement. This establishes
ordinary server-handler earning and cold persistence, not authenticated socket
login, native navigation/rendering, expected real-time collection duration or a
compatible 320-achievement career. The private startup reports 1363 closed because
its terrain is still staged; its separate vote-pulse schema warning also remains.
No live server, client or public patch was deployed. The 200-mount achievement is
still impossible with the reviewed 199-design compatible catalog.

## Same-character expedition continuation (2026-10-01)

The retained collection character now also completes the four installed Forgotten
Realms routes (1360, 1361, 1362 and 1364). Each admission uses Rowan's normal NPC
handler, ten loaded Lost Sentries die through the combat handler, and the Trail
Keeper grants one permanent bound universal rune and returns the character to
Rowan. No hunt, travel or reward receipt is inserted by the fixture.

Phases `collection-realms-earn` and `collection-realms-reload` run full
`Server.Run` initialization against the same retained private database. After
both game and PostgreSQL restart, the second phase compares the exact saved
items, currencies, collection receipts, hunt receipts and travel evidence, then
re-enters each realm and repeats the reward request. All four requests preserve
that snapshot. Achievements 10723, 10724, 10725 and 10727 appear in the normal
achievement view. All original collection items, 156 exchange claims, eight
Planet completions and collection milestone receipts remain intact.

The closed 1363 entrance refuses entry without creating a hunt or achievement
10726. Its release remains pending. Both phases pass with the same production
assembly hash above; the helper compiles with warnings as errors. No production
rebuild, live restart or release was necessary. Private PostgreSQL is stopped
after validation, and the other retained Kingdom/Supervisor characters remain.

The new checkpoint `revora_collection_realms_probe` preserves the continued
character's snapshot. The earlier collection checkpoint stays historical: its
whole-inventory snapshot intentionally predates the four additional runes.
Use the new reload phase to verify the continued state; do not reset the
character to make the earlier whole-inventory assertion pass.

Read-only inspection finds 18 saved achievement identities on this character.
That is not 18 independently proven career steps: some predicates observe the
supplied level/eligibility profile. This continuation establishes coexistence of
the nine collection milestones and four earned travel milestones, with their
actual rewards and restart recovery. It does not establish the remaining career.
Calendar, within-map proximity and combat damage are controlled; socket login,
native travel/rendering and real-time difficulty remain untested here.

## Shop-bought sword mastery on the same character (2026-10-01)

The retained character purchases sword410301 from the loaded Twin City
weaponsmith using its earned silver and equips it through the normal item
handler. Production melee then reaches proficiency410 level12, with71185 XP,
after2682 attacks. The fixture does not set damage, monster HP, weapon metadata,
proficiency XP/levels or XP rates. Only proximity and attack cooldowns are
controlled. Achievement10323 is recorded once and appears in the achievement
packet. The supplied character profile remains a test prerequisite.

`collection-sword-earn` and `collection-sword-reload` pass full game/database
restart with the same production assembly hash above. Recovery compares the
exact saved items, purse and earlier receipts, separately checks the saved
proficiency level/XP, and verifies the achievement view. An additional reload
checks that every pre-training item and all prior expedition, exchange and
Planet receipts survive unchanged. No character reset or evidence insertion is
used. The latest whole-inventory checkpoint is `revora_collection_sword_probe`;
the previous two snapshots remain historical. Private PostgreSQL is stopped.

There are now19 saved achievement identities, of which this composed journey
establishes nine collection, four travel and one combat mastery milestone.
That is **14 coexisting route milestones**, not a320-achievement certificate.

The class-transition review found all three rite implementations call
`AchievementCatalog.PreserveMasteries` inside their reset transaction. Existing
`CheckCandidateMasteryLifecycle` covers reset/rollback/reload with supplied
proficiency inputs; that is not an earned multi-class combat journey. Read-only
inspection of this collection fixture finds level140/class15/rebirth2 but zero
first/second rebirth class and level history, consistent with its documented
supplied eligibility. `ReincarnationRules.Profile` correctly refuses that
incomplete history. Do not patch the fixture's history and describe it as earned:
a complete multi-class career still needs actual first/second rite history and
ordinary recovery before class switching. The collection/sword evidence remains
valid within its stated profile controls. Native acceptance and deployment remain
pending.
