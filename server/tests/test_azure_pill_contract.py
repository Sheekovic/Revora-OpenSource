import sys
import unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"tools"))
from audit_azure_pill import analyze, STAGES, ROUTES, ITEMS

class AzureContractTests(unittest.TestCase):
    def fixture(self):
        rows=[]
        for index,key in enumerate(STAGES):
            texts={1729:"[chk_item 721695,1] [chk_item 721696,3] [chk_item 721697,2]",1730:"2 minutes",1731:"[chk_item 721698,1]"}
            rows.append(f"[{index}]\nMissionId={key}\nTypeId=4\nLv_min=40\nLv_max=60\nProfession=0\nPrequest={0 if index==0 else key-1}\nBeginNpcId={ROUTES[index][0]},1002,1,2\nFinishNpcId={ROUTES[index][1]},1011,3,4\nPrize=[item 728238,0]\nIntentionDesp={texts.get(key,'')}\n")
        return "".join(rows),"\n".join(f"{key}@@{name}@@0" for key,name in ITEMS.items())
    def test_complete_chain_reports_collision_without_completion_claim(self):
        q,i=self.fixture();r=analyze(q,i,"case 4482: case 4484:")
        self.assertEqual(r["server_dialogue_id_collisions"],[4482,4484]);self.assertEqual(len(r["stages"]),6)
        self.assertFalse(r["aggregate_10931_proven"]);self.assertFalse(r["implemented"])
    def test_rejects_changed_chain_contract(self):
        q,i=self.fixture()
        for before,after in [("Prequest=1727","Prequest=9999"),("721696,3","721696,2"),("2 minutes","1 minute"),("Lv_max=60","Lv_max=200"),("item 728238,","item 1,")]:
            with self.subTest(before=before), self.assertRaises(ValueError):analyze(q.replace(before,after),i,"")
    def test_rejects_duplicate_and_missing_missions(self):
        q,i=self.fixture()
        for changed in [q+q,q[:q.rfind("[5]")]]:
            with self.assertRaises(ValueError):analyze(changed,i,"")
    def test_rejects_changed_material_identity(self):
        q,i=self.fixture()
        with self.assertRaises(ValueError):analyze(q,i.replace("RatlingEye","WrongEye"),"")
if __name__=="__main__":unittest.main()
