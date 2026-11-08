import sys
import unittest
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools/ClientPatches"))
from prepare_artisan_journal import rewrite


class ArtisanJournalTests(unittest.TestCase):
    def fixture(self):
        return b"header=keep\r\n" + b"".join(
            ("[%d]\r\nMissionId=%d\r\nLv_max=135\r\nMap=0\r\nBeginNpcId=old\r\nFinishNpcId=old\r\n"
             "Prize=old\r\nIntentionDesp=old\r\nIntentAmount=2\r\nIntention1=old [chk_quest 1723,0,1,1]\r\nIntention2=old\r\nContent=old\r\n" % (i,1733+i)).encode()
            for i in range(5)) + b"[999]\r\nMissionId=99\r\nContent=keep\r\n"

    def test_routes_rewards_and_unchanged_sections(self):
        before=self.fixture();after=rewrite(before)
        self.assertTrue(after.startswith(b"header=keep\r\n"))
        self.assertTrue(after.endswith(before[before.index(b"[999]"):]))
        self.assertEqual(after.count(b"Lv_max=255"),5)
        self.assertEqual(after.count(b"bound P7 soul"),5)
        self.assertIn(b"FinishNpcId=900071727,1002,473,300,TaskmasterChang,TwinCity",after)
        self.assertIn(b"BeginNpcId=4466,1002,459,331,ApprenticeLuo,TwinCity",after)
        self.assertIn(b"FinishNpcId=4467,1002,342,640,ArtisanLuo,TwinCity",after)
        self.assertNotIn(b"[chk_quest",after)
        self.assertNotIn(b"Intention2=",after)
        self.assertEqual(rewrite(after),after)

    def test_missing_or_duplicate_missions_rejected(self):
        before=self.fixture()
        for data in (before.replace(b"MissionId=1737",b"MissionId=9999"),before+before):
            with self.assertRaises(ValueError):rewrite(data)

    def test_missing_or_ambiguous_fields_rejected(self):
        before=self.fixture()
        for data in (before.replace(b"Prize=old",b"Prize=old\r\nPrize=duplicate",1),before.replace(b"Content=old\r\n",b"",1)):
            with self.assertRaises(ValueError):rewrite(data)


if __name__ == "__main__":unittest.main()
