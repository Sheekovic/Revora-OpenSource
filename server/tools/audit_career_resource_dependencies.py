"""Reconcile earning-route resource joins without replaying the saved career."""
import argparse
import ast
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'docs/research/CAREER_RESOURCE_DEPENDENCIES.json'
INPUTS = [
    'Game/Features/SubclassProgression.cs',
    'Game/Features/SubclassShop.cs',
    'tools/ClientPatches/prepare_progression_shops.py',
    'tools/CheckCandidateFreshSubclass.cs',
    'docs/SUBCLASS_PROGRESSION.md',
    'docs/research/FRESH_SECOND_REBIRTH_VALIDATION.json',
    'docs/research/FRESH_DUKE_FUNDING_BOUND.json',
    'Game/Features/SecondRebirth.cs',
    'Game/Features/ExemptionTokenShop.cs',
    'tools/CheckCandidateFreshHonor.cs',
    'tools/CheckCandidateFreshVirtue.cs',
    'docs/research/RETAINED_EARNING_REVIEW.json',
    'Game/Features/RevoraMasterPeak.cs',
    'Game/Features/RevoraMasterSystem.cs',
    'tools/CheckCandidateCareerRites.cs',
    'docs/REVORA_MASTER_PEAK_TRANSACTION.md',
    'docs/research/CAREER_RECORDED_EXPENSES.json',
]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def report(log_root):
    source = (ROOT / INPUTS[0]).read_text(encoding='utf-8-sig')
    def numbers(name):
        match = re.search(r'\b' + name + r'\s*=\s*\{([\d, ]+)\}', source)
        if not match:
            raise ValueError('Review changed subclass array: ' + name)
        return [int(x) for x in match[1].split(',')]
    ordinary, other, apothecary = [sum(numbers(n)) for n in ['OrdinaryCosts', 'OtherCosts', 'ApothecaryCosts']]
    study = ordinary * 4 + other * 3 + apothecary
    # Two ordinary schools, one double-cost school, three other schools, apothecary.
    module = ast.parse((ROOT / INPUTS[2]).read_text(encoding='utf-8-sig'))
    prices = next(ast.literal_eval(n.value) for n in module.body if isinstance(n, ast.Assign)
                  and any(isinstance(t, ast.Name) and t.id == 'SUBCLASS' for t in n.targets))
    enrollment = sum(prices[item] * count for item, count in zip(numbers('Materials'), numbers('Counts')))
    logs = {}
    for phase in ['subclass-earn', 'subclass-reload', 'honor-pay', 'honor-reload', 'virtue-earn', 'virtue-reload']:
        name = 'arena-process-career-fresh-' + phase + '.log'
        path = log_root / name
        raw = path.read_bytes()
        text = raw.decode('utf-16' if raw[:2] in (b'\xff\xfe', b'\xfe\xff') else 'utf-8-sig', errors='strict')
        marker = '[career-process] career-fresh-' + phase + '-passed'
        if marker not in text:
            raise ValueError('Missing completed saved evidence: ' + name)
        logs[name] = digest(path)
        if phase == 'subclass-reload':
            match = re.search(r'stage=1 books=(\d+) spent-study=(\d+) cp=(\d+)', text)
            if not match:
                raise ValueError('Missing subclass result')
            books, spent, paid = map(int, match.groups())
    if study != spent or paid != enrollment + books * prices[723342]:
        raise ValueError('Catalog costs disagree with actual paid subclass evidence')
    for phase in ['peak-fault', 'peak-earn', 'peak-reload', 'cap-checkpoint']:
        path = log_root / ('arena-process-career-' + phase + '.log')
        raw = path.read_bytes()
        text = raw.decode('utf-16' if raw[:2] in (b'\xff\xfe', b'\xfe\xff') else 'utf-8-sig')
        if '[career-process] career-' + phase + '-passed' not in text:
            raise ValueError('Missing completed peak-route evidence: ' + path.name)
        if phase == 'cap-checkpoint' and '[career-cap-progress] level=137 ' not in text:
            raise ValueError('Review changed pre-peak level evidence')
        logs[path.name] = digest(path)
    expenses = json.loads((ROOT / 'docs/research/CAREER_RECORDED_EXPENSES.json').read_text())
    peak = [row for row in expenses['ledgers'] if row['source'] == 'peak_eligibility']
    if len(peak) != 1 or peak[0]['receipts'] != 1 or peak[0]['currency'] != 'CP':
        raise ValueError('Review changed peak purchase history')
    second = json.loads((ROOT / INPUTS[5]).read_text())
    funding = json.loads((ROOT / INPUTS[6]).read_text())
    if second['result']['reborn'] != 2 or second['character_uid'] != funding['profile']['uid']:
        raise ValueError('Second-rebirth and funding profiles differ')
    zero_books = (study + 499) // 500
    return {
        'scope': 'Resource prerequisite composition; not a full-career expenditure audit or native acceptance',
        'uid': second['character_uid'],
        'subclasses': {
            'achievement_count': 14, 'schools': 7, 'required_study': study,
            'observed_books': books, 'observed_paid_cps': paid,
            'enrollment_cps': enrollment, 'book_cps': prices[723342],
            'zero_initial_study_books': zero_books,
            'zero_initial_study_cps': enrollment + zero_books * prices[723342],
            'conclusion': 'All seven level9/phase9 schools already passed ordinary earning and cold reload. A zero-Study start can buy the finite missing Study from the same shop; no seeded Study or daily quest P7 is required.',
        },
        'second_rebirth': {
            'item_type': 723701, 'observed_cost_cps': second['result']['token_cost_cp'],
            'route': 'Use the already earned/purchased Exemption Token route, not the retained Abyss route.',
            'excludes_achievement': 11031, 'enables_abyss': False,
        },
        'level140': {
            'evidence_uid': expenses['uid'],
            'route': 'Existing ordinary RevoraMaster peak purchase on the retained Pirate route.',
            'before_level': 137, 'after_level': 140, 'after_class': 75,
            'rebirths_unchanged': 2, 'observed_cost_cps': peak[0]['spent'],
            'cost_already_in_recorded_ledger': True,
            'validation': 'Existing rollback, quoted payment, duplicate refusal and cold reload phases; no new runtime run.',
            'order': 'Finish the selected Pirate trainer promotions before buying peak training; it directly sets final class and must not be used to infer intermediate trainer receipts.',
            'scope': 'Reuses the retained Pirate result only. Does not claim all-class peak acceptance, native UI, a fresh-character level140 run or natural combat leveling.',
        },
        'currency_sources': [
            {'currencies': ['CPs', 'RVPs', 'silver'], 'source': INPUTS[6],
             'conclusion': 'Use the ordinary boss bounty and reviewed Duke/maintenance bound as a funding prerequisite. Preserve currency caps, scheduled opportunities and the reserve needed before the fight.'},
            {'currencies': ['Study Points'], 'source': 'Game/Features/SubclassShop.cs',
             'conclusion': 'Purchased Modesty Books supply500 points each; train between purchases to respect ushort capacity.'},
            {'currencies': ['Honor'], 'source': 'tools/CheckCandidateFreshHonor.cs',
             'conclusion': 'Calculated Arena match, saved ranking settlement and cold payment already prove at least8000 Honor for Sword Soul. CP bounty is not treated as Honor.'},
            {'currencies': ['Virtue'], 'source': 'tools/CheckCandidateFreshVirtue.cs',
             'conclusion': 'Fresh team training earned2005 Virtue in452 kills for Labyrinth admission. The retained separate5022-Virtue training/Shelby route covers the5000 exchange; do not spend the same points twice.'},
        ],
        'composition_order': [
            'Use earned starter/level/first-rebirth foundation, then purchase the second-rebirth token before consuming it.',
            'Fund optional purchases before entering timed encounters or resetting class/level; retain medicine and repair funds.',
            'Train subclasses incrementally using CP-bought materials and books; their14 completed receipts survive later rites.',
            'Earn Arena Honor before Sword Soul and separate Virtue balances before Labyrinth admission and Shelby exchange.',
            'Keep the previously reviewed female-before-marriage, class-before-mastery-reset and permanent-collection ordering.',
        ],
        'compatible_career_verified': False,
        'remaining': [
            'Reconcile the full selected route spending and reserves, including peer purchases, against scheduled funding; these bounded joins are not that total.',
            'Do not infer every historical controlled encounter is naturally survivable from the one boss model.',
            'Native timing, admission/activation gates and coordinated release remain separate.',
        ],
        'log_sha256': logs,
        'input_sha256': {name: digest(ROOT / name) for name in INPUTS},
    }


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--log-root', type=Path, default=Path('C:/Revora/DisposablePostgres/moon-box-candidate'))
    args = parser.parse_args()
    result = report(args.log_root)
    if args.check:
        if json.loads(OUTPUT.read_text()) != result:
            raise SystemExit('Career resource dependency review is stale')
    else:
        OUTPUT.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'subclasses': result['subclasses'], 'second_rebirth': result['second_rebirth']}))
