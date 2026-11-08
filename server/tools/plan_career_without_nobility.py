"""Select a concrete 320-identity plan; this does not certify a played career."""
import argparse
import hashlib
import json
from pathlib import Path
from audit_current_career_candidates import report as current_candidates
from audit_career_funding_recurrence import report as current_funding

ROOT = Path(__file__).resolve().parents[1]
INPUTS = [
    'docs/research/CURRENT_CAREER_CANDIDATES.json',
    'docs/research/CURRENT_CAREER_RECEIPTS.json',
    'docs/research/RETAINED_EARNING_REVIEW.json',
    'docs/research/RETAINED_OBSERVED_PROVENANCE.json',
    'docs/research/RETAINED_COLLECTION_200.json',
    'docs/research/RETAINED_BATTLE_POWER_AUDIT.json',
    'docs/research/FRESH_DUKE_FUNDING_BOUND.json',
    'docs/research/CAREER_RESOURCE_DEPENDENCIES.json',
    'docs/research/CAREER_ORDERING_REVIEW.json',
    'docs/research/CAREER_FUNDING_RECURRENCE.json',
    'docs/research/FRESH_NORMAL_KO.json',
    'docs/research/FRESH_SNOW_TIMED_COMBAT.json',
    'docs/research/ROADMAP_COMBAT_CLOSEOUT.json',
    'docs/research/FRESH_ARCHER_ARROW.json',
    'docs/research/FRESH_MOON_VALIDATION.json',
    'docs/research/POWER_ARENA_WINDOW_BUDGET.json',
    'docs/research/COLLECTION_RELEASE_SUPPLY.json',
    'docs/research/FLAME_GATE_BUDGET.json',
    'docs/research/CAREER_CATALOG_COMBAT.json',
    'docs/research/DIS_CITY_COMBAT_BUDGET.json',
    'docs/research/UNARMED_EXTRACTION_BUDGET.json',
    'docs/research/CAREER_320_ACCEPTANCE_GAPS.json',
]


def report():
    candidate, receipts, earned, observed, collection, power, funding, resources, ordering, recurrence, ko, snow, combat, arrow, moon, power_window, collection_release, flame, catalog_combat, dis_combat, unarmed, acceptance = [
        json.loads((ROOT / name).read_text(encoding='utf-8-sig')) for name in INPUTS
    ]
    if (dis_combat['result'] != 'passed' or len(dis_combat['cases']) != 10
            or dis_combat['hall']['minimum_hp'] <= 0
            or dis_combat['hall']['medicines'] > dis_combat['hall']['starting_stock']
            or dis_combat['hall']['wear_bound'] >= dis_combat['hall']['starting_durability']):
        raise ValueError('Standard Dis City stock bound is missing or failed')
    for name, digest in {**dis_combat['source_sha256'], **dis_combat['catalog_file_sha256']}.items():
        if hashlib.sha256((ROOT / name).read_bytes()).hexdigest() != digest:
            raise ValueError('Dis City combat input changed: ' + name)
    if (unarmed['result'] != 'passed' or unarmed['minimum_hp'] <= 0
            or unarmed['medicines'] > unarmed['medicine_stock']
            or unarmed['budget_seconds'] >= unarmed['remaining_event_seconds']):
        raise ValueError('Unarmed final-blow budget is missing or failed')
    for name, digest in {**unarmed['source_sha256'], **unarmed['catalog_file_sha256']}.items():
        if hashlib.sha256((ROOT / name).read_bytes()).hexdigest() != digest:
            raise ValueError('Unarmed budget input changed: ' + name)
    for name, digest in acceptance['source_sha256'].items():
        if hashlib.sha256((ROOT / name).read_bytes()).hexdigest() != digest:
            raise ValueError('Career acceptance handoff input changed: ' + name)
    if candidate != current_candidates():
        raise ValueError('Current candidate index is stale')
    if (catalog_combat['result'] != 'passed' or catalog_combat['profile_uid'] != 1096322
            or len(catalog_combat['cases']) != 48 or catalog_combat['minimum_hp'] <= 0
            or catalog_combat['maximum_medicines'] > 100):
        raise ValueError('Catalog encounter batch is missing or exceeds its standalone reserve')
    for name, digest in {**catalog_combat['source_sha256'], **catalog_combat['catalog_file_sha256']}.items():
        if hashlib.sha256((ROOT / name).read_bytes()).hexdigest() != digest:
            raise ValueError('Catalog combat input changed: ' + name)
    if (flame['result'] != 'passed' or flame['profile_uid'] != 1096322
            or flame['finish_seconds'] >= flame['window_seconds']):
        raise ValueError('Flame gate budget is missing or exceeds the war window')
    for name, digest in flame['source_hashes'].items():
        if hashlib.sha256((ROOT / name).read_bytes()).hexdigest() != digest:
            raise ValueError('Flame gate evidence source changed: ' + name)
    if recurrence != current_funding():
        raise ValueError('Funding recurrence review is stale')
    uid = receipts['retained_uid']
    if ordering['retained_uid'] != uid or not ordering.get('current_reconciliation'):
        raise ValueError('Current same-character ordering review is missing')
    if any(value != uid for value in [earned['uid'], observed['retained_uid'], collection['uid'], power['uid']]):
        raise ValueError('Do not combine evidence for different retained characters')
    excluded = set(candidate['retained_early_promotions_excluded_as_unproven'])
    excluded.update(candidate['activation_gated_exclusions'])
    retained = {row['achievement_id'] for row in receipts['retained_receipts']}
    pool = retained - excluded
    if len(pool) != candidate['retained_conservative_candidates']:
        raise ValueError('Candidate pool changed; review the route before using this plan')
    omitted = {10318, 11120}
    if not omitted <= pool:
        raise ValueError('Nobility alternatives are no longer in the candidate pool')
    selected = pool - omitted
    if len(selected) != 320:
        raise ValueError('Plan no longer meets exactly320; do not lower the target')
    groups = []
    covered = set()
    for name, document in [(INPUTS[2], earned), (INPUTS[3], observed)]:
        for index, group in enumerate(document['groups']):
            identities = sorted(selected.intersection(group['ids']))
            if identities:
                covered.update(identities)
                groups.append({'review': name, 'group_index': index, 'ids': identities})
    if collection['result'] != 'passed' or collection['mounts'] < 200:
        raise ValueError('The plan requires the existing permanent200-mount earning evidence')
    if (len(covered) != acceptance['reviewed_group_identity_count']
            or collection['achievement'] != acceptance['separate_collection_identity']
            or earned['open_findings']):
        raise ValueError('Earning-review coverage changed; reconcile acceptance gaps')
    covered.add(collection['achievement'])
    if covered != selected:
        raise ValueError('Selected identities lack an earning review')
    # The saved collection witness used older catalogs. A release must preserve
    # its suppliers, not merely its achievement rows or the catalog's capacity.
    if (11232 not in selected or collection_release['staged']['Mounts'] < 200
            or collection_release['staged']['Garments'] < 200
            or len(set(collection_release['pack_outputs'].values())) != 17):
        raise ValueError('Selected collection tiers lack the reviewed release supply')
    for name, expected in collection_release['input_sha256'].items():
        if hashlib.sha256((ROOT / name).read_bytes()).hexdigest() != expected:
            raise ValueError('Collection release input changed; rerun its loader review: ' + name)
    for entry in collection_release['files']:
        if hashlib.sha256(Path(entry['source']).read_bytes()).hexdigest() != entry['before']:
            raise ValueError('Installed collection catalog changed; reconcile its release evidence')
        staged = ROOT / 'obj/collection-mount-packs/staged' / entry['group'] / 'emoneyShopV2.ini'
        if hashlib.sha256(staged.read_bytes()).hexdigest() != entry['after']:
            raise ValueError('Staged collection catalog does not match the reviewed restoration')
    parts = ['level', 'rebirth', 'nobility', 'guild_share', 'independent_bonus', 'mentor_share', 'clan_share', 'equipment']
    if sum(power[key] for key in parts) != power['total']:
        raise ValueError('Historical Battle Power breakdown does not reconcile')
    core_power = power['level'] + power['rebirth'] + power['equipment']
    if core_power < 300:
        raise ValueError('Selected Battle Power milestones need another supported route')
    profile = funding['profile']
    bound = profile['funding_bound']
    if (funding['result'] != 'passed' or profile['uid'] != receipts['fresh_uid']
            or profile['donation'] != 0 or profile['rank'] != 'Duke'
            or bound['elapsed_ms'] >= 7200000 or bound['net_bounty_silver_bound'] <= 0
            or bound['maximum_wear_between_repairs'] >= bound['minimum_maximum_durability']
            or not funding['saved_career_snapshot_unchanged']):
        raise ValueError('Zero-donation funding dependency lacks the reviewed numeric bound')
    if (resources['uid'] != profile['uid'] or resources['second_rebirth']['enables_abyss']
            or resources['second_rebirth']['excludes_achievement'] in selected
            or resources['subclasses']['achievement_count'] != 14):
        raise ValueError('Resource route no longer matches the selected achievement plan')
    if (ko['uid'] != profile['uid'] or ko['result'] != 'passed'
            or ko['buffered_run']['chain_kills'] < 300
            or ko['buffered_run']['buffer_callback_every_chain_kills'] <= 0
            or ko['buffered_run']['cold_snapshot'] != 'equal'):
        raise ValueError('KO timing dependency lacks the earned buffered chain and cold recovery')
    if (snow['uid'] != profile['uid'] or not snow['server_side_victory_verified']
            or snow['result']['stage'] != 3
            or snow['reward_evidence']['persisted_victory_killer'] != profile['uid']):
        raise ValueError('Timed Snow dependency lacks the saved ordinary victory')
    deadline = combat['deadline_dependency']
    spook = combat['spook_maintenance']
    if (spook['profile_uid'] != profile['uid'] or spook['result'] != 'passed'
            or spook['modeled_seconds'] + spook['approach_allowance_seconds'] >= spook['deadline_seconds']
            or spook['minimum_hp'] <= 0 or spook['medicines'] > spook['available_medicines']
            or spook['per_item_wear_bound'] >= spook['available_non_arrow_durability']):
        raise ValueError('Selected Spook encounter exceeds its reviewed survival or time reserve')
    for name, digest in spook['source_sha256'].items():
        if hashlib.sha256((ROOT / name).read_bytes()).hexdigest() != digest:
            raise ValueError('Spook encounter evidence source changed: ' + name)
    survival = deadline['survival']
    stamina = combat['acquisition_dependencies']['stamina']
    if (stamina['result'] != 'ordinary regeneration prerequisite verified'
            or stamina['standing_per_callback'] <= 0
            or any(row['stamina'] > stamina['unblessed_cap'] for row in stamina['catalog_rows'])):
        raise ValueError('Learned skill costs exceed the verified ordinary stamina recovery')
    if (moon['character_uid'] != profile['uid'] or moon['rng_override']
            or moon['calculated_melee_attacks'] <= 0
            or 'career-fresh-moon-reload' not in moon['passed_logs']):
        raise ValueError('Moon Box dependency lacks the calculated earned route and cold recovery')
    if (combat['result'] != 'passed' or deadline['profile_uid'] != profile['uid']
            or any(row['modeled_seconds'] >= row['deadline_seconds'] for row in deadline['encounters'])
            or survival['combined_medicines'] > survival['available_medicines']
            or survival['per_item_wear_bound'] >= survival['lowest_saved_non_arrow_durability']):
        raise ValueError('Encounter model no longer fits its deadline or maintenance reserve')
    if (arrow['uid'] != profile['uid'] or arrow['result'] != 'passed'
            or not arrow['cold_game_process_reload'] or arrow['equipped_durability'] <= 0
            or not arrow['old_broken_item_preserved'] or not arrow['repeated_equip_unchanged']):
        raise ValueError('Ordinary replacement arrow dependency lacks cold equipment recovery')
    if (power_window['result'] != 'passed' or power_window['profile_uid'] != profile['uid']
            or sum(power_window['guardian_needs']) != 365
            or power_window['total_budget_seconds'] >= power_window['window_seconds']):
        raise ValueError('Power Arena quota lacks its reviewed attack/population budget')
    return {
        'scope': 'Concrete target selection and historical BP arithmetic; not a jointly earned career certificate',
        'input_sha256': {name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest() for name in INPUTS},
        'target': 320,
        'selected_count': len(selected),
        'selected_ids': sorted(selected),
        'omitted_nobility_ids': sorted(omitted),
        'reason': 'Avoid requiring donation/King receipts whose ordinary NPC/native access remains unresolved.',
        'review_coverage': groups,
        'collection_review': INPUTS[4],
        'collection_release_dependency': {
            'review': INPUTS[16],
            'installed_supplier_potential': collection_release['before'],
            'staged_supplier_potential': collection_release['staged'],
            'requires_coordinated_catalog_release': not collection_release['deployed'],
            'native_acceptance': collection_release['native_acceptance'],
            'scope': 'Reuse historical earning only with its restored suppliers. Staged capacity is not installed availability or a newly earned collection.',
        },
        'battle_power_dependency': {
            'historical_total': power['total'],
            'without_nobility': power['total'] - power['nobility'],
            'level_rebirth_equipment_only': core_power,
            'highest_selected_threshold': 300,
            'scope': 'Arithmetic on a previously validated repaired loadout, not a new zero-nobility gameplay run.',
        },
        'compatible_career_verified': False,
        'ordered_state_dependency': {
            'review': INPUTS[8],
            'sequence': ordering['current_reconciliation']['ordering'],
            'scope': ordering['current_reconciliation']['scope'],
        },
        'funding_dependency': {
            'review': INPUTS[6],
            'recurrence_review': INPUTS[9],
            'purchase_policy': recurrence['purchase_policy'],
            'scope': 'Loaded ordinary Duke stats, conservative combat/repair arithmetic and reused payment/repair evidence; not native timing or a full-career budget.',
            'modeled_hunt_ms': bound['elapsed_ms'],
            'net_silver_bound': bound['net_bounty_silver_bound'],
        },
        'resource_dependency': {
            'review': INPUTS[7],
            'second_rebirth_route': 'Purchased Exemption Token; no disabled Abyss dependency',
            'second_rebirth_cps': resources['second_rebirth']['observed_cost_cps'],
            'all_subclasses_from_zero_study_cps': resources['subclasses']['zero_initial_study_cps'],
            'scope': 'Specific resource joins only; Honor and Virtue retain their separate earned sources.',
        },
        'reconciled_encounter_dependencies': {
            'unarmed_extraction': {'review': INPUTS[20], 'status': 'weapon-assisted-final-blow-budget',
                                   'scope': unarmed['scope'], 'limits': unarmed['limits']},
            'dis_city': {'review': INPUTS[19], 'status': 'standard-numeric-stock-bound',
                         'scope': dis_combat['scope'], 'limits': dis_combat['limits']},
            'catalog_families': {'review': INPUTS[18], 'status': 'independent-one-enemy-bounds',
                                 'scope': catalog_combat['scope'], 'limits': catalog_combat['limits']},
            'spook': {'review': INPUTS[12], 'status': 'calculated-survival-budget',
                      'scope': spook['scope'], 'limits': spook['limits']},
            'flame_lit': {'review': INPUTS[17], 'status': 'calculated-gate-budget',
                          'scope': flame['scope'], 'limits': flame['limits']},
            'power_arena': {'review': INPUTS[15], 'status': 'attack-population-budgeted',
                            'scope': power_window['scope'], 'limits': power_window['limits']},
            'moon_box': {'review': INPUTS[14], 'status': 'earned-cold-validated',
                         'scope': moon['scope'], 'controls': moon['controls']},
            'ko_chain': {'review': INPUTS[10], 'status': 'earned-cold-validated',
                         'route': 'Learned Cyclone with ordinary attack cadence and periodic buffer callbacks; the earlier Omnipotence timing limitation does not require another chain.'},
            'snow': {'review': INPUTS[11], 'status': 'earned-cold-validated',
                     'scope': snow['scope']},
            'terato_sword': {'review': INPUTS[12], 'status': 'numerically-bounded',
                             'scope': deadline['scope'],
                             'encounters': deadline['encounters']},
            'replacement_arrow': {'review': INPUTS[13], 'status': 'earned-cold-validated',
                                  'scope': arrow['scope']},
            'stamina_recovery': {'review': INPUTS[12], 'status': 'callback-validated',
                                 'scope': stamina['scope']},
            'limits': 'These resolve named prerequisites only. Separate character receipts are not summed; numerical encounter bounds are not new saved victories. Native traversal and release remain open.',
        },
        'acceptance_handoff': INPUTS[21],
        'remaining': [row['id'] + ': ' + row['requirement']
                      for row in acceptance['acceptance_gaps'] if row['status'] != 'passed'],
        'separate_roadmap_gates': acceptance['separate_roadmap_gates'],
        'non_changes': [
            'No catalog or title threshold change, no receipt deletion/insertion, no currency or profile mutation.',
            'Fresh and retained character receipts are not added together.',
            'Nobility remains part of the game and its existing work is not marked complete or removed from the roadmap.',
        ],
    }


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    result = report()
    target = ROOT / 'docs/research/CAREER_320_SELECTION.json'
    if args.check:
        if json.loads(target.read_text(encoding='utf-8-sig')) != result:
            raise SystemExit('320-identity plan is stale')
    else:
        target.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({key: result[key] for key in ['selected_count', 'omitted_nobility_ids', 'battle_power_dependency', 'compatible_career_verified']}))
