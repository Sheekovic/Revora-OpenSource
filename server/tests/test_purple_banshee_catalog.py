from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).parents[1] / "tools/ClientPatches"))
import prepare_purple_banshee_catalog as patch


def row(description=patch.OLD, broken=False):
    fields = [str(n).encode("ascii") for n in range(66)]
    fields[0], fields[1], fields[52], fields[53] = b"3004259", b"BansheeSpirit", b"Item", description
    if broken:
        fields[53:54] = [patch.OLD_HEAD, patch.OLD_TAIL]
    return b"@@".join(fields) + b"\r\n"


class PurpleBansheeCatalogTests(unittest.TestCase):
    def test_server_separator_repair_preserves_following_logical_fields(self):
        unrelated = b"header\n3001172@@AnniversaryBead@@preserve\xff\n"
        before = unrelated + row(broken=True)
        after = patch.rewrite(before, server=True)
        self.assertEqual(unrelated + row(patch.DESCRIPTION), after)
        self.assertEqual(after, patch.rewrite(after, server=True))
        old = row(broken=True).split(b"@@")
        new = row(patch.DESCRIPTION).split(b"@@")
        self.assertEqual(old[:53], new[:53])
        self.assertEqual(old[55:], new[54:])

    def test_client_changes_only_description(self):
        before = row()
        after = patch.rewrite(before)
        self.assertEqual(row(patch.DESCRIPTION), after)
        self.assertEqual([53], [n for n, (a, b) in enumerate(zip(before.split(b"@@"), after.split(b"@@"))) if a != b])
        self.assertEqual(after, patch.rewrite(after))

    def test_encrypted_round_trip_and_contract(self):
        encrypted = patch.transform(row(), True)
        result = patch.transform(patch.rewrite(patch.transform(encrypted)), True)
        self.assertEqual(row(patch.DESCRIPTION), patch.transform(result))
        for token in (b"own~house", b"Pacific~day", b"30-minute", b"disconnecting", b"200~Study", b"50~per~teammate",
                      b"60%~P5/P6", b"40%~Lv5/6", b"bound~P7", b"35,000~Honor"):
            self.assertIn(token, patch.DESCRIPTION)

    def test_unreviewed_rows_fail_closed(self):
        for server in (False, True):
            for data in (b"", row()+row(), row(b"changed"), row().replace(b"BansheeSpirit", b"Other"),
                         row().replace(b"@@Item@@", b"@@Other@@"), row(broken=True).replace(patch.OLD_TAIL, b"changed"),
                         row().replace(b"@@10@@", b"@@")):
                with self.subTest(server=server, data=data[:24]), self.assertRaises(ValueError):
                    patch.rewrite(data, server=server)
        with self.assertRaises(ValueError):
            patch.rewrite(row(broken=True))


if __name__ == "__main__":
    unittest.main()
