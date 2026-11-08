using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string RetainedLabBoxSnapshot(GameState p,GameState recipient,string eliteRun) {
        return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,'boxes',(SELECT json_agg(r ORDER BY box_uid) FROM revora_lab_boxes r WHERE uid=@uid))::text",
            "career",RetainedUnarmedSnapshot(p,recipient,eliteRun),"uid",(long)CareerUid));
    }
    static void RetainedLabBoxProcess(bool reload) {
        careerRitesFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;
        var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);Revora.Database.KnownPersons.LoadMentor(p);
        new CoatStorage().Login(p);new TitleStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);WayofHeroes.Load(p);EntityVariableTable.Load(p);Call(typeof(Revora.Database.JiangHu),"LoadClient",p);
        nextUid=4294918104;var recipient=Player(Kernel.Maps[1002],true);recipient.LoadItemStats();ChiTable.Load(recipient);WayofHeroes.Load(recipient);
        nextUid=4294918206;var storage=Player(Kernel.Maps[1002],true);storage.LoadItemStats();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));


        string eliteRun=Convert.ToString(Sql("SELECT run FROM revora_retained_elite_combat_probe"));
        if(!reload) {
            Require((bool)Sql("SELECT to_regclass('revora_retained_lab_box_probe') IS NULL"));
            Require(RetainedUnarmedSnapshot(p,recipient,eliteRun)==Convert.ToString(Sql("SELECT snapshot FROM revora_retained_unarmed_probe")));
            uint boxUid=Convert.ToUInt32(Sql("SELECT i.uid FROM items i JOIN revora_lab_exchanges e ON i.uid=ANY(e.rewards) AND e.uid=i.entityid WHERE i.entityid=@uid AND i.id=721540 AND e.kind=3 AND NOT EXISTS(SELECT 1 FROM revora_lab_boxes b WHERE b.box_uid=i.uid) ORDER BY i.uid LIMIT 1","uid",(long)CareerUid));
            ConquerItem box;
            if(!p.Inventory.TryGetItem(boxUid,out box)) {
                var bank=p.Warehouses.Single(w=>w.Value.Objects.Any(i=>i.UID==boxUid));uint banker=(uint)bank.Key;
                var town=new ushort[]{1002,1011,1020,1000,1015,1036,1213}.Select(id=>Kernel.Maps[id]).Single(m=>m.Npcs.ContainsKey(banker));var npc=town.Npcs[banker];
                p.Entity.Teleport(town.ID,npc.X,npc.Y);GrailWarehouse(p,boxUid,2,banker);Require(p.Inventory.TryGetItem(boxUid,out box));
            }
            Require(box.Bound && p.Inventory.Count<=39);var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();ItemHandler.UseItem(box,p);
            Require(!p.Inventory.ContainsUID(boxUid) && !PirateFenced(p));
            int roll=Convert.ToInt32(Sql("SELECT roll FROM revora_lab_boxes WHERE box_uid=@box AND uid=@uid","box",(long)boxUid,"uid",(long)CareerUid));
            var saved=(long[])Sql("SELECT rewards FROM revora_lab_boxes WHERE box_uid=@box","box",(long)boxUid);var added=p.Inventory.Objects.Where(i=>!before.Contains(i.UID)).ToArray();
            Require(saved.OrderBy(i=>i).SequenceEqual(added.Select(i=>(long)i.UID).OrderBy(i=>i)) && added.All(i=>i.Bound));
            Require(roll>=0 && roll<100 && (roll<60 ? added.Length==2 && added.All(i=>i.ID==1088001) : roll<90 ? added.Length==1 && new uint[]{700001,700011,700021,700031,700041,700051,700061,700071}.Contains(added[0].ID) : added.Length==1 && added[0].ID==1088000));
            Console.WriteLine("[retained-lab-box] box="+boxUid+" roll="+roll+" items="+string.Join(",",added.Select(i=>i.ID)));
            Require(EntityTable.SaveEntity(p));string once=RetainedLabBoxSnapshot(p,recipient,eliteRun);ItemHandler.UseItem(box,p);Require(once==RetainedLabBoxSnapshot(p,recipient,eliteRun));
            Sql("CREATE TABLE revora_retained_lab_box_probe(box bigint,snapshot text);INSERT INTO revora_retained_lab_box_probe VALUES(@box,@snapshot)","box",(long)boxUid,"snapshot",once);
        }
        Require(RetainedLabBoxSnapshot(p,recipient,eliteRun)==Convert.ToString(Sql("SELECT snapshot FROM revora_retained_lab_box_probe")));
        uint consumed=Convert.ToUInt32(Sql("SELECT box FROM revora_retained_lab_box_probe"));Require(!p.Inventory.ContainsUID(consumed));
        var stale=new ConquerItem(true){UID=consumed,ID=721540,Bound=true,StackSize=1};ItemHandler.UseItem(stale,p);
        Require(!PirateFenced(p) && RetainedLabBoxSnapshot(p,recipient,eliteRun)==Convert.ToString(Sql("SELECT snapshot FROM revora_retained_lab_box_probe")));
        Console.WriteLine("[career-process] career-retained-lab-box-"+(reload?"reload":"open")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Retained Lab box did not close.");
    }
}
