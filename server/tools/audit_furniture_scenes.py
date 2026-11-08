"""Audit the twelve installed furniture scenes without changing map collision.

Layout is corroborated by the local legacy scene reader and exact file lengths.
Flag meaning and world-coordinate origin are deliberately not inferred here.
"""
import hashlib
import json
from pathlib import Path
import struct
import re
from audit_warrior_epic import sections, require

HASHES = (
    "07900ec0f43f197b2f76613ff7da017c2da4b3aa3488c8ef8a5ffcd1198073f6",
    "a7ad508c46018444d259bacf2473cc042d19a33478fc26e77f8af6f239a1d59a",
    "73d4e62c57038ebb1082d0663fbc14f028cbbb5865228d550ca874cdcad1a5ec",
    "9f2d85d29265892eae8595517b790280dfe4f4f501804f6c0f9a8669d6432bd3",
    "0d9cbcf067b87703f617b2f1152fe07aced52ae3d47472fae7cdd801d984867b",
    "77fc1116b2188a41f50a8d3f5c12c8cc536905921ecf8b33a37f1c74e2fbef2c",
    "dd6b27f57a0eca9fa9896a2aac30f9d11aad51723af146a9d4061afd6cf1a8a3",
    "4179040b031b3d1b19aa00bb7fff700a243f546603c2db33a3a85a925176fa71",
    "75821be46f8a86ad023a710b2ec506f5a47372876f507e195d693069ca8897c0",
    "89cab430f481f18f725d928348e688924e70e0fab119b465c21fb349333aa20b",
    "1ae6420969d499223b49d1c10db2b2fe1ad631478c963ea8ce6610b1eac39399",
    "019095cc5763fae8df46f5b495048125fe5a286f7f2bb66cd8e6e7eee6f9b67a",
)


def inspect(data):
    require(360 <= len(data) <= 360 + 16 * 16 * 12, "Scene size outside basic furniture bounds")
    require(struct.unpack_from("<i", data)[0] == 1, "Only audited single-part furniture is supported")
    width, height = struct.unpack_from("<ii", data, 336)
    require(1 <= width <= 16 and 1 <= height <= 16, "Invalid furniture dimensions")
    require(len(data) == 360 + width * height * 12, "Scene length disagrees with cell count")
    origin = struct.unpack_from("<ii", data, 348)
    cells = [struct.unpack_from("<iii", data, 360 + i * 12) for i in range(width * height)]
    require(all(row[0] in (0, 1) for row in cells), "Unknown scene cell flag")
    return dict(width=width, height=height, start=origin,
                access_mask=sum(row[0] << index for index, row in enumerate(cells)),
                zero_flag_cells=sum(row[0] == 0 for row in cells),
                one_flag_cells=sum(row[0] == 1 for row in cells),
                height_range=[min(row[2] for row in cells), max(row[2] for row in cells)])


def audit():
    client = Path(__file__).resolve().parents[3] / "6711"
    terrain = (client / "ini/terrainnpc.ini").read_bytes()
    require(hashlib.sha256(terrain).hexdigest() == "9bfdf8d8a8ed4ef49885632736526d7eb3be207a99ca813db40780c2fd30e12c", "Terrain catalog changed")
    blocks = list(sections(terrain))
    result = []
    for model, expected in enumerate(HASHES, 1):
        entries = [values for header, values in blocks if header == ("[NpcType%d]" % model).encode()]
        require(len(entries) == 1, "Missing or duplicate terrain model")
        relative = "map/scene/house%d.scene" % (model + 12)
        require(all(entries[0].get(("Dir%d" % i).encode()) == relative.encode() for i in range(8)), "Furniture directions no longer share one scene")
        data = (client / relative).read_bytes()
        require(hashlib.sha256(data).hexdigest() == expected, "Scene preimage changed")
        result.append(dict(model=model, item=721176 + model, sha256=expected, **inspect(data)))
    source=(Path(__file__).resolve().parents[1] / "Game/Features/FurnitureFootprint.cs").read_text()
    configured=[tuple(map(int,match)) for match in re.findall(r"new Shape\((\d+),(\d+),(\d+)\)",source)]
    require(configured == [(row["width"],row["height"],row["access_mask"]) for row in result], "Server furniture access masks differ from installed scenes")
    require(all(row["start"] == (0,0) for row in result), "Nonzero scene start requires updated server mapping")
    return dict(scenes=result, confidence="static access masks and source parity; native rendered acceptance and height-based movement remain open", collision_changed=False)


if __name__ == "__main__":
    print(json.dumps(audit(), indent=2))
