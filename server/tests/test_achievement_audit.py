import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("achievement_audit", Path(__file__).parents[1] / "tools/audit_achievement_producers.py")
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class AchievementAuditTests(unittest.TestCase):
    def test_sparse_masks_exclude_undefined_positions(self):
        source = "const int Count=2; Masks={0x00000001u," + ",".join(["0x00000000u"] * 11) + ",0x00400000u};"
        self.assertEqual([10101, 11323], audit.catalog_ids(source))

    def test_changed_catalog_rejected(self):
        with self.assertRaises(ValueError):
            audit.catalog_ids("const int Count=337; Masks={0xFFFFFFFFu};")
        with self.assertRaises(ValueError):
            audit.catalog_ids("const int Count=337; Masks={" + ",".join(["0x00000000u"] * 13) + "};")

    def test_checked_in_inventory_matches_sources(self):
        root = Path(__file__).parents[1]
        rows = audit.inventory(root)
        self.assertEqual(337, len(rows))
        self.assertEqual(len(rows), len({row["achievement_id"] for row in rows}))
        self.assertEqual((root / "docs/research/ACHIEVEMENT_PRODUCER_INVENTORY.csv").read_text(), audit.serialize(rows))


if __name__ == "__main__":
    unittest.main()
