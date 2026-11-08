import sys
import unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"tools"))
from audit_ghost_city import analyze,ITEMS,CHANG,AUNT,WANG,SHINE,MOMO,POINTS

class GhostCityContractTests(unittest.TestCase):
    def fixture(self):
        rows=[]
        goals={1789:" ".join("[mob Fireworks,1002,%d,%d][chk_quest 1789,1,%d,1]"%(x,y,i+1) for i,(x,y) in enumerate(POINTS)),1791:"[chk_item 721712,1]",1792:"[chk_item 721714,1]"}
        for i,mission in enumerate(range(1787,1795)):
            rows.append("[%d]\nMissionId=%d\nTypeId=4\nActivityType=0\nActivityType=0\nLv_min=75\nLv_max=95\nProfession=0\nPrequest=%d\nBeginNpcId=%s\nFinishNpcId=%s\nPrize=[item 728246,0]\nIntention1=%s\n" %
                (i,mission,mission-1 if i else 0,",".join(map(str,[CHANG,AUNT,WANG,WANG,SHINE,SHINE,MOMO,AUNT][i])),",".join(map(str,[AUNT,WANG,WANG,SHINE,SHINE,MOMO,AUNT,CHANG][i])),goals.get(mission,"Talk")))
        return "".join(rows),"\n".join(str(k)+"@@"+v for k,v in ITEMS.items())
    def test_evidence_is_not_completion(self):
        q,i=self.fixture();r=analyze(q,i);self.assertEqual(len(r["stages"]),8);self.assertFalse(r["implemented"]);self.assertFalse(r["aggregate_10931_proven"])
    def test_changed_contract_rejected(self):
        q,i=self.fixture()
        for a,b in [("Prequest=1787","Prequest=0"),("1789,1,3,1]","1789,1,3,2]"),("721712,1]","721712,2]"),("465,356","386,377"),("Lv_min=75","Lv_min=55"),("728246,","728244,")]:
            with self.assertRaises(ValueError):analyze(q.replace(a,b),i)
    def test_only_known_identical_duplicate_is_accepted(self):
        q,i=self.fixture()
        for source in (q+q,q.replace("MissionId=1794","MissionId=999"),q.replace("TypeId=4","TypeId=4\nTypeId=4",1),q.replace("ActivityType=0","ActivityType=1",1),q.replace("ActivityType=0","ActivityType=0\nActivityType=0",1)):
            with self.assertRaises(ValueError):analyze(source,i)
        with self.assertRaises(ValueError):analyze(q,i.replace("MagicFirework","Wrong"))

if __name__=="__main__":unittest.main()
