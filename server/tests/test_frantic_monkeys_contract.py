import sys
import unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"tools"))
from audit_frantic_monkeys import analyze,ITEMS,CHANG,BAO,LI


class FranticContractTests(unittest.TestCase):
    def fixture(self):
        rows=[]
        goals={1746:"[chk_item 721699,1]",1748:"[chk_quest 1748,1,1,5]",1749:"[chk_item 721702,1]"}
        for i,mission in enumerate(range(1745,1751)):
            rows.append("[%d]\nMissionId=%d\nTypeId=4\nLv_min=50\nLv_max=70\nProfession=0\nPrequest=%d\nBeginNpcId=%s\nFinishNpcId=%s\nPrize=[item 728243,0]\nIntention1=%s\n" %
                (i,mission,mission-1 if i else 0,",".join(map(str,[CHANG,BAO,BAO,LI,LI,BAO][i])),",".join(map(str,[BAO,BAO,LI,LI,BAO,CHANG][i])),goals.get(mission,"Talk")))
        return "".join(rows),"\n".join(str(k)+"@@"+v for k,v in ITEMS.items())
    def test_evidence_is_not_completion(self):
        q,i=self.fixture();r=analyze(q,i);self.assertEqual(len(r["stages"]),6);self.assertFalse(r["implemented"]);self.assertFalse(r["aggregate_10931_proven"])
    def test_changed_contract_rejected(self):
        q,i=self.fixture()
        for a,b in [("Prequest=1745","Prequest=0"),("1,1,5]","1,1,4]"),("721699,1]","721699,2]"),("440,343","424,343"),("Lv_min=50","Lv_min=45"),("[item 728243,","[item 728239,")]:
            with self.assertRaises(ValueError):analyze(q.replace(a,b),i)
    def test_ambiguous_or_missing_data_rejected(self):
        q,i=self.fixture()
        for source in (q+q,q.replace("MissionId=1750","MissionId=999"),q.replace("TypeId=4","TypeId=4\nTypeId=4",1)):
            with self.assertRaises(ValueError):analyze(source,i)
        with self.assertRaises(ValueError):analyze(q,i.replace("Poison","Wrong"))


if __name__=="__main__":unittest.main()
