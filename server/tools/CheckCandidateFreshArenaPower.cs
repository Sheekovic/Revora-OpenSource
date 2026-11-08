using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshArenaPowerSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,'run',(SELECT row_to_json(r) FROM revora_power_runs r WHERE uid=@uid),'claims',(SELECT json_agg(r ORDER BY window_start,stage) FROM revora_power_claims r WHERE uid=@uid),'total',(SELECT row_to_json(r) FROM revora_power_totals r WHERE uid=@uid),'exchanges',(SELECT json_agg(r ORDER BY created_at,id) FROM revora_power_exchanges r WHERE uid=@uid))::text","career",FreshTreasurySnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshArenaPowerJourney(string phase){
        var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);Server.GamePool=new[]{p};Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        var eventType=Feature("PowerArenaEvent");DateTime now=new DateTime(2041,3,13,20,0,0,DateTimeKind.Utc);bool first=phase.EndsWith("first"),second=phase.EndsWith("second");if(!first)now=now.AddHours(7);
        eventType.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));Feature("PowerArenaPacks").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        if(first){
            Require((bool)Sql("SELECT to_regclass('revora_fresh_arena_power_probe') IS NULL") && FreshTreasurySnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_treasury_probe")));
            FreshClassicTalk(p,1020,10028,0);var bank=p.Warehouses[Revora.Game.ConquerStructures.Warehouse.WarehouseID.ApeCity];int store=Math.Max(0,p.Inventory.Count-32);Require(bank!=null && bank.Count+store<=60);foreach(var item in p.Inventory.Objects.Where(i=>i.Days==0).Take(store).ToArray()){var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);Revora.Network.Writer.WriteUInt32(10028,4,frame);frame[12]=1;frame[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);Require(!p.Inventory.ContainsUID(item.UID) && bank.ContainsUID(item.UID));}Require(p.Inventory.Count<=32);FreshClassicTalk(p,1002,500010,0);
        }else Require(FreshArenaPowerSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_arena_power_probe")));
        int initialBag=p.Inventory.Count;
        if(first || second){
            HeartTalk(p,500010,1);Require(p.Entity.MapID>=1730 && p.Entity.MapID<=1736);int floors=first?7:5,shots=0;int[] needs={30,45,60,35,45,60,90};
            for(int floor=p.Entity.MapID-1729;floor<=floors;floor++){
                uint npc=(uint)(3955+floor);HeartTalk(p,npc,1);Require((long)Sql("SELECT count(*) FROM revora_power_claims WHERE uid=@uid AND window_start=@window AND stage=@stage","uid",(long)p.Entity.UID,"window",new DateTimeOffset(now),"stage",floor)==0);
                for(int n=Convert.ToInt32(Sql("SELECT kills FROM revora_power_runs WHERE uid=@uid","uid",(long)p.Entity.UID));n<needs[floor-1];n++){
                    var mob=p.Map.Entities.Values.FirstOrDefault(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==(uint)(2132+floor) && !e.Dead);if(mob==null){Call(typeof(Map),"_timerCallBack",p.Map,Time32.Now.AddSeconds(600).TotalMilliseconds);mob=p.Map.Entities.Values.FirstOrDefault(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==(uint)(2132+floor) && !e.Dead);}Require(mob!=null);p.Entity.X=mob.X;p.Entity.Y=(ushort)(mob.Y+1);p.Screen.Add(mob);p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);int hits=0;
                    while(!mob.Dead && hits<100){p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=mob.UID,X=mob.X,Y=mob.Y,AttackType=Attack.Melee},p);hits++;shots++;}Require(mob.Dead && !p.Entity.Dead && !PirateFenced(p));
                }
                Require(Convert.ToInt32(Sql("SELECT kills FROM revora_power_runs WHERE uid=@uid","uid",(long)p.Entity.UID))==needs[floor-1]);HeartTalk(p,npc,1);string after=FreshArenaPowerSnapshot(p);HeartTalk(p,npc,1);Require(after==FreshArenaPowerSnapshot(p));
                if(floor<floors){HeartTalk(p,npc,3);Require(p.Entity.MapID==1730+floor);}else HeartTalk(p,npc,5);
            }
            Require(p.Entity.MapID==1002);HeartTalk(p,500010,101);HeartTalk(p,500010,230);string exchanged=FreshArenaPowerSnapshot(p);HeartTalk(p,500010,230);Require(exchanged==FreshArenaPowerSnapshot(p));
            Require(Convert.ToInt32(Sql("SELECT badges FROM revora_power_totals WHERE uid=@uid","uid",(long)p.Entity.UID))==(first?7:12));Require(p.Inventory.Count==initialBag+(first?3:0));
            Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id IN(11013,11014)","uid",(long)p.Entity.UID)==(first?1:2));
            Require((long)Sql("SELECT count(*) FROM revora_power_claims c JOIN items i ON i.uid=c.mount_uid WHERE c.uid=@uid AND i.entityid=@uid AND i.id=200449 AND i.bound=0","uid",(long)p.Entity.UID)==1);
            Require(EntityTable.SaveEntity(p));if(first)Sql("CREATE TABLE revora_fresh_arena_power_probe(done boolean NOT NULL,snapshot text);INSERT INTO revora_fresh_arena_power_probe VALUES(false,@snapshot)","snapshot",FreshArenaPowerSnapshot(p));else Sql("UPDATE revora_fresh_arena_power_probe SET done=true,snapshot=@snapshot","snapshot",FreshArenaPowerSnapshot(p));
            Console.WriteLine("[career-process] power floors="+floors+" melee-hits="+shots);
        }else{Require((bool)Sql("SELECT done FROM revora_fresh_arena_power_probe"));Call(eventType,"OnLogin",p);Require(p.Entity.MapID==1002);HeartTalk(p,500010,230);Require(FreshArenaPowerSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_arena_power_probe")));}
        Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Career Power Arena did not close.");
    }
    static string FreshArenaPacksSnapshot(GameState p){return Convert.ToString(Sql("SELECT json_build_object('career',@prior::json,'outputs',(SELECT json_agg(r ORDER BY id) FROM revora_power_pack_results r WHERE uid=@uid))::text","prior",FreshArenaPowerSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshArenaPacksJourney(string phase){
        var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);Server.GamePool=new[]{p};Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));DateTime now=new DateTime(2041,3,14,3,0,0,DateTimeKind.Utc);Feature("PowerArenaEvent").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));Feature("PowerArenaPacks").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        if(phase.EndsWith("open")){
            Require((bool)Sql("SELECT to_regclass('revora_fresh_arena_packs_probe') IS NULL") && FreshArenaPowerSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_arena_power_probe WHERE done")));
            var packs=p.Inventory.Objects.Where(i=>i.ID==723655).ToArray();Require(packs.Length==2 && (long)Sql("SELECT count(*) FROM revora_power_exchanges WHERE uid=@uid AND pack_type=723655 AND badges=6","uid",(long)p.Entity.UID)==2);
            foreach(var pack in packs){Require(pack.Bound && pack.StackSize==1 && (long)Sql("SELECT count(*) FROM revora_power_exchanges WHERE uid=@uid AND pack_uid=@pack","uid",(long)p.Entity.UID,"pack",(long)pack.UID)==1);var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();ItemHandler.UseItem(pack,p);var added=p.Inventory.Objects.Where(i=>!before.Contains(i.UID)).ToArray();Require(!p.Inventory.ContainsUID(pack.UID) && added.Length==1 && added[0].Bound);Require((long)Sql("SELECT count(*) FROM revora_power_pack_results WHERE uid=@uid AND pack_uid=@pack AND result_uid=@result AND result_type=@type","uid",(long)p.Entity.UID,"pack",(long)pack.UID,"result",(long)added[0].UID,"type",(int)added[0].ID)==1);string exact=FreshArenaPacksSnapshot(p);ItemHandler.UseItem(pack,p);Require(exact==FreshArenaPacksSnapshot(p));Console.WriteLine("[fresh-arena-packs] output="+added[0].ID);}
            Require(EntityTable.SaveEntity(p));Sql("CREATE TABLE revora_fresh_arena_packs_probe(snapshot text);INSERT INTO revora_fresh_arena_packs_probe VALUES(@snapshot)","snapshot",FreshArenaPacksSnapshot(p));
        }else Require(phase.EndsWith("reload"));
        Require((long)Sql("SELECT count(*) FROM revora_power_pack_results r JOIN items i ON i.uid=r.result_uid AND i.entityid=r.uid AND i.id=r.result_type AND i.bound=1 WHERE r.uid=@uid","uid",(long)p.Entity.UID)==2 && FreshArenaPacksSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_arena_packs_probe")));string once=FreshArenaPacksSnapshot(p);HeartTalk(p,500010,230);Require(once==FreshArenaPacksSnapshot(p) && !PirateFenced(p));Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Fresh arena packs did not close.");
    }
}
