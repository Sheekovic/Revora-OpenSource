using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshDemonSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,'runs',(SELECT json_agg(r ORDER BY day) FROM revora_demon_runs r WHERE uid=@uid))::text","career",FreshFlameSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshDemonJourney(string phase){
        var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);Server.GamePool=new[]{p};Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        var feature=Feature("DemonExterminators");var now=new DateTime(2041,3,25,20,0,0,DateTimeKind.Utc);feature.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        bool begin=phase.EndsWith("0"),claim=phase.EndsWith("claim"),reload=phase.EndsWith("reload");
        if(begin){Require((bool)Sql("SELECT to_regclass('revora_fresh_demon_probe') IS NULL") && FreshFlameSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_flame_probe")));FreshClassicTalk(p,1020,10028,0);var bank=p.Warehouses[Revora.Game.ConquerStructures.Warehouse.WarehouseID.ApeCity];int store=Math.Max(0,p.Inventory.Count-34);Require(bank!=null && bank.Count+store<=60);foreach(var item in p.Inventory.Objects.Where(i=>i.Days==0).Take(store).ToArray()){var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);Revora.Network.Writer.WriteUInt32(10028,4,frame);frame[12]=1;frame[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);Require(!p.Inventory.ContainsUID(item.UID) && bank.ContainsUID(item.UID));}Require(p.Inventory.Count<=34);FreshClassicTalk(p,1002,500010,0);int bag=p.Inventory.Count;CareerPortal(p,1036);HeartTalk(p,2000,1);Require(p.Inventory.Count==bag+1);Sql("CREATE TABLE revora_fresh_demon_probe(step integer NOT NULL,snapshot text);INSERT INTO revora_fresh_demon_probe VALUES(0,@snapshot)","snapshot",FreshDemonSnapshot(p));}
        else Require(FreshDemonSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_demon_probe")));
        Call(feature,"OnLogin",p);
        if(!claim && !reload){
            int stage=int.Parse(phase.Substring(phase.Length-1));int[] needs={600,1200,1500,1800};uint[] targets={18,20,57,58};
            Require(Convert.ToInt32(Sql("SELECT stage FROM revora_demon_runs WHERE uid=@uid AND day='20410325'","uid",(long)p.Entity.UID))==stage);
            HeartTalk(p,2000,2);Require(Convert.ToInt32(Sql("SELECT kills FROM revora_demon_runs WHERE uid=@uid AND day='20410325'","uid",(long)p.Entity.UID))==0);
            HeartTalk(p,45,1);Require(p.Entity.MapID==1002);if(stage==0)CareerPortal(p,1015);else{CareerPortal(p,1000);CareerPortal(p,1001);}
            int shots=0;
            for(int n=0;n<needs[stage];n++){
                var mob=p.Map.Entities.Values.FirstOrDefault(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==targets[stage] && !e.Dead);
                if(mob==null){Call(typeof(Map),"_timerCallBack",p.Map,Time32.Now.AddSeconds(600).TotalMilliseconds);mob=p.Map.Entities.Values.FirstOrDefault(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==targets[stage] && !e.Dead);}Require(mob!=null);
                p.Entity.X=mob.X;p.Entity.Y=(ushort)(mob.Y+1);p.Screen.Add(mob);p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);int hits=0;
                while(!mob.Dead && hits++<100){p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=mob.UID,X=mob.X,Y=mob.Y,AttackType=Attack.Melee},p);shots++;}Require(mob.Dead && !p.Entity.Dead && !PirateFenced(p));
            }
            Require(Convert.ToInt32(Call(feature,"JarCount",p))==needs[stage]);
            if(stage==0)CareerPortal(p,1002);else{CareerPortal(p,1000);CareerPortal(p,1002);}CareerPortal(p,1036);
            if(stage<3){HeartTalk(p,2000,2);Require(Convert.ToInt32(Call(feature,"JarCount",p))==0);}
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_demon_probe SET step=@step,snapshot=@snapshot","step",stage+1,"snapshot",FreshDemonSnapshot(p));Console.WriteLine("[career-process] demon stage="+(stage+1)+" melee-hits="+shots);
        }else if(claim){
            Require(Convert.ToInt32(Sql("SELECT step FROM revora_fresh_demon_probe"))==4 && p.Inventory.Count<=35);int count=p.Inventory.Count;HeartTalk(p,2000,2);Require(p.Inventory.Count==count+5);
            Require((long)Sql("SELECT count(*) FROM revora_demon_runs r JOIN items i ON i.uid=ANY(r.reward_ids) WHERE r.uid=@uid AND r.day='20410325' AND r.state='complete' AND i.entityid=@uid AND i.bound=1 AND (i.id=722136 OR i.id=r.rune_reward_id)","uid",(long)p.Entity.UID)==6);
            Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11001 AND source='demon-exterminators-v1'","uid",(long)p.Entity.UID)==1);
            string after=FreshDemonSnapshot(p);HeartTalk(p,2000,2);HeartTalk(p,2000,1);Require(after==FreshDemonSnapshot(p));HeartTalk(p,45,1);Require(EntityTable.SaveEntity(p));
            Sql("UPDATE revora_fresh_demon_probe SET step=5,snapshot=@snapshot","snapshot",FreshDemonSnapshot(p));
        }else{Require(Convert.ToInt32(Sql("SELECT step FROM revora_fresh_demon_probe"))==5);Require(Convert.ToInt32(Call(feature,"JarCount",p))==0);Require(FreshDemonSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_demon_probe")));}
        Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Career Demon hunt did not close.");
    }
}
