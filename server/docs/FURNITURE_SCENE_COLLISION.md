# Furniture scene collision investigation

2026-09-20. The initial research used source baseline `ed23f2f`; the
follow-up below now connects scene access-mask collision. Native rendered and
height-based movement acceptance remain open.

The twelve installed basic furniture scenes contain one part each. Dimensions
range from 1x1 to 3x2 or 2x4, with distinct per-cell flag and height values.
`tools/audit_furniture_scenes.py` binds each scene to its SHA-256, verifies the
terrain catalog and all eight direction references, and checks bounded dimensions,
exact file length and cell flag values. It emits derived metadata, not assets.
All twelve pass; three synthetic layout/malformed-input tests also pass.

The observed single-part layout is count at byte 0, width/height at 336/340,
start coordinates at 348/352 and twelve-byte cell records from 360. Exact length
is `360 + width * height * 12`. The legacy reader corroborates these offsets.
Native scene loader 0x9A430D separately reads the part header, then iterates rows
and columns at 0x9A461A–0x9A4689. At 0x9A4662–0x9A4676 it stores the low sixteen
bits of the second, first and third cell integers at tile offsets 4, 6 and 8.
Do not infer flag polarity or height collision solely from those stores.

Native terrain creation 0x9BF251 indexes the selected direction's scene path
at 0x9BF2DC–0x9BF2E0 and loads it through 0x9A430D at 0x9BF2F2. All eight
directions in each of these twelve installed terrain entries refer to the same
scene. That proves shared source data, not an independently verified rotation
or world-coordinate transformation.

The existing server has two incompatible scene placement formulas:

- one map-loading branch uses `anchor + start + index - size`;
- `MergeSceneToTextureArea` uses `anchor + start - index`.

Its `NoAccess` array also stores `cellFlag == 0` and passes that value to a floor
setter where true means walkable. Names alone are insufficient evidence. Three
basic scenes have all-zero first cell values despite nonzero heights, so treating
the whole rectangle or every nonzero-height cell as blocked would be a new
unverified gameplay rule.

## Confirmed access-mask follow-up

The approved client applies loaded cells through 0x9BC71E. It resolves each part
anchor using 0x9BC297, subtracts the column and row at 0x9BC7CA/0x9BC7CD, looks up
the world tile at 0x9BC7DD, and appends the resulting overlay at 0x9BC84A.
Scene placement converts an anchor coordinate to `anchor * 64 + 32` (0x9DA740).
Part offsets from the scene reader are `start * 64 - size * 32 + 32`; the
container adds those offsets to the scene position (0x9DBDFF?0x9DBE14).
The part anchor getter adds half the part size, divides by 64 and subtracts
0.5 before truncating. For the positive house coordinates, this resolves to
`anchor + start`; the cell loop then gives **anchor + start - column/row**.
All twelve audited scene starts are zero.

The scene's first cell integer becomes overlay word +2 at 0x9BC819/0x9BC81D.
Getter 0x99D500 follows the overlay chain through 0x99D3C3 and returns that word.
Callers at 0x994812 and 0x9958C6 test equality with 1 and branch out of their
eligibility paths. This supports the access-mask rule: value 1 blocks; value 0
does not itself block. The third cell integer is separately added to the
underlying tile height at 0x9BC830/0x9BC835. Height is not equivalent to the
access flag and is not imported into the server by this change.

`FurnitureFootprint` now carries the twelve derived dimensions and masks;
`audit_furniture_scenes.py` checks them against every exact installed scene.
The same footprints apply on insertion and cold map reconstruction. A separate
floor flag records furniture blocking, so removal preserves terrain and remaining
furniture blockers. Whole-footprint overlap, bounds, player occupancy and exit
clearance are checked before item consumption. Reserving the full scene rectangle
against other furniture is a Revora placement rule; only flagged cells block
walking. All-zero masks remain walkable instead of becoming guessed walls.

Validation: isolated build and 376 housing checks pass, including the purchased
furniture journeys, cold reconstruction, selective masks, all eight directions,
independent terrain blockers and overlapping furniture removal. Twelve scene
preimages, three parser tests and 88 approved-client instruction assertions pass.
Full native visual walking/jumping and height-based movement remain open. The
broader furniture milestone stays unchecked; no deployment occurred.
