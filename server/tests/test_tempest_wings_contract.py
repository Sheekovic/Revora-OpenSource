"""Reject ambiguous or changed Tempest quest inputs before implementation."""
import sys
import unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"tools"))
import audit_tempest_wings as audit

class TempestContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        client=Path(__file__).resolve().parents[3]/"6711"
        cls.quest=audit.checked(client/"ini/Questinfo.ini",audit.QUEST_SHA256).decode("cp1252")
        cls.items=audit.checked(client/"ini/itemtype.txt",audit.ITEM_SHA).decode("latin1")
    def test_original(self):
        r=audit.analyze(self.quest,self.items);self.assertEqual(r["mission"],200)
        self.assertFalse(r["checked_quest_present"]);self.assertFalse(r["implemented"])
        self.assertEqual(r["official_entry"]["count"],5)
    def test_missing(self):
        with self.assertRaises(ValueError):audit.analyze(self.quest.replace("MissionId=200\n","MissionId=199\n",1),self.items)
    def test_duplicate_identity(self):
        with self.assertRaises(ValueError):audit.analyze(self.quest.replace("MissionId=200\n","MissionId=200\nMissionId=200\n",1),self.items)
    def test_changed_reward(self):
        with self.assertRaises(ValueError):audit.analyze(self.quest.replace("[item 204008,","[item 204009,"),self.items)
    def test_changed_objective(self):
        with self.assertRaises(ValueError):audit.analyze(self.quest.replace("[chk_quest 3429,1,1,1]","[chk_quest 3428,1,1,1]"),self.items)
    def test_missing_item(self):
        with self.assertRaises(ValueError):audit.analyze(self.quest,"\n".join(l for l in self.items.splitlines() if not l.startswith("3006392@@")))
    def test_changed_item(self):
        with self.assertRaises(ValueError):audit.analyze(self.quest,self.items.replace("204008@@TempestWing@@","204008@@OtherWing@@",1))

if __name__=="__main__":unittest.main()
