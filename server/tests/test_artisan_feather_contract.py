import sys
import unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"tools"))
from audit_artisan_feather import analyze,ITEMS,CHANG,APPRENTICE,ARTISAN


class ArtisanContractTests(unittest.TestCase):
    def fixture(self):
        rows=[]
        for i,mission in enumerate(range(1733,1738)):
            rows.append("[%d]\nMissionId=%d\nTypeId=4\nActivityType=0\nActivityType=0\nLv_min=45\nLv_max=65\nProfession=0\nPrequest=%d\nBeginNpcId=%s\nFinishNpcId=%s\nPrize=[item 728239,0]\nIntention1=%s\n" %
                (i,mission,mission-1 if i else 0,",".join(map(str,[CHANG,APPRENTICE,ARTISAN,ARTISAN,ARTISAN][i])),",".join(map(str,[APPRENTICE,ARTISAN,ARTISAN,ARTISAN,CHANG][i])),
                 "[chk_quest 1735,1,1,100]" if i==2 else "[chk_item 729976,1]" if i==3 else "Talk"))
        return "".join(rows),"\n".join(str(k)+"@@"+v for k,v in ITEMS.items())
    def test_contract_is_not_completion(self):
        q,i=self.fixture();r=analyze(q,i)
        self.assertEqual(len(r["stages"]),5);self.assertEqual(r["minimum_level"],45)
        self.assertFalse(r["implemented"]);self.assertFalse(r["aggregate_10931_proven"])
    def test_changed_contract_rejected(self):
        q,i=self.fixture()
        for a,b in [("Prequest=1733","Prequest=0"),("1,1,100]","1,1,99]"),("729976,1]","729976,2]"),("342,640","603,672"),("Lv_min=45","Lv_min=30"),("[item 728239,","[item 728237,")]:
            with self.assertRaises(ValueError):analyze(q.replace(a,b),i)
    def test_ambiguous_or_missing_data_rejected(self):
        q,i=self.fixture()
        for source in (q+q,q.replace("MissionId=1737","MissionId=999"),q.replace("TypeId=4","TypeId=4\nTypeId=4",1),q.replace("ActivityType=0","ActivityType=1",1)):
            with self.assertRaises(ValueError):analyze(source,i)
        with self.assertRaises(ValueError):analyze(q,i.replace("VultureFeather","Wrong"))


if __name__=="__main__":unittest.main()
