from pathlib import Path
import sys
import unittest
import json
import re
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"tools"))
from audit_collection_capacity import compare


class CollectionCapacityTests(unittest.TestCase):
    def test_reviewed_mount_proficiency_is_nonbinding_but_other_fields_stay_strict(self):
        for identity in (200621,200622,200623,200624):
            ident=str(identity).encode()
            coat=[ident,b"0",b"2",b"0",b"0",b"0",b"0",b"5"]
            client=[ident,b"name",b"0",b"0",b"0",b"0"]
            server=client[:];server[3]=b"1"
            def accepted(s,c,w=coat):
                return compare({identity:w},{identity:w},{identity:s},{identity:c})["mounts"]["compatible_client_ids"]
            self.assertEqual([identity],accepted(server,client))
            for field in (2,4,5):
                changed=server[:];changed[field]=b"1"
                self.assertEqual([],accepted(changed,client))
            changed=server[:];changed[3]=b"2"
            self.assertEqual([],accepted(changed,client))
            garment=coat[:];garment[2]=b"1"
            self.assertEqual([],compare({identity:garment},{identity:garment},{identity:server},{identity:client})["garments"]["compatible_client_ids"])
        identity=200625
        coat=[b"200625",b"0",b"2",b"0",b"0",b"0",b"0",b"5"]
        server=[b"200625",b"name",b"0",b"1",b"0",b"0"]
        client=server[:];client[3]=b"0"
        self.assertEqual([],compare({identity:coat},{identity:coat},{identity:server},{identity:client})["mounts"]["compatible_client_ids"])

    def test_production_numeric_catalog_matches_reviewed_evidence(self):
        root=Path(__file__).resolve().parents[1]
        evidence=json.loads((root/"docs/research/COLLECTION_CAPACITY.json").read_text())
        source=(root/"Game/Features/CollectionAchievementCatalog.cs").read_text()
        for field,category in (("Garments","garments"),("Mounts","mounts")):
            block=re.search(field+r"\s*=\s*\{(.*?)\};",source,re.S)[1]
            self.assertEqual(evidence["categories"][category]["compatible_client_ids"],
                             [int(i) for i in re.findall(r"(\d+)u",block)])
        repair=(root/"Game/Features/WardrobeCatalogRepair.cs").read_text()
        numeric=re.search(r"Missing\s*=\s*\{(.*?)\};",repair,re.S)[1]
        expected=[r[k] for category in ("garments","mounts")
                  for r in evidence["categories"][category]["missing_metadata"] for k in ("id","category","index","stars")]
        self.assertEqual(expected,[int(i) for i in re.findall(r"(\d+)u",numeric)])

    def test_missing_wrong_category_and_eligibility_are_not_counted_as_shared(self):
        def coat(category, stars=b"5"):
            return [b"0",b"0",category,b"0",b"0",b"0",b"0",stars]
        base=[b"id",b"name",b"class",b"skill",b"level",b"gender"]
        server={1:coat(b"2"),2:coat(b"1"),4:coat(b"2"),5:coat(b"2",b"4")}
        client={i:coat(b"2") for i in range(1,6)}
        items={i:base[:] for i in range(1,6)}
        altered={i:r[:] for i,r in items.items()};altered[4][5]=b"other"
        result=compare(server,client,items,altered)["mounts"]
        self.assertEqual(2,result["shared_compatible_item_designs"])
        self.assertEqual([2,3],result["compatible_missing_server_wardrobe"])
        self.assertEqual([4],result["incompatible_client_item_ids"])
        self.assertEqual([dict(id=4,reasons=["item-identity-or-eligibility-disagreement"])],result["incompatible_client_items"])
        self.assertEqual([5],result["shared_star_disagreements"])
        self.assertTrue(result["thresholds"][0]["shared_catalog_can_support"])
        self.assertFalse(result["thresholds"][1]["repaired_catalog_can_support"])
        self.assertFalse(any(r["ordinary_earning_verified"] for r in result["thresholds"]))

    def test_unknown_category_or_truncated_row_rejected(self):
        for row in ([b"0"]*3,[b"0"]*8):
            with self.assertRaises(ValueError):compare({1:row},{},{},{})

    def test_display_name_difference_is_reported_without_changing_numeric_eligibility(self):
        coat=[b"1",b"0",b"2",b"0",b"0",b"0",b"0",b"5"]
        base=[b"1",b"original",b"0",b"0",b"0",b"0"]
        renamed=base[:];renamed[1]=b"Revora name"
        report=compare({1:coat},{1:coat},{1:renamed},{1:base})["mounts"]
        self.assertEqual([1],report["compatible_client_ids"])
        self.assertEqual([1],report["compatible_display_name_differences"])
        self.assertEqual([],report["incompatible_client_items"])

    def test_missing_both_item_definitions_is_distinct_from_field_disagreement(self):
        coat=[b"1",b"0",b"2",b"0",b"0",b"0",b"0",b"5"]
        report=compare({1:coat},{1:coat},{},{})["mounts"]
        self.assertEqual([dict(id=1,reasons=["missing-server-item","missing-client-item"])],
                         report["incompatible_client_items"])
        self.assertEqual(0,report["compatible_client_designs"])
        self.assertFalse(report["thresholds"][0]["repaired_catalog_can_support"])
        truncated=compare({1:coat},{1:coat},{1:[b"1"]*6},{1:[b"1"]})["mounts"]
        self.assertEqual([dict(id=1,reasons=["truncated-item-identity"])],truncated["incompatible_client_items"])


if __name__ == "__main__":unittest.main()
