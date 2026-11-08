"""Read-only cross-catalog evidence for five-star quest reward candidates.

Writes derived identifiers/observations only, never decrypted client catalogs.
No active client, database or server configuration is changed.
"""
import argparse
import hashlib
import json
from pathlib import Path


def decode(data):
    seed, key = 0x2537, []
    for _ in range(128):
        seed = (seed * 214013 + 2531011) & 0xffffffff
        key.append((seed >> 16) & 255)
    plain = bytearray(len(data))
    for i, value in enumerate(data):
        shift = i % 8
        value ^= key[i % 128]
        plain[i] = ((value >> shift) | (value << (8 - shift))) & 255
        encoded = (((plain[i] << shift) | (plain[i] >> (8 - shift))) & 255) ^ key[i % 128]
        if encoded != data[i]:
            raise ValueError('Catalog round trip failed')
    return bytes(plain)


def rows(data, delimiter):
    result = {}
    for line in data.splitlines():
        fields = line.split(delimiter) if delimiter else line.split()
        if not fields or not fields[0].isdigit():
            continue
        identity = int(fields[0])
        if identity in result:
            raise ValueError('Duplicate catalog identity: %d' % identity)
        result[identity] = fields
    return result


def audit(root):
    sources = {
        'server_rarity': root / 'Source/Revora-6711/bin/Debug/database/coat_storage_type.txt',
        'server_items': root / 'Source/Revora-6711/bin/Debug/database/itemtype-new.txt',
        'client_rarity': root / '6711/ini/coat_storage_type.dat',
        'client_items': root / '6711/ini/itemtype.dat',
    }
    data = {key: path.read_bytes() for key, path in sources.items()}
    rarity = rows(data['server_rarity'], None)
    client_rarity = rows(decode(data['client_rarity']), b'@@')
    items = rows(data['server_items'], b'@@')
    client_items = rows(decode(data['client_items']), b'@@')
    candidates = []
    for identity, fields in sorted(rarity.items()):
        if len(fields) < 8:
            raise ValueError('Truncated rarity row')
        if fields[7] != b'5':
            continue
        reasons = []
        other = client_rarity.get(identity, [])
        if len(other) < 8 or other[7] != b'5' or other[2] != fields[2]:
            reasons.append('client rarity/type missing or different')
        server, client = items.get(identity), client_items.get(identity)
        if server is None:
            reasons.append('missing loaded server item')
        if client is None:
            reasons.append('missing client item')
        if server is not None and client is not None and server[:6] != client[:6]:
            reasons.append('item identity/name/eligibility differs')
        if fields[2] not in (b'1', b'2'):
            reasons.append('unknown cosmetic category')
        candidates.append(dict(id=identity, category='garment' if fields[2] == b'1' else 'mount',
                               stars=5, catalog_verified=not reasons, exclusions=reasons))
    return dict(scope='Catalog identities only; not native rendering, trade, permanence or earned-claim acceptance.',
                hashes={key: hashlib.sha256(value).hexdigest() for key, value in data.items()},
                candidates=candidates)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[3])
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    result = audit(args.root)
    if args.output:
        args.output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    accepted = [entry for entry in result['candidates'] if entry['catalog_verified']]
    print(json.dumps(dict(candidates=len(result['candidates']), verified=len(accepted),
                          garment_ids=[entry['id'] for entry in accepted if entry['category'] == 'garment'],
                          mount_ids=[entry['id'] for entry in accepted if entry['category'] == 'mount']), indent=2))
