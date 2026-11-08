"""Stage the native P6 shop against the owner's current client; preserve exact originals.
Does not publish a patch, modify the donor, or restart a server.
"""
from pathlib import Path
import hashlib
import json
import re
import zipfile
from prepare_shop_buy_hotfix import transform

PROJECT = Path(__file__).resolve().parents[2]
CLIENT = PROJECT.parents[1] / '6711'
WORK = PROJECT / 'obj/chi-refill-validation/p6-shop'
PRICE = 2500
UID = 91010
sha = lambda data: hashlib.sha256(data).hexdigest()


def prepare():
    source = (PROJECT / 'Game/Features/P6SoulShop.cs').read_text(encoding='utf-8')
    assert int(re.search(r'Price = (\d+);', source)[1]) == PRICE, 'Server/client price mismatch'
    stock = [int(x) for x in re.search(r'Items = \{(.*?)\};', source, re.S)[1].replace('\n', '').split(',')]
    assert len(stock) == len(set(stock)) == 27
    encrypted = (CLIENT / 'ini/itemtype.dat').read_bytes()
    plain = transform(encrypted)
    assert transform(plain, True) == encrypted
    lines = plain.splitlines(keepends=True)
    found = {}
    for index, line in enumerate(lines):
        sep = b'@@' if b'@@' in line else b' '
        fields = line.split(sep)
        if not fields[0].isdigit() or int(fields[0]) not in stock:
            continue
        item = int(fields[0])
        assert item not in found and len(fields) > 55 and int(fields[55]) == 6
        found[item] = fields[1].decode('ascii')
        fields[36] = str(PRICE).encode('ascii')
        # Same positive-price requirement verified for the existing native CP shops.
        fields[11] = b'1'
        lines[index] = sep.join(fields)
    assert set(found) == set(stock)
    catalog = transform(b''.join(lines), True)
    shops = (CLIENT / 'ini/Shop.dat').read_bytes()
    assert not re.search(rb'^ID=91010\s*$', shops, re.M)
    number = max(int(x) for x in re.findall(rb'^\[Shop(\d+)\]', shops, re.M)) + 1
    shops, count = re.subn(rb'^Amount=\d+', b'Amount=' + str(number+1).encode(), shops, count=1, flags=re.M)
    assert count == 1
    section = '\r\n[Shop%d]\r\nID=91010\r\nName=P6SoulMerchant\r\nType=1\r\nMoneyType=1\r\nItemAmount=27\r\n' % number
    section += ''.join('Item%d=%d\r\n' % (i, item) for i, item in enumerate(stock))
    shops += section.encode('ascii')
    npcs = (CLIENT / 'ini/npc.ini').read_bytes()
    assert b'[NpcType4609]' not in npcs
    template = re.search(rb'\[NpcType1\]\r?\n.*?(?=\r?\n\[)', npcs, re.S)[0]
    entry = template.replace(b'[NpcType1]', b'[NpcType4609]').replace(b'Name=Storekeeper', b'Name=P6SoulMerchant').replace(b'Note=[Store]', b'Note=[P6~Dragon~Souls]')
    npcs += b'\r\n' + entry + b'\r\n'
    records = []
    for relative, data in [('ini/itemtype.dat', catalog), ('ini/Shop.dat', shops), ('ini/npc.ini', npcs)]:
        original = (CLIENT / relative).read_bytes()
        backup = WORK / 'originals' / relative
        backup.parent.mkdir(parents=True, exist_ok=True)
        if backup.exists():
            assert backup.read_bytes() == original, 'Source changed since staging: ' + relative
        else:
            backup.write_bytes(original)
        staged = WORK / 'client' / relative
        staged.parent.mkdir(parents=True, exist_ok=True)
        staged.write_bytes(data)
        records.append(dict(path=relative, before=sha(original), after=sha(data)))
    archive = WORK / 'p6-shop-client.zip'
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as output:
        for row in records:
            info = zipfile.ZipInfo(row['path'], (2026, 9, 22, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            output.writestr(info, (WORK/'client'/row['path']).read_bytes())
    with zipfile.ZipFile(archive) as check:
        assert check.testzip() is None
        for row in records:
            assert sha(check.read(row['path'])) == row['after']
    manifest = dict(npc=UID, map=1002, x=401, y=339, mesh=46090, price=PRICE,
                    items=[dict(id=i, name=found[i]) for i in stock], files=records,
                    archive_sha256=sha(archive.read_bytes()),
                    native_limitation='Carry one silver per requested unit; the server deducts CPs only.')
    (WORK/'manifest.json').write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8')
    print(f'Staged 27 P6 souls at {PRICE:,} CPs; exact originals and SHA-256 manifest preserved.')
    print(archive)


if __name__ == '__main__':
    prepare()
