"""Privately compose starter-quest corrections over reviewed journal fixes.

No active client changes, installation, signing or publication.
"""
import json
import re
from prepare_warrior_journal import (
    ROOT, WORK, INSTALLED, BUNDLE, DX9, checked, sha,
    monk_rewrite, rewrite as warrior_rewrite,
)

FIELDS = {
    90001: {
        'Prize': 'New hunts: bound P7 soul, 25,000 gold, 100 Revora Points, Exp Potions and level puff. Earlier accepted hunts retain their promised reward.',
    },
    90002: {
        'Prize': '8,000 CPs, 80,000 gold, 200 Revora Points, bound P7 soul, 7-day VIP 6, puff to 120',
        'Intention2': "Ask [npc Cerys,900000017,1002,319,303] for passage, then hunt 20 Bandits in Phoenix Castle.[chk_quest 90002,1,2,20]",
        'Content': "Cerys sends you to the City Commander, Phoenix Castle Bandits, and PurseClerk. Return to her for the recovered purse. Once per life.",
    },
    90003: {
        'Prize': 'Bound P7 soul, Exp Potions, 50,000 gold, 500 Revora Points, level puff',
    },
}


def rewrite(before):
    edits, seen = [], set()
    for match in re.finditer(rb'^\[\d+\]\r?$[\s\S]*?(?=^\[|\Z)', before, re.M):
        original = match.group()
        identities = re.findall(rb'^MissionId=([^\r\n]*)\r?$', original, re.M)
        if not any(value.isdigit() and int(value) in FIELDS for value in identities):
            continue
        if len(identities) != 1:
            raise ValueError('Ambiguous starter mission identity')
        identity = int(identities[0])
        if identity in seen:
            raise ValueError('Duplicate starter mission')
        seen.add(identity)
        changed = original
        for key, value in FIELDS[identity].items():
            expression = rb'^' + key.encode() + rb'=([^\r\n]*)(\r?)(?=\n|\Z)'
            if len(re.findall(expression, original, re.M)) != 1:
                raise ValueError('Missing or duplicate starter field: ' + key)
            changed = re.sub(expression, lambda m: key.encode() + b'=' + value.encode('ascii') + m[2], changed, flags=re.M)
        edits.append((match.start(), match.end(), original, changed))
    if seen != set(FIELDS):
        raise ValueError('Missing starter mission')
    after = before
    for start, end, original, changed in reversed(edits):
        after = after[:start] + changed + after[end:]
    restored = after
    for _, _, original, changed in edits:
        if restored.count(changed) != 1:
            raise ValueError('Ambiguous rewritten starter section')
        restored = restored.replace(changed, original, 1)
    if restored != before:
        raise ValueError('Unrelated journal bytes changed')
    return after


def prepare():
    installed_path = ROOT / '6711/ini/Questinfo.ini'
    executable = ROOT / '6711/Env_DX9/Conquer.exe'
    source = WORK / 'quest-client-bundle-v1/ini/Questinfo.ini'
    installed = checked(installed_path, INSTALLED)
    checked(executable, DX9)
    bundle = checked(source, BUNDLE)
    composed = warrior_rewrite(monk_rewrite(bundle), valuable=True)
    after = rewrite(composed)
    if rewrite(after) != after:
        raise ValueError('Starter correction is not idempotent')
    manifest = dict(purpose='private-composed-starter-quest-journal-review', relative='ini/Questinfo.ini',
                    before=INSTALLED, source_bundle=BUNDLE, previous_composed=sha(composed), after=sha(after),
                    approved_dx9=DX9, architecture='x86', missions=list(FIELDS),
                    preserved='Prior quest bundle, Monk and Warrior corrections; only five fields in starter missions changed.',
                    route='Courier: Cerys passage to Phoenix Castle 1011; Commander and Clerk navigation unchanged.',
                    limits='Cerys link directs to the verified departure NPC, not an inferred Bandit spawn coordinate. Native links, text fit and binding display need review.',
                    installed=False, published=False, signed=False, native_acceptance='pending',
                    rollback='Future deployment must verify and back up the installed preimage, and restore originals/ini/Questinfo.ini transactionally.')
    destination = WORK / 'starter-quest-journal-v2'
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
