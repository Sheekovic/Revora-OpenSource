from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).parents[1] / "tools/ClientPatches"))
import prepare_anniversary_weekend_catalog as patch


def row(description=patch.OLD):
    fields = [b"0"] * 66
    fields[0], fields[1], fields[52], fields[53] = b"3001172", b"AnniversaryBead", b"RevoraEvent", description
    return b"@@".join(fields) + b"\r\n"


class AnniversaryWeekendCatalogTests(unittest.TestCase):
    def test_only_description_changes_with_all_other_bytes_preserved(self):
        unrelated = b"header\r\n720842@@SwordSoul@@preserve\xff\r\n"
        before = unrelated + row()
        after = patch.rewrite(before)
        self.assertEqual(unrelated + row(patch.DESCRIPTION), after)
        self.assertEqual(after, patch.rewrite(after))
        self.assertEqual([53], [n for n, (a, b) in enumerate(zip(row().split(b"@@"), row(patch.DESCRIPTION).split(b"@@"))) if a != b])

    def test_encrypted_round_trip_and_player_contract(self):
        source = patch.transform(row(), True)
        candidate = patch.transform(patch.rewrite(patch.transform(source)), True)
        self.assertEqual(row(patch.DESCRIPTION), patch.transform(candidate))
        for text in (b"Lv140", b"twice~reborn", b"VIP6+", b"(323,307)", b"Sat-Sun~Pacific",
                     b"15,000~spirits", b"bound~P7", b"finish~any~day", b"daily~Spirit~Ball"):
            self.assertIn(text, patch.DESCRIPTION)

    def test_missing_ambiguous_and_changed_catalogs_are_rejected(self):
        for sample in (b"", row()+row(), row(b"unexpected"), row().replace(b"AnniversaryBead", b"Other"),
                       row().replace(b"RevoraEvent", b"Other"), row().replace(b"@@0@@", b"@@", 1)):
            with self.subTest(sample=sample[:30]), self.assertRaises(ValueError):
                patch.rewrite(sample)


if __name__ == "__main__":
    unittest.main()
