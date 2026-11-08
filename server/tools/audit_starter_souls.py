"""Read-only P7 catalog evidence for legacy starter quest candidate IDs."""
import argparse
import hashlib
import json
import re
from pathlib import Path

from audit_five_star_rewards import decode, rows


def audit(root):
    repo = root / 'Source/Revora-6711'
    sources = {
        'courier': repo / 'Game/Features/CourierLoopSystem.cs',
        'trial': repo / 'Game/Features/SoulTrialSystem.cs',
        'server_items': repo / 'bin/Debug/database/itemtype-new.txt',
        'client_items': root / '6711/ini/itemtype.dat',
    }
    data = {key: path.read_bytes() for key, path in sources.items()}
    pools = {key: sorted(set(map(int, re.findall(rb'Offer\(pool,\s*(\d+)\)', data[key]))))
             for key in ('courier', 'trial')}
    if not pools['courier'] or pools['courier'] != pools['trial']:
        raise ValueError('Starter candidate sets are empty or differ; review their contracts')
    server = rows(data['server_items'], b'@@')
    client = rows(decode(data['client_items']), b'@@')
    candidates = []
    for identity in pools['courier']:
        a, b = server.get(identity, []), client.get(identity, [])
        reasons = []
        if len(a) <= 55 or a[55] != b'7':
            reasons.append('server item missing, truncated or not P7')
        if len(b) <= 55 or b[55] != b'7':
            reasons.append('client item missing, truncated or not P7')
        if a[:6] != b[:6]:
            reasons.append('identity/name/catalog eligibility differ')
        candidates.append(dict(id=identity, verified=not reasons, exclusions=reasons))
    return dict(scope='Candidate identity and P7 grade only; not class exclusivity, purification use, quest earning or native acceptance.',
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
    print(json.dumps(dict(candidates=len(result['candidates']),
                         verified=sum(row['verified'] for row in result['candidates']))))
    raise SystemExit(0 if all(row['verified'] for row in result['candidates']) else 1)
