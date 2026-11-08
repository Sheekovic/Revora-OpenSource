# Terato Dragon: server earning route (M6.48b)

## New-offering P7 contract and retained process route (2026-10-01)

New paid summons now freeze `valuable_reward=true`. Their victory soul is one
random catalog P7 and is bound; the existing Superior Refinery Pack, seven-day
accessory and50 team Study Points retain their contracts. Older paid runs
default false and retain the previous unbound P1-P6 soul pool. Existing recorded
loot defaults unbound and is not rerolled or rebound. This is Revora's approved
valuable event reward policy, not a historical Conquer reward claim.

Victory writes the selected soul and binding with the other loot in the existing
transaction. Reconstruction uses that saved binding; pickup checks the in-memory
and saved preimages, then writes the bound item, collector receipt and10932 in
one transaction. A changed saved binding cannot silently produce a differently
bound item. Existing ownership/public-pickup windows and two-minute expiry stay
intact, as does the rule that mere victory does not award10932.

The retained ordinary character4294907001 spends the Dragon Pill earned through
all seven hunts. Four private process phases pass:

- `career-terato-summon`: exact pill consumption, one active paid encounter,
  immediate resume replay without another charge, then abrupt exit.
- `career-terato-win`: production Seal Keeper recovery after database/game
  restart,314 normal ranged hits against50M HP, actual victory,10526,50 Study
  Points and three uncollected saved drops, then another abrupt exit.
- `career-terato-loot`: reconstruction after restart, normal floor-pickup packets
  including repeated requests, exactly three items including one bound P7, and
 10932. The saved item UIDs, purse, study and prior-career state are checkpointed.
- `career-terato-reload`: exact cold recovery, no new actor/loot from replay, and
  exact snapshot preservation after restoring only controlled NPC-approach
  coordinates. Further contract checks use transaction-local temporary tables:
  false selects an unbound P1-P6 soul; true selects a bound P7. Those synthetic
  branch checks roll back and create no real paid run or reward.

Checkpoint: `revora_career_terato_probe` stage3 / `CareerTeratoSnapshot`.
Build and process checks pass on candidate SHA-256
`E7DCA61B62DBA49412DEE383A633D7AB4447C1B7C52AB5370F6B7006E20682E9`.
The final replay/contract fixture initially had a compile-only uint/int mismatch;
it was corrected before execution. The existing Terato suite's artifact journey
now explicitly selects its historical contract and its new-reward expectation is
updated; that broader suite was not rerun in this batch.

NPC/combat approach and attack cooldowns are controlled. The encounter clock is
fixed per phase at the original test stamp plus0/10/20/30 seconds so unclaimed
loot remains within its saved deadline; this does not prove real elapsed expiry.
Damage and loot draws are not supplied. Native UI/HP bars, fair difficulty,
power-loss/COMMIT ambiguity and deployment remain separate gates. No live restart
or client patch occurred. Earlier source totals and unbound reward descriptions
below are historical and are superseded only within this stated new-run scope.

Ordinary players can now finish the Dragon Slayer server route: earn all seven
ghoul pills, craft a Dragon Pill, summon Terato, defeat it with their team, and
collect its rewards. Receiving the encounter's Dragon Soul earns achievement
10932. Genuine 6711 rendering, gameplay acceptance and deployment remain open;
the server portion of M6.48 is complete; native acceptance and coordinated
release remain open under M6.48c.

## Earning and summoning

Use a Dragon Pill near Frozen Grotto F6 (532,394), or offer one Dragon Pill and
five Meteors to Royal Guardian at Wind Plain (517,742). Royal Guardian presents
a fresh, two-minute confirmation before spending anything. Cancel, expiry,
changed materials and repeated confirmations cannot spend another offering.
Both bound and unbound owned offerings work, including partial stacks.

The installed client describes those two sites. The legacy server instead put
NPC 180810 on map 5000 at (90,743). Startup validates that known placement and
mesh 8630, then relocates it in memory to canonical map 1002. Its database row
is preserved. A Seal Keeper using the same reviewed appearance stands at the
F6 site for paid-hunt recovery; this additional recovery NPC is a Revora choice.
The reviewed town terrain hash is
`9F75CC638480A0485E6B17BDF9F9F76886A9AB7449317BEBE18E72EF6AD34A10`.

The summoner and up to four nearby canonical teammates form the admitted party.
They may fight the owned dragon; unrelated players and guards cannot take over
its combat. Registered summoned companions can credit their owners. Departed,
replaced or unregistered companions cannot impersonate an admitted fighter.
Team members who leave the summoner's party lose encounter access.

## Boss and combat

The separate reserved family 9916100 is named exactly `TeratoDragon`, with
50,000,000 HP, level 138 and Boss=1. Existing DragonKing template 4152 stays
unchanged; its known dragon body 950 supplies the presentation. The installed
Terato NPC's motion-family metadata also points to 950000. Native monster
rendering remains an acceptance task.

The approved x86 GameData loader resolves `TeratoDragon` with Boss=1 and
MaxLife=50,000,000 in both reviewed monster.dat variants. Server fixtures
round-trip actual HP at full, half, one and zero through spawn and health-update
serialization. This is stronger than merely checking a server Boss flag, but
does not claim that a person has seen the new encounter in the live client.

Terato rotates through existing 6711 spell identities:

| Attack | Spell | Revora geometry |
| --- | --- | --- |
| Dragon Breath | 7014 | Forward 90-degree cone, radius 8 |
| Tail Sweep | 7011 | Backward 90-degree cone, radius 8 |
| Flame Shower | 7013 | Circle, radius 6 |
| Misty Flame | 7017 | Ring, radius 3 through 8 |
| Fear Roar | 7015 | Circle, radius 8; two seconds of fear |

Each cast warns the party two seconds ahead and then has a four-second recovery.
The threatened area is fixed when the warning begins, allowing movement to
avoid it. Timer resolution continues even if players retreat beyond normal
melee range. Tail Sweep uses the common melee calculation; damaging flames
use the common magic calculation, preserving the corresponding defenses.
Fear uses the existing timed status and update packet. Nearby bystanders never
enter these area attacks. Ordinary invulnerability is respected.

Attack 8,000-12,000, magic attack 12,000, defense 7,000, magic defense 40,
timings and geometry are explicit Revora balance choices. The official guide
establishes named mechanics, not these historical server numbers. No legacy
area-attack loop that damages unrelated screen occupants is reused.

## Recovery and rewards

`revora_terato_runs` and `revora_terato_materials` commit the offering and paid
entitlement together. One unfinished paid encounter is allowed per character;
up to 16 temporary dragons may coexist across both sites. Capacity or landing
failure never consumes an offering. Actor IDs avoid collisions between the two
maps' independent counters.

A temporary hunt lasts 30 minutes. Owner logout/departure, session replacement,
or having no living admitted fighters releases it. A paid owner can resume at
the same seal without another offering, including after world loss. Resuming
rebuilds full boss health and admits the currently gathered party; it does not
preserve damage from an abandoned fight. These are Revora lifecycle policies.

Victory commits once, together with all Study Point entitlements and three
ground-loot records. Each admitted teammate still nearby receives 50 Study
Points, including a fallen summoner. Distant, foreign and newly joined players
receive none. Each payment locks the character and checks the saved balance;
overflow retains the full award for later rather than clipping or wrapping.
An online recovery poll services bounded, rotating batches so offline or full
balances cannot indefinitely starve other players' pending awards.

The official guide lists Superior Refinery Packs, equipment accessories and
Dragon Souls without a complete loot distribution. Revora explicitly supplies
one of each per victory:

- One Superior Refinery Pack, 720399; its [70/30 opening service](SUPERIOR_REFINERY_PACK.md)
  produces an installed level-four/five refinery material.
- One uniformly selected seven-day accessory: Broom 350001, Wrench 360001,
  Wooden Bow 370001 or Wok 380001. Their installed duration is 10,080 minutes.
- One uniformly selected installed phase-one through phase-six Dragon Soul.
  Phase-seven daily-world-boss loot remains a separate reward system.

The three items lie on the battlefield for two minutes. The killer and permitted
team collectors receive the existing 15-second ownership window, then normal
public collection is allowed. Reconstruction preserves absolute UTC deadlines;
it does not renew ownership or expiry. A full bag leaves the item on the ground.
Accessories start their seven-day lifetime when successfully collected.

`revora_terato_study` records one payment per eligible player and run.
`revora_terato_loot` records one collector and item UID per loot slot. Native
1101 pickup validates framing, the original floor object, location and exact
provenance. Receipt, item insertion and achievement evidence share one
transaction. Suppressed writes roll back; replay and stale reconstructed floor
objects cannot duplicate a reward. Inventory, Study Point and achievement
updates follow commit.

Achievement 10932's installed requirement is receipt of a Dragon Soul from this
quest. Killing Terato, collecting its accessory/pack, or owning an unrelated
same-type soul does not grant it. A public collector who actually receives this
quest-provenanced soul after the ownership window can earn it. Source evidence
uses `dragon-slayer-v1` with the durable encounter ID. Producer coverage is now
157/337; a full per-character route to 320 remains incomplete.

## Validation and remaining gates

The final staged build passes 262 Grotto/Dragon Slayer checks. These include the
entire grass-to-ghoul-to-pill-to-Terato route, real lethal attack and pickup
dispatch, all five combat shapes, health-packet round trips, actual team credit,
paid reconnect recovery, cross-map actor IDs, native cancellation, capacity,
database rollback, loot reconstruction, achievement serialization and ordinary
character/item reload after completed rewards.

Related final-build checks pass: 213 Potency Hunt/daily boss/Extraction, 164
Dis City, 47 monster equipment, 174 refinery, 64 subclass, 105 travel, nine
quest-packet, 74 progression, 179 cosmetic ownership, 669 combat, 57 companion,
22 native profile lookups and three producer-audit checks. Total: 2,042.

Staged server SHA-256:
`00C8BA6CE411955570FB24B0D84824570FEF39D7EF8A6E407875FDA31FB4B811`.

The tests reconstruct temporary state and use production character/item loaders;
they do not run a full live Host restart or native network login. Remaining
acceptance covers real client NPCs, monster/spell rendering, full and damaged HP
bars, fear expiry, team combat balance, floor loot and wardrobe notifications.
No live restart, deployment, client installation, public patch or website update
was performed. See [protocol evidence](PROTOCOL_RESEARCH.md) for the exact
installed file hashes and the distinction between native and inferred facts.

## Fresh earned-pill summon victory and loot recovery (2026-10-03)

- [x] Consume the fresh career's actually crafted Dragon Pill through normal
  item use at the installed summoning NPC. One owned actor and one consumed
  material receipt are recorded; repeated recovery creates no second actor.
- [x] After process restart, reconstruct the paid encounter through its ordinary
  recovery option without another pill. Defeat the50,000,000-HP Terato through
  7,163 calculated melee attacks using existing earned equipment. Award50 Study
  Points,10526 and three uncollected loot entitlements.
- [x] After another restart, reconstruct all three floor drops and pick them up
  through sealed floor-item packets. Repeat every pickup: each item is received
  once. Exactly one is a bound catalog-P7, and10932 is saved.
- [x] Final cold reload preserves exact possessions, run, consumed material,
  loot/study records and nested career. NPC recovery and loot polling recreate
  neither a boss nor items. Controlled NPC-approach coordinates are restored
  after this replay check before comparing the whole snapshot.

`tools/CheckCandidateFreshTerato.cs` passes
`career-fresh-terato-{summon,win,loot,reload}` on the first run. Checkpoint:
`revora_fresh_terato_probe`, stage3. Private game SHA-256:
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
Together with the Dragon Slayer prerequisite, all six phases pass without
seeding combat results, keys, quest materials, pills or reward items.

Clock, approach, screen registration and attack cooldown are controlled.
Retaliation, natural difficulty, authenticated/native presentation and live
acceptance remain unverified. No production rebuild, client patch, live restart
or deployment occurred. The post-Flame achievement export retains its dated
scope and does not yet include this batch's two additional identities.

## Target-distance overflow correction (2026-10-04)

Terato's attack geometry previously squared coordinate differences in32-bit
integers. A difference of46,341 tiles overflowed the squared distance and could
classify a distant admitted party member as inside its attack area. Normal map
coordinates did not expose this in the earlier encounter fixtures. Both products
now use64-bit arithmetic, covering differences across the ushort coordinate range.

The new focused regression failed against the previous assembly at phase0,
offset(46341,0). After the correction,67 checks pass: signed distant offsets
across all five phases, normal forward/rear cones, diagonal facing, circle and
annulus boundaries. The default combat suite also includes these checks.
Reproduce with `tests/CombatBalance/Verify-Combat.ps1 -ArtifactRoot
obj/counter-validation/bin -TeratoGeometryOnly`.

The isolated build passes; SHA-256:
`0142B592B8B2C419A19F66E2E92862FD4BB22D9B6FC5EB870EB4EDF719B064C4`.
This is a targeting correction, not a new full encounter/persistence pass.
Unrelated owner edits were excluded from the candidate build. No live server,
database or client changed.
