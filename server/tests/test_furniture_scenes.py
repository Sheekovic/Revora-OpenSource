from pathlib import Path
import struct
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from audit_furniture_scenes import inspect


class FurnitureSceneTests(unittest.TestCase):
    def scene(self):
        data = bytearray(384)
        struct.pack_into("<i", data, 0, 1)
        struct.pack_into("<ii", data, 336, 2, 1)
        struct.pack_into("<iii", data, 360, 1, 0, 80)
        struct.pack_into("<iii", data, 372, 0, 0, 20)
        return data

    def test_layout_without_collision_inference(self):
        row = inspect(self.scene())
        self.assertEqual((row["width"], row["height"]), (2, 1))
        self.assertEqual((row["zero_flag_cells"], row["one_flag_cells"]), (1, 1))
        self.assertEqual(row["height_range"], [20, 80])

    def test_bad_sizes_and_counts(self):
        for data in (b"", self.scene()[:-1], self.scene() + b"\0"):
            with self.assertRaises(ValueError):
                inspect(data)
        for offset, value in ((0, 2), (336, 0), (336, -1), (340, 17)):
            data = self.scene()
            struct.pack_into("<i", data, offset, value)
            with self.assertRaises(ValueError):
                inspect(data)

    def test_unknown_flags(self):
        data = self.scene()
        struct.pack_into("<i", data, 360, 2)
        with self.assertRaises(ValueError):
            inspect(data)
