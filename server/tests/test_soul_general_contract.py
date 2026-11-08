import sys
import unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"tools"))
from audit_soul_general import analyze,ITEMS,CHANG,AMBER,ZHAO,GUARD


class SoulGeneralContractTests(unittest.TestCase):
    def fixture(self):
        rows=[]
        goals={1752:"[chk_item 721703,1]",1753:"[chk_quest 1753,1,1,1] [chk_quest 1753,1,2,10] [chk_quest 1753,1,3,10]",1754:"[chk_item 721705,1]",1755:"[chk_quest 1755,0,1,1]",1756:"[chk_item 721706,1]"}
        for i,mission in enumerate(range(1751,1758)):
            rows.append("[%d]\nMissionId=%d\nTypeId=4\nLv_min=55\nLv_max=75\nProfession=0\nPrequest=%d\nBeginNpcId=%s\nFinishNpcId=%s\nPrize=[item 728244,0]\nIntention1=%s\n" %
                (i,mission,mission-1 if i else 0,",".join(map(str,[CHANG,AMBER,AMBER,AMBER,AMBER,GUARD,ZHAO][i])),",".join(map(str,[AMBER,AMBER,AMBER,AMBER,GUARD,ZHAO,CHANG][i])),goals.get(mission,"Talk")))
        return "".join(rows),"\n".join(str(k)+"@@"+v for k,v in ITEMS.items())
    def test_evidence_is_not_completion(self):
        q,i=self.fixture();r=analyze(q,i);self.assertEqual(len(r["stages"]),7);self.assertFalse(r["implemented"]);self.assertFalse(r["aggregate_10931_proven"])
    def test_changed_contract_rejected(self):
        q,i=self.fixture()
        for a,b in [("Prequest=1751","Prequest=0"),("1,2,10]","1,2,9]"),("721703,1]","721703,2]"),("865,540","879,562"),("Lv_min=55","Lv_min=45"),("[item 728244,","[item 728239,")]:
            with self.assertRaises(ValueError):analyze(q.replace(a,b),i)
    def test_ambiguous_or_missing_data_rejected(self):
        q,i=self.fixture()
        for source in (q+q,q.replace("MissionId=1757","MissionId=999"),q.replace("TypeId=4","TypeId=4\nTypeId=4",1)):
            with self.assertRaises(ValueError):analyze(source,i)
        with self.assertRaises(ValueError):analyze(q,i.replace("SilkPack","Wrong"))


if __name__=="__main__":unittest.main()
