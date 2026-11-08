"""Privately stage exact-name Abyss HUD profiles; never modify an active client."""
from pathlib import Path
import hashlib
import json
import re
from prepare_shop_buy_hotfix import transform

ROOT = Path(__file__).resolve().parents[4]
PROJECT = ROOT / 'Source/Revora-6711'
WORK = PROJECT / 'obj/chi-refill-validation/abyss-boss-hud'
PREIMAGE = '99fa2c8f14c3851d797a0b20fbd325053f9acee35c7dab4c5e73edc2f5792585'
DX9 = '3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60'
TEMPLATES = ['HillSpirit', 'SwiftDevil', 'Banshee', 'CleansingDevil', 'Andrew',
             'Peter', 'Philip', 'Timothy', 'Daphne', 'Victoria', 'Wayne', 'Theodore',
             'Satan', 'BeastSatan', 'FurySatan']
BASE_HEALTH = [50000, 55000, 60460] + [65535] * 12


def sha(data):
    return hashlib.sha256(data).hexdigest()


def section(data, name):
    key = name.replace(' ', '~').encode('ascii')
    rows = re.findall(rb'^\[' + re.escape(key) + rb'\]\r?$[\s\S]*?(?=^\[|\Z)', data, re.M)
    if len(rows) != 1:
        raise ValueError('Expected exactly one profile: ' + name)
    return rows[0]


def field(row, key):
    values = re.findall(rb'^' + key.encode() + rb'=(\d+)\r?$', row, re.M)
    if len(values) != 1:
        raise ValueError('Ambiguous profile field: ' + key)
    return int(values[0])


def profiles():
    source = (PROJECT / 'Game/Features/AbyssTrialWorld.cs').read_text(encoding='utf-8-sig')
    names = re.findall(r'"([^"]+)"', re.search(r'HudNames\s*=\s*\{([^}]+)\}', source).group(1))
    health = [int(n) for n in re.findall(r'\d+', re.search(r'HudHealth\s*=\s*\{([^}]+)\}', source).group(1))]
    if len(names) != 15 or len(health) != 15 or len(set(names)) != 15:
        raise ValueError('Server HUD profile count or identity changed')
    if any(not 0 < hp <= 2147483647 for hp in health) or any(not 0 < len(n.encode('ascii')) <= 16 for n in names):
        raise ValueError('Invalid server profile name or health')
    return list(zip(names, health))


def prepare():
    before = (ROOT / '6711/ini/monster.dat').read_bytes()
    if sha(before) != PREIMAGE or sha((ROOT / '6711/Env_DX9/Conquer.exe').read_bytes()) != DX9:
        raise ValueError('Approved client preimage changed')
    plain = transform(before)
    if transform(plain, True) != before:
        raise ValueError('Catalog codec round trip failed')
    updated = plain
    records = []
    for i, (name, hp) in enumerate(profiles()):
        old = section(plain, TEMPLATES[i])
        if field(old, 'TypeID') != 3632+i or field(old, 'MaxLife') != BASE_HEALTH[i] or field(old, 'Boss') != 0:
            raise ValueError('Legacy template changed: ' + TEMPLATES[i])
        header = ('[' + name.replace(' ', '~') + ']').encode('ascii')
        if header in plain or ('[' + name + ']').encode('ascii') in plain:
            raise ValueError('Profile alias already exists: ' + name)
        alias = old.replace(('[' + TEMPLATES[i] + ']').encode(), header, 1)
        for key, value in [('MaxLife', hp), ('Boss', 1), ('Level', 120)]:
            alias, count = re.subn(rb'^' + key.encode() + rb'=\d+\r?$', key.encode()+b'='+str(value).encode()+b'\r', alias, flags=re.M)
            if count != 1:
                raise ValueError('Profile field replacement failed')
        updated += b'\r\n' + alias
        if section(updated, name) != alias or section(updated, TEMPLATES[i]) != old:
            raise ValueError('Profile preservation failed')
        records.append(dict(name=name, template=TEMPLATES[i], type_id=3632+i, max_life=hp, boss=1))
    after = transform(updated, True)
    if updated[:len(plain)] != plain or transform(after) != updated:
        raise ValueError('Catalog preservation or codec failed')
    for folder, content in [('originals', before), ('staged', after)]:
        path = WORK / folder / 'ini/monster.dat'
        path.parent.mkdir(parents=True, exist_ok=True)
        if path.exists() and path.read_bytes() != content:
            raise ValueError('Existing private artifact differs: ' + str(path))
        path.write_bytes(content)
    manifest = dict(purpose='private-abyss-hud-validation', relative='ini/monster.dat',
                    before=sha(before), after=sha(after), approved_dx9=DX9,
                    preserves_original_catalog=True, requires_restart=True,
                    installed=False, published=False, profiles=records)
    (WORK/'manifest.json').write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(manifest, indent=2))


if __name__ == '__main__':
    prepare()
