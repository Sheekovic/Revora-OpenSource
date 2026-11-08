"""Check the reviewed renewable-funding invariant, not a complete career budget."""
import argparse
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
INPUTS = [
    'docs/research/FRESH_DUKE_FUNDING_BOUND.json',
    'docs/research/CAREER_RECORDED_EXPENSES.json',
    'docs/research/FRESH_DAILY_BOSS_COMBAT.json',
    'Game/Features/DailyWorldBosses.cs',
    'Game/Entity.cs',
]


def report():
    funding, expenses, earned = [json.loads((ROOT / f).read_text(encoding='utf-8-sig')) for f in INPUTS[:3]]
    source = (ROOT / INPUTS[3]).read_text(encoding='utf-8-sig')
    entity = (ROOT / INPUTS[4]).read_text(encoding='utf-8-sig')
    rewards = {}
    for currency, name in [('CP', 'RewardCp'), ('RVP', 'RewardRvp'), ('silver', 'RewardGold')]:
        rewards[currency] = int(re.search(r'\b' + name + r'\s*=\s*(\d+)', source).group(1))
    # These checks pin the source contract reviewed with this report. They are
    # not substitutes for the existing transaction/duplicate-payment tests.
    for fragment in ['day.ToString("yyyy-MM-dd")+"/"+slot', 'slot<4',
                     'state=2 AND killer=@uid AND NOT paid', 'tx.Commit();',
                     'uint.MaxValue-RewardCp', 'uint.MaxValue-RewardRvp']:
        if fragment not in source:
            raise ValueError('Daily funding contract changed; review recurrence and payment again')
    if 'MaximumSilver = long.MaxValue' not in entity:
        raise ValueError('Review the durable silver cap')
    profile = funding['profile']
    if (earned['uid'] != profile['uid'] or earned['result']['stage'] != 3
            or earned['reward']['cps'] != rewards['CP']
            or earned['reward']['rvps'] != rewards['RVP']
            or earned['reward']['silver'] != rewards['silver']):
        raise ValueError('Earned bounty evidence does not match the reviewed source')
    bound = profile['funding_bound']
    reserve = expenses['funding_comparison']['initial_silver_reserve']
    operating = bound['medicine_silver'] + bound['repair_silver_bound']
    if (funding['result'] != 'passed' or profile['donation'] != 0
            or reserve != operating or profile['silver'] < reserve
            or bound['elapsed_ms'] >= 7200000):
        raise ValueError('The reviewed starting profile cannot sustain this funding model')
    net = dict(rewards)
    net['silver'] -= operating
    if net != expenses['funding_comparison']['net_per_modeled_boss'] or min(net.values()) <= 0:
        raise ValueError('Funding does not replenish its operating reserve')
    subtotal = expenses['reviewed_budget_subtotal']
    wins = max((subtotal[c] + net[c] - 1) // net[c] for c in net)
    if wins != expenses['funding_comparison']['wins_for_subtotal_with_allowances']:
        raise ValueError('Recorded subtotal funding comparison is stale')
    return {
        'scope': 'Source-reviewed recurrence plus the existing earned-profile funding bound; not new kills, a complete budget, or a320 certificate',
        'uid': profile['uid'], 'starting_silver': profile['silver'],
        'operating_reserve_silver': reserve, 'net_per_modeled_win': net,
        'known_subtotal_wins': wins,
        'known_subtotal_surplus': {c: wins * net[c] - subtotal[c] for c in net},
        'reserve_invariant': 'Begin with R plus optional funds. Spend at most R on the hunt, receive10M silver, and retain R before optional purchases. Each successful modeled cycle adds5628944 spendable silver,1M CP and50K RVP.',
        'renewal_contract': 'Four date-and-slot keyed opportunities per local day; payment is once per victorious run and killer, not a lifetime character quota. A new opportunity still requires a new earned victory.',
        'purchase_policy': 'Accumulate funds before a quoted purchase or class reset. Preserve R and the qualified funding loadout. Spend between cycles to retain CP/RVP/silver headroom; do not rely on a payout beyond the caps.',
        'other_resources': 'Honor and Virtue keep their independent Arena and apprentice-training routes. Boss rewards are not counted as those currencies.',
        'conclusion': 'The reviewed repeatable source has positive net income and an already affordable reserve. Finite additional purchases need additional cycles, not an exact lifetime expenditure certificate. This does not prove that every required item has an ordinary acquisition route.',
        'limits': [
            'Success is conditional on the reviewed combat/repair/travel model, available scheduled bosses and winning the kill; contested victory is not guaranteed.',
            'The recorded subtotal omits variable expenses. Eight wins is a comparison, not a full-career total or promised time to finish.',
            'Random acquisition has no fixed retry/spending bound. Positive income does not prove reachability of an unavailable random outcome.',
            'No fresh and retained achievement receipts are combined; no native or deployment gate is closed.',
        ],
        'input_sha256': {f: hashlib.sha256((ROOT / f).read_bytes()).hexdigest() for f in INPUTS},
    }


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    result = report()
    target = ROOT / 'docs/research/CAREER_FUNDING_RECURRENCE.json'
    if args.check:
        if json.loads(target.read_text()) != result:
            raise SystemExit('Funding recurrence review is stale')
    else:
        target.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({k: result[k] for k in ['operating_reserve_silver', 'net_per_modeled_win', 'known_subtotal_wins']}))
