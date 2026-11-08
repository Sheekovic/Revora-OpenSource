# Revora DeityLand Relics

Deployed to the main production environment 2026-10-05. Native acceptance pending.

Enter through DeityGuide in Twin City (396,344), level 120+, free and no VIP/GM
requirement. Saint Taiyi at DeityLand (1021,1281) identifies earned Relics and
returns players to Twin City. Forty Banshees patrol reachable tiles beyond camp,
with 60,460 HP, 1,200?1,800 attack, 150 defence and a 30-second respawn.

Each legitimate player kill has a 5% chance to award a randomly selected Relic:
Western Mirror, Imperial Tripod Vessel, Beads of Calm Sea, Heartless Seal or Bell
of Doom. Rewards are saved as pending discoveries and identified one at a time.
Full bags and reconnects do not lose discoveries. Claiming inserts the item and
marks its receipt in one transaction, under the player's item/DB locks. No CP,
RVP or P7 prizes are issued by these hunting monsters.

Each Relic is unbound and has three distinct attributes, including its featured
attribute. Revora ranges: HP 300?600, P/M attack 150?300, other supported bonuses
50?100 in the server's existing attribute units. Five family definitions now
exist; previously the dictionary was empty and equipping could throw. Uses the
existing item fields, equipment slot 13, database columns and stat calculation.
Saint Taiyi repairs the equipped Relic to its maximum durability for 1,000 CPs;
full durability is free of charge, item and payment updates are atomic.

These are documented Revora rules, not a claim to reproduce the full original
DeityLand altar, rankings, bosses or crafting system. The original client map
asset and released client route were already present. The missing server route
was restored. Patch 1.0.21 preserves the client route and adds the
separately requested Chi/Jiang boss HUD profiles. No client executable changes.

Validation: build passed; 31 compiled generation/bounds checks; live-main-DB
transaction tests of receipt, attributes, wrong owner and duplicate claim,
all rolled back; provisioning of map/two NPCs/40 connected spawns tested with
rollback; patch ZIP integrity and prior catalog preservation checked. Native
entry, hunting, claim, equipment UI and relog acceptance remain pending.

Deployment: back up bin/Debug/Revora.exe and database/GameMap.dat, gracefully
stop server, run tools/provision_deityland.py --apply with the local protected
DB helper available, copy built server and obj/deityland/server-GameMap.dat,
then restart and publish signed patch 1.0.21. The provisioning script refuses
existing conflicting catalog rows. Preserve pending reward receipts on rollback;
return any players in map 10250 to Twin City before disabling its map route.

Deployment verified: production startup reported relic hunting ready; TCP 9958/9959 listening. Public patch archive hash verified: 13d9b0c80d79d592c681028f775ad0c787f3f2f9cd06611b19f4e55fc21c17b1. News published at https://playrevora.com/en/news/patch-1-0-21 .

## Island gate correction

The first deployment omitted the map's portal routes. Entering a gate reached
GeneralData.UsePortal's unmatched-portal fallback, which returned the player to
Twin City. The production Portals.ini now defines all 32 endpoints as 16
bidirectional crossings within map 10250. Saint Taiyi remains the intended exit.

tools/prepare_deityland_gates.py reads the local DMap's portal coordinates and
prepares an additive Portals.ini in obj/deityland-gates. Adjacent numbered gates
are paired as documented Revora routes inferred from island geometry, not
verified original quest progression. Connectivity checks cover all 16 regions.
Every landing has a clear 3x3 server floor, original DMap access, and distance
greater than four tiles from every gate, avoiding immediate return crossings.
The existing server loader and portal handler are reused; no packet or client
changes are required. Existing routes are preserved byte for byte. Back up the
live Portals.ini before installing the generated file and restart gracefully.
Owner confirmed traversing all island areas after this correction.

## Monster placement correction

The original provisioner mistakenly selected cache byte 1 (blocked terrain).
All 40 configured positions were blocked, so Map.LoadMonsters skipped all of
them. Configuration row counts did not prove a live population. The corrected
shared generator, tools/deityland_spawns.py, uses byte 0 and checks original
DMap access, clear 3x3 surroundings, gate-connected regions and separation from
gate entrances and Saint Taiyi. Forty creatures now cover 15 hunting regions;
the tiny three-gate junction remains clear. A nearby spawn is (984,1284).

tools/repair_deityland_spawns.py defaults to validating and rolling back. With
--apply it backs up the 40 old catalog rows and updates only their coordinates
in one transaction, preserving counts, respawn time, stats and rewards. The
initial provisioner uses the same corrected generator. DeityLand startup now
requires 40 actual registered Banshees and logs the count before declaring the
hunt ready. Validation: placement/terrain checks, transaction rollback and build
passed. Native monster visibility and combat still require player confirmation.
Production restart confirmed `monsters=40` and healthy listeners on 9958/9959.
