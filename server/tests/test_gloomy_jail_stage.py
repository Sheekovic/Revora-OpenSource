import importlib.util
from pathlib import Path
import struct
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("gloomy", Path(__file__).parents[1] / "tools/stage_gloomy_jail.py")
gloomy = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gloomy)


def index(rows):
    return struct.pack("<I", len(rows)) + b"".join(
        struct.pack("<II", identity, len(path)) + path + struct.pack("<I", value)
        for identity, path, value in rows)


SIBLINGS = [(1360 + n, b"map/map/maidan-" + suffix + b".7z", 256)
            for n, suffix in ((0, b"A"), (1, b"B"), (2, b"C"), (4, b"E"))]
LEGACY = [(1067, b"map/map/qiling-z.7z", 256), (1067, b"map/map/09Christmas02.7z", 128)]


def dmap():
    header = struct.pack("<II", 1004, 0) + b"map\\puzzle\\maidan-D.pul".ljust(260, b"\0") + struct.pack("<II", 2, 2)
    rows = b"".join(struct.pack("<HHH", access, 55, 2) for access in (0, 1)) + b"\0" * 4
    return header + rows + rows + struct.pack("<III", 0, 1, 4) + b"\0" * 416 + b"\0" * 4


class GloomyJailTests(unittest.TestCase):
    def test_only_appended_record_and_count_change(self):
        original = index(SIBLINGS + LEGACY)
        after = gloomy.add_map(original)
        self.assertEqual(SIBLINGS + LEGACY + [gloomy.RECORD], gloomy.index_records(after))
        self.assertEqual(original[4:], after[4:len(original)])
        self.assertEqual(7, struct.unpack_from("<I", after)[0])

    def test_existing_destination_rejected(self):
        with self.assertRaises(ValueError):
            gloomy.add_map(index(SIBLINGS + [gloomy.RECORD]))

    def test_unknown_duplicates_rejected(self):
        for rows in (SIBLINGS + [SIBLINGS[0]], SIBLINGS + LEGACY[::-1], SIBLINGS + LEGACY + [LEGACY[0]]):
            with self.subTest(rows=rows), self.assertRaises(ValueError):
                gloomy.index_records(index(rows))

    def test_changed_sibling_rejected(self):
        for rows in (SIBLINGS[:-1], SIBLINGS[:-1] + [(1364, b"map/map/maidan-E.7z", 128)]):
            with self.subTest(rows=rows), self.assertRaises(ValueError):
                gloomy.add_map(index(rows))

    def test_malformed_indexes_rejected(self):
        original = index(SIBLINGS)
        cases = [original[:n] for n in range(len(original))] + [original + b"x", struct.pack("<I", 4097),
                 index([(0, b"map/a.7z", 256)]), index([(1, b"/outside", 256)]),
                 index([(1, b"../outside", 256)]), index([(1, b"C:/outside", 256)]),
                 index([(1, b"map/./x", 256)]), index([(1, b"map/\0x", 256)]),
                 index([(1, b"x" * 261, 256)])]
        for data in cases:
            with self.subTest(size=len(data)), self.assertRaises(ValueError):
                gloomy.index_records(data)

    def test_walkability_polarity(self):
        floor = gloomy.floor_from_dmap(dmap())
        self.assertEqual(struct.pack("<II", 2, 2) + bytes([0, 1, 0, 1]), floor)

    def test_truncated_dmap_rejected(self):
        data = dmap()
        for length in range(len(data)):
            with self.subTest(length=length), self.assertRaises(ValueError):
                gloomy.floor_from_dmap(data[:length])

    def test_unreviewed_dmap_layouts_rejected(self):
        original = dmap()
        cases = []
        for offset, value, fmt in ((0, 1005, "I"), (268, 0, "I"), (272, 4097, "I"),
                                   (276, 2, "H"), (308, 1, "I"), (312, 10001, "I"), (316, 1, "I")):
            data = bytearray(original)
            struct.pack_into("<" + fmt, data, offset, value)
            cases.append(data)
        cases += [original + b"x", original[:-1] + b"x", original[:8] + b"X" + original[9:]]
        for data in cases:
            with self.subTest(data=data[:16]), self.assertRaises(ValueError):
                gloomy.floor_from_dmap(data)

    def fixture(self, root):
        client, server, extracted = root / "client", root / "server", root / "donor/map.DMap"
        sources = {client / "Env_DX9/Conquer.exe": b"synthetic, never executable",
                   client / "ini/GameMap.dat": index(SIBLINGS + LEGACY),
                   client / "map/map/maidan-D.7z": b"synthetic archive",
                   server / "database/GameMap.dat": index(SIBLINGS), extracted: dmap()}
        for path, data in sources.items():
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
        values = dict(zip(("CLIENT_EXE", "CLIENT_INDEX", "ARCHIVE", "SERVER_INDEX", "DMAP"),
                          map(gloomy.digest, sources.values())))
        floor = struct.pack("<II", 560, 560) + b"\0" * 134697 + b"\1" * 178903
        return client, server, extracted, sources, values, floor

    def test_atomic_stage_preserves_inputs_and_rejects_overwrite(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            client, server, donor, sources, values, floor = self.fixture(root)
            destination = root / "review"
            with patch.multiple(gloomy, **values), patch.object(gloomy, "floor_from_dmap", return_value=floor):
                manifest = gloomy.stage(client, server, donor, destination)
                for path, data in sources.items():
                    self.assertEqual(data, path.read_bytes())
                for output in manifest["outputs"]:
                    self.assertEqual(output["sha256"], gloomy.digest((destination / output["path"]).read_bytes()))
                with self.assertRaises(ValueError):
                    gloomy.stage(client, server, donor, destination)

    def test_source_and_destination_guards(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            client, server, donor, sources, values, floor = self.fixture(root)
            with patch.multiple(gloomy, **values), patch.object(gloomy, "floor_from_dmap", return_value=floor):
                for destination in (client / "child", server / "child", donor.parent / "child", root):
                    with self.subTest(destination=destination), self.assertRaises(ValueError):
                        gloomy.stage(client, server, donor, destination)
                for path, data in sources.items():
                    path.write_bytes(data + b"x")
                    try:
                        with self.subTest(path=path), self.assertRaises(ValueError):
                            gloomy.stage(client, server, donor, root / "rejected")
                        self.assertFalse((root / "rejected").exists())
                    finally:
                        path.write_bytes(data)

    def test_changed_input_during_staging_leaves_no_published_directory(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            client, server, donor, sources, values, floor = self.fixture(root)
            def changed_source(_):
                donor.write_bytes(sources[donor] + b"changed")
                return floor
            with patch.multiple(gloomy, **values), patch.object(gloomy, "floor_from_dmap", side_effect=changed_source):
                with self.assertRaises(ValueError):
                    gloomy.stage(client, server, donor, root / "review")
            self.assertFalse((root / "review").exists())
            self.assertEqual([], list(root.glob("gloomy-stage-*")))


if __name__ == "__main__":
    unittest.main()
