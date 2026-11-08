"""Synthetic terrain and map-index fixtures; no client assets."""
from pathlib import Path
import struct
import sys
import unittest
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
from audit_trojan_epic import approach, bindings


def index(entries):
    return struct.pack('<I', len(entries)) + b''.join(
        struct.pack('<II', identity, len(path)) + path + struct.pack('<I', 0)
        for identity, path in entries)


def grid(blocked=()):
    tiles = bytearray(20 * 20)
    for x, y in blocked:
        tiles[x + y * 20] = 1
    return struct.pack('<ii', 20, 20) + tiles


class TrojanEvidenceTests(unittest.TestCase):
    def test_duplicate_bindings_preserved(self):
        self.assertEqual(bindings(index([(1, b'a'), (1, b'b')])), {1: ['a', 'b']})

    def test_truncation_rejected(self):
        raw = index([(3825, b'map/map/test.7z')])
        for length in range(len(raw)):
            with self.subTest(length=length), self.assertRaises(ValueError):
                bindings(raw[:length])

    def test_bad_count_path_and_trailer(self):
        for raw in (struct.pack('<I', 100001), index([(1, b'')]), index([]) + b'x'):
            with self.assertRaises(ValueError):
                bindings(raw)

    def test_blocked_npc_tile_allows_approach(self):
        result = approach(grid([(19, 19)]), (0, 0), (19, 19))
        self.assertTrue(result['reachable'])
        self.assertEqual(result['steps'], 26)
        self.assertNotEqual(result['approach'], [19, 19])

    def test_disconnected_room(self):
        self.assertFalse(approach(grid([(10, y) for y in range(20)]), (0, 0), (19, 19))['reachable'])

    def test_entry_cannot_overlap_talker_footprint(self):
        for start in ((10, 10), (9, 9), (11, 11)):
            with self.subTest(start=start), self.assertRaises(ValueError):
                approach(grid(), start, (10, 10))

    def test_invalid_terrain_and_coordinates(self):
        for raw, start, target in (
            (b'', (0, 0), (19, 19)),
            (grid()[:-1], (0, 0), (19, 19)),
            (grid()[:-1] + b'\x02', (0, 0), (19, 19)),
            (grid([(0, 0)]), (0, 0), (19, 19)),
            (grid(), (0, 0), (20, 19)),
        ):
            with self.assertRaises(ValueError):
                approach(raw, start, target)


if __name__ == '__main__':
    unittest.main()
