import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from audit_achievement_career import report


class CareerAccountingTests(unittest.TestCase):
    def evaluate(self, issues):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "docs/research").mkdir(parents=True)
            (root / "docs/research/ACHIEVEMENT_CAREER_GAPS.json").write_text(
                json.dumps(dict(target=3, reviewed_input_sha256={}, issues=issues)),
                encoding="utf-8")
            rows = [dict(achievement_id=i, source_status="producer") for i in range(1, 6)]
            rows.append(dict(achievement_id=6, source_status="no-trusted-producer"))
            with patch("audit_achievement_career.inventory", return_value=rows):
                return report(root)

    def test_admission_uncertainty_reduces_planning_not_unavailability_ceiling(self):
        result = self.evaluate([
            dict(id=1, classification="unavailable"),
            dict(id=2, classification="supplier-unproven"),
            dict(id=3, classification="admission-unproven")])
        self.assertEqual(result["ceiling_after_known_unavailable"], 4)
        self.assertEqual(result["unproven_planning_pool"], 2)
        self.assertEqual(result["planning_margin"], -1)
        self.assertEqual(result["admission_routes_unproven"], [3])
        self.assertFalse(result["compatible_career_verified"])

    def test_rejects_double_subtraction_and_missing_producer_overlap(self):
        for ids in ([1, 1], [6], [99]):
            with self.subTest(ids=ids), self.assertRaises(ValueError):
                self.evaluate([dict(id=i, classification="admission-unproven") for i in ids])

    def test_rejects_unknown_classification(self):
        with self.assertRaises(ValueError):
            self.evaluate([dict(id=1, classification="earned")])


if __name__ == "__main__":
    unittest.main()
