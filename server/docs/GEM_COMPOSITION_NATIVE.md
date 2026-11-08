# Gem composition: approved 6711 static evidence

Research date: 2026-09-20. Source baseline: `b6264ab`.
Inspected only the owner's `6711/Env_DX9/Conquer.exe`, SHA-256
`3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60`.
The executable was read, not executed or modified. No proprietary bytes or
disassembly are included here. Addresses are virtual addresses for image base
`00400000`, x86. Reproduce the bounded checks with
`python tools/audit_gem_composition_native.py` (pefile and capstone required).

## Request construction

The common constructor `008990F2` sets opcode 1009 and declared length 95.
Its three arguments occupy UInt32 offsets 8 and 12 and UInt16 action offset 20.
It writes ticks at 4 and a separately sampled tick XOR hero field at 22.
Do not require these two ticks to be identical. With the existing eight-byte
transport seal the ordinary request is 103 bytes.

The ordinary composer sender `004DF502` reads its selected descriptor's type
at offset 28, subtracts one, and passes this as argument one. Argument two is
the result of `008794F4`; action is 39. This classification helper returns one
for object byte `+0x274` values 1 through 3 and zero otherwise. Inventory
counting at `0088887C` matches both type and this classification, so combining
the two pools indiscriminately would contradict the client selection behavior.
The server's item serializer labels byte 32 Bound; a native item update path at
`00980906` passes byte 32 through setter `008DF66B` to object `+0x274`.
This supports bound/unbound interpretation for the server's emitted values
0/1; meanings of additional native values 2/3 are not independently established.
Follow-up tracing at `004DFCD9` resolves the preview assignment: dialog field
`+0x1F4` is checked against input type plus one, then populated by constructing
that incremented type at `004DFD77`. The sender's subtraction therefore restores
the input type. This closes the input-type ambiguity at static-code level.

The mixed Tortoise sender `0048EF24` requires exactly seven vector entries and
at least 100,000 silver. It builds action 51 with both initial arguments zero,
then appends the seven selected item UIDs through `00898FF5`. That appender
reads count at offset 26, writes each UID at `91 + 4 * count`, adds four to
declared length, and increments count. Thus the resulting declared length is
123 and transport length is 131, with seven UIDs starting at offset 91.
These are owned-item identities, not seven predetermined item types.

## Response consumption and remaining limits

The native action-39 receiver at `0089AFFA` dispatches offset 12 to UI event
3326. The action-51 receiver at `0089B7E7` dispatches each appended value to
event 3547 and offset 12 to event 3548. Their consuming UI callbacks still need
inspection: these facts alone do not prove that the old server's success value
one is correct or sufficient to refresh the dialog.

The audit checks 45 instructions at aligned boundaries, under the exact binary
hash. It is regression evidence for these static observations, not a capture,
full sender emulator, live transaction, or proof of display behavior. Response
UI semantics, ordinary-player persistence, malformed
requests, replay/concurrency, material and fee rollback, and genuine-client
acceptance must all be closed before claiming the feature complete.

## Repair requirements

The existing compiled GemCompose handler ignores removal failure and fees;
ToristSuper ignores the selected UID list. Replace these with a recipe whitelist,
saved-owner/material checks and one atomic material/payment/reward transaction.
Preserve separate binding pools and output binding. Reject duplicate, foreign,
equipped, stored, locked or otherwise ineligible selected UIDs before mutation.
Strictly validate request framing and selected-count bounds.

The [official PC guide](https://co.99.com/guide/guides/gemcomposing.shtml)
specifies fifteen same-type normal gems and 10,000 silver for refined; fifteen
refined and 800,000 silver for super, except Tortoise costs 1,000,000 silver.
Its separate seven-type recipe costs 100,000 silver and lists multiple Tortoise
outcomes without odds. Do not present invented odds or the old single-refined
grant as retail parity. A server policy decision remains necessary for
that randomized recipe; ordinary deterministic composition can proceed separately.

No gameplay change, deployment, client patch or milestone completion is claimed
by this evidence batch.

## Implementation follow-up

`GemComposition` replaces the deterministic action-39 handler. Its ten families
are the installed Phoenix, Dragon, Fury, Rainbow, Kylin, Violet, Moon, Tortoise,
Thunder and Glory gems; only normal/refined inputs are admitted, with Tortoise
restricted to the published refined-to-super recipe (19 recipes total). It consumes
fifteen eligible individual objects of the requested binding, matching the
native object-counting behavior. Legacy zero-sized singleton items are accepted;
stacked gems, locked/stored/equipped/temporary/suspicious materials are excluded.

The published fee, exact owned materials, reward and material/payment receipts
commit together under canonical session and database owner locks. Saved bag
identities and each material preimage are checked. Output binding is preserved.
Saved silver and inventory changes are sent after commit; any transaction or
projection failure fences the session for reload. Full bags may use the slots
freed by their fifteen consumed materials.

Exact request hashes suppress replays across reconnects. A one-second durable
cooldown on matching type/binding additionally suppresses rapid clicks whose
native ticks differ. The hash is a replay key, not authentication or proof of
client integrity. The current server success parameter one is retained with
the existing response serializer, pending the UI acceptance noted above.

The mixed action-51 branch fails closed for all players with a service-unavailable
message. Its previous unchecked consumption/grant path is removed. It is an open
feature, not a completed Tortoise alternative. Neither this change nor static
evidence certifies live client behavior; no deployment is part of this batch.

The isolated production build and `Verify-PotencyHunt.ps1 -GemsOnly` pass **115
checks**. The fixture exercises the real ItemUsage dispatch for all 19 recipes
and both bindings with exact silver, production character/item reload, same-frame
replay with another full batch available, rapid requests with different ticks,
subsequent intentional composition, and full-bag replacement. Negative cases
cover missing/mixed/locked/stored/equipped/suspicious/stacked/temporary materials,
insufficient silver, trade, death, stale database ownership, invalid framing,
opcode, seal, binding/count/type/action and unsupported normal Tortoise inputs.
Suppressed silver update, material deletion, output insertion, composition receipt
and material receipt each roll back and permit one retry after production reload.
The mixed recipe cannot enter the removed legacy grant path. These tests use a
new private fixture schema; initial gems and silver are fixture inputs, not proof
of the later complete mined-gem-to-house journey. Static audit: 45 checks pass.
