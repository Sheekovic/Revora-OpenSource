using System;
using System.IO;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool chiClaimFixture;
    static void ChiClaimRequest(GameState p,ushort action,byte power){
        var packet=new Chi(true){UID=p.Entity.UID,Type=action,Mode=(Enums.ChiPowerType)power}.ToArray();
        Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,packet.Length-8,packet);
        Revora.Network.PacketHandler.HandlePacket(packet,p);
    }
    static void ChiClaimDecodeChecks(){
        var service=Feature("WayOfHeroesClaims");
        Func<byte[],int> score=bytes=>(int)Call(service,"ChiScore",bytes);
        Require(score(new byte[0])==0 && score(new byte[]{0})==0);
        byte[] blob=new byte[27];blob[0]=2;blob[1]=1;blob[14]=2;
        for(int i=0;i<4;i++){blob[2+3*i]=1;Revora.Network.Writer.WriteUInt16(200,3+3*i,blob);}
        blob[15]=1;Revora.Network.Writer.WriteUInt16(198,16,blob);Require(score(blob)==499);
        blob[18]=2;Revora.Network.Writer.WriteUInt16(2,19,blob);Require(score(blob)==500);
        Action<byte[]> invalid=bytes=>{bool failed=false;try{score(bytes);}catch(System.Reflection.TargetInvocationException ex){failed=ex.InnerException is InvalidOperationException;}Require(failed);};
        invalid(new byte[]{5});invalid(blob.Take(26).ToArray());invalid(blob.Concat(new byte[]{0}).ToArray());
        var copy=(byte[])blob.Clone();copy[14]=1;invalid(copy);
        copy=(byte[])blob.Clone();copy[15]=14;invalid(copy);
        copy=(byte[])blob.Clone();Revora.Network.Writer.WriteUInt16(201,16,copy);invalid(copy);
        copy=(byte[])blob.Clone();copy[15]=0;invalid(copy);
    }
    static void ChiClaimProcess(bool reload){
        chiClaimFixture=true;ChiClaimDecodeChecks();const uint uid=4294909401;
        bool absent=(bool)Sql("SELECT to_regclass('revora_chi_claim_probe') IS NULL");
        if(absent){Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=110,reborn=1,conquerpoints=2000 WHERE uid=@uid","uid",(long)uid);
            Sql("CREATE TABLE revora_chi_claim_probe(snapshot text)");Sql("INSERT INTO revora_chi_claim_probe VALUES(NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);
        Require(p.Account.State==AccountTable.AccountState.Player && !p.Fake && !PirateFenced(p));
        object snapshot=Sql("SELECT snapshot FROM revora_chi_claim_probe");
        if(snapshot==DBNull.Value){Require(!reload && p.ChiPowers.Count==0 && p.ChiPoints==0);
            NobilityClaimPacket(p,605);Require(p.Inventory.Count==0 && !PirateFenced(p));
            uint cp=p.Entity.ConquerPoints;ChiClaimRequest(p,Chi.BuyStrength,1);
            Require(p.ChiPoints==4000 && p.Entity.ConquerPoints==cp-Config.ChiFullRefillCp);
            var next=typeof(GeneralData).GetField("ChiValueNext",Hidden);var rate=typeof(GeneralData).GetField("ChiValueRate",Hidden);
            object oldNext=next.GetValue(null),oldRate=rate.GetValue(null);
            try{
                next.SetValue(null,(Func<int,int,int>)((lo,hi)=>hi-1));rate.SetValue(null,(Func<int,int,bool>)((a,b)=>true));
                ChiClaimRequest(p,Chi.Unlock,1);ChiClaimRequest(p,Chi.Study,1);
                Require(p.ChiPowers.Count==1 && p.ChiPowers[0].Points==400 && p.ChiPoints==3650);
                // Cached score cannot satisfy a claim whose saved power totals only 400.
                p.ChiPowers[0].Points=1600;NobilityClaimPacket(p,605);p.ChiPowers[0].CalculatePoints();
                Require(p.Inventory.Count==0 && !PirateFenced(p));
                ChiClaimRequest(p,Chi.Unlock,2);NobilityClaimPacket(p,605);
                Require(p.Inventory.Count==0 && p.ChiPoints==3350 && !PirateFenced(p));
                ChiClaimRequest(p,Chi.Study,2);Require(p.ChiPoints==3300 && p.ChiPowers.Sum(i=>i.Points)==800);
            }finally{next.SetValue(null,oldNext);rate.SetValue(null,oldRate);}
            System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,605));
            Require(!PirateFenced(p) && (long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=605 AND NOT legacy","uid",(long)uid)==1);
            var reward=p.Inventory.Objects.Single();Require(reward.ID==3005126 && reward.Bound && reward.Plus==0 && reward.StackSize==1);
            var use=new ItemUsage(true){UID=reward.UID,Action=ItemUsage.Mode.EquipItem}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,use.Length-8,use);
            ItemUsage.Process(use,p);ItemUsage.Process(use,p);Require(!PirateFenced(p) && p.Inventory.Count==0 && p.ChiPoints==3800);
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_chi_claim_probe SET snapshot=@snapshot","snapshot",NobilityClaimSnapshot(p));
        }
        Require(p.ChiPowers.Count==2 && p.ChiPowers.Sum(i=>i.Points)==800 && p.ChiPoints==3800 && !PirateFenced(p));
        string exact=NobilityClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_chi_claim_probe")));
        NobilityClaimPacket(p,605);Require(!PirateFenced(p) && exact==NobilityClaimSnapshot(p));
        System.Console.WriteLine("[career-process] career-chi-claim-"+(reload?"reload":"earn")+"-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Chi claim process did not close.");
    }
}
