import re
import sys
import unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
import audit_equipment_aggregate as audit


class AggregateAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.raw=(ROOT.parents[1]/'6711/ini/Questinfo.ini').read_bytes()
        cls.text=cls.raw.decode('cp1252')
        cls.block=next(b for b in re.findall(r'(?m)^\[\d+\]\n[\s\S]*?(?=^\[|\Z)',cls.text.replace('\r\n','\n')) if '\nMissionId=200\n' in b)

    def test_reviewed_numeric_coverage(self):
        result=audit.audit(self.raw,ROOT)
        self.assertEqual(len(result['chains']),19)
        self.assertEqual(result['named_missions'],132)
        self.assertEqual(result['excluded_unnamed_missions'],list(range(950,968)))
        self.assertTrue(result['enabled']);self.assertFalse(result['original_aggregate_rule_verified'])

    def test_changed_catalog_rejected(self):
        with self.assertRaises(ValueError):audit.audit(self.raw+b'\n',ROOT)

    def test_missing_named_mission_rejected(self):
        with self.assertRaises(ValueError):audit.coverage(self.text.replace('MissionId=200\n','MissionId=201\n'),ROOT)

    def test_duplicate_identity_rejected(self):
        with self.assertRaises(ValueError):audit.coverage(self.text+'\n'+self.block,ROOT)

    def test_changed_edge_rejected(self):
        normalized=self.text.replace('\r\n','\n')
        with self.assertRaisesRegex(ValueError,'predecessor'):
            audit.coverage(normalized.replace(self.block,self.block.replace('Prequest=0','Prequest=520')),ROOT)

    def test_duplicate_field_rejected(self):
        with self.assertRaisesRegex(ValueError,'Ambiguous'):
            audit.coverage(self.text+'\n'+self.block.replace('TypeId=4','TypeId=4\nTypeId=4'),ROOT)


if __name__=='__main__':unittest.main()
