import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("quest_audit", Path(__file__).parents[1] / "tools/audit_quest_prerequisites.py")
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


def node(item, kind, predecessor="0", low=1, high=140, name="Synthetic"):
    return f"[{item}]\nMissionId={item}\nTypeId={kind}\nPrequest={predecessor}\nLv_min={low}\nLv_max={high}\nName={name}\n"


class QuestPrerequisiteTests(unittest.TestCase):
    def test_branches_duplicates_and_missing_edges(self):
        result = audit.analyze(node(1, 1) + node(1, 1) + node(2, 1, "1|99") + node(3, 4, name=""))
        self.assertEqual([1], result["duplicate_mission_ids"])
        self.assertEqual([99], result["categories"]["1"]["missing_listed_predecessors"])
        self.assertEqual([3], result["categories"]["4"]["blank_name_ids"])
        self.assertEqual([], result["categories"]["4"]["named_root_ids"])

    def test_candidate_does_not_certify_original_aggregate(self):
        chain = "".join(node(i, 1, str(i-1) if i>3478 else "0") for i in range(3478, 3482))
        result = audit.analyze(chain)["newcomer_intro_candidate"]
        self.assertTrue(result["unique_complete_chain"])
        self.assertFalse(result["original_aggregate_verified"])
        self.assertIsNone(result["rows"][0]["begin_npc_map_xy"])
        self.assertFalse(audit.analyze(chain + node(3478, 1))["newcomer_intro_candidate"]["unique_complete_chain"])
        self.assertFalse(audit.analyze(chain.replace("Prequest=3479", "Prequest=3479|900"))["newcomer_intro_candidate"]["unique_complete_chain"])
        self.assertFalse(audit.analyze(chain.replace("MissionId=3481", "MissionId=99"))["newcomer_intro_candidate"]["unique_complete_chain"])

    def test_level_reversal_is_separate_from_missing_parent(self):
        result = audit.analyze(node(10, 4, low=98, high=118) + node(11, 4, "10", low=45, high=65))
        self.assertEqual([{"parent": 10, "child": 11, "child_max_level": 65}], result["categories"]["4"]["level_reversals"])
        self.assertEqual([], result["categories"]["4"]["missing_listed_predecessors"])


if __name__ == "__main__":
    unittest.main()
