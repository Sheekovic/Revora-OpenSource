# Housing furniture protocol investigation

Implementation follow-up: [the basic furniture service](HOUSE_FURNITURE.md)
supersedes this checkpoint's missing dispatch and ownership service. The source
now connects purchase, selection, placement and removal; native acceptance and
the documented scene-footprint limitation remain open.

2026-09-20, source baseline `03aa304`. Furniture remains unimplemented; no
achievement credit, inbound handler, native mutation or release is enabled here.

## Approved native evidence

Read-only static inspection targets `6711/Env_DX9/Conquer.exe`, x86, image base
0x400000, SHA-256
`3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60`.
`python tools/audit_furniture_native.py` verifies 50 instruction assertions.
No proprietary bytes or disassembly are committed.

- CMsgNpcInfo's serializer at 0x96DB3B associates object storage offsets
  0x34/0x38/0x3C/0x40/0x44 with protobuf fields 4/5/6/7/8.
- Builder 0x96DFBE takes three arguments, marks fields 4/5/6 present, serializes,
  and assigns packet ID 2030. It does not set field 2 (the spawn actor UID).
- Preview-confirm paths at 0x792E6B and 0x792EEA call that builder, followed by
  sends at 0x792E7A and 0x792EF9. Both operate on local preview identity
  0xEE6B2801. The first obtains the preview object's coordinates and an appearance
  value; the second uses the alternate scene representation.
- Preview activation is 0x792C01. The call at 0x92AF22 is reached from inbound
  CMsgNpc processing at 0x92AEE4 when field 1 equals 5. Field 3 selects the
  representation; field 4 supplies the preview value. Values 25/26 select the
  alternate branch, among other explicitly checked values. Serializer 0x92A766
  establishes these field numbers; the server's current `CMsgNpc` field names
  are not reliable semantic documentation for this direction.
- A separate five-argument builder at 0x96DF20 sends fields 4 through 8. Its
  caller 0x8B9777 uses a different, restricted placement path. It must not be
  conflated with the three-argument preview path or used to infer a furniture UID.

The two removal-request call sites 0x724A2E and 0x725235 use the CMsgNpc builder
with action 6, the selected actor identity and two zero arguments, then send.
Alternate preview lookup at 0x9BEC1B reconstructs the appearance as model * 10
plus direction at 0x9BEC57/0x9BEC5A.

These are static control-flow and serialization facts, not an observed furniture
placement in the genuine client. The exact furniture-item-to-appearance mapping,
item-use activation and complete destructive removal flow still need verification.

## Installed basic furniture assets

`ini/terrainnpc.ini` SHA-256 is
`9bfdf8d8a8ed4ef49885632736526d7eb3be207a99ca813db40780c2fd30e12c`.
Its first twelve terrain types have GBK names matching the basic lamp, low shelf,
cabinet, high shelf, low chest, rosewood cabinet, high cabinet, folding screen,
dresser, basin rack, chair and low table. They reference existing
`map/scene/house13.scene` through `house24.scene` in the same order. All eight
directions reference the corresponding scene. This supports a Revora mapping of
basic items 721177 through 721188 to those twelve terrain models; it does not
prove the original game's server-side mapping. ItemBox 721189 is excluded from
this decorative-furniture mapping and needs its separate storage service.

The installed NPC catalog contains FurnitureNPC 30161, type 2, mesh 8120,
at Twin City 339,528, but no actors on furniture-store map 1511. Its current
dialog offers travel to 1511 without a connected purchase path. All twelve basic
items have zero silver catalog prices, so a usable earning route still requires
an explicit Revora furniture price policy and purchase service.

## Bounded decoder prerequisite

`FurnitureRequest` separates inbound preview placement from outbound spawn
contracts. It accepts exactly fields 4/5/6 for placement and action-6 fields
1/2/3/4 for removal, with canonical bounded varints, exact frame length and seal,
no duplicate/unknown fields, and no narrowing overflow. It does not authorize
coordinates, appearance or actor ownership; those are service responsibilities.
The source is compiled but not dispatched, so no player mutations are enabled.

Isolated build and 44 synthetic packet checks pass via
`tests/Furniture/Verify-FurnitureProtocol.ps1`. Checks cover valid field ordering,
ushort boundaries, the other placement family, every truncated prefix, malformed
varints, missing/duplicate fields, wrong actions/seals and concatenated frames.
These are reconstructed fixtures from static evidence, not native captures.

## Server implications and next implementation

`Network/PacketHandler.cs` has no incoming 2030 dispatch. The uncalled
`HouseTable.SaveFurniture` deserializes a spawn and inserts its UID, appearance,
type and coordinates without a matching owned item debit. It cannot be connected
as the placement implementation. `NpcSpawn.CMsgNpcInfo` omits field 8 and models
outbound spawn fields as required; it is also unsuitable as a validated inbound
placement contract.

Trace ordinary item use to the action-5 activation and verify the appearance
mapping against installed furniture assets. Then retain the selected owned item
in a bounded server offer, validate the three-field request strictly, and commit
item consumption, durable owner/map placement and achievement together. The
server must allocate the furniture UID rather than trusting a preview identity.
Test house ownership, level capacity, floor/portal occupancy, stale selection,
duplicate confirmation, reconnect, failed writes and destructive removal.
Keep achievement 10101 and the roadmap furniture milestone unchecked until the
actual earning and placement path works for ordinary players.
