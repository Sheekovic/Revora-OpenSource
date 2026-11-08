from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).parents[1] / "tools/ClientPatches"))
import prepare_sword_master_catalog as patch


def row(identity=720842, description=patch.OLD):
    fields = [b"0"] * 66
    fields[0], fields[1], fields[52], fields[53] = str(identity).encode(), b"SwordSoul", b"Treasure", description
    return b"@@".join(fields) + b"\r\n"


class SwordMasterCatalogTests(unittest.TestCase):
    def test_only_tooltip_changes_and_other_bytes_survive(self):
        other = b"header\r\n" + row(720841) + b"legacy=\xff\r\n"
        result = patch.rewrite(other + row())
        self.assertEqual(result, other + row(description=patch.DESCRIPTION))
        before, after = row().split(b"@@"), patch.rewrite(row()).split(b"@@")
        self.assertEqual([53], [i for i in range(66) if before[i] != after[i]])
        self.assertEqual(result, patch.rewrite(result))

    def test_both_reviewed_preimages_produce_the_same_contract(self):
        plain = patch.rewrite(row(description=patch.OLD.replace(b"+~", bytes([253]), 1)))
        encrypted = patch.transform(patch.rewrite(patch.transform(patch.transform(row(), True))), True)
        self.assertEqual(plain, patch.transform(encrypted))
        for expected in (b"Lv100+", b"own~house", b"once~daily", b"30~minutes", b"disconnecting", b"20~Study~Points", b"bound~P7"):
            self.assertIn(expected, plain)

    def test_unreviewed_or_ambiguous_sources_are_rejected(self):
        samples = (row(720841), row() + row(), row().replace(b"SwordSoul", b"OtherSoul"),
                   row().replace(b"Treasure", b"Other"), row(description=b"Changed"), row().replace(b"@@0@@", b"@@", 1))
        for sample in samples:
            with self.subTest(sample=sample[:40]), self.assertRaises(ValueError):
                patch.rewrite(sample)


if __name__ == "__main__":
    unittest.main()
