using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Game;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle
{
    static bool ctfClaimFixture;
    static string CtfClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('base',@snapshot::json,
'members',(SELECT json_agg(m ORDER BY run) FROM revora_ctf_members m WHERE uid=@uid),
'returns',(SELECT row_to_json(r) FROM revora_ctf_returns r WHERE uid=@uid),
'deliveries',(SELECT json_agg(d ORDER BY run,token) FROM revora_ctf_deliveries d WHERE uid=@uid))::text","snapshot",PromotionClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void CtfClaimProcess(bool reload){
        ctfClaimFixture=true;const uint uid=4294912301;
        if((bool)Sql("SELECT to_regclass('revora_ctf_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=130,reborn=2,conquerpoints=2000000 WHERE uid=@uid","uid",(long)uid);
            Sql("CREATE TABLE revora_ctf_claim_probe(snapshot text);INSERT INTO revora_ctf_claim_probe VALUES(NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);p.LoadItemStats();Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if(Sql("SELECT snapshot FROM revora_ctf_claim_probe")==DBNull.Value){
            Require(!reload && p.Inventory.Count==0);
            long previousEntries=(long)Sql("SELECT count(*) FROM revora_ctf_members WHERE uid=@uid AND entered_at IS NOT NULL","uid",(long)uid);
            Call(Feature("CtfResults"),"Return",p,true);
            if(previousEntries==0){string before=CtfClaimSnapshot(p);NobilityClaimPacket(p,609);Require(before==CtfClaimSnapshot(p));}
            if(p.Guild==null){
                HeartTalk(p,10003,9);var name=System.Text.Encoding.ASCII.GetBytes("CtfProbe9300");var frame=new byte[28+name.Length];
                Revora.Network.Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Revora.Network.Writer.WriteUInt16(2032,2,frame);Revora.Network.Writer.WriteUInt32(10003,8,frame);frame[14]=2;frame[17]=(byte)name.Length;name.CopyTo(frame,18);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);
                Revora.Network.PacketHandler.HandlePacket(frame,p);Require(p.Guild!=null && p.Guild.Members.ContainsKey(uid));
            }
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;var old=Server.Thread.CTF;var gate=typeof(CaptureTheFlag).GetField("sync",flags).GetValue(old);lock(gate)typeof(CaptureTheFlag).GetField("nextTick",flags).SetValue(old,DateTime.MaxValue);
            DateTime now=new DateTime(2035,1,7,4,59,0,DateTimeKind.Utc).AddDays(7*previousEntries);var ctf=(CaptureTheFlag)Activator.CreateInstance(typeof(CaptureTheFlag),flags,null,new object[]{Kernel.Maps[2057],(Func<DateTime>)(()=>now)},null);Server.Thread.CTF=ctf;
            ctf.Tick();HeartTalk(p,380,11);Require(p.Entity.MapID==1002);if(previousEntries==0){string closed=CtfClaimSnapshot(p);NobilityClaimPacket(p,609);Require(closed==CtfClaimSnapshot(p));}
            now=new DateTime(2035,1,7,5,0,1,DateTimeKind.Utc).AddDays(7*previousEntries);ctf.Tick();Require(CaptureTheFlag.IsWar);
            string run=(string)typeof(CaptureTheFlag).GetField("run",flags).GetValue(ctf);
            // Negative fixture: saved registration alone, without arrival, must not qualify.
            Require((bool)Call(Feature("CtfResults"),"Register",run,uid,p.Guild.ID,now));if(previousEntries==0){string registered=CtfClaimSnapshot(p);NobilityClaimPacket(p,609);Require(registered==CtfClaimSnapshot(p));}
            HeartTalk(p,380,11);Require(p.Entity.MapID==2057 && !PirateFenced(p));
            Require((bool)Sql("SELECT entered_at IS NOT NULL FROM revora_ctf_members WHERE run=@run AND uid=@uid","run",run,"uid",(long)uid));
            var stamp=Sql("SELECT entered_at FROM revora_ctf_members WHERE run=@run AND uid=@uid","run",run,"uid",(long)uid);
            HeartTalk(p,900000060,1);Require(p.Entity.MapID==1002);now=now.AddSeconds(3);ctf.Tick();HeartTalk(p,380,11);Require(p.Entity.MapID==2057);
            Require(Equals(stamp,Sql("SELECT entered_at FROM revora_ctf_members WHERE run=@run AND uid=@uid","run",run,"uid",(long)uid)));
            HeartTalk(p,900000060,1);Require(p.Entity.MapID==1002);
            System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,609));var ball=p.Inventory.Objects.Single();Require(ball.ID==723700 && ball.Bound);SoulClaimUse(p,ball);
            now=new DateTime(2035,1,7,5,30,0,DateTimeKind.Utc).AddDays(7*previousEntries);ctf.Tick();Require(!CaptureTheFlag.IsWar);
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_ctf_claim_probe SET snapshot=@snapshot","snapshot",CtfClaimSnapshot(p));
        }
        Require(p.Inventory.Count==0 && p.Entity.MapID==1002 && !PirateFenced(p));
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=609 AND NOT legacy","uid",(long)uid)==1);
        string exact=CtfClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_ctf_claim_probe")));NobilityClaimPacket(p,609);Require(exact==CtfClaimSnapshot(p));
        System.Console.WriteLine("[career-process] career-ctf-claim-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("CTF claim did not close.");
    }
}
