using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;

internal static partial class PotencyHuntTests
{
    static readonly Type Moon=typeof(Entity).Assembly.GetType("Revora.Game.Features.MoonBoxQuest");
    static int moonRoute,moonLoot=99;
    static void MoonNear(GameState p,uint id) {p.ActiveNpc=id;var actor=p.Map.Npcs[id];p.Entity.X=actor.X;p.Entity.Y=actor.Y;}
    static void MoonNpc(GameState p,uint id,byte option) {MoonNear(p,id);Npcs.GetDialog(new NpcRequest(true){NpcID=id,OptionID=option},p);}
    static GameState MoonPlayer(Map town) {
        var p=ProgressPlayer(town,"");p.Entity.Level=140;p.Entity.Titles=new ConcurrentDictionary<TitlePacket.Titles,DateTime>();return p;
    }
    static Entity MoonMob(GameState p) {
        var m=(Entity)typeof(CompanionTests).GetMethod("Monster",Hidden).Invoke(null,new object[]{p.Map,p.Entity.X,p.Entity.Y,1u});
        m.MonsterInfo.ID=(uint)(6000+p.Entity.MapID-1043);m.MonsterInfo.Name=m.Name="ToughHorn";m.IsDropped=true;return m;
    }
    static long MoonCount(GameState p,string expression) {return Scalar("SELECT "+expression+" FROM revora_moon_runs WHERE uid=@uid",p.Entity.UID);}
    static void MoonHunt(GameState p,int stage) {
        moonRoute=stage;MoonNpc(p,600003,1);var m=MoonMob(p);m.Die(p.Entity);
        if(!p.Inventory.Objects.Any(i=>i.ID==721010+stage))throw new Exception("Expected immediate fixture token.");
        MoonNpc(p,(uint)(600005+stage*2),1);
    }
    static void MoonToLife(GameState p) {
        moonLoot=0;MoonNpc(p,600050,1);
        for(int i=0;i<6;i++)MoonHunt(p,i);
        MoonNpc(p,600003,1);MoonNpc(p,600017,1);
    }
    static void VerifyMoon(string runtime,Map town) {
        Query("ALTER TABLE entities ADD COLUMN my_title integer DEFAULT 0,ADD COLUMN level integer DEFAULT 140,ADD COLUMN mapid integer DEFAULT 1002,ADD COLUMN x integer DEFAULT 0,ADD COLUMN y integer DEFAULT 0");
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");Query("CREATE TABLE itemadding (LIKE public.itemadding INCLUDING ALL)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);ConquerItem.ItemUID=new Counter(4100000);
        Query("CREATE TABLE npcs (LIKE public.npcs INCLUDING ALL)");
        Query("INSERT INTO npcs SELECT * FROM public.npcs WHERE id IN (600050,600003) OR (id BETWEEN 600005 AND 600017 AND id%2=1) OR id BETWEEN 600018 AND 600042");
        for(ushort id=1042;id<=1050;id++)World(runtime,id);
        Moon.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>max==7?moonRoute:max==25?0:moonLoot));
        Call(Moon,null,"Initialize");Call(Moon,null,"PlaceNpcs");
        Check(town.Npcs.ContainsKey(600050) && Kernel.Maps[1042].Npcs.ContainsKey(600003) && Kernel.Maps[1050].Npcs.Count==25,"all installed Moon Box quest NPCs are restored on actual map floors");
        Check(CtfCount("SELECT count(DISTINCT s.mapid) FROM public.monsterspawns s WHERE s.mapid BETWEEN 1043 AND 1049 AND s.npctype=6000+s.mapid-1043")==7,"all seven hostile tactics have installed matching monster spawns");
        var p=MoonPlayer(town);p.Entity.Level=69;MoonNpc(p,600050,1);
        Check(Scalar("SELECT count(*) FROM revora_moon_runs WHERE uid=@uid",p.Entity.UID)==0,"under-level character cannot begin Moon Box");p.Entity.Level=140;
        p.Fake=true;MoonNpc(p,600050,1);p.Fake=false;p.Entity.X=1;p.Entity.Y=1;
        Npcs.GetDialog(new NpcRequest(true){NpcID=600050,OptionID=1},p);
        Check(p.Entity.MapID==1002 && Scalar("SELECT count(*) FROM revora_moon_runs WHERE uid=@uid",p.Entity.UID)==0,"fake and remote signup requests cannot create a run");
        MoonNear(p,600050);Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=600050,OptionID=1},p));
        Check(p.Entity.MapID==1042 && Scalar("SELECT count(*) FROM revora_moon_runs WHERE uid=@uid",p.Entity.UID)==1,"ordinary concurrent signup enters the portal with one saved run");
        moonRoute=6;MoonNpc(p,600003,1);
        Check(p.Entity.MapID==1049,"random entry can reach Death Tactic");
        var bought=Enumerable.Range(0,6).Select(i=>Pack(p,721010u+(uint)i,1)).ToArray();MoonNpc(p,600017,1);
        Check(p.Entity.MapID==1049 && p.Inventory.Count==6 && MoonCount(p,"jade_uid")==0,"six unearned tokens cannot substitute for this run's earned materials");
        foreach(var item in bought){Query("DELETE FROM items WHERE uid=@uid","uid",(long)item.UID);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}
        p.Entity.Teleport(1002,408,330);Check(p.Entity.MapID==1049,"external teleport cannot bypass the Death Tactic");
        MoonNpc(p,600017,3);Check(p.Entity.MapID==1049,"surrender preview does not abandon the run");MoonNpc(p,600017,4);
        Check(p.Entity.MapID==1002 && Scalar("SELECT count(*) FROM revora_moon_runs WHERE uid=@uid AND phase='abandoned'",p.Entity.UID)==1 && !HasAchievement(p,11022),"surrender returns the player without a fabricated reward or achievement");
        MoonNpc(p,600050,1);moonRoute=0;MoonNpc(p,600003,1);var first=MoonMob(p);
        for(int i=0;i<19;i++){first.Hitpoints=first.MaxHitpoints;first.Die(p.Entity);}
        Check(p.Inventory.Count==0 && MoonCount(p,"kills[1]")==19,"failed rolls build saved progress toward the twenty-kill guarantee");
        Query("ALTER TABLE revora_moon_runs ADD CONSTRAINT moon_token_failure CHECK(tokens[1]=0) NOT VALID");first.Hitpoints=first.MaxHitpoints;first.Die(p.Entity);
        Check(Fenced(p) && p.Inventory.Count==0 && MoonCount(p,"kills[1]")==19 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==0,"failed token transaction preserves both counter and inventory");
        Query("ALTER TABLE revora_moon_runs DROP CONSTRAINT moon_token_failure");DemonReset(p);first.Hitpoints=first.MaxHitpoints;first.Die(p.Entity);
        Check(p.Inventory.Count==1 && p.Inventory.Objects[0].ID==721010 && p.Inventory.Objects[0].Bound,"twentieth committed kill supplies the guaranteed bound tactic token");
        Parallel.For(0,12,i=>first.Die(p.Entity));Check(p.Inventory.Count==1,"repeated corpse notifications cannot issue another token");
        var token=p.Inventory.Objects[0];ItemHandler.DropItem(new ItemUsage(true){UID=token.UID},p);
        var storage=new Revora.Game.ConquerStructures.Warehouse(p,Revora.Game.ConquerStructures.Warehouse.WarehouseID.Market);
        Check(p.Inventory.Count==1 && !storage.Add(token) && token.Warehouse==0,"tactic tokens cannot be dropped or stored");
        MoonNear(p,600005);ItemHandler.HandleSellToNPC(new ItemUsage(true){UID=600005,dwParam=token.UID},p);
        Check(p.Inventory.Count==1,"tactic token cannot be sold to an NPC");
        p.Inventory.Remove(token.UID,Enums.ItemUse.None,false);ConquerItemTable.LoadItems(p);Call(Moon,null,"Initialize");Call(Moon,null,"OnLogin",p);
        Check(p.Entity.MapID==1043 && p.Inventory.Objects.Any(i=>i.UID==token.UID && i.Bound),"ordinary reconnect loading restores the token and saved tactic");
        var opponent=MoonPlayer(town);opponent.BindWorldMap(1043);opponent.Entity.X=p.Entity.X;opponent.Entity.Y=p.Entity.Y;
        p.Entity.Die(opponent.Entity);Call(Moon,null,"Tick");
        Check(p.Entity.MapID==1042 && !p.Entity.Dead && p.Inventory.Objects.Any(i=>i.UID==token.UID),"actual PvP death returns through the owned queue and preserves the bound token");
        moonLoot=0;for(int i=1;i<6;i++)MoonHunt(p,i);
        Check(p.Inventory.Objects.Count(i=>i.ID>=721010 && i.ID<=721015)==6 && MoonCount(p,"(SELECT count(*) FROM unnest(tokens) id WHERE id<>0)")==6,"six distinct tactics supply six tracked token identities");
        MoonNpc(p,600003,1);Check(p.Entity.MapID==1049,"complete token set leads to the Death exchange");
        Query("ALTER TABLE revora_moon_runs ADD CONSTRAINT moon_jade_failure CHECK(jade_uid=0) NOT VALID");MoonNpc(p,600017,1);
        Check(Fenced(p) && p.Inventory.Count==6 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==6,"failed Soul Jade exchange consumes none of the six tokens");
        Query("ALTER TABLE revora_moon_runs DROP CONSTRAINT moon_jade_failure");DemonReset(p);MoonNpc(p,600017,1);
        Check(p.Entity.MapID==1050 && p.Inventory.Count==1 && p.Inventory.Objects[0].ID==721072,"six tokens exchange atomically for the Soul Jade and Life entry");
        MoonNpc(p,600019,1);Check(p.Inventory.Count==1 && !HasAchievement(p,11022),"a different wandering ghost cannot finalize the quest");
        Query("ALTER TABLE revora_moon_results ADD CONSTRAINT moon_reward_failure CHECK(false) NOT VALID");MoonNpc(p,600018,1);
        Check(Fenced(p) && p.Inventory.Count==1 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id BETWEEN 800000 AND 899999",p.Entity.UID)==0 && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11022",p.Entity.UID)==0,"failed final reward rolls back Soul Jade consumption and achievement");
        Query("ALTER TABLE revora_moon_results DROP CONSTRAINT moon_reward_failure");DemonReset(p);MoonNear(p,600018);
        Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=600018,OptionID=1},p));
        Check(p.Entity.MapID==1002 && p.Inventory.Count==2 && p.Inventory.Contains(721090,1)
            && Scalar("SELECT count(*) FROM revora_moon_results WHERE uid=@uid",p.Entity.UID)==1,"concurrent terminal requests deliver one Moon Box and return to town");
        Check(HasAchievement(p,11022),"ordinary terminal quest completion grants achievement 11022 immediately");
        EvidenceRefresh(p);foreach(var item in p.Inventory.Objects.ToArray())p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);ConquerItemTable.LoadItems(p);
        Check(p.Inventory.Count==2 && p.Inventory.Contains(721090,1) && HasAchievement(p,11022),"Moon Box and completion evidence survive ordinary reload");
        VerifyIslandSoul(p,"revora_moon_results");
        var meteor=MoonPlayer(town);MoonToLife(meteor);var fillers=Enumerable.Range(0,39).Select(i=>Pack(meteor,1088001,1)).ToArray();MoonNpc(meteor,600018,2);
        Check(meteor.Entity.MapID==1050 && meteor.Inventory.Count==40 && !HasAchievement(meteor,11022),"full inventory leaves the three-Meteor choice claimable");
        foreach(var item in fillers.Take(3)){Query("DELETE FROM items WHERE uid=@uid","uid",(long)item.UID);meteor.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}
        MoonNpc(meteor,600018,2);Check(meteor.Entity.MapID==1002 && meteor.Inventory.Count==40 && !HasAchievement(meteor,11022),"three-Meteor ending completes the run without the Moon Box achievement");
        VerifyIslandSoul(meteor,"revora_moon_results");
        var old=MoonPlayer(town);MoonToLife(old);
        Query("UPDATE revora_moon_runs SET valuable_reward=false WHERE uid=@uid","uid",(long)old.Entity.UID);
        Call(Moon,null,"Initialize");MoonNpc(old,600018,1);
        Check(!Fenced(old) && old.Inventory.Count==1 && old.Inventory.Contains(721090,1),"old Moon contract retains original reward after initialization");
        var refused=MoonPlayer(town);MoonToLife(refused);MoonNpc(refused,600018,3);
        Check(refused.Entity.MapID==1002 && refused.Inventory.Count==0 && !HasAchievement(refused,11022),"refusing the lonely spirit consumes the jade without rewards or achievement");
    }
}
