using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;
using People = Revora.Database.KnownPersons;

internal static partial class PotencyHuntTests
{
    static readonly Type MonsterLoot=typeof(Server).Assembly.GetType("Revora.Game.Features.MonsterEquipmentDrops");
    static uint lootMonster=610000;
    static Entity LootMonster(GameState p)
    {
        var info=new MonsterInformation { ID=1,Name="FixtureLoot",Level=40 };
        var mob=new Entity(EntityFlag.Monster,false){UID=++lootMonster,MapID=p.Map.ID,X=p.Entity.X,Y=p.Entity.Y,Name=info.Name,MonsterInfo=info,Hitpoints=0,DeathStamp=Time32.Now};
        info.Owner=mob;p.Map.Entities.Add(mob.UID,mob);return mob;
    }
    static FloorItem EquipmentDrop(GameState p)
    {
        var existing=p.Map.FloorItems.Keys.ToArray();Call(MonsterLoot,null,"TryDrop",LootMonster(p),p.Entity);
        return p.Map.FloorItems.Values.Single(i=>!existing.Contains(i.UID));
    }
    static byte[] LootPacket(FloorItem drop)
    { var q=new FloorItem(true){UID=drop.UID,Type=FloorItem.Remove,ItemID=730001,X=1,Y=1};var frame=q.ToArray();Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);return frame; }
    static void Loot(GameState p,FloorItem drop)
    { p.Entity.X=drop.X;p.Entity.Y=drop.Y;FloorItem.Process(LootPacket(drop),p); }
    static void VerifyMonsterEquipment(Map map)
    {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL);CREATE TABLE apprentice (LIKE public.apprentice INCLUDING ALL);CREATE TABLE itemadding (LIKE public.itemadding INCLUDING ALL)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);Call(EvidenceType,null,"Initialize");Call(Admissions,null,"Initialize");Call(MonsterLoot,null,"Initialize");ConquerItem.ItemUID=new Counter(4970000);
        MonsterLoot.GetField("Roll",Hidden).SetValue(null,new Func<int,int>(max=>0));Config.MonsterPlusEquipmentBasisPoints=100;
        var mentor=DeparturePlayer(map,130);var child=DeparturePlayer(map,100);uint childWardrobe=StoredQuestGarment(child);MentorLink(mentor,child,0);var drop=EquipmentDrop(child);uint itemUid=drop.Item.UID;Loot(child,drop);
        Check(!Fenced(child) && child.Account.State==AccountTable.AccountState.Player && child.Inventory.ContainsUID(itemUid) && !map.FloorItems.ContainsKey(drop.UID)
            && drop.Item.Plus==1 && !drop.Item.Bound && drop.Item.StackSize==1 && !drop.Item.MobDropped,"ordinary native pickup preserves the exact generated +1 item and removes the floor drop");
        var saved=ConquerItemTable.LoadItem(itemUid);
        Check(saved.ID==drop.Item.ID && saved.Plus==1 && saved.Durability==drop.Item.Durability && saved.MaximDurability==drop.Item.MaximDurability && saved.Color==drop.Item.Color,
            "the equipment's type, plus, durability and appearance survive production item reload");
        Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",childWardrobe)==1,"monster equipment pickup preserves stored wardrobe design");
        Check(StonePoints(child)==1 && mentor.PrizePlusStone==1 && child.AsApprentice.Actual_Plus==1 && child.AsApprentice.Total_Plus==1,"genuine monster pickup earns one durable composing point and refreshes both mentor views");
        Loot(child,drop);Call(MonsterLoot,null,"Pickup",child,drop);People.LoadMentor(child);People.LoadMentor(mentor);
        Check(StonePoints(child)==1 && mentor.PrizePlusStone==1 && Scalar("SELECT count(*) FROM revora_monster_equipment_pickups WHERE collector=@uid",child.Entity.UID)==1,"repeated pickup and relationship reload cannot repeat a contribution");
        var forged=new FloorItem(true){UID=FloorItem.FloorUID.Next,Item=drop.Item,ItemID=drop.Item.ID,MapID=map.ID,X=child.Entity.X,Y=child.Entity.Y,Type=FloorItem.Drop,OnFloor=Time32.Now};forged.Item.MobDropped=true;
        Check(!(bool)Call(MonsterLoot,null,"Pickup",child,forged) && StonePoints(child)==1,"re-dropped, purchased or traded equipment cannot gain provenance from the mutable MobDropped flag");
        var racing=DeparturePlayer(map,100);var racingMentor=DeparturePlayer(map,130);MentorLink(racingMentor,racing,0);var shared=EquipmentDrop(racing);
        Task.WaitAll(Enumerable.Range(0,8).Select(i=>Task.Run(()=>Loot(racing,shared))).ToArray());
        Check(!Fenced(racing) && racing.Inventory.Count==1 && StonePoints(racing)==1,"parallel native pickup requests produce one equipment item and one composing point");
        var outsider=DeparturePlayer(map,100);var held=EquipmentDrop(racing);Loot(outsider,held);
        Check(!held.PickedUpAlready && outsider.Inventory.Count==0,"the killer's ownership window protects generated equipment");
        held.OnFloor=Time32.Now.AddSeconds(-Constants.FloorItemAvailableAfter);Loot(outsider,held);
        Check(!Fenced(outsider) && held.PickedUpAlready && outsider.Inventory.Count==1 && StonePoints(racing)==1,"after ownership expires the collector receives the item without crediting the killer's mentor");
        var offline=DeparturePlayer(map,130);var online=DeparturePlayer(map,100);MentorLink(offline,online,0);Kernel.GamePool.Remove(offline.Entity.UID);Loot(online,EquipmentDrop(online));Kernel.GamePool[offline.Entity.UID]=offline;People.LoadMentor(offline);
        Check(offline.PrizePlusStone==1 && StonePoints(online)==1,"an offline mentor retains the collector's composing point for later claims");
        var finish=DeparturePlayer(map,130);var farmer=DeparturePlayer(map,100);MentorLink(finish,farmer,0);
        uint mentorWardrobe=StoredQuestGarment(finish),farmerWardrobe=StoredQuestGarment(farmer);
        for(int n=0;n<100;n++)
        {
            var loot=EquipmentDrop(farmer);Loot(farmer,loot);
            if(Fenced(farmer))throw new Exception("Farm fixture pickup failed.");
            Query("UPDATE items SET warehouse=1002 WHERE uid=@uid","uid",(long)loot.Item.UID);farmer.Inventory.Remove(loot.Item.UID,Enums.ItemUse.None,false);
        }
        ClaimStones(finish);
        Check(!Fenced(finish) && StoneCount(finish)==1 && StonePoints(farmer)==0
            && Scalar("SELECT count(*) FROM revora_monster_equipment_pickups WHERE collector=@uid",farmer.Entity.UID)==100,
            "one hundred actual generated-and-picked items earn one claimable +1 Stone end to end");
        var claimed=ConquerItemTable.LoadItem(finish.Inventory.Objects.Single().UID);
        Check(claimed.ID==730001 && claimed.Plus==1 && !claimed.Bound
            && Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",mentorWardrobe)==1
            && Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",farmerWardrobe)==1,
            "earned mentor stone survives production item load and preserves both stored wardrobe designs");
        var integrated=DeparturePlayer(map,130);var realDeath=LootMonster(integrated);int prior=map.FloorItems.Count;
        bool money=Config.MoneyDropEnabled;Config.MoneyDropEnabled=false;
        try { realDeath.MonsterInfo.Drop(integrated.Entity); } finally { Config.MoneyDropEnabled=money; }
        Check(map.FloorItems.Count==prior+1,"the production monster Drop dispatcher actually reaches the restored equipment source");
        var competitive=EquipmentDrop(child);competitive.OnFloor=Time32.Now.AddSeconds(-Constants.FloorItemAvailableAfter);var rival=DeparturePlayer(map,100);var rivalMentor=DeparturePlayer(map,130);MentorLink(rivalMentor,rival,0);
        uint originalCount=(uint)child.Inventory.Count;Task.WaitAll(Task.Run(()=>Loot(child,competitive)),Task.Run(()=>Loot(rival,competitive)));
        Check(!Fenced(child) && !Fenced(rival) && child.Inventory.Count+rival.Inventory.Count==originalCount+1
            && StonePoints(child)+StonePoints(rival)==2,"two eligible collectors racing for one floor item earn only one additional composing point");
        var atDistance=EquipmentDrop(child);child.Entity.X=(ushort)(atDistance.X+1);child.Entity.Y=atDistance.Y;FloorItem.Process(LootPacket(atDistance),child);
        Check(!atDistance.PickedUpAlready,"forged pickup coordinates cannot replace the collector's real map position");
        VerifyLootGuards(map);VerifyLootFailures(map);
    }
    static void VerifyLootGuards(Map map)
    {
        var p=DeparturePlayer(map,130);var mob=LootMonster(p);int before=map.FloorItems.Count;
        MonsterLoot.GetField("Roll",Hidden).SetValue(null,new Func<int,int>(max=>max-1));Call(MonsterLoot,null,"TryDrop",mob,p.Entity);
        MonsterLoot.GetField("Roll",Hidden).SetValue(null,new Func<int,int>(max=>0));Call(MonsterLoot,null,"TryDrop",mob,p.Entity);
        Check(map.FloorItems.Count==before,"an unsuccessful roll cannot be retried on the same monster death");
        mob.DeathStamp=mob.DeathStamp.AddSeconds(1);Call(MonsterLoot,null,"TryDrop",mob,p.Entity);Call(MonsterLoot,null,"TryDrop",mob,p.Entity);
        Check(map.FloorItems.Count==before+1,"a new monster life can roll once while duplicate death dispatch cannot add another drop");
        foreach(string mode in new[]{"alive","boss","guard","reviver","companion","unregistered","wrong-map"})
        {
            var invalid=LootMonster(p);if(mode=="alive")invalid.Hitpoints=1;else if(mode=="boss")invalid.MonsterInfo.Boss=true;else if(mode=="guard")invalid.MonsterInfo.Guard=true;
            else if(mode=="reviver")invalid.MonsterInfo.Reviver=true;else if(mode=="companion")invalid.Companion=true;else if(mode=="unregistered")map.Entities.Remove(invalid.UID);else invalid.MapID=1036;
            int count=map.FloorItems.Count;Call(MonsterLoot,null,"TryDrop",invalid,p.Entity);Check(map.FloorItems.Count==count,"ineligible monster cannot become a composing source: "+mode);
        }
        foreach(int chance in new[]{0,-1})
        { Config.MonsterPlusEquipmentBasisPoints=chance;int count=map.FloorItems.Count;Call(MonsterLoot,null,"TryDrop",LootMonster(p),p.Entity);Check(map.FloorItems.Count==count,"nonpositive configured chance disables the equipment source"); }
        Config.MonsterPlusEquipmentBasisPoints=100;
        var replacement=EquipmentDrop(p);map.RemoveFloorItem(replacement);
        var second=new FloorItem(true){UID=replacement.UID,MapID=map.ID,X=replacement.X,Y=replacement.Y,Type=FloorItem.Drop};map.AddFloorItem(second);map.RemoveFloorItem(replacement);
        Check(map.FloorItems.ContainsKey(second.UID) && ReferenceEquals(map.FloorItems[second.UID],second),"expiry of a stale drop object cannot remove a replacement with the same floor UID");map.RemoveFloorItem(second);
        var counter=FloorItem.FloorUID;FloorItem.FloorUID=new Counter(8000000);
        var reservedNpc=new NpcSpawn { UID=8000001 };map.Npcs.Add(reservedNpc.UID,reservedNpc);
        var reservedDrop=new FloorItem(true){UID=8000002,MapID=map.ID,X=p.Entity.X,Y=p.Entity.Y,Type=FloorItem.Drop};map.AddFloorItem(reservedDrop);
        try
        {
            var unique=EquipmentDrop(p);Check(unique.UID==8000003 && map.Npcs.ContainsKey(reservedNpc.UID) && ReferenceEquals(map.FloorItems[reservedDrop.UID],reservedDrop),
                "generated floor identity skips both existing NPC and floor-item identities");
        }
        finally { FloorItem.FloorUID=counter;map.Npcs.Remove(reservedNpc.UID);map.RemoveFloorItem(reservedDrop); }
        var invalidFrame=EquipmentDrop(p);
        foreach(string mode in new[]{"short","length","family","seal","oversize"})
        {
            var frame=LootPacket(invalidFrame);if(mode=="short")Array.Resize(ref frame,12);else if(mode=="length")frame[0]--;else if(mode=="family")frame[2]++;
            else if(mode=="seal")frame[frame.Length-1]^=1;else Array.Resize(ref frame,129);
            p.Entity.X=invalidFrame.X;p.Entity.Y=invalidFrame.Y;FloorItem.Process(frame,p);
            Check(!Fenced(p) && !invalidFrame.PickedUpAlready && !p.Inventory.ContainsUID(invalidFrame.Item.UID),"malformed floor pickup cannot grant equipment: "+mode);
        }
        var expired=EquipmentDrop(p);expired.OnFloor=Time32.Now.AddSeconds(-Constants.FloorItemSeconds);Loot(p,expired);
        Check(!Fenced(p) && !expired.PickedUpAlready,"expired generated equipment cannot be collected from a stale screen");
        var removed=EquipmentDrop(p);map.RemoveFloorItem(removed);Loot(p,removed);
        Check(!removed.PickedUpAlready && !p.Inventory.ContainsUID(removed.Item.UID),"a removed map object cannot be recovered by a stale pickup request");
        var full=DeparturePlayer(map,130);for(int n=0;n<40;n++)Pack(full,1088001,1);var wait=EquipmentDrop(full);Loot(full,wait);
        Check(!Fenced(full) && !wait.PickedUpAlready && full.Inventory.Count==40,"a full inventory leaves equipment and its composing entitlement on the ground");
    }
    static void VerifyLootFailures(Map map)
    {
        foreach(string mode in new[]{"credit","item","receipt","silent-item","silent-receipt"})
        {
            var mentor=DeparturePlayer(map,130);var child=DeparturePlayer(map,100);MentorLink(mentor,child,0);var drop=EquipmentDrop(child);
            string table=mode=="credit"?"apprentice":mode.EndsWith("item")?"items":"revora_monster_equipment_pickups";
            string op=mode=="credit"?"UPDATE OF actual_plus":"INSERT";string effect=mode.StartsWith("silent")?"RETURN NULL;":"RAISE EXCEPTION ''fixture loot failure'';";
            Query("CREATE FUNCTION fail_loot() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN "+effect+" END';CREATE TRIGGER fail_loot BEFORE "+op+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION fail_loot()");Loot(child,drop);
            Check(Fenced(child) && !drop.PickedUpAlready && map.FloorItems.ContainsKey(drop.UID) && child.Inventory.Count==0 && StonePoints(child)==0
                && Scalar("SELECT count(*) FROM items WHERE uid=@uid",drop.Item.UID)==0,"equipment, composing credit and receipt roll back together: "+mode);
            Query("DROP TRIGGER fail_loot ON "+table+";DROP FUNCTION fail_loot()");DemonReset(child);People.LoadMentor(child);Loot(child,drop);
            Check(!Fenced(child) && drop.PickedUpAlready && child.Inventory.Count==1 && StonePoints(child)==1,"a failed pickup can recover its still-live floor item after reload: "+mode);
        }
        var stale=DeparturePlayer(map,130);var hidden=Pack(stale,1088001,1);stale.Inventory.Remove(hidden.UID,Enums.ItemUse.None,false);var dropStale=EquipmentDrop(stale);Loot(stale,dropStale);
        Check(Fenced(stale) && !dropStale.PickedUpAlready,"a stale inventory image cannot receive equipment or its contribution");
        var mentorDup=DeparturePlayer(map,130);var childDup=DeparturePlayer(map,100);MentorLink(mentorDup,childDup,0);Query("INSERT INTO apprentice SELECT * FROM apprentice WHERE apprenticeid=@uid","uid",(long)childDup.Entity.UID);var ambiguous=EquipmentDrop(childDup);Loot(childDup,ambiguous);
        Check(Fenced(childDup) && !ambiguous.PickedUpAlready && childDup.Inventory.Count==0 && AdmissionRows(childDup)==2,"duplicate historical memberships preserve the loot and original rows for repair");
    }
}
