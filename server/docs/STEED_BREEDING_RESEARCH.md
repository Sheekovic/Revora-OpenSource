# Steed breeding restoration contract

Research baseline: `4ace95c`, 2026-09-20. No production change, no new achievement
producer, and no claim of native protocol verification.

## Requirement and earning route

The installed achievement catalog assigns 10112 to breeding a mount. Its hash is
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.
The producer audit currently marks this identity missing. Equipping a mount or
opening a steed pack is insufficient evidence of breeding.

The official [steed guide](https://co.99.com/guide/mount/steed.shtml), reviewed
2026-09-20, describes two-parent breeding, a maximum lineage level of twelve,
retaining a minor mount's lineage progress, and acquisition through saddle
exchange or CP vendors. It publishes these arrays:

| Meaning | Values in ascending level order |
| --- | --- |
| Level 0 through 11 advancement thresholds | 20, 20, 90, 240, 720, 2160, 6480, 19440, 58320, 2700, 5500, 9000 |
| Level 0 through 8 minor contributions | 1, 10, 30, 120, 360, 1080, 3240, 9720, 29160 |

It does not establish a numeric color mutation algorithm, level 9?12 minor
contributions or patch-6711 wire fields. Treat those as unresolved, not as zero
or a level-eight clamp. The [bound-item guide](https://co.99.com/guide/items/bound_cps.shtml)
restricts bound material to bound equipment; never let breeding turn bound
value into an unbound mount. These web references are published rule evidence,
not proof of the installed client's edition.

An existing source earning alternative is Elite PK: `ElitePkPacks` selects
720049 for its highest prize tier; `EliteNestedPacks` consumes that pack atomically
and creates item 300000 at +6. `ElitePrizeClaimTests` exercises claim through pack
opening and reload. Two independently earned prize mounts can supply a breeding
journey without inventing a vendor price. The original saddle route and current
vendor stock still require investigation; no store or price change is proposed.
The active item catalog contains item 300000. Its SHA-256 is
`1ed7a23b7a34b5e3706277f97e421e3cdf0b269eeb5be7367a4ebc0e4f22b3ef`.

## Inspected defects

`PacketHandler` sends packet 2036 directly to `ComposePlus`. `Compose` reads mode
at byte 4, count at byte 5 and successive UIDs from byte 8. Modes 2 and 3 are
named CurrentSteed and NewSteed. These are existing source interpretations;
there is no native capture establishing count, padding or mode semantics here.
The generic dispatcher does not provide composition-specific count/UID bounds.

`ItemHandler.ComposePlus` resolves a main inventory/equipment item and inventory
minors, but it does not require both to be steeds in modes 2/3. It rejects a
minor equal to the main UID but not duplicate minor UIDs. It neither holds the
owner item lock nor revalidates durable ownership before independent updates.
It does not enforce the complete bound/lock/storage/expiry/session contract.

Modes 2/3 reuse equipment `StonePlusPoints` and `ComposePlusPoints`. Those arrays
use 40 rather than 30 at minor level two and 80 rather than 90 for level-two
advancement. The minor's saved PlusProgress is ignored; levels above eight are
silently clamped to eight. Do not globally replace these arrays: equipment and
mount restoration must have separate contracts.

Mode 3 independently saves plus, progress, packed color and next color and
removes the minor twice when the main is below +12. At +12 it still executes the
color branch and final removal. Partial failure can leave a consumed material
with only part of its result saved. There is no durable breeding receipt or
achievement producer. The current 90/10 per-channel formula is legacy behavior,
not a verified retail algorithm or published random mutation rule.

Read-only evaluation against the actual arrays confirmed the level-two
mismatches and level-nine/twelve clamp. A +6 minor carrying 100 progress currently
contributes only 3240; the published additive interpretation gives 3340. That
last arithmetic is an interpretation of the guide and must be tested against
version evidence before claiming exact parity.

Source hashes inspected:

| File | SHA-256 |
| --- | --- |
| Network/GamePackets/Compose.cs | 756b1748ca54c33816144eb820019a2da489a863d1677d5283ca4af611fc022d |
| Network/GamePackets/ItemHandler.cs | 8a2844c017d0b3017e2f67ccedb92ee4b2b5c6b294e7ec65ec67af954465966b |
| Database/DataHolderTable.cs | e85e5ba6ac6a4ffd9b9902d216d3c3f772a2f0a2a51ac541f51362e6ee883fef |

## Implementation and acceptance sequence

1. Establish the installed composition request bounds and distinguish lineage-only
   and new-breed requests. Preserve source-derived versus native-verified labels.
   Identify client color/lineage configuration before adopting the legacy formula.
2. Add a mount-specific calculation contract without changing equipment helpers.
   Unsupported numerical cases must remain explicit gaps; do not silently consume
   materials using guessed contributions or substitute pack opening for breeding.
3. Under the canonical owner's item lock and database owner lock, validate all
   unique inputs, exact saved attributes, binding and container state. Commit
   minor consumption, every result attribute, a provenance receipt and the
   appropriate breeding achievement together. Publish inventory/stats only after
   commit, and fence on ambiguous failure. Preserve main UID and unrelated stats.
4. Intercept every mount-mutating compose mode so the old generic handler cannot
   bypass the restored contract. Check concurrent use/equip/trade/warehouse paths.
5. Validate an ordinary-player two-prize earning journey through real breeding,
   immediate inventory/achievement projection and production reload. Cover each
   supported level boundary, progress carry, binding combinations, +12 behavior,
   duplicate/replayed UIDs, malformed counts, stale ownership, concurrent requests,
   and suppressed writes at each mutation. Existing pack tests alone do not prove
   breeding or the complete two-prize journey.
6. Verify the genuine 6711 panel, preview/result colors, lineage, equipment stats
   and achievement display. Keep deployment and any client patch separately gated.

The next source batch is the owned breeding service and its earning tests, after
resolving the calculation/request seams above. This research is a prerequisite;
the mount achievement and full earning route remain unfinished.

## Approved-client static evidence and rules foundation

Follow-up to `3d582fb`, 2026-09-20. Read-only PE inspection used the owner-approved
x86 DX9 `6711/Env_DX9/Conquer.exe`, SHA-256
`3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60`.
Its sibling `Conquer.6711.original.exe` has the same hash. No executable was run,
patched, copied into Git or dynamically instrumented. Python pefile/capstone
were already installed. Addresses below are virtual addresses at preferred image
base 0x400000; they are metadata for this exact image, not generic hook offsets.

| Boundary | Observed fact | Confidence and limitation |
| --- | --- | --- |
| 0x54F14C / 0x54F18B, confirmation path 0x54F060?0x54F0AF | Original/new-breed confirmation selects request modes 2/3 | Static control-flow evidence; no live panel acceptance |
| 0x8BFE45?0x8BFEC2 | Constructor writes 2036, mode at 4, total UID count at 5, main UID at 8, first minor at 12, remaining minors from 16; declared length is 8 + 4 * count | Exact-image static construction evidence; transport seal comes from existing Revora framing, not this constructor |
| 0x879193?0x8791F9 | All twelve lineage thresholds match the published array, including 90 at level two | Exact-image numeric evidence |
| 0x87A994?0x87AA0B and 0x87AA0C?0x87AA35 | Mount-specific contributions are 1,10,30,120,360,1080,3240,9720,29160; a separate branch selects the equipment table | Exact-image numeric and selector evidence |
| 0x54B756?0x54B80B | Minor-selection predicate requires mount type and level zero through eight; it also checks an additional flag whose meaning is not yet established | High-level minor refusal is supported, not an invented missing-table workaround |
| 0x54D9E0?0x54D9F6; inverse at 0x54DC9C?0x54DCBF | Minor selection adds its contribution and saved progress; removal subtracts both | Static evidence supports retaining progress, superseding the earlier guide-only inference |
| 0x54CCD4?0x54CEAA | Color preview starts from the main packed color and folds selected minors in order using separate integer 90/10 channel truncation; no extra +1 in this routine | Static preview evidence; runtime color extraction/display and any server-side rare mutation still need acceptance |
| 0x54AF87 | The +12 display path returns 2,000,000 rather than another advancement threshold | Presentation evidence only; do not invent a +13 or infer final consumption/capping rules from this value |

The installed Cn_Res.ini has both original/new-breed prompts and a stale-looking
major-below-minor warning. Strings alone do not prove a restriction is active.
Its hash is `de05f60546fe635b9d3a37c5134761ddf27d5c21851058cac2e65e585ef76599`.
The observed selection predicate does not establish a major-versus-minor level
comparison; do not add one solely from that resource string.

`SteedBreedingRules` now provides the independently written numeric helpers and
bounded mode-2/3 request decoder. It accepts unique nonzero UIDs and at most 41
items (an equipped main and forty inventory slots). It rejects unsupported minor
levels, invalid RGB values and noncanonical minor progress instead of truncating
or overflowing them. These are input/number helpers only: they do not consume
items, initialize tables or award achievements and are not wired to a player
handler. +12 final progress, binding and full ownership remain service concerns.

Validation: isolated server build and **108 focused checks** pass. Tests cover
the independently evidenced arrays, saved progress, all 65,536 channel pairs for
range invariants, specific truncation/channel-order vectors, both request modes,
owned decode snapshots, equipped-main capacity, zero/duplicate UIDs, unsupported
modes, truncations, bad count/length/opcode/seal and extra bytes. The packet tests
use synthetic frames and do not constitute genuine-client acceptance.
Run `tests/SteedBreeding/Verify-Rules.ps1` to repeat the component checks.

This completes the calculation/decoder prerequisite, not M6.65b. The next service
batch must close ownership, atomic consumption/result/receipt, achievement,
ordinary two-parent acquisition and reload tests before adding a source producer.
No live change or deployment was performed.

## Service follow-up

The [owned service](STEED_BREEDING_SERVICE.md) now connects this foundation to
ordinary players, atomic parent/result/evidence writes and a complete two-prize
earning fixture. It also corrects item fields proven by the native parser:
Suspicious is word 38 and NextGreen is uint32 34, independently of PlusProgress
at 42. Native color mixing reads the minor's preview channels (object fields
0x230/0x234/0x238, filled by item bytes 31/33/34), not necessarily its packed
visible color. The service compares those saved preview values and records them
alongside the parent's visible color. The earlier no-dispatch statements describe
the prerequisite commit, superseded by this follow-up. Full parity/live gates
remain open; +12 retained-progress capping is an explicit Revora policy.
