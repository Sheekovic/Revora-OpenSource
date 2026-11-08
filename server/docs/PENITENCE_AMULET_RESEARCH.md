# Penitence Amulet earning-path prerequisite

2026-09-20, research baseline `20d4228`. The ordinary use service is now
implemented in source; see the final section for validation and remaining
earning/native gates. Nothing in this batch is deployed.

## Evidence

The [Magnolia guide](https://co.99.com/guide/quest/2012/Magnolias_All_Around.shtml)
includes a Penitence Amulet among Elite-grade bonus examples. It supplies neither
an item ID nor complete bonus selection probabilities. Restoring usable amulets
is a prerequisite to that reward chain, not proof of Magnolia issuance parity.

The installed client and server catalogs agree on IDs **720128 and 723727**,
both named PenitenceAmulet. Both descriptions specify a 30-point reduction,
a one-hour interval, a half-hour Deity interval and a VIP3+ exception. The first
explicitly requires at least 30 PK points; the existing handler applies that
minimum to both. The second also refers to Collector Zhao, which does not prove
that collector's admission or reward contract.

The [official FAQ](https://co.99.com/guide/faqs.shtml) supports the 30-point
reduction. The [patch 6680 notice](https://co.99.com/news/2018-02-06/patch_6680__power_of_mystery_runes_expansion_and_new_server_mythicalrunes_.shtml)
and [Deity Land guide](https://co.99.com/guide/quest/deity_land.shtml) identify
Deityland as a PK-enabled hunting area with a 30-minute amulet interval. This
places that rule before the target 6711 patch. These pages do not resolve whether
the VIP exemption overrides Deityland's special interval. Do not infer a map ID
or exception precedence from a name match.

`python tools/audit_penitence_amulets.py` verifies both catalog hashes, decrypts
the client data with an exact encrypted round trip, compares item identity and
requirements, and checks the four rule phrases. It prints only hashes, IDs and
derived contract values, never proprietary catalog rows. Validated hashes:

- `6711/ini/itemtype.dat`: `d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a`.
- `bin/Debug/database/itemtype.txt`: `594ffe5f252c9445a6087004372519a6f150f089c7bff42d4ec30400911a0e88`.

The existing Warrior catalog/terrain audit also passes after its row reader was
generalized to accept an explicit set of item identities. No active assets or
database rows changed. Catalog agreement is direct evidence; native consumption,
cooldown display and historical exception precedence remain unverified.

## Baseline defects and implementation requirements

`ItemHandler` removes an amulet and then subtracts 30 from the in-memory PK
balance. It has no cooldown, durable receipt or atomic item/balance operation.
`Entity.PKPoints` projects the numeric update and red/black-name flags, but does
not itself persist the balance. Ordinary autosave later writes PK points.

A replacement needs a durable per-character use record shared by both item IDs,
not a per-item or per-session timer. Under the character owner lock, one database
transaction must lock the character, verify the exact source item and saved PK
preimage, check eligibility/cooldown, consume one item unit, persist the reduced
PK points and insert the use receipt. The runtime balance and name flags change
only after commit. Stacked items, full bags, binding, timed items, stale ownership,
replay, reconnect and failed writes must preserve the whole transaction.

PK mutation ownership is a prerequisite: the five-minute decay in `Thread`,
player-kill increments in `Entity.DieCore`, administrative setters and character
saves currently reach the same property. Locking only amulet use cannot prevent
read-modify-write races with those paths. Coordinate increment/decrement operations
and durable preimages rather than overwriting a new kill penalty or allowing an
autosave to restore the pre-amulet balance. Keep administrative authorization
unchanged; ordinary eligible players must be able to use the item.

Tests must cover 29/30/99/100 PK boundaries and projected name transitions,
alternating aliases, one hour exactly, backward clocks, reconnect, VIP expiry,
failure of each required write, concurrent decay/kill/save/use, and source
consumption after an ordinary earning route. Locate and verify Deityland's actual
runtime identity before implementing its exception. Do not mark Magnolia bonus
issuance or the amulet use service complete from this research checkpoint.

## Shared PK mutation prerequisite implemented

The source now serializes PK reads, absolute assignments and delta updates on a
dedicated per-character `PkSyncRoot`. A delta uses one critical section for the
read, saturation to 0..65535, write and existing number/name-flag projections.
Player-kill penalties and five-minute decay use that operation. The decay stamp
is checked and advanced under the same lock, preventing concurrent ticks from
applying the same interval twice. Absolute administrative assignments retain
their existing authorization and share the same setter lock.

Character save takes `ItemSyncRoot`, then `VirtueSyncRoot`, then `PkSyncRoot`
through snapshot construction and the database write. Existing amulet use and
PK-reduction NPC 10021252 take item ownership before PK ownership while checking
and applying their existing terms. Their payment/consumption behavior is still
legacy and nontransactional; these locks are not a claim that either service
is complete. Pure PK mutations never acquire an item lock while holding the PK
lock, avoiding a new cross-character inventory lock in the kill path.

Validation: isolated x86 build; 462 Experience checks and 60 Local PK checks.
New assertions race 1,000 kill-style increments against 1,000 decay operations
and character saves, verify the final durable balance, prevent ushort overflow
and underflow, and check red/black-name transitions at 100 and 30 points.
Local PK regressions exercise actual battle deaths, finalization and restart
recovery. Tests are in the disposable database; native name rendering is pending.

Durable PK preimages, the shared amulet cooldown, atomic source consumption/use
receipt, Deity exception resolution and earned amulet acceptance remain open.
No live deployment or new reward issuance is included in this prerequisite.

## Durable PK balance prerequisite implemented

`PkBalance` now records the exact nullable PK value read during character load,
separately from pending in-memory penalties or decay. It is bound to both the
session and entity instance. Values outside 0..65535 fail validation; historical
NULL retains the loader's zero interpretation without losing its SQL preimage.

Autosave includes that preimage in its conditional character update and advances
it only after exactly one row is saved. An unregistered character may bootstrap
only when its current balance matches the stored value. A stale row or unproven
pending cache value cannot overwrite the database; normal save failure requires
reload. This also protects unrelated fields in that same character update.

The transaction API requires both item and PK ownership locks and a database
transaction. It locks the character row, compares the last saved balance, then
requires one affected update. The calling item transaction can roll it back
together with consumption and cooldown; `ApplyCommitted` projects the final PK
and name flags and advances the saved image only after successful commit.
This API is ready for the amulet service; it does not itself consume an item.

Validation: isolated x86 build and **472 Experience checks**, including ten
additional durable-balance checks for rollback with pending penalties, commit
followed by autosave, suppressed writes, stale transactions and saves, NULL
preimages, missing images and invalid saved ranges. The **453 Warrior Epic**
checks cover ordinary earning, character saves and production cold reload with
the new balance integration; **60 Local PK** checks cover battle/restart paths.
Logs: `obj/chi-refill-validation/pk-balance-{experience,warrior,localpk}.log`.
No live database or executable changed. Durable amulet cooldown/consumption and
Deity/VIP exception resolution remain open.

## Owned amulet-use service implemented

`PenitenceAmulets` replaces both legacy item cases and is initialized at server
startup. Both direct item use and bounded 1009/action-4 dispatch reach one owned
service for every eligible ordinary authenticated player. The exact carried item
must be unlocked, non-suspicious, outside equipment/warehouse/wardrobe, and
unexpired. A player must be alive, out of trade/booth, and have at least 30 PK.
Zero-stack legacy singletons and multi-unit stacks are supported; no output slot
is needed. Invalid or stale ownership cannot consume an item.

Under item and PK locks, one transaction locks the character, checks the latest
successful use, consumes exactly one source unit, persists PK minus 30 through
`PkBalance`, upserts `revora_penitence_cooldowns` and inserts a complete
`revora_penitence_uses` receipt. Required writes must each affect one row. A
failure rolls everything back; uncertain failure fences the session for reload.
After commit, PK/name flags and inventory are projected through existing APIs.

The latest use is shared by both aliases and all sessions. Ordinary cooldown is
one hour; the installed `region.ini` identifies Deityland as map **10250**, whose
server terrain exists, so that BaseID selects thirty minutes. The updated
read-only audit checks region SHA-256
`939c68eb8a7d00b6a21adbf494ab421140b8f7b7f139f5eb7a2c9645ef404618`
and the matching server terrain's existence. Tests instantiate the server map;
this does not prove native travel or a restored Deityland event.

Explicit interpretation: the installed VIP3+ exemption applies in both locations.
Eligibility uses the current VIP tier and map against the latest successful use;
losing VIP therefore restores the appropriate cooldown. A one-second durable
request debounce remains even for VIP, protecting concurrent stacked requests
and immediate reconnect replay. Backward clocks cannot bypass the last use.
This is not a claim of a captured native request identifier or historical
Deity/VIP precedence.

**Existing Revora policy matters:** `EntityTable` raises login/new characters
to at least VIP4. It is preserved. Thus ordinary current players qualify for
the exemption after login. The tests explicitly set lower current tiers to
exercise hourly/Deity rules after actual production reload, rather than silently
changing the server's VIP policy or claiming those players remain VIP0 at login.

Validation: isolated x86 build; **579 gift checks**, **472 Experience checks**
and **60 Local PK checks**. The gift suite covers both IDs/bindings, zero/single/
stacked sources, simultaneous use/save, production reload, alias cooldown,
exact hour/half-hour boundaries, VIP exemption/expiry, clock rollback, PK
boundaries, full bags, timed items, malformed frames and stale ownership/balance.
Suppressed source delete, stack update, PK update, cooldown insert and receipt
insert each preserve the whole transaction; production reload can retry safely.
The item/map audit also passes. Logs are
`obj/chi-refill-validation/penitence-{gifts,experience,localpk}.log`.

The standalone gift tests supply owned amulets as fixtures. A subsequent
[Magnolia bonus batch](MAGNOLIA_BONUSES.md) now validates ordinary paid seed
change, planting, earned amulet use and cold reload. Remaining gates are native
consumption/name rendering/reconnect acceptance and exact exception precedence.
No deployed service, earned Magnolia reward or completed historical parity is
claimed by the standalone fixture; the separate Magnolia evidence proves that
earned source in the initial Revora bonus pool. Preserve both use/cooldown tables on eventual rollback; never erase
receipts to reset eligibility.
