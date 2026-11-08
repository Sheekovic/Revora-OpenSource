using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool soulClaimFixture;
    static string SoulClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'base',@base::json,'effects',(SELECT json_agg(a ORDER BY a.uid,a.addingtype) FROM itemadding a JOIN items i ON i.uid=a.uid WHERE i.entityid=@uid))::text",
        "base",NobilityClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void SoulClaimProcess(bool reload){
        const uint uid=4294909601;soulClaimFixture=true;
        bool absent=(bool)Sql("SELECT to_regclass('revora_soul_claim_probe') IS NULL");
        if(absent){Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=140,reborn=2,conquerpoints=50000 WHERE uid=@uid","uid",(long)uid);
            Sql("CREATE TABLE revora_soul_claim_probe(snapshot text);INSERT INTO revora_soul_claim_probe VALUES(NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);p.LoadItemStats();
        Require(p.Account.State==AccountTable.AccountState.Player && !p.Fake && !PirateFenced(p));
        uint[] stages={604,807,908,1008};
        if(Sql("SELECT snapshot FROM revora_soul_claim_probe")==DBNull.Value){Require(!reload);
            uint target=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID/1000==160).OrderByDescending(i=>i.Level).ThenByDescending(i=>i.ID).First().ID;
            uint soul=ConquerItemInformation.BaseInformations.Values.Where(i=>i.PurificationLevel==7 && (bool)Call(Feature("ArtifactApplication"),"Fits",target,i.ID)).OrderBy(i=>i.ID).First().ID;
            // Controlled inputs: qualifying boots and a bound P7 soul. Effects and prizes are earned below.
            if(!p.Inventory.Objects.Any(i=>i.ID==target))foreach(uint type in new[]{target,soul}){
                ushort dur=ConquerItemInformation.BaseInformations[type].Durability;
                Require(p.Inventory.Add(new ConquerItem(true){ID=type,Plus=0,Bound=true,StackSize=1,Durability=dur,MaximDurability=dur},Revora.Game.Enums.ItemUse.CreateAndAdd));
            }
            var boots=p.Inventory.Objects.Single(i=>i.ID==target);var material=p.Inventory.Objects.Single(i=>i.ID==soul);
            if(boots.StackSize==0 && material.StackSize==0){
                Require(!boots.Bound && !material.Bound && (long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid","uid",(long)uid)==0);
                // Repair only the two supplied fixture inputs from the failed legacy Add overload.
                Require((long)Sql("WITH fixed AS(UPDATE items SET stacksize=1,bound=1 WHERE entityid=@uid AND uid=ANY(@ids) AND stacksize=0 AND bound=0 RETURNING uid) SELECT count(*) FROM fixed","uid",(long)uid,"ids",new long[]{boots.UID,material.UID})==2);
                boots.StackSize=material.StackSize=1;boots.Bound=material.Bound=true;
            }
            int needed=ConquerItemInformation.BaseInformations[soul].PurificationMeteorNeed;
            while(p.Inventory.Objects.Where(i=>i.ID==720027).Sum(i=>(int)i.StackSize)*10<needed){
                for(int n=0;n<10;n++)CareerBuy(p,1088001);
                p.Entity.Teleport(1002,400,350);HeartTalk(p,11275,1);
            }
            int before=p.Inventory.Objects.Where(i=>i.ID==720027).Sum(i=>(int)i.StackSize)*10;
            var apply=RetainedArtifactPacket(boots.UID,material.UID);Purification.Process(apply,p);
            Require(!PirateFenced(p) && !p.Inventory.ContainsUID(material.UID) && boots.Purification.PurificationLevel==7 && boots.Purification.PurificationDuration>0);
            int remaining=p.Inventory.Objects.Where(i=>i.ID==720027).Sum(i=>(int)i.StackSize)*10+p.Inventory.Objects.Where(i=>i.ID==1088001).Sum(i=>(int)i.StackSize);
            Require(before-remaining==needed);
            foreach(uint stage in stages)NobilityClaimPacket(p,stage);
            Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid","uid",(long)uid)==0 && !PirateFenced(p));
            RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.EquipItem);Require(p.Equipment.TryGetItem(8).UID==boots.UID);
            var clock=Feature("WayOfHeroesClaims").GetField("Clock",Hidden);object old=clock.GetValue(null);
            try{clock.SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow.AddDays(30)));foreach(uint stage in stages)NobilityClaimPacket(p,stage);}
            finally{clock.SetValue(null,old);}
            Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid","uid",(long)uid)==0 && !PirateFenced(p));
            for(int n=0;n<3;n++){
                uint stage=stages[n];var previous=p.Inventory.Objects.Select(i=>i.UID).ToArray();System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,stage));
                Require(!PirateFenced(p) && (long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage AND NOT legacy","uid",(long)uid,"stage",(int)stage)==1);
                var reward=p.Inventory.Objects.Single(i=>!previous.Contains(i.UID));Require(reward.Bound && reward.Plus==0 && reward.StackSize==1 && reward.ID==(n==0?723694u:n==1?3005127u:3005128u));
                if(n>0)SoulClaimUse(p,reward);
            }
            Require(p.ChiPoints==3000);NobilityClaimPacket(p,1008);
            Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=1008","uid",(long)uid)==0);
            uint small=p.Inventory.Objects.Single(i=>i.ID==723694).UID;int cost=DataHolder.PurifyStabilizationPoints(7);
            int large=(cost-10+99)/100;for(int n=0;n<large;n++)CareerBuy(p,723695);
            var stones=new[]{small}.Concat(p.Inventory.Objects.Where(i=>i.ID==723695).Take(large).Select(i=>i.UID)).ToArray();Require(stones.Length==large+1);
            var stabilize=RetainedArtifactStones(boots.UID,stones);ItemAddingStabilization.Process(stabilize,p);
            Require(!PirateFenced(p) && boots.Purification.PurificationDuration==0 && stones.All(id=>!p.Inventory.ContainsUID(id)));
            RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.UnequipItem);NobilityClaimPacket(p,1008);
            Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=1008","uid",(long)uid)==0);
            RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.EquipItem);System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,1008));
            var last=p.Inventory.Objects.Single(i=>i.ID==3005129);Require(last.Bound && last.Plus==0 && last.StackSize==1);SoulClaimUse(p,last);
            string state=SoulClaimSnapshot(p);Purification.Process(apply,p);ItemAddingStabilization.Process(stabilize,p);Require(state==SoulClaimSnapshot(p));
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_soul_claim_probe SET snapshot=@snapshot","snapshot",SoulClaimSnapshot(p));
        }
        Require(p.ChiPoints==6000 && !PirateFenced(p));var worn=p.Equipment.TryGetItem(8);Require(worn!=null && worn.Purification.PurificationLevel==7 && worn.Purification.PurificationDuration==0);
        string exact=SoulClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_soul_claim_probe")));
        foreach(uint stage in stages)NobilityClaimPacket(p,stage);Require(exact==SoulClaimSnapshot(p) && !PirateFenced(p));
        System.Console.WriteLine("[career-process] career-soul-claims-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Soul claim did not close.");
    }
    static void SoulClaimUse(GameState p,ConquerItem reward){
        var frame=new ItemUsage(true){UID=reward.UID,Action=ItemUsage.Mode.EquipItem}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);
        uint id=reward.UID;ItemUsage.Process(frame,p);ItemUsage.Process(frame,p);Require(!p.Inventory.ContainsUID(id) && !PirateFenced(p));
    }
}
