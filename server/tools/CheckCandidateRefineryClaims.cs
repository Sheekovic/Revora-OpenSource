using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool refineryClaimFixture;
    static ConquerItem RefineryClaimGive(GameState p,uint type){
        ushort durability=ConquerItemInformation.BaseInformations[type].Durability;
        var item=new ConquerItem(true){ID=type,Bound=true,StackSize=1,Durability=durability,MaximDurability=durability};
        Require(p.Inventory.Add(item,Revora.Game.Enums.ItemUse.CreateAndAdd));return item;
    }
    static void RefineryClaimApply(GameState p,ConquerItem target,uint type){
        var material=RefineryClaimGive(p,type);var frame=RetainedArtifactPacket(target.UID,material.UID);frame[4]=0;
        Purification.Process(frame,p);Require(!PirateFenced(p) && !p.Inventory.ContainsUID(material.UID) && target.RefineItem==type);
    }
    static void RefineryClaimProcess(bool reload){
        const uint uid=4294909701;refineryClaimFixture=true;
        if((bool)Sql("SELECT to_regclass('revora_refinery_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=140,reborn=2,conquerpoints=50000 WHERE uid=@uid","uid",(long)uid);
            Sql("CREATE TABLE revora_refinery_claim_probe(snapshot text);INSERT INTO revora_refinery_claim_probe VALUES(NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);p.LoadItemStats();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if(Sql("SELECT snapshot FROM revora_refinery_claim_probe")==DBNull.Value){
            Require(!reload && p.Inventory.Count==0);
            var boots=RefineryClaimGive(p,160249);var service=Feature("RefineryProgress");
            uint low=Kernel.DatabaseRefinery.Values.Where(i=>i.Level==1 && (bool)Call(service,"Fits",boots,i)).OrderBy(i=>i.Identifier).First().Identifier;
            uint high=Kernel.DatabaseRefinery.Values.Where(i=>i.Level==5 && (bool)Call(service,"Fits",boots,i)).OrderBy(i=>i.Identifier).First().Identifier;
            RefineryClaimApply(p,boots,low);NobilityClaimPacket(p,808);
            Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid","uid",(long)uid)==0);
            RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.EquipItem);Require(p.Equipment.TryGetItem(8).UID==boots.UID);
            var clock=Feature("WayOfHeroesClaims").GetField("Clock",Hidden);object old=clock.GetValue(null);
            try{clock.SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow.AddDays(30)));NobilityClaimPacket(p,808);}
            finally{clock.SetValue(null,old);}
            Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid","uid",(long)uid)==0);
            System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,808));
            var reward=p.Inventory.Objects.Single(i=>i.ID==3005127);Require(reward.Bound && reward.StackSize==1 && reward.Plus==0);SoulClaimUse(p,reward);
            NobilityClaimPacket(p,909);Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=909","uid",(long)uid)==0 && p.ChiPoints==1000);
            RefineryClaimApply(p,boots,high);int points=(int)Call(service,"Cost",5u);int count=(points+99)/100;
            for(int n=0;n<count;n++)CareerBuy(p,723695);
            var stones=p.Inventory.Objects.Where(i=>i.ID==723695).Select(i=>i.UID).ToArray();Require(stones.Length==count);
            var stable=RetainedArtifactStones(boots.UID,stones);stable[4]=0;ItemAddingStabilization.Process(stable,p);
            Require(!PirateFenced(p) && stones.All(id=>!p.Inventory.ContainsUID(id)) && (long)Sql("SELECT count(*) FROM itemadding WHERE uid=@uid AND addingtype=1 AND duration=0 AND addinglevel=5","uid",(long)boots.UID)==1);
            // An active lower temporary overlay must mask the higher permanent effect.
            RefineryClaimApply(p,boots,low);NobilityClaimPacket(p,909);
            Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=909","uid",(long)uid)==0 && !PirateFenced(p));
            try{clock.SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow.AddDays(30)));System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,909));}
            finally{clock.SetValue(null,old);}
            reward=p.Inventory.Objects.Single(i=>i.ID==3005128);Require(reward.Bound && reward.StackSize==1 && reward.Plus==0);SoulClaimUse(p,reward);
            string before=SoulClaimSnapshot(p);ItemAddingStabilization.Process(stable,p);Require(before==SoulClaimSnapshot(p));
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_refinery_claim_probe SET snapshot=@snapshot","snapshot",SoulClaimSnapshot(p));
        }
        Require(!PirateFenced(p) && p.ChiPoints==3000 && (long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid IN(808,909) AND NOT legacy","uid",(long)uid)==2);
        string exact=SoulClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_refinery_claim_probe")));
        NobilityClaimPacket(p,808);NobilityClaimPacket(p,909);Require(exact==SoulClaimSnapshot(p) && !PirateFenced(p));
        System.Console.WriteLine("[career-process] career-refinery-claims-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Refinery claims did not close.");
    }
}
