using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type HuntLoot = typeof(Entity).Assembly.GetType("Revora.Game.Features.DailyHuntDrops");
    static int huntRoll;
    static long HuntPickups(GameState p) { return Scalar("SELECT count(*) FROM revora_daily_hunt_pickups WHERE collector=@uid",p.Entity.UID); }
    static Entity HuntMonster(GameState p, string name="Macaque") {
        var mob=LootMonster(p);var info=MonsterInformation.MonsterInformations.Values.First(m=>m.Name==name).Copy();
        mob.MonsterInfo=info;info.Owner=mob;mob.Name=info.Name;return mob;
    }
    static FloorItem HuntDrop(GameState p) {
        var before=p.Map.FloorItems.Keys.ToArray();Call(HuntLoot,null,"TryDrop",HuntMonster(p),p.Entity);
        return p.Map.FloorItems.Values.Single(i=>!before.Contains(i.UID));
    }
    static GameState HuntPlayer(Map map) {
        var p=BallPlayer(map);var xy=FlowerTile(map,608,556);p.Entity.X=xy.Item1;p.Entity.Y=xy.Item2;EntityTable.SaveEntity(p);return p;
    }
    static void VerifyHuntLoot(string runtime,Map town) {
        VerifyFirstRebirth(runtime,town);Call(HuntLoot,null,"Initialize");Call(HuntLoot,null,"Initialize");Call(MonsterLoot,null,"Initialize");
        MonsterLoot.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>max-1));
        HuntLoot.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>huntRoll));Config.DailyPriceHuntDropPercent=40;
        var names=new[]{"BanditL97","BanditL98","Macaque","MacaqueL48","SandMonster","SandMonsterL68","Bandit","BanditL33","HillMonster"};
        var maps=new ushort[]{1015,1015,1020,1020,1000,1000,1011,1011,1000};
        var types=new uint[]{729096,729096,729101,729101,729099,729099,729093,729093,1080001};
        var xs=new ushort[]{396,396,608,608,611,611,212,212,611};var ys=new ushort[]{528,528,556,556,636,636,372,372,636};
        foreach(ushort id in maps.Distinct())World(runtime,id);
        bool money=Config.MoneyDropEnabled,cpRewards=Config.KillConquerPointsEnabled;
        Config.MoneyDropEnabled=false;Config.KillConquerPointsEnabled=false;
        try {
            for(int index=0;index<names.Length;index++) {
                var map=Kernel.Maps[maps[index]];var p=BallPlayer(map);var xy=FlowerTile(map,xs[index],ys[index]);p.Entity.X=xy.Item1;p.Entity.Y=xy.Item2;
                EntityTable.SaveEntity(p);var mob=HuntMonster(p,names[index]);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT count(*) FROM public.monsterspawns WHERE npctype=@family AND mapid=@map",c)) {
                    q.Parameters.AddWithValue("family",(long)mob.MonsterInfo.ID);q.Parameters.AddWithValue("map",(int)map.ID);
                    Check(Convert.ToInt64(q.ExecuteScalar())>0,"installed hunting source "+names[index]+" family="+mob.MonsterInfo.ID+" map="+map.ID);
                }}
                p.Entity.EvreyThink=index%2==0; // Both completed and unaccepted players can hunt tradable materials.
                mob.Hitpoints=mob.MaxHitpoints=100;mob.IsDropped=false;var before=map.FloorItems.Keys.ToArray();
                if(types[index]==1080001){uint damage=mob.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,mob,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=mob.UID},ref damage,null);}
                else mob.Die(p.Entity);
                var drop=map.FloorItems.Values.Single(i=>!before.Contains(i.UID) && i.ItemID==types[index]);
                Check(p.Account.State==AccountTable.AccountState.Player && p.Entity.PriceItemId==0 && drop.Item.StackSize==1
                    && !drop.Item.Bound && drop.OwnerUID==p.Entity.UID,"ordinary real death grants the material before acceptance/after completion: "+names[index]);
                int count=map.FloorItems.Count;Parallel.For(0,10,i=>Call(HuntLoot,null,"TryDrop",mob,p.Entity));
                Check(map.FloorItems.Count==count,"one material roll per actual monster life: "+names[index]);
                Loot(p,drop);Check(!Fenced(p) && drop.PickedUpAlready && HuntPickups(p)==1 && p.Inventory.ContainsUID(drop.Item.UID),"native pickup saves earned material and provenance: "+names[index]);
                p=ReloadBall(p);var saved=p.Inventory.Objects.Single(i=>i.UID==drop.Item.UID);
                Check(saved.ID==types[index] && !saved.Bound && saved.StackSize==1 && HuntPickups(p)==1,"ordinary production reload preserves the earned tradable material: "+names[index]);
                if(types[index]==1080001) {
                    var job=World(runtime,1004);
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT id,lookface,cellx,celly FROM public.npcs WHERE id=10022 AND mapid=1004",c))
                    using(var r=q.ExecuteReader()) {
                        if(!r.Read())throw new Exception("Installed Trojan trainer missing.");
                        job.AddNpc(new NpcSpawn{UID=10022,MapID=1004,Mesh=Convert.ToUInt16(r[1]),X=Convert.ToUInt16(r[2]),Y=Convert.ToUInt16(r[3]),Type=Enums.NpcType.Talker});
                    }}
                    p.Entity.Class=12;p.Entity.Teleport(1004,job.Npcs[10022].X,job.Npcs[10022].Y);p.ActiveNpc=10022;
                    p.Entity.MyAchievement=new Achievement(p.Entity);p.Entity.MyAchievement.Load("");
                    if(!EntityTable.SaveEntity(p))throw new Exception("Emerald promotion preparation failed.");
                    PromotionPrepare(p);PromotionNpc(p,3);
                    Check(!Fenced(p) && p.Entity.Class==13 && !p.Inventory.ContainsUID(saved.UID) && HasAchievement(p,10821),
                        "ordinary trainer consumes the hunted Emerald and records rank-three promotion");
                    p=ReloadBall(p);
                    Check(p.Entity.Class==13 && !p.Inventory.ContainsUID(saved.UID) && PromotionCount(p)==1,
                        "earned Emerald consumption and promotion survive full reload");
                }
            }
        } finally {Config.MoneyDropEnabled=money;Config.KillConquerPointsEnabled=cpRewards;}
        var desert=Kernel.Maps[1000];var emeraldHunter=HuntPlayer(desert);
        Config.DailyPriceHuntDropPercent=0;
        var emeraldFamily=MonsterInformation.MonsterInformations.Values.First(m=>m.Name=="HillMonster");
        int emeraldChance=Config.AdjustedItemDropRate(emeraldFamily.OwnItemRate);
        Check(emeraldFamily.OwnItemID==1080001 && emeraldChance>0,"installed HillMonster Emerald rate remains reachable: "+emeraldChance);
        foreach(int roll in new[]{0,Math.Max(0,emeraldChance-1),Math.Min(99,emeraldChance),99}.Distinct()) {
            huntRoll=roll;int before=desert.FloorItems.Count;
            Call(HuntLoot,null,"TryDrop",HuntMonster(emeraldHunter,"HillMonster"),emeraldHunter.Entity);
            Check(desert.FloorItems.Count==before+(roll<emeraldChance?1:0),"Emerald preserves installed rate independently of daily quest chance: "+roll);
        }
        Config.DailyPriceHuntDropPercent=40;
        var ape=Kernel.Maps[1020];var hunter=HuntPlayer(ape);
        foreach(int roll in new[]{0,39,40,99}) {
            huntRoll=roll;int before=ape.FloorItems.Count;Call(HuntLoot,null,"TryDrop",HuntMonster(hunter),hunter.Entity);
            Check(ape.FloorItems.Count==before+(roll<40?1:0),"40-percent configured chance boundary: "+roll);
        }
        huntRoll=40;var failed=HuntMonster(hunter);int ground=ape.FloorItems.Count;Call(HuntLoot,null,"TryDrop",failed,hunter.Entity);
        huntRoll=0;Call(HuntLoot,null,"TryDrop",failed,hunter.Entity);Check(ape.FloorItems.Count==ground,"a failed roll cannot be rerun for the same corpse");
        failed.DeathStamp=failed.DeathStamp.AddSeconds(1);Call(HuntLoot,null,"TryDrop",failed,hunter.Entity);Call(HuntLoot,null,"TryDrop",failed,hunter.Entity);
        Check(ape.FloorItems.Count==ground+1,"a later respawn life rolls once");
        foreach(byte chance in new byte[]{0,101}) {
            Config.DailyPriceHuntDropPercent=chance;huntRoll=99;ground=ape.FloorItems.Count;Call(HuntLoot,null,"TryDrop",HuntMonster(hunter),hunter.Entity);
            Check(ape.FloorItems.Count==ground+(chance>100?1:0),"drop chance clamps to zero through one hundred: "+chance);
        }
        Config.DailyPriceHuntDropPercent=40;huntRoll=0;
        foreach(string invalid in new[]{"living","family","name","companion","boss","guard","reviver","foreign-owner","noncanonical","wrong-map","fake-killer"}) {
            var mob=HuntMonster(hunter);ground=ape.FloorItems.Count;
            if(invalid=="living")mob.Hitpoints=100;if(invalid=="family")mob.MonsterInfo.ID=1;if(invalid=="name")mob.MonsterInfo.Name="Bandit";
            if(invalid=="companion")mob.Companion=true;if(invalid=="boss")mob.MonsterInfo.Boss=true;if(invalid=="guard")mob.MonsterInfo.Guard=true;
            if(invalid=="reviver")mob.MonsterInfo.Reviver=true;if(invalid=="foreign-owner")mob.MonsterInfo.Owner=HuntMonster(hunter);
            if(invalid=="noncanonical")ape.Entities.Remove(mob.UID);if(invalid=="wrong-map")mob.MapID=1002;if(invalid=="fake-killer")hunter.Fake=true;
            Call(HuntLoot,null,"TryDrop",mob,hunter.Entity);hunter.Fake=false;Check(ape.FloorItems.Count==ground,"invalid source cannot create a quest material: "+invalid);
        }
        var packetDrop=HuntDrop(hunter);
        foreach(string invalid in new[]{"short","length","family","seal","oversize"}) {
            var packet=LootPacket(packetDrop);if(invalid=="short")Array.Resize(ref packet,16);else if(invalid=="length")packet[0]--;
            else if(invalid=="family")packet[2]++;else if(invalid=="seal")packet[packet.Length-1]^=1;else Array.Resize(ref packet,129);
            hunter.Entity.X=packetDrop.X;hunter.Entity.Y=packetDrop.Y;FloorItem.Process(packet,hunter);
            Check(!packetDrop.PickedUpAlready && HuntPickups(hunter)==0,"malformed hunt pickup rejected: "+invalid);
        }
        hunter.Entity.X++;FloorItem.Process(LootPacket(packetDrop),hunter);Check(!packetDrop.PickedUpAlready,"server position is authoritative for floor pickup");
        var stranger=HuntPlayer(ape);Loot(stranger,packetDrop);Check(!packetDrop.PickedUpAlready && HuntPickups(stranger)==0,"fresh material protects the actual killer");
        Loot(hunter,packetDrop);Parallel.For(0,12,i=>Call(HuntLoot,null,"Pickup",hunter,packetDrop));
        Check(!Fenced(hunter) && HuntPickups(hunter)==1,"repeated pickup commits just one material");
        var race=HuntDrop(hunter);race.OnFloor=Time32.Now.AddSeconds(-Constants.FloorItemAvailableAfter);
        Task.WaitAll(Task.Run(()=>Loot(hunter,race)),Task.Run(()=>Loot(stranger,race)));
        Check(!Fenced(hunter) && !Fenced(stranger) && race.PickedUpAlready && HuntPickups(hunter)+HuntPickups(stranger)==2,"two eligible collectors receive exactly one released material");
        var direct=HuntDrop(hunter);hunter.Entity.X=direct.X;hunter.Entity.Y=direct.Y;hunter.Screen.Add(direct);long receipts=HuntPickups(hunter);
        ItemHandler.PickupItem(new FloorItem(true){UID=direct.UID},hunter);
        Check(!Fenced(hunter) && direct.PickedUpAlready && HuntPickups(hunter)==receipts+1,"auto-hunt direct pickup uses the same atomic material delivery");
        foreach(string fault in new[]{"items","revora_daily_hunt_pickups"}) {
            var p=HuntPlayer(ape);var drop=HuntDrop(p);
            Query("CREATE FUNCTION suppress_hunt() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_hunt BEFORE INSERT ON "+fault+" FOR EACH ROW EXECUTE FUNCTION suppress_hunt()");
            try{Loot(p,drop);}finally{Query("DROP TRIGGER suppress_hunt ON "+fault+";DROP FUNCTION suppress_hunt()");}
            Check(Fenced(p) && !drop.PickedUpAlready && HuntPickups(p)==0 && p.Inventory.Count==0
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==0,"suppressed "+fault+" leaves the material on the ground and rolls back the receipt");
            p=ReloadBall(p);Loot(p,drop);Check(!Fenced(p) && drop.PickedUpAlready && HuntPickups(p)==1,"reconnected player retries unpaid pickup: "+fault);
        }
        var full=HuntPlayer(ape);for(int i=0;i<40;i++)Pack(full,1088001,1);var waiting=HuntDrop(full);Loot(full,waiting);
        Check(!Fenced(full) && !waiting.PickedUpAlready && HuntPickups(full)==0,"full bag leaves the drop available");
        DisRemove(full,full.Inventory.Objects[0]);Loot(full,waiting);Check(!Fenced(full) && full.Inventory.Count==40 && HuntPickups(full)==1,"making one space permits one material pickup");
        var expired=HuntDrop(hunter);expired.OnFloor=Time32.Now.AddSeconds(-Constants.FloorItemSeconds);receipts=HuntPickups(hunter);Loot(hunter,expired);
        Check(!expired.PickedUpAlready && HuntPickups(hunter)==receipts,"expired drops cannot be claimed");
        foreach(var oldDrop in ape.FloorItems.Values.ToArray())ape.RemoveFloorItem(oldDrop);
        foreach(string change in new[]{"type","uid","bound","stack","durability","owner","coordinate"}) {
            var p=HuntPlayer(ape);var drop=HuntDrop(p);
            if(change=="type")drop.Item.ID++;if(change=="uid")drop.Item.UID++;if(change=="bound")drop.Item.Bound=true;
            if(change=="stack")drop.Item.StackSize=2;if(change=="durability")drop.Item.MaximDurability++;
            if(change=="owner")drop.OwnerUID++;if(change=="coordinate")drop.X++;
            p.Entity.X=change=="coordinate"?(ushort)(drop.X-1):drop.X;p.Entity.Y=drop.Y;FloorItem.Process(LootPacket(drop),p);
            Check(Fenced(p) && !drop.PickedUpAlready && HuntPickups(p)==0,"mutated origin cannot mint a material: "+change);
        }
    }
}
