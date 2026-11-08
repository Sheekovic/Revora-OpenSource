# Full-level rune achievement evidence

Implementation checkpoint: 2026-09-16 UTC. M6.46a research used baseline
`a244c90`; M6.46b now implements ten class-specific producers. This closes the
catalog identity ambiguity recorded in M6.7. Rune acquisition and native
acceptance remain separate unfinished work.

The approved client `ini/Achievement.dat` was hash-checked and decoded in memory
using the already-reviewed catalog codec. SHA-256:
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.
No client file was changed or copied to Git.

The following numeric records require five full-level equipped runes. Their
surrounding category headers identify the class, including the two identically
named Taoist rewards. The Pirate and Dragon IDs are not in profession order.

| Client record | Achievement | Category | Existing server class family |
| --- | --- | --- | --- |
| 50105 | 11312 | Trojan | 10-15 |
| 50006 | 11313 | Warrior | 20-25 |
| 50205 | 11314 | Fire Taoist | 140-145 |
| 50306 | 11315 | Water Taoist | 130-135 |
| 50405 | 11316 | Archer | 40-45 |
| 50505 | 11317 | Ninja | 50-55 |
| 50607 | 11318 | Monk | 60-65 |
| 50805 | 11319 | Dragon Warrior | 80-85 |
| 50705 | 11320 | Pirate | 70-75 |
| 51005 | 11321 | Windwalker | 160-165 |

Category headers are records 50099-50899 and 51099. The class-family association
uses the current server's `LocalPkRules.Families/Classes` mapping; the client
achievement file itself does not encode those server profession numbers.
Maximum rune levels are already reviewed in [RUNES](RUNES.md): red 1, blue 27,
yellow 9. The existing Chosen One wardrobe predicate uses those same maxima and
counts a complete primary or alternate set independently.

## Implemented behavior

`RuneLoadout.Evidence` validates the post-change persisted equipment against
exact owned session items, including slot, ID, binding, stack and EXP. Five
maximum runes must occupy distinct valid slots in one loadout. The saved class
must match the live class before the matching receipt is selected. Unrecognized
or unspecialized Taoist classes receive no class receipt.

Action 57 handles a genuine equip transition. Action 63 composition shares the
validator using explicit post-upgrade ID, EXP and binding, without changing the
cache before commit. Both paths lock the owner row before items. Final rune
progress, material consumption and the class receipt commit together. A failed
or silently suppressed receipt insert rolls back all changes unless the exact
receipt already exists. Failures fence the session for reload. Cached items,
skills, prestige and achievement/title reconciliation follow commit.

Composition now also requires a fully loaded canonical ordinary session and
checks exact target/material UIDs and the target's persisted stack. Both paths
recheck session eligibility before commit. Existing short-window composition
replay suppression remains bounded; it is not a new native nonce or indefinite
packet deduplication mechanism.

Historical class receipts survive unequip and reconnect; Chosen One remains a
continuous five-maximum-rune predicate. No receipt is inferred from login,
inventory visits or legacy client-authored achievement bits. Existing holders
can earn on a genuine equip transition. Proving a character can earn multiple
class receipts still requires legitimate rebirth/profession changes.

## Validation and outstanding work

Build passed. `-RuneLoadoutOnly` passes 129 checks (93 added): all ten class
families using matching class runes at installed minimum/promoted boundaries,
primary fifth equip, alternate
final +1-stone upgrade, concurrent replay, exact class selection, split sets,
four maxima plus one lower rune, historical reload, stale class, stale equipped
item data, fake/unloaded/noncanonical sessions, suppressed receipt inserts,
rollback of target and materials, recovery and already-earned idempotence.
Fixtures are ordinary non-GM, second-rebirth characters. They load the installed
spell catalog, including Warrior/Monk passive rune definitions; they do not
substitute Trojan runes for other classes. A synthetic Screen lacks the native
timer-disposal lock, so the failure fixture contains that known disconnect
exception only after asserting the session is fenced; production storage
rollback is still exercised and checked.

Related suites pass: rune composition 21, titles 91, progression 74, default
boss/Potency 216, prestige 58. Total: **589 gameplay checks**, plus three
producer-audit tests. Source coverage is now 167/337, with 170 missing producers;
this is not proof of a playable per-character route to 320.

Validated server SHA-256:
`16D2E5E530C8C32A023AEC0107B7542BD930230C4CA18554BD9C1DC2E1EF2104`.
No live deployment or client patch is included. Native skill/title/achievement
display remains open. The subsequent [rune admission implementation](RUNE_ADMISSION_RESEARCH.md)
enforces class/level/rebirth and distinct-yellow prerequisites with additional
recovery and active-effect checks. [M6.50c](RUNE_ACQUISITION.md) subsequently
validated a complete boss-RVP and purchased-material server route to the equipped
title; native acceptance and acquisition-cost review remain open.

## Retained all-class earning journey (2026-10-01)

The current candidate catalog was checked against the production rune admission
predicate for all ten classes, including each blue rune's maximum item ID. The
same three already-owned maximum yellow runes are legal for every class. Each
class needs its own red and blue pair, priced at 1,000 and 750 RVPs. The nine
pairs missing from this retained Archer's career therefore cost 15,750 RVPs,
before CP material, Oblivion Dew and reincarnation expenses. These costs use
existing saved currency; this batch does not prove its entire prior provenance.

The candidate's full bag and two equipped offhands require preparation. Normal
trade moves one unbound 350001 accessory and five stored unbound weapons to an
ordinary peer. Five bound P7 souls move into the resulting warehouse spaces,
leaving six bag slots free. All traded possessions remain saved on that peer;
no inventory row is discarded or injected. Cosmetic collections are retained.

Each bounded earning phase changes to one missing class through Sereth's paid
reincarnation dialogue and request. Purchased EXP balls raise the character to
its normal recovery threshold, restoring the saved level 140. The phase buys
that class's two runes from Vesper, equips them through the ordinary rune
packets and purchases +Stones to raise the blue rune to level 27. The red rune
is already terminal at level one, and the three yellow runes remain level nine.
Every consumed stone packet is replayed to check that it has no second effect.
The prior class runes remain owned in the rune bag.

Each class is followed by a fresh process comparing saved items, profile,
skills, proficiencies, paid reincarnation history, achievement receipts, guild
arsenal and the peer's items. The fixture checks earlier earned class receipts
remain present. Reincarnation intentionally applies the ordinary equipment
level and skill/proficiency transition rules; unchanged gear levels or an
unchanged profession are not claimed.

- [x] Trojan, Warrior, Ninja, Monk, Pirate, Dragon Warrior, Water Taoist,
  Fire Taoist and Windwalker each pass earning and a separate cold reload:
  18 process phases in total. The earlier earned Archer receipt is retained.
- [x] All ten class-specific receipts coexist on the same retained UID.
- [x] Each new blue rune consumes 106 purchased +Stones (954 total); each class
  uses 232 purchased EXP balls (2,088 total) with controlled daily clocks.
- [x] Final saved character is level 140, twice reborn, class 161. Prior class
  runes stay owned; no fabricated class-promotion receipts are added.

The first preflight compilation failed on an unqualified generic List name.
After qualification it passed; no candidate player operation ran in that failed
compilation. All subsequent earning/reload phases passed without a recovery
repair or repeated payment.
Evidence lives in tools/CheckCandidateCareerRites.cs and the private
retained-rune-class-N-earn.log / retained-rune-class-N-reload.log files.
Production game SHA-256 used by the private probe:
07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2.
Only the fixture and documentation change. No game rebuild or live deployment.
Proximity and purchase/daily-consumption clocks are controlled; native client
interaction and naturally elapsed daily limits remain outside this evidence.
