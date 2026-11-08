using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshExtractionSnapshot(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,'claims',(SELECT json_agg(r ORDER BY cycle) FROM revora_expedition_claims r WHERE uid=@uid))::text",
            "career",FreshBeastSnapshot(p),"uid",(long)p.Entity.UID));
    }
    static void FreshExtractionProcess(bool reload) {
        var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);Server.GamePool=new[]{p};Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        var feature=Feature("ExpeditionEvent");DateTime start=(DateTime)feature.GetField("CycleStart",Hidden).GetValue(null),now=start.AddMinutes(-4);
        feature.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        if(!reload) {
            Require((bool)Sql("SELECT to_regclass('revora_fresh_extraction_probe') IS NULL"));
            Require(FreshBeastSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_beast_probe")));
            Require((long)Sql("SELECT count(*) FROM items WHERE entityid=@uid AND position=0","uid",(long)p.Entity.UID)>40);
            Require((long)Sql("SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID)==p.Inventory.Count);
            FreshClassicTalk(p,1020,10028,0);var bank=p.Warehouses[Revora.Game.ConquerStructures.Warehouse.WarehouseID.ApeCity];Require(bank!=null && bank.Count+4<=60 && p.Inventory.Count==40);
            foreach(var item in p.Inventory.Objects.Where(i=>i.Days==0).Take(4).ToArray()){var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);Revora.Network.Writer.WriteUInt32(10028,4,frame);frame[12]=1;frame[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);Require(!p.Inventory.ContainsUID(item.UID) && bank.ContainsUID(item.UID));}
            int count=p.Inventory.Count;uint rvp=p.Entity.RevoraPoint;var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();Require(count<=36);
            p.Entity.Teleport(1002,408,330);HeartTalk(p,91002,1);HeartTalk(p,91002,1);
            var members=(System.Collections.IDictionary)feature.GetField("Members",Hidden).GetValue(null);Require(members.Count==1);
            now=start;Revora.Game.Features.ExpeditionEvent.Tick();var map=(Map)feature.GetField("EventMap",Hidden).GetValue(null);Require(p.Entity.MapID==map.ID && map.BaseID==1015);
            HeartTalk(p,91120,3);Require(p.Inventory.Count==count);
            var encounters=(System.Collections.IDictionary)feature.GetField("Encounters",Hidden).GetValue(null);
            var guard=encounters.Values.Cast<object>().First(e=>(int)e.GetType().GetField("Camp",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(e)==0);
            var body=(Entity)guard.GetType().GetField("Body",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(guard);
            p.Entity.X=(ushort)(body.X+1);p.Entity.Y=body.Y;p.Screen.Add(body);p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);int hits=0;uint initial=body.Hitpoints;
            while(!body.Dead && hits<20000) {
                p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);
                Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=body.UID,X=body.X,Y=body.Y,AttackType=Attack.Melee},p);hits++;
                if(hits==10)Require(body.Hitpoints<initial);
            }
            Require(body.Dead && !p.Entity.Dead && !PirateFenced(p));Console.WriteLine("[fresh-extraction] guard-hp="+initial+" hits="+hits);
            HeartTalk(p,91120,3);var member=members[p.Entity.UID];var loot=(System.Collections.Generic.List<uint>)member.GetType().GetField("Loot").GetValue(member);Require(loot.Count==3);
            HeartTalk(p,91120,3);Require(loot.Count==3);
            now=start.AddMinutes(5);HeartTalk(p,91110,2);now=now.AddSeconds(9);Call(feature,"CompleteExtract",p);Require(p.Inventory.Count==count && p.Entity.MapID==map.ID);
            Revora.Game.Features.ExpeditionEvent.NotifyAction(p,"controlled interruption");now=now.AddSeconds(2);Call(feature,"CompleteExtract",p);Require(p.Inventory.Count==count);
            HeartTalk(p,91110,2);now=now.AddSeconds(11);Revora.Game.Features.ExpeditionEvent.Tick();
            Require(p.Entity.MapID==1002 && p.Inventory.Count==count+4 && p.Entity.RevoraPoint==rvp+410 && !PirateFenced(p));
            var rewards=p.Inventory.Objects.Where(i=>!before.Contains(i.UID)).ToArray();Require(rewards.Length==4 && rewards.Count(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1);
            Require((long)Sql("SELECT count(*) FROM revora_expedition_claims r JOIN items i ON i.uid=r.soul_uid WHERE r.uid=@uid AND i.entityid=r.uid AND i.id=r.soul_type AND i.bound=1","uid",(long)p.Entity.UID)==1);
            Require(EntityTable.SaveEntity(p));Sql("CREATE TABLE revora_fresh_extraction_probe(snapshot text);INSERT INTO revora_fresh_extraction_probe VALUES(@snapshot)","snapshot",FreshExtractionSnapshot(p));
        }
        Require(p.Entity.MapID==1002 && FreshExtractionSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_extraction_probe")));
        string once=FreshExtractionSnapshot(p);Call(feature,"CompleteExtract",p);Require(once==FreshExtractionSnapshot(p));
        Console.WriteLine("[career-process] career-fresh-extraction-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Extraction did not close.");
    }
}
