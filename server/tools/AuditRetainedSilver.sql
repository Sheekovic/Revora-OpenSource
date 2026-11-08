-- Private retained fixture only. Supply credentials outside command arguments.
BEGIN READ ONLY;
SELECT json_build_object(
    'uid', 4294907001,
    'first_rite_purse', (
        SELECT snapshot::jsonb->'belongings'->'purse'
        FROM revora_career_rites_probe WHERE uid=4294907001),
    'first_rite_silver_receipt', (
        SELECT EXISTS(SELECT 1 FROM jsonb_array_elements(snapshot::jsonb->'achievements') a
                      WHERE (a->>'achievement_id')::int=10106)
        FROM revora_career_rites_probe WHERE uid=4294907001),
    'current_silver_receipt', (
        SELECT count(*) FROM revora_achievement_evidence
        WHERE uid=4294907001 AND achievement_id=10106 AND source='observed-state'),
    'paid_boss_run_count', (
        SELECT count(*) FROM revora_daily_boss_runs WHERE killer=4294907001 AND paid),
    'first_paid_boss_run', (
        SELECT row_to_json(r) FROM (
            SELECT slot,planned,state,paid FROM revora_daily_boss_runs
            WHERE killer=4294907001 AND paid ORDER BY planned LIMIT 1) r));
ROLLBACK;
