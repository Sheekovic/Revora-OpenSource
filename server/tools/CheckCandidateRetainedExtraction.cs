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
    static string RetainedExtractionSnapshot(GameState p,GameState recipient,string eliteRun) {
        return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,'claims',(SELECT json_agg(r ORDER BY cycle) FROM revora_expedition_claims r WHERE uid=@uid))::text",
            "career",RetainedRushSnapshot(p,recipient,eliteRun),"uid",(long)CareerUid));
    }
    static void RetainedExtractionProcess(bool reload) {
        careerRitesFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;
        var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);Revora.Database.KnownPersons.LoadMentor(p);
        new CoatStorage().Login(p);new TitleStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);WayofHeroes.Load(p);EntityVariableTable.Load(p);Call(typeof(Revora.Database.JiangHu),"LoadClient",p);
        nextUid=4294918104;var recipient=Player(Kernel.Maps[1002],true);recipient.LoadItemStats();ChiTable.Load(recipient);WayofHeroes.Load(recipient);
        nextUid=4294918206;var storage=Player(Kernel.Maps[1002],true);storage.LoadItemStats();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));


        string eliteRun=Convert.ToString(Sql("SELECT run FROM revora_retained_elite_combat_probe"));
        var feature=Feature("ExpeditionEvent");DateTime start=(DateTime)feature.GetField("CycleStart",Hidden).GetValue(null),now=start.AddMinutes(-4);
        feature.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        if(!reload) {
            Require((bool)Sql("SELECT to_regclass('revora_retained_extraction_probe') IS NULL"));
            Require(RetainedRushSnapshot(p,recipient,eliteRun)==Convert.ToString(Sql("SELECT snapshot FROM revora_retained_rush_probe")));
            Require((long)Sql("SELECT count(*) FROM items WHERE entityid=@uid AND position=0","uid",(long)CareerUid)>40);
            Require((long)Sql("SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)CareerUid)==p.Inventory.Count);
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
                Attack.HandleAttack(new Attack(true){Attacker=CareerUid,Attacked=body.UID,X=body.X,Y=body.Y,AttackType=Attack.Melee},p);hits++;
                if(hits==1)Require(body.Hitpoints<initial);
            }
            Require(body.Dead);Console.WriteLine("[retained-extraction] guard-hp="+initial+" hits="+hits);
            HeartTalk(p,91120,3);var member=members[CareerUid];var loot=(System.Collections.Generic.List<uint>)member.GetType().GetField("Loot").GetValue(member);Require(loot.Count==3);
            HeartTalk(p,91120,3);Require(loot.Count==3);
            now=start.AddMinutes(5);HeartTalk(p,91110,2);now=now.AddSeconds(9);Call(feature,"CompleteExtract",p);Require(p.Inventory.Count==count && p.Entity.MapID==map.ID);
            Revora.Game.Features.ExpeditionEvent.NotifyAction(p,"controlled interruption");now=now.AddSeconds(2);Call(feature,"CompleteExtract",p);Require(p.Inventory.Count==count);
            HeartTalk(p,91110,2);now=now.AddSeconds(11);Revora.Game.Features.ExpeditionEvent.Tick();
            Require(p.Entity.MapID==1002 && p.Inventory.Count==count+4 && p.Entity.RevoraPoint==rvp+410 && !PirateFenced(p));
            var rewards=p.Inventory.Objects.Where(i=>!before.Contains(i.UID)).ToArray();Require(rewards.Length==4 && rewards.Count(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1);
            Require((long)Sql("SELECT count(*) FROM revora_expedition_claims r JOIN items i ON i.uid=r.soul_uid WHERE r.uid=@uid AND i.entityid=r.uid AND i.id=r.soul_type AND i.bound=1","uid",(long)CareerUid)==1);
            Require(EntityTable.SaveEntity(p));Sql("CREATE TABLE revora_retained_extraction_probe(snapshot text);INSERT INTO revora_retained_extraction_probe VALUES(@snapshot)","snapshot",RetainedExtractionSnapshot(p,recipient,eliteRun));
        }
        Require(p.Entity.MapID==1002 && RetainedExtractionSnapshot(p,recipient,eliteRun)==Convert.ToString(Sql("SELECT snapshot FROM revora_retained_extraction_probe")));
        string once=RetainedExtractionSnapshot(p,recipient,eliteRun);Call(feature,"CompleteExtract",p);Require(once==RetainedExtractionSnapshot(p,recipient,eliteRun));
        Console.WriteLine("[career-process] career-retained-extraction-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Retained Extraction did not close.");
    }
}
