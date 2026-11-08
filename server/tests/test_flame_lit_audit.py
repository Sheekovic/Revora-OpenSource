import sys
import struct
import unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parents[1]/'tools'))
import audit_flame_lit as audit

class FlameAuditTests(unittest.TestCase):
    def test_approach_avoids_reserved_stone_and_stays_in_range(self):
        points=[(0,0),(10,10),(19,19)]
        found=audit.approaches(struct.pack('<ii',20,20)+bytes(400),points)
        for stone,point in zip(points,found):
            self.assertTrue(0<=point[0]<20 and 0<=point[1]<20)
            distance=max(abs(stone[i]-point[i]) for i in (0,1))
            self.assertGreater(distance,1);self.assertLessEqual(distance,6)
    def test_blocked_approach_fails(self):
        with self.assertRaises(ValueError):audit.approaches(struct.pack('<ii',20,20)+bytes([1])*400,[(10,10)])
    def test_truncated_oversized_unknown_and_out_of_bounds_fail(self):
        valid=struct.pack('<ii',20,20)+bytes(400)
        for data in [b'',valid[:-1],valid+b'x',struct.pack('<ii',-1,20),struct.pack('<ii',4097,20),valid[:-1]+b'\x02']:
            with self.assertRaises(ValueError):audit.approaches(data,[(10,10)])
        for point in [(-1,0),(20,0),(0,20)]:
            with self.assertRaises(ValueError):audit.approaches(valid,[point])
    def test_only_reachable_radius_cell_is_selected(self):
        grid=bytearray([1])*400;grid[16+10*20]=0
        self.assertEqual([(16,10)],audit.approaches(struct.pack('<ii',20,20)+grid,[(10,10)]))
        with self.assertRaises(ValueError):audit.approaches(struct.pack('<ii',20,20)+grid,[(9,10)])
if __name__=='__main__':unittest.main()
