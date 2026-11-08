"""Prepare an unsigned Trojan journal review overlay; never modify the active client."""
import json
import re
from prepare_warrior_journal import ROOT, WORK, INSTALLED, DX9, checked, sha

SOURCE = '160a4d649f5120216cba33f813c5612f16c291715a296bb200f4795b4e2387de'
ACTORS = {
    'pak': '10579,1002,425,298,Pak`sGhost,TwinCity',
    'mok': '10580,1002,466,400,SuperMok,TwinCity',
    'general': '10581,3825,34,54,GeneralPak,OldenMemory',
    'monk': '10583,3825,54,34,MonkMisery,OldenMemory',
    'mirror': '10582,3825,40,35,Mr.Mirror,OldenMemory',
    'essence': '10600,3825,34,54,GeneralPak,OldenMemory',
    'jade': '10602,3825,54,54,DivineJade,OldenMemory',
    'final': '10603,3825,34,54,GeneralPak,OldenMemory',
}


def link(key):
    uid, world, x, y, name, _ = ACTORS[key].split(',')
    return f'[npc {name},{uid},{world},{x},{y}]'


ROWS = {
    3269: ('pak', 'mok', f'Help {link("mok")} recover 27 Solar Scraps.',
           'Begin with Pak`s Ghost. Super Mok can recover one scrap with your daily energy. Return each day until all 27 are gathered.'),
    3278: ('mok', 'pak', f'Ask {link("pak")} to reforge the Solar Blade.',
           'The scraps are gathered. Pak`s Ghost can reforge the blade and lead you into an ancient memory.'),
    3275: ('pak', 'general', f'Enter Pak`s memory and speak with {link("general")}.',
           'Ask Pak`s Ghost to enter the memory. General Pak waits in the first chamber. Speak with him about the evil array.'),
    3270: ('general', 'monk', f'Help {link("monk")} weaken the array with 10,000 points of magic.',
           'Lend your strength to Monk Misery. Each daily effort weakens the array; 23 efforts finish this task.'),
    3276: ('general', 'general', f'Report to {link("general")}.',
           'The array is broken. General Pak knows where the lingering shades can be found.'),
    3271: ('general', 'mirror', f'Ask {link("mirror")} to reveal and defeat 18 Ghost Reavers.',
           'Mr. Mirror reveals one shade at a time. Defeat each shade yourself, then return for your next hunt.'),
    3277: ('general', 'general', f'Report to {link("general")}.',
           'The last shade reveals the traitor. General Pak will send you to confront Monk Misery.'),
    3272: ('monk', 'general', f'Confront {link("monk")} and defeat him.[chk_quest 3272,1,1,1]',
           'Challenge Monk Misery, defeat his revealed form, then follow General Pak to the next chamber.'),
    3273: ('general', 'essence', f'Follow General Pak, then ask {link("essence")} to gather 99 Solar Essences.',
           'General Pak leads you to the second chamber. Gather solar essence with him over 25 efforts. Daily energy renews each day.'),
    3279: ('essence', 'jade', f'Infuse the essence at {link("jade")}.',
           'The essence is ready. Infuse it into the blade at Divine Jade, then follow General Pak to the final chamber.'),
    3280: ('final', 'final', f'Borrow the Solar Blade from {link("final")} [chk_item 3003340,1].',
           'General Pak lends you the Solar Blade. Keep it in your bag for the final battle. He can recover a lost blade, but cannot duplicate one you still possess.'),
    3274: ('final', 'final', 'Defeat the [mob FuriousFlameDevastator,3825,45,40][chk_quest 3274,1,1,1].',
           'Ask General Pak to reveal the Devastator. Defeat it, then return the borrowed blade to claim a new saber. Your existing weapons are not consumed.'),
}
PRIZE = 'Complete the 90-energy journey for one new unbound level-140 FangCrossSaber: +12, -7 damage reduction, two empty sockets.'


def rewrite(before):
    edits, seen = [], set()
    for match in re.finditer(rb'^\[\d+\]\r?$[\s\S]*?(?=^\[|\Z)', before, re.M):
        original = match.group()
        ids = re.findall(rb'^MissionId=(\d+)\r?$', original, re.M)
        if not any(int(i) in ROWS for i in ids):
            continue
        if len(ids) != 1 or int(ids[0]) in seen:
            raise ValueError('Ambiguous Trojan mission identity')
        identity = int(ids[0]); seen.add(identity)
        begin, finish, intention, content = ROWS[identity]
        changes = {'BeginNpcId': ACTORS[begin], 'FinishNpcId': ACTORS[finish],
                   'Intention1': intention, 'Content': content, 'Prize': PRIZE,
                   'IntentionDesp': re.sub(r'\[chk_[^\]]+\]', '', re.sub(r'\[(?:npc|mob) ([^,]+),[^\]]+\]', r'\1', intention))}
        changed = original
        for key, value in changes.items():
            pattern = rb'^' + key.encode() + rb'=[^\r\n]*(\r?)(?=\n|\Z)'
            changed, count = re.subn(pattern, lambda m: key.encode() + b'=' + value.encode('ascii') + m[1], changed, flags=re.M)
            if count != 1:
                raise ValueError('Missing or duplicate Trojan field: ' + key)
        # Retain the evidenced physical-item and two kill objectives exactly.
        if re.findall(rb'\[chk_[^\]]+\]', original) != re.findall(rb'\[chk_[^\]]+\]', changed):
            raise ValueError('Trojan objective token changed')
        edits.append((match.start(), match.end(), original, changed))
    if seen != set(ROWS):
        raise ValueError('Missing Trojan mission')
    after = before
    for start, end, _, changed in reversed(edits):
        after = after[:start] + changed + after[end:]
    restored = after
    for _, _, original, changed in edits:
        if restored.count(changed) != 1:
            raise ValueError('Ambiguous rewritten Trojan section')
        restored = restored.replace(changed, original, 1)
    if restored != before:
        raise ValueError('Unrelated journal bytes changed')
    return after


def prepare():
    installed_path = ROOT / '6711/ini/Questinfo.ini'
    executable = ROOT / '6711/Env_DX9/Conquer.exe'
    source = WORK / 'warrior-max-reward-journal/staged/ini/Questinfo.ini'
    installed = checked(installed_path, INSTALLED); checked(executable, DX9)
    after = rewrite(checked(source, SOURCE))
    if rewrite(after) != after:
        raise ValueError('Trojan rewrite is not idempotent')
    manifest = dict(purpose='private-trojan-journal-review', relative='ini/Questinfo.ini',
                    before=INSTALLED, source=SOURCE, after=sha(after), approved_dx9=DX9,
                    architecture='x86', missions=list(ROWS), installed=False, published=False,
                    signed=False, native_acceptance='pending',
                    limits='3825 is verified server terrain, not verified native private-map link resolution. Validate links, reborn low-level visibility, item/kill objectives and final claim state on genuine DX9.',
                    preserved='Prior composed quest edits; all unselected fields, non-Trojan sections and chk_ objective tokens.',
                    rollback='Future installation must verify the full preimage and back it up; restore originals/ini/Questinfo.ini transactionally.')
    destination = WORK / 'trojan-epic-journal'
    files = [(destination / 'originals/ini/Questinfo.ini', installed),
             (destination / 'staged/ini/Questinfo.ini', after),
             (destination / 'manifest.json', (json.dumps(manifest, indent=2) + '\n').encode())]
    for path, data in files:
        if path.exists() and path.read_bytes() != data:
            raise ValueError('Existing review artifact differs: ' + str(path))
    for path, data in files:
        path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(data)
    checked(installed_path, INSTALLED); checked(executable, DX9); checked(source, SOURCE)
    print(json.dumps(manifest, indent=2))


if __name__ == '__main__':
    prepare()
