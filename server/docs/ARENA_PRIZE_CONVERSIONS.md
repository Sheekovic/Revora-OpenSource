# Arena prize material conversions

The four existing material recipes now use one transaction for exact material
consumption, output insertion and an immutable receipt:

| Material | Required units | Output |
| --- | ---: | --- |
| Radiant Star Stone (3009002) | 10 | 3009003 |
| P7 Weapon Soul Scrap (3004243) | 15 | P7 Weapon Soul Pack (3004247) |
| P7 Equipment Soul Scrap (3004244) | 15 | P7 Equipment Soul Pack (3004248) |
| Sacred Refinery Scrap (3004245) | 15 | Sacred Refinery Pack (3004249) |

These are existing Revora recipes, not newly claimed retail mechanics. Previously
output creation preceded material removal and always requested an unbound output.
The new output is bound if any consumed material is bound. Both P7 pack opening
paths independently enforce bound souls, including packs made from unbound scraps.

The clicked material is consumed first, followed by other usable stacks ordered
by UID. Zero-stack legacy items count as one unit. Locked, suspicious, timed,
wardrobe and stored materials are excluded. Trade/booth sessions cannot convert.
Exact saved ownership, type, binding and stack counts are checked under the
canonical player's inventory lock and durable owner row lock. A failed write
rolls everything back and requires reconnect before retry. Output and partial
stack updates are projected after commit. A saved one-second per-owner/recipe
cooldown limits repeat clicks across reconnects; later intentional conversions
remain possible. Capacity includes slots freed by fully consumed materials.

## Validation and remaining work

The focused P7 fixture includes ordinary non-GM material conversion, mixed
binding, split stacks, zero-stack items, full bag reuse, partial stack capacity,
locked and insufficient materials, simultaneous requests and reconnect cooldown.
Injected DELETE, UPDATE, output INSERT and receipt INSERT failures preserve
materials; production reconnect can retry. A complete material-to-pack-to-bound
P7 path survives production reload. Materials are fixture supplied: this does
not prove Arena prize earning. Existing P7 output pool tests remain included.

The build passes. Test results are recorded in QUEST_EVENT_STATUS.md; ignored
logs are arena-conversions-tests.log and arena-conversions-packets.log under
obj/chi-refill-validation/. No production process or installed client changed.

Arena custom prize entitlements/delivery and activation remain unfinished.
Sacred Refinery Pack opening and the resulting star item's use still need their
own transaction review. This batch does not claim those downstream paths are
safe merely because material conversion is atomic. Native display and live
restart acceptance remain separate gates.
