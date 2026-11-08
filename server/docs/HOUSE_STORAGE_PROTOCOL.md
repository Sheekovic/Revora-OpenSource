# House storage protocol prerequisites

Current follow-up: [the owned box service](HOUSE_ITEM_BOXES.md) now connects
ordinary purchase, placement, access, transfers, removal and reload in source.
The historical checkpoints below explain its prerequisites; statements that the
service is not connected are superseded by that implementation. Native acceptance,
legacy-data reconciliation and deployment remain open.

2026-09-20, source baseline `2f73f10`. House item boxes are not yet connected.

Approved genuine DX9 executable SHA-256:
`3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60`.
Static inspection identifies CMsgPackage builder 0x97BB5E: declared length 164
at 0x97BB6F, packet ID 1102 at 0x97BB7D, storage key at offset 4, action byte at
12, storage-kind byte at 13, a ushort argument at 14 and item UID at 18.
The existing connection seal adds eight bytes, making the framed request 172.
Callers 0x816175/0x816207/0x8162B3 supply actions 0/1/2 respectively.
The client receiver at 0x97BCC0 reads action byte 12, corroborating that reply
action placement; storage lookup reads the key and kind at 0x97BC19–0x97BC27.
These are static facts; no native storage interaction is claimed.

The previous server `Warehouse.Count` setter wrote the action as a uint at
offset 20 instead of preserving byte 12. List rebuilding therefore changed an
add/remove reply into action 0. `ProcessOwned` also read fields without checking
native request length, and indexed a dictionary with client-supplied storage
identity without checking existence. Empty list requests sent no reply.

The correction requires exact native framing/seal and supported action before
reading fields, preserves byte 12 while rebuilding reply lists, checks storage
lookup, and emits the empty list with capacity. The ushort storage-key model,
portable bag identity truncation, NPC proximity/authorization and non-atomic
legacy item moves are separate unresolved problems. Do not connect new house
boxes to that path until their durable ownership/storage contract is implemented.

Validation: isolated build, 188 synthetic protocol checks and 16 hash-bound
native instruction assertions pass. The fixture checks all three supported
actions, list rebuilding, empty replies/capacity, every truncated request length,
wrong family/length/seal, unsupported actions and trailing bytes. It does not
certify live storage transactions, reconnect, portable bags or native UI clicks.
`tests/Furniture/Verify-WarehouseProtocol.ps1` and
`tools/audit_house_storage_native.py` reproduce this evidence.

The existing House bucket (100) is initialized for every character and stores
60 items; its existence is not proof of an earned, placed or reachable item box.
The runtime item model's warehouse field is ushort, so newly allocated furniture
actor IDs must not be cast into storage keys. Two house boxes need distinct,
durable owned storage identities, and a nonempty box must not be removable.

## Storage identity and reply reuse follow-up

Static inspection of the same approved executable now traces the key/kind pair
through client storage construction, lookup and request construction. Constructor
0x898DE8 stores the first argument as a full DWORD at +0x2C and the second at
+0x28; its default capacity at +0x30 is 60. Getters 0x872D59 and 0x904B53
return those fields to the request builder. Receiver 0x97BC19/0x97BC1E passes
the kind byte and full key to lookup 0x816095, which constructs a two-DWORD
lookup key. No 16-bit truncation occurs along this inspected path.

The UI path at 0x46975D reads a selected actor and derives kind 10 when its
field +0x3CC equals 3, otherwise kind 20, then passes that kind and the selected
identity to 0x81652E at 0x4697CC. The semantic meaning of actor field +0x3CC,
the item-box activation route and rendered UI remain unverified. Do not label
kind 20 as a confirmed house-box contract solely from this branch.

This evidence supports retaining a full-width external key plus kind for new
storage, with an explicit durable mapping to owned contents. It does not justify
casting furniture actor IDs into the existing ushort warehouse field.

Reply reuse now resets the item cursor when Count rebuilds a packet or
Deserialize replaces one. Append rejects null items and writes beyond the
declared record capacity before touching the packet. Five additional fixtures
check distinct records, cursor reset on both paths and unchanged bytes after
rejected writes. The isolated build, 193 synthetic protocol checks and 40 static
instruction assertions pass. This does not validate every item-record field.

Transfer inspection also confirms that legacy withdrawal clears Warehouse before
Inventory.Add succeeds, and ItemUse.Move may merge/delete a stack through
separate database writes. A new box service must commit checked ownership and
location changes atomically, then install runtime state without that merging
path. Legacy transfer recovery, two-box ownership, nonempty removal, cold reload
and native player acceptance remain unfinished; no live deployment occurred.

## Atomic native transfers

Native action-1/action-2 requests now call `Warehouse.Transfer` under the owning
player's item lock. The transfer checks the canonical session, registered storage
instance, trade/booth/event state, exact source container and destination space.
It locks the saved character and checks persisted destination capacity, then
updates only the selected item's location with owner, type, location, stack,
binding and wardrobe preconditions. A missing/suppressed update fences the session
for reload. No item is created or deleted, and no stack is merged.

After commit, runtime containers and the item's warehouse field are updated
without invoking the legacy database-writing inventory move path. A failed
projection requires reconnect; production item loading reconstructs the committed
location. The native reply/ordinary item update follows the commit. Existing
storage material restrictions remain, and their rune check now uses item type
rather than the unrelated item UID.

The 393-check housing suite includes 17 new storage assertions: concurrent
deposit/withdrawal, enabled auto-stacking and item-use flags, preserved UID/stack,
two production reloads, suppressed writes in both directions with reconnect and
retry, full bag/storage, stale stack and owner, foreign UID, trading, item
attributes/binding and persisted capacity despite stale runtime state. These
fixtures use ordinary accounts with supplied materials, not earned storage boxes.

The Magnolia suite passes 838 checks, including its eight-day earned saddle
journey through storage and Wrangler enrollment. Its warehouse fixtures now use
the audited sealed 172-byte request rather than an outbound reply constructor.
The same request correction is applied to Guild prize rollover regressions.
All 344 Guild season checks pass, including withdrawal of a converted trophy
and concurrent native warehouse transfers during rollover. The 193 protocol
checks also pass against this build.

This is a prerequisite for boxes, not the complete box feature. Legacy `Add`
and `Remove` remain for existing non-packet callers/hydration and have not been
made generally transactional. Full-width box identities, box activation and
ownership, proximity/password authorization in the legacy packet path, nonempty
removal, and genuine-client acceptance remain open. Nothing is deployed.

## Box representation and ordinary interaction

The next static trace resolves the previously unknown actor field +0x3CC:
the spawn parser reads protobuf tag 0x38 (field 7) into +0x40 at
0x96E44D/0x96E456, and receiver 0x96E7B4/0x96E7BA copies it into actor
+0x3CC. It is the spawn type. Types 25/26 branch to terrain-scene creation at
0x96E8EB; type 4 takes the ordinary NPC branch at 0x96E74C. Therefore the
decorative terrain-furniture representation cannot simply be reused for a box.

The hash-checked installed `ini/npc.ini` defines NPC model 820 as ItemBox,
SimpleObjID 315. Both active server and client item catalogs contain item
721189 named ItemBox, with gold field 11 and CP field 36 both zero. These do
not establish a retail price or authorize a free purchase. The item/model
association is a Revora mapping supported by matching names and the existing
legacy box branch, not a recovered original server implementation.

The ordinary NPC click switch reads actor type at 0x724A56 and uses the tables
at 0x725531/0x7254DD. Both types 3 and 4 select 0x724B34. That branch passes
the actor UID to 0x8D5931, which builds CMsgNpc with action 0, the full UID and
two zero arguments at 0x8D5967, then sends at 0x8D5976. The existing preview
activation branch likewise selects the ordinary representation for type 4.
Combined with the storage window branch already traced at 0x4697A6, a type-4
box selects kind 20; type 3 selects kind 10. This closes the spawn-type meaning
but does not prove rendered window behavior or a completed box interaction.

`FurnitureRequest.TryInteraction` now decodes the bounded action-0 contract
without narrowing the actor UID. It shares canonical-varint, exact-field,
length/seal and zero-argument validation with removal, while keeping those
actions distinct. It is not dispatched until the owned box service exists.
There is no player-facing box purchase, placement or access grant in this step.

Validation: isolated build, 82 furniture/click protocol checks, 65 hash-bound
native instruction assertions, two click-table checks and the exact NPC asset
profile pass. The added tests cover full uint identities, field ordering,
wrong action/family/arguments, duplicate/unknown fields, overlong/overflowing
varints, every truncated click prefix and a wrong seal. No client was run or
modified, and no source or client release was deployed.

## Preserving full storage keys

A read-only schema inspection confirms `public.items.warehouse` is PostgreSQL
`integer` (32-bit signed). No DDL or data migration is needed for the reserved
furniture actor range 1,800,000,000 through 1,899,999,999. The old restriction was
in `ConquerItem.Warehouse`, `WarehouseID`, item deserialization and the Guild
rollover snapshot, all of which narrowed to ushort. They now retain uint keys.
Transfers reject zero and keys above the database's signed maximum before
mutation; this does not claim persistence of the entire unsigned wire range.

The packet handler resolves an exact registered key first. It only falls back
to a legacy low-16-bit key for this account's exact EntityID or an exact owned
portable-bag UID. Arbitrary values sharing a town warehouse's low bits no longer
alias it. Existing account and sash numeric keys remain explicitly unchanged;
no item rows are renamed or moved by the compatibility change. Owned legacy
sash containers are registered before item hydration, so their contents can be
loaded without incorrectly looking for an item whose UID equals the shortened key.

Legacy collisions cannot be resolved from a shortened key alone. In particular,
two old sashes sharing low bits, or a sash colliding with a town/account key,
still need an evidence-backed migration/reconciliation policy. This change does
not claim to separate those existing contents. New box identities must use exact
registered keys and durable ownership records, not the legacy alias mechanism.

Full-width registration in the fixture is a supplied prerequisite. A production
house-box registry still needs to create and reload those registrations, enforce
box count and access, and protect nonempty removal. Wider item keys alone do not
enable a box service. No schema, live data, Host or client was changed.

The isolated build and 402 housing/storage checks pass. Nine identity checks
cover two large keys with matching low bits, rejection of an unregistered town
alias, production item hydration, cross-key withdrawal refusal, exact-key
withdrawals, an owned legacy sash through reconnect, account compatibility and
the signed database limit. All 347 Guild season checks pass, including earning
a Gold Prize, depositing it under a large key, offline seasonal rollover before
hydration and withdrawal of the same permanent trophy. The fixture explicitly
registers the large containers before load; durable house-box registration is
not claimed by these results.
The 838-check Magnolia regression and 193 warehouse protocol checks also pass
against the updated model and loader.
