from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).parents[1] / "tools/ClientPatches"))
import prepare_anniversary_catalog as patch


def row(identity=3001172):
    values = [b"0"] * 66
    values[0] = str(identity).encode(); values[1] = b"VirtuePack(A)"
    values[52] = b"Father`sDay"; values[53] = b"Old~description"
    return b"@@".join(values) + b"\r\n"


class AnniversaryCatalogTests(unittest.TestCase):
    def test_exact_scope_preserves_other_items_and_fields(self):
        original = b"header\r\n" + row(3006048) + row() + b"unrelated=\xff\r\n"
        changed = patch.rewrite(original)
        self.assertEqual(original.count(b"\r\n"), changed.count(b"\r\n"))
        self.assertTrue(changed.startswith(b"header\r\n" + row(3006048)))
        self.assertTrue(changed.endswith(b"unrelated=\xff\r\n"))
        old, new = row().split(b"@@"), patch.rewrite(row()).split(b"@@")
        self.assertEqual([1, 52, 53], [i for i in range(66) if old[i] != new[i]])
        self.assertIn(b"bound~P7", changed)
        self.assertEqual(changed, patch.rewrite(changed))

    def test_rejects_missing_duplicate_malformed_or_unknown_identity(self):
        for value in (row(3006048), row() + row(), row().replace(b"@@0@@", b"@@", 1), row().replace(b"VirtuePack(A)", b"Unknown")):
            with self.assertRaises(ValueError):
                patch.rewrite(value)

    def test_encrypted_client_and_plain_server_have_matching_edits(self):
        original = row(3006048) + row()
        encrypted = patch.transform(original, True)
        server = patch.rewrite(original)
        client = patch.transform(patch.rewrite(patch.transform(encrypted)), True)
        self.assertEqual(server, patch.transform(client))


if __name__ == "__main__":
    unittest.main()
