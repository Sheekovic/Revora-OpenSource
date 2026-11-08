using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Game;

internal static partial class PotencyHuntTests
{
    static void VerifyDisRunRules(GameState p, Map town) {
        var rules = typeof(Entity).Assembly.GetType("Revora.Game.Features.DisCityRunRules");
        Guid run;
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT run FROM revora_dis_runs WHERE uid=@uid",c)){
            q.Parameters.AddWithValue("uid",(long)p.Entity.UID);run=(Guid)q.ExecuteScalar();}}
        var saved=Call(rules,null,"Load",p.Entity.UID,run);
        Check(Convert.ToInt32(Field(saved,"Difficulty"))==0 && Convert.ToInt32(Field(saved,"Revision"))==1
            && Convert.ToUInt32(Field(saved,"OrdinaryHp"))==100000 && Convert.ToUInt32(Field(saved,"GuardianHp"))==500000
            && Convert.ToUInt32(Field(saved,"BossHp"))==5000000,
            "ordinary Dis City admission freezes the existing combat edition with its mirror and run");
        bool denied=false;try{Call(rules,null,"Load",p.Entity.UID+1,run);}catch(InvalidOperationException){denied=true;}
        Check(denied,"another character cannot resolve a foreign Dis City combat contract");
        Query("DELETE FROM revora_dis_run_rules WHERE run=@run","run",run);
        Call(rules,null,"Initialize");Call(rules,null,"Initialize");
        Check(Scalar("SELECT count(*) FROM revora_dis_run_rules WHERE uid=@uid AND difficulty=0 AND revision=1",p.Entity.UID)==1,
            "legacy current runs acquire the unchanged standard edition exactly once");
        foreach(int difficulty in new[]{1,2}) {
            var hero=DisPlayer(town);
            DisNpc(hero,3215,(byte)(10+difficulty));
            Check(Scalar("SELECT count(*) FROM revora_dis_runs WHERE uid=@uid",hero.Entity.UID)==0,"difficulty admission rejects an unsolicited selection");
            DisNpc(hero,3215,7);disNow=disNow.AddMinutes(3);DisNpc(hero,3215,(byte)(10+difficulty));
            Check(Scalar("SELECT count(*) FROM revora_dis_runs WHERE uid=@uid",hero.Entity.UID)==0,"difficulty admission rejects an expired offer");
            DisNpc(hero,3215,7);DisNpc(hero,3215,(byte)(10+difficulty));
            Check(!Fenced(hero) && Scalar("SELECT difficulty FROM revora_dis_run_rules WHERE uid=@uid",hero.Entity.UID)==difficulty
                && hero.Map.Entities.Values.Any(m=>m.EntityFlag==EntityFlag.Monster && m.Hitpoints==100000*(difficulty+1)),"chosen difficulty persists and scales actual ordinary monsters");
            var instances=(System.Collections.IDictionary)DisWorld.GetField("Players",Hidden).GetValue(null);
            var instance=instances[hero.Entity.UID];
            Call(DisWorld,null,"Spawn",instance,-1,true);
            Check(hero.Map.Entities.Values.Any(m=>m.MonsterInfo!=null && m.MonsterInfo.Boss
                && m.MaxHitpoints==5000000*(difficulty+1) && m.MinAttack==5500*(4+difficulty)/4
                && m.MaxAttack==7500*(4+difficulty)/4),"harder Pluto uses its full scaled HP and attack range");
            Call(DisWorld,null,"Home",hero,false);Call(DisWorld,null,"Release",instance);
            Call(rules,null,"Initialize");DisNpc(hero,3215,7);DisNpc(hero,3215,10);
            Check(!Fenced(hero) && Scalar("SELECT difficulty FROM revora_dis_run_rules WHERE uid=@uid",hero.Entity.UID)==difficulty
                && hero.Map.Entities.Values.Any(m=>m.EntityFlag==EntityFlag.Monster && m.Hitpoints==100000*(difficulty+1)),"resume after reinitialization cannot downgrade an unfinished trial");
            instance=instances[hero.Entity.UID];Call(DisWorld,null,"Home",hero,false);Call(DisWorld,null,"Release",instance);
        }
        var failed=DisPlayer(town);
        Query("CREATE FUNCTION dis_rules_fail() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER dis_rules_fail BEFORE INSERT ON revora_dis_run_rules FOR EACH ROW EXECUTE FUNCTION dis_rules_fail()");
        try{DisNpc(failed,3215,1);}finally{Query("DROP TRIGGER dis_rules_fail ON revora_dis_run_rules;DROP FUNCTION dis_rules_fail()");}
        Check(Fenced(failed) && Scalar("SELECT count(*) FROM revora_dis_runs WHERE uid=@uid",failed.Entity.UID)==0
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=3006981",failed.Entity.UID)==0,
            "failed combat-contract write rolls back Dis City admission and mirror issuance");
    }
}
