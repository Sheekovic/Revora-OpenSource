# Dragon Slayer earning branches (M6.47)

## Retained-career capacity preparation (2026-10-01)

The next composed-process route uses the existing ordinary career4294907001,
not a new character with supplied pills. Its bag was full and all warehouses
were full. `career-dragon-prepare` sends one carried arrow and seven stored
unbound spare weapons to the existing peer4294907002 through normal trade
packets. Each weapon is withdrawn through warehouse1102 first. Six earned bound
runes and the unopened bound Ancestor Box take those seven warehouse places.

Main and peer now each carry32 items; warehouse8 remains at60. The same UIDs,
bindings and exact composed prior-career snapshot survive a full game/database
restart in `career-dragon-prepared`. Nothing is deleted, redeemed for no value,
or fabricated to create capacity. No new achievement or Dragon Slayer completion
is claimed. Both phases pass on candidate SHA-256
8424C9F7FB1FAB516906EA053C7A54329CC029FC1367C1F214F40FC626846A08.
Checkpoint: `revora_career_dragon_prepare_probe` / `CareerBeadSnapshot`.
The older bead checkpoint is retained as historical evidence, not overwritten
to hide these ordinary ownership/location changes.

Next: ordinary Grotto admission, all seven grass/minion/ghoul branches, earned
Dragon Pill, paid Terato summon and interrupted encounter/reward recovery.
Preserve native acceptance and live-deployment gates. Existing handler-level
Terato tests do not yet establish this composed-process career route.

## Retained seven-hunt route and crafted-pill restart (2026-10-01)

`career-dragon-hunts` continues that same character through the installed desert
portal, Grotto Envoy27837 option2 and the third-floor portal. Twenty-nine normal
ranged monster kills earn a Grotto Key through the existing random drop rule;
normal floor pickup records its provenance. The fourth-floor Guardian7764
consumes it for fifth-floor passage, and the installed portal reaches floor6.
No key, VIP privilege or summon ingredient is supplied.

Guardian7767 issues each of the seven bound grasses. Ordinary item use summons
nine owned minions per branch; normal ranged attacks earn all63 kill slots.
Guardian exchange produces each pellet, consumed beside its actual statue to
summon the corresponding ghoul. Seven more normal ranged kills earn all seven
distinct bound pills. The ordinary reviewed Guardian recipe consumes those
seven pills and creates one bound Dragon Pill. No damage amount, death callback,
loot draw or quest-progress record is supplied for this route.

The saved completed hunts, all70 kill receipts, key provenance, paid passage,
craft/material records and composed prior-career/item snapshot survive abrupt
process exit and database/game restart in `career-dragon-pill`. Both phases pass
on candidate8424C9F7FB1FAB516906EA053C7A54329CC029FC1367C1F214F40FC626846A08.
Checkpoint: `revora_career_dragon_hunts_probe` done=true /
`CareerDragonSnapshot`. Character7001 remains on2056 with the unspent pill.
NPC/combat approach coordinates and attack cooldowns are controlled; this is
server-handler earning, not native movement or fair-combat acceptance.

The retained receipt inventory now has217 stored identities and120 absent,
adding the three actual floor4/5/6 travel receipts. Its older identities still
have mixed provenance: this is not217 independently earned achievements or an
overall completion percentage.

Before the paid Terato phase, source inspection found a remaining reward gap:
`TeratoRewards.Initialize` limits the soul pool to P1-P6 and all floor rewards
are currently reconstructed unbound. New offerings still need the owner's
valuable bound-P7 event contract. Preserve existing paid runs and recorded loot
when introducing that contract; then validate paid-summon and victory/pickup
recovery with this earned pill. Terato victory and10932 are not claimed here.

The seven grass-to-ghoul branches now have ordinary-player earning paths on
Frozen Grotto floor six (2056). This completes M6.47's server implementation;
M6.48b now supplies [Terato Dragon and its server rewards](TERATO_DRAGON.md).
Genuine-client acceptance remains M6.48 work.
See [the researched identities](DRAGON_SLAYER_RESEARCH.md) for catalog hashes,
the official guide and the installed 6711 coordinates.

## Player route

Guardian 7767 at (361,645) offers one active hunt at a time. The player chooses
a branch, receives its bound grass, and uses it to summon nine matching minions.
After nine recorded kills, the Guardian exchanges that grass for its pellet.
Using the pellet beside the matching statue consumes it and awakens the ghoul.
Defeating the ghoul awards its distinct bound pill. The existing seven-pill
recipe combines all seven earned pills into one bound Dragon Pill.

The statues occupy the seven reviewed 6711 sites, with reserved NPC IDs
900000120 through 900000126. Server startup checks the floor hash, walkability,
existing identities and presentation templates before installing them. No
database NPC or monster template is overwritten.

Dialogs remain brief. The Guardian reports the current objective and offers
recovery or an outstanding reward when needed. A statue can resume a paid ghoul
summon without consuming another pellet.

## Ownership and recovery

`revora_dragon_hunts` stores the branch, nine-bit objective mask, material UID and
phase. `revora_dragon_hunt_kills` permits one receipt per encounter slot. Exact
registered actors and their owning ordinary session are required. The owner's
registered summoned companion may fight and credit the owner; foreign,
unregistered and replaced companions cannot. Combat admission blocks unrelated
players from attacking these actors, and blocks actors from attacking bystanders.

Material exchange and ghoul payment verify exact persisted ownership and item
preimages under the player's inventory lock and a database character-row lock.
Reward insertion, death credit and hunt progress commit together. Suppressed
writes or stale ownership roll back and fence the session for reload. Generic
monster loot is disabled for the reserved encounter families.

Partial minion progress survives reconnects. Only unfinished minions are recreated.
A committed ghoul payment survives loss of temporary world actors. An earned pill
remains claimable when the bag is full. Recovery replaces an unspent grass or
pellet only when its old UID is absent from both persistent items and inventory;
warehousing an item does not make a second copy available.

Revora policies, rather than claims about historical Conquer rules:

- One unfinished branch per character, bound quest materials and pills.
- Private combat ownership; the final dragon's team rules are separate work.
- At most 32 concurrent hunts, with a 15-minute temporary actor lifetime.
  Death, departure or session replacement also releases actors without erasing
  durable progress or paid summons. Capacity failure never consumes the pellet.
- Minions use the existing FrostBladeDevil body 154; ghouls use MagmaDemon body
  163. Both have level 128 and 65,535 HP, matching the installed named profiles'
  declared level/life. Minions use attack 1,600-2,328 and defense 100; ghouls use
  attack 4,000-6,000 and defense 1,200. These combat and artwork choices require
  play testing; they are not verified original server balance or original art.

## Native evidence and its limits

Read-only static analysis of installed x86 `Env_DX9/GameData.dll`, SHA-256
`8F5687DD11A17E7950358C19AEFBB197E220F8F1FC24442A826EA0E28A2E409C`,
finds the Monster.dat parser at `100173b9`. It stores TypeID at record offset 0
and BodyType at offset `0x28`; exact-name lookup is `10010cc5`, vtable slot 11.
In the approved DX9 executable, `0089cbb0` combines a base actor body with
BodyType times 1,000. All fourteen named minion/ghoul profiles have BodyType 0.
This does not establish their original wire body values. TypeIDs 3066-3079 were
therefore not substituted as monster bodies. Analysis artifacts stay private
under ignored research/isolated and obj; no proprietary implementation was copied.

## Validation

The final staged build passes 191 Grotto checks, including all seven branches
through 63 minion kills, seven ghoul kills and one Dragon Pill craft; simultaneous
death dispatch; exact material ownership; rollback of silent database failures;
full-bag reward recovery; real character/item reload; paid summon reconstruction;
and registered-companion ownership. These are ordinary non-GM server fixtures.
They do not constitute a native login, rendered encounter or full Host restart.

Combat regression: 669 checks on the final build. Neighboring suites passed
before the final companion ownership refinement: monster equipment 47, travel
105, cosmetic ownership 179, quest packets 9, Labyrinth 43, Release Souls 35,
Summer 65, Sky Pass 34, Snake Islands 25, Dis City 164, Power Arena 95,
Moon Box 31 and Demon Hunt 44. Producer audit and its three tests remain valid;
coverage is still 156/337, not a completed 320-achievement route.

Final staged server SHA-256:
`E64029C81BF74E7FE486CA82FD8FBD3612B87E411E1C13FE8EABE88F0EB60FA9`.

No live restart, deployment, client installation, public patch or website change
was performed. The subsequent M6.48b implementation enables its final summon
in server fixtures; see the linked evidence for that milestone's remaining gates.

## Fresh seven-hunt Dragon Pill earning (2026-10-03)

- [x] Continue ordinary FreshRoadmap1 from its completed Demon Exterminators
  checkpoint. Preserve possessions through ordinary warehouse storage as needed,
  travel through installed portals/NPCs and earn the Grotto key721347 from a
  normal floor drop after five calculated-combat kills. Pick it up through the
  sealed floor-item handler and use the ordinary Grotto passage.
- [x] Complete all seven Dragon Slayer branches using their issued grass/pellet,
  owned summoned actors and calculated melee. All70 quest kills are saved and
  each branch produces its bound quest material through the ordinary NPC.
- [x] Craft exactly one bound Dragon Pill720598 from the seven earned materials;
  the materials are consumed. A separate game/database process preserves the
  exact pill, seven completed branches, kill/craft/material/key/passage records
  and nested prior career.

`tools/CheckCandidateFreshDragon.cs` passes `career-fresh-dragon-hunts` and
`career-fresh-dragon-pill` on the first run. Checkpoint:
`revora_fresh_dragon_hunts_probe`, done=true. Private game SHA-256:
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
The earned pill is subsequently consumed by the
[fresh Terato batch](TERATO_DRAGON.md#fresh-earned-pill-summon-victory-and-loot-recovery-2026-10-03).
Target approach, screen registration, cooldown and available-map respawn timing
are controlled. Native navigation, retaliation, natural balance and live
acceptance remain open. No production rebuild, live restart or deployment.
