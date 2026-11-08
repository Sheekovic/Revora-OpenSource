"""Fix 6711 monster section names: the native parser requires ~ for spaces."""
from pathlib import Path
import hashlib
import json
import re
import zipfile
from prepare_shop_buy_hotfix import transform
from prepare_daily_boss_patch import PROFILES

ROOT = Path(__file__).resolve().parents[4]
WORK = ROOT / 'Source/Revora-6711/obj/chi-refill-validation/boss-hud-space'
EXPECTED = 'bc40ae4d515ffc3475da17a6630e1155ef59736116cb6bc40185178e5c2f68c0'
sha = lambda data: hashlib.sha256(data).hexdigest()


def prepare():
    before = (ROOT / '6711/ini/monster.dat').read_bytes()
    assert sha(before) == EXPECTED, 'Unexpected monster catalog preimage'
    exe_hash = sha((ROOT / '6711/Env_DX9/Conquer.exe').read_bytes())
    assert exe_hash == '3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60'
    plain = transform(before)
    assert transform(plain, True) == before
    updated = plain
    for name, type_id, hp in PROFILES:
        old = ('[' + name + ']').encode()
        new = ('[' + name.replace(' ', '~') + ']').encode()
        assert updated.count(old) == 1 and new not in updated
        updated = updated.replace(old, new, 1)
        rows = re.findall(rb'^' + re.escape(new) + rb'\r?$[\s\S]*?(?=^\[|\Z)', updated, re.M)
        assert len(rows) == 1
        for key, value in [('Boss', 1), ('MaxLife', hp), ('TypeID', type_id)]:
            assert re.search(rb'^' + key.encode() + rb'=0*' + str(value).encode() + rb'\r?$', rows[0], re.M)
    # Only nine section headers change. Preserve profiles, order, and all other bytes.
    restored = updated
    for name, _, _ in PROFILES:
        restored = restored.replace(('[' + name.replace(' ', '~') + ']').encode(), ('[' + name + ']').encode(), 1)
    assert restored == plain
    headers = re.findall(rb'^\[([^\r\n]+)\]', updated, re.M)
    assert all(not any(chr(c).isspace() for c in header) for header in headers)
    after = transform(updated, True)
    assert transform(after) == updated
    original = WORK / 'originals/ini/monster.dat'
    staged = WORK / 'staged/ini/monster.dat'
    original.parent.mkdir(parents=True, exist_ok=True)
    staged.parent.mkdir(parents=True, exist_ok=True)
    if original.exists():
        assert original.read_bytes() == before
    original.write_bytes(before)
    staged.write_bytes(after)
    archive = WORK / '1.0.13.zip'
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        info = zipfile.ZipInfo('ini/monster.dat', (2026, 9, 14, 0, 0, 0))
        info.compress_type = zipfile.ZIP_DEFLATED
        z.writestr(info, after)
    with zipfile.ZipFile(archive) as z:
        assert z.namelist() == ['ini/monster.dat'] and z.testzip() is None and z.read('ini/monster.dat') == after
    manifest = dict(version='1.0.13', before=sha(before), after=sha(after),
                    zip_sha256=sha(archive.read_bytes()), zip_size=archive.stat().st_size,
                    approved_dx9=exe_hash, corrected_names=[p[0] for p in PROFILES])
    (WORK / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print(json.dumps(manifest, indent=2))


if __name__ == '__main__':
    prepare()
