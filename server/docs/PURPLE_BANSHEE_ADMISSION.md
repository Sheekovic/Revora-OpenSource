# Purple Banshee house admission

Current follow-up: the [Purple Banshee encounter candidate](PURPLE_BANSHEE_ENCOUNTER.md)
now connects these participants to owned item confirmation, combat and victory.
Earlier dormant/source-integration statements below describe their historical
batches; native/live acceptance and catalog repair remain open.

The [inactive reward participant](PURPLE_BANSHEE_REWARDS.md) now implements
Study/loot persistence and pending-spoils protection. Earlier statements below
that those prerequisites are absent are superseded; combat activation remains open.

This is an inactive admission participant, not a completed earning route. Neither
item 3004259 nor its old 2032 continuation has been reopened. No production
initialization, live database migration, Host restart or client installation is
part of this batch.

## Evidence and contract

The reviewed local 6711 BansheeSpirit tooltip specifies a once-daily summon in
one's own house, 200 Study Points for the summoner, 50 for teammates, and possible
P5/P6 souls or refinery items. It does not publish odds or a level gate. Purple
admission therefore does not inherit Sword Master's level-100 restriction.
The installed Honor shop lists this item at 35,000 Honor. Played Arena earning
and a complete purchase-to-Purple-victory journey remain open.

The installed server row has 67 fields: its description is split between fields
53 and 54 at the space between `you` and `will`. The decoded client row has 66
fields and the complete description in field 53. A reviewed matching catalog
repair is still required; this batch does not change either installed file.
The generic legacy scrap/money loot pool differs from the tooltip and will not
be treated as proof of the permanent reward contract. Original odds and Purple
skill details remain unverified; any Revora choices must be identified as such.
See [identity and source evidence](BANSHEE_RESTORATION.md). Purple must not silently
inherit SnowBansheeSoul's spirit-credit identity.

## Transaction and ownership

`PurpleBansheeAdmission` records the exact item UID, pre-consumption stack and
binding, canonical owner house/map/terrain/level, frozen party and Pacific day.
It consumes one item in the same transaction as reserving the daily attempt.
Ownership, inventory, party and live context are rechecked before commit. A
calendar rollover rolls back without spending the item. Failed attempts retain
their daily reservation; clock rollback cannot reopen them. Failure transition
requires the exact run and owner and can occur only once.

Both house admissions lock the same durable player row before checking the
other boss's active run, in addition to the existing player item lock. One active
Sword Master prevents Purple and vice versa. Terminal attempts preserve each
boss's independent daily allowance. Active Purple attempts also block house
construction before a permit is consumed. Cross-feature queries are enabled
only after that participant's schema initialization; the future coordinator must
initialize both admissions before opening Purple item use and recover interrupted
runs before accepting players. No current public route can create Purple runs.

## Validation and remaining work

The isolated build and **679 checks** pass: 28 Purple admission, 79 Sword Master,
557 housing and 15 quest-packet checks. The packet suite confirms both item use
and stale native continuations still preserve bound/unbound Banshee Spirits.
Logs remain ignored under `obj/chi-refill-validation/`: `purple-banshee-admission-tests.log`,
`purple-admission-sword-regression.log`, `purple-admission-housing-regression.log`
and `purple-admission-packet-regression.log`. Build SHA-256:
`bb2b572df90685f5c58d7472b5ae12890481f504f532fca2c3d448e01805a0fb`.

The isolated fixture covers exact bound-stack consumption, concurrent duplicate
admission, saved house and party identity, reload and repeated initialization,
wrong owner, locked item, missing live authorization, clock rollback, next-day
admission, receipt/item/context failure rollback, commit-day rollover, low-level
party members, both cross-boss orderings, simultaneous different summons and a
normal house upgrade refusal. It writes only the disposable fixture schema.

Combat, confirmation offers, owned victory/rewards, interruption recovery,
uncollected-loot house protection, catalog repair and ordinary full earning-path
validation remain unfinished. Genuine client behavior and live deployment remain
separate acceptance gates. This participant is not a claim of player availability.
