# Equipment achievement rule gaps

## Current decision and implementation (2026-10-01)

The owner-decision and implementation-pending statements below describe the
September25 research checkpoint. They were superseded by the September27
[approved Revora rules](EQUIPMENT_SET_ACHIEVEMENTS.md): six core slots plus valid
equipped offhand, twelve Super gems for10428, and an actual complete alternate-set
switch for10432. All ten identities have implemented producers. Ordinary earning
and retained full-set cold recovery are documented in the roadmap; genuine-client
acceptance and a compatible320-achievement career remain unproven.

The amount-policy decision is also implemented in
[Equipment Enchantment](EQUIPMENT_ENCHANTMENT.md). Exact original6711 parity is
still unverified. Do not reopen these owner product decisions from the historical
notes below.

## Historical research (2026-09-25)

Reviewed 2026-09-25. The official guide at
https://co.99.com/guide/guides/achievement.shtml describes equipment achievements
but does not enumerate the slots required for a full set. The equipment FAQ at
https://co.99.com/guide/faq/items.shtml confirms the quality hierarchy, not the
achievement-specific treatment of a mixed-quality set.

The installed Achievement.dat (SHA-256
df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc)
identifies full-set achievements 10410–10417 and a twelve-gem milestone 10428.
It does not resolve optional offhands, bows/arrows, talismans, mounts, cosmetics,
alternate sets, or whether higher qualities satisfy lower-tier achievements.
Do not infer those rules from translated titles or player-authored tips.

Owner decision requested: retain the unresolved original rules, or adopt explicit
Revora full-set rules based on head, necklace, armor, ring, boots and a valid weapon
setup. No default has been activated. A custom implementation must document
class-specific weapon coverage, optional offhand behavior, quality thresholds,
socket/gem validity, plus/blessing/enchantment limits and ordinary earning routes.
The full-set milestones remain unchecked pending those requirements and validation.

Independent single-item milestones can proceed where their semantics are clear.
10430 observes enchanted equipment, not the act of spending an enchanting gem.
The current producer requires a saved active core equipment item with enchantment
1–255 (the server item field is a byte). It does not reward cosmetic/talisman
enchantment fields or malformed values. The enchanting transaction and a mined-material earning route are now validated
in ENCHANTING_TRANSACTION_AUDIT.md; gem-family amount policy remains pending.
