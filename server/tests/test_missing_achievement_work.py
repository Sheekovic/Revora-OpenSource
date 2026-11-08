import json
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import audit_missing_achievement_work as audit


class MissingAchievementWorkTests(unittest.TestCase):
    def test_every_missing_identity_has_exactly_one_work_package(self):
        saved = json.loads((ROOT / "docs/research/MISSING_ACHIEVEMENT_WORK.json").read_text())
        rows = audit.inventory(ROOT)
        missing = {r["achievement_id"] for r in rows if r["source_status"] == "no-trusted-producer"}
        assigned = [i for p in saved["packages"] for i in p["missing_ids"]]
        self.assertEqual(missing, set(assigned))
        self.assertEqual(len(missing), len(assigned))
        self.assertEqual(len(missing), saved["missing_producer_count"])
        self.assertEqual(len(rows)-len(missing), saved["source_producer_count"])
        self.assertFalse(saved["playable_route_verified"])
        self.assertEqual(saved["packages"], [
            dict(package=name, missing_ids=sorted(missing.intersection(ids)), required_work=work)
            for name, ids, work in audit.PACKAGES if missing.intersection(ids)])

    def test_changed_client_is_rejected_before_decoding(self):
        with self.assertRaisesRegex(ValueError, "catalog changed"):
            audit.report(ROOT, b"unreviewed catalog")


if __name__ == "__main__":
    unittest.main()
