using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshTowerSnapshot(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,
'run',(SELECT json_agg(r ORDER BY uid) FROM revora_defense_talisman r WHERE uid=@uid),
'materials',(SELECT json_agg(r ORDER BY item_uid) FROM revora_defense_talisman_materials r WHERE uid=@uid),
'recoveries',(SELECT json_agg(r ORDER BY old_uid) FROM revora_defense_talisman_recoveries r WHERE uid=@uid),
'purchases',(SELECT json_agg(r ORDER BY item_uid) FROM revora_defense_talisman_purchases r WHERE uid=@uid),
'fish',(SELECT json_agg(r ORDER BY attempt) FROM revora_defense_talisman_fishing r WHERE uid=@uid),
'shawn',(SELECT json_agg(r ORDER BY attempt) FROM revora_tower_shawn_kills r WHERE uid=@uid),
'desert',(SELECT json_agg(r ORDER BY kind) FROM revora_tower_desert_kills r WHERE uid=@uid),
'generals',(SELECT json_agg(r ORDER BY slot) FROM revora_tower_general_kills r WHERE uid=@uid),
'feng',(SELECT json_agg(r ORDER BY uid) FROM revora_tower_feng_kills r WHERE uid=@uid),
'visits',(SELECT json_agg(r ORDER BY visit) FROM revora_tower_visits r WHERE uid=@uid),
'house',(SELECT json_agg(r ORDER BY attempt) FROM revora_tower_house_kills r WHERE uid=@uid))::text","prior",FreshPowerSnapshot(p),"uid",(long)p.Entity.UID));
    }
    static void FreshTowerSave(GameState p,string stage,int kills,int hits) {
        Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_tower_probe SET stage=@stage,kills=@kills,hits=@hits,snapshot=@snapshot","stage",stage,"kills",kills,"hits",hits,"snapshot",FreshTowerSnapshot(p));
    }
    static void FreshTowerProcess(string phase) {
        Require((bool)Feature("DefenseTalismanQuest").GetProperty("Enabled",Hidden).GetValue(null,null));
        var p=FreshLoadSaved(true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_tower_probe') IS NULL")) {
            Require(phase.EndsWith("earn") && FreshPowerSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_power_probe")) && (long)Sql("SELECT count(*) FROM revora_defense_talisman WHERE uid=@uid","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_tower_probe(stage text,kills integer,hits integer,snapshot text);INSERT INTO revora_fresh_tower_probe VALUES('new',0,0,@snapshot)","snapshot",FreshTowerSnapshot(p));
        }
        Require(FreshTowerSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_tower_probe")));
        string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_tower_probe"));int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_tower_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_tower_probe"));
        if(phase.EndsWith("earn")) {
            Require(stage=="new");var clock=Feature("DefenseTalismanStore").GetField("Clock",Hidden);object original=clock.GetValue(null);DateTime now=DateTime.UtcNow;
            Action tick=()=>{now=now.AddSeconds(2);Call(Feature("DefenseTalismanQuest"),"Tick");};
            Action kill=()=>{var body=p.Map.Entities.Values.Single(e=>e.MonsterInfo!=null && e.MonsterInfo.ID>=9905200 && e.MonsterInfo.ID<9905211 && !e.Dead);FreshClassicKill(p,p.Entity.MapID,body.MonsterInfo.ID,ref kills,ref hits);tick();};
            Action<uint,byte> talk=(npc,opt)=>{if(npc==4567 || npc==4568 || npc==4574 || npc==4578){HeartTalk(p,npc,opt);Require(!PirateFenced(p));}else FreshClassicTalk(p,npc==4572 || npc==4573?(ushort)1000:(ushort)1015,npc,opt);};
            Func<int> step=()=>Convert.ToInt32(Sql("SELECT stage FROM revora_defense_talisman WHERE uid=@uid","uid",(long)p.Entity.UID));
            try {
                clock.SetValue(null,(Func<DateTime>)(()=>now));talk(4565,1);talk(4565,1);Require(p.Map.BaseID==1782);
                p.Entity.X=40;p.Entity.Y=57;tick();for(int i=0;i<5;i++)kill();talk(4567,1);talk(4566,1);Require(step()==3);
                for(int i=0;i<3;i++){ulong silver=p.Entity.Money;talk(4579,1);Require(p.Entity.Money==silver-30000);string paid=FreshTowerSnapshot(p);talk(4579,1);Require(paid==FreshTowerSnapshot(p));p.Entity.X=310;p.Entity.Y=380;TowerUse(p,721779);kill();}
                talk(4569,1);talk(4575,1);talk(4576,1);talk(4577,1);Require(step()==4 && p.Map.BaseID==1784);
                talk(4568,1);talk(4568,2);p.Entity.Teleport(1015,400,650);now=now.AddMinutes(10-now.Minute%10);tick();kill();talk(4577,1);talk(4568,1);Require(step()==5);
                talk(4572,1);p.Entity.X=166;p.Entity.Y=226;tick();kill();kill();talk(4566,1);talk(4566,1);Require(step()==7 && p.Map.BaseID==1783);
                p.Entity.X=40;p.Entity.Y=57;tick();for(int i=0;i<5;i++)kill();talk(4574,1);talk(4573,1);Require(step()==8 && p.Map.BaseID==1785);
                ushort[,] stands={{96,124},{70,64},{113,126},{62,85}};
                for(int i=0;i<4;i++){p.Entity.X=stands[i,0];p.Entity.Y=stands[i,1];tick();kill();if(i==1 || i==3){p.Entity.X=(ushort)(i==1?70:62);p.Entity.Y=(ushort)(i==1?56:77);TowerUse(p,721786);}}
                Require(p.Map.BaseID==1787);tick();kill();for(byte i=10;i<14;i++)talk(4578,i);talk(4578,1);Require(step()==9 && p.Entity.MapID==1015);talk(4565,0);
            } finally {clock.SetValue(null,original);}
            Require((long)Sql("SELECT count(*) FROM revora_defense_talisman WHERE uid=@uid AND stage=9 AND cardinality(rewards)=0","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_defense_talisman_purchases WHERE uid=@uid AND price=30000","uid",(long)p.Entity.UID)==3 && (long)Sql("SELECT count(*) FROM revora_tower_house_kills WHERE uid=@uid","uid",(long)p.Entity.UID)==10 && (long)Sql("SELECT count(*) FROM revora_tower_general_kills WHERE uid=@uid","uid",(long)p.Entity.UID)==4);FreshTowerSave(p,"ready",kills,hits);
        } else if(phase.EndsWith("claim")) {
            Require(stage=="ready");var prior=p.Inventory.Objects.Where(i=>i.ID!=721785).Select(i=>i.UID).ToArray();FreshClassicTalk(p,1015,4565,1);var added=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(added.Length==2 && added.All(i=>i.Bound) && prior.All(uid=>p.Inventory.ContainsUID(uid)));
            Require((long)Sql("SELECT count(*) FROM revora_defense_talisman WHERE uid=@uid AND stage=10 AND cardinality(rewards)=2","uid",(long)p.Entity.UID)==1);FreshTowerSave(p,"paid",kills,hits);
        } else Require(phase.EndsWith("reload") && stage=="paid");
        if(phase.EndsWith("claim") || phase.EndsWith("reload")) {
            var ids=(long[])Sql("SELECT rewards FROM revora_defense_talisman WHERE uid=@uid","uid",(long)p.Entity.UID);Require(ids.Length==2 && ids.Distinct().Count()==2);var rewards=p.Inventory.Objects.Where(i=>ids.Contains((long)i.UID)).ToArray();Require(rewards.Length==2 && rewards.All(i=>i.Bound) && rewards.Count(i=>i.ID==202008)==1 && rewards.Count(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1);
            string before=FreshTowerSnapshot(p);FreshClassicTalk(p,1015,4565,1);Require(before==FreshTowerSnapshot(p));
        }
        Require(FreshTowerSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_tower_probe")) && !PirateFenced(p));System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" level="+p.Entity.Level+" kills="+kills+" hits="+hits+" bag="+p.Inventory.Count);Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Defense Talisman did not close.");
    }
}
