import sys
import re
import unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"tools"))
import audit_attack_talisman as audit
class FanContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        root=Path(__file__).resolve().parents[3]/"6711"
        cls.quest=audit.checked(root/"ini/Questinfo.ini",audit.QUEST_SHA256).decode("cp1252")
        cls.items=audit.checked(root/"ini/itemtype.txt",audit.ITEM_SHA).decode("latin1")
    def test_verified_contract(self):
        r=audit.analyze(self.quest,self.items);self.assertEqual(len(r["stages"]),10);self.assertEqual(len(r["items"]),16);self.assertEqual(r["prerequisite_conflict"]["revora_required_tower_stage"],10)
    def test_missing_stage(self):
        with self.assertRaises(ValueError):audit.analyze(self.quest.replace("MissionId=400","MissionId=399",1),self.items)
    def test_duplicate_stage(self):
        with self.assertRaises(ValueError):audit.analyze(self.quest.replace("MissionId=401","MissionId=400",1),self.items)
    def test_changed_prerequisite(self):
        block=next(b for b in re.split(r"(?m)^\[",self.quest) if re.search(r"(?m)^MissionId=400$",b))
        with self.assertRaises(ValueError):audit.analyze(self.quest.replace(block,block.replace("Prequest=527","Prequest=528",1),1),self.items)
    def test_wrong_equipment_reward(self):
        with self.assertRaises(ValueError):audit.analyze(self.quest.replace("[item 201009,","[item 710221,",1),self.items)
    def test_changed_item_identity(self):
        with self.assertRaises(ValueError):audit.analyze(self.quest,self.items.replace("721806@@BeeKettle","721806@@Other",1))
    def test_missing_item(self):
        with self.assertRaises(ValueError):audit.analyze(self.quest,self.items.replace("721806@@BeeKettle","999999@@BeeKettle",1))
if __name__=="__main__":unittest.main()
