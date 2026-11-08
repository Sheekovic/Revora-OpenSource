# Ordinary +composition

2026-09-26; implementation and isolated fixtures, not deployed.

Existing 2036 mode 0 now enters `EquipmentUpgrades.HandlePlus` before the legacy
handler. It validates exact length/seal/count, unique nonzero UIDs, canonical
ordinary-player readiness, target eligibility and owned inventory materials.
Eligible equipment and stones retain installed `StonePlusPoints` and
`ComposePlusPoints` values. Zero-enhancement equipment still supplies one point.
Mounts remain on the separate breeding route. Chance mode 5 is unchanged and is
not certified by this batch; native mode-0 acceptance is pending.

The item lock and database owner lock cover one transaction. Exact saved item
ownership, location, binding, stack and enhancement preimages are checked. Target
Plus/PlusProgress, consumed material units and receipt commit together. No cache
projection precedes commit. A database conflict or post-commit projection failure
fences the character for reconnect; ordinary item loading restores committed
state. Bound materials require a bound target. Materials after the +12 cap stay
owned; a selected stack contributes one unit per accepted request.

Startup creates additive `revora_plus_compositions` and its recent-request index.
Receipts store the requested material UID sequence, including unconsumed trailing
UIDs when the target reaches its cap. Identical target/material requests within
one second are suppressed durably, including after reconnect. There is no verified
client nonce, so this is bounded suppression, not arbitrary delayed-intent dedup.
Do not reset real player enhancement fields or migrate inventory for installation.
Deployment and its normal database backup remain pending.

Validation uses committed production sources plus this patch, preserving unrelated
working files. Build passes with legacy warnings (`obj/plus-composition-build.log`).
The full career passes 1,193 combined checks (`obj/earned-superman-tests.log`),
including 20 composition checks and eight new earning/title checks. A focused
rerun passes 426 combined checks (406 shared and 20 composition checks) and
additionally asserts fencing on each failure case. Do not sum overlapping runs. Coverage includes
concurrent stacked consumption, reconnect replay, bound payment rules, malformed
requests, invalid sessions, stale/foreign preimages, suppressed writes, a failure
immediately after commit, zero-enhancement credits and unused materials at cap.

The existing earned character buys twelve +8 stones for 314,928 boss-earned CPs.
Four stones raise each of its original headgear, necklace and armor to +12 while
retaining their Perfection 54. The ordinary wardrobe handler equips its earned
600,000-Prestige title. Full reload and all 23 online/offline categories agree at
615,587. [Career scope and controlled inputs](PRESTIGE_EARNING.md).
