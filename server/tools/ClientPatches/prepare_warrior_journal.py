"""Stage the seven Warrior journal entries over prior quest and Monk fixes.

No active client files are changed. The output remains an unsigned private
review overlay, not a public release or native-acceptance claim.
"""
from pathlib import Path
import hashlib
import json
import re
from prepare_monk_journal import rewrite as monk_rewrite

ROOT = Path(__file__).resolve().parents[4]
WORK = ROOT / 'Source/Revora-6711/obj/chi-refill-validation'
INSTALLED = 'f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14'
BUNDLE = '1cf153510ca3767b49345192c57e852e44abd122f27fd36bc9f993c84028f310'
DX9 = '3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60'
MASTER = '10001,1004,42,74,WarriorGod,JobCenter'
MEMORY = '19253,10009,42,40,Chest,WeirdFormation'
STELE = '19256,10010,83,86,SealingStele,MiracleRelics'
CHEST = '19254,10011,41,39,Chest,MiracleSign'
EVIL = '19257,10011,49,47,TinyMindofEvil,MiracleSign'
SPIRIT = '19259,10011,39,44,Miracle`sSpirit,MiracleSign'
ROWS = {
    3678: (1004, MASTER, MASTER, 'Speak with [npc WarriorGod,10001,1004,42,74] and read the ancient book.',
           'Warrior God holds an ancient book about a pair of sealed fists. Ask him to reveal its story.'),
    3679: (1004, MASTER, MEMORY, 'Use your Memory Jade Slips in the Job Center.',
           'The jade slips carry an ancient memory. Use them in the Job Center to follow it.'),
    3695: (10009, MEMORY, MEMORY, 'Examine the [npc Chest,19253,10009,42,40].',
           'The memory reveals a chest beyond a powerful seal. Examine it and follow the lingering force.'),
    3680: (10010, MEMORY, STELE, 'Destroy the [mob SealingStele,10010,83,86].',
           'Break the Sealing Stele to reach the hidden cave. The Guarding Stele near the entrance can return you to your master.'),
    3681: (10011, STELE, CHEST, 'Open the [npc Chest,19254,10011,41,39].',
           'The seal is broken. Search the chest in the hidden cave, but beware the darkness within.'),
    3682: (10011, CHEST, EVIL, 'Defeat the [mob TinyMindofEvil,10011,49,47].',
           'A dark presence has risen from the chest. Defeat it to complete the trial.'),
    3683: (10011, EVIL, SPIRIT, 'Speak with [npc Miracle`sSpirit,19259,10011,39,44].',
           'The darkness is vanquished. Speak with the Spirit and leave two spaces in your bag for the paired fists.'),
}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def checked(path, expected):
    if path.stat().st_size > 32 * 1024 * 1024:
        raise ValueError('Unexpected input size')
    data = path.read_bytes()
    if sha(data) != expected:
        raise ValueError('Reviewed preimage changed: ' + str(path))
    return data


def rewrite(before, valuable=False):
    matches = list(re.finditer(rb'^\[\d+\]\r?$[\s\S]*?(?=^\[|\Z)', before, re.M))
    edits, seen = [], set()
    for match in matches:
        original = match.group()
        mission = re.findall(rb'^MissionId=(\d+)\r?$', original, re.M)
        if len(mission) != 1 and any(int(value) in ROWS for value in mission):
            raise ValueError('Ambiguous Warrior mission identity')
        if len(mission) != 1 or int(mission[0]) not in ROWS:
            continue
        identity = int(mission[0])
        if identity in seen:
            raise ValueError('Duplicate Warrior mission')
        seen.add(identity)
        world, begin, finish, intention, content = ROWS[identity]
        changes = {'Map': str(world), 'BeginNpcId': begin, 'FinishNpcId': finish,
                   'IntentionDesp': re.sub(r'\[(?:npc|mob) ([^,]+),[^\]]+\]', r'\1', intention),
                   'Intention1': intention + '[chk_quest %d,1,1,1]' % identity,
                   'Content': content}
        if valuable:
            changes['Prize'] = ('Complete the trial to earn two unbound level-140 EvilSlayer fists: '
                                '+12, -7 damage reduction, two empty sockets. Earlier accepted trials retain their promised reward.')
        changed = original
        for key, value in changes.items():
            expression = rb'^' + key.encode() + rb'=([^\r\n]*)(\r?)(?=\n|\Z)'
            if len(re.findall(expression, original, re.M)) != 1:
                raise ValueError('Missing or duplicate selected field: ' + key)
            changed, count = re.subn(expression, lambda m: key.encode() + b'=' + value.encode('ascii') + m[2], changed, flags=re.M)
            if count != 1:
                raise ValueError('Unexpected replacement count')
        edits.append((match.start(), match.end(), original, changed))
    if seen != set(ROWS):
        raise ValueError('Missing Warrior mission')
    after = before
    for start, end, original, changed in reversed(edits):
        after = after[:start] + changed + after[end:]
    # Restore exactly the changed sections and require byte equality elsewhere.
    restored = after
    for _, _, original, changed in edits:
        if restored.count(changed) != 1:
            raise ValueError('Ambiguous rewritten section')
        restored = restored.replace(changed, original, 1)
    if restored != before:
        raise ValueError('Unrelated journal data changed')
    return after


def prepare():
    installed_path = ROOT / '6711/ini/Questinfo.ini'
    executable = ROOT / '6711/Env_DX9/Conquer.exe'
    source = WORK / 'quest-client-bundle-v1/ini/Questinfo.ini'
    installed = checked(installed_path, INSTALLED)
    checked(executable, DX9)
    bundle = checked(source, BUNDLE)
    intermediate = monk_rewrite(bundle)
    after = rewrite(intermediate, valuable=True)
    if rewrite(after, valuable=True) != after:
        raise ValueError('Warrior rewrite is not idempotent')
    manifest = dict(purpose='private-composed-warrior-max-reward-journal-review', relative='ini/Questinfo.ini',
                    before=INSTALLED, source_bundle=BUNDLE, monk_intermediate=sha(intermediate), after=sha(after),
                    approved_dx9=DX9, architecture='x86', missions=list(ROWS),
                    reward='New trials: two 624439 fists, level 140, +12, bless 7, socket bytes 255/255, unbound; old contracts unchanged.',
                    preserved='Prior quest bundle edits plus mission 3526 Monk navigation; all unrelated bytes preserved.',
                    installed=False, published=False, signed=False, native_acceptance='pending',
                    limits='Combat metadata IDs 19256/19257 are catalog references, not allocated server monster UIDs. Native link resolution in private maps must be reviewed.',
                    rollback='Do not install independently of the matching server. Future deployment must back up and verify the installed preimage; restore originals/ini/Questinfo.ini transactionally.')
    destination = WORK / 'warrior-max-reward-journal'
    files = [(destination / 'originals/ini/Questinfo.ini', installed),
             (destination / 'staged/ini/Questinfo.ini', after),
             (destination / 'manifest.json', (json.dumps(manifest, indent=2) + '\n').encode())]
    for path, data in files:
        if path.exists() and path.read_bytes() != data:
            raise ValueError('Existing private artifact differs: ' + str(path))
    for path, data in files:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    checked(installed_path, INSTALLED)
    checked(executable, DX9)
    checked(source, BUNDLE)
    print(json.dumps(manifest, indent=2))


if __name__ == '__main__':
    prepare()
