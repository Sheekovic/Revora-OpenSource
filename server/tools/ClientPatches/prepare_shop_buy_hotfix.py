"""Stage the data-only 1.0.9 shop workaround from the preserved 1.0.8 inputs.

The 6711 NPC buy path validates GetPrice() even for MoneyType=1. A positive
client-only price allows the request through; the server still charges CPs.
This does not remove the native client's silver affordability requirement.
"""
from pathlib import Path
import hashlib
import json
import re
import zipfile

ROOT = Path(__file__).resolve().parents[4]
PROJECT = ROOT / 'Source/Revora-6711'
PREVIOUS = PROJECT / 'obj/chi-refill-validation/subclass-shop'
WORK = PROJECT / 'obj/chi-refill-validation/shop-buy-hotfix'
sha = lambda value: hashlib.sha256(value).hexdigest()


def transform(data, encrypt=False):
    seed = 0x2537
    key = []
    for _ in range(128):
        seed = (seed * 214013 + 2531011) & 0xffffffff
        key.append((seed >> 16) & 255)
    output = bytearray(len(data))
    for index, value in enumerate(data):
        shift = index % 8
        if encrypt:
            output[index] = (((value << shift) | (value >> (8-shift))) & 255) ^ key[index % 128]
        else:
            value ^= key[index % 128]
            output[index] = ((value >> shift) | (value << (8-shift))) & 255
    return bytes(output)


def prepare():
    previous = json.loads((PREVIOUS / 'manifest.json').read_text())
    prices = {row['id']: row['cps'] for row in previous['items']}
    files = [row for row in previous['files'] if row['group'] == 'client']
    records = []
    changed = []
    for row in files:
        relative = row['relative']
        before = (PREVIOUS / 'staged/client' / relative).read_bytes()
        assert sha(before) == row['after'], relative
        after = before
        if relative == 'ini/itemtype.dat':
            plain = transform(before)
            assert transform(plain, True) == before
            lines = plain.splitlines(keepends=True)
            seen = set()
            for index, line in enumerate(lines):
                if not re.match(rb'^\d+@@', line):
                    continue
                fields = line.split(b'@@')
                item = int(fields[0])
                if item not in prices:
                    continue
                assert item not in seen and int(fields[36]) == prices[item]
                seen.add(item)
                if int(fields[11]) == 0:
                    old = fields.copy()
                    fields[11] = b'1'
                    assert all(a == b for i, (a, b) in enumerate(zip(old, fields)) if i != 11)
                    lines[index] = b'@@'.join(fields)
                    changed.append(item)
            assert seen == set(prices) and len(changed) == 26
            result = b''.join(lines)
            assert len(result) == len(plain)
            assert sum(a != b for a, b in zip(plain, result)) == 26
            after = transform(result, True)
            assert transform(after) == result
        backup = WORK / 'originals/client' / relative
        backup.parent.mkdir(parents=True, exist_ok=True)
        if backup.exists():
            assert backup.read_bytes() == before
        else:
            backup.write_bytes(before)
        staged = WORK / 'staged/client' / relative
        staged.parent.mkdir(parents=True, exist_ok=True)
        staged.write_bytes(after)
        records.append(dict(relative=relative, before=sha(before), after=sha(after)))
    archive = WORK / '1.0.9.zip'
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as output:
        for row in sorted(records, key=lambda x: x['relative']):
            info = zipfile.ZipInfo(row['relative'], (2026, 9, 13, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            output.writestr(info, (WORK / 'staged/client' / row['relative']).read_bytes())
    with zipfile.ZipFile(archive) as check:
        assert check.testzip() is None
        assert set(check.namelist()) == {row['relative'] for row in records}
        for row in records:
            assert sha(check.read(row['relative'])) == row['after']
    manifest = dict(version='1.0.9', files=records, changed_items=changed,
                    zip_sha256=sha(archive.read_bytes()), zip_size=archive.stat().st_size,
                    limitation='Native silver affordability remains: carry at least one silver per requested unit of a corrected item. Server charges CPs only.')
    (WORK / 'manifest.json').write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(manifest, indent=2))


if __name__ == '__main__':
    prepare()
