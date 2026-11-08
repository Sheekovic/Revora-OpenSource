import sys
import unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"tools"))
from audit_mad_man import analyze,ITEMS,CHANG,DU,JUDD,DONG

class MadManContractTests(unittest.TestCase):
    def fixture(self):
        rows=[]
        goals={1783:"[chk_item 729978,1]",1785:" ".join("[chk_quest 1785,1,%d,1]"%i for i in range(1,8))+" [chk_item 729982,1]"}
        for i,mission in enumerate(range(1781,1787)):
            rows.append("[%d]\nMissionId=%d\nTypeId=4\nActivityType=0\nActivityType=0\nLv_min=60\nLv_max=80\nProfession=0\nPrequest=%d\nBeginNpcId=%s\nFinishNpcId=%s\nPrize=[item 728245,0]\nIntention1=%s\n" %
                (i,mission,mission-1 if i else 0,",".join(map(str,[CHANG,DU,JUDD,JUDD,DONG,DU][i])),",".join(map(str,[DU,JUDD,JUDD,DONG,DU,CHANG][i])),goals.get(mission,"Talk")))
        return "".join(rows),"\n".join(str(k)+"@@"+v for k,v in ITEMS.items())
    def test_evidence_is_not_completion(self):
        q,i=self.fixture();r=analyze(q,i);self.assertEqual(len(r["stages"]),6);self.assertFalse(r["implemented"]);self.assertFalse(r["aggregate_10931_proven"])
    def test_changed_contract_rejected(self):
        q,i=self.fixture()
        for a,b in [("Prequest=1781","Prequest=0"),("1785,1,7,1]","1785,1,7,2]"),("729978,1]","729978,2]"),("431,331","386,377"),("Lv_min=60","Lv_min=55"),("728245,","728244,")]:
            with self.assertRaises(ValueError):analyze(q.replace(a,b),i)
    def test_only_known_identical_duplicate_is_accepted(self):
        q,i=self.fixture()
        for source in (q+q,q.replace("MissionId=1786","MissionId=999"),q.replace("TypeId=4","TypeId=4\nTypeId=4",1),q.replace("ActivityType=0","ActivityType=1",1),q.replace("ActivityType=0","ActivityType=0\nActivityType=0",1)):
            with self.assertRaises(ValueError):analyze(source,i)
        with self.assertRaises(ValueError):analyze(q,i.replace("GreenToxin","Wrong"))

if __name__=="__main__":unittest.main()
