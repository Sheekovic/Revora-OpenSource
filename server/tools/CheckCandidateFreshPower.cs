using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Game;
using Revora.Network;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshPowerSnapshot(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,
'run',(SELECT json_agg(r ORDER BY life) FROM revora_power_book r WHERE uid=@uid),
'purchases',(SELECT json_agg(r ORDER BY life) FROM revora_power_book_purchases r WHERE uid=@uid),
'lives',(SELECT json_agg(r ORDER BY id) FROM revora_power_book_lives r WHERE uid=@uid),
'rewards',(SELECT json_agg(r ORDER BY life) FROM revora_power_book_rewards r WHERE uid=@uid),
'recoveries',(SELECT json_agg(r ORDER BY life,old_uid) FROM revora_power_book_recoveries r WHERE uid=@uid),
'visits',(SELECT json_agg(r ORDER BY visit) FROM revora_power_book_visits r WHERE uid=@uid))::text","prior",FreshExpeditionSnapshot(p),"uid",(long)p.Entity.UID));
    }
    static void FreshPowerSave(GameState p,string stage,int kills,int hits) {
        Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_power_probe SET stage=@stage,kills=@kills,hits=@hits,snapshot=@snapshot","stage",stage,"kills",kills,"hits",hits,"snapshot",FreshPowerSnapshot(p));
    }
    static void FreshPowerProcess(string phase) {
        var p=FreshLoadSaved(true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_power_probe') IS NULL")) {
            Require(phase.EndsWith("earn") && FreshExpeditionSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_expedition_probe")) && (long)Sql("SELECT count(*) FROM revora_power_book WHERE uid=@uid","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_power_probe(stage text,kills integer,hits integer,snapshot text);INSERT INTO revora_fresh_power_probe VALUES('new',0,0,@snapshot)","snapshot",FreshPowerSnapshot(p));
        }
        Require(FreshPowerSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_power_probe")));
        string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_power_probe"));int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_power_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_power_probe"));
        if(phase.EndsWith("earn")) {
            Require(stage=="new");var clock=Feature("PowerBookStore").GetField("Clock",Hidden);object original=clock.GetValue(null);DateTime now=DateTime.UtcNow;
            Action pulse=()=>{now=now.AddSeconds(3);Call(Feature("PowerBookQuest"),"Tick");};
            Action<uint> use=id=>ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==id),p);
            Action gate=()=>{p.Entity.X=140;p.Entity.Y=140;use(721924);};
            Func<int> step=()=>Convert.ToInt32(Sql("SELECT stage FROM revora_power_book WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID));
            Action<int,bool> kill=(kind,herd)=>{
                Func<Revora.Game.Entity> select=()=>p.Map.Entities.Values.Where(b=>!b.Dead && b.MonsterInfo!=null && b.MonsterInfo.ID==9918400+kind).OrderBy(b=>herd?Kernel.GetDistance(b.X,b.Y,77,23):0).ThenBy(b=>b.UID).FirstOrDefault();
                var body=select();if(body==null){pulse();body=select();}Require(body!=null);
                if(herd){for(int move=0;move<120 && Kernel.GetDistance(body.X,body.Y,77,23)>3;move++){
                    p.Entity.X=(ushort)(body.X+Math.Sign(77-body.X)*Math.Min(4,Math.Abs(77-body.X)));p.Entity.Y=(ushort)(body.Y+Math.Sign(23-body.Y)*Math.Min(4,Math.Abs(23-body.Y)));body.MonsterInfo.LastMove=new Time32(1);typeof(Screen).GetMethod("DriveMonsterAt",Hidden).Invoke(null,new object[]{body,p.Entity,Time32.Now});
                }Require(Kernel.GetDistance(body.X,body.Y,77,23)<=3);}
                // Attack the actual lured body, never substitute a same-family actor.
                p.Entity.X=body.X;p.Entity.Y=(ushort)(body.Y+1);p.Screen.Add(body);p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);uint hp=body.Hitpoints;int attacks=0;
                while(!body.Dead && attacks<1000){p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=body.UID,X=body.X,Y=body.Y,AttackType=Attack.Melee},p);attacks++;hits++;}
                Require(body.Dead && body.Hitpoints<hp && !p.Entity.Dead && !PirateFenced(p));kills++;
            };
            try {
                clock.SetValue(null,(Func<DateTime>)(()=>now));uint cp=p.Entity.ConquerPoints;ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=2888,dwParam=1088001,dwExtraInfo=1},p);Require(p.Entity.ConquerPoints==cp-3 && p.Inventory.Contains(1088001,1));
                FreshClassicTalk(p,1002,900071727,41);FreshClassicTalk(p,1002,900071841,1);string purchase=FreshPowerSnapshot(p);FreshClassicTalk(p,1002,900071841,1);Require(purchase==FreshPowerSnapshot(p) && step()==2 && !p.Inventory.Contains(1088001,1));
                FreshClassicTalk(p,1002,900071841,2);Require(p.Map.BaseID==1500);
                for(int i=0;i<20;i++)kill(0,false);Require(step()==3);pulse();for(int i=0;i<10;i++)kill(1,true);Require(step()==4);pulse();
                for(int i=0;i<15;i++){kill(0,false);kill(1,false);}gate();Require(step()==5 && p.Map.BaseID==1501);
                kill(2,false);kill(2,false);Require(step()==6);pulse();
                for(int i=0;step()==6 && i<200;i++){if(!p.Inventory.Contains(721926,1))kill(3,false);if(p.Inventory.Contains(721926,1)){uint uid=p.Inventory.Objects.Single(item=>item.ID==721926).UID;use(721926);Require(!p.Inventory.ContainsUID(uid));}}
                Require(step()==7);pulse();for(int i=0;i<10;i++){kill(2,false);kill(3,false);}gate();Require(step()==8 && p.Map.BaseID==1502);
                kill(4,false);kill(4,false);Require(step()==9);pulse();
                for(int i=0;step()==9 && i<200;i++){if(!p.Inventory.Contains(721928,1) && !p.Inventory.Contains(721929,1))kill(5,false);if(p.Inventory.Contains(721928,1)){uint uid=p.Inventory.Objects.Single(item=>item.ID==721928).UID;p.Entity.X=100;p.Entity.Y=66;use(721928);Require(!p.Inventory.ContainsUID(uid));}if(p.Inventory.Contains(721929,1))use(721929);}
                Require(step()==10);pulse();for(int i=0;i<5;i++){kill(4,false);kill(5,false);}Require(step()==11);pulse();for(int i=0;!p.Inventory.Contains(721931,1) && i<100;i++)kill(4,false);Require(p.Inventory.Contains(721931,1));gate();Require(p.Map.BaseID==1503);
                for(int i=0;i<10;i++){kill(6,false);use(721931);Require(Convert.ToInt32(Sql("SELECT count(*) FROM revora_power_book_lives WHERE uid=@uid AND life=2 AND burned","uid",(long)p.Entity.UID))==i+1);}
                Require(step()==12);p.Entity.X=77;p.Entity.Y=27;use(721924);Require(p.Entity.MapID==1002);FreshClassicTalk(p,1002,900071841,3);Require(step()==13);string reported=FreshPowerSnapshot(p);FreshClassicTalk(p,1002,900071841,3);Require(reported==FreshPowerSnapshot(p));FreshClassicTalk(p,1002,900071727,0);
            } finally {clock.SetValue(null,original);}
            Require((long)Sql("SELECT count(*) FROM revora_power_book_rewards WHERE uid=@uid","uid",(long)p.Entity.UID)==0);FreshPowerSave(p,"ready",kills,hits);
        } else if(phase.EndsWith("claim")) {
            Require(stage=="ready");var prior=p.Inventory.Objects.Where(i=>i.ID!=721715 && i.ID!=721924).Select(i=>i.UID).ToArray();FreshClassicTalk(p,1002,900071727,43);var added=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(added.Length==2 && added.All(i=>i.Bound) && prior.All(uid=>p.Inventory.ContainsUID(uid)) && !p.Inventory.Objects.Any(i=>i.ID==721715 || i.ID==721924 || i.ID==721931));
            Require(Convert.ToInt32(Sql("SELECT stage FROM revora_power_book WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID))==14);FreshPowerSave(p,"paid",kills,hits);
        } else Require(phase.EndsWith("reload") && stage=="paid");
        if(phase.EndsWith("claim") || phase.EndsWith("reload")) {
            var ids=(long[])Sql("SELECT ARRAY[pack_uid,soul_uid] FROM revora_power_book_rewards WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID);Require(ids.Length==2 && ids.Distinct().Count()==2);var rewards=p.Inventory.Objects.Where(i=>ids.Contains((long)i.UID)).ToArray();Require(rewards.Length==2 && rewards.All(i=>i.Bound) && rewards.Count(i=>i.ID==728251)==1 && rewards.Count(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1);
            string before=FreshPowerSnapshot(p);FreshClassicTalk(p,1002,900071727,43);Require(before==FreshPowerSnapshot(p));
        }
        Require(FreshPowerSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_power_probe")) && !PirateFenced(p));System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" level="+p.Entity.Level+" kills="+kills+" hits="+hits+" bag="+p.Inventory.Count);Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Power Book did not close.");
    }
}
