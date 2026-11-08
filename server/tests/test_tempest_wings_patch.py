import contextlib
import io
import re
import sys
import unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'tools/ClientPatches'))
import prepare_tempest_wings as patch

def sections(raw):return re.findall(rb'^\[\d+\]\r?$[\s\S]*?(?=^\[|\Z)',raw,re.M)
def owned(block):return re.search(rb'^MissionId=200\r?$',block,re.M)


class TempestPatchTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source=patch.WORK/'attack-talisman-composed-v3'
        cls.before=patch.checked(cls.source/'client/ini/Questinfo.ini',patch.SOURCES['client/ini/Questinfo.ini'])
        cls.items=patch.checked(cls.source/'client/ini/itemtype.txt',patch.SOURCES['client/ini/itemtype.txt'])
        cls.after=patch.rewrite(cls.before)

    def test_only_target_journal_changed(self):
        self.assertEqual(patch.rewrite(self.after),self.after)
        self.assertEqual([b for b in sections(self.before) if not owned(b)],[b for b in sections(self.after) if not owned(b)])

    def test_missing_duplicate_and_malformed_mission(self):
        block=next(b for b in sections(self.before) if owned(b))
        for bad in [self.before.replace(block,b''),self.before+block,
                    self.before.replace(block,block.replace(b'Profession=',b'Removed=',1)),
                    self.before.replace(block,block.replace(b'Content=',b'Content=bad\r\nContent=',1))]:
            with self.assertRaises(ValueError):patch.rewrite(bad)

    def test_real_route_and_reborn_visibility(self):
        block=next(b for b in sections(self.after) if owned(b))
        for token in [b'Lv_min=1\n',b'Lv_max=255',b'Profession=0',b'Prequest=0',b'Level 100 or reborn',
                      b'five Meteor Scrolls',b'136,103',b'145,111',b'212,196',b'bound Elite Tempest Wing',b'bound P7 soul',b'Once per character']:
            self.assertIn(token,block)
        for token in [b'chk_quest',b'chk_item',b'3429',b'Intention2=',b'50,000']:
            self.assertNotIn(token,block)

    def test_tooltip_only_preserves_item_stats(self):
        after=patch.tooltip(self.items);self.assertEqual(patch.tooltip(after),after);changed=[]
        for a,b in zip(self.items.splitlines(),after.splitlines()):
            if a==b:continue
            x,y=a.split(b'@@'),b.split(b'@@');changed.append(int(x[0]));self.assertEqual(x[:53]+x[54:],y[:53]+y[54:])
        self.assertEqual(changed,[3006392]);self.assertEqual(len(self.items.splitlines()),len(after.splitlines()))

    def test_missing_duplicate_changed_tooltip_rejected(self):
        line=next(l for l in self.items.splitlines(keepends=True) if l.startswith(b'3006392@@'))
        for bad in [self.items.replace(line,b''),self.items+line,self.items.replace(patch.TIPS[3006392][0].encode(),b'changed',1)]:
            with self.assertRaises(ValueError):patch.tooltip(bad)

    def test_private_composition_and_preserved_inputs(self):
        with contextlib.redirect_stdout(io.StringIO()):manifest=patch.prepare();self.assertEqual(patch.prepare(),manifest)
        destination=patch.WORK/'tempest-wings-composed-v2'
        for row in manifest['outputs']:
            self.assertEqual(patch.sha((destination/row['path']).read_bytes()),row['sha256'])
            if row['path'] in patch.SOURCES and row['path'] not in ['client/ini/Questinfo.ini','client/ini/itemtype.txt','client/ini/itemtype.dat']:
                self.assertEqual(row['sha256'],patch.SOURCES[row['path']])
        self.assertEqual((destination/'server/database/maps/tempest-1100.map').read_bytes(),(patch.WORK/'tempest-scene/server/database/maps/tempest-1100.map').read_bytes())
        for row in manifest['inputs']:self.assertEqual(patch.sha(Path(row['path']).read_bytes()),row['sha256'])
        self.assertFalse(manifest['installed']);self.assertFalse(manifest['signed']);self.assertFalse(manifest['published'])
        dat=(destination/'client/ini/itemtype.dat').read_bytes();self.assertEqual(patch.transform(patch.transform(dat),True),dat)


if __name__=='__main__':unittest.main()
