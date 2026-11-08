"""Read-only checks of the private composed Tower patch and malformed sources."""
import contextlib
import io
import re
import sys
import unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"tools/ClientPatches"))
import prepare_defense_talisman as patch
from stage_gloomy_jail import index_records, RECORD
from stage_defense_talisman_scenes import RECORDS

class TowerPatchTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.before=patch.checked(patch.WORK/"desert-expedition-journal/staged/ini/Questinfo.ini",patch.SOURCE)
        cls.after=patch.rewrite(cls.before)
        cls.items=patch.checked(patch.WORK/"desert-expedition-journal/staged/ini/itemtype.txt","945d0c61f987f1bde6f051f832dc61a59f3127799dd5a454bf88e08f95d73f58")
    def test_journal_idempotence(self):self.assertEqual(patch.rewrite(self.after),self.after)
    def test_missing_mission(self):
        with self.assertRaises(ValueError):patch.rewrite(self.before.replace(b"MissionId=520",b"MissionId=519",1))
    def test_duplicate_mission(self):
        with self.assertRaises(ValueError):patch.rewrite(self.before.replace(b"MissionId=521",b"MissionId=520",1))
    def test_missing_field(self):
        changed=self.before
        blocks=list(re.finditer(rb"^\[\d+\]\r?$[\s\S]*?(?=^\[|\Z)",changed,re.M))
        block=next(m.group() for m in blocks if re.search(rb"^MissionId=520\r?$",m.group(),re.M))
        with self.assertRaises(ValueError):patch.rewrite(changed.replace(block,block.replace(b"Lv_min=100",b"Removed=100",1),1))
    def test_unrelated_journal(self):
        def unrelated(raw):return [m.group() for m in re.finditer(rb"^\[\d+\]\r?$[\s\S]*?(?=^\[|\Z)",raw,re.M) if not re.search(rb"^MissionId=52[0-8]\r?$",m.group(),re.M)]
        self.assertEqual(unrelated(self.before),unrelated(self.after))
    def test_eligibility_and_reward(self):
        own=[m.group() for m in re.finditer(rb"^\[\d+\]\r?$[\s\S]*?(?=^\[|\Z)",self.after,re.M) if re.search(rb"^MissionId=52[0-8]\r?$",m.group(),re.M)]
        self.assertEqual(len(own),9)
        for b in own:self.assertIn(b"Lv_max=255",b);self.assertIn(b"Once per character",b);self.assertIn(b"bound P7 soul",b)
    def test_tooltips_only(self):
        edited=patch.tooltip(self.items);self.assertEqual(patch.tooltip(edited),edited)
        for old,new in zip(self.items.splitlines(),edited.splitlines()):
            a,b=old.split(b"@@"),new.split(b"@@")
            if a[0].isdigit() and int(a[0]) in patch.TIPS:self.assertEqual(a[:53]+a[54:],b[:53]+b[54:])
            else:self.assertEqual(old,new)
    def test_changed_tooltip(self):
        old=next(iter(patch.TIPS.values()))[0].encode()
        with self.assertRaises(ValueError):patch.tooltip(self.items.replace(old,b"Changed",1))
    def test_both_map_repairs_preserve_prior_records(self):
        for side,path,expected in [("client",patch.ROOT/"6711/ini/GameMap.dat",patch.CLIENT_INDEX),("server",patch.ROOT/"Source/Revora-6711/bin/Debug/database/GameMap.dat",patch.SERVER_INDEX)]:
            original=patch.checked(path,expected);combined=patch.add_scenes(patch.add_map(original))
            self.assertEqual(index_records(combined),index_records(original)+[RECORD]+RECORDS)
            with self.assertRaises(ValueError):patch.add_scenes(combined)
            with self.assertRaises(ValueError):patch.add_map(combined)
    def test_private_bundle_repeat_and_readback(self):
        with contextlib.redirect_stdout(io.StringIO()):manifest=patch.prepare();self.assertEqual(patch.prepare(),manifest)
        root=patch.WORK/"defense-talisman-composed-v2"
        for row in manifest["outputs"]:self.assertEqual(patch.sha((root/row["path"]).read_bytes()),row["sha256"])
        for row in manifest["inputs"]:self.assertEqual(patch.sha(Path(row["path"]).read_bytes()),row["sha256"])
        self.assertFalse(manifest["installed"]);self.assertFalse(manifest["signed"]);self.assertFalse(manifest["published"])

if __name__=="__main__":unittest.main()
