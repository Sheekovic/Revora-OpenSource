# Ancient Devil collection achievement

Source implementation, 2026-09-20. Achievement 11020 now has a transaction-backed
producer at the legitimate amulet collection boundary. It does not require a
summon, boss death or reward claim. Production quest activation and native-client
acceptance remain open; this change is not deployed.

## Installed evidence

The installed encrypted `ini/Achievement.dat` has SHA-256
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.
Its row uses UI identity 60303, achievement identity 11020 and score 10. The title
is “United we stand!” and the description concerns collecting Ancient Devil
amulets. The score is not an item count. The achievement description itself does
not state a threshold; using nine distinct classes follows the installed amulet
quest requirement. This interpretation is explicit, not a recovered retail
completion script.

The read-only Ancient Devil audit now hash-checks this catalog, round-trips its
codec and checks the unique numeric row. It reports numeric facts without copying
the client catalog into Git. Ten audit tests pass, including missing, duplicate
and changed achievement row rejection.

## Earning and persistence

After a class certificate exchange or an ordinary trade changes saved item
ownership, the same transaction checks the resulting inventory. Nine distinct
recognized class amulets must join their real completed trial exchange records.
Items must be in the bag, unbound, unlocked, non-suspicious, permanent and single
units. Duplicate classes and catalog items without earning provenance do not
satisfy this rule. The completion does not consume the collection.

One collection header per character and nine immutable item/type/origin receipts
are written with the achievement evidence. Receipts retain the original trial
identities after the items are traded again or consumed by a summon. The normal
achievement/title refresh occurs only after the earning transaction and runtime
inventory installation succeed. If completion cannot be saved, the final trade
or certificate exchange rolls back. Earlier earned evidence is never removed
when the player later spends or transfers the amulets.

The producer remains disabled until the Ancient quest initializes its tables.
It has no arbitrary client completion endpoint or GM/account-name requirement.
It does not import old achievement bits or backfill unproven catalog items.

## Validation

The isolated build passes. The combined seal route passes 196 checks; the summon
route passes 200, including retaining achievement evidence after offering
consumption and expiry. The shared Summer trade regression passes 19 checks.
Ten Ancient catalog/terrain tests and three achievement producer-audit tests pass;
the installed-data audit also succeeds. Producer inventory is updated to 238/337.

The combined Ancient Seal fixture earns the collection through real class trials
and ordinary trades. It checks no award at eight classes and immediate completion
at the ninth trade, then production achievement/title login reload. A second
ordinary character receives eight earned classes and obtains the ninth through
actual guard combat and certificate exchange. Later transferring those amulets
does not erase completion.

A suppressed achievement insert rolls back the final trade and all new collection
receipts; reconnect and retry succeed. Trading nine fixture-created catalog items
without trial provenance grants no collection achievement. The complete seal
route still pays, fights and claims rewards independently. This is server evidence,
not native notification/rendering or production Host acceptance.

## Retained Monk trial checkpoint (2026-10-02)

The ordinary retained character 4294907001, already a level-140 Monk through
paid reincarnation and purchased EXP balls, entered through the Bird Island
Exorcist and Warrior Guard. `career-retained-ancient-trial` defeated the private
trial monster through `Attack.HandleAttack` with calculated ranged damage,
claimed the bound Monk certificate 711335, and exchanged it for one unbound
Monk amulet 711336. The exchanged trial references that exact item UID.

`career-retained-ancient-reload` passed in a fresh process, comparing the saved
career and trial snapshot before replaying certificate/exchange requests.
Neither replay created another item. Achievement 11020 remained absent: one
amulet must not satisfy the nine-distinct-amulet collection requirement.

Both phases passed against private game SHA-256
`34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`.
The fixture controls proximity and attack cooldown, reuses previously earned
gear with mixed funding provenance, and does not exercise native client input.
This checkpoint does not complete the retained collection: eight other class
amulets and their ordinary trade/provenance path remain to be verified. Older
donor tests used injected damage and are not upgraded to calculated-combat
proof by this result. No production deployment or client release occurred.

## Retained nine-amulet collection (2026-10-02)

The follow-up `career-retained-ancient-trades-*` fixture uses the existing
ordinary donor characters for Warrior, Trojan, Fire Taoist, Water Taoist,
Archer, Ninja, Pirate and Dragon Warrior. Each earned a **fresh** amulet through
its guard trial, calculated melee attacks through `Attack.HandleAttack`, bound
certificate claim and certificate exchange. These are not the amulets from the
older injected-damage trials. The retained Monk supplied its own ninth type.

After restarting the game, reciprocal sealed trade requests, item selection
and both parties' acceptance transferred the eight exact earned item UIDs.
Achievement 11020 was absent after the first seven trades (eight types total)
and present after the eighth (nine types). Another cold reload compared the
complete saved snapshot, verified nine distinct origin trials joined to their
exchanged amulets still owned by the recipient, and checked the achievement
packet bit. No reward or achievement was inserted by the fixture.

The first donor run failed after its successful exchange while saving through
mutable `Server.GamePool`. Checkpoint saving now uses the explicitly loaded
participants. Before resuming, a read-only full snapshot diff found only one
new donor item 60954906 and its exchanged trial; the career snapshot was
unchanged. A temporary exact-hash recovery accepted that already committed
exchange without repeating combat or reward delivery:

- Prior: `37ed574a3298d0a9fbc022168e283710aee40ef3994df59284ea86da03e43115`.
- Current: `94c67335568a3a82a99f04236f90462bb03d3418cf1ca8958ff7e57e78de5b25`.

The remaining donor trials, cold trade phase and reload passed. The recovery
branch was removed; the final fixture then passed another cold reload. This
records a recovered initial failure, not an uninterrupted first-pass run.

Donor starting classes/level100, controlled proximity and attack cooldowns are
fixture inputs. Stats are loaded through the ordinary item-stat loader; no
lethal damage is injected in these new trials. This proves the collection and
trade persistence path privately, not donor leveling, native input or the
complete compatible achievement career. The private game binary is unchanged
from the preceding checkpoint; no live deployment occurred.
