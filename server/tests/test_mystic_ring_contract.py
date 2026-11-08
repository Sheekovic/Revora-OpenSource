import sys
import unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"tools"))
from audit_mystic_ring import analyze,ITEMS,CHANG,MINSTREL,SHEN


class MysticContractTests(unittest.TestCase):
    def fixture(self):
        rows=[]
        goals={1740:"[chk_item 1000000,12] [chk_item 1001000,12]",1741:"[chk_quest 1741,1,1,20]",
               1742:"[npc ShenJunior,4502,1011,920,552] [chk_quest 1742,0,1,1]",1743:"[chk_quest 1743,1,1,1] [chk_quest 1743,1,2,1]"}
        for i,mission in enumerate(range(1738,1745)):
            rows.append("[%d]\nMissionId=%d\nTypeId=4\nLv_min=48\nLv_max=68\nProfession=0\nPrequest=%d\nBeginNpcId=%s\nFinishNpcId=%s\nPrize=[item 728240,0]\nIntention1=%s\n" %
                (i,mission,mission-1 if i else 0,",".join(map(str,[CHANG,MINSTREL,SHEN,SHEN,SHEN,SHEN,MINSTREL][i])),",".join(map(str,[MINSTREL,SHEN,SHEN,SHEN,SHEN,MINSTREL,CHANG][i])),goals.get(mission,"Talk")))
        return "".join(rows),"\n".join(str(k)+"@@"+v for k,v in ITEMS.items())
    def test_evidence_is_not_completion(self):
        q,i=self.fixture();r=analyze(q,i);self.assertEqual(len(r["stages"]),7);self.assertFalse(r["implemented"]);self.assertFalse(r["aggregate_10931_proven"])
    def test_changed_contract_rejected(self):
        q,i=self.fixture()
        for a,b in [("Prequest=1738","Prequest=0"),("1,1,20]","1,1,19]"),("1000000,12]","1000000,11]"),("920,552","924,556"),("Lv_min=48","Lv_min=45"),("[item 728240,","[item 728239,")]:
            with self.assertRaises(ValueError):analyze(q.replace(a,b),i)
    def test_ambiguous_or_missing_data_rejected(self):
        q,i=self.fixture()
        for source in (q+q,q.replace("MissionId=1744","MissionId=999"),q.replace("TypeId=4","TypeId=4\nTypeId=4",1)):
            with self.assertRaises(ValueError):analyze(source,i)
        with self.assertRaises(ValueError):analyze(q,i.replace("CastleGate","Wrong"))


if __name__=="__main__":unittest.main()
