import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools/ClientPatches'))
import prepare_trojan_journal as patch


def fixture():
    result = b'[99]\r\nMissionId=99\r\nContent=Unrelated\x81\xff\r\n'
    for index, (mission, (_, _, intention, _)) in enumerate(patch.ROWS.items(), 100):
        tokens = b''.join(patch.re.findall(rb'\[chk_[^\]]+\]', intention.encode()))
        result += (f'[{index}]\r\nMissionId={mission}\r\nLv_min=120\r\nMap=0\r\nAuto=0\r\n'
                   'BeginNpcId=old\r\nFinishNpcId=old\r\nIntentionDesp=old\r\nIntention1=old').encode()
        result += tokens + b'\r\nContent=old\r\nPrize=old\r\n'
    return result


class TrojanJournalPatchTests(unittest.TestCase):
    def test_preservation_and_idempotence(self):
        before = fixture(); after = patch.rewrite(before)
        self.assertEqual(patch.rewrite(after), after)
        self.assertTrue(after.startswith(before.split(b'[100]')[0]))
        self.assertEqual(after.count(b'Lv_min=120\r\nMap=0\r\nAuto=0'), 12)
        self.assertEqual(patch.re.findall(rb'\[chk_[^\]]+\]', before), patch.re.findall(rb'\[chk_[^\]]+\]', after))
        self.assertIn(b'[mob FuriousFlameDevastator,3825,45,40]', after)
        self.assertEqual(after.count(patch.PRIZE.encode()), 12)

    def test_missing_mission(self):
        with self.assertRaises(ValueError): patch.rewrite(fixture().replace(b'MissionId=3269', b'MissionId=1'))

    def test_duplicate_mission(self):
        with self.assertRaises(ValueError): patch.rewrite(fixture().replace(b'MissionId=3278', b'MissionId=3269'))

    def test_missing_field(self):
        with self.assertRaises(ValueError): patch.rewrite(fixture().replace(b'Prize=old\r\n', b'', 1))

    def test_duplicate_field(self):
        with self.assertRaises(ValueError): patch.rewrite(fixture().replace(b'Prize=old', b'Prize=old\r\nPrize=second', 1))

    def test_objective_drift(self):
        with self.assertRaises(ValueError): patch.rewrite(fixture().replace(b'[chk_item 3003340,1]', b'[chk_item 3003340,2]'))

    def test_ambiguous_identity(self):
        with self.assertRaises(ValueError): patch.rewrite(fixture().replace(b'MissionId=3269', b'MissionId=3269\r\nMissionId=2'))


if __name__ == '__main__':
    unittest.main()
