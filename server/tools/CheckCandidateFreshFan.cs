using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshFanSnapshot(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,
'run',(SELECT json_agg(r ORDER BY uid) FROM revora_attack_talisman r WHERE uid=@uid),
'materials',(SELECT json_agg(r ORDER BY item_uid) FROM revora_attack_talisman_materials r WHERE uid=@uid),
'recoveries',(SELECT json_agg(r ORDER BY old_uid) FROM revora_attack_talisman_recoveries r WHERE uid=@uid),
'purchases',(SELECT json_agg(r ORDER BY item_uid) FROM revora_attack_talisman_purchases r WHERE uid=@uid),
'ghost',(SELECT json_agg(r ORDER BY box) FROM revora_fan_ghost_kills r WHERE uid=@uid),
'courtesan',(SELECT json_agg(r ORDER BY uid) FROM revora_fan_courtesan_kills r WHERE uid=@uid),
'lake',(SELECT json_agg(r ORDER BY uid) FROM revora_fan_lake_kills r WHERE uid=@uid),
'ambush',(SELECT json_agg(r ORDER BY slot) FROM revora_fan_ambush_kills r WHERE uid=@uid),
'cave',(SELECT json_agg(r ORDER BY kind) FROM revora_fan_cave_kills r WHERE uid=@uid),
'visits',(SELECT json_agg(r ORDER BY visit) FROM revora_fan_visits r WHERE uid=@uid))::text","prior",FreshTowerSnapshot(p),"uid",(long)p.Entity.UID));
    }
    static void FreshFanSave(GameState p,string stage,int kills,int hits) {
        Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_fan_probe SET stage=@stage,kills=@kills,hits=@hits,snapshot=@snapshot","stage",stage,"kills",kills,"hits",hits,"snapshot",FreshFanSnapshot(p));
    }
    static void FreshFanProcess(string phase) {
        Require((bool)Feature("AttackTalismanQuest").GetProperty("Enabled",Hidden).GetValue(null,null));
        var p=FreshLoadSaved(true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_fan_probe') IS NULL")) {
            Require(phase.EndsWith("earn") && FreshTowerSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_tower_probe")) && (long)Sql("SELECT count(*) FROM revora_attack_talisman WHERE uid=@uid","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_fan_probe(stage text,kills integer,hits integer,snapshot text);INSERT INTO revora_fresh_fan_probe VALUES('new',0,0,@snapshot)","snapshot",FreshFanSnapshot(p));
        }
        Require(FreshFanSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_fan_probe")));
        string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_fan_probe"));int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_fan_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_fan_probe"));
        if(phase.EndsWith("earn")) {
            Require(stage=="new");var clock=Feature("AttackTalismanStore").GetField("Clock",Hidden);object original=clock.GetValue(null);DateTime now=DateTime.UtcNow;ulong before=p.Entity.Money;
            Action tick=()=>{now=now.AddSeconds(2);Call(Feature("AttackTalismanQuest"),"Tick");};
            Action kill=()=>{tick();var body=p.Map.Entities.Values.Single(e=>e.MonsterInfo!=null && e.MonsterInfo.ID>=9905400 && e.MonsterInfo.ID<9905407 && !e.Dead);FreshClassicKill(p,p.Entity.MapID,body.MonsterInfo.ID,ref kills,ref hits);tick();};
            Action<uint,byte> talk=(npc,opt)=>{ushort map=npc==4565 || npc>=9910410 && npc<=9910414?(ushort)1015:npc==4583?(ushort)1000:npc==4581 || npc==4582 || npc==9910430?(ushort)1011:p.Entity.MapID;FreshClassicTalk(p,map,npc,opt);};
            Func<int> step=()=>Convert.ToInt32(Sql("SELECT stage FROM revora_attack_talisman WHERE uid=@uid","uid",(long)p.Entity.UID));
            try {
                clock.SetValue(null,(Func<DateTime>)(()=>now));talk(4565,80);talk(4565,81);talk(4582,1);talk(4581,1);string paid=FreshFanSnapshot(p);talk(4581,1);Require(paid==FreshFanSnapshot(p) && step()==2 && p.Entity.Money==before+100-10000);
                for(int i=0;p.Inventory.Objects.Count(item=>item.ID==710220)<5 && i<200;i++)FreshDesertHeroKill(p,1011,new uint[]{6,65},ref kills,ref hits);Require(p.Inventory.Objects.Count(i=>i.ID==710220)==5);
                talk(4582,1);talk(4565,81);talk(4583,1);ulong cave=p.Entity.Money;talk(4583,2);Require(step()==5 && p.Map.BaseID==1025 && p.Entity.Money==cave-10000);
                p.Entity.X=54;p.Entity.Y=50;kill();p.Entity.X=58;p.Entity.Y=50;kill();TowerUse(p,721806);Require(p.Entity.MapID==1000);
                talk(4583,1);TowerUse(p,721802);TowerUse(p,721804);talk(4582,1);Require(step()==7);p.Entity.X=790;p.Entity.Y=460;for(int i=0;i<5;i++)kill();talk(4582,1);Require(step()==8);
                talk(9910430,1);p.Entity.X=475;p.Entity.Y=844;TowerUse(p,721799);talk(9910401,1);p.Entity.X=477;p.Entity.Y=844;kill();Require(p.Entity.MapID==1011);
                talk(4582,1);int site=Convert.ToInt32(Sql("SELECT courtesan_site FROM revora_attack_talisman WHERE uid=@uid","uid",(long)p.Entity.UID));var points=(ushort[,])Feature("AttackTalismanStore").GetField("CourtesanSites",Hidden).GetValue(null);p.Entity.Teleport(1015,(ushort)(points[site,0]-2),points[site,1]);kill();
                for(uint npc=9910410;npc<9910414;npc++)talk(npc,1);talk(9910414,1);Require(step()==10 && p.Map.BaseID==3937);
                // Open in player-visible order, without selecting from the hidden winning-box field.
                for(int box=0;!p.Inventory.Contains(710221,1) && box<3;box++){talk(9910420u+(uint)box,1);if(!p.Inventory.Contains(710221,1)){p.Entity.X=(ushort)(52+box*6);p.Entity.Y=53;kill();}}
                Require(p.Inventory.Contains(710221,1));talk(9910423,1);Require(p.Entity.MapID==1015);talk(4565,80);Require(p.Entity.Money==before-20000+900);
            } finally {clock.SetValue(null,original);}
            Require((long)Sql("SELECT count(*) FROM revora_attack_talisman WHERE uid=@uid AND stage=10 AND book_given AND cave_paid AND cardinality(rewards)=0","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_attack_talisman_purchases WHERE uid=@uid AND price=10000","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_fan_ambush_kills WHERE uid=@uid","uid",(long)p.Entity.UID)==5);FreshFanSave(p,"ready",kills,hits);
        } else if(phase.EndsWith("claim")) {
            Require(stage=="ready");var prior=p.Inventory.Objects.Where(i=>i.ID!=710222).Select(i=>i.UID).ToArray();FreshClassicTalk(p,1015,4565,81);var added=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(added.Length==2 && added.All(i=>i.Bound) && prior.All(uid=>p.Inventory.ContainsUID(uid)));
            Require((long)Sql("SELECT count(*) FROM revora_attack_talisman WHERE uid=@uid AND stage=11 AND cardinality(rewards)=2","uid",(long)p.Entity.UID)==1);FreshFanSave(p,"paid",kills,hits);
        } else Require(phase.EndsWith("reload") && stage=="paid");
        if(phase.EndsWith("claim") || phase.EndsWith("reload")) {
            var ids=(long[])Sql("SELECT rewards FROM revora_attack_talisman WHERE uid=@uid","uid",(long)p.Entity.UID);Require(ids.Length==2 && ids.Distinct().Count()==2);var rewards=p.Inventory.Objects.Where(i=>ids.Contains((long)i.UID)).ToArray();Require(rewards.Length==2 && rewards.All(i=>i.Bound) && rewards.Count(i=>i.ID==201009)==1 && rewards.Count(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1);
            string before=FreshFanSnapshot(p);FreshClassicTalk(p,1015,4565,81);Require(before==FreshFanSnapshot(p));
        }
        Require(FreshFanSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_fan_probe")) && !PirateFenced(p));System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" level="+p.Entity.Level+" kills="+kills+" hits="+hits+" bag="+p.Inventory.Count);Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Attack Talisman did not close.");
    }
}
