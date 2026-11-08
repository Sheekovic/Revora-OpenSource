import sys
import unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parents[1]/'tools/ClientPatches'))
import prepare_ancient_profiles as profiles

class AncientProfileTests(unittest.TestCase):
    boss=b'[AncientDevil]\r\nTypeID=2407\r\nMaxLife=65535\r\nLevel=120\r\nBoss=0\r\n'
    guard=b'[WarriorDevil]\r\nTypeID=7001\r\nMaxLife=23588\r\nLevel=70\r\nBoss=0\r\n'
    def test_only_intended_fields_change(self):
        source=self.boss+self.guard+b'[Unrelated]\r\nBoss=7\r\n'
        output=profiles.patch(source)
        self.assertEqual(self.guard,profiles.section(output,'WarriorDevil'))
        self.assertTrue(output.startswith(source.replace(self.boss,self.boss.replace(b'Boss=0',b'Boss=1'))))
        self.assertEqual(profiles.section(source,'Unrelated').rstrip(b'\r\n'),profiles.section(output,'Unrelated').rstrip(b'\r\n'))
        self.assertEqual(self.boss.replace(b'Boss=0',b'Boss=1'),profiles.section(output,'AncientDevil'))
        guard=profiles.section(output,'DevilGuard')
        self.assertEqual([7000,6590,100,0],[profiles.field(guard,k) for k in ['TypeID','MaxLife','Level','Boss']])
    def test_missing_duplicate_changed_and_existing_profiles_rejected(self):
        source=self.boss+self.guard
        for bad in [self.boss,self.guard,source+self.boss,source+self.guard,source+b'[DevilGuard]\r\nBoss=0\r\n',source.replace(b'23588',b'123'),source.replace(b'Boss=0',b'Boss=1'),source.replace(b'TypeID=2407',b'TypeID=7006')]:
            with self.assertRaises(ValueError):profiles.patch(bad)
    def test_repeated_patch_refused(self):
        with self.assertRaises(ValueError):profiles.patch(profiles.patch(self.boss+self.guard))
if __name__=='__main__':unittest.main()
