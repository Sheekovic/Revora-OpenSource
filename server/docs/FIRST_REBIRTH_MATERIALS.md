# First-rebirth material restoration

M6.52a-c, 2026-09-16 UTC, baseline `4d6b460`. Restores Clean Water earning and
atomic Heavenly Stone crafting. At that checkpoint, final rebirth and gem
acquisition were separate open milestones; see the completion update below.
Native acceptance and deployment remain open.

## Evidence and edition selection

The official [first-rebirth quest](https://co.99.com/guide/quests/q_reborn.shtml)
requires seven different normal gems and Clean Water, exchanged for a Celestial
Stone before rebirth. Its gems are Phoenix, Dragon, Moon, Rainbow, Violet, Fury
and Kylin; Water Devils in the Adventure Islands are the published water source.
The [original Chinese quest](https://zf.99.com/guide/mission/zsrw1.shtml) describes
the periodically appearing leader as the source. The installed Waterlord family
and spawn provide the concrete local encounter identity. Exact drop probability
is unpublished. The newer [Origin Server edition](https://co.99.com/guide/quests/origin_server_-_rebirth_quests.shtml)
changes the entry level and recipe and adds a ten-kill guarantee; those rules
are not silently imported into the selected seven-gem edition.

Revora's established rebirth NPC is Sereth, ID 59558, in Twin City (396,364).
It combines the former Celestine/Eternity roles. The database has no NPC 20005;
its retained legacy handler now only directs players to Sereth. The actual
placement was loaded into the integration fixture. This is a Revora placement,
not the official site's coordinate set. Sereth's short recipe text now includes
Fury correctly; it no longer asks for an unnamed eighth kind of gem.

Installed server item catalog SHA-256:
`1ed7a23b7a34b5e3706277f97e421e3cdf0b269eeb5be7367a4ebc0e4f22b3ef`.

| Material/result | Installed ID |
| --- | --- |
| Normal Phoenix Gem | 700001 |
| Normal Dragon Gem | 700011 |
| Normal Fury Gem | 700021 |
| Normal Rainbow Gem | 700031 |
| Normal Kylin Gem | 700041 |
| Normal Violet Gem | 700051 |
| Normal Moon Gem | 700061 |
| Clean Water | 721258 |
| Celestial Stone, called Heavenly Stone by Sereth | 721259 |

Read-only database inspection found Waterlord family **8500**, level 120, and
spawn **1080** on map **1212**, centered at **517,732**, with existing spawn timing
900. No monster/spawn/NPC row was changed. Names alone do not qualify a monster:
the earning service requires that exact family on the physical map 1212, with
canonical map/entity ownership. Similar WaterDevil families on other maps and
private copies are excluded. The existing NPC's claim about a WaterEvilElder
had no matching server producer and has been corrected to the installed boss.

Inspection helper: ignored `obj/chi-refill-validation/RebirthSourceInspect.cs`,
compiled x64 from the reviewed Revora read-only inspector template. Executed
binary SHA-256 `7D7AE1273956694E4DB8B0E6C47923C88E2F195E04801FC3F475C342373B29D0`.
It uses protected local configuration, starts a read-only transaction and prints
only relevant catalog/spawn/NPC facts. No secrets or player records were printed.

## Earning and transaction behavior

`Config.CleanWaterBasisPoints` defaults to **2500/10000 (25%)** per Waterlord
death. This is an explicit configurable Revora probability, not an official
rate claim. Nonpositive values disable the source; values above 10000 clamp.
The installed boss population and timing remain unchanged. Ordinary eligible
players can hunt without accepting a quest or obtaining GM status.

One random decision is allowed per canonical monster death stamp. Repeated
notifications cannot reroll failure or duplicate a drop; a later respawn life
can earn another. Successful rolls produce an unbound Clean Water floor item
using the existing killer/team protection, public availability and expiry rules.
There is no direct inventory gift bypassing pickup ownership or bag capacity.

Both native floor-item processing and direct server/auto-hunt pickup dispatch
enter `CleanWaterDrops.Pickup`. It checks frame bounds at the existing wire
boundary, real position, canonical floor reference, expiry, claimant ownership
and immutable item origin. Under player/drop locks, item insertion and a unique
pickup receipt commit together. Only then does the floor item disappear and the
inventory update. A failed or suppressed write preserves the item on the ground
and fences the stale session. Floor cleanup compares object identity so an old
expiry cannot remove a newer drop with the same floor UID.

At Sereth, option 38 previews the exact eight selected item identities and
option 39 confirms once within two minutes. Changing the selection, reopening,
cancelling or speaking to another NPC invalidates the offer. Material selection
accepts only the seven **normal** gem IDs and Clean Water, unlocked, permanent,
unsuspicious and in the owner's bag. A single unit is taken from each stack;
legacy stack-zero singleton items remain usable. The output inherits binding
if any input is bound. There is no new CP, RVP or silver charge.

The craft transaction locks the owner, verifies persisted location and bag
capacity, conditionally consumes all eight exact material preimages, inserts the
stone, and records the craft plus all eight material identities. A fully consumed
input can free the result slot in a full bag; partial stacks require spare room.
Missing, stale or suppressed writes roll back the entire craft. Inventory changes
occur only after commit. Duplicate confirmations cannot repeat a recipe, while a
new intentional offer can legitimately craft another stone.

Startup initializes three new tables idempotently: `revora_clean_water_pickups`,
`revora_heavenly_stone_crafts` and `revora_heavenly_stone_materials`. Validation
created them only inside the disposable test schema. Production initialization
awaits deployment. No achievement is granted for possession or crafting: 11030
and 10315 still require their distinct reviewed rebirth completion/growth routes.
The old multi-write rebirth implementation is not presented as repaired by this
material milestone.

## Validation and limits

Server build SHA-256:
`DEF295704AE0421BACC69631986E90E251C2B2D4238D43D0F60A983F3449BE99`.
`Verify-PotencyHunt.ps1 -RebirthMaterialsOnly` passes **74** checks. They include
actual installed-family monster death, owned native pickup, direct/auto-hunt
pickup dispatch, malformed frames, exact probability boundaries, failed-roll
replay, later respawn, foreign collectors, concurrent claims, full bags, expiry,
removed/replaced floor objects, tampered origins, suppressed item/receipt writes,
all eight recipe materials, inherited binding, partial and legacy stacks,
changed offers, stale ownership, atomic rollback/retry, and production
character/item reload. An ordinary character carries its actually hunted Clean
Water into the craft. The seven gems in that fixture are supplied prerequisites;
it is not a complete mining or first-rebirth playthrough.

The same build passes Grotto **262**, monster equipment **47**, class promotions
**266**, Moon Box **31**, progression evidence **74** and default hunt/boss reward
**216** checks: **970 gameplay checks**, plus **3** producer-audit tests. Suites
ran sequentially in the shared disposable schema. Ignored logs are
`obj/chi-refill-validation/rebirth-materials.log` and `rebirth-*.log`.

The achievement producer inventory stays **217/337**. This milestone provides a
necessary real quest material path; it does not award unrelated achievements to
raise that count. Wardrobe classification totals remain unchanged.

Remaining work:

- The [M6.53 mining batch](MINING_GEM_EARNING.md) and subsequent
  [M6.54 island journey](WATERLORD_ACCESS.md) complete automated ordinary material
  access, walking, mining, hunting, return, crafting and reload. Native acceptance
  and actual hunt/drop pacing remain open.
- Replace first-rebirth's separate class/stat/item/skill/proficiency writes and
  late stone debit with an atomic final transition and reviewed reward choices.
- Resolve exact quest-versus-growth achievement completion boundaries and the
  separate second-rebirth trial; Exemption Token use is not trial completion.
- Validate genuine 6711 boss, drop/tooltips, pickup, crafting dialogs and reconnect,
  and deploy only under the separate live-release authorization.

No client patch was installed, no public download was published and no running
server was replaced or restarted.


M6.52d update: the [atomic final rite](FIRST_REBIRTH_COMPLETION.md) now completes
the earned-stone journey. Material acquisition is covered by M6.53/M6.54; native
acceptance, deployment and the documented post-rebirth parity follow-ups remain open.
