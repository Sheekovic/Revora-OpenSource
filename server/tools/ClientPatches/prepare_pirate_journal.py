"""Privately stage mission 6868 navigation; never install into an active client."""
from pathlib import Path
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[4]
WORK = ROOT / 'Source/Revora-6711/obj/chi-refill-validation/pirate-journal'
PREIMAGE = 'f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14'
DX9 = '3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def rewrite(before):
    sections = re.findall(rb'^\[\d+\]\r?$[\s\S]*?(?=^\[|\Z)', before, re.M)
    matches = [row for row in sections if re.search(rb'^MissionId=6868\r?$', row, re.M)]
    if len(matches) != 1:
        raise ValueError('Expected exactly one Pirate Epic mission')
    original = matches[0]
    changed = original
    for key in (b'BeginNpcId', b'FinishNpcId'):
        old = key + b'=9391,1004,58,34,PirateLord,JobCenter'
        new = key + b'=4272,1004,72,35,PirateLord,JobCenter'
        if len(re.findall(rb'^' + key + rb'=', original, re.M)) != 1 or original.count(old) != 1:
            raise ValueError('Pirate navigation preimage changed')
        changed = changed.replace(old, new, 1)
    after = before.replace(original, changed, 1)
    if after.replace(changed, original, 1) != before:
        raise ValueError('Unrelated quest content changed')
    return after


def prepare():
    before = (ROOT / '6711/ini/Questinfo.ini').read_bytes()
    if sha(before) != PREIMAGE or sha((ROOT / '6711/Env_DX9/Conquer.exe').read_bytes()) != DX9:
        raise ValueError('Approved client preimage changed')
    after = rewrite(before)
    # Check every existing artifact before writing any of this bounded stage.
    files = [(WORK / folder / 'ini/Questinfo.ini', data)
             for folder, data in [('originals', before), ('staged', after)]]
    manifest = dict(purpose='private-pirate-journal-validation', relative='ini/Questinfo.ini',
                    before=sha(before), after=sha(after), approved_dx9=DX9,
                    mission=6868, npc=4272, map=1004, x=72, y=35,
                    installed=False, published=False, signed=False,
                    native_acceptance=False, composition_pending=True,
                    rollback='Restore originals/ini/Questinfo.ini after verifying its recorded hash.')
    files.append((WORK / 'manifest.json', (json.dumps(manifest, indent=2) + '\n').encode()))
    for path, data in files:
        if path.exists() and path.read_bytes() != data:
            raise ValueError('Existing private artifact differs: ' + str(path))
    for path, data in files:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    if (ROOT / '6711/ini/Questinfo.ini').read_bytes() != before:
        raise ValueError('Active catalog changed during private staging')
    print(json.dumps(manifest, indent=2))


if __name__ == '__main__':
    prepare()
