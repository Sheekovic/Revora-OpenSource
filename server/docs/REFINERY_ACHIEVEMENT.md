# First refinery achievement and effect persistence

Reviewed 2026-09-25. Installed Achievement.dat identifies 10431 as first refinery;
10501 and 10502 separately concern Dragon Souls and are not inferred from this
operation. Catalog SHA-256:
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.

RefineryProgress.Apply now records 10431 in the same transaction as the checked
effect replacement, target update and material consumption. Success refreshes
achievements after commit. Stabilizing, expiring, loading or merely owning an
effect does not independently award first-application credit. Existing material
compatibility, binding, duration, stabilization prices and effects are preserved.

The prerequisite audit found unchecked itemadding writes. Save now verifies both
the expected deletion count and every effect insert. A suppressed write cannot
silently consume material or replace the target's public effect without its saved
state. Saved item ownership also rejects wardrobe state and changed locks;
material eligibility rejects a wardrobe-marked cache object.

Isolated Debug/x86 build, 191 refinery checks and five producer audits pass.
Coverage includes ordinary application, concurrent replay, effect/purse-independent
material consumption, temporary/permanent replacement and stabilization behavior,
production inventory effect loading, suppressed effect insert/deletion and
achievement insertion, stale saved wardrobe/lock state, and a fresh achievement
object reading durable completion. The latter fault case uses the suite's existing
fence reset; it is not a full character reconnect test. Gear and materials are
supplied fixtures, including pack-opening coverage, not an independently earned
refinery-material route.

The initial batch left earned target/material acquisition and full character
reload open. The combined server route below now covers those checks.
Native application/stabilization UI and live deployment remain pending. No release
or live database mutation occurred; unrelated owner edits were in the isolated
assembly. Other equipment transactions and Dragon Soul achievements remain open.

## Earned Terato material and character reload

The Grotto/Terato suite now connects the existing seven-ghoul hunt and Dragon
Pill crafting route to the admitted killer's real Superior Refinery Pack pickup,
full character/item reload, pack opening, actual refinery request, and another
full character/item/achievement reload. The initial 305 Grotto checks passed against the
existing isolated assembly. The saved effect is level four; the consumed pack
and material remain absent, achievement 10431 has exactly one durable receipt,
and stale requests cannot duplicate either reward.

The blade target is explicitly supplied by this fixture. This proves earned
material acquisition and reload, not independent target acquisition or the full
combined milestone. Boss lethal damage, loot and pack draws remain controlled
fixture inputs. The suite does not prove native combat/UI acceptance. This batch
changes tests and documentation only; no additional production build or live
mutation was needed.

## Combined earned target and material route

The subsequent 308-check Grotto run replaces the supplied blade with a Pickaxe
purchased from the installed shop for 1,000 silver. The same ordinary Terato
killer defeats a scheduled daily boss through ReceiveAttack, verifies the saved
paid victory and exact 10,000,000-silver increase, then buys its target through
HandleBuyFromNPC. The Superior pack draw selects an installed level-four material
compatible with that purchased target; no new catalog entries or economy rules
are introduced. Application, effect persistence, achievement 10431, consumed-item
absence and stale-request rejection pass after full character and item reload.

This closes the combined server-side acquisition/application/reload check for one
compatible target/material pair. Boss damage, schedule and random draws remain
controlled; the test does not establish combat balance, natural odds, all target
families or native UI acceptance. Original player qualification is fixture setup.
Native/live gates remain open. Production source is unchanged in this batch.
