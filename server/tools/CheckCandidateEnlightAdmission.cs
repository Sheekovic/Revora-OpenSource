using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle
{
    static bool enlightAdmissionFixture;
    static byte[] EnlightFrame(GameState sender,GameState target){var b=new Enlight(true){Enlighter=sender.Entity.UID,Enlighted=target.Entity.UID}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,32,b);return b;}
    static string EnlightAdmissionState(GameState p){return p.Entity.Experience+"/"+p.Entity.EnlightenPoints+"/"+p.Entity.ReceivedEnlightenPoints+"/"+p.Entity.EnlightmentTime+"/"+NobilityClaimSnapshot(p);}
    static void EnlightAdmissionProcess(){
        enlightAdmissionFixture=true;const uint uid=4294912801;
        if((bool)Sql("SELECT to_regclass('revora_enlight_admission_probe') IS NULL")){
            Require((long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @first AND @last","first",(long)uid,"last",(long)uid+4)==0);
            byte[] levels={130,110,111,89,60};nextUid=uid;
            for(int n=0;n<5;n++){var p=Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=@level,reborn=0,enlightenpoints=100,enlightsreceived=0,enlightmentwait=0 WHERE uid=@uid","level",(int)levels[n],"uid",(long)p.Entity.UID);}
            Sql("CREATE TABLE revora_enlight_admission_probe(checked_at timestamptz)");
        }
        nextUid=uid;var players=Enumerable.Range(0,5).Select(_=>Player(Kernel.Maps[1002],true)).ToArray();
        foreach(var p in players){p.LoadItemStats();WayofHeroes.Load(p);Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));p.Entity.X=330;p.Entity.Y=300;}
        var sender=players[0];var target=players[1];var type=typeof(Enlight);
        Require((bool)Call(type,"Eligible",sender,target));Require(!(bool)Call(type,"Eligible",sender,players[2]));Require(!(bool)Call(type,"Eligible",players[3],players[4]));Require(!(bool)Call(type,"Eligible",sender,sender));
        string[] before=players.Select(EnlightAdmissionState).ToArray();byte[] frame=EnlightFrame(sender,target);
        Enlight.Process(null,sender);Enlight.Process(frame,null);for(int length=0;length<40;length++)Enlight.Process(frame.Take(length).ToArray(),sender);
        Enlight.Process(frame.Concat(new byte[]{0}).ToArray(),sender);
        foreach(int index in new[]{0,2,12,32}){var malformed=(byte[])frame.Clone();malformed[index]^=1;Enlight.Process(malformed,sender);}
        Enlight.Process(EnlightFrame(sender,sender),sender);Enlight.Process(EnlightFrame(sender,players[2]),sender);Enlight.Process(EnlightFrame(players[3],players[4]),players[3]);
        target.Entity.X=349;Require(!(bool)Call(type,"Eligible",sender,target));Enlight.Process(frame,sender);target.Entity.X=348;Require((bool)Call(type,"Eligible",sender,target));target.Entity.X=330;
        ushort map=target.Entity.MapID;try{target.Entity.MapID=1036;Require(!(bool)Call(type,"Eligible",sender,target));Enlight.Process(frame,sender);}finally{target.Entity.MapID=map;}
        try{target.Fake=true;Require(!(bool)Call(type,"Eligible",sender,target));Enlight.Process(frame,sender);}finally{target.Fake=false;}
        using(var held=new ManualResetEventSlim(false))using(var release=new ManualResetEventSlim(false)){
            var busy=Task.Run(()=>{lock(target.ItemSyncRoot){held.Set();Require(release.Wait(5000));}});
            try{Require(held.Wait(5000));Enlight.Process(frame,sender);}finally{release.Set();busy.GetAwaiter().GetResult();}
        }
        Require(players.Select(EnlightAdmissionState).SequenceEqual(before));
        NobilityClaimPacket(sender,606);Require(before[0]==EnlightAdmissionState(sender));
        Sql("INSERT INTO revora_enlight_admission_probe(checked_at) VALUES(@now)","now",DateTime.UtcNow);
        System.Console.WriteLine("[career-process] career-enlight-admission-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Enlight admission did not close.");
    }
}
