"""Synthetic byte-preservation and rejection tests; no proprietary contents."""
from pathlib import Path
import sys
import unittest
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools/ClientPatches'))
from prepare_starter_journal import FIELDS, rewrite


def fixture(newline=b'\r\n'):
    result = b'Preamble=\xff\r\n[1]\r\nMissionId=99\r\nPrize=Keep\r\n'
    for number, (mission, fields) in enumerate(FIELDS.items(), 2):
        lines = ['[%d]' % number, 'MissionId=%d' % mission, 'Unknown=keep', 'IntentAmount=3']
        lines += [key + '=old' for key in fields]
        lines += ['Intention1=keep', 'Intention3=keep']
        result += newline.join(line.encode() for line in lines) + newline
    return result


class StarterJournalTests(unittest.TestCase):
    def test_preservation_and_idempotence(self):
        for newline in (b'\n', b'\r\n'):
            before = fixture(newline)
            after = rewrite(before)
            self.assertEqual(after[:after.index(b'[2]')], before[:before.index(b'[2]')])
            self.assertEqual(after.count(b'Unknown=keep' + newline), 3)
            self.assertEqual(after.count(b'Intention1=keep' + newline), 3)
            self.assertEqual(after.count(b'Intention3=keep' + newline), 3)
            self.assertEqual(after.count(b'IntentAmount=3' + newline), 3)
            self.assertEqual(rewrite(after), after)

    def test_route_and_rewards(self):
        after = rewrite(fixture())
        self.assertIn(b'[npc Cerys,900000017,1002,319,303]', after)
        self.assertIn(b'[chk_quest 90002,1,2,20]', after)
        self.assertIn(b'Phoenix Castle', after)
        self.assertNotIn(b'Bird Island', after)
        self.assertNotIn(b'[mob ', after)
        self.assertEqual(after.lower().count(b'bound p7 soul'), 3)

    def test_missing_mission(self):
        with self.assertRaises(ValueError):
            rewrite(fixture().replace(b'MissionId=90003', b'MissionId=7'))

    def test_duplicate_mission(self):
        with self.assertRaises(ValueError):
            rewrite(fixture() + b'[8]\nMissionId=90002\n')

    def test_ambiguous_identity(self):
        for extra in (b'99', b'bad', b''):
            with self.subTest(extra=extra), self.assertRaises(ValueError):
                rewrite(fixture().replace(b'MissionId=90002', b'MissionId=90002\nMissionId=' + extra))

    def test_missing_and_duplicate_selected_fields(self):
        for key in ('Prize', 'Intention2', 'Content'):
            for replacement in (b'Other=old', key.encode() + b'=old\r\n' + key.encode() + b'=again'):
                with self.subTest(key=key, replacement=replacement), self.assertRaises(ValueError):
                    rewrite(fixture().replace(key.encode() + b'=old', replacement, 1))

    def test_server_departure_and_quota_match(self):
        source = (Path(__file__).resolve().parents[1] / 'Game/Features/CourierLoopSystem.cs').read_text(encoding='utf-8-sig')
        config = (Path(__file__).resolve().parents[1] / 'System/Config.cs').read_text(encoding='utf-8-sig')
        for name, value in (('NpcId', 900000017), ('TwinCityMap', 1002), ('ClaimX', 319), ('ClaimY', 303), ('PhoenixMap', 1011)):
            self.assertRegex(source, r'\b' + name + r'\s*=\s*' + str(value) + r';')
        for name, value in (('CourierLoopBanditKills', 20), ('CourierLoopCp', 8000),
                            ('CourierLoopGold', 80000), ('CourierLoopRevoraPoints', 200),
                            ('CourierLoopPuff', 120), ('CourierLoopVipLevel', 6),
                            ('CourierLoopVipDays', 7), ('SoulTrialGold', 50000),
                            ('SoulTrialRevoraPoints', 500), ('PathHuntGold', 25000),
                            ('PathHuntRevoraPoints', 100)):
            self.assertRegex(config, r'\b' + name + r'\s*=\s*' + str(value) + r';')


if __name__ == '__main__':
    unittest.main()
