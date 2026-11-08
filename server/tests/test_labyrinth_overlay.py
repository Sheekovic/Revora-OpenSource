import importlib.util
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).parents[1] / "tools"))
import stage_labyrinth_overlay as overlay


def row(item):
    fields = [b"0"] * 66
    fields[0] = str(item).encode("ascii")
    fields[46] = b"10000"
    fields[53] = b"Synthetic~description"
    return b"@@".join(fields) + b"\r\n"


class LabyrinthOverlayTests(unittest.TestCase):
    def test_only_descriptions_change(self):
        source = row(42) + b"".join(row(i) for i in overlay.DESCRIPTIONS)
        result, changes = overlay.revise(source)
        self.assertEqual(9, len(changes))
        self.assertTrue(result.startswith(row(42)))
        self.assertEqual(source.count(b"\r\n"), result.count(b"\r\n"))
        for old, new in zip(source.splitlines(), result.splitlines()):
            before, after = old.split(b"@@"), new.split(b"@@")
            self.assertEqual([53] if before[0] != b"42" else [], [i for i in range(66) if before[i] != after[i]])

    def test_duplicate_and_missing_rejected(self):
        with self.assertRaises(ValueError):
            overlay.revise(row(721540))
        with self.assertRaises(ValueError):
            overlay.revise(b"".join(row(i) for i in overlay.DESCRIPTIONS) + row(721540))

    def test_profile_and_source_protection(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "source/ini/itemtype.dat"
            source.parent.mkdir(parents=True)
            source.write_bytes(b"unreviewed")
            for destination in [root / "stage", root / "source/stage"]:
                with self.assertRaises(ValueError):
                    overlay.stage(source, destination)
            self.assertEqual(b"unreviewed", source.read_bytes())
            self.assertFalse((root / "stage").exists())


if __name__ == "__main__":
    unittest.main()
