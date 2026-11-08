# Mastery weapon supply checkpoint

Reviewed 2026-09-27 at `36810792`. `tools/CheckMasteryWeaponSupply.cs` uses the
real item and merged shop loaders without starting the server or opening a
database. Its [numeric output](research/MASTERY_WEAPON_SUPPLY.csv) covers all
33 weapon families in the mastery predicates; barehand identity 0 needs no item.
It records catalog counts and a lowest-proficiency/level positively priced stock
example, including class and proficiency requirements. A stock row is not proof
of an accessible NPC, successful purchase/equip or the full training journey.

27 families have positively priced stock. Six have none in these loaded shops:

| Family | Achievement | Other source evidence |
| --- | --- | --- |
| 614 | 11214 | Restored Trojan Epic final claim; earned process route documented |
| 616 | 11215 | Restored Ninja Epic katana conversion; purchased input and restart verified |
| 622 | 11223 | Restored Monk Epic beads conversion; purchased input, hunted flowers and restart verified |
| 620 | 11222 | Restored Taoist Epic conversion; normal startup, purchased input, hunted flowers and exact reward restart verified |
| 671 | 11322 | OracleoftheSea consumer only; no ordinary producer established |
| 670 | 11323 | Same two-output OracleoftheSea consumer; no ordinary producer established |

The two pirate families remain **supplier-unproven**, not missing achievement producers.
Do not seed their proficiency records and count that as an ordinary career.
The paired pirate pack's legacy consumer also loses its second output with only
one bag slot available; adding it to stock would expose that defect. The existing
closed epic seller is not an ordinary supplier. See [the pack audit](EPIC_WEAPON_PACKS.md)
and [the Taoist restoration contract](TAOIST_EPIC_RESTORATION.md).

This classification reduces the current unproven career planning pool from 325
to 323, leaving three candidates beyond the 320 target. Taoist startup activation
restores its supplier evidence only; full mastery training is not certified. It does not prove that
323 are compatible, obtainable or live. No stock or prices changed.

The probe compiled x86 with warnings as errors and ran against candidate SHA-256
`D6EEE3E24C517266D0BAAD98E40BDF909AD7B1AEDA2D1DB4EC30A308585B32D0`.
Compile against that private build using the framework C# compiler, referencing
Revora.exe and System.Core.dll; run with the candidate game directory as working
directory. Raw output is `obj/mastery-supply.log` (ignored). No credentials or
proprietary catalog contents are copied into Git.

Reviewed runtime inputs:

| Relative path | SHA-256 |
| --- | --- |
| `database/itemtype-new.txt` | `1ed7a23b7a34b5e3706277f97e421e3cdf0b269eeb5be7367a4ebc0e4f22b3ef` |
| `database/ItemtypeSub.txt` | `6e318b1e53b0fb28036bd547b5cb78914e3e66b1272d8054259b6b0bcaa66aea` |
| `database/shops/Shop.dat` | `9cbf67cf0edf03a781a50d50f581544878ba352a3019cc91cb14b411f21552f3` |
| `database/shops/emoneyshop.ini` | `3549e60a10e4391508f6731932850102565b8a3239ea60b241975394ebbda3c9` |
| `database/shops/emoneyshopV2.ini` | `0f81b11689546fee470c04ce5114778b8e2e35b1b0378087743c8d30a4dc56d6` |


## Pirate supplier reconciliation (2026-09-27, source98f751e8)

The earlier Oracle-only description is historical. The private Pirate quest now
purchases ordinary weapons, earns Hearts through the paid-map and flower paths,
and awakens the same weapon UIDs into rapier671004 and pistol670004. The fixture
checks receipt ancestry, enhancement preservation, bound P7 awards and exact
reload after a lost successful-COMMIT reply. Existing evidence:
`obj/pirate-search-commit-loss-process.log`, two passing phases on gameplay hash
`9B72D1C220424D314A9C2E9879B98EED6C063C294C202F642CA2E1AAB1800B33`.
This audit does not rerun or extend that older test result.

Ordinary startup remains the gap: `CheckCandidatePirateLifecycle.PirateStore`
explicitly calls the store initializer, world map preparation, world initializer
and travel initializer. `Server.cs` prepares Monk and Taoist maps, but has no
corresponding Pirate startup call. No Pirate initializer call was found in the
production Game/Server/Thread sources. Thus the absence of an Oracle pack seller
is no longer the implementation target; the next work is guarded normal-startup
integration of the existing earned quest, followed by ordinary access validation.

11322/11323 remain supplier-unproven for the normal-startup career inventory.
Do not add two earned mastery identities merely because private conversion
works. Terrain identity, ordinary access, native object collision and coordinated
deployment remain distinct gates. The old CSV remains the dated loaded-shop
snapshot, not a complete inventory of later private quest conversions.


## Pirate normal-startup supplier verified (2026-09-27)

Normal startup now initializes the Pirate world/store/travel route, and the
existing ordinary earned-input, two-weapon awakening and restart fixture passes
without helper-only activation. [Evidence and scope](PIRATE_EPIC_RESTORATION.md#normal-startup-and-earned-supplier-2026-09-27).
This supersedes the missing-startup classification above. Families671 and670
have an ordinary earned supplier; achievement11322/11323 mastery training remains
unproven. Remove only their supplier exclusions from the career planning audit:
the unproven pool becomes325, not an earned325-achievement character. Native
collision, authenticated journey and live deployment are still pending.


## Pirate combat mastery verified (2026-09-27)

The later [earned-weapon combat fixture](PIRATE_EPIC_RESTORATION.md#earned-weapon-combat-mastery-and-persistence-2026-09-27)
now earns11322/11323 at proficiency12 through actual production melee damage,
then verifies exact XP/level persistence and duplicate observation after restart.
This supersedes the unverified Pirate training statement above. Supplied fixture
eligibility, enhancements, proximity and cooldown advancement remain explicit;
no mastery XP/levels or damage were supplied. Authenticated/native acceptance and
the compatible320-achievement career remain unproven; planning counts are unchanged.
