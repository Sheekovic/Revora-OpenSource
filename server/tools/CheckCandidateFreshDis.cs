using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static long FreshDisValue(string field){return Convert.ToInt64(Sql("SELECT CASE @field WHEN 'stage' THEN stage WHEN 'progress' THEN progress WHEN 'quota' THEN quota WHEN 'crystal_order[progress+1]' THEN crystal_order[progress+1] END FROM revora_dis_runs WHERE uid=@uid","field",field,"uid",(long)1096322));}
    static int freshDisKills,freshDisHits;
    static void FreshDisKill(GameState p,ref DateTime now){
        now=now.AddSeconds(3);Call(Feature("DisCityQuest"),"Tick");var target=p.Map.Entities.Values.First(e=>e.MonsterInfo!=null && !e.Dead);p.Entity.X=target.X;p.Entity.Y=(ushort)(target.Y+1);p.Screen.Add(target);p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);
        int shots=0,stalled=0;while(!target.Dead && shots<2000){
            uint hp=target.Hitpoints;p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=target.UID,X=target.X,Y=target.Y,AttackType=Attack.Melee},p);shots++;
            stalled=target.Hitpoints==hp?stalled+1:0;if(stalled>=32)break;
        }
        if(!target.Dead)Console.WriteLine("[fresh-dis] stalled shots="+shots+" hp="+target.Hitpoints+" attack="+p.Entity.MaxAttack+" player-hp="+p.Entity.Hitpoints);
        Require(target.Dead && p.Action==2 && !p.Entity.Dead && !PirateFenced(p));freshDisKills++;freshDisHits+=shots;
    }
    static string FreshDisSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,
'run',(SELECT row_to_json(r) FROM revora_dis_runs r WHERE uid=@uid),
'claims',(SELECT json_agg(r ORDER BY stage) FROM revora_dis_stage_claims r WHERE uid=@uid),
'completion',(SELECT json_agg(r ORDER BY week) FROM revora_dis_completions r WHERE uid=@uid))::text","career",FreshRushSnapshot(p),"uid",(long)1096322));}
    static void FreshDisJourney(bool reload){
        var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);Server.GamePool=new[]{p};Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p) && p.Entity.UID==1096322);
        var type=Feature("DisCityQuest");var oldClock=type.GetField("Clock",Hidden).GetValue(null);DateTime now=new DateTime(2041,3,11,20,0,0,DateTimeKind.Utc);type.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        try{
            if((bool)Sql("SELECT to_regclass('revora_fresh_dis_probe') IS NULL")){
                Require(!reload && FreshRushSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_rush_probe")));
                Sql("CREATE TABLE revora_fresh_dis_probe(done boolean NOT NULL,snapshot text);INSERT INTO revora_fresh_dis_probe VALUES(false,@snapshot)","snapshot",FreshDisSnapshot(p));
                FreshClassicTalk(p,1020,10028,0);var bank=p.Warehouses[Revora.Game.ConquerStructures.Warehouse.WarehouseID.ApeCity];Require(bank!=null && bank.Count+20<=60 && p.Inventory.Count==40);
                foreach(var item in p.Inventory.Objects.Where(i=>i.Days==0).Take(20).ToArray()){var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);Revora.Network.Writer.WriteUInt32(10028,4,frame);frame[12]=1;frame[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);Require(!p.Inventory.ContainsUID(item.UID) && bank.ContainsUID(item.UID));}Require(p.Inventory.Count==20);p.Entity.Teleport(1002,408,330);
                Require(EntityTable.SaveEntity(p));
            }
            if(!reload){
                Require(!(bool)Sql("SELECT done FROM revora_fresh_dis_probe"));
                Call(type,"OnLogin",p);Require(p.Entity.MapID==1002);
                bool completedRun=(bool)Sql("SELECT EXISTS(SELECT 1 FROM revora_dis_runs WHERE uid=@uid AND phase='complete')","uid",(long)p.Entity.UID);
                if(!completedRun){HeartTalk(p,3215,1);Require(p.Action==2 && p.Map.BaseID>=2021 && p.Map.BaseID<=2024);}else Console.WriteLine("[fresh-dis] resuming completed rewards for replay verification only");
                while(FreshDisValue("stage")<=4 && Convert.ToString(Sql("SELECT phase FROM revora_dis_runs WHERE uid=@uid","uid",(long)1096322))!="complete"){
                    int stage=(int)FreshDisValue("stage");
                    if(stage==1 || stage==2){int quota=stage==1?5:(int)FreshDisValue("quota");if(stage==2)Require(quota==400);while(FreshDisValue("progress")<quota){FreshDisKill(p,ref now);if(FreshDisValue("progress")%100==0)Console.WriteLine("[fresh-dis] stage="+stage+" progress="+FreshDisValue("progress"));}HeartTalk(p,3215,2);HeartTalk(p,3215,1);Require(FreshDisValue("stage")==stage+1);}
                    else if(stage==3){while(FreshDisValue("progress")<9){uint crystal=(uint)(900000080+FreshDisValue("crystal_order[progress+1]"));FreshDisKill(p,ref now);HeartTalk(p,crystal,1);}HeartTalk(p,3215,1);Require(FreshDisValue("stage")==4);}
                    else {while(FreshDisValue("progress")<4)FreshDisKill(p,ref now);HeartTalk(p,3215,2);ItemHandler.UseItem(p.Inventory.Objects.First(i=>i.ID==3006983),p);ItemHandler.UseItem(p.Inventory.Objects.First(i=>i.ID==3006984),p);FreshDisKill(p,ref now);HeartTalk(p,3215,1);Require(p.Entity.MapID==1002);}
                    Require(EntityTable.SaveEntity(p));Console.WriteLine("[fresh-dis] finished-stage="+stage+" kills="+freshDisKills+" hits="+freshDisHits);
                }
                Require((long)Sql("SELECT count(*) FROM revora_dis_stage_claims WHERE uid=@uid","uid",(long)1096322)==4);foreach(int id in new[]{11002,11003,11004,11005})Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@id AND source='dis-city-v1'","uid",(long)1096322,"id",id)==1);
                HeartTalk(p,3215,0);string completed=FreshDisSnapshot(p);HeartTalk(p,3215,1);Require(p.Entity.MapID==1002 && FreshDisSnapshot(p)==completed);Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_dis_probe SET done=true,snapshot=@snapshot","snapshot",FreshDisSnapshot(p));
            }
            Require((long)Sql("SELECT count(*) FROM revora_dis_stage_claims c CROSS JOIN LATERAL unnest(c.rewards) reward(uid) LEFT JOIN items i ON i.uid=reward.uid AND i.entityid=c.uid WHERE c.uid=@uid AND i.uid IS NULL","uid",(long)p.Entity.UID)==0);
            Require((long)Sql("SELECT count(*) FROM revora_dis_stage_claims c CROSS JOIN LATERAL unnest(c.rewards) reward(uid) JOIN items i ON i.uid=reward.uid WHERE c.uid=@uid AND c.stage=4 AND i.bound=1 AND i.id BETWEEN 800000 AND 899999","uid",(long)p.Entity.UID)==1);
            if(reload){string exact=FreshDisSnapshot(p);HeartTalk(p,3215,1);Require(exact==FreshDisSnapshot(p));}
            Require(FreshDisValue("stage")==4 && FreshDisValue("progress")==4 && FreshDisValue("quota")==400);
            Require((bool)Sql("SELECT done FROM revora_fresh_dis_probe") && p.Entity.MapID==1002 && FreshDisSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_dis_probe")));
        }finally{type.GetField("Clock",Hidden).SetValue(null,oldClock);}
        Console.WriteLine("[career-process] career-fresh-dis-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Dis City did not close.");
    }
}
