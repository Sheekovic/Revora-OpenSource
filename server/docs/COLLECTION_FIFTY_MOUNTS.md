# Fifty permanent mounts earned by one character

2026-09-30. Private earning and restart validation passes. Native acceptance
and live deployment remain pending.

| Ordinary route | New mount designs |
| --- | ---: |
| Eight Planet expeditions | 9 |
| Vesper purchases, excluding two already earned from Planet | 18 |
| Seventeen stocked CP mount packs | 17 |
| Six Easter packs issued by sign-in milestones | 6 |
| Total distinct permanent mounts | **50** |

Run `tools/Verify-ArenaProcess.ps1 -CollectionOnly`. One saved non-GM character
finishes eight Planet expeditions through Rowan, 800 loaded Birdman kills, 24
sentry defeats and eight cache deliveries. The same character earns three daily
boss purses, buys the 18 additional Vesper mounts for 45,000 RVP and all 17 stocked
mount packs for 47,583 CP, then opens and deposits those rewards.

Fourteen normal sign-ins per controlled month, over three months, issue six
Easter packs through their seven- and fourteen-day milestone packs. Each daily
and milestone pack is opened on its earning date. Controlled Easter choices
produce 200481, 200499, 200500, 200543, 200563 and 200570. These are actual issued
packs consumed by the owned handlers, not seeded reward items. Four concurrent
normal requests per day charge once. Repeated pack use and wardrobe deposit do
not duplicate a consumed item or design.

The fixture does **not** promise six desired mounts in six random rolls to real
players. It selects valid outcomes deterministically to prove this possible
route; ordinary random odds are unchanged. Eligibility, combat damage,
calendar advancement and proximity are fixture controls. Native gameplay and
authenticated TCP are not tested here. This is not the full 320-achievement
career, and it does not establish all 52 potential supplier outputs.

All other permanent sign-in gifts are retained through actual warehouse
requests: 60 in Twin City, 60 in Phoenix City and 54 in Ape City. No timed packs
remain. The wardrobe holds the 50 mounts plus ten Planet garments. Cold reload
matches every item UID, type, binding, stack, blessing and storage location,
as well as currencies and earning receipts. The threshold remains absent at 49
and produces exactly one `wardrobe-permanent-v1` / `mounts:50` receipt at 50.
The normal achievement packet still has bit 11228 set after restart and replay.

Final isolated database inspection confirmed:

- Three paid daily boss victories, 42 sign-in receipts, 51 sign-in pack openings,
  six Easter openings, 17 mount-pack openings and eight Planet completions.
- 60 wardrobe items and 174 stored non-cosmetic rewards.
- 482,957,462 CP, 21,000 RVP, 2,430,100,000 silver and 1,500,000 bound CP.
  Those totals include 5,045 CP and 100,000 silver already earned during Planet;
  the fixture preserves that baseline instead of resetting the character's purse.

All phases passed against executable SHA-256
`99E6736D25B8DE087E95DD8562CB8D26901ABBD884D872E0D57439C809C948D5`.
Probe compilation treats warnings as errors. A fixture compilation correction
and local-date boss scheduling correction resumed the retained earned database;
completed Planet hunts and paid bosses were not replaced by seeded progress.
Logs remain ignored under `obj/collection-*` and the private candidate's
`arena-process-collection-*.log`. The owned clone was removed and private
PostgreSQL stopped after verification. No production files or services changed.
