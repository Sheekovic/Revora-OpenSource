import struct
import sys
import unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'tools'))
import stage_tempest_scene as scene


class SceneTests(unittest.TestCase):
    def floor(self):
        return bytearray(struct.pack('<II',224,224)+bytes(224*224))

    def test_reserved_npc_still_allows_combat_and_exit(self):
        result=scene.routes(self.floor())
        self.assertEqual([t['walk_steps'] for t in result['targets']],[13,2])

    def test_blocked_npc_footprint(self):
        floor=self.floor();floor[8+110*224+144]=1
        with self.assertRaisesRegex(ValueError,'footprint'):scene.routes(floor)

    def test_isolated_combat_rejected(self):
        floor=self.floor();x,y=scene.ROC
        for px,py in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]:floor[8+py*224+px]=1
        with self.assertRaisesRegex(ValueError,'Unreachable'):scene.routes(floor)

    def test_malformed_floor_rejected(self):
        for floor in [self.floor()[:-1],self.floor()+b'\0',bytearray(50184)]:
            with self.assertRaises(ValueError):scene.routes(floor)
        floor=self.floor();floor[8]=2
        with self.assertRaises(ValueError):scene.routes(floor)

    def test_unreviewed_native_rejected(self):
        with self.assertRaisesRegex(ValueError,'Unreviewed'):scene.convert(bytes(400))


if __name__=='__main__':unittest.main()
