import struct
import sys
from pathlib import Path
import unittest
sys.path.insert(0,str(Path(__file__).parents[1]/"tools"))
import audit_ancient_devil as audit


def index(rows):
    result=struct.pack("<I",len(rows))
    for identity,path in rows:
        value=path.encode("ascii");result+=struct.pack("<II",identity,len(value))+value+struct.pack("<I",256)
    return result

class AncientAuditTests(unittest.TestCase):
    def test_collection_achievement_identity_and_score(self):
        row=b"60303 11020 fixture description 10 trigger null\n"
        self.assertEqual({"ui_identity":60303,"achievement_id":11020,"score":10},audit.collection_achievement(row))
        for changed in (b"",row+row,row.replace(b"10 trigger",b"9 trigger"),row.replace(b"60303",b"60304"),row.replace(b" null",b"")):
            with self.assertRaises(ValueError):audit.collection_achievement(changed)
    profile=b"[AncientDevil]\nMaxLife=65535\nLevel=120\nTypeID=2407\nBoss=0\nZoomPercent=150\n"
    def test_reviewed_devil_profile(self):
        self.assertEqual({"MaxLife":65535,"Level":120,"TypeID":2407,"Boss":0,"ZoomPercent":150},audit.devil_profile(self.profile+b"[Other]\nBoss=1\n"))
        self.assertEqual(65535,audit.devil_profile(self.profile.replace(b"\n",b"\r\n"))["MaxLife"])
    def test_missing_or_duplicate_devil_profile(self):
        for data in (b"",b"[AncientDemon]\nBoss=0\n",self.profile+self.profile):
            with self.assertRaises(ValueError):audit.devil_profile(data)
    def test_changed_or_ambiguous_devil_fields(self):
        for data in (self.profile.replace(b"Boss=0",b"Boss=1"),self.profile+b"Boss=0\n",self.profile.replace(b"Level=120\n",b""),self.profile.replace(b"TypeID=2407",b"TypeID=7006")):
            with self.assertRaises(ValueError):audit.devil_profile(data)
    def test_target_index_and_unrelated_duplicates(self):
        data=index([(5,"map/a"),(1082,"map/ancient"),(5,"map/b")])
        self.assertEqual(("map/ancient",256),audit.index_entry(data,1082))
    def test_duplicate_or_missing_target_rejected(self):
        for rows in [[],[(5,"map/a")],[(1082,"map/a"),(1082,"map/a")]]:
            with self.assertRaises(ValueError):audit.index_entry(index(rows),1082)
    def test_truncated_and_trailing_index(self):
        data=index([(1082,"map/ancient")])
        for length in range(len(data)):
            with self.assertRaises(ValueError):audit.index_entry(data[:length],1082)
        with self.assertRaises(ValueError):audit.index_entry(data+b"x",1082)
        with self.assertRaises(ValueError):audit.index_entry(struct.pack("<I",100000),1082)
    def test_approaches_respect_actor_reservations(self):
        result=audit.topology(struct.pack("<ii",24,24)+bytes(576),((4,4),(19,19)),(12,12))
        self.assertEqual(3,len(result["approaches"]))
        for row in result["approaches"]:
            distance=max(abs(row["actor"][i]-row["approach"][i]) for i in (0,1))
            self.assertGreater(distance,1);self.assertLessEqual(distance,6)
    def test_disconnected_hunt_area_rejected(self):
        grid=bytearray(576)
        for y in range(24):grid[12+y*24]=1
        with self.assertRaises(ValueError):audit.topology(struct.pack("<ii",24,24)+grid,((4,4),(20,20)),(5,10))
    def test_invalid_terrain_and_actor_bounds(self):
        for data in [b"",struct.pack("<ii",-1,24),struct.pack("<ii",24,24)+bytes(575),struct.pack("<ii",24,24)+bytes([2])*576]:
            with self.assertRaises(ValueError):audit.topology(data,((4,4),),(12,12))
        with self.assertRaises(ValueError):audit.topology(struct.pack("<ii",24,24)+bytes(576),((25,4),),(12,12))

if __name__=="__main__":unittest.main()
