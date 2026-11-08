# Owned mount breeding

Source implementation, 2026-09-20. Not deployed or accepted in the genuine client.
The [research contract](STEED_BREEDING_RESEARCH.md) records the approved executable,
static instruction boundaries, published requirements and unresolved parity gates.

## Ordinary player behavior

The existing mount breeding panel's keep-breed and new-breed requests now use one
owned transaction. The main mount may be in the bag or equipped; each minor must
be a distinct owned inventory steed at level 0?8. Locked, suspicious, temporary,
stored, stacked and wardrobe items are rejected, as are trade, booth, poker,
dead, fake, stale-session and persistence-fenced callers. Bound minors cannot
improve an unbound main. Unbound minors may improve a bound main.

Both choices add every minor's level contribution and saved progress. The new-breed
choice folds colors in request order using the main's visible packed color and
the minors' transmitted preview channels, matching the inspected client routine.
The keep-breed choice retains the main color. Neither charges extra currency.
The main UID, binding, position and unrelated saved attributes are retained.

Lineage thresholds and minor contributions use separate mount rules, leaving
equipment composition values unchanged. At +12, retained progress is capped at
2,000,000 as explicit Revora policy based on the client's displayed scale; this
is not verified retail server capping behavior. A request with no remaining
lineage or color effect consumes nothing. Random rare mutation behavior is not
claimed restored. Noncanonical historical progress is rejected, not silently
rewritten. Remaining original saddle/vendor acquisition is tracked separately.

Actual parent consumption awards achievement 10112 for either breed choice.
Merely opening a pack, equipping a mount or sending an invalid/replayed request
does not qualify. This follows the catalog's breeding requirement and the two
native breed choices; it is not a claim of captured retail achievement timing.

## Atomicity and entry points

The public ComposePlus entry holds the owner's item lock and delegates mount
modes before legacy equipment code. Other compose modes selecting a mount are
rejected. The old mount mutation branches were removed. Both the packet
handler and direct ComposePlus callers therefore share the same protection.

Startup creates `revora_steed_breedings` and `revora_steed_parents`. The transaction
locks the database owner, compares saved item ownership, containers, binding,
lineage, visible/preview colors and restrictions, then saves the receipt,
consumes every minor, updates the main and inserts achievement evidence. Every
required write checks its affected-row count. A consumed minor has one unique
parent receipt, and repeats cannot consume another item in its place.

Only after commit does the service remove client inventory objects, project the
updated item, refresh equipped stats and refresh achievements. The projection
sends the committed item frame directly: legacy ConquerItem.Send can otherwise
write NextSteedColor after commit. Failed persistence fences the player for reload;
login recovers the durable item and achievement through existing loaders.

## Shared item-layout repair

A high-progress test exposed Suspicious aliasing byte 44 inside the uint32
PlusProgress at 42. Loading the saved boolean could erase lineage bits, while
writing progress could falsely mark a steed suspicious. The approved 6711 parser
reads the separate flag as a word at 38. The property now uses that field.

The same parser reads the next green channel as uint32 at 34; Revora incorrectly
put it at byte 37, its high byte. NextGreen now writes the correct field. Exact
static boundaries are in [protocol research](PROTOCOL_RESEARCH.md). No proprietary
binary/disassembly was committed, and no live client files were changed.
Historical values already damaged by old saves cannot be reconstructed from the
current row alone; audit trustworthy history before any production repair.

## Validation

- Isolated server build and 108 calculation/decoder checks pass.
- 63 focused breeding checks cover both modes/bindings, all advancement boundaries,
  higher-level minors, retained progress, +12 cap/no-effect protection, equipped
  main reload, independent item fields, native preview channels, mixed binding,
  ordered multiple parents, concurrent replay, stale ownership and rollback after
  suppressed receipt, parent receipt, first/final consumption, main and achievement
  writes. Production reload retries each failed operation.
- 587 combined earning/Elite prize checks pass. One ordinary level-140 character
  wins two actual eight-player brackets on controlled weekly dates, claims both
  prizes, opens both packs, deposits first-week non-mount rewards through the
  warehouse to free inventory space, breeds the two earned mounts and reconnects.
  Both parents join back to saved pack openings, prize claim item UIDs and champion
  placements. Starting characters and controlled combat/dates are fixture inputs;
  neither breeding parent is injected. This is not real-time difficulty testing.
- Shared regressions pass: 23 rune checks and 444 housing/storage checks. Three
  achievement-audit tests pass; source producer coverage is 237/337, not a verified
  per-character route to 320 or an overall completion percentage.

The earning test also corrected its prize-desk fixture to use a walkable standing
point and its reload helper to run normal HP calculations before re-entering a
healed arena. These changes do not alter tournament rules or grant eligibility.

Run `Verify-PotencyHunt.ps1 -SteedBreedingOnly` or `-SteedEarningOnly` under
`tests/PotencyHunt`; logs are private under `obj/chi-refill-validation/steed-*`.
The latter includes the complete existing Elite prize regression prerequisites.

Before release, validate the real panel, ordered previews, equipped stats,
achievement display, warehouse item display and +12 behavior in genuine 6711.
Back up and review production DDL and existing suspicious/lineage data first.
Preserve breeding receipts on rollback; do not restore the legacy partial writer
against a deployed ledger. No production DDL, restart, deployment or public patch
was performed in this batch. M6.65b's full parity gate and M6.65c stay open.

## Retained prize parent and equipped breeding (2026-10-01)

The retained character's saved Elite PK champion placement `epk-20350105-3`
was still unclaimed. The private clock is set inside that existing entitlement's
claim window. It is not extended or rewritten. Earlier placement earning used a
two-player bracket with controlled damage/death and receipt rollback/retry;
this batch does not relabel that fight as normal-combat evidence.

Ordinary deposits into the existing Stone City warehouse free room for the full
prize. The Elite envoy (448582, options 6/7) pays exactly 5,000,000 CPs and the
saved champion recipe, with zero EXP because the character has advanced beyond
its signup level. The unbound 720049 pack opens through ordinary item use into
one unbound +6 steed. Its opening receipt joins directly to the paid prize's
reward UID array, preserving material provenance.

A sealed keep-breed request (2036, mode 2) consumes that +6 parent into the
character's equipped +12 steed, which was purchased through the Mount Kit route.
The main UID, binding, equipped position and colour remain; its lineage progress
increases by exactly 3,240. One parent receipt and achievement 10112 are saved.
The consumed parent cannot be reused, and the prize cannot be claimed again.

A fresh game/database process restores the exact career, claim, nested opening,
breeding and parent snapshot. Repeating the consumed-parent packet and the prize
NPC claim leaves it unchanged. The material origin join is checked again after
reload. No rewards are injected or deleted to prepare space; only the explicitly
selected breeding parent is consumed by the normal service.

Calendar, NPC approach and travel are controlled. This is equipped keep-breed
and persistence evidence; colour blending, all cap/binding boundaries and native
panel presentation retain their separate evidence/gates. No production code,
live restart, deployment or client patch changed.

Validation: `career-retained-breed-earn` and `career-retained-breed-reload`
passed on candidate SHA256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
