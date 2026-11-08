"""Stage the 17 existing collection packs in both V2 mall catalogs; never deploy."""
import hashlib
import json
import re
import sys
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[2]
ROOT = PROJECT.parents[1]
sys.path.insert(0, str(PROJECT / 'tools'))
from audit_five_star_rewards import decode, rows

WORK = PROJECT / 'obj/collection-mount-packs'
BLOCK = rb'(?ms)^ID=2888\r?\n.*?(?=^ID=|\Z)'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def extend(data, packs):
    matches = list(re.finditer(BLOCK, data))
    if len(matches) != 1:
        raise ValueError('Expected one V2 mall block')
    block = matches[0][0]
    if not re.search(rb'(?m)^MoneyType=1\r?$', block):
        raise ValueError('Mall must use CPs')
    present = set(map(int, re.findall(rb'(?m)^(\d+)\s', block)))
    added = sorted(set(packs) - present)
    lines = b''.join(
        (str(i) + ' item Permanent~mount~armor~pack.~2799~CPs.\r\n').encode()
        for i in added)
    if block.count(b'[recommend]') != 1:
        raise ValueError('Unreviewed mall section layout')
    changed = block.replace(b'[recommend]', lines + b'[recommend]', 1)
    after = data[:matches[0].start()] + changed + data[matches[0].end():]
    assert re.sub(BLOCK, b'', data) == re.sub(BLOCK, b'', after)
    assert set(map(int, re.findall(rb'(?m)^(\d+)\s', changed))) == present | set(packs)
    return after, added


def prepare():
    db = PROJECT / 'bin/Debug/database'
    source = (PROJECT / 'Game/Features/FixedOutputPacks.cs').read_text(encoding='utf-8-sig')
    body = source.split('internal static uint MountFor(uint id)', 1)[1].split('default:return 0;', 1)[0]
    packs = {int(a): int(b) for a, b in re.findall(r'case (\d+):return (\d+);', body)}
    if len(packs) != 17 or len(set(packs.values())) != 17:
        raise ValueError('Review changed fixed-output pack mapping')
    server = rows((db / 'itemtype-new.txt').read_bytes(), b'@@')
    server.update(rows((db / 'ItemtypeSub.txt').read_bytes(), b'@@'))
    client_bytes = (ROOT / '6711/ini/itemtype.dat').read_bytes()
    client = rows(decode(client_bytes), b'@@')
    for pack, mount in packs.items():
        for catalog in (server, client):
            if pack not in catalog or mount not in catalog:
                raise ValueError('Missing pack/output item')
            if int(catalog[pack][36]) != 2799 or int(catalog[pack][38]) != 0:
                raise ValueError('Existing permanent pack price changed')
    records = []
    for group, path in (
        ('server', db / 'shops/emoneyShopV2.ini'),
        ('client', ROOT / '6711/ini/emoneyshopv2.ini'),
    ):
        before = path.read_bytes()
        after, added = extend(before, packs)
        for folder, content in (('originals', before), ('staged', after)):
            target = WORK / folder / group / 'emoneyShopV2.ini'
            target.parent.mkdir(parents=True, exist_ok=True)
            if target.exists() and target.read_bytes() != content:
                raise ValueError('Existing staging artifacts differ; review before replacing')
            target.write_bytes(content)
        assert extend(after, packs) == (after, [])
        records.append(dict(group=group, source=str(path), before=sha(before), after=sha(after), added=added))
        assert path.read_bytes() == before
    manifest = dict(scope='Private coordinated catalog staging only', price_cps=2799,
                    packs=packs, files=records, client_item_catalog_sha256=sha(client_bytes),
                    native_acceptance=False, deployed=False, published=False)
    (WORK / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print(json.dumps(dict(packs=len(packs), price_cps=2799,
                         added={r['group']:len(r['added']) for r in records}, deployed=False)))


if __name__ == '__main__':
    prepare()
