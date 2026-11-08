# Artisan's Feather implementation

Current status (2026-10-01): this chain is included in the completed private
[nineteen-chain earning/restart validation](EQUIPMENT_QUEST_AGGREGATE.md).
The approved Revora10931 evaluator is implemented and tested; older statements
below that it is disabled or that no process restart was tested are historical.
Native/authenticated-client acceptance and live deployment remain pending.

## Evidence and intended route ? 2026-09-26

The [official guide](https://co.99.com/guide/quests/feather.shtml) describes
Chang, the two Luos, 100 feathers from Pheasants/Turtledoves/Robins, then a
Vulture Feather, with the quest restarting after rebirth. Its NPC table places
Artisan Luo at 342,640, but its walkthrough says 442,431. The Facebook guide
uses older NPC coordinates and badges and is not the selected edition.

The installed 6711 journal identifies missions 1733..1737, level 45..65,
all classes, with this route:

1. Chang (8796, 1002:473,300) to Apprentice Luo (4466, 1002:459,331): 100 silver.
2. Apprentice to Artisan Luo (4467, 1002:342,640): 100 silver.
3. Artisan requests 100 feathers; mission counter 1735,1,1,100: 100 silver.
4. Hunt Vultures for item 729976, return to Artisan: 1,500 silver and 1/3 EXP Ball.
5. Return to Chang for pack 728239, Level50UniqueWeaponPack.

`audit_artisan_feather.py` validates the installed journal hash, exact chain,
eligibility, objective count, route and item names. The installed rows repeat
ActivityType=0 twice; that exact duplicate is tolerated, while other duplicates
or conflicting values fail. Three deterministic contract tests pass. The JSON
contract is evidence, not a completion claim or proof of achievement 10931.

The read-only loaded catalog confirms birds 1/2/3, RobinCub 2101, and Vulture
2143 (level 47, 1,467 HP). Only families 1/2/3 are planned for the first hunt;
RobinCub is not implicitly included. Bird spawns exist on 1002. Vultures exist
on 1020 around 713,678 and 866,552, which differs from the journal hint 586,771.
Directions must use the loaded spawn regions. Neither Luo NPC currently has a
matching database row. Client npc.ini supplies types 550/551 (meshes 5500/5510).
The catalog report contains only derived metadata; no proprietary files copied.

Revora implementation policy: minimum level 45, no upper limit, once per rebirth
0/1/2, existing Chang UID 900071727, and the installed journal's Luo positions.
Use one saved feather credit per genuine eligible bird death and an explicit
Revora 50% Vulture Feather chance; original drop odds are unpublished. Final
claim should grant the bound weapon pack plus a frozen bound P7 soul, in line
with the owner's one-time quest reward policy. No daily P7 source is added.
The original journal's 1,800 total silver and twenty-minute EXP remain.

## Reward pack checkpoint

Normal item dispatch now handles pack 728239 through FixedOutputPacks' atomic
consume/output/receipt path. It grants one Unique (+0) weapon and preserves
source binding. No EXP Ball is inserted into this pack. Class is captured at
opening; unsupported classes keep their pack. These are explicit Revora class
choices rather than a claim of verified original per-class pack contents:

| Class | Weapon ID | Required level |
| --- | --- | --- |
| Trojan | 410097 MoonBlade | 50 |
| Warrior | 510097 UnionGlaive | 50 |
| Archer | 500087 QinBow | 50 |
| Ninja | 601097 FangKatana | 50 |
| Monk | 610097 JeweledPrayerBeads | 50 |
| Pirate | 611097 StreamRapier | 50 |
| Dragon Warrior | 617097 DragonNunchaku | 50 |
| Water/Fire Taoist | 421087 IronBacksword | 45 |
| Windwalker | 626097 PrideFan | 50 |

The catalog lacks a level-50 ordinary backsword; Taoists receive the nearest
lower ordinary tier rather than an epic item. Runtime selection validates each
item's level, required class and quality against the loaded catalog.

Build and **435 checks pass** (406 prerequisites + 29 pack checks), covering
bound/unbound stacks, concurrent opening, full bags, injected insert rollback,
reconnect retry, ten class cases and exact reward persistence, and unsupported
class refusal. Logs: `obj/artisan-pack-build.log`, `obj/artisan-pack-tests.log`.

**Still incomplete:** NPC/hunt progression, durable feather count and Vulture
item ownership/recovery, final quest rewards, saved journal/overlay, terrain and
ordinary full-route acceptance. Native and Host acceptance remain pending.
No production schema, live server, active client or deployment changed.

## Ordinary route, persistence and private journal ? 2026-09-26

ArtisanFeatherQuest now initializes the two NPCs, registers missions 1733..1737
and connects ordinary NPC, death and revive-reset handlers. Chang's menu offers
the quest at level 45+. Saved stages require Apprentice, Artisan, the 100-bird
hunt, the Vulture Feather and Chang's final claim in order. Every eligible
ordinary character can participate once per rebirth 0/1/2. Each genuine bird
death credits one feather, capped at 100; replayed deaths and RobinCub do not
count. Vultures use the documented Revora 50% chance. Full bags and failed rolls
cannot be retried against the same corpse.

Hand-ins atomically persist stage, silver/EXP and items. The frozen final P7
and weapon pack are bound and require two spaces. The tracked Vulture Feather
must be the character's exact usable bound item. Stored items cannot be copied;
discarded ones reset the tracked UID through a recovery receipt, permitting a
new Vulture hunt while preserving all 100 bird credits. Finished quests cannot
recover consumed feathers. Journal statuses come from saved stages; client
Begin and NPC abandon requests cannot overwrite them. The NPC reports the saved
feather count; original native subtask-counter behavior is not claimed.

Build and **482 checks pass** (406 prerequisites + 76 route checks): three
rebirth states, forged Begin, remote acceptance, ordered referrals, 99 versus
100 actual bird deaths, replay/cap/reload, full bags and failed-roll replay,
Vulture ownership, storage/disposal recovery, silver/EXP, failed P7 insertion
rollback, duplicate claims, exact persisted rewards and ordinary pack opening.
Logs: `obj/artisan-quest-build.log`, `obj/artisan-quest-tests.log`.

The private composed journal tool rewrites only missions 1733..1737 over the
Spun Gold overlay. Three rewrite tests pass. The result is
`obj/chi-refill-validation/artisan-feather-journal/`, SHA-256
`7526375244fd08edd06d1bece91049f9d52d31c5a4135bf58587a6dcdda7ddaf`.
It remains unsigned, uninstalled and unpublished. Inputs and approved DX9 are
hash-checked and unrelated journal bytes are preserved.

### Open terrain acceptance finding

The read-only terrain audit does **not pass** shared cardinal route acceptance.
Twin City's cached server grid is 824x824 and every cell is open; the bound
client archive is 852x852. Native flags include 4/5, which the conservative audit
treats as blocked without inferring their meaning. Chang to Apprentice has a
shared flag-zero path, but Artisan 342,640 and the Robin hint 295,684 have no such
path from Chang. This does not prove that native jumps, portals or other movement
cannot reach them. Ape City's two loaded Vulture regions have a shared path.
No terrain was changed. The report records exact hashes, dimensions and failed
route results; the audit writes that evidence then exits 2. The initial strict
0/1 parser rejected the newer flags before this explicit conservative handling.

NPC placement follows installed quest evidence. Source earning tests position
ordinary test characters directly and cannot resolve native navigation.
**Native navigation, NPC/visual/journal acceptance, concurrent storage UI,
companion earning and actual Host interruption remain open.** Deployment and
aggregate achievement 10931 stay gated. This checkpoint changes source only.


## Loaded hunts and material recovery through full restart (2026-10-01)

Four private phases pass: `career-artisan-birds`, `career-artisan-vulture`,
`career-artisan-claim`, `career-artisan-reload`. Existing ordinary actor4294908401
accepts Chang's request, visits Apprentice and Artisan in order, and earns99
feather credits from loaded Pheasant deaths. The premature hand-in preserves99
and stage3. The actor saves and exits abruptly without a final reward.

After full game/database restart, the exact quest, profile, belongings and prior
Azure/Connector/event snapshots remain. One more loaded Pheasant death reaches100;
Artisan opens the Vulture stage. Installed Phoenix/Ape City portals provide the
map travel. The unchanged50% roll succeeds on the first loaded Vulture death in
this run, producing one tracked bound feather. The actor returns through the
installed portals, saves with that feather and exits abruptly again.

Another cold startup retains that exact material. Artisan consumes it and Chang
grants exactly two new bound items: weapon pack728239 and the saved P7 soul.
The route's silver increase is1,800. Normal hand-in EXP is retained in the profile
snapshot. Completion replay and completed-material recovery give nothing else.
A final cold restart preserves stage6, both saved reward UIDs and the complete
snapshot; repeated completion/recovery requests still change nothing.
Checkpoint: `revora_artisan_process_probe`, step3 and ArtisanProcessSnapshot.

No stages, credits, materials or rewards are fixture-seeded. NPC approach and
lethal damage are controlled; loaded monsters use the production ReceiveAttack
path, and the Vulture roll was not overridden. This proves server earning and
persistence, not native travel to the disputed Artisan coordinate, natural combat,
all spawn rows, companion ownership or pack opening. The earlier terrain finding
remains unresolved and must not be marked accepted from these positioned NPC
checks. Native journal/UI and deployment remain pending.
No production code/build changed; private candidate SHA256:
`05F7A80A0EC5F74ED699076019CE7FFD9F2B5B2646F7C69562FEC1A4E43BD9A6`.
The main retained career is unchanged. No live server or client was modified.

## Retained Artisan Feather and Mystic Ring batch (2026-10-02)

The retained twice-reborn Water Taoist follows Chang900071727 option71,
Apprentice4466 and Artisan4467. Ninety-nine calculated Pheasant kills each add
exactly one feather credit. Premature turn-in changes nothing. A full game and
database restart preserves the99 checkpoint. The hundredth kill unlocks the
Vulture request; ordinary Phoenix/Ape portals reach family2143, whose calculated
defeat yields the Vulture Feather under a controlled successful drop roll.
Returning through the portals and NPCs consumes the feather and grants bound
728239 plus a bound P7 soul. The chain adds exactly1,800 silver.

The same character then follows Mystic Ring through Chang option81, Minstrel4500
and Shen4501. It buys twelve units each of medicine1000000 and1001000 through the
ordinary silver shop, checking exact configured payment and quantity. Shen
consumes both supplies, and twenty calculated family8 kills advance the chain.
The remaining NPC deliveries and item721708 use lead to the final two bound
rewards, including a P7 soul. Normal QuestAppearance cleanup runs afterward.

A second full game/database restart preserves the exact nested career and all
four final reward UIDs. Repeated final claims for both chains change nothing.
`career-retained-artisanring-{gather,finish,reload}` all pass on candidate SHA-256
`34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`.
Probe `revora_retained_artisanring_probe` ends at step3. Fresh read-only evaluation
confirms five of nineteen aggregate final receipts, with fourteen remaining.
Achievement10931 is still absent; these are prerequisites, not another completed
aggregate achievement.

Starting qualifications/funding retain their earlier provenance. Proximity,
cooldowns, respawn timing and the Vulture drop outcome are controlled; attack
damage is calculated, not assigned. Native UI/authenticated acceptance remains
pending. No production code/build or live deployment changed in this batch.
