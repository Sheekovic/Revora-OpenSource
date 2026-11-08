# Stocked mount-pack earning

2026-09-27. The 17 already-stocked packs 3000254 and 3000256 through 3000271
now use `FixedOutputPacks` for both ordinary item use and direct `UseItem`.
Their legacy switch branches fail closed. Prices and shop stock are unchanged.

The saved-owner transaction consumes exactly one source unit, inserts the mount
and writes `revora_fixed_output_pack_uses` together. The output keeps its original
permanent, unbound, +0, -1 specification and catalog durability. The existing
fixed-output receipt constraint includes these 17 identities; other pack contracts
retain their prior behavior. Blessing is now explicitly persisted for fixed outputs
(zero for existing non-mount gifts). In-memory changes happen only after commit;
an uncertain/failing result fences the session for reload.

## Ordinary earning and process validation

Run `tools/Verify-ArenaProcess.ps1 -MountPacksOnly`. It uses the existing marked,
disposable database clone and three separate game processes:

1. An ordinary character earns 1,000,000 CPs through the daily boss reward path
   and purchases all 17 packs through mall 2888 at 2,799 CPs each. A suppressed
   use receipt forces rollback: the source pack remains and no mount is saved.
2. After database/game restart, all 17 packs open through `ItemHandler.UseItem`.
   Eight concurrent calls per singleton produce exactly one reward/receipt.
   Each mount retains -1, unbound and permanent metadata in memory and storage;
   each deposits through `CoatStorage`, including duplicate deposit requests.
   The normal achievement snapshot records the ten-mount milestone.
3. Another game process loads the same character, exact 17 wardrobe item UIDs,
   17 opening receipts, ten-mount achievement and remaining **952,417 CPs**.

The final three-process run passed. Build completed with existing legacy warnings.
Executable SHA-256:
`2CE8DF4B6824E702D03B821765B8E2AF105986C294841A02C2B224D9058BC944`.
Logs: ignored `obj/mount-pack-build.log`, `obj/mount-pack-process.log` and the
candidate's redacted `arena-process-mount-{buy,open,verify}.log` files.

Two fixture assumptions were corrected before the passing run: the boss earner
must first load its persisted profile, and wardrobe invalidation needs the normal
achievement snapshot before asserting evidence. No production behavior was changed
to accommodate either fixture. Failed clones were inspected and replaced only
through the exact ownership-marker check. The passing clone was removed and its
private PostgreSQL process stopped.

### Capacity and ownership follow-up

The expanded three-process suite also passes these boundary checks on the same
assembly, without another server build:

- A bound two-unit pack is refused in a 40-slot bag without consuming a unit or
  writing a receipt. After freeing one slot, it grants one mount, retains one
  source unit and returns to 40 slots. Opening the last unit still fits, and a
  stale-object replay grants nothing. Both outputs preserve the original unbound,
  permanent, -1 rule; exactly two use receipts exist.
- A singleton pack in a 40-slot bag consumes its own slot and grants one mount.
- Empty, 11-byte, 31-byte and oversized frames, incorrect declared length,
  packet type, action and seal leave the source unchanged and grant nothing.
  These malformed inputs exercise the owned service boundary; valid uses exercise
  the normal `ItemUsage.Process` path.
- Passing another character's item grants nothing. A saved source stack changed
  behind the session fails the conditional write, preserves the actual stack,
  creates neither mount nor receipt and fences the stale session for reload.

These checks use three additional ordinary accounts with explicitly supplied
inventories in the disposable clone. They are fault fixtures, not new earned
collection evidence. The original boss-funded character and its 17 exact item
UIDs still pass the earning/restart checks in the same run. Log:
`obj/mount-pack-boundaries.log`. The fixture was removed and private PostgreSQL
stopped after success. No production code changed in this follow-up.

## Limits and remaining work

This proves a real server-side paid pack route; it does not prove 50 mounts or
320 achievements on one character. The combined 45-mount supplier ceiling still
requires other acquisition paths. The eight unstocked legacy mount-pack cases are
unchanged. The boundary checks above exercise representative newly owned pack
identities; they do not exhaust every possible stack size, malformed frame or
transport fragmentation/coalescing behavior.

The test uses a supplied level/class profile, controlled lethal combat and normal
server handlers, not authenticated native-client interaction. It verifies the
normal mall purchase, not purchase crash/retry guarantees for the legacy generic
shop handler. Native rendering, immediate client display and live deployment remain
pending. Existing owner work on shop payments and source binding remains separate
and untouched; only this batch's changes were staged from the dirty shared files.
