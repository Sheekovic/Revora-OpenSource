"""Build one hash-pinned private owner-test data payload; never install or publish."""
import json
import sys
from pathlib import Path
from compose_owner_journal import PROJECT, WORK, DX9, checked, relative, sha

NAME = 'owner-data-local-v1'
PINS = {
    'owner-journal-local-v1/manifest.json':'ec08bf09edc437d011a03855202d8c927d1bf0a7be45661672fbecfdaa43f077',
    'revora-starter-catalog/review.json':'1982d178ffaab5aa782548ab5428f6f2e8669519d3895a10f8150cce20ab3680',
    'quest-client-bundle-v2/review.json':'f81feb8b6adb76aec7b0e0508372fbeb6465c6e5fc62e99acf812d29da9c2ef7',
}

def starter_row(data):
    rows=[row for row in data.splitlines() if row.startswith(b'727990@@')]
    if len(rows)!=1 or len(rows[0].split(b'@@'))!=66:
        raise ValueError('Expected one reviewed starter record')
    return rows[0]

def append_starter(data,row):
    if any(line.startswith(b'727990@@') for line in data.splitlines()):
        raise ValueError('Starter identity collision')
    # Reviewed encoded catalog ends in a complete record without a final newline.
    newline=b'\r\n' if b'\r\n' in data else b'\n'
    separator=b'' if data.endswith(b'\n') else newline
    result=data+separator+row+newline
    if starter_row(result)!=row or result[:len(data)]!=data:
        raise ValueError('Unrelated catalog change')
    return result

def compose():
    checked(PROJECT.parents[1]/'6711/Env_DX9/Conquer.exe',DX9)
    checked(PROJECT/'tools/stage_summer_overlay.py','377861e64311c0ebb5f9122d4937a7c5616c6c073e69714b2da9e51de0ffed0a')
    sys.path.insert(0,str(PROJECT/'tools'))
    from stage_summer_overlay import transform
    manifests={name:json.loads(checked(WORK/name,digest)) for name,digest in PINS.items()}
    outputs={}
    for entry in manifests['owner-journal-local-v1/manifest.json']['outputs']:
        name=str(relative(entry['destination']))
        if name in outputs:raise ValueError('Duplicate base destination')
        outputs[name]=checked(WORK/relative(entry['source']),entry['sha256'])
        if len(outputs[name])!=entry['bytes']:raise ValueError('Base size mismatch')
    if len(outputs)!=10:raise ValueError('Incomplete base composition')
    starter=manifests['revora-starter-catalog/review.json']
    source=WORK/'revora-starter-catalog'
    server_row=starter_row(checked(source/'server/database/itemtype-new.txt',starter['server_after']))
    client_row=starter_row(transform(checked(source/'client/ini/itemtype.dat',starter['client_after'])))
    if server_row!=client_row:raise ValueError('Starter client/server definitions differ')
    outputs['client/ini/itemtype.txt']=append_starter(outputs['client/ini/itemtype.txt'],client_row)
    plain=append_starter(transform(outputs['client/ini/itemtype.dat']),client_row)
    outputs['client/ini/itemtype.dat']=transform(plain,encode=True)
    if transform(outputs['client/ini/itemtype.dat'])!=plain:raise ValueError('Item encoding round trip failed')
    server_before=checked(PROJECT/'bin/Debug/database/itemtype-new.txt','d66c0489807d877e19053c8c04994d6738ffccf5c86565f216399f5dbfd55279')
    outputs['server/database/itemtype-new.txt']=append_starter(server_before,server_row)
    for name,hashes in starter['icons'].items():
        outputs['client/'+str(relative(name))]=checked(source/'client'/relative(name),hashes['after'])
    boss=next(row for row in manifests['quest-client-bundle-v2/review.json']['files'] if row['destination']=='ini/monster.dat')
    outputs['client/ini/monster.dat']=checked(WORK/'quest-client-bundle-v2/ini/monster.dat',boss['after'])
    collection=json.loads(checked(PROJECT/'docs/research/COLLECTION_RELEASE_SUPPLY.json','d17143136b9c9f8dc250f0aeb57ac8a3ddfb2c4d9207a3f5b10479b977718bcf'))
    for entry in collection['files']:
        group=entry['group']
        if group not in ('client','server'):raise ValueError('Unknown collection destination')
        name='client/ini/emoneyshopv2.ini' if group=='client' else 'server/database/shops/emoneyShopV2.ini'
        outputs[name]=checked(PROJECT/'obj/collection-mount-packs/staged'/group/'emoneyShopV2.ini',entry['after'])
    if len(outputs)!=16:raise ValueError('Unexpected payload count')
    report=dict(purpose='Private combined owner-test data payload',approved_dx9=DX9,input_manifest_sha256=PINS,
        files=[dict(path=name,sha256=sha(data),bytes=len(data)) for name,data in sorted(outputs.items())],
        checks=['Pinned10-file journal/terrain base retained except explicit starter catalog additions.',
                'Starter727990 identical in server/plain client/decoded client catalogs; no existing row replaced.',
                'Encoded item catalog round trip verified.',
                'Both reviewed mall catalogs and starter icon definitions pinned.',
                'Boss HUD data preserved from reviewed quest bundle.'],
        installed=False,published=False,signed=False,standalone=False,native_acceptance=False,
        remaining=['Server candidate and dependency/configuration reconciliation.',
                   'Destination preimage backups and transactional installation.',
                   'Native owner checklist and coordinated release approval.'])
    outputs['manifest.json']=(json.dumps(report,indent=2)+'\n').encode()
    destination=WORK/NAME
    for name,data in outputs.items():
        relative(name)
        path=destination/name
        if path.exists() and path.read_bytes()!=data:raise ValueError('Existing payload differs: '+name)
    for name,data in outputs.items():
        path=destination/name;path.parent.mkdir(parents=True,exist_ok=True)
        if not path.exists():
            with path.open('xb') as stream:stream.write(data)
        checked(path,sha(data))
    print(json.dumps(dict(files=len(outputs)-1,bytes=sum(len(data) for name,data in outputs.items() if name!='manifest.json'),
                         manifest_sha256=sha(outputs['manifest.json']),installed=False)))

if __name__=='__main__':compose()
