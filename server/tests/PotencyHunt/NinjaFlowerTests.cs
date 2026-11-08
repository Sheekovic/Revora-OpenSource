using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type NinjaFlowers=typeof(Entity).Assembly.GetType("Revora.Game.Features.NinjaFlowerHunt");
    static int ninjaFlowerRoll;
    static long FlowerReceipts(GameState p){return Scalar("SELECT count(*) FROM revora_ninja_flower_pickups WHERE collector=@uid",p.Entity.UID);}
    static void PrepareNinjaFlowers(string runtime) {
        var map=World(runtime,1700);
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand("SELECT id,cellx,celly,lookface,type FROM public.npcs WHERE mapid=1700",c))
        using(var r=q.ExecuteReader())while(r.Read())map.AddNpc(new NpcSpawn{UID=Convert.ToUInt32(r[0]),MapID=1700,X=Convert.ToUInt16(r[1]),Y=Convert.ToUInt16(r[2]),Mesh=Convert.ToUInt16(r[3]),Type=(Enums.NpcType)Convert.ToByte(r[4])});}
    }
    static GameState FlowerHunter(Map job) {var p=EpicPlayer(job);p.Quests=new Quests(p);p.Quests.Load();EpicNpc(p,4720,87);if(p.Entity.MapID!=1700 || Fenced(p))throw new Exception("Flower passage failed.");return p;}
    static FloorItem NinjaFlower(GameState p,int roll,uint family=3600) {
        foreach(var old in p.Map.FloorItems.Values.Where(i=>!(bool)Call(NinjaFlowers,null,"Managed",i)).ToArray())p.Map.RemoveFloorItem(old);
        ninjaFlowerRoll=roll;var before=p.Map.FloorItems.Keys.ToArray();var body=AbyssWild(p,family);body.MaxHitpoints=body.Hitpoints=100;body.Die(p.Entity);
        var result=p.Map.FloorItems.Values.Single(i=>!before.Contains(i.UID) && (i.ItemID==722732 || i.ItemID==722736));
        int count=p.Map.FloorItems.Count;Parallel.For(0,8,_=>Call(NinjaFlowers,null,"TryDrop",body,p.Entity));
        Check(p.Map.FloorItems.Count==count,"one flower roll per canonical monster death");p.Map.RemoveEntity(body);return result;
    }
    static void HarvestFlowers(GameState p) {for(int n=0;n<4;n++)foreach(int color in new[]{0,1}){var drop=NinjaFlower(p,color);Loot(p,drop);if(Fenced(p))throw new Exception("Flower harvest failed.");}}
    static void FlowerOffering(GameState p) {
        HarvestFlowers(p);EpicNpc(p,3604,90);EpicEnter(p);NinjaRoll(0);EpicNpc(p,199464,4);EpicConfirm(p);
        if(Fenced(p) || EpicAmount(p,722732)!=0 || EpicAmount(p,722736)!=0 || EpicAmount(p,3004462)!=1)throw new Exception("Hunted flower offering failed.");
    }
    static void FlowerWheel(GameState p,int roll) {
        FlowerOffering(p);EpicNpc(p,199464,3);EpicNpc(p,199465,1);EpicConfirm(p);EpicNpc(p,199465,3);NinjaRoll(roll);EpicNpc(p,199466,1);EpicConfirm(p);
    }
    static void VerifyNinjaFlowers(string runtime) {
        VerifyNinjaEpicQuest(runtime);bool money=Config.MoneyDropEnabled,cp=Config.KillConquerPointsEnabled;
        Config.MoneyDropEnabled=false;Config.KillConquerPointsEnabled=false;
        try{VerifyNinjaFlowerRoute();}finally{Config.MoneyDropEnabled=money;Config.KillConquerPointsEnabled=cp;}
    }
    static void VerifyNinjaFlowerRoute() {
        var job=Kernel.Maps[1004];var map=Kernel.Maps[1700];
        NinjaFlowers.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>ninjaFlowerRoll));
        var reached=new HashSet<int>{641*map.Floor.Bounds.Width+603};var queue=new Queue<int>(reached);int width=map.Floor.Bounds.Width;
        while(queue.Count>0){int cell=queue.Dequeue(),x=cell%width,y=cell/width;foreach(var a in new[]{new[]{x-1,y},new[]{x+1,y},new[]{x,y-1},new[]{x,y+1}})
            if(a[0]>=0 && a[1]>=0 && a[0]<width && a[1]<map.Floor.Bounds.Height && map.Floor[a[0],a[1],MapObjectType.Player] && reached.Add(a[1]*width+a[0]))queue.Enqueue(a[1]*width+a[0]);}
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand("SELECT npctype,bound_x,bound_y,bound_cx,bound_cy,max_per_gen FROM public.monsterspawns WHERE mapid=1700 AND npctype BETWEEN 3600 AND 3631",c))
        using(var r=q.ExecuteReader())while(r.Read()){
            int x=Convert.ToInt32(r[1]),y=Convert.ToInt32(r[2]),cx=Math.Max(1,Convert.ToInt32(r[3])),cy=Math.Max(1,Convert.ToInt32(r[4]));bool found=false;
            for(int xx=x;xx<x+cx && !found;xx++)for(int yy=y;yy<y+cy && !found;yy++)found=reached.Contains(yy*width+xx);
            Check(Convert.ToInt32(r[5])>0 && found,"installed flower monster spawn is reachable from camp: "+r[0]);
        }}
        var hunter=FlowerHunter(job);hunter.Quests=new Quests(hunter);hunter.Quests.Load();
        Check(hunter.Account.State==AccountTable.AccountState.Player && hunter.Map.Floor[hunter.Entity.X,hunter.Entity.Y,MapObjectType.Player],"ordinary second-reborn Ninja reaches the flower camp without enrolling in the rebirth trial");
        for(uint family=3600;family<=3631;family++){var drop=NinjaFlower(hunter,(int)(family%2),family);Loot(hunter,drop);
            Check(!Fenced(hunter) && drop.PickedUpAlready && hunter.Inventory.ContainsUID(drop.Item.UID),"actual family death and native pickup grant one flower: "+family);}
        Check(FlowerReceipts(hunter)==32 && !hunter.Quests.HasQuest(QuestID.Secound_Quest),"flower earning needs no fabricated quest enrollment");
        hunter=ReloadBall(hunter);Check(FlowerReceipts(hunter)==32 && EpicAmount(hunter,722732)==16 && EpicAmount(hunter,722736)==16,"hunted flowers survive production reload");
        var journey=FlowerHunter(job);
        for(int trial=0;trial<2;trial++){FlowerWheel(journey,99);EpicNpc(journey,199546,254);EpicNpc(journey,4720,87);}
        EpicNpc(journey,3604,90);EpicEnter(journey);EpicNpc(journey,199546,1);EpicConfirm(journey);EpicNpc(journey,199546,254);EpicNpc(journey,4720,81);
        Check(!Fenced(journey) && journey.Spells.ContainsKey(12090) && journey.Entity.ConquerPoints==1000 && FlowerReceipts(journey)==16,"hunted flowers fund failed trials, a Stardust scroll and learned Shadow Clone without spending CPs");
        EpicNpc(journey,4720,87);FlowerWheel(journey,0);var blade=RebirthGear(journey,4,601439);EpicNpc(journey,199446,4);EpicConfirm(journey);journey=ReloadBall(journey);
        Check(!Fenced(journey) && journey.Equipment.TryGetItem(4).UID==blade.UID && journey.Equipment.TryGetItem(4).ID==616439 && journey.Spells.ContainsKey(12090)
            && journey.Entity.ConquerPoints==1000 && FlowerReceipts(journey)==24,"complete hunted-material-to-skill-and-epic journey survives reload without a CP charge");
        VerifyNinjaFlowerFailures(job);
    }
    static void VerifyNinjaFlowerFailures(Map job) {
        foreach(string table in new[]{"items","revora_ninja_flower_pickups"}) {
            var p=FlowerHunter(job);var drop=NinjaFlower(p,0);
            Query("CREATE FUNCTION suppress_flower() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_flower BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_flower()");
            try{Loot(p,drop);}finally{Query("DROP TRIGGER suppress_flower ON "+table+";DROP FUNCTION suppress_flower()");}
            Check(Fenced(p) && !drop.PickedUpAlready && FlowerReceipts(p)==0 && p.Inventory.Count==0,"failed flower pickup rolls back item and receipt: "+table);
            p=ReloadBall(p);Loot(p,drop);Check(!Fenced(p) && drop.PickedUpAlready && FlowerReceipts(p)==1,"reconnect retries the still-owned ground flower: "+table);
        }
        var full=FlowerHunter(job);for(int n=0;n<40;n++)Pack(full,1088001,1);var waiting=NinjaFlower(full,1);Loot(full,waiting);
        Check(!Fenced(full) && !waiting.PickedUpAlready && FlowerReceipts(full)==0,"full bag preserves flower on the ground");DisRemove(full,full.Inventory.Objects[0]);Loot(full,waiting);
        Check(!Fenced(full) && waiting.PickedUpAlready && FlowerReceipts(full)==1,"freed bag space delivers the same flower once");
        var owner=FlowerHunter(job);var stranger=FlowerHunter(job);var owned=NinjaFlower(owner,0);Loot(stranger,owned);
        Check(!owned.PickedUpAlready && FlowerReceipts(stranger)==0,"fresh flower protects the killer's ownership");
        Loot(owner,owned);Parallel.For(0,8,_=>Call(NinjaFlowers,null,"Pickup",owner,owned));Check(FlowerReceipts(owner)==1,"concurrent flower pickup cannot duplicate delivery");
        var auto=NinjaFlower(owner,1);owner.Entity.X=auto.X;owner.Entity.Y=auto.Y;owner.Screen.Add(auto);ItemHandler.PickupItem(new FloorItem(true){UID=auto.UID},owner);
        Check(!Fenced(owner) && auto.PickedUpAlready && FlowerReceipts(owner)==2,"auto-hunt pickup shares atomic flower ownership");
        foreach(string mode in new[]{"short","seal","distance","expired","trade","altered"}) {
            var p=FlowerHunter(job);foreach(var old in p.Map.FloorItems.Values.ToArray())p.Map.RemoveFloorItem(old);var drop=NinjaFlower(p,0);var frame=LootPacket(drop);p.Entity.X=drop.X;p.Entity.Y=drop.Y;
            if(mode=="short")Array.Resize(ref frame,12);if(mode=="seal")frame[frame.Length-1]++;if(mode=="distance")p.Entity.X++;
            if(mode=="expired")drop.OnFloor=Time32.Now.AddSeconds(-Constants.FloorItemSeconds);if(mode=="trade")p.Trade.InTrade=true;if(mode=="altered")drop.Item.ID=1088001;
            FloorItem.Process(frame,p);Check(!drop.PickedUpAlready && FlowerReceipts(p)==0,"invalid flower pickup cannot deliver: "+mode);
        }
        var missed=FlowerHunter(job);var monster=AbyssWild(missed,3600);monster.Hitpoints=0;monster.DeathStamp=Time32.Now;
        int before=missed.Map.FloorItems.Count;ninjaFlowerRoll=23;Call(NinjaFlowers,null,"TryDrop",monster,missed.Entity);ninjaFlowerRoll=0;Call(NinjaFlowers,null,"TryDrop",monster,missed.Entity);
        Check(missed.Map.FloorItems.Count==before,"failed flower roll cannot be rerolled on the same death");
        var odds=FlowerHunter(job);int red=0,white=0;
        for(int roll=0;roll<24;roll++) {
            foreach(var old in odds.Map.FloorItems.Values.ToArray())odds.Map.RemoveFloorItem(old);
            var body=AbyssWild(odds,3600);body.Hitpoints=0;body.DeathStamp=Time32.Now;ninjaFlowerRoll=roll;Call(NinjaFlowers,null,"TryDrop",body,odds.Entity);
            red+=odds.Map.FloorItems.Values.Count(i=>i.ItemID==722732);white+=odds.Map.FloorItems.Values.Count(i=>i.ItemID==722736);odds.Map.RemoveEntity(body);
        }
        Check(red==1 && white==1,"each flower has exactly one of 24 outcomes with 22 empty outcomes");
        foreach(string mode in new[]{"alive","companion","guard","foreign-owner","foreign-instance","distant","class","rebirth"}) {
            var p=FlowerHunter(job);var body=AbyssWild(p,3600);body.Hitpoints=0;ninjaFlowerRoll=0;
            if(mode=="alive")body.Hitpoints=100;if(mode=="companion")body.Companion=true;if(mode=="guard")body.MonsterInfo.Guard=true;
            if(mode=="foreign-owner")body.MonsterInfo.Owner=p.Entity;if(mode=="foreign-instance")p.Map.Entities[body.UID]=new Entity(EntityFlag.Monster,false){UID=body.UID};
            if(mode=="distant")body.X=1;if(mode=="class")p.Entity.Class=45;if(mode=="rebirth")p.Entity.Reborn=1;
            int ground=p.Map.FloorItems.Count;Call(NinjaFlowers,null,"TryDrop",body,p.Entity);
            Check(p.Map.FloorItems.Count==ground,"invalid flower kill cannot create loot: "+mode);p.Map.RemoveEntity(body);
        }
        var cup=FlowerHunter(job);cup.Quests.Accept((QuestID)2414);cup.Quests.IncreaseQuestKills((QuestID)2414,27);cup.Quests.Save();
        var spoils=NinjaFlower(cup,0);Loot(cup,spoils);cup=ReloadBall(cup);cup.Quests.Load();
        Check(cup.Quests.GetQuest((QuestID)2414).Kills==27 && FlowerReceipts(cup)==1,"flower hunting preserves separate Slayer Cup progress across reload");
        var first=FlowerHunter(job);var second=FlowerHunter(job);var released=NinjaFlower(first,1);released.OnFloor=Time32.Now.AddSeconds(-Constants.FloorItemAvailableAfter);
        Task.WaitAll(Task.Run(()=>Loot(first,released)),Task.Run(()=>Loot(second,released)));
        Check(!Fenced(first) && !Fenced(second) && released.PickedUpAlready && FlowerReceipts(first)+FlowerReceipts(second)==1,"two collectors can claim a released flower only once");
        var staleOwner=FlowerHunter(job);var stale=NinjaFlower(staleOwner,0);
        var replacement=new FloorItem(true){UID=stale.UID,MapID=stale.MapID,X=stale.X,Y=stale.Y,ItemID=1088001};staleOwner.Map.FloorItems[stale.UID]=replacement;
        staleOwner.Map.RemoveFloorItem(stale);FloorItem actual;
        Check(staleOwner.Map.FloorItems.TryGetValue(stale.UID,out actual) && ReferenceEquals(actual,replacement),"expiring an old managed flower cannot remove a different floor item with a reused UID");
    }
}
