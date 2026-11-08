import sys
import unittest
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools/ClientPatches"))
from prepare_power_journal import rewrite


class PowerJournalTests(unittest.TestCase):
    def fixture(self):
        return b"header=keep\r\n" + b"".join(
            ("[%d]\r\nMissionId=%d\r\nLv_max=135\r\nMap=0\r\nBeginNpcId=old\r\nFinishNpcId=old\r\n"
             "Prize=old\r\nIntentionDesp=old\r\nIntentAmount=2\r\nIntention1=old [chk_quest 1840,0,1,1]\r\nIntention2=old\r\nContent=old\r\n" % (i,1840+i)).encode()
            for i in range(13)) + b"[999]\r\nMissionId=99\r\nContent=keep\r\n"

    def test_routes_rewards_and_unchanged_sections(self):
        before=self.fixture();after=rewrite(before)
        self.assertTrue(after.startswith(b"header=keep\r\n"))
        self.assertTrue(after.endswith(before[before.index(b"[999]"):]))
        self.assertEqual(after.count(b"Lv_max=255"),13)
        self.assertEqual(after.count(b"bound P7 soul"),13)
        self.assertIn(b"FinishNpcId=900071727,1002,473,300,TaskmasterChang,TwinCity",after)
        self.assertNotIn(b"[chk_quest",after)
        self.assertNotIn(b"Intention2=",after)
        self.assertEqual(rewrite(after),after)

    def test_known_installed_hand_in_typo(self):
        before=self.fixture();start=before.index(b"MissionId=1851");end=before.index(b"[12]",start)
        broken=before[start:end].replace(b"Intention1=old [chk_quest 1840,0,1,1]",b"Intention1Talk to [npc OldBeggar,4704,1002,466,331].")
        after=rewrite(before[:start]+broken+before[end:])
        self.assertNotIn(b"Intention1Talk",after)
        self.assertEqual(after.count(b"Intention1="),13)

    def test_missing_or_duplicate_missions_rejected(self):
        before=self.fixture()
        for data in (before.replace(b"MissionId=1852",b"MissionId=9999"),before+before):
            with self.assertRaises(ValueError):rewrite(data)

    def test_missing_or_ambiguous_fields_rejected(self):
        before=self.fixture()
        for data in (before.replace(b"Prize=old",b"Prize=old\r\nPrize=duplicate",1),before.replace(b"Content=old\r\n",b"",1)):
            with self.assertRaises(ValueError):rewrite(data)


if __name__ == "__main__":unittest.main()
