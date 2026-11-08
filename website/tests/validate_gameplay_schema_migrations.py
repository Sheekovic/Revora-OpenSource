#!/usr/bin/env python3
"""Static, dependency-focused checks for additive gameplay migrations."""

from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
MIGRATIONS = ROOT / "db" / "migrations"

required = {
    12: ["gameplay_reference.maps", "gameplay_reference.map_documents", "map_release_entries"],
    13: ["gameplay_reference.item_types", "item_type_release_entries"],
    14: ["game.item_instances", "game.inventory_profiles", "game.item_locations",
         "game.gameplay_command_receipts"],
    15: ["game.character_wallets", "game.currency_ledger", "currency_ledger_append_only"],
    16: ["game.character_attributes", "game.character_vitals", "game.equipped_items"],
    17: ["gameplay_reference.shop_catalogs", "shop_catalog_items", "game.commerce_operations"],
    18: ["gameplay_reference.warehouse_definitions", "game.warehouse_vaults", "game.warehouse_items",
         "enforce_single_item_placement"],
    19: ["gameplay_reference.item_enhancement_rules", "game.item_enhancement_operations"],
    20: ["gameplay_reference.monster_types", "gameplay_reference.monster_spawn_regions"],
    21: ["gameplay_reference.monster_drop_rules", "game.character_pk_state",
         "game.combat_operation_receipts"],
    22: ["gameplay_reference.magic_types", "game.character_skills",
         "game.character_proficiencies", "game.character_progression"],
    23: ["game.teams", "game.team_members", "game.social_relations", "game.social_invitations"],
    24: ["game.trade_sessions", "game.trade_items", "game.personal_shops"],
    25: ["game.guilds", "game.guild_members", "game.marriages"],
    26: ["gameplay_reference.quest_definitions", "gameplay_reference.npc_declarative_actions",
         "game.character_quest_progress"],
    27: ["gameplay_reference.event_definitions", "game.event_instances", "game.ranking_snapshots"],
    28: ["gameplay_reference.furniture_definitions", "gameplay_reference.mining_nodes",
         "game.character_houses"],
    29: ["admin.gameplay_permissions", "game.chat_moderation_state", "game.gameplay_admin_operations"],
    30: ["npcs_active_month_check", "active_month between 0 and 13"],
    31: ["gameplay.feature_rollouts", "gameplay.feature_canaries", "legacy_character_uid",
         "operations.owner_test_grant_events", "owner_test_grant_events_append_only"],
    32: ["game.gameplay_role_assignments", "role in ('gm')",
         "references game.characters(character_id, account_id)"],
    34: ["game.ninja_character_initialization", "game.ninja_promotion_receipts",
         "gameplay_reference.live_monster_spawn_points", "combat.runtime", "ninja.native",
         "request_fingerprint", "profession_family in ('trojan','warrior','archer','taoist','ninja')"],
    35: ["gameplay.account_feature_canaries", "game.ninja_canary_test_grants",
         "delete from gameplay.feature_gate_dependencies",
         "depends_on_feature_key='progression.rebirth'", "51650035-0000-4e56-8000-000000000001",
         "e15b066b324cf9eac62a7137f96026ec1e12b12aea516b1605fce99fafd69cc4",
         "400000035", "1002,454,455,0,0,false"],
    36: ["target_profession in (11,21,41,55,132,142)",
         "('ninja','ninja',55,1,true)", "601339::bigint,4,13",
         "601339::bigint,5,13", "reviewed ninja loadout row count mismatch"],
    37: ["damage_reduction", "activation_category", "game.monster_reward_receipts",
         "game.monster_attack_receipts", "game.stamina_audit_receipts",
         "legacy_uid=400000035", "required_migration=37"],
    38: ["game.monster_death_resolutions", "game.world_ground_drops",
         "game.ground_pickup_receipts", "ground_item_uid_seq", "classification",
         "sitting_interrupted", "previous_target_stamina", "resulting_target_stamina",
         "reject_ground_item_multi_placement",
         "gameplay_reference.monster_spawn_dependency_gates", "missing-map-dmap"],
    41: ["city_guard.character_wanted", "city_guard.bounty_cycles",
         "city_guard.account_honor_balances", "city_guard.honor_ledger",
         "unique(correlation_key)", "city_guard.reward_redemptions",
         "city_guard.arena_sentences", "city_guard.achievement_progress",
         "city_guard.jurisdictions", "city_guard.guard_power_tiers",
         "city_guard.guard_definitions", "city_guard.patrol_waypoints",
         "guards.emergency.disable", "enabled boolean not null default false"],
    42: ["game.gm_inventory_clear_receipts", "gm-discarded",
         "gm_inventory_clear_receipts_append_only", "removed_item_uids", "empty-inventory"],
    43: ["insert into game.inventory_profiles", "left join game.inventory_profiles",
         "where p.character_id is null", "on conflict (character_id) do nothing"],
    44: ["drop constraint characters_profession_check",
         "add constraint characters_profession_check",
         "20,21,22,23,24,25", "100,132,133,134,135,142,143,144,145"],
}

files = sorted(MIGRATIONS.glob("V0*_*.sql"))
versions = [int(path.name[1:4]) for path in files]
if versions != sorted(set(versions)):
    raise SystemExit("migration versions are duplicated or unordered")

for version, fragments in required.items():
    matches = list(MIGRATIONS.glob(f"V{version:03d}__*.sql"))
    if len(matches) != 1:
        raise SystemExit(f"expected exactly one V{version:03d} migration")
    text = matches[0].read_text(encoding="utf-8")
    normalized = text.lower()
    if not normalized.startswith("begin;") or "set local role revora_migrator;" not in normalized:
        raise SystemExit(f"V{version:03d} does not use the restricted migrator transaction")
    if not normalized.rstrip().endswith("commit;"):
        raise SystemExit(f"V{version:03d} does not commit explicitly")
    if re.search(r"insert\s+into\s+(identity\.game_accounts|game\.characters)", normalized):
        raise SystemExit(f"V{version:03d} attempts to import identity or character state")
    for fragment in fragments:
        if fragment.lower() not in normalized:
            raise SystemExit(f"V{version:03d} is missing required structure: {fragment}")

native_ninja = (MIGRATIONS / "V034__disabled_live_combat_and_native_ninja.sql").read_text(encoding="utf-8").lower()
if native_ninja.count("'51650034-0000-4e56-8000-000000000001','ninja',") != 120:
    raise SystemExit("V034 does not contain exactly 120 hash-pinned Ninja stat rows")
if "('51650034-0000-4e56-8000-000000000001','ninja',120,57,306,49,0)" not in native_ninja:
    raise SystemExit("V034 does not contain the exact level-120 Ninja stat sentinel")

native_ninja_canary = (MIGRATIONS / "V035__native_ninja_canary_catalog.sql").read_text(encoding="utf-8").lower()
if native_ninja_canary.count('"id":') != 548:
    raise SystemExit("V035 does not contain exactly 548 exact-client Ninja item records")
for lower, upper, expected in ((601000, 601339, 273), (112000, 112109, 88),
                               (123000, 123109, 88), (135000, 135109, 99)):
    actual = sum(lower <= int(value) <= upper for value in re.findall(r'"id":(\d+)', native_ninja_canary))
    if actual != expected:
        raise SystemExit(f"V035 sparse Ninja catalog count mismatch for {lower}-{upper}: {actual}")

inventory = (MIGRATIONS / "V014__item_instances_and_inventory.sql").read_text(encoding="utf-8").lower()
for invariant in (
    "owner_character_id uuid not null references game.characters(character_id)",
    "unique (item_instance_id, owner_character_id)",
    "unique (item_instance_id, owner_character_id, item_source_id, item_type)",
    "references gameplay_reference.item_types(source_id, item_type)",
    "unique (owner_character_id, location_kind, slot)",
):
    if invariant not in inventory:
        raise SystemExit(f"inventory ownership invariant missing: {invariant}")

ledger = (MIGRATIONS / "V015__transactional_currency_ledger.sql").read_text(encoding="utf-8").lower()
if "before update or delete on game.currency_ledger" not in ledger:
    raise SystemExit("currency ledger is not append-only")
if "unique (character_id, currency_type, operation_id)" not in ledger:
    raise SystemExit("currency ledger lacks its replay/idempotency key")

if "grant select, insert on game.gameplay_command_receipts to revora_game" not in inventory:
    raise SystemExit("gameplay command receipts do not have narrow runtime grants")

warehouse = (MIGRATIONS / "V018__warehouse_storage.sql").read_text(encoding="utf-8").lower()
if warehouse.count("execute function game.enforce_single_item_placement()") != 2:
    raise SystemExit("warehouse and inventory do not share the single-placement guard")

commerce = (MIGRATIONS / "V017__shops_and_commerce.sql").read_text(encoding="utf-8").lower()
if "foreign key (item_instance_id, character_id, item_source_id, item_type)" not in commerce:
    raise SystemExit("commerce operations do not bind item instance ownership and definition")

maps = (MIGRATIONS / "V012__complete_map_catalog.sql").read_text(encoding="utf-8").lower()
for header in ("'1003'", "'1004'", "'dmap100'", "'dmap101'"):
    if header not in maps:
        raise SystemExit(f"complete map catalog does not permit reviewed header {header}")

spawns = (MIGRATIONS / "V020__monster_spawn_and_ai_catalog.sql").read_text(encoding="utf-8").lower()
if "enabled boolean not null default false" not in spawns:
    raise SystemExit("unvalidated spawn regions do not fail closed")

quests = (MIGRATIONS / "V026__declarative_quests_and_npc_actions.sql").read_text(encoding="utf-8").lower()
for prohibited in ("execute ", "script", "sql_text", "handler_type"):
    if prohibited in quests:
        raise SystemExit(f"declarative quest schema contains prohibited execution surface: {prohibited}")

progression = (MIGRATIONS / "V039__combat_progression_and_proficiency.sql").read_text(encoding="utf-8").lower()
for required in ("combat_proficiency_results", "proficiency_level_requirements",
                 "character_experience_awarded", "primary key (operation_id,proficiency_type)"):
    if required not in progression:
        raise SystemExit(f"combat progression migration lacks {required}")

uid_runtime = (MIGRATIONS / "V040__exact_client_monster_uids.sql").read_text(encoding="utf-8").lower()
assert "monster_runtime_uid_assignments" in uid_runtime
assert "legacy_uid > 400000 and legacy_uid < 600000" in uid_runtime
assert "generate_series" in uid_runtime
assert "required_migration=40" in uid_runtime
assert "alter table game.monster_death_resolutions" in uid_runtime

city_guard = (MIGRATIONS / "V041__intelligent_city_guard_foundation.sql").read_text(encoding="utf-8").lower()
for prohibited in ("delete from game.character_pk_state", "update game.character_pk_state set pk_points=0"):
    if prohibited in city_guard:
        raise SystemExit(f"city guard migration mutates authentic PK state: {prohibited}")
if city_guard.count("'disabled',41") != 13:
    raise SystemExit("V041 does not install exactly 13 fail-closed feature gates")
if "'twin-city-commander','commander','city-veteran'" not in city_guard.replace("\n", ""):
    raise SystemExit("V041 lacks the disabled stable Commander definition")

print("gameplay schema migrations V012-V044: structural invariants passed")
