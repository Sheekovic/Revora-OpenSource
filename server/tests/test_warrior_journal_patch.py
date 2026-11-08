"""Synthetic journal transformation checks; contains no proprietary catalog."""
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools/ClientPatches'))
from prepare_warrior_journal import ROWS, rewrite


def fixture(newline=b'\r\n'):
    content = b'Preamble=keep\xff\r\n[1]\r\nMissionId=99\r\nContent=Unrelated,1002,132,550\r\n'
    for number, mission in enumerate(ROWS, 10):
        lines = ['[%d]' % number, 'MissionId=%d' % mission, 'Map=1002',
                 'BeginNpcId=old', 'FinishNpcId=old', 'IntentionDesp=old',
                 'Intention1=old', 'Content=old', 'Prize=624078', 'Lv_min=40',
                 'Prequest=%d' % (list(ROWS)[number - 11] if number > 10 else 0)]
        content += newline.join(line.encode() for line in lines) + newline
    return content


class WarriorJournalPatchTests(unittest.TestCase):
    def test_preserves_unrelated_bytes_and_rewards(self):
        before = fixture()
        after = rewrite(before)
        self.assertEqual(before[:before.index(b'[10]')], after[:after.index(b'[10]')])
        self.assertEqual(after.count(b'Prize=624078'), 7)
        self.assertEqual(after.count(b'Lv_min=40'), 7)
        self.assertEqual(after.count(b'[chk_quest '), 7)
        self.assertEqual(rewrite(after), after)

    def test_valuable_rewards_replace_all_seven_without_inventing_link_fields(self):
        for newline in (b'\r\n', b'\n'):
            before = fixture(newline)
            after = rewrite(before, valuable=True)
            self.assertEqual(after.count(b'Prize=Complete the trial to earn two unbound level-140 EvilSlayer fists:'), 7)
            self.assertEqual(after.count(b'+12, -7 damage reduction, two empty sockets.'), 7)
            self.assertEqual(after.count(b'Earlier accepted trials retain their promised reward.'), 7)
            self.assertNotIn(b'[item ', after)
            self.assertNotIn(b'624078', after)
            self.assertEqual(after.count(b'Lv_min=40' + newline), 7)
            self.assertEqual(rewrite(after, valuable=True), after)
            self.assertEqual(before[:before.index(b'[10]')], after[:after.index(b'[10]')])

    def test_reward_field_missing_or_duplicated_is_rejected(self):
        for replacement in (b'Other=624078', b'Prize=624078\r\nPrize=again'):
            with self.assertRaises(ValueError):
                rewrite(fixture().replace(b'Prize=624078', replacement, 1), valuable=True)

    def test_coordinates_and_name_match_server_route(self):
        after = rewrite(fixture())
        for value in (b'10001,1004,42,74', b'19253,10009,42,40',
                      b'SealingStele,10010,83,86', b'19254,10011,41,39',
                      b'TinyMindofEvil,10011,49,47', b'19259,10011,39,44'):
            self.assertIn(value, after)

    def test_line_endings_preserved(self):
        for newline in (b'\r\n', b'\n'):
            after = rewrite(fixture(newline))
            self.assertIn(b'Map=1004' + newline, after)

    def test_missing_mission_rejected(self):
        with self.assertRaises(ValueError):
            rewrite(fixture().replace(b'MissionId=3678', b'MissionId=1'))

    def test_duplicate_mission_rejected(self):
        with self.assertRaises(ValueError):
            rewrite(fixture() + b'[99]\r\nMissionId=3678\r\n')

    def test_ambiguous_identity_rejected(self):
        with self.assertRaises(ValueError):
            rewrite(fixture() + b'[99]\r\nMissionId=3678\r\nMissionId=99\r\n')

    def test_missing_field_rejected(self):
        with self.assertRaises(ValueError):
            rewrite(fixture().replace(b'Intention1=old', b'Different=old', 1))

    def test_duplicate_field_rejected(self):
        with self.assertRaises(ValueError):
            rewrite(fixture().replace(b'Content=old', b'Content=old\r\nContent=again', 1))


if __name__ == '__main__':
    unittest.main()
