# Guild War occupation10302: approved Revora edition

The owner approved this definition through "use all recommendations": eligible
members of the winning guild earn10302 at regular Guild War finalization. It is
not a claim about the original6711 occupation trigger.

GuildWarPurses.Snapshot supplies the same authenticated, ordinary-player,
current-guild/rank/roster and battlefield-presence checks used for war purses.
The existing pole capture establishes the winning guild. Offline, absent,
unaffiliated and unsupported-rank characters are not added to the snapshot.
This definition does not require every recipient to damage the pole.

Freeze records10302 alongside each purse entitlement inside the final-result
transaction. A failed receipt rolls back the result and all entitlements; retry
uses the normal finalization path. Completed wars cannot grant a second receipt.
Online recipients' achievement caches are invalidated after commit, outside the
battlefield lock. No historical backfill, new payout or changed purse amount.

## Private validation (2026-10-01)

Three separate non-GM fixtures4294907201-4294907203 have supplied guild affiliation:
one leader, one ordinary member on1038 and one ordinary member remaining in town.
A controlled calendar and production attack handler capture the pole. Refusing
10302 writes through a temporary database trigger leaves the battle active,
purses unfrozen and zero entitlements. Removing the fault and retrying ends the
war with two entitlements and two occupation receipts; the absent member has none.
Repeated finalization preserves those counts. Character purses and inventories
stay unchanged because reward delivery has not yet been claimed.

Full server/PostgreSQL restart retains the won result, two unclaimed purses,
source-keyed occupation receipts and the outgoing achievement bits. The test uses
supplied membership and controlled proximity/damage/calendar, not native clients
or evidence of real-time balance. These separate fixtures are not added to the
retained career's achievement total. Existing purse delivery tests remain separate.

Build58A61FDE21CA31128BDF7534DBCAA0F983787854379E6321CACB27AD8EE01D46.
Production build retains existing warnings; helper compiles with warnings as
errors. The initial fixture needed a durable character reload before saving;
the harness success-marker mapping also needed the new phase. Earning completed
and was preserved; the corrected harness then passed cold reload without resetting
the database or replaying the capture. Private database stopped.

Native presentation, live deployment and the full compatible320 career remain
pending. Newcomer10930 now has its separate implemented/validated producer;
see [the current status](QUEST_EVENT_STATUS.md).
