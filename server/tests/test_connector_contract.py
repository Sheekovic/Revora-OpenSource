import sys
import unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"tools"))
from audit_connector import analyze,ITEMS,CHANG,WONG,CHEN


class ConnectorTests(unittest.TestCase):
    def fixture(self):
        rows=[]
        for i,mission in enumerate(range(1718,1723)):
            rows.append("[%d]\nMissionId=%d\nTypeId=4\nLv_min=22\nLv_max=42\nProfession=0\nPrequest=%d\nBeginNpcId=%s\nFinishNpcId=%s\nPrize=[item 728236,0]\nIntention1=%s\n" %
                        (i,mission,mission-1 if i else 0,",".join(map(str,[CHANG,WONG,CHEN,CHEN,WONG][i])),",".join(map(str,[WONG,CHEN,CHEN,WONG,CHANG][i])),
                         "[chk_quest 1720,1,1,5]" if i==2 else "[chk_item 729942,1]" if i==3 else "Talk"))
        return "".join(rows),"\n".join(str(k)+"@@"+v for k,v in ITEMS.items())
    def test_contract_is_evidence_not_completion(self):
        q,i=self.fixture();result=analyze(q,i)
        self.assertEqual(len(result["stages"]),5);self.assertFalse(result["implemented"]);self.assertFalse(result["aggregate_10931_proven"])
    def test_changed_contract_rejected(self):
        q,i=self.fixture()
        for a,b in [("Prequest=1718","Prequest=0"),("1,1,5]","1,1,4]"),("729942,1]","729942,2]"),("440,339","474,332"),("Lv_min=22","Lv_min=20"),("[item 728236,","[item 728238,")]:
            with self.assertRaises(ValueError):analyze(q.replace(a,b),i)
    def test_missing_ambiguous_or_wrong_items_rejected(self):
        q,i=self.fixture()
        for source in (q+q,q.replace("MissionId=1722","MissionId=999"),q.replace("TypeId=4","TypeId=4\nTypeId=4",1)):
            with self.assertRaises(ValueError):analyze(source,i)
        with self.assertRaises(ValueError):analyze(q,i.replace("ConnectorLetter","Wrong"))


if __name__=="__main__":unittest.main()
