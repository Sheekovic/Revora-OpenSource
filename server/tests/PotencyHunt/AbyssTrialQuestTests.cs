using System;
using System.Collections;
using System.Linq;
using Npgsql;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type AbyssQuest=typeof(Entity).Assembly.GetType("Revora.Game.Features.AbyssTrialQuest");
    static void AbyssOpen(GameState p,uint npc) {
        p.ActiveNpc=0;var packet=new CMsgNpc{UID=npc,OptionID=0}.ToConquerPacket(2031);
        Writer.WriteUInt64(PacketHandler.ClientSeal,packet.Length-8,packet);PacketHandler.HandlePacket(packet,p);
        if(Fenced(p) || p.ActiveNpc!=npc)throw new Exception("Abyss native NPC-open fixture failed.");
    }
    static object AbyssLive(GameState p) {
        var actors=AbyssWorld.GetField("Actors",Hidden).GetValue(null);
        return ((IEnumerable)actors.GetType().GetProperty("Values").GetValue(actors,null)).Cast<object>().Single(a=>ReferenceEquals(AbyssField(a,"Player"),p));
    }
    static void AbyssKeeper(GameState p,uint npc,byte option) { AbyssAt(p,p.Map,npc);AbyssOpen(p,npc);SoulWire(p,option,npc);if(Fenced(p))throw new Exception("Abyss keeper fixture failed."); }
    static object AbyssNpcSummon(GameState p,uint family) {
        uint npc=family==3632?3605u:family==3633?3606u:family==3634?3607u:family==3635?3609u:family>=3644?3608u
            :new uint[]{3616,3618,3620,3622,3628,3630,3633,3635}[family-3636];
        AbyssKeeper(p,npc,1);SoulWire(p,20,npc);if(Fenced(p))throw new Exception("Abyss summon confirmation failed.");return AbyssLive(p);
    }
    static void AbyssHuntMaterial(GameState p,uint item) {
        int roll=item==722723?0:item==722724?16:item==722725?33:0;
        AbyssWorld.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>roll));
        var body=AbyssWild(p,item==722727?3603u:3600u);body.Die(p.Entity);p.Map.RemoveEntity(body);
        if(Fenced(p))throw new Exception("Abyss hunted material fixture failed.");
    }
    static void AbyssNpcCraft(GameState p,uint output) {
        if(output>=722723 && output<=722725){AbyssHuntMaterial(p,output);return;}
        uint family=output==722722?3632u:output==722726?3633u:output==722729?3634u:output==722731?3635u:0;
        // Craft nested stones first: their recipes consume the same base herbs.
        foreach(object cost in AbyssCost(family==0?"ExchangeCost":"SummonCost",family==0?output:family).Cast<object>().Reverse())
            while(AbyssAmount(p,AbyssType(cost))<AbyssCount(cost))AbyssNpcCraft(p,AbyssType(cost));
        if(family==0){AbyssKeeper(p,3601,output==722721?(byte)1:output==722728?(byte)2:(byte)3);SoulWire(p,20,3601);if(Fenced(p))throw new Exception("Abyss exchange confirmation failed.");}
        else AbyssFight(p,AbyssNpcSummon(p,family));
    }
    static void AbyssNpcEnter(GameState p) { AbyssOpen(p,59558);SoulWire(p,181,59558);SoulWire(p,182,59558); }
    static void VerifyAbyssQuest(string runtime,Map town) {
        VerifyAbyssWorld(runtime,town);var map=Kernel.Maps[1700];
        // Independent installed metadata; no client assets or player rows are copied.
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand("SELECT id,name,type,lookface,cellx,celly FROM public.npcs WHERE mapid=1700 ORDER BY id",c))
        using(var r=q.ExecuteReader())while(r.Read()) {uint id=checked((uint)Convert.ToInt64(r[0]));map.Npcs[id]=new NpcSpawn{UID=id,Name=r.GetString(1),Type=(Enums.NpcType)Convert.ToInt32(r[2]),Mesh=checked((ushort)Convert.ToInt32(r[3])),X=checked((ushort)Convert.ToInt32(r[4])),Y=checked((ushort)Convert.ToInt32(r[5])),MapID=1700};}}
        AbyssQuest.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>abyssNow));Call(AbyssQuest,null,"Initialize");
        var p=SecondPlayer(town);p.Quests=new Quests(p);p.Quests.Load();p.Quests.Accept((QuestID)2414);p.Quests.IncreaseQuestKills((QuestID)2414,27);p.Quests.Save();
        AbyssNpcEnter(p);
        Check(!Fenced(p) && p.Entity.MapID==1700 && p.Account.State==AccountTable.AccountState.Player && AbyssRead(p)!=null
            && p.Quests.GetQuest((QuestID)2414).Kills==27,"ordinary protobuf NPC open and sealed dialogue enter the Abyss while preserving Cup progress");
        AdventureNear(p,3601);AbyssOpen(p,3601);
        for(int n=0;n<3;n++)AbyssNpcCraft(p,722731);
        Check(AbyssAmount(p,722731)==3
            && Scalar("SELECT count(*) FROM revora_abyss_deaths d JOIN revora_abyss_trials r ON r.id=d.run WHERE r.uid=@uid",p.Entity.UID)==351,
            "ordinary NPC material chain earns all 351 base pieces from actual wild death dispatch and crafts three Pure Vigors");
        AbyssKeeper(p,3601,4);SoulWire(p,20,3601);AbyssKeeper(p,3602,1);
        for(int n=0;n<80;n++){var lord=AbyssWild(p,3603);lord.Die(p.Entity);map.RemoveEntity(lord);if(Fenced(p))throw new Exception("Abyss grievance NPC journey failed.");}
        AbyssKeeper(p,3602,2);AbyssKeeper(p,3603,1);
        for(int n=0;n<72;n++)AbyssFight(p,AbyssNpcSummon(p,3636u+(uint)(n%8)));
        AbyssKeeper(p,3603,2);AbyssHuntMaterial(p,722727);
        for(uint family=3644;family<=3646;family++)AbyssFight(p,AbyssNpcSummon(p,family));
        Check(!Fenced(p) && HasAchievement(p,11031) && !HasAchievement(p,10316) && p.Quests.GetQuest((QuestID)2414).Kills==27,
            "complete ordinary NPC and hunted-material journey earns only actual Abyss completion without touching Cup objectives");
        AbyssKeeper(p,3604,1);
        Check(p.Entity.MapID==1002 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,town.Npcs[59558].X,town.Npcs[59558].Y)<=6,
            "Bruce returns the completed hunter beside Sereth through actual server teleport");
        p=ReloadBall(p);SkillTable.LoadProficiencies(p);Call(AbyssQuest,null,"OnLogin",p);p.Entity.MyAchievement.Send();
        Check(HasAchievement(p,11031) && p.Quests.GetQuest((QuestID)2414).Kills==27 && AbyssValue(AbyssField(AbyssRead(p),"Progress"),"Step")==5,
            "NPC journey, achievement and untouched Cup objective survive reconnect");
        VerifyAbyssEarnedRite(p,town,map);
        VerifyAbyssNpcFailures(town,map);VerifyAbyssNpcWalking(town,map);
    }
    static void VerifyAbyssNpcFailures(Map town,Map map) {
        foreach(string mode in new[]{"no-offer","cancel","expired","wrong-echo","far","low-level","bad-body","trade"}) {
            var p=SecondPlayer(town);p.Quests=new Quests(p);p.Quests.Load();AbyssOpen(p,59558);
            if(mode!="no-offer")SoulWire(p,181,59558);
            if(mode=="cancel")SoulWire(p,255,59558);if(mode=="expired")abyssNow=abyssNow.AddMinutes(3);
            if(mode=="far")p.Entity.X=10;if(mode=="low-level")p.Entity.Level=119;if(mode=="trade")p.Trade.InTrade=true;
            SoulWire(p,182,mode=="wrong-echo"?59559u:59558u,mode=="bad-body"?19:20);
            Check(!Fenced(p) && p.Entity.MapID==1002 && AbyssRead(p)==null,"invalid Abyss admission makes no run or teleport: "+mode);
        }
        foreach(string mode in new[]{"no-offer","cancel","expire","changed","duplicate","wrong-echo","bad-body"}) {
            var p=SecondPlayer(town);p.Quests=new Quests(p);p.Quests.Load();AbyssNpcEnter(p);var horns=Pack(p,722722,3);
            AbyssKeeper(p,3601,mode=="no-offer"?(byte)0:(byte)1);
            if(mode=="cancel")SoulWire(p,255,3601);if(mode=="expire")abyssNow=abyssNow.AddMinutes(3);
            if(mode=="changed"){horns.StackSize=4;Query("UPDATE items SET stacksize=4 WHERE uid=@uid","uid",(long)horns.UID);}
            SoulWire(p,20,mode=="wrong-echo"?3602u:3601u,mode=="bad-body"?121:20);
            if(mode=="duplicate")SoulWire(p,20,3601);
            Check(!Fenced(p) && AbyssAmount(p,722721)==(mode=="duplicate"?1:0) && AbyssAmount(p,722722)==(mode=="duplicate"?0:mode=="changed"?4:3),
                "NPC offering is owned, expiring and one-shot: "+mode);
        }
        var resume=SecondPlayer(town);resume.Quests=new Quests(resume);resume.Quests.Load();AbyssNpcEnter(resume);
        foreach(uint type in new uint[]{722723,722724,722725})Pack(resume,type,1);var actor=AbyssNpcSummon(resume,3632);Guid id=AbyssId(AbyssField(actor,"Encounter"));
        resume=ReloadBall(resume);SkillTable.LoadProficiencies(resume);Call(AbyssQuest,null,"OnLogin",resume);Call(AbyssWorld,null,"Tick");AbyssKeeper(resume,3605,2);
        Check(!Fenced(resume) && AbyssId(AbyssField(AbyssLive(resume),"Encounter"))==id
            && new uint[]{722723,722724,722725}.All(type=>AbyssAmount(resume,type)==0),"NPC resumes the original paid challenge after reconnect without another offering");
        AbyssFight(resume,AbyssLive(resume));AbyssKeeper(resume,3604,1);AbyssOpen(resume,59558);SoulWire(resume,181,59558);SoulWire(resume,183,59558);
        Check(!(bool)Call(AbyssJournal,null,"Selected",resume),"ordinary Sereth conversation restores the preserved Cup route");
        var expiring=SecondPlayer(town);expiring.Quests=new Quests(expiring);expiring.Quests.Load();AbyssNpcEnter(expiring);
        foreach(uint type in new uint[]{722723,722724,722725})Pack(expiring,type,1);AbyssKeeper(expiring,3605,1);
        AbyssWorld.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>{abyssNow=abyssNow.AddMinutes(3);return abyssNow;}));
        try {SoulWire(expiring,20,3605);}
        finally {AbyssWorld.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>abyssNow));}
        Check(Fenced(expiring) && AbyssVersion(expiring)==0 && new uint[]{722723,722724,722725}.All(type=>AbyssAmount(expiring,type)==1)
            && Scalar("SELECT count(*) FROM revora_abyss_encounters e JOIN revora_abyss_trials r ON r.id=e.run WHERE r.uid=@uid",expiring.Entity.UID)==0,
            "NPC summon expiring during payment rolls back all ingredients and the encounter");
        expiring=ReloadBall(expiring);SkillTable.LoadProficiencies(expiring);Call(AbyssQuest,null,"OnLogin",expiring);
        Check(!Fenced(expiring) && new uint[]{722723,722724,722725}.All(type=>AbyssAmount(expiring,type)==1),
            "expired payment reconnect recovers the original durable ingredients");
    }
    static void VerifyAbyssNpcWalking(Map town,Map map) {
        var p=SecondPlayer(town);p.Quests=new Quests(p);p.Quests.Load();AbyssNpcEnter(p);
        foreach(uint npc in new uint[]{3601,3602,3603,3605,3606,3607,3609,3616,3618,3620,3622,3628,3630,3633,3635,3608,3604}) {
            AdventureNear(p,npc);AbyssOpen(p,npc);
            Check(!Fenced(p) && Kernel.GetDistance(p.Entity.X,p.Entity.Y,map.Npcs[npc].X,map.Npcs[npc].Y)<=6,"ordinary movement and actual NPC open reach the installed Abyss keeper: "+npc);
        }
        SoulWire(p,1,3604);Check(p.Entity.MapID==1002,"walked Abyss loop returns through Bruce");
    }
}
