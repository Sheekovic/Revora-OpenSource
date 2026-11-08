import sys
import unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"tools"))
from audit_spun_gold import analyze,ITEMS,CHANG,HONG


class SpunGoldTests(unittest.TestCase):
    def fixture(self):
        rows=[]
        for i,mission in enumerate(range(1723,1727)):
            rows.append("[%d]\nMissionId=%d\nTypeId=4\nLv_min=30\nLv_max=50\nProfession=0\nPrequest=%d\nBeginNpcId=%s\nFinishNpcId=%s\nPrize=[item 728237,0]\nIntention1=%s\n" %
                        (i,mission,mission-1 if i else 0,",".join(map(str,[CHANG,HONG,HONG,HONG][i])),",".join(map(str,[HONG,HONG,HONG,CHANG][i])),
                         "[chk_item 729971,1]" if i==1 else "[chk_quest 1725,0,1,1] [chk_quest 1725,1,2,1] [chk_item 729974,1]" if i==2 else "Talk"))
        return "".join(rows),"\n".join(str(k)+"@@"+v for k,v in ITEMS.items())
    def test_contract_not_completion(self):
        q,i=self.fixture();r=analyze(q,i);self.assertEqual(len(r["stages"]),4);self.assertFalse(r["implemented"]);self.assertFalse(r["aggregate_10931_proven"])
    def test_changed_stages_rejected(self):
        q,i=self.fixture()
        for a,b in [("Prequest=1723","Prequest=0"),("1,2,1]","1,2,2]"),("729971,1]","729971,2]"),("194,260","194,261"),("Lv_min=30","Lv_min=20"),("[item 728237,","[item 728238,")]:
            with self.assertRaises(ValueError):analyze(q.replace(a,b),i)
    def test_ambiguous_or_missing_data_rejected(self):
        q,i=self.fixture()
        for source in (q+q,q.replace("MissionId=1726","MissionId=999"),q.replace("TypeId=4","TypeId=4\nTypeId=4",1)):
            with self.assertRaises(ValueError):analyze(source,i)
        with self.assertRaises(ValueError):analyze(q,i.replace("Offering","Wrong"))


if __name__=="__main__":unittest.main()
