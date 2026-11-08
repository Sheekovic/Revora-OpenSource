# Gold Prize to Gold Trophy conversion (M7.2c)

An earned seasonal Gold Prize becomes a bound, permanent Gold Trophy when its
recorded season ends. Conversion changes item type 2100075 to 2100085 in place:
the UID, owner, inventory/equipment/warehouse location and other item properties
remain intact. It needs no free bag slot and pays no additional CPs.

This follows the replacement rule documented in [the season research](HOLY_GRAIL_RESEARCH.md).
Only the precise item recorded by an earned Gold Prize exchange qualifies.
Unrecorded legacy/shop prizes and Bronze/Silver rewards are untouched. A missing,
transferred, timed or otherwise ineligible item does not mint a replacement.

## Online and returning players

A bounded online poll processes at most 32 due owners per minute and rotates its
UID cursor. Busy inventory locks, active trades and booths defer processing.
The inventory object changes immediately after commit; equipped conversion also
recalculates item/HP statistics and sends status/prestige updates. A stored trophy
keeps its warehouse object and appears through the next ordinary warehouse list
or withdrawal. No extra inventory object is created for a stored item.

Offline characters convert in their owned pre-load transaction when they next
log in, before inventory, equipment or warehouse objects are read. Offline rows
are not mass-rewritten by the world poll. Account/character identity and the
canonical session must agree. Fake players are excluded.

Native warehouse processing now shares the character's inventory lock, so a
deposit or withdrawal cannot race conversion and resave the old item type.
Conversion preserves the original lock state: it does not unlock protected gear.

## Persistence and recovery

The transaction locks the character, prize receipts and exact owned items. The
item type update and `rolled_at`/`trophy_uid` receipt must both affect one row.
Failure rolls back the whole conversion and fences the session for reload.
The receipt records the same item UID as the permanent trophy's earned provenance.

Conversion precedes runtime projection. If the database commit succeeded but no
item was loaded, a fresh login reads the already-converted trophy without another
grant. More than 1,000 due items for one owner fails closed before mutation.
This bound is separate from the 32-owner poll limit.

## Validation

Final staged build SHA-256:
`7065B915C8CDA625C0BCDD8B9F156328E665EA08DEB9B35E2AC1D77A964CD5B7`.

The guild-season suite passes **131 checks**: the previous 104 checks plus 27
rollover checks. Each rollover fixture earns its prize through three actual
scheduled battles, leader claims and the NPC exchange. These are ordinary
non-GM characters in a disposable schema.

Coverage includes the exact boundary, suppressed item/receipt writes online and
during login, trade/session exclusion, equipped stats recalculation, a full bag,
warehouse withdrawal, concurrent native warehouse moves, the scheduled poll and
normal offline inventory/equipment/warehouse loading. Repeated reload and a
committed conversion with deliberately absent runtime projection remain single
grants. The latter models missing acknowledgment; it is not a full Host restart.

Related final-build suites: refinery 174, progression 74, Potency/daily bosses/
Extraction 216 and combat 669. Total: **1,264 checks**.

## Remaining M7 work

M7.2d corrects the [five cup stat contributions](GUILD_CUP_STATS.md), including
the legacy flat additions and extra Gold Prize percentage.
[M7.3](HOLY_GRAIL_CRAFTING.md) adds twelve-trophy Grail crafting and both wardrobe
awards. [M7.2e](GUILD_WAR_PURSES.md) adds atomic regular Sunday rank purses. Native inventory/equipment/warehouse display acceptance is also pending.

No live restart, deployment, public patch or production-row mutation occurred.
