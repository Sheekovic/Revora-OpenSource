"""Stage the exact-name HorrifyingSpecter client profile; preserve existing profiles."""
from pathlib import Path
import hashlib
import json
import re
import zipfile
from prepare_shop_buy_hotfix import transform

ROOT = Path(__file__).resolve().parents[4]
WORK = ROOT / 'Source/Revora-6711/obj/chi-refill-validation/boss-hud'
CLIENT = ROOT / '6711'
EXPECTED = 'c9e7616d4b3888d508f15c41972c084d0403db558145ad5b95d0d59079f60178'
sha = lambda data: hashlib.sha256(data).hexdigest()


def section(data, name):
    matches = list(re.finditer(rb'^\[' + re.escape(name) + rb'\]\r?$[\s\S]*?(?=^\[|\Z)', data, re.M))
    if len(matches) != 1:
        raise ValueError('Expected exactly one profile: ' + name.decode())
    return matches[0].group()


def prepare():
    before = (CLIENT / 'ini/monster.dat').read_bytes()
    if sha(before) != EXPECTED:
        raise ValueError('Client monster catalog preimage changed')
    exe_hash = sha((CLIENT / 'Env_DX9/Conquer.exe').read_bytes())
    if exe_hash != '3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60':
        raise ValueError('Unexpected DX9 executable')
    plain = transform(before)
    assert transform(plain, True) == before
    assert b'[HorrifyingSpecter]' not in plain
    original = section(plain, b'CreepySpook')
    assert re.search(rb'^TypeID=2719\r?$', original, re.M)
    assert re.search(rb'^Boss=1\r?$', original, re.M)
    alias = original.replace(b'[CreepySpook]', b'[HorrifyingSpecter]', 1)
    alias, count = re.subn(rb'^MaxLife=55000000\r?$', b'MaxLife=5000000\r', alias, count=1, flags=re.M)
    assert count == 1
    updated = plain + b'\r\n' + alias
    after = transform(updated, True)
    assert transform(after) == updated and updated[:len(plain)] == plain
    assert section(updated, b'CreepySpook') == original
    assert section(updated, b'HorrifyingSpecter') == alias
    assert re.search(rb'^MaxLife=5000000\r?$', alias, re.M)
    originals = WORK / 'originals/ini/monster.dat'
    staged = WORK / 'staged/ini/monster.dat'
    originals.parent.mkdir(parents=True, exist_ok=True)
    staged.parent.mkdir(parents=True, exist_ok=True)
    if originals.exists() and originals.read_bytes() != before:
        raise ValueError('Existing backup changed')
    originals.write_bytes(before)
    staged.write_bytes(after)
    archive = WORK / '1.0.11.zip'
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        info = zipfile.ZipInfo('ini/monster.dat', (2026, 9, 14, 0, 0, 0))
        info.compress_type = zipfile.ZIP_DEFLATED
        z.writestr(info, after)
    with zipfile.ZipFile(archive) as z:
        assert z.namelist() == ['ini/monster.dat'] and z.testzip() is None
        assert z.read('ini/monster.dat') == after
    manifest = dict(version='1.0.11', relative='ini/monster.dat', before=sha(before), after=sha(after),
                    zip_sha256=sha(archive.read_bytes()), zip_size=archive.stat().st_size,
                    profile='HorrifyingSpecter', template='CreepySpook', type_id=2719,
                    max_life=5000000, boss=1, approved_dx9=exe_hash,
                    preserved_existing_profiles=True, requires_client_restart=True)
    (WORK / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(manifest, indent=2))


if __name__ == '__main__':
    prepare()
