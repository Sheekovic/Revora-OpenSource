"""Resolve the owner-test journal against current one-time reward contracts.
Private delta only: no client install, signing, credentials or server launch.
"""
import hashlib
import json
from pathlib import Path, PurePosixPath

PROJECT = Path(__file__).resolve().parents[2]
WORK = PROJECT / 'obj/chi-refill-validation'
NAME = 'owner-journal-local-v1'
BASE = 'pirate-journal-composed-v1'
BASE_HASH = 'de7695d26c3bf2aeb8ba7a097141d5be19c0535c5318f54181c809a8fd9bd32b'
REWRITE_HASH = 'f848012e5ac1c19add35c6de23899003a8badac028c35322e607d840776350da'
DX9 = '3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60'

def sha(data):
    return hashlib.sha256(data).hexdigest()

def checked(path, expected):
    if path.stat().st_size > 32 * 1024 * 1024:
        raise ValueError('Oversized composition input')
    data = path.read_bytes()
    if sha(data) != expected:
        raise ValueError('Composition preimage changed: ' + str(path))
    return data

def relative(value):
    p = PurePosixPath(value)
    if not value or p.is_absolute() or str(p) != value or '..' in p.parts or ':' in value or '\\' in value:
        raise ValueError('Invalid relative artifact path')
    return p

def compose():
    checked(PROJECT.parents[1] / '6711/Env_DX9/Conquer.exe', DX9)
    checked(Path(__file__).with_name('prepare_starter_journal.py'), REWRITE_HASH)
    from prepare_starter_journal import rewrite
    manifest = json.loads(checked(WORK / BASE / 'manifest.json', BASE_HASH))
    outputs, seen, journal = [], set(), None
    for entry in manifest['outputs']:
        destination = str(relative(entry['destination']))
        source = relative(entry['source'])
        if destination in seen:
            raise ValueError('Duplicate destination')
        seen.add(destination)
        data = checked(WORK / source, entry['sha256'])
        if len(data) != entry['bytes']:
            raise ValueError('Source size changed')
        updated = dict(entry)
        if destination == 'client/ini/Questinfo.ini':
            journal = rewrite(data)
            if rewrite(journal) != journal or journal == data:
                raise ValueError('Expected stable nonempty starter correction')
            updated.update(source=NAME+'/'+destination, sha256=sha(journal), bytes=len(journal))
        outputs.append(updated)
    if len(outputs) != 10 or journal is None:
        raise ValueError('Incomplete base bundle')
    report = dict(purpose='Private owner-test journal composition', approved_dx9=DX9,
        base_manifest_sha256=BASE_HASH, rewrite_sha256=REWRITE_HASH, outputs=outputs,
        changed_missions=[90001,90002,90003],
        reward_contract='Current source grants bound P7 for new Path Hunt and one-time Courier/Soul Trial. Exclude stale cosmetic/rune-only starter-journal-v3.',
        scope='Five journal fields only; Pirate and other composed mission edits retained byte-for-byte.',
        standalone=False, installed=False, published=False, signed=False, native_acceptance=False,
        remaining=['Combine starter item/icon catalog and collection mall restoration.',
                   'Reconcile all server/client inputs and prepare the identified isolated candidate.',
                   'Validate native checklist and coordinate release/rollback.'])
    destination = WORK / NAME
    artifacts = {'client/ini/Questinfo.ini':journal,
                 'manifest.json':(json.dumps(report,indent=2)+'\n').encode()}
    # Idempotent reuse only; never overwrite a different private artifact.
    for name,data in artifacts.items():
        path=destination/name
        if path.exists() and path.read_bytes()!=data:
            raise ValueError('Existing composition differs: '+str(path))
    for name,data in artifacts.items():
        path=destination/name;path.parent.mkdir(parents=True,exist_ok=True)
        if not path.exists():
            with path.open('xb') as stream: stream.write(data)
        checked(path,sha(data))
    print(json.dumps(dict(files_referenced=len(outputs),journal_sha256=sha(journal),
                         delta_bytes=len(journal),standalone=False)))

if __name__ == '__main__':
    compose()
