import sys
import unittest
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from stage_magnolia_custom_packs import revise, TIP
from stage_summer_overlay import transform


def row(identity):
    fields = [str(identity).encode()] + [b"keep"] * 65
    fields[-1] += b"\r\n"
    return b"@@".join(fields)


class OverlayTests(unittest.TestCase):
    def test_only_reviewed_descriptions_change(self):
        before = row(721269)+row(720957)+row(1088001)
        after = revise(before)
        for index, (a, b) in enumerate(zip(before.splitlines(keepends=True), after.splitlines(keepends=True))):
            old, new = a.split(b"@@"), b.split(b"@@")
            self.assertEqual([i for i in range(66) if old[i] != new[i]], [53] if index < 2 else [])
            if index < 2:
                self.assertEqual(new[53], TIP)
        self.assertEqual(transform(transform(after, encode=True)), after)

    def test_custom_pack_retains_bound_soul(self):
        self.assertIn(b"bound~P7~Dragon~Soul", TIP)
        self.assertIn(b"always~bound", TIP)
        self.assertIn(b"1,000~RVPs~and~5,000~CPs", TIP)

    def test_missing(self):
        with self.assertRaises(ValueError): revise(row(721269))

    def test_duplicate(self):
        with self.assertRaises(ValueError): revise(row(721269)*2+row(720957))

    def test_malformed(self):
        with self.assertRaises(ValueError): revise(b"721269@@short\n"+row(720957))
