import csv
import importlib.util
import io
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("second_matrix", ROOT / "tools/build_second_rebirth_matrix.py")
matrix = importlib.util.module_from_spec(spec)
spec.loader.exec_module(matrix)

class SecondMatrixTests(unittest.TestCase):
    def test_sibling_blocks_do_not_lose_water_ninja_destinations(self):
        source = """<h4>Water Taoist &gt; Ninja</h4><div>Trojan &gt; Stigma</div>
            <div><strong>Monk/Saint &gt;</strong> Toxic Fog, Stigma<br>
            Pirate &gt; Magic Shield, Toxic Fog</div></table>footer"""
        rows = matrix.extract(source)
        self.assertEqual(set(rows), {(13, 5, 1), (13, 5, 6), (13, 5, 7)})
        self.assertEqual(matrix.magic_ids(rows[13, 5, 6][0]), [1095, 6001])

    def test_duplicate_rows_are_preserved_for_explicit_review(self):
        rows = matrix.extract("<h4>Archer &gt; Pirate</h4><div>Archer &gt; Gale Bomb</div>"
                              "<div>Archer &gt; Poisonous Arrows, Gale Bomb</div></table>")
        self.assertEqual(rows[4, 7, 4], ["Gale Bomb", "Poisonous Arrows, Gale Bomb"])

    def test_hash_gate_and_unknown_numeric_mapping_fail_closed(self):
        with self.assertRaises(ValueError):
            matrix.render(b"modified public source")
        with self.assertRaises(KeyError):
            matrix.magic_ids("Unknown Art")
        with self.assertRaises(ValueError):
            matrix.extract("<h4>New Class &gt; Trojan</h4>")

    def test_known_punctuation_aliases_keep_both_skills(self):
        self.assertEqual(matrix.magic_ids("Healing Rain Water Elf, Spirit Healing and Golem, Flying Moon . Reflect"),
                         [1055, 1190, 1270, 1280, 1320, 3060])
        self.assertEqual(matrix.magic_ids("Poison, SpiritHeading".replace("SpiritHeading", "Spirit Heading")), [1190, 5002])

    def test_committed_coverage_has_exactly_two_labeled_inferences(self):
        with (ROOT / "Game/Features/Data/SecondRebirthSkills.csv").open(encoding="utf-8") as source:
            rows = list(csv.DictReader(source))
        keys = [(int(r["first_school"]), int(r["second_school"]), int(r["third_school"])) for r in rows]
        self.assertEqual(len(keys), 729)
        self.assertEqual(len(set(keys)), 729)
        inferred = [key for key, row in zip(keys, rows) if row["evidence"] == "revora-retention-inference"]
        self.assertEqual(inferred, [(4, 7, 1), (4, 7, 4)])
        for row in rows:
            for art in row["retained_magic_ids"].split(";"):
                if art:
                    self.assertIn(int(art), matrix.MAGIC.values())

if __name__ == "__main__":
    unittest.main()
