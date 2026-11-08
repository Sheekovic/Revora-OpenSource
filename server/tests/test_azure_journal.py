import sys
import unittest
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools/ClientPatches"))
from prepare_azure_journal import rewrite


class AzureJournalTests(unittest.TestCase):
    def fixture(self):
        return b"header=unchanged\r\n" + b"".join(
            ("[%d]\r\nMissionId=%d\r\nLv_max=60\r\nBeginNpcId=8796,1002,473,300,TaskmasterChang,JobCenter\r\n"
             "FinishNpcId=4482,1011,214,231,Wu,PhoenixCastle\r\nPrize=old\r\nContent=old\r\n"
             "Intention1=[npc Bian,4484,1002,425,331] [chk_item 721696,3]\r\n" % (i,1727+i)).encode()
            for i in range(6)) + b"[999]\r\nMissionId=99\r\nBeginNpcId=4484,5001,78,66\r\n"

    def test_aliases_rewards_and_unrelated_bytes(self):
        before=self.fixture();after=rewrite(before)
        self.assertTrue(after.endswith(before[before.index(b"[999]"):]))
        self.assertTrue(after.startswith(b"header=unchanged\r\n"))
        self.assertEqual(after.count(b"Lv_max=255"),6)
        self.assertEqual(after.count(b"[npc Bian,900071730,"),6)
        self.assertEqual(after.count(b"[chk_item 721696,3]"),6)
        self.assertEqual(after.count(b"bound P7 soul"),6)
        self.assertEqual(rewrite(after),after)

    def test_missing_and_duplicate_missions_rejected(self):
        before=self.fixture()
        for data in (before.replace(b"MissionId=1732",b"MissionId=9000"),before+before):
            with self.assertRaises(ValueError):rewrite(data)

    def test_ambiguous_fields_rejected(self):
        before=self.fixture()
        for data in (before.replace(b"Prize=old",b"Prize=old\r\nPrize=duplicate",1),
                     before.replace(b"Lv_max=60\r\n",b"",1),
                     before.replace(b"MissionId=1727",b"MissionId=1727\r\nMissionId=99",1)):
            with self.assertRaises(ValueError):rewrite(data)


if __name__ == "__main__":
    unittest.main()
