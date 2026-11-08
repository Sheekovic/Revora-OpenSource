from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).parents[1] / "tools"))
import stage_dis_city_overlay as overlay
import stage_summer_overlay as summer
import stage_labyrinth_overlay as labyrinth


def item_row(item):
    fields = [b"0"] * 66
    fields[0] = str(item).encode("ascii")
    fields[53] = b"Synthetic~description"
    return b"@@".join(fields) + b"\r\n"


def mission_row(mission):
    return ("[" + str(mission - 5849) + "]\r\nMissionId=" + str(mission) + "\r\nName=Test\r\n" +
            "".join(key + "=synthetic\r\n" for key in overlay.quest_fields(mission)) + "Untouched=\x80\r\n").encode("latin1")


class DisCityOverlayTests(unittest.TestCase):
    def setUp(self):
        self.items = item_row(42) + b"".join(item_row(item) for item in overlay.DESCRIPTIONS)
        self.quests = b"[1]\r\nMissionId=42\r\nUntouched=\xff\r\n" + b"".join(mission_row(mission) for mission in overlay.OBJECTIVES)

    def test_only_reviewed_item_descriptions_change(self):
        result, changes = overlay.revise_items(self.items)
        self.assertEqual(11, len(changes))
        self.assertTrue(result.startswith(item_row(42)))
        for before, after in zip(self.items.splitlines(), result.splitlines()):
            old, new = before.split(b"@@"), after.split(b"@@")
            self.assertEqual([] if old[0] == b"42" else [53], [i for i in range(66) if old[i] != new[i]])

    def test_missions_preserve_unrelated_bytes_and_match_ready_counter(self):
        result, changes = overlay.revise_quests(self.quests)
        self.assertEqual(4, len(changes))
        self.assertEqual(self.quests.count(b"\r\n"), result.count(b"\r\n"))
        self.assertTrue(result.startswith(self.quests.split(b"[623]")[0]))
        self.assertEqual(4, result.count(b"Untouched=\x80\r\n"))
        self.assertEqual(4, result.count(b"ActivityEndTime=0\r\n"))
        self.assertEqual(4, result.count(b"FinishNpcId=\r\n"))
        for mission in overlay.OBJECTIVES:
            self.assertIn(("[chk_quest " + str(mission) + ",1,1,1]").encode("ascii"), result)

    def test_difficulty_and_bound_event_reward_contract(self):
        result, _ = overlay.revise_quests(self.quests)
        for mode in (b"Standard", b"Veteran", b"Nightmare"):
            self.assertEqual(4 if mode == b"Standard" else 8, result.count(mode))
        self.assertEqual(4, result.count(b"All difficulties share one weekly victory"))
        self.assertEqual(4, result.count(b"in each difficulty and combat edition"))
        self.assertEqual(1, result.count(b"one random bound P7 soul"))
        self.assertIn(b"Newly admitted expeditions", result)
        self.assertEqual(result, overlay.revise_quests(result)[0])

    def test_combined_profile_keeps_prior_unrelated_content(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            items, quests = root / "items/ini/itemtype.dat", root / "journal/ini/Questinfo.ini"
            custom = item_row(721269).replace(b"Synthetic~description", b"Bound~P7~1000~RVP~5000~CP")
            epic = b"[8000]\r\nMissionId=3274\r\nPrize=Unbound saber +12 -7 two empty sockets\r\n"
            inherited_rows = []
            for item in list(summer.DESCRIPTIONS) + list(labyrinth.DESCRIPTIONS):
                fields = item_row(item).split(b"@@"); fields[46] = b"10000"
                inherited_rows.append(b"@@".join(fields))
            inherited = b"".join(inherited_rows)
            encrypted = overlay.transform(self.items + inherited + custom, True)
            journal = self.quests + epic
            for source, content in ((items, encrypted), (quests, journal)):
                source.parent.mkdir(parents=True); source.write_bytes(content)
            with patch.object(overlay, "COMBINED_ITEM_SHA256", overlay.digest(encrypted)), patch.object(overlay, "COMBINED_QUEST_SHA256", overlay.digest(journal)):
                manifest = overlay.stage(items, quests, root / "stage", "combined")
                self.assertEqual("combined", manifest["source_profile"])
                self.assertEqual(36, len(manifest["files"][0]["changes"]))
                decoded = overlay.transform((root / "stage/ini/itemtype.dat").read_bytes())
                for item, description in summer.DESCRIPTIONS.items():
                    self.assertIn(description.replace(" ", "~").encode(), decoded)
                for description in labyrinth.DESCRIPTIONS.values():
                    self.assertIn(description.replace(" ", "~").encode(), decoded)
                self.assertFalse(manifest["signed"] or manifest["installed"] or manifest["published"])
                self.assertTrue(overlay.transform((root / "stage/ini/itemtype.dat").read_bytes()).endswith(custom))
                self.assertTrue((root / "stage/ini/Questinfo.ini").read_bytes().endswith(epic))
                with self.assertRaises(ValueError):
                    overlay.stage(items, quests, root / "wrong", "baseline")
            self.assertEqual(encrypted, items.read_bytes())
            self.assertEqual(journal, quests.read_bytes())

    def test_missing_duplicate_and_malformed_content_rejected(self):
        for data in [self.items + item_row(723085), item_row(723085), self.items.replace(b"723085@@", b"723085@@0@@")]:
            with self.assertRaises(ValueError):
                overlay.revise_items(data)
        for data in [self.quests + mission_row(6472), mission_row(6472), self.quests.replace(b"Prize=synthetic\r\n", b"", 1),
                     self.quests.replace(b"Name=Test", b"MissionId=42\r\nName=Test", 1)]:
            with self.assertRaises(ValueError):
                overlay.revise_quests(data)

    def test_profile_and_destination_gates_preserve_inputs(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            items, quests = root / "items/ini/itemtype.dat", root / "client/ini/Questinfo.ini"
            for source in (items, quests):
                source.parent.mkdir(parents=True)
                source.write_bytes(b"unreviewed")
            for destination in [root / "stage", root / "items/stage", root / "client/stage", root]:
                with self.assertRaises(ValueError):
                    overlay.stage(items, quests, destination)
            self.assertEqual(b"unreviewed", items.read_bytes())
            self.assertEqual(b"unreviewed", quests.read_bytes())
            self.assertFalse((root / "stage").exists())

    def test_complete_stage_is_separate_roundtripped_and_immutable(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            items, quests = root / "items/ini/itemtype.dat", root / "client/ini/Questinfo.ini"
            encrypted = overlay.transform(self.items, True)
            for source, content in [(items, encrypted), (quests, self.quests)]:
                source.parent.mkdir(parents=True)
                source.write_bytes(content)
            with patch.object(overlay, "ITEM_SHA256", overlay.digest(encrypted)), patch.object(overlay, "QUEST_SHA256", overlay.digest(self.quests)):
                manifest = overlay.stage(items, quests, root / "stage")
                for entry in manifest["files"]:
                    self.assertEqual(entry["output_sha256"], overlay.digest((root / "stage" / entry["destination"]).read_bytes()))
                self.assertEqual(overlay.revise_items(self.items)[0], overlay.transform((root / "stage/ini/itemtype.dat").read_bytes()))
                with self.assertRaises(ValueError):
                    overlay.stage(items, quests, root / "stage")
            self.assertEqual(encrypted, items.read_bytes())
            self.assertEqual(self.quests, quests.read_bytes())


if __name__ == "__main__":
    unittest.main()
