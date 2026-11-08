# The Missing Key: hunting and admission

M6.45e connects ordinary monster kills on Frozen Grotto floors three/four to the
fifth-floor Guardian service implemented in [M6.45d](GROTTO_PASSAGE.md). It is
implemented and verified in staging; native presentation and deployment remain
pending.

## Route and evidence

Kill ordinary monsters on map 1999 or 2054, collect a Grotto Key (721347), then
give one key to Guardian 7764 on floor four at (388,460). The player arrives on
floor five and earns exploration achievement 10805 through the existing travel
service. No GM status, named account or quest-acceptance flag is required.

This follows the location and admission requirements in the official
[Missing Key guide](https://co.99.com/guide/quests/10expansion/missing_key.shtml).
The original guide does not establish a drop percentage. Revora uses
`Config.GrottoKeyBasisPoints = 100`: a 1% independent roll per eligible monster
life. The setting is clamped to 0–10,000; zero disables the source. This is an
explicit Revora balance policy, not an asserted original rate or guaranteed
drop after a kill count. The global generic-item multiplier does not affect it.

Read-only installed spawn inspection found:

| Floor | Map | Family | Monster | Spawn records |
| --- | --- | --- | --- | --- |
| 3 | 1999 | 2460 | DarkLady | 3 |
| 3 | 1999 | 2461 | DarkElf | 33 |
| 4 | 2054 | 2466 | GrottoLady | 32 |

These are spawn records, not claimed concurrent populations. Runtime catalog
fixtures confirm those three profiles are ordinary eligible monsters. Existing
spawns, combat strength and respawn settings are unchanged. Copied map instances,
bosses, guards, revivers, companions and adventurers cannot produce this loot.

## Ground ownership and persistence

The generated key is an unbound, permanent, single item. It uses the normal
floor-item presentation, ownership/team window and expiration. The collector
must stand on its actual tile. Full inventory leaves it on the ground. Keys can
also be consumed from existing stacks by the Guardian; this pickup creates a new
single-item stack and requires a free slot, matching the managed-loot path.

Each floor object has server-owned provenance independent of mutable item flags.
Repeated death dispatch cannot reroll a failed chance. Pickup locks the
collector inventory and floor object, validates ownership and item preimages,
then inserts the item and `revora_grotto_key_pickups` receipt in one transaction.
Only a committed transaction removes the floor object and refreshes inventory.
Receipt/item insertion failure preserves the still-live ground item and fences
the collector for reload. A claimed key survives restart through ordinary item
persistence; unclaimed ground loot is transient, like other floor items.

No CP, RVP, gold, mentor composing credit or separate completion reward is added.
Existing VIP-4 admission remains available without a key. The Guardian briefly
explains where to hunt and honors an interrupted paid passage after reconnect.

## Validation and limits

The staged build passes 76 Grotto checks, including the production drop and both
pickup dispatchers, all three installed monster profiles, chance boundaries,
single-roll monster lives, competing collectors, malformed frames, expiration,
full/stale inventories, forged provenance, raised/suppressed database writes and
the complete ordinary hunt → pickup → save → reload → key payment → fifth-floor
achievement route.

Related regression suites pass: 47 monster equipment, 105 travel, 179 cosmetic
ownership, 9 quest-packet, 43 Labyrinth, 35 Release Souls, 65 Summer, 34 Sky Pass,
25 Snake Islands and 3 producer-audit checks. Total: 621 checks. These use the
disposable validation schema; production spawn inspection is read-only.

Staged server SHA-256:
`651E264C3C5E074D0DE68DC8389ED5BFA799B7C226985D522F344B92A05F1CFC`.

This completes the server earning/admission route, not native-client acceptance
or the separate [Dragon Slayer quest](DRAGON_SLAYER_RESEARCH.md), whose identities
and crafting prerequisite are now reviewed. Source producer coverage remains 156/337.
No live restart, deployment, client installation, public patch or website
publication was performed.
