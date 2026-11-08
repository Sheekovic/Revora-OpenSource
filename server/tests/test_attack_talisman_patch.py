"""Read-only checks and private Fan patch composition validation."""
import contextlib
import io
import re
import sys
import unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"tools/ClientPatches"))
import prepare_attack_talisman as patch

def sections(raw):return re.findall(rb"^\[\d+\]\r?$[\s\S]*?(?=^\[|\Z)",raw,re.M)
def owned(block):return re.search(rb"^MissionId=40[0-9]\r?$",block,re.M)

class FanPatchTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source=patch.WORK/"defense-talisman-composed-v2"
        cls.before=patch.checked(cls.source/"client/ini/Questinfo.ini",patch.SOURCES["client/ini/Questinfo.ini"])
        cls.after=patch.rewrite(cls.before)
        cls.items=patch.checked(cls.source/"client/ini/itemtype.txt",patch.SOURCES["client/ini/itemtype.txt"])
    def test_idempotence(self):self.assertEqual(patch.rewrite(self.after),self.after)
    def test_missing_mission(self):
        with self.assertRaises(ValueError):patch.rewrite(self.before.replace(b"MissionId=400",b"MissionId=399",1))
    def test_duplicate_mission(self):
        with self.assertRaises(ValueError):patch.rewrite(self.before.replace(b"MissionId=401",b"MissionId=400",1))
    def test_missing_or_duplicate_field(self):
        block=next(b for b in sections(self.before) if re.search(rb"^MissionId=400\r?$",b,re.M))
        for bad in (block.replace(b"Profession=",b"Removed=",1),block.replace(b"Profession=",b"Profession=0\r\nProfession=",1),block.replace(b"Content=",b"Removed=",1)):
            with self.assertRaises(ValueError):patch.rewrite(self.before.replace(block,bad,1))
    def test_unrelated_journal_preserved(self):
        self.assertEqual([b for b in sections(self.before) if not owned(b)],[b for b in sections(self.after) if not owned(b)])
    def test_prerequisite_eligibility_and_reward(self):
        own=[b for b in sections(self.after) if owned(b)];self.assertEqual(len(own),10)
        for i,b in enumerate(own):
            for text in (b"Lv_min=100",b"Lv_max=255",b"Profession=0",b"bound Super Heaven Fan",b"bound P7 soul",b"Once per character"):self.assertIn(text,b)
            self.assertRegex(b,rb"(?m)^Prequest="+str(528 if i==0 else 399+i).encode()+rb"\r?$")
            self.assertNotIn(b"chk_item",b);self.assertNotIn(b"Intention2=",b)
    def test_directions_match_revora_route(self):
        own=b"\n".join(b for b in sections(self.after) if owned(b))
        for target in (b"469,840",b"475,844",b"680,720",b"694,720",b"680,734",b"694,734",b"9910414,1015,686,723",b"60,64"):self.assertIn(target,own)
        for obsolete in (b"55,109",b"653,700",b"4591,1015",b"Elite Star Fan"):self.assertNotIn(obsolete,own)
    def test_only_tooltip_fields_change(self):
        after=patch.tooltip(self.items);self.assertEqual(patch.tooltip(after),after)
        self.assertEqual(len(self.items.splitlines()),len(after.splitlines()));changed=set()
        for old,new in zip(self.items.splitlines(),after.splitlines()):
            a,b=old.split(b"@@"),new.split(b"@@")
            if old!=new:changed.add(int(a[0]));self.assertEqual(a[:53]+a[54:],b[:53]+b[54:])
        self.assertEqual(changed,set(patch.TIPS))
    def test_changed_missing_duplicate_tooltip(self):
        old=patch.TIPS[710216][0].encode();line=next(l for l in self.items.splitlines(keepends=True) if l.startswith(b"710216@@"))
        for bad in (self.items.replace(old,b"Changed",1),self.items.replace(line,b"",1),self.items+line):
            with self.assertRaises(ValueError):patch.tooltip(bad)
    def test_private_bundle_repeat_readback_and_prior_repairs(self):
        with contextlib.redirect_stdout(io.StringIO()):manifest=patch.prepare();self.assertEqual(patch.prepare(),manifest)
        target=patch.WORK/"attack-talisman-composed-v3"
        for row in manifest["outputs"]:
            data=(target/row["path"]).read_bytes();self.assertEqual(patch.sha(data),row["sha256"])
            if row["path"] not in ("client/ini/Questinfo.ini","client/ini/itemtype.txt","client/ini/itemtype.dat"):self.assertEqual(patch.sha(data),patch.SOURCES[row["path"]])
        for row in manifest["inputs"]:self.assertEqual(patch.sha(Path(row["path"]).read_bytes()),row["sha256"])
        self.assertFalse(manifest["installed"]);self.assertFalse(manifest["signed"]);self.assertFalse(manifest["published"])
        dat=(target/"client/ini/itemtype.dat").read_bytes();self.assertEqual(patch.transform(patch.transform(dat),True),dat)

if __name__=="__main__":unittest.main()
