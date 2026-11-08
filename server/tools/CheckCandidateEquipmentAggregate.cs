using System;
using System.Reflection;
using Npgsql;
using Revora;
using Revora.Database;

internal static partial class CheckCandidateArenaLifecycle
{
    static void EquipmentAggregateProcess() {
        var feature=Feature("ProgressionEvidence");int checkedCases=0;
        using(var c=DataHolder.MySqlConnection) {
            c.Open();using(var t=c.BeginTransaction()) {
                Func<string,object> sql=statement=>{using(var q=new NpgsqlCommand(statement,c,t)){q.Parameters.AddWithValue("uid",(long)FirstUid);return q.ExecuteScalar();}};
                Action record=()=>Call(feature,"RecordEquipmentQuestAggregate",c,t,FirstUid);
                Func<long> count=()=>Convert.ToInt64(sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10931"));
                sql(@"CREATE TEMP TABLE revora_achievement_evidence(uid bigint,achievement_id integer,source text,source_key text,rule_version integer,PRIMARY KEY(uid,achievement_id));
SET LOCAL search_path=pg_temp;");
                record();Require(count()==0);
                sql(@"CREATE TEMP TABLE revora_connector(uid bigint,life integer,stage integer,rewards bigint[]);
INSERT INTO revora_connector VALUES(@uid,0,6,ARRAY[1000,1001,1002]::bigint[]);
CREATE TEMP TABLE revora_spun_gold(uid bigint,life integer,stage integer,rewards bigint[]);
INSERT INTO revora_spun_gold VALUES(@uid,1,8,ARRAY[1010,1011]::bigint[]);
CREATE TEMP TABLE revora_azure_pill(uid bigint,life integer,stage integer,rewards bigint[]);
INSERT INTO revora_azure_pill VALUES(@uid,2,7,ARRAY[1020,1021]::bigint[]);
CREATE TEMP TABLE revora_artisan_feather(uid bigint,life integer,stage integer,rewards bigint[]);
INSERT INTO revora_artisan_feather VALUES(@uid,0,6,ARRAY[1030,1031]::bigint[]);
CREATE TEMP TABLE revora_mystic_ring(uid bigint,life integer,stage integer,rewards bigint[]);
INSERT INTO revora_mystic_ring VALUES(@uid,1,10,ARRAY[1040,1041]::bigint[]);
CREATE TEMP TABLE revora_frantic_monkeys(uid bigint,life integer,stage integer,rewards bigint[]);
INSERT INTO revora_frantic_monkeys VALUES(@uid,2,10,ARRAY[1050,1051]::bigint[]);
CREATE TEMP TABLE revora_soul_general(uid bigint,life integer,stage integer,rewards bigint[]);
INSERT INTO revora_soul_general VALUES(@uid,0,12,ARRAY[1060,1061]::bigint[]);
CREATE TEMP TABLE revora_mad_man(uid bigint,life integer,stage integer,rewards bigint[]);
INSERT INTO revora_mad_man VALUES(@uid,1,9,ARRAY[1070,1071,1072]::bigint[]);
CREATE TEMP TABLE revora_ghost_city(uid bigint,life integer,stage integer,rewards bigint[]);
INSERT INTO revora_ghost_city VALUES(@uid,2,11,ARRAY[1080,1081,1082]::bigint[]);
CREATE TEMP TABLE revora_tomboy(uid bigint,life integer,stage integer,rewards bigint[]);
INSERT INTO revora_tomboy VALUES(@uid,0,6,ARRAY[1090,1091,1092]::bigint[]);
CREATE TEMP TABLE revora_desert_blood(uid bigint,life integer,stage integer,rewards bigint[]);
INSERT INTO revora_desert_blood VALUES(@uid,1,11,ARRAY[1100,1101,1102]::bigint[]);
CREATE TEMP TABLE revora_hero_wings(uid bigint,life integer,stage integer,rewards bigint[]);
INSERT INTO revora_hero_wings VALUES(@uid,2,12,ARRAY[1110,1111,1112]::bigint[]);
CREATE TEMP TABLE revora_family_feud(uid bigint,life integer,stage integer,rewards bigint[]);
INSERT INTO revora_family_feud VALUES(@uid,0,10,ARRAY[1120,1121,1122]::bigint[]);
CREATE TEMP TABLE revora_desert_treasure(uid bigint,life integer,stage integer,rewards bigint[]);
INSERT INTO revora_desert_treasure VALUES(@uid,1,8,ARRAY[1130,1131,1132]::bigint[]);
CREATE TEMP TABLE revora_desert_expedition(uid bigint,life integer,stage integer,rewards bigint[]);
INSERT INTO revora_desert_expedition VALUES(@uid,2,6,ARRAY[1140,1141,1142]::bigint[]);
CREATE TEMP TABLE revora_power_book(uid bigint,life integer,stage integer,rewards bigint[]);
INSERT INTO revora_power_book VALUES(@uid,0,14,ARRAY[1150,1151]::bigint[]);
CREATE TEMP TABLE revora_defense_talisman(uid bigint,life integer,stage integer,rewards bigint[]);
INSERT INTO revora_defense_talisman VALUES(@uid,1,10,ARRAY[1160,1161]::bigint[]);
CREATE TEMP TABLE revora_attack_talisman(uid bigint,life integer,stage integer,rewards bigint[]);
INSERT INTO revora_attack_talisman VALUES(@uid,2,11,ARRAY[1170,1171]::bigint[]);
CREATE TEMP TABLE revora_tempest_wings(uid bigint,life integer,stage integer,rewards bigint[]);
INSERT INTO revora_tempest_wings VALUES(@uid,0,4,ARRAY[1180,1181]::bigint[]);
CREATE TEMP TABLE revora_power_book_rewards(uid bigint,life integer,pack_uid bigint,soul_uid bigint);
INSERT INTO revora_power_book_rewards VALUES(@uid,0,9000,9001);");
                foreach(string mutation in new[]{"UPDATE revora_connector SET uid=@uid+1 WHERE uid=@uid",
"UPDATE revora_connector SET stage=0 WHERE uid=@uid",
"UPDATE revora_connector SET life=3 WHERE uid=@uid",
"UPDATE revora_connector SET rewards=NULL WHERE uid=@uid",
"UPDATE revora_connector SET rewards='{}'::bigint[] WHERE uid=@uid",
"UPDATE revora_connector SET rewards=ARRAY[1,1,1]::bigint[] WHERE uid=@uid",
"UPDATE revora_connector SET rewards=ARRAY[NULL,5,6]::bigint[] WHERE uid=@uid",
"UPDATE revora_connector SET rewards=ARRAY[-1,5,6]::bigint[] WHERE uid=@uid",
"UPDATE revora_spun_gold SET uid=@uid+1 WHERE uid=@uid",
"UPDATE revora_spun_gold SET stage=0 WHERE uid=@uid",
"UPDATE revora_spun_gold SET life=3 WHERE uid=@uid",
"UPDATE revora_spun_gold SET rewards=NULL WHERE uid=@uid",
"UPDATE revora_spun_gold SET rewards='{}'::bigint[] WHERE uid=@uid",
"UPDATE revora_spun_gold SET rewards=ARRAY[1,1]::bigint[] WHERE uid=@uid",
"UPDATE revora_spun_gold SET rewards=ARRAY[NULL,5]::bigint[] WHERE uid=@uid",
"UPDATE revora_spun_gold SET rewards=ARRAY[-1,5]::bigint[] WHERE uid=@uid",
"UPDATE revora_azure_pill SET uid=@uid+1 WHERE uid=@uid",
"UPDATE revora_azure_pill SET stage=0 WHERE uid=@uid",
"UPDATE revora_azure_pill SET life=3 WHERE uid=@uid",
"UPDATE revora_azure_pill SET rewards=NULL WHERE uid=@uid",
"UPDATE revora_azure_pill SET rewards='{}'::bigint[] WHERE uid=@uid",
"UPDATE revora_azure_pill SET rewards=ARRAY[1,1]::bigint[] WHERE uid=@uid",
"UPDATE revora_azure_pill SET rewards=ARRAY[NULL,5]::bigint[] WHERE uid=@uid",
"UPDATE revora_azure_pill SET rewards=ARRAY[-1,5]::bigint[] WHERE uid=@uid",
"UPDATE revora_artisan_feather SET uid=@uid+1 WHERE uid=@uid",
"UPDATE revora_artisan_feather SET stage=0 WHERE uid=@uid",
"UPDATE revora_artisan_feather SET life=3 WHERE uid=@uid",
"UPDATE revora_artisan_feather SET rewards=NULL WHERE uid=@uid",
"UPDATE revora_artisan_feather SET rewards='{}'::bigint[] WHERE uid=@uid",
"UPDATE revora_artisan_feather SET rewards=ARRAY[1,1]::bigint[] WHERE uid=@uid",
"UPDATE revora_artisan_feather SET rewards=ARRAY[NULL,5]::bigint[] WHERE uid=@uid",
"UPDATE revora_artisan_feather SET rewards=ARRAY[-1,5]::bigint[] WHERE uid=@uid",
"UPDATE revora_mystic_ring SET uid=@uid+1 WHERE uid=@uid",
"UPDATE revora_mystic_ring SET stage=0 WHERE uid=@uid",
"UPDATE revora_mystic_ring SET life=3 WHERE uid=@uid",
"UPDATE revora_mystic_ring SET rewards=NULL WHERE uid=@uid",
"UPDATE revora_mystic_ring SET rewards='{}'::bigint[] WHERE uid=@uid",
"UPDATE revora_mystic_ring SET rewards=ARRAY[1,1]::bigint[] WHERE uid=@uid",
"UPDATE revora_mystic_ring SET rewards=ARRAY[NULL,5]::bigint[] WHERE uid=@uid",
"UPDATE revora_mystic_ring SET rewards=ARRAY[-1,5]::bigint[] WHERE uid=@uid",
"UPDATE revora_frantic_monkeys SET uid=@uid+1 WHERE uid=@uid",
"UPDATE revora_frantic_monkeys SET stage=0 WHERE uid=@uid",
"UPDATE revora_frantic_monkeys SET life=3 WHERE uid=@uid",
"UPDATE revora_frantic_monkeys SET rewards=NULL WHERE uid=@uid",
"UPDATE revora_frantic_monkeys SET rewards='{}'::bigint[] WHERE uid=@uid",
"UPDATE revora_frantic_monkeys SET rewards=ARRAY[1,1]::bigint[] WHERE uid=@uid",
"UPDATE revora_frantic_monkeys SET rewards=ARRAY[NULL,5]::bigint[] WHERE uid=@uid",
"UPDATE revora_frantic_monkeys SET rewards=ARRAY[-1,5]::bigint[] WHERE uid=@uid",
"UPDATE revora_soul_general SET uid=@uid+1 WHERE uid=@uid",
"UPDATE revora_soul_general SET stage=0 WHERE uid=@uid",
"UPDATE revora_soul_general SET life=3 WHERE uid=@uid",
"UPDATE revora_soul_general SET rewards=NULL WHERE uid=@uid",
"UPDATE revora_soul_general SET rewards='{}'::bigint[] WHERE uid=@uid",
"UPDATE revora_soul_general SET rewards=ARRAY[1,1]::bigint[] WHERE uid=@uid",
"UPDATE revora_soul_general SET rewards=ARRAY[NULL,5]::bigint[] WHERE uid=@uid",
"UPDATE revora_soul_general SET rewards=ARRAY[-1,5]::bigint[] WHERE uid=@uid",
"UPDATE revora_mad_man SET uid=@uid+1 WHERE uid=@uid",
"UPDATE revora_mad_man SET stage=0 WHERE uid=@uid",
"UPDATE revora_mad_man SET life=3 WHERE uid=@uid",
"UPDATE revora_mad_man SET rewards=NULL WHERE uid=@uid",
"UPDATE revora_mad_man SET rewards='{}'::bigint[] WHERE uid=@uid",
"UPDATE revora_mad_man SET rewards=ARRAY[1,1,1]::bigint[] WHERE uid=@uid",
"UPDATE revora_mad_man SET rewards=ARRAY[NULL,5,6]::bigint[] WHERE uid=@uid",
"UPDATE revora_mad_man SET rewards=ARRAY[-1,5,6]::bigint[] WHERE uid=@uid",
"UPDATE revora_ghost_city SET uid=@uid+1 WHERE uid=@uid",
"UPDATE revora_ghost_city SET stage=0 WHERE uid=@uid",
"UPDATE revora_ghost_city SET life=3 WHERE uid=@uid",
"UPDATE revora_ghost_city SET rewards=NULL WHERE uid=@uid",
"UPDATE revora_ghost_city SET rewards='{}'::bigint[] WHERE uid=@uid",
"UPDATE revora_ghost_city SET rewards=ARRAY[1,1,1]::bigint[] WHERE uid=@uid",
"UPDATE revora_ghost_city SET rewards=ARRAY[NULL,5,6]::bigint[] WHERE uid=@uid",
"UPDATE revora_ghost_city SET rewards=ARRAY[-1,5,6]::bigint[] WHERE uid=@uid",
"UPDATE revora_tomboy SET uid=@uid+1 WHERE uid=@uid",
"UPDATE revora_tomboy SET stage=0 WHERE uid=@uid",
"UPDATE revora_tomboy SET life=3 WHERE uid=@uid",
"UPDATE revora_tomboy SET rewards=NULL WHERE uid=@uid",
"UPDATE revora_tomboy SET rewards='{}'::bigint[] WHERE uid=@uid",
"UPDATE revora_tomboy SET rewards=ARRAY[1,1,1]::bigint[] WHERE uid=@uid",
"UPDATE revora_tomboy SET rewards=ARRAY[NULL,5,6]::bigint[] WHERE uid=@uid",
"UPDATE revora_tomboy SET rewards=ARRAY[-1,5,6]::bigint[] WHERE uid=@uid",
"UPDATE revora_desert_blood SET uid=@uid+1 WHERE uid=@uid",
"UPDATE revora_desert_blood SET stage=0 WHERE uid=@uid",
"UPDATE revora_desert_blood SET life=3 WHERE uid=@uid",
"UPDATE revora_desert_blood SET rewards=NULL WHERE uid=@uid",
"UPDATE revora_desert_blood SET rewards='{}'::bigint[] WHERE uid=@uid",
"UPDATE revora_desert_blood SET rewards=ARRAY[1,1,1]::bigint[] WHERE uid=@uid",
"UPDATE revora_desert_blood SET rewards=ARRAY[NULL,5,6]::bigint[] WHERE uid=@uid",
"UPDATE revora_desert_blood SET rewards=ARRAY[-1,5,6]::bigint[] WHERE uid=@uid",
"UPDATE revora_hero_wings SET uid=@uid+1 WHERE uid=@uid",
"UPDATE revora_hero_wings SET stage=0 WHERE uid=@uid",
"UPDATE revora_hero_wings SET life=3 WHERE uid=@uid",
"UPDATE revora_hero_wings SET rewards=NULL WHERE uid=@uid",
"UPDATE revora_hero_wings SET rewards='{}'::bigint[] WHERE uid=@uid",
"UPDATE revora_hero_wings SET rewards=ARRAY[1,1,1]::bigint[] WHERE uid=@uid",
"UPDATE revora_hero_wings SET rewards=ARRAY[NULL,5,6]::bigint[] WHERE uid=@uid",
"UPDATE revora_hero_wings SET rewards=ARRAY[-1,5,6]::bigint[] WHERE uid=@uid",
"UPDATE revora_family_feud SET uid=@uid+1 WHERE uid=@uid",
"UPDATE revora_family_feud SET stage=0 WHERE uid=@uid",
"UPDATE revora_family_feud SET life=3 WHERE uid=@uid",
"UPDATE revora_family_feud SET rewards=NULL WHERE uid=@uid",
"UPDATE revora_family_feud SET rewards='{}'::bigint[] WHERE uid=@uid",
"UPDATE revora_family_feud SET rewards=ARRAY[1,1,1]::bigint[] WHERE uid=@uid",
"UPDATE revora_family_feud SET rewards=ARRAY[NULL,5,6]::bigint[] WHERE uid=@uid",
"UPDATE revora_family_feud SET rewards=ARRAY[-1,5,6]::bigint[] WHERE uid=@uid",
"UPDATE revora_desert_treasure SET uid=@uid+1 WHERE uid=@uid",
"UPDATE revora_desert_treasure SET stage=0 WHERE uid=@uid",
"UPDATE revora_desert_treasure SET life=3 WHERE uid=@uid",
"UPDATE revora_desert_treasure SET rewards=NULL WHERE uid=@uid",
"UPDATE revora_desert_treasure SET rewards='{}'::bigint[] WHERE uid=@uid",
"UPDATE revora_desert_treasure SET rewards=ARRAY[1,1,1]::bigint[] WHERE uid=@uid",
"UPDATE revora_desert_treasure SET rewards=ARRAY[NULL,5,6]::bigint[] WHERE uid=@uid",
"UPDATE revora_desert_treasure SET rewards=ARRAY[-1,5,6]::bigint[] WHERE uid=@uid",
"UPDATE revora_desert_expedition SET uid=@uid+1 WHERE uid=@uid",
"UPDATE revora_desert_expedition SET stage=0 WHERE uid=@uid",
"UPDATE revora_desert_expedition SET life=3 WHERE uid=@uid",
"UPDATE revora_desert_expedition SET rewards=NULL WHERE uid=@uid",
"UPDATE revora_desert_expedition SET rewards='{}'::bigint[] WHERE uid=@uid",
"UPDATE revora_desert_expedition SET rewards=ARRAY[1,1,1]::bigint[] WHERE uid=@uid",
"UPDATE revora_desert_expedition SET rewards=ARRAY[NULL,5,6]::bigint[] WHERE uid=@uid",
"UPDATE revora_desert_expedition SET rewards=ARRAY[-1,5,6]::bigint[] WHERE uid=@uid",
"UPDATE revora_power_book SET uid=@uid+1 WHERE uid=@uid",
"UPDATE revora_power_book SET stage=0 WHERE uid=@uid",
"UPDATE revora_power_book SET life=3 WHERE uid=@uid",
"UPDATE revora_defense_talisman SET uid=@uid+1 WHERE uid=@uid",
"UPDATE revora_defense_talisman SET stage=0 WHERE uid=@uid",
"UPDATE revora_defense_talisman SET rewards=NULL WHERE uid=@uid",
"UPDATE revora_defense_talisman SET rewards='{}'::bigint[] WHERE uid=@uid",
"UPDATE revora_defense_talisman SET rewards=ARRAY[1,1]::bigint[] WHERE uid=@uid",
"UPDATE revora_defense_talisman SET rewards=ARRAY[NULL,5]::bigint[] WHERE uid=@uid",
"UPDATE revora_defense_talisman SET rewards=ARRAY[-1,5]::bigint[] WHERE uid=@uid",
"UPDATE revora_attack_talisman SET uid=@uid+1 WHERE uid=@uid",
"UPDATE revora_attack_talisman SET stage=0 WHERE uid=@uid",
"UPDATE revora_attack_talisman SET rewards=NULL WHERE uid=@uid",
"UPDATE revora_attack_talisman SET rewards='{}'::bigint[] WHERE uid=@uid",
"UPDATE revora_attack_talisman SET rewards=ARRAY[1,1]::bigint[] WHERE uid=@uid",
"UPDATE revora_attack_talisman SET rewards=ARRAY[NULL,5]::bigint[] WHERE uid=@uid",
"UPDATE revora_attack_talisman SET rewards=ARRAY[-1,5]::bigint[] WHERE uid=@uid",
"UPDATE revora_tempest_wings SET uid=@uid+1 WHERE uid=@uid",
"UPDATE revora_tempest_wings SET stage=0 WHERE uid=@uid",
"UPDATE revora_tempest_wings SET rewards=NULL WHERE uid=@uid",
"UPDATE revora_tempest_wings SET rewards='{}'::bigint[] WHERE uid=@uid",
"UPDATE revora_tempest_wings SET rewards=ARRAY[1,1]::bigint[] WHERE uid=@uid",
"UPDATE revora_tempest_wings SET rewards=ARRAY[NULL,5]::bigint[] WHERE uid=@uid",
"UPDATE revora_tempest_wings SET rewards=ARRAY[-1,5]::bigint[] WHERE uid=@uid",
"UPDATE revora_power_book_rewards SET life=1",
"UPDATE revora_power_book_rewards SET uid=@uid+1",
"UPDATE revora_power_book_rewards SET soul_uid=pack_uid",
"UPDATE revora_power_book_rewards SET soul_uid=0",
"ALTER TABLE revora_tempest_wings RENAME TO absent_tempest"}) {
                    sql("SAVEPOINT invalid_receipt");sql(mutation);record();Require(count()==0);
                    sql("ROLLBACK TO SAVEPOINT invalid_receipt");sql("RELEASE SAVEPOINT invalid_receipt");checkedCases++;
                }
                sql(@"SAVEPOINT suppressed;
CREATE FUNCTION pg_temp.suppress_equipment_award() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER suppress_equipment_award BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW EXECUTE FUNCTION pg_temp.suppress_equipment_award();");
                bool failed=false;try{record();}catch(TargetInvocationException ex){failed=ex.InnerException is InvalidOperationException && ex.InnerException.Message=="Achievement completion was not saved.";}
                Require(failed && count()==0);sql("ROLLBACK TO SAVEPOINT suppressed");sql("RELEASE SAVEPOINT suppressed");
                // No items table: consuming rewards must not erase saved completion.
                sql("SAVEPOINT outer_completion");record();Require(count()==1);
                sql("ROLLBACK TO SAVEPOINT outer_completion");Require(count()==0);sql("RELEASE SAVEPOINT outer_completion");
                record();record();Require(count()==1);
                Require((bool)sql("SELECT source='equipment-quests-revora-v1' AND source_key='nineteen-chains-cross-life' AND rule_version=1 FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10931"));
                t.Rollback();
            }
        }
        System.Console.WriteLine("[equipment-process] invalid-receipt-cases="+checkedCases);
        System.Console.WriteLine("[equipment-process] equipment-aggregate-passed");
        Server.CommandsAI("@close");throw new Exception("Equipment aggregate did not close.");
    }
}
