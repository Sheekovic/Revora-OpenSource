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
    static string RetainedUnarmedSnapshot(GameState p,GameState recipient,string eliteRun) {
        return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,'claims',(SELECT json_agg(r ORDER BY cycle) FROM revora_expedition_claims r WHERE uid=@uid),'unarmed',(SELECT json_agg(r ORDER BY life) FROM revora_unarmed_wild_victories r WHERE uid=@uid))::text",
            "career",RetainedBeastSnapshot(p,recipient,eliteRun),"uid",(long)CareerUid));
    }
    static void RetainedUnarmedProcess(bool reload) {
        careerRitesFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;
        var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);Revora.Database.KnownPersons.LoadMentor(p);
        new CoatStorage().Login(p);new TitleStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);WayofHeroes.Load(p);EntityVariableTable.Load(p);Call(typeof(Revora.Database.JiangHu),"LoadClient",p);
        nextUid=4294918104;var recipient=Player(Kernel.Maps[1002],true);recipient.LoadItemStats();ChiTable.Load(recipient);WayofHeroes.Load(recipient);
        nextUid=4294918206;var storage=Player(Kernel.Maps[1002],true);storage.LoadItemStats();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));


        string eliteRun=Convert.ToString(Sql("SELECT run FROM revora_retained_elite_combat_probe"));
        var feature=Feature("ExpeditionEvent");DateTime start=DateTime.Parse(Convert.ToString(Sql("SELECT max(cycle) FROM revora_expedition_claims WHERE uid=@uid","uid",(long)CareerUid)),null,System.Globalization.DateTimeStyles.RoundtripKind).AddHours(3).ToLocalTime(),now=start.AddMinutes(-4);
        feature.GetField("CycleStart",Hidden).SetValue(null,start);
        feature.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        if(!reload) {
            Require((bool)Sql("SELECT to_regclass('revora_retained_unarmed_probe') IS NULL"));
            // Restore only the exact hand UIDs from the last verified checkpoint if an
            // interrupted fixture left them in the bag. No inventory or receipt reset.
            foreach(int slot in new[]{4,5}) {
                var prior=Sql("SELECT DISTINCT (j->>'uid')::bigint FROM revora_retained_beast_probe, LATERAL jsonb_path_query(snapshot::jsonb,'strict $.** ? (@.entityid == 4294907001 && @.warehouse == 0)') j WHERE (j->>'position')::int=@slot","slot",slot);
                if(prior!=null && prior!=DBNull.Value) {
                    uint id=Convert.ToUInt32(prior);
                    if(p.Equipment.TryGetItem((byte)slot)==null && p.Inventory.ContainsUID(id))ItemHandler.EquipItem(new ItemUsage(true){UID=id,dwParam=(uint)slot,Action=ItemUsage.Mode.EquipItem},p);
                    Require(p.Equipment.TryGetItem((byte)slot)!=null && p.Equipment.TryGetItem((byte)slot).UID==id);
                }
            }
            string expected=Convert.ToString(Sql("SELECT snapshot FROM revora_retained_beast_probe")),actual=RetainedBeastSnapshot(p,recipient,eliteRun);
            // Exact recovery of the first calculated kill: only recorded ring/fan wear.
            Require(expected==actual || (bool)Sql("SELECT jsonb_set(jsonb_set(jsonb_set(jsonb_set(@before::jsonb,@path0,to_jsonb(@value0::int)),@path1,to_jsonb(@value1::int)),@path2,to_jsonb(@value2::int)),@path3,to_jsonb(@value3::int))=@after::jsonb","before",expected,"after",actual,"path0",new string[]{"career","career","career","career","base","jiang","base","items","528","durability"},"value0",6821,"path1",new string[]{"career","career","career","career","base","jiang","base","items","642","durability"},"value1",6404,"path2",new string[]{"career","career","career","career","base","career","career","career","career","career","career","career","career","base","base","items","528","durability"},"value2",6821,"path3",new string[]{"career","career","career","career","base","career","career","career","career","career","career","career","career","base","base","items","642","durability"},"value3",6404));
            Require((long)Sql("SELECT count(*) FROM items WHERE entityid=@uid AND position=0","uid",(long)CareerUid)>40);
            Require((long)Sql("SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)CareerUid)==p.Inventory.Count);
            int count=p.Inventory.Count;uint rvp=p.Entity.RevoraPoint;var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();Require(count<=36);
            p.Entity.Teleport(1002,408,330);HeartTalk(p,91002,1);HeartTalk(p,91002,1);
            var members=(System.Collections.IDictionary)feature.GetField("Members",Hidden).GetValue(null);Require(members.Count==1);
            now=start;Revora.Game.Features.ExpeditionEvent.Tick();var map=(Map)feature.GetField("EventMap",Hidden).GetValue(null);Require(p.Entity.MapID==map.ID && map.BaseID==1015);
            var hands=new[]{p.Equipment.TryGetItem(4),p.Equipment.TryGetItem(5)}.Where(i=>i!=null).Select(i=>new{Uid=i.UID,Slot=i.Position}).ToArray();
            foreach(byte slot in new byte[]{5,4})if(p.Equipment.TryGetItem(slot)!=null)ItemHandler.UnequipItem(new ItemUsage(true){dwParam=slot,Action=ItemUsage.Mode.UnequipItem},p);
            Require(p.Equipment.TryGetItem(4)==null && p.Equipment.TryGetItem(5)==null && !p.AlternateEquipment);
            Require((long)Sql("SELECT count(*) FROM items WHERE entityid=@uid AND position IN(4,5)","uid",(long)CareerUid)==0);
            now=start.AddMinutes(10);Revora.Game.Features.ExpeditionEvent.Tick();
            var body=map.Entities.Values.Single(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==9913002 && !e.Dead);
            Require(body.MonsterInfo.Level==140 && body.Hitpoints==5000000);
            long priorLives=(long)Sql("SELECT count(*) FROM revora_unarmed_wild_victories WHERE uid=@uid AND family=9913002 AND unarmed","uid",(long)CareerUid);
            p.Entity.X=(ushort)(body.X+1);p.Entity.Y=body.Y;p.Screen.Add(body);p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);int hits=0;uint initial=body.Hitpoints;
            while(!body.Dead && hits<20000) {
                p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);
                Attack.HandleAttack(new Attack(true){Attacker=CareerUid,Attacked=body.UID,X=body.X,Y=body.Y,AttackType=Attack.Melee},p);hits++;
                if(hits==1)Require(body.Hitpoints<initial);
            }
            Require(body.Dead);Console.WriteLine("[retained-unarmed] guard-hp="+initial+" hits="+hits);
            Require((long)Sql("SELECT count(*) FROM revora_unarmed_wild_victories WHERE uid=@uid AND family=9913002 AND unarmed","uid",(long)CareerUid)==priorLives); // Existing10525 intentionally suppresses another grant/life receipt.
            Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10525","uid",(long)CareerUid)==1);
            var member=members[CareerUid];var loot=(System.Collections.Generic.List<uint>)member.GetType().GetField("Loot").GetValue(member);Require(loot.Count==3);
            foreach(var hand in hands.OrderBy(i=>i.Slot))ItemHandler.EquipItem(new ItemUsage(true){UID=hand.Uid,dwParam=hand.Slot,Action=ItemUsage.Mode.EquipItem},p);
            Require(hands.All(h=>p.Equipment.TryGetItem((byte)h.Slot)!=null && p.Equipment.TryGetItem((byte)h.Slot).UID==h.Uid));
            now=start.AddMinutes(10);HeartTalk(p,91110,2);now=now.AddSeconds(9);Call(feature,"CompleteExtract",p);Require(p.Inventory.Count==count && p.Entity.MapID==map.ID);
            Revora.Game.Features.ExpeditionEvent.NotifyAction(p,"controlled interruption");now=now.AddSeconds(2);Call(feature,"CompleteExtract",p);Require(p.Inventory.Count==count);
            HeartTalk(p,91110,2);now=now.AddSeconds(11);Revora.Game.Features.ExpeditionEvent.Tick();
            Require(p.Entity.MapID==1002 && p.Inventory.Count==count+4 && p.Entity.RevoraPoint==rvp+660 && !PirateFenced(p));
            var rewards=p.Inventory.Objects.Where(i=>!before.Contains(i.UID)).ToArray();Require(rewards.Length==4 && rewards.Count(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1);
            Require((long)Sql("SELECT count(*) FROM revora_expedition_claims r JOIN items i ON i.uid=r.soul_uid WHERE r.uid=@uid AND i.entityid=r.uid AND i.id=r.soul_type AND i.bound=1","uid",(long)CareerUid)==2);
            Require(EntityTable.SaveEntity(p));Sql("CREATE TABLE revora_retained_unarmed_probe(snapshot text);INSERT INTO revora_retained_unarmed_probe VALUES(@snapshot)","snapshot",RetainedUnarmedSnapshot(p,recipient,eliteRun));
        }
        Require(p.Entity.MapID==1002 && RetainedUnarmedSnapshot(p,recipient,eliteRun)==Convert.ToString(Sql("SELECT snapshot FROM revora_retained_unarmed_probe")));
        string once=RetainedUnarmedSnapshot(p,recipient,eliteRun);Call(feature,"CompleteExtract",p);Require(once==RetainedUnarmedSnapshot(p,recipient,eliteRun));
        Console.WriteLine("[career-process] career-retained-unarmed-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Retained Extraction did not close.");
    }
}
