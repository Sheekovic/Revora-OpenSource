"""Compose reviewed quest data into one private bundle; no installer or public patch."""
from pathlib import Path
import argparse
import hashlib
import json
import os
import tempfile

ROOT = Path(__file__).resolve().parents[3]
WORK = ROOT / 'Source/Revora-6711/obj/chi-refill-validation'
CLIENT = ROOT / '6711'
DX9 = '3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60'
FILES = [
    ('ini/itemtype.dat', 'dis-city-ranking-overlay',
     'd49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a',
     '96f0596b854f2e3ff9df6007317c5272fb9d08329995c6dc3357d33addc86f1d'),
    ('ini/Questinfo.ini', 'dis-city-ranking-overlay',
     'f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14',
     '1cf153510ca3767b49345192c57e852e44abd122f27fd36bc9f993c84028f310'),
    ('ini/monster.dat', 'abyss-boss-hud/staged',
     '99fa2c8f14c3851d797a0b20fbd325053f9acee35c7dab4c5e73edc2f5792585',
     'df6431be254159ce83a812532b6f24330df799bfec66e1cfc2ba6436b5d6970a'),
]


def digest(data):
    return hashlib.sha256(data).hexdigest()


def checked(path, expected):
    if path.stat().st_size > 32 * 1024 * 1024:
        raise ValueError('Unexpected input size: ' + str(path))
    data = path.read_bytes()
    if digest(data) != expected:
        raise ValueError('Reviewed input changed: ' + str(path))
    return data


def compose(version=2):
    if version not in (1, 2):
        raise ValueError('Unsupported private bundle version')
    destination = WORK / ('quest-client-bundle-v%d' % version)
    files = list(FILES)
    if version == 2:
        relative, _, before, _ = files[1]
        files[1] = (relative, 'warrior-journal/staged', before,
                    '3a8fb7bc198a33291123350bfffab54bcc550cdcfc376db1537c18b3b97b74fe')
    if destination.exists():
        raise ValueError('Private bundle already exists; preserve it for review')
    checked(CLIENT / 'Env_DX9/Conquer.exe', DX9)
    inputs, rows = {}, []
    for relative, stage, before, after in files:
        checked(CLIENT / relative, before)
        inputs[relative] = checked(WORK / stage / relative, after)
        rows.append(dict(destination=relative, source_stage=stage, before=before,
                         after=after, size=len(inputs[relative])))
    provenance = {}
    sources = ['summer-overlay/review.json', 'labyrinth-overlay/review.json',
               'dis-city-ranking-overlay/review.json', 'abyss-boss-hud/manifest.json']
    if version == 2:
        sources += ['quest-client-bundle-v1/review.json', 'warrior-journal/manifest.json']
    for relative in sources:
        provenance[relative] = digest((WORK / relative).read_bytes())
    manifest = dict(purpose='Unsigned private quest client review bundle',
                    version=version,
                    approved_dx9=DX9, architecture='x86', profile='genuine-6711-DX9',
                    files=rows, provenance=provenance, installed=False, published=False,
                    native_profile_loading='verified separately; see ABYSS_TRIAL_RESTORATION.md',
                    rendered_acceptance='pending',
                    rollback='No installed files changed. A future installer must back up exact preimages and restore them transactionally.')
    temporary = Path(tempfile.mkdtemp(prefix='.quest-client-', dir=WORK)).resolve()
    created = []
    try:
        (temporary/'ini').mkdir()
        for relative, data in inputs.items():
            path = temporary/relative
            created.append(path)
            path.write_bytes(data)
        review = temporary/'review.json'
        review.write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8')
        created.append(review)
        # Revalidate source and output bytes before the directory becomes reviewable.
        checked(CLIENT/'Env_DX9/Conquer.exe', DX9)
        for relative, stage, before, after in files:
            checked(CLIENT/relative, before)
            checked(WORK/stage/relative, after)
            checked(temporary/relative, after)
        for relative, expected in provenance.items():
            checked(WORK/relative, expected)
        os.rename(str(temporary), str(destination))
    except Exception:
        # Only this invocation's known files in its owned temporary directory.
        if temporary.parent != WORK.resolve() or not temporary.name.startswith('.quest-client-'):
            raise RuntimeError('Unexpected temporary directory; preserve for review')
        for path in created:
            path.unlink()
        (temporary/'ini').rmdir()
        temporary.rmdir()
        raise
    print(json.dumps(manifest, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--version', type=int, choices=(1, 2), default=2,
                        help='2 includes Monk/Warrior navigation; 1 reconstructs the earlier prerequisite')
    compose(parser.parse_args().version)
