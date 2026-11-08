import struct
import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]/'tools'))
from audit_elimination_world import routes


class TerrainTests(unittest.TestCase):
    def terrain(self, blocked=()):
        cells = bytearray(30*30)
        for x, y in blocked:
            cells[x+y*30] = 1
        return struct.pack('<ii', 30, 30)+cells

    def test_connected(self):
        result = routes(self.terrain(), [(20,20)], (2,2), [(20,20)])
        self.assertEqual(result[0]['steps'], 12)

    def test_disconnected(self):
        with self.assertRaisesRegex(ValueError, 'Unreachable'):
            routes(self.terrain([(10,y) for y in range(30)]), [(20,20)], (2,2), [(20,20)])

    def test_no_diagonal_corner_cutting(self):
        with self.assertRaisesRegex(ValueError, 'Unreachable'):
            routes(self.terrain([(1,0),(0,1)]), [], (0,0), [(20,20)])

    def test_footprint_blocks_origin(self):
        with self.assertRaisesRegex(ValueError, 'Blocked origin'):
            routes(self.terrain(), [(3,3)], (2,2), [(20,20)])

    def test_invalid_terrain(self):
        for data in (b'', struct.pack('<ii', -1, 30), self.terrain()[:-1], self.terrain()[:-1]+b'\x02'):
            with self.assertRaises(ValueError):
                routes(data, [], (2,2), [(20,20)])

    def test_blocked_actor(self):
        with self.assertRaisesRegex(ValueError, 'Actor center'):
            routes(self.terrain([(20,20)]), [(20,20)], (2,2), [(20,20)])


if __name__ == '__main__':
    unittest.main()
