# Earned Dragon Soul application and stabilization

Validated 2026-09-25 against installed shops, NPC placement, item catalog and the
existing ordinary-player Grotto/Terato encounter suite. This is isolated server
acceptance, not native client acceptance.

The combined route now performs the seven-ghoul hunt and Dragon Pill crafting,
owned Terato combat, and actual soul 800001 pickup. The same admitted killer earns
daily-boss silver/CPs, visits installed weapon shop 10015 on its saved map, buys
level-120 blade 410233, and purchases three Meteors plus one ten-point stabilization
stone from installed CP mall 2888. Actual application and stabilization handlers
consume those earned/purchased items and award achievements 10501 and 10502.

Full EntityTable character loading and ConquerItemTable item loading are exercised
between application and stabilization and again afterward. The temporary effect,
permanent effect, exact two achievement receipts and paid silver/CP balances
survive reload; consumed items remain absent. Replayed spent-material packets do
not charge again or duplicate progress. Qualified characters, combat lethal damage,
schedule, route movement and loot draw remain controlled fixtures. This is one
complete server route, not proof of every phase/class, natural odds, native UI,
visuals or combat balance.

The route exposed a genuine shop prerequisite: the legacy silver purchase saved
non-stackable equipment with stack size zero, then changed the cache to one, and
left its silver debit unflushed until later character saving. Core silver equipment
(slots 1?6 and 8) now uses the existing WeaponBookShop transaction with installed
stock/NPC/price checks, exact purse preimage, correct single-item stack, bounded
quantity, replay protection and atomic item/debit/receipt persistence. The legacy
non-stackable branch also initializes stack size before persisting new items.
This is not a certification of all remaining generic shop paths or old zero-stack
inventory. No live inventory migration was performed.

Validation: isolated Debug/x86 build and 320 Grotto checks pass, including an
injected weapon-insert failure preserving purses and purchase receipts across
real reload, followed by concurrent purchase producing exactly one charge/item.
Unrelated owner edits were in the isolated build. No live deployment, database
migration or client patch occurred. Native artifact acceptance remains open.

## Retained P6 shop route and process restarts (2026-10-02)

Ordinary character4294907001 continues from the retained accessory checkpoint.
Stone City deposits make room without deleting possessions. Installed weapon
shop10015 sells FrostBlade410233 for100,000 silver. Ordinary paid Meteor upgrades
raise that same UID from level120 to130; successful draws and upgrade cooldowns
are controlled. P6SoulMerchant91010 then sells unbound TombBlade800000 for exactly
2,500 CPs. MillionaireLee11275 option1 packs purchased Meteors into three scrolls,
and mall2888 supplies two distinct BigPermanentStone723695 items at3,950 CPs each.
No soul, target, payment item or permanent effect is seeded.

A complete game/database restart separates preparation from application. The2076
handler consumes the soul and exactly30 Meteor units and creates its phase6 timed
effect. Another restart restores that temporary effect before the stabilization
handler consumes both100-point stones and makes it permanent. A final full restart
preserves the exact target UID, soul identity, permanent effect, currencies,
packing records, achievements10501/10502 and nested earlier career. Spent-soul and
spent-stone replay packets change nothing. The prior career's mixed-provenance
funding limitation still applies; this is not independent natural currency earning.

All `career-retained-artifact-{prepare,apply,stabilize,reload}` phases pass on
unchanged private candidate SHA-256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
Checkpoint `revora_retained_artifact_probe` is step3. The cleaned final helper,
with its one-time recovery removed, also passes cold reload.

The first preparation exposed a private-runtime staging omission:
`database/Slayer/souls_protection.txt` was absent, so the bought soul failed the
compatibility check. The installed source file exists under
`bin/Debug/database/Slayer/souls_protection.txt`; its11,884 bytes hash to
`B06C415252EA26D72F5229B77FDB4B7896636D1813C0A0A70BCCA863FA26FFD9`.
The stopped private runtime received that exact file and its hash-manifest entry.
`prepare_moon_box_runtime.py` now requires it for future staging. No catalog data
was committed to Git, no production compatibility rule was relaxed and no live
runtime was changed. The interrupted purchase was retained after a full snapshot
comparison proved exactly one unbound soul addition and its2,500-CP debit; a
one-time exact-hash guard adopted its UID without refund or duplicate purchase.

NPC approach, calendar and successful upgrade draws are fixture controls. The
application clock is in the retained future calendar; this checks saved state and
payment, not real-time expiry at the boundary. Native packet/UI/visual acceptance,
natural upgrade odds, other artifact families and live release remain separate.
