import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("summer_overlay", Path(__file__).parents[1] / "tools/stage_summer_overlay.py")
overlay = importlib.util.module_from_spec(spec)
spec.loader.exec_module(overlay)


def row(item):
    fields = [b"0"] * 66
    fields[0] = str(item).encode("ascii")
    fields[46] = b"10000"
    fields[53] = b"Synthetic~old~description"
    return b"@@".join(fields) + b"\r\n"


class SummerOverlayTests(unittest.TestCase):
    def test_round_trip(self):
        data = bytes(range(256)) * 3
        self.assertEqual(data, overlay.transform(overlay.transform(data, True)))

    def test_exact_field_scope(self):
        plain = b"header\r\n" + row(42) + b"".join(row(i) for i in overlay.DESCRIPTIONS)
        revised, changes = overlay.revise(plain)
        self.assertEqual(16, len(changes))
        self.assertTrue(revised.startswith(b"header\r\n" + row(42)))
        for old, new in zip(plain.splitlines(), revised.splitlines()):
            if b"@@" not in old or old.startswith(b"42@@"):
                self.assertEqual(old, new)
                continue
            before, after = old.split(b"@@"), new.split(b"@@")
            item = int(before[0])
            for index in range(66):
                if index not in (46, 53):
                    self.assertEqual(before[index], after[index])
            self.assertEqual(b"1" if item in overlay.IDENTITIES else b"10000", after[46])
        self.assertEqual(plain.count(b"\r\n"), revised.count(b"\r\n"))

    def test_incomplete_or_duplicate_rejected(self):
        with self.assertRaises(ValueError):
            overlay.revise(row(3303704))
        with self.assertRaises(ValueError):
            overlay.revise(b"".join(row(i) for i in overlay.DESCRIPTIONS) + row(3303704))

    def test_unapproved_source_and_client_destination_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "client/ini/itemtype.dat"
            source.parent.mkdir(parents=True)
            source.write_bytes(b"unreviewed")
            with self.assertRaises(ValueError):
                overlay.stage(source, root / "stage")
            with self.assertRaises(ValueError):
                overlay.stage(source, root / "client/stage")
            self.assertEqual(b"unreviewed", source.read_bytes())
            self.assertFalse((root / "stage").exists())


if __name__ == "__main__":
    unittest.main()
