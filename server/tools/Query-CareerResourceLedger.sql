-- psql -v career_uid=<reviewed character UID> -f this-file
-- Read-only historical expenses, not a complete budget or proof of income.
BEGIN READ ONLY;
WITH owner AS (SELECT :'career_uid'::bigint AS uid), ledger AS (
 SELECT 'attack_talisman' AS source,'silver' AS currency,price AS amount FROM revora_attack_talisman_purchases JOIN owner USING(uid)
 UNION ALL SELECT 'defense_talisman','silver',price FROM revora_defense_talisman_purchases JOIN owner USING(uid)
 UNION ALL SELECT 'desert_portrait','silver',price FROM revora_desert_treasure_bids JOIN owner USING(uid) WHERE won
 UNION ALL SELECT 'equipment_repairs','silver',cost FROM revora_equipment_repairs JOIN owner USING(uid)
 UNION ALL SELECT 'euro_shop',CASE WHEN bound THEN 'bound_CP' ELSE 'CP' END,cost FROM revora_euro_jersey_purchases JOIN owner USING(uid)
 UNION ALL SELECT 'exemption_and_stones',CASE WHEN bound THEN 'bound_CP' ELSE 'CP' END,cost FROM revora_exemption_purchases JOIN owner USING(uid)
 UNION ALL SELECT 'gem_composition','silver',cost FROM revora_gem_compositions JOIN owner USING(uid)
 UNION ALL SELECT 'honor_shop','Honor',cost FROM revora_honor_purchases JOIN owner USING(uid)
 UNION ALL SELECT 'house_construction','silver',silver_paid FROM revora_house_builds JOIN owner USING(uid)
 UNION ALL SELECT 'furniture','CP',cps_paid FROM revora_furniture_purchases JOIN owner USING(uid) WHERE cps_paid>0
 UNION ALL SELECT 'furniture','silver',silver_paid FROM revora_furniture_purchases JOIN owner USING(uid) WHERE silver_paid>0
 UNION ALL SELECT 'appearance','silver',charged FROM revora_cosmetic_receipts JOIN owner USING(uid)
 -- Runs.fee already includes rerolls; do not also add magnolia_changes.fee.
 UNION ALL SELECT 'magnolia','CP',fee FROM revora_magnolia_runs JOIN owner USING(uid)
 UNION ALL SELECT 'mount_kit','CP',price FROM revora_mount_kit_purchases JOIN owner USING(uid)
 UNION ALL SELECT 'ninja_epic','CP',cost FROM revora_ninja_epic_operations JOIN owner USING(uid)
 UNION ALL SELECT 'oblivion_dew','CP',cost FROM revora_oblivion_purchases JOIN owner USING(uid)
 UNION ALL SELECT 'peak_eligibility','CP',price FROM revora_peak_purchases JOIN owner USING(uid)
 UNION ALL SELECT 'reincarnation','CP',cost FROM revora_reincarnations JOIN owner USING(uid)
 UNION ALL SELECT 'travel_scrolls','silver',cost FROM revora_scroll_purchases JOIN owner USING(uid)
 UNION ALL SELECT 'sign_in','RVP',cost FROM revora_sign_in_claims JOIN owner USING(uid)
 UNION ALL SELECT 'vestments','CP',price FROM revora_vestment_purchases JOIN owner USING(uid)
 UNION ALL SELECT 'vip','CP',price FROM revora_vip_purchases JOIN owner USING(uid)
 UNION ALL SELECT 'weapon_books','silver',cost FROM revora_weapon_book_purchases JOIN owner USING(uid)
), grouped AS (
 SELECT source,currency,count(*) AS receipts,sum(amount) AS spent FROM ledger GROUP BY source,currency
), totals AS (
 SELECT currency,sum(receipts) AS receipts,sum(spent) AS spent FROM grouped GROUP BY currency
), material_uses AS (
 SELECT 'upgrade' AS source,m.item_type,sum(m.before_stack-m.after_stack) AS units
 FROM revora_equipment_upgrade_materials m JOIN revora_equipment_upgrades u ON u.id=m.receipt JOIN owner ON owner.uid=u.uid GROUP BY m.item_type
 UNION ALL SELECT 'repair',1088001,sum(c.used) FROM revora_equipment_repairs JOIN owner USING(uid) CROSS JOIN LATERAL unnest(consumed)c(used)
 UNION ALL SELECT 'sockets',material_type,sum(cardinality(materials)) FROM revora_equipment_sockets JOIN owner USING(uid) GROUP BY material_type
 UNION ALL SELECT 'embedding',gem_type,sum(before_stack-after_stack) FROM revora_gem_embeddings JOIN owner USING(uid) GROUP BY gem_type
 UNION ALL SELECT 'enchantment',gem_type,sum(before_stack-after_stack) FROM revora_item_enchantments JOIN owner USING(uid) GROUP BY gem_type
)
SELECT json_build_object('uid',(SELECT uid FROM owner),
 'ledgers',(SELECT json_agg(r ORDER BY source,currency) FROM grouped r),
 'totals',(SELECT json_agg(r ORDER BY currency) FROM totals r),
 'material_uses',(SELECT json_agg(r ORDER BY source,item_type) FROM material_uses r WHERE units>0),
 'fixed_passages',(SELECT json_agg(r ORDER BY source) FROM (
   SELECT 'mining' AS source,trips,100 AS unit_silver FROM revora_plain_mine_passages JOIN owner USING(uid)
   UNION ALL SELECT 'training',trips,1000 FROM revora_training_ground_passages JOIN owner USING(uid)
   UNION ALL SELECT 'adventure',trips,1000 FROM revora_adventure_passages JOIN owner USING(uid)
 )r));
COMMIT;
