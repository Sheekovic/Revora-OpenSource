# Leftover silver and study bags

M6.56b9e, 2026-09-22. Server implementation; not deployed.

Ordinary 1009/action-4 and direct `UseItem` of leftover bags **3005117–3005125**
go through `SilverStudyBags`. Those nine legacy cases fail closed. All nine
remain Way of Heroes `StageGoal` catalog prizes. Stages 101, 201, 301, and 401
pay 3005117–3005120 from the saved level. Stages 501 and 701 pay 3005121 and
3005122 from the saved rebirth. A later rebirth does not invent the old level.
Stages 504, 608, and 711 pay 3005124 and 3005125 from distinct saved subclass enrollments. Guild stage 303 stays refused pending atomic membership and payment persistence (see the review below). Stage 408 stays refused: catalog quality 9 is HeavenFan 201009 and StarTower 202009, plus is a different field, and the donation NPC that sells those ids also forces plus 12. Held bags remain usable. Silver uses
`SilverBalance`. Study uses `entities.studypoints` compare-and-swap. Messages
and the `zf2-e300` screen effect occur only after commit. There is no invented
cap. Checked silver/`ushort` overflow refuses without source loss.

Executable leftover amounts are preserved. Catalog copy for 3005118–3005122
names larger silver purses than the live handler; this batch does not rebalance
them.

| ID | Catalog | Silver | Study | Success text |
| ---: | --- | ---: | ---: | --- |
| 3005117 | Class1FortunePack | 5000 | 0 | none |
| 3005118 | Class2FortunePack | 5000 | 0 | none (catalog names 10,000 Silver) |
| 3005119 | Class3FortunePack | 5000 | 0 | none (catalog names 20,000 Silver) |
| 3005120 | Class4FortunePack | 5000 | 0 | none (catalog names 50,000 Silver) |
| 3005121 | Class5FortunePack | 10000 | 0 | none (catalog names 100,000 Silver) |
| 3005122 | Class6FortunePack | 20000 | 400 | `You received 400 Study Points!` (catalog names 200,000 Silver) |
| 3005123 | DiligentStudyPack | 0 | 200 | `You received 200 Study Points!` |
| 3005124 | EliteStudyPack | 0 | 300 | `You received 300 Study Points!` |
| 3005125 | SuperStudyPack | 0 | 500 | `You received 500 Study Points!` |

Receipt table `revora_silver_study_bag_uses`. Direct daily P7 is unchanged.

## Study producer review (MCP 334)

Subclass stages use saved `subclasses.id` as player UID and `subclasses.uid` as
class ID. Only distinct supported IDs 1–6 and 9 with `1 <= phase <= level <= 9`
count; duplicate rows refuse the transaction. Installed TaskType 24,
Progress 1/2/3, OpenLev 1015/1101/2015, and one bound plus-0 pack are checked.
These rows have no `Require`; owned rules now parse and reject nonzero `Require`.
Disposable ordinary enrollment packets use supplied materials and saved
level/rebirth prerequisites. Claims, reload, item-packet consumption and replay
prove the existing 300/500/500 study payouts. They are not live or complete
player-journey acceptance.

The proposed guild join record was a separate write after `entities.guildid`.
Suppressing its insert reproduced a saved guild ID without either the record
or in-memory membership. In `Guild.Create`, that exception also occurred
outside the creation catch and after NPC payment, bypassing the false-result
refund branch. Separate leave-record deletion could leave stale evidence.
The supervisor removed the new guild hooks and kept 303 refused. Its earlier fixture manually paid before calling
`Guild.Create`, so it did not prove the complete paid NPC flow. Implement atomic
payment/create/join evidence plus fenced leave/disband and stale-save protection
before restoring this 200-study producer. Held 3005123 still pays 200 study.

The next proposal (`65b565e`, MCP 338) grouped payment and creation in a
transaction, but did not complete guild lifecycle ownership. Supervisor
reproductions on that exact source demonstrated:

- Packet 2227 admitted an applicant below the guild's recruitment requirement
  and issued stage 303. The fixture had not proven authorized recruitment.
- Suppressing disband's entity update or membership delete still committed
  deletion of the guild, leaving orphaned state.
- A deleted guild's membership qualified after another guild reused its ID.
- Failure before creation's memory projection left the paid guild absent from
  the registry after reconnect. Character Save then erased the founder's rank.

The unsafe integration and stage 303 eligibility are removed. The retained
save fix prevents stale or missing memory membership from writing `GuildID`.
Packet 2227 now checks the current session, reload fence, blacklist and existing
guild level/rebirth/class requirements before joining; duplicate packets share
the character lock. Union changes follow a successful join. Disposable tests
prove those guards, successful ordinary legacy joining, reload and departure,
using a supplied guild/leader fixture. They do not prove paid atomic creation,
complete advertisement authorization, or a new reward producer. Guild rank and
other guild fields still need authoritative lifecycle ownership.

Next, implement and validate recovery of paid creation, then serialized,
identity-bound join/depart/disband with checked writes and authorization.
Keep 303 closed until those paths survive failures, concurrent Save and claims,
and reconnect without relying on preexisting memory projections. Preserve
existing guilds and configured prices; do not backfill historical eligibility.

### REVIEW-343: incarnation proposal remains incomplete

The next proposal (`be871c6`, MCP 343) is also not a completed guild producer.
Supervisor disposable reproductions on that source showed that production
reconnect invented the guild name and reset a saved fund to 500000; an existing
registry entry shaped like `GuildTable.Load` output kept its empty incarnation;
member-first recovery lost the actual leader; and a replaced member's quit
packet removed the replacement session's membership. Packet 2227 also admitted
and rewarded a member without active advertisement or automatic recruitment
consent. The lifecycle integration and stage 303 enablement were removed.

The retained fixes preserve saved rank when the membership pointer is missing
and recheck level, rebirth, class and blacklist at both reciprocal acceptance
routes. Guild commands now reject replaced/fenced callers under their character
lock, and recruitment refuses fenced counterpart sessions. Fixtures use ordinary
characters and production invitation/acceptance, quit and reload paths, with
supplied guild/leader prerequisites. They do not prove a paid guild producer,
complete reciprocal authorization/concurrency, or advertisement authorization.
Stale non-null rank and other guild metadata still need lifecycle ownership.

Review 348 rejected `0869ee0`'s enrollment-based incarnation integration.
Three disposable reproductions showed that a cached guild still authorized its
leader after deletion of the durable guild row, a replacement guild on the same
day inherited an old incarnation, and an unbound cached object acquired an
incarnation without rereading the real guild name or funds. Enrollment is a day,
not a durable row identity. The unsafe snapshot/reconnect wiring is removed.

The retained prerequisite shares `GuildTable.Materialize` between actual
startup and internal `LoadRecord`. Both read the existing guild row, members,
legacy rank calculations, spouse ranks and populated arsenal inscriptions.
Startup resolves ally/enemy references after all guilds are registered.
`LoadRecord` is disconnected from production reconnect: it neither registers a
guild nor establishes identity, and read exceptions propagate to its caller.
It is not an atomic snapshot or proof of lifecycle recovery. Legacy rank
recalculation remains; exact saved ranks, blacklists, cache ownership and
concurrent reconnect/Save still need the managed recovery implementation.

Review fixtures check the real name, fund 888888, saved recruitment/event
settings, offline leader, deputy and spouse ranks, both directions of allies,
enemies, and a populated weapon inscription with its actual item. They check
startup/single-record agreement, ordinary leader eligibility and no stage 303
payout. A missing guild yields no fabricated object; an actual database-read
failure leaves the existing registry entry unchanged. These supplied rows are
not a paid Guild Director purchase or earned-reward evidence.

`GuildIdentity` is a disconnected, partial saved-image read. `None` only classifies
identity; it does not authorize a session or a registry entry. Schema setup is
explicit and additive; reads never install schema or generate identities. A
null `guilds.incarnation` is legacy only when no known member identity, and no
other managed guild membership, contradicts that classification. An empty UUID
is refused. The image returns stored guild metadata, advertisement expiry,
CTF donation amounts, member donations, flowers, exploits, rewards, and raw
last-login. It does not decode last-login, apply `DateTime.FromBinary`,
recompute ranks, or derive spouse rank. Arsenal, inscription, ally, and enemy
rows count only when `revora_guild_association` names this incarnation; a
numeric id match is not enough, and those marks are not cascaded. `BlackList`
still has no durable loader. An inscribed item is the stored `items` row from
this same transaction: identity, durability, position, agate, socket and plus
progress, both sockets, effect, plus, bless, bound, enchant, lock, raw unlock
and suspicion times, color, warehouse, stack, refinery id and raw refinery time,
wardrobe flag, raw day stamp, days, perfection, signature, rune experience,
relic attributes, the inscribed flag, raw steed color, and `revora_item_token`.
`ConquerItemTable.LoadItem` and `deserialzeItem` are not called, and `ConquerItem`
is not constructed. Clock-derived time left, expired-item deletion, refinery
clearing, and lock clearing are not applied. `Arsenal.GetDonation` is not
recomputed; `arsenal_donation` stays the stored value. `Arsenal.GetDonation`
does not read `itemadding`. This read does not build an `ItemAdding` packet,
apply soul stats, or score perfection. `Arsenal.MetoEditionars.ArsenalPosition`
is compared with the saved inscription category and is not itself a stored field.
`guilds.unionid` now selects a saved union with a direct row token and matching
guild/member bindings, described below. Saved face, body, and nobility rows are
read with those characters. Donation rank is not derived. `Guild.BlackList` has
no durable loader. Full projection parity remains unresolved. This is not a
complete guild recovery image and startup/reconnect still do not call it.

Review 358 reconciles association marks by guild id **or** incarnation, so a
same-incarnation mark with another guild id cannot be silently omitted. Orphan
association marks also prevent `None` for an absent or legacy owner. Managed
relationships require an existing peer with a nonempty incarnation matching
the mark; NULL cannot bind a reused legacy peer id. Inscription owner uid/name
must match the saved roster and `items.entityid` must match that owner. These
checks do not permit a later write. `items.uid` is the primary key of the
current row. `ConquerItem.ItemUID` is a monotonic counter saved as
`configuration.ItemUID` on shutdown, and a deleted uid can be inserted again, so
the uid alone is not a lifetime. Nullable `items.revora_item_token` and
`revora_guild_item_binding` (`item_uid`, incarnation, owner uid, token) are the
direct binding. A null token, an empty token, or a binding that disagrees with
the item and this incarnation is a contradiction. There is no foreign key, no
cascade, and no historical backfill. Supplied fixture tokens are not evidence
that a production inscription writer maintained them. A future writer must set
the token, binding, inscription, and association in one transaction. Deleting
an inscription or association while retaining its direct binding is a contradiction
for that managed incarnation; the reader reconciles all its bindings. A bound
item inscribed in another guild is also refused. Binding lifetime is the
incarnation, not a historical numeric guild id inferred from an absent row.
The association primary key is `(incarnation, kind, subject_id)`; stale marks
remain visible instead of cascading away. Future lifecycle and association
writers must maintain owner, member, item and peer invariants atomically and
explicitly retire obsolete marks. No legacy row is enrolled by these readers.
The actual lock order is requested owner, requester (managed reads), member
marks, roster, association marks, arsenal, inscriptions, item rows, item
bindings (each item then its binding), all remaining bindings for this
incarnation, then edges and peer guilds. Legacy/missing classification checks
association/member marks and durable requester membership. All statements share
one repeatable-read transaction; row changes before a later share lock may
return `40001`. Locks end at return. Deterministic arsenal, item, and binding
update fixtures exercise that failure and a fresh read; supplied rows/marks are
not earned gameplay evidence. Review 363 also covers a committed inscription
change. NULL, fractional, out-of-range item numerics and unsupported flags/enums
are refused before conversion can supply default gear. Raw clock fields stay
uninterpreted. `Project` is a partial DTO copy of item/arsenal, roster and selected
guild fields, including exact saved ranks; it is not a complete Guild projection. It opens no connection
and does not publish `Kernel.Guilds`, `Kernel.Guilds_adv`, `Kernel.GamePool`,
or a session. A contradictory snapshot or a projection fault returns an empty
unsuccessful projection. Snapshot success does not authorize a later write.
A non-null value is that row's identity, so a same-day, same-name, or
same-founder replacement with a new value is a different guild. Member rows
live in `revora_guild_member_identity` and are not cascaded: deleting the guild
row leaves them, and the next read is a contradiction rather than a legacy
guild or a usable leader. The schema has a unique non-null guild incarnation
and one mark per entity, deliberately without a foreign key or cascade; the
reader reconciles both directions of owner/mark/entity membership and refuses
orphans and conflicting marks. Future writers must preserve this invariant in
one transaction. One repeatable-read transaction supplies an MVCC snapshot and
share-locks the guild row, ordered member identities, and ordered entity rows.
Locks end when the read returns; the result cannot authorize a later write or
registry publication. Concurrent changes before a later lock can produce a
serialization failure, reported as `ReadFailure`. Enrollment date is display
metadata. Saved ranks are returned as stored; this read does not recompute
ranks below 920 or derive spouse ranks. `BlackList` has no durable loader, so a new empty list is not recovered history.
The read does not publish `Kernel.Guilds`, advertisement entries, or a session,
and `GuildTable.Load`, `EntityTable.LoadEntityState`, NPC creation, and claims
do not call it. A database error is a read failure, not an absent guild.
Fixtures supply the rows, including association marks. They are not a paid
Guild Director purchase, and the read does not pay stage 303. Startup and
reconnect still do not call it. Review 353 adds rejection of omitted non-requester
members, wrong-id/incarnation marks, multiple leaders, empty UUIDs and managed
requesters presented as legacy. A real missing-table read is `ReadFailure`;
fixtures verify a blocked writer through `pg_blocking_pids` and a committed
roster change through SQLSTATE 40001. Neither case mutates a registry/session.

Saved additions are `itemadding` rows from the same transaction. `iditemadding`
is `bigint GENERATED BY DEFAULT AS IDENTITY` (`itemadding_iditemadding_seq`).
The sequence does not by itself stop an explicit insert from reusing a deleted
id, and `uid` is only the current item number. Review 367 reproduced a deleted
addition ID inheriting a stale binding after explicit reinsertion. Each row now
requires a nonempty `itemadding.revora_addition_token`; a partial unique index
prevents two rows sharing a token. `revora_item_addition_binding`
(`addition_id`, `item_uid`, `item_token`, `incarnation`, `addition_token`) must
match that row token as well as the item lifetime and guild incarnation.
Both added token columns are nullable, with no default or historical backfill;
an old unbound row is refused. `EnsureSchema` remains explicit and disconnected.
No foreign key or cascade. A future writer must mint a new token for every
inserted/replacement addition and maintain the row and binding atomically with
the item lifetime. It must reconcile deletion/transfer marks in that transaction;
reusing the previous row token is forbidden. `ItemAddingTable` inserts and
`RefineryProgress.Save` deletes/reinserts additions without these bindings today,
so they cannot yet produce a managed image. No production writer was changed.
A row without a
matching binding, a binding without its row, a null row id, a second
purification, a second permanent or temporary refinery, or `addingtype` outside
0/1 is a contradiction. No rows and no marks means the effect is absent, not a
zero-valued effect. Purification is `addingtype` 0 with percent 0. Permanent
refinery is `addingtype` 1 with duration 0. Temporary refinery is `addingtype` 1
with a nonzero duration. Stabilization credit is the maximum stored
`stabilizationpoints` on the refinery rows. `RefineryProgress.Cost`,
`Current`, catalog lookup, expiry deletion, and `DateTime.FromBinary` are not
applied. `items.refineryitem` and `items.refinerytime` stay the stored item
cache and are not rewritten from the addition rows. `AsText` maps a null text
value to an empty string for guild, member, and item text columns; addition
columns are numeric, and a null numeric is refused except a null purification
`stabilizationpoints`, which stays absent. `Project` copies these addition
objects and remains a partial DTO copy, not a complete guild projection or an
activation-ready effect.

| Field | Status |
| --- | --- |
| `iditemadding`, `uid`, `addingtype`, `addingid`, `addinglevel`, `addingpercent`, `duration`, `addedon`, `stabilizationpoints` | Stored. `addedon` stays a raw integer. |
| `revora_addition_token`, binding `addition_token`, item token and guild incarnation | Stored direct lifetime identities. Nonempty matching row/binding tokens required. No historical enrollment. |
| Purification / permanent / temporary classification | Stored shape from type and duration. Not an active-effect decision. |
| Refinery stabilization credit | Maximum stored refinery `stabilizationpoints`. Not `Cost(level)`. |
| Null text via `AsText` | Normalized to empty for text columns. Not used for addition numbers. |
| Time left, `RemoveAdding`, `FromBinary`, `RefineryProgress.Current`, `DatabaseRefinery`, soul and perfection scores, arsenal packets | The read stores the rows. `Derive` and `DeriveScores` are separate and do not delete them or emit `ArsenalPacket`. |
| Union id, token, fund, roles, and member bindings | Stored when `guilds.unionid` is nonzero. Role numbers are not authority. Face, body, and nobility are separate raw sources. Rank and blacklist remain unresolved. |

The read uses the existing repeatable-read transaction: share-lock the item's
addition rows in ID order, then its binding rows in ID order, then sweep the
guild incarnation's addition bindings after the item-binding sweep. Locks end
when the read returns. Committed row or binding changes after the snapshot
return `ReadFailure` with SQLSTATE 40001; a fresh read distinguishes a current
value from a mismatched lifetime. This is a saved image, not permission to write
or publish after those locks end.

Disposable ordinary-player fixtures cover all three addition slots, every saved
and copied field, token uniqueness, replacement IDs/items, transferred additions,
null/empty/mismatched identities, malformed values, duplicate slots, orphan marks,
real database read errors, row and binding races, and projection failure with
populated additions. Seeded rows and tokens do not prove player earning or a
production inscription writer. `Arsenal.GetDonation` uses item quality, sockets
and Plus, not addition rows; saved donation is retained. `LoadArsenal2` still
calls the separately connected `ConquerItemTable.LoadItem` in legacy gameplay.
This read covers the stored columns consumed by `GetAddingsForItem` and
`RefineryProgress.Read`, but does not claim their active-effect semantics,
`ItemAdding` packet parity, expiry handling, catalog validity, soul stats, or
perfection scores. Those derived and packet checks remain prerequisites for
full projection and activation.

A nonzero `guilds.unionid` is read from `unions` in the same transaction.
`revora_union_token` is the union lifetime. `Union.UnionCounter` only supplies
the next numeric id and is saved as `configuration.UnionID`; deleting a row
lets that number be inserted again. `revora_guild_union_binding` ties one guild
incarnation to that token, and `revora_union_member_binding` ties each entity
whose `UnionID` equals the union. Neither table has a foreign key, a default,
or a backfill. A guild member may store a different `UnionID`, including none.
An independent entity can belong to the union without joining the guild.
Official, harem, and guard lists are the raw writer lengths, 8, 10, and 20.
`UnionTable.Load` reads only the first six officials; this read keeps all eight
and does not treat the numbers as mesh, nobility, or packet authority. Null
text becomes empty through `AsText`. A missing token, a reused id with the old
binding, a short or duplicate role list, and an orphan mark are contradictions.
No `unionid` and no binding means the union is absent. `Project` copies that
object and still does not publish `Kernel.Unions`.
`Server.EntityUID` is the counter saved as `configuration.EntityID`.
`entities.uid` is only the current row key. `entities.revora_entity_token`
is the character-row lifetime, and guild and union member marks must carry
the same nonempty token. A roster member may name a different nonzero union;
that union is read on the same transaction and deduplicated. Stored role
columns stay unchanged. Leader precedence and array slots produce separate
derived values: leader official 1, official slots 2/4/8/16/32/64/128/256,
harem slots 1024/2048/4096, and guard presence 512. A character may appear
on more than one list. Face, body, and nobility are separate raw sources on
the same read. Donation rank and blacklist persistence stay unresolved.

Review 373 corrects two reproduced omissions: an additional saved guild with
`unionid` but no binding, and a stale incarnation mark for the requested guild
under another union. Both previously returned `Consistent`. The read now checks
guild references and marks in both directions, refuses orphan marks even on a
missing/legacy/no-union guild, validates binding integers before conversion, and
copies the verified guild IDs and incarnations as independent `Union.Guilds`
objects. A referenced guild needs a nonempty matching incarnation. This does
not load that other guild's full roster or authorize its use.

| Union image fields | Provenance and limits |
| --- | --- |
| Id, Token, GoldBricks, Fund, LeaderUid | Saved union row; strict integral ranges and nonempty direct token. |
| Name, Title, LeaderName, Bulletin, Declaration | Saved text via `AsText`; name and leader name must be nonempty. Leader UID/name must match a saved union member. |
| OfficialsRaw, HaremRaw, GuardsRaw and parsed arrays | Saved role lists, lengths 8/10/20; numeric members, no duplicates within each list. Cross-list membership is allowed by existing writers. |
| Members: Uid, EntityToken, Name, Level, Class, Exploits, Official, Harem, Guards, GuildId | Saved entity columns selected by UnionID, matched to direct union and character bindings; nullable GuildId stays absent. Stored roles remain raw. |
| Members: DerivedOfficial, DerivedHarem, DerivedGuards | Leader/array-derived assignments, separate from saved columns. Null means no assignment, not a computed zero or a command to clear a stored role. |
| Guilds: GuildId, Incarnation | Saved guild references reconciled with every relevant union binding. Union identity is the enclosing Id/Token. |
| Guild member EntityToken | Direct nonempty entity-row token, equal to its guild identity mark and any relevant union mark; preserved through copying. |
| Guild member StoredUnionId, UnionExploits, UnionOfficial, UnionHarem, UnionGuards | Saved optional entity metadata, preserved through copying. Each distinct nonzero union named by this guild or its roster is validated once. OtherUnions contains the additional images in ascending ID order; this does not recursively load other guild rosters. |

Within the existing repeatable-read transaction, after the saved guild/item
image, share-lock this guild's union bindings by incarnation **or** guild ID,
then the union row, ordered member entities and member bindings, all bindings
for that union or its saved guild references, then the referenced guild rows.
Then lock roster union marks by entity UID,
and load additional unions in ascending ID order through the same member/guild
checks. Roster entities and their character tokens were locked earlier with the
guild roster. This order is for the disconnected reader; a future writer must
establish compatible lock ordering and retry semantics before activation.
Locks end at return. Committed member, union, and binding updates after the
snapshot return `ReadFailure 40001`; fresh reads distinguish new saved values
from mismatched tokens. Absent-row inserts after the snapshot remain outside
that snapshot; this read is not a mutation fence or activation protocol.

Future managed writers must create a fresh token on union insert/replacement
and atomically reconcile union, guild and member bindings on transfer/deletion.
No existing production writer does this and no historical rows are enrolled.
Character insert/replacement must mint a fresh `entities.revora_entity_token`
and atomically reconcile guild and union member marks. The nullable column and
nullable mark columns have no default or backfill; the entity token has a partial
unique index. Reads reject missing, empty, mismatched and orphan identities.
These supplied identities are fixture evidence, not an enrollment path.

`Sistemas/Union/UnionPacket.cs` owns `GetMemberRankOffical/Harem/Guard`:
leader precedence applies only to Official; a missing array assignment leaves
the existing live property unchanged. Cross-list assignments are permitted.
`Client/GameState.cs` role setters update entity columns and packet buffers;
`AddCoreoffical/AddHarem/AddGuards` then save union arrays separately through
`UnionTable`. Removal paths also save arrays and entity roles in separate calls.
`EntityTable.Load` reads saved roles, while `UpGradeUnion` invokes derivation for
members of the selected kingdom. This reader preserves both sources and does
not reproduce those writes, ranking selection, or packet effects. Resolving
split writes and deciding which source controls activation remain prerequisites.

Review 378 removes the artificial guild-ID-zero query key when a union has no
guild bindings. An unrelated saved guild must not invalidate that union; an
actual unbound guild referencing it is still refused by the UnionID predicate.

Saved appearance and nobility are part of this disconnected read. `entities.face`
and `entities.body` stay raw. Null is absent; zero is stored. `BodyDigit` is
`body % 10`, the value `Nobility.ApplyDonation` writes into `nobility.gender`.
`LoginFallbackGender` is the session fallback used when `Nobility.Board` has no
row: `body % 10 >= 3` selects 0 and any smaller digit selects 1. Neither value
is the saved nobility gender. `ConcatenatedMesh` is the `Face.ToString() +
Body.ToString()` value `UnionTable.LoadMembers` parses. It is null when either
column is null or the text does not fit a uint. It is not `nobility.mesh` and
not `Entity.Mesh`, which also includes the live transformation id.

`nobility` is keyed by `entityuid`. That key can be reused, so the row also
stores nullable `revora_entity_token` with no default, backfill, or foreign key.
A partial unique index applies when the token is present. `EnsureSchema` does
not fill existing rows. A missing row is absent, not a zero donation or serf.
A present row must carry the same nonempty token as `entities.revora_entity_token`.
A null, empty, mismatched, or other-uid token is a contradiction. Donation,
gender, mesh, and `rey` keep null distinct from zero. Values that do not fit the
live byte or uint readers are refused. `duracionrey` stays raw text. `NobilityTable.Load`
does not read it, and `Nobility.Sort` does not either, so it is not an expiry.

`DerivedRank` stays null. Live `Nobility.Sort` orders every board row by donation,
keeps dictionary order on ties, forces king when `rey` is 1, then applies
donation and place thresholds (serf below 30,000,000 unless paid king; king at
positions 0..3 when qualifying; prince before position 16; duke before position 150; otherwise earl,
baron, or knight from the remaining donation thresholds). That method also
replaces `Nobility.Board`, sends icons, and announces titles. This read does not
call it and does not lock the whole table, so one row cannot prove a place.
`PacketGender` is only the guild member-list transform of a saved gender: 3
becomes 1 and every other saved value becomes that value plus 1. A missing row
does not become packet gender 1. `guilds.malefemale` remains the advertisement
requirement. `Flowers.IsGirl` is not applied.

`Guild.BlackList` is a `List<uint>` on the live guild object. `GuildCommand`
adds or removes an entry in memory, and only while the target is online for an
addition. No table, loader, or save writes it. `BlackList` on this snapshot stays
null and `BlackListState` is `unsupported`. An empty list would claim that nobody
is blacklisted. Activation cannot reconstruct a pre-restart blacklist from this
read; packet 2227 would consult the new process list. This batch adds no
blacklist table or workflow.

The read share-locks nobility rows for the roster and for members of the loaded
unions after those character rows, matching `entityuid` or the character token,
in entity-uid order. It does not lock unrelated nobility rows. Locks end at
return. An update to a snapshot-visible nobility row committed before its later
share lock returns `ReadFailure` 40001; a fresh read shows the new donation or a
mismatched token. A newly inserted row after the snapshot stays absent in that
read and is visible on the next read; row locks do not fence missing rows or
authorize cached use after return. Review 384 tests both behaviors and reruns
`EnsureSchema` while a nobility row is unbound to verify no enrollment.
Future nobility writers must store
the current character token in the same transaction as character insert or
replacement. `NobilityTable` insert, donation update, paid-king changes, and
`ClearDonations` do not write the token today.

| Appearance and nobility | Provenance and limits |
| --- | --- |
| Face, Body | Saved `entities.face` and `entities.body`. Null is absent. Outside 0..65535 is refused. |
| BodyDigit | `body % 10` when body is present. This is the donation writer's gender input, not the saved nobility gender. |
| LoginFallbackGender | Session fallback for a missing board row. Not saved nobility gender or guild `malefemale`. |
| ConcatenatedMesh | Union loader's face/body text join when it fits a uint. Not `nobility.mesh` or live `Entity.Mesh`. |
| Nobility row | Absent when no row. Present fields are saved `entityname`, donation, gender, mesh, `rey`, and `duracionrey`, plus the character token. |
| DerivedRank | Always unresolved. Place, ties, paid `rey`, and weekly reset are live sort behavior. |
| PacketGender | Guild member-list transform of saved gender only. |
| BlackList | Unsupported. Null is unknown, not an empty recovered list. |

| Existing writer | Activation obligation |
| --- | --- |
| `Nobility.ApplyDonation`, `NobilityTable.InsertNobilityInformation` and `UpdateNobilityInformation` | Donation mutates the live board and separately inserts or updates the saved row. Validate the character lifetime and bind a new row atomically; a UID-only update cannot authorize a replacement character. |
| `NobilityTable.AgregarRey`, `RemoverRey`, `ClearDonations` | Paid flags and global resets are separate writes without token checks. Preserve payment/reset semantics and fence lifetimes before activation. |
| `GameState` rename paths | `TryRenameByUid` updates saved nobility names separately from entities and the board. The legacy name path also issues a nobility update. Neither binds a lifetime; saved names may differ from character names. |
| `EntityTable` creation/save and `Entity.Body`/`Face` setters | Durable face/body and live appearance are separate sources. Setters change live mesh and packets, not the nobility row. Preserve owner/lifetime checks and atomic durable writes before recovery is activated. |

The union loader's text concatenation also differs from live mesh arithmetic
when body is not four digits: `Face * 10000 + Body`, plus transformation. The
saved derivation is evidence of that loader only, not a canonical appearance.

`Project` now copies every saved snapshot field: incarnation, funds, bulletin
and dates, requirements, advertisement text and rate, `malefemale`, the stored
union id, wins, losses, CTF amounts, max enchant, allies, enemies, and the
unsupported blacklist state. Nested members, arsenal, inscriptions, additions,
unions, and nobility stay independent copies. `BlackList` remains null.
`DerivedRank` remains null. A failed projection returns empty arrays, a null
blacklist, and `Guid.Empty` incarnation; it does not keep a partial guild.

`guilds.adend` is copied as the stored integer. `Guild.adend` get uses
`DateTime.FromBinary` on that long, while its set stores `Ticks`.
`GuildTable.Read` assigns `FromBinary(column)` through the setter, so the live
object stores ticks of an already decoded value and the getter decodes those
ticks again. This projection does not choose either interpretation.

Raw parity is not active-effect or packet parity. These call sites stay outside
the projection:

| Runtime behavior | Call site | Projection status |
| --- | --- | --- |
| Guild row materialization, spouse ranks, member rank recompute, CTF rank | `GuildTable.Materialize`, `CreateMembersRank`, `ApplySpouseRanks`, `CalculateCTFRank` | Not applied. Saved rank stays stored. |
| Ally and enemy registration | `GuildTable.LoadAllyEnemy` | Saved ids and peer incarnations only. No `Kernel.Guilds` objects. |
| Advertisement list | `GuildTable.Load` adds a guild to `Kernel.Guilds_adv` when `DateTime.Now < guild.adend` | Not applied. Raw `adend` is not compared with the clock. |
| Packet 2225 | `PacketHandler` case 2225 | Leader-only in-memory bulletin and silver deduction. It does not call `Guild_Advertise.Process`, and it does not save `AutoRecruit` or `adend`. |
| Packet 2227 join | `PacketHandler` case 2227, `Guild_Advertise.PassJoinRequirements` | Uses the live guild's level, rebirth, class flags, and in-memory blacklist. Not this projection. |
| Item time, expiry, lock, refinery | `ConquerItemTable.LoadItem`, `deserialzeItem` | Not called. Raw clock columns stay integers. |
| Addition activity | `ItemAddingTable`, `RefineryProgress.Current` and `Cost` | The projection stores the rows. `Derive` reads a supplied clock and catalog and does not delete them. |
| Arsenal packet and donation | `ArsenalTable.LoadArsenal`, `Arsenal.GetDonation` | The projection keeps the stored donation. `DeriveScores` recomputes it and does not emit `ArsenalPacket`. |
| Nobility rank | `Nobility.Sort` | Unresolved. |

Live `Guild` members that this image does not recover: `BlackList`, war and
pole scores, arsenal display flags, `GuildCounter`, and `Kernel.Guilds_adv`.
Those are memory, configuration, or clock-derived. Startup and reconnect still
do not call this read.

`GuildIdentity.Derive` reads a consistent snapshot with a caller-supplied clock
and refinery catalog. It does not open a connection, call `deserialzeItem`,
`LoadItem`, `GetAddingsForItem`, or `RefineryProgress.Read`/`Project`, and it
does not delete or rewrite items. A missing or uneven catalog, zero or duplicate
catalog keys, or a fault
during derivation, returns an unsuccessful image with no items. Per-effect
unknown and mismatched catalog results stay on a successful image and do not
become active effects.

The loaders decode `FromBinary`. Several insert paths store `Ticks`; day-stamp
and unlock updates also use `ToBinary`. Ticks omit the kind bits carried by
binary dates. The pure result records the loader and tick-writer facts and decodes with
`FromBinary` only. It does not convert the supplied clock to local time. Live
item and purification code uses `DateTime.Now`. Live refinery progress uses
`Clock().ToLocalTime()`. That clock conversion is not reproduced. `FromBinary`
itself interprets local-kind values using the machine time zone; callers must
capture a clock in the matching comparison domain. This is not portable UTC parity.

Day limit: `days` 0 is an absent limit after validating the date. A stored day-stamp 0 still decodes.
Expiry is `clock >= stamp.AddDays(days)`, the same inclusive boundary as
`deserialzeItem`. Remaining time is the second count that loader assigns to
`TimeLeftInMinutes`. A negative or out-of-range second count is overflow, not
a wrapped uint. The item calculation allows the full unsigned 32-bit range;
purification uses signed 32-bit seconds, while refinery selection compares dates
without a signed-second limit. An invalid binary value is malformed, even for
zero days, permanent purification, or non-timed locks, since those loaders still
decode the saved dates. Out-of-byte day counts are malformed. A date that cannot accept
`AddDays` is overflow. Expired items set `LoaderWouldDelete` and do not remove
the row. The loader deletes and then still returns the in-memory item.

Lock 2 clears when `clock >= FromBinary(unlockend)`. Any other valid lock value is
not time-cleared. Saved refinery id and time clear only when the id is nonzero,
the time is nonzero, and `clock > FromBinary(time)`. Equality keeps the saved
refinery. The raw id is not replaced.

Purification duration 0 is permanent. A nonzero duration expires when the
remaining whole seconds are `<= 0`, and the loader would delete that row.
Refinery catalog rows must match id, level, and percent, and `Cost(level)` must
be nonzero. Otherwise the effect is unknown or mismatched. A duration inconsistent
with its permanent/temporary slot is malformed. `State.Current`
selects the temporary effect only while `clock < added + duration`; at that
instant it falls back to the permanent effect. Derived credit is the stored
stabilization maximum, then `Cost(level)` for a recognized permanent row. An
invalid refinery state leaves both derived selection and credit unresolved;
the raw credit remains available on the snapshot. No rows yields absent derived
credit, distinct from stored zero. `CurrentRefineryId` is the `State.Current`
selection, not proof that `Load` will apply it. `LoaderWouldProjectRefinery`
models the `Load` guard: no addition rows preserve a nonzero legacy saved id
unless the earlier saved-refinery expiry step clears it. `LoaderWouldReplaceRefineItem`
reports a guarded in-memory replacement relative to the raw saved id, not a
database write.

`DeriveScores` adds the read-only arsenal donation, battle power, prestige, and
packet scalars. `Arsenal.GetDonation` depends on the quality digit, one socket
when only the first is open, both sockets, and plus 1 through 12. Plus outside
that table adds nothing. A second socket with the first closed adds no socket
bonus. The inscribed `arsenal_donation` stays raw, and a difference is reported
rather than written back. `ConquerItem.BattlePower` adds quality above 4, plus,
one per open socket, and one more when a socket’s last digit is 3. It doubles
the total for family 421 and for any id whose decimal text starts with 5.
`ItemHandler.IsTwoHander` is not that rule. Neither calculation constructs a
`ConquerItem` or sends a packet.

Prestige uses the caller’s item-level and ability-band catalogs. Garment,
bottle, steed-armor, and accessory positions score zero from the id alone.
A missing item level, a missing band, or a saved refinery id that is not in the
supplied catalog leaves prestige unknown. The live getter treats a missing
refinery record as material level 0; this path does not invent that zero.
Overlapping bands, or the same item id with two levels, reject the whole image.
A level of zero or less rejects the catalog and does not disconnect a client. An
expired purification does not contribute soul phase, because the loader does
not attach it. Gem quality follows `GemsQuality`: socket 255 and a last digit
of 0 are skipped. The material phase reads the saved refinery id, not the
current temporary effect. `PerfectionLevel` at 28 and above adds 100 for each
level past 27. A product or running total that does not fit a signed integer
is overflow, the score stays null, and donation already calculated on that
item remains.

`ItemAdding.Append` rewrites effect ids 724440 through 724444 and 3004140 to
301 only for an addition effect. `RefineryPacketStatus` describes this separate
calculation: whole seconds truncate toward zero within the signed-int range.
A timed effect with less than one second left is still current in refinery state
but is omitted by Append when percent is nonzero. Percent zero includes the
effect without a time field. Permanent additions use their actual signed delta;
a negative delta would wrap in Append and is explicitly `Unresolved` here.
Out-of-range signed deltas are `Overflow`; neither produces a claimed packet
inclusion or fabricated seconds. Append2 has the same time gate, but takes its
type from the packet and does not rewrite the effect id. It is not emitted here.

`SoulEffectId`, `SoulSeconds`, `SoulCatalogStatus`, and `SoulWouldInclude` model
the separate `SendExtras` / `RefineStats` / `RefinerySeconds` ItemSoul path after
the loader guard. The id is never rewritten to 301. A legacy saved refinery with
no addition rows can emit ItemSoul, but cannot invent an ExtraEffect for Append.
ItemSoul seconds use the unsigned range and direct date subtraction; permanent
effects have zero seconds. Invalid legacy dates and unknown catalogs remain
explicit failures. These are scalar predictions, not emitted/native packet proof.

Zero item keys, contradictory item levels, invalid ability sorts, reversed or
negative bands, negative scores, and refinery levels outside signed score-key
range reject the entire score image. Raw plus, sockets, bless, and enchant must
fit their byte consumers; malformed values reject atomically without partial
donation or battle power. Malformed/overflowing purification cannot silently
lower a prestige result. Independent fixtures compare the real read-only
`Arsenal.GetDonation` and `ConquerItem.BattlePower` across quality, family,
socket, and plus boundaries without publishing or saving those disposable items.

Remaining item consumers are explicit gates: soul combat-stat catalogs are not
supplied; `Arsenal_State.Donation` sums saved inscription donations, while
`ArsenalSystem.work` mutates potency/battle power and flags. `ArsenalPacket`
reads these live aggregates, member donation, pagination/order, enhancement,
and each item's battle power (cast to a byte), plus saved inscription donation.
Those aggregate/order/cast results and packet emission are not implemented by
this item score image. `ArsenalView.cs` and `ArsenalCalculate.cs` are empty
placeholders in this tree. Native packet acceptance remains owner-deferred.

Next checkpoint: close the remaining item consumer gates above, then guild and member derived ranks, advertisement and
recruitment behavior through packets 2225 and 2227. Startup and reconnect stay
disconnected until the durable owner is checked before cached use. Paid
creation, join, depart, disband, and stage 303 remain unfinished. Existing
legacy gameplay remains as published.

## Remaining work

Catalog-only Chi pills 3000051/729476–729479, catalog-only Surprise box 722178,
nested/gear packs 721222–721245 (catalog + unreachable EShop 8422 stock),
classified silver leftovers and other leftover split-writes remain open. Native
acceptance and deployment remain open. Parent
[M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.
