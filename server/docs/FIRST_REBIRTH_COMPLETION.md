# First rebirth completion

M6.52d: the final first-rebirth transaction is implemented and code-validated.
Neither deployment nor native acceptance is complete. Recorded level-130
recovery is completed in [M6.55a](FIRST_REBIRTH_RECOVERY.md). The remaining
edition-specific catalog audit and historical reconciliation stay open; this
is not a claim of complete retail parity.

The material journey is documented in [WATERLORD_ACCESS.md](WATERLORD_ACCESS.md).
Completing that journey is not evidence that a character has completed rebirth.

## Evidence reviewed on 2026-09-16

Primary rules: [quest](https://co.99.com/guide/quests/q_reborn.shtml),
[requirements](https://co.99.com/guide/1reborn.shtml),
[advantages and bonus table](https://co.99.com/guide/reborn/rebornadv.shtml),
[items](https://co.99.com/guide/reborn/1rebornsi.shtml),
[skills](https://co.99.com/guide/reborn/rebornspe.shtml),
[Dragon Warrior](https://co.99.com/news/2014-11-10/dragon_warrior_reborn_skills_revealed.shtml),
[Windwalker](https://co.99.com/guide/windwalker_rebirth_skills.shtml).
These are gameplay references, not packet-layout evidence. Current Origin-server
level-90 rules and classes newer than this client are not imported.

The published bonus table gives a triangular progression capped at 55. Water
Saint advances every two levels above 110; other classes advance every level
above 120. Unlisted intervening Water levels retain the preceding award. This
interpolation is an inference consistent with the table, not an independently
published row. The total at level 15 is 72 plus that bonus: ten base points,
42 ordinary level points, and twenty rebirth points. Installed level-15 class
distributions are retained and the remainder is freely assignable.

Identified defects: the legacy bonus helper's arguments are reversed at rebirth
and reincarnation callers; odd Water levels can fall into a 55-point default;
the first rite adds 52 free points on top of an already allocated level-15
profile; Dragon Warrior's profile falls through to Taoist; Windwalker's first
class choice collides with a second-rebirth option. The old transaction grants
a weapon, saves profile/equipment/skills separately, then removes the stone.

## Implemented rite

Sereth's first-rebirth choice opens explicit class, body-size, reward and final
confirmation stages. The offer expires after two minutes and is invalidated by
another dialogue or changed character/belongings. All ten supported classes
are normal-player choices. Body size preserves gender. Old arithmetic options
cannot call the legacy first-rebirth function; that function now accepts only
the existing second-rebirth path.

The owner lock covers preparation, commit and projection. One database
transaction locks the character and owned rows, validates durable preimages,
consumes exactly one usable stone, changes class/stats/appearance, lowers
equipment, returns both off-hands, resets skills/proficiencies, inserts gifts,
records the rite and grants achievements 11030 and 10315. Missing/extra loaded
ownership is rejected, as are silent writes and stale saved data. Cache changes
and native update packets follow commit. Projection failure fences the session
for reload. Proficiency autosaves use the same owner lock and respect the fence.

Equipment retains quality, plus, sockets and other untouched properties. Both
sets are considered. Lower grades come from the same installed item family and
quality; special tools with no lower grade retain their minimum catalog level.
Blessing randomly selects an eligible unblessed primary piece, so an empty slot
cannot silently consume the reward. A bound stone binds gifts and the selected
blessed piece; the confirmation states this Revora ownership rule. Full-bag
checks include the consumed stone's slot, both off-hands and every gift.

The rite retains the installed original-class gift IDs/effects (410077, 500057,
421077); Ninja and Monk no longer receive the obsolete generic blade. Dragon
Warrior and Windwalker retain this edition's no-weapon-gift behavior pending
specific catalog evidence. This compatibility decision is not an assertion
that every retail weapon reward is implemented. The seven original super gems
form the random reward pool.

Reviewed cross-class arts use the cited tables and installed spell identities.
Dragon Roar and Frost Gaze I survive eligible changes, Monk auras do not leak,
and Fire's inherited Thunder/Fire are retained. Weapon/common skills survive;
known rune-derived skills are rebuilt from actual eligibility. Unclassified
spell IDs retain compatibility behavior rather than being silently deleted.
Six same-class first-rebirth arts are inserted in the same transaction. The
remaining catalog audit includes Archer's freezing effect and newer class
weapon gifts. Retained skills and proficiencies reset to zero and remember their
former level for the existing recovery mechanism.

The one-row-per-character `revora_first_rebirths` receipt records previous
class/level/experience, selected class/body, stone identity/binding and selected
reward identity. It also supplies the data used by atomic
[level-130 recovery](FIRST_REBIRTH_RECOVERY.md). First rebirth never grants second-rebirth receipts.
Existing per-life hunt eligibility naturally advances with `Reborn`; progress
and completion masks from the former life remain intact.

## Validation

The focused suite verifies all 100 original-class/destination combinations,
the independent bonus table including odd Water levels, twelve cross-class
skill transitions, six same-class arts, body sizes, both reward choices, bound
materials, exact capacity, legacy stack-zero stones and production reload.
Fault injection covers owner, item, skill, proficiency, rite and achievement
writes, including failures after stone consumption. Concurrent final clicks
and a competing proficiency autosave cannot duplicate or overwrite the rite.

The Adventure suite now continues its existing walked purchase/mining/island/
Waterlord/return/crafting journey through Windwalker rebirth and a second full
reload. It preserves the purse, seven mined gems' provenance, water pickup,
craft, fare, rite and six distinct achievements. The fixture starts with an
eligible promoted level-135 ordinary character; mining/drop rolls and time are
deterministic, and Waterlord uses reduced fixture HP. This does not establish
level-one progression, live combat pacing or native-client acceptance.

Second rebirth, token exemptions and paid reincarnation remain separate paths.
Their bonus-helper argument order is corrected, but their overall transaction
and attribute-budget restoration are not claimed complete here.


Validation totals: 392 focused first-rebirth, 171 complete Adventure journey,
452 experience, 74 progression, 23 appearance, 266 class promotion, 185 rune
loadout, 75 mastery and 216 default gameplay checks: **1,854**, plus the three
producer-audit tests. Audit coverage is 219 present / 118 absent, not a claim
that Rising Star's 320 achievements are reachable on one character.

Isolated validation executable SHA-256:
`D53A61B01B35A92EFCBD9B1462B591584EE661DA04E168268FF369699D0C53FE`.
Installed `Stats.ini` SHA-256:
`3121A5357274F33366ED65695E2EE0235BC6C38BF4405B4887A2427A5994B48F`.
Build and harness output stays under ignored `obj/chi-refill-validation`;
`first-rebirth-final-*.log` records the suites. The focused/Adventure tests were
rerun after the final inventory snapshot restriction; adjacent suites passed
before that isolated restriction. No server restart, deployment, client patch,
site publication or live character mutation was performed.

## Second-life integration follow-up (2026-09-16)

M6.55c2 restores the missing Archer first-rite grants: Freezing Arrow for
Archer-to-Archer and Poisonous Arrows when leaving Archer. This allows the
actual earned first-life art to feed the second-life retention matrix. The
original evidence below remains the earlier checkpoint; current consolidated
counts and ordinary purchase/rite/recovery evidence are in
[the second-rebirth report](SECOND_REBIRTH_RESEARCH.md). Native arrow level,
weapon and combat behavior still require separate acceptance.

## Five-star garment reward (2026-09-21)

Newly confirmed first-rebirth rites give a permanent unbound Armor of Holy Light
(187405) alongside the existing gem/blessing and supported class-weapon rewards.
The garment has matching five-star rarity and unrestricted class/level/gender
in the installed server/client catalogs; see [catalog evidence](FIVE_STAR_QUEST_REWARDS.md).
It remains unbound even when the Heavenly Stone binds the original rewards.

The nullable unique `revora_first_rebirths.garment_uid` records the item in the
same transaction as stone consumption, character transition, equipment, skills,
proficiencies and achievement evidence. Existing gem/blessed-item receipt fields
retain their meanings. Capacity includes this extra gift and displaced off-hands.
A failed garment insert rolls back the entire rite; reconnect retry can then
complete once. Historical receipts remain null and are not retroactively paid.
The service's confirmation is transient; Sereth states the garment before consent.

Build and 401 first-rebirth checks pass, including actual hunted/crafted material
completion, permanent unbound identity and receipt, ordinary equipment use,
equipped reload, bound stone handling, exact bag capacity and targeted insert
failure/retry. Log: `obj/chi-refill-validation/first-garment.log` (ignored).
Earned wardrobe admission, title eligibility and reconnect now pass the follow-up
fixture below. Genuine-client reward presentation and deployment remain open.

The downstream second-rebirth regression also passes all 1,839 checks with the
new first-life garment present (`second-after-garment.log`, ignored).

## Earned wardrobe and five-star title acceptance

The ordinary first-rebirth fixture now earns a second actual garment, submits
`CoatStorage.Action.Addtowardrobe` twice, and verifies one saved wardrobe item,
unbound ownership, equipped state and Starlight Wings (60030022) after reconnect.
This uses the existing handler and title reconciliation rather than injecting
wardrobe ownership or a title bit. The loaded catalog reports five stars.

It exposed a production replay fault: the second request reset the same item's
slot, removed it, then passed slot zero back to `Equipment.Add`, throwing an
index exception. An already stored, equipped garment/mount now receives an
acknowledgement without unequipping itself. The single-argument equipment add
also rejects null/zero/out-of-range slots before indexing.

Build and 403 first-rebirth checks pass, including the new earned wardrobe/title
route. Log: `obj/chi-refill-validation/first-garment-wardrobe.log` (ignored).
This validates the successful ordinary transfer, repeat request and reload;
it is not a full failure-injection audit of every legacy wardrobe operation.
Native five-star/wing rendering and live deployment remain open.

### Same-character Starlight earning proof (2026-09-25)

The ordinary hunted Clean Water/crafted Heavenly Stone journey now continues
with its own reward garment through production unequip, wardrobe admission,
character/item/wardrobe reload and Starlight wing equip. This closes the earlier
gap where the wardrobe test used a separate rebirth character supplied with its
stone. The earned garment remains unique after reload. **406 checks pass**,
including three additional journey checks. Native rendering remains pending.


## Ordinary socket earning and creation-body compatibility (2026-09-27)

Actual earning found and fixed a body-choice rejection for newly created 6711
bodies 1006/2005. Both now use the existing gender-preserving size choices;
unsupported bodies remain rejected. The ordinary male 1006 career character
bought the existing 30-CP stone with earned funds and completed the whole NPC
rite. Level15/class11/rebirth1, 72 attributes, consumed material, three unbound
rewards and both achievement receipts persist across actual game/database
restart. 22 focused body-mapping checks also pass. [Evidence and limits](ROADMAP_STARTUP_ACCEPTANCE.md#first-rebirth-earned-through-the-ordinary-npc-2026-09-27).
This does not certify the separate material-crafting route, a female socket
career, native rendering or production deployment.
