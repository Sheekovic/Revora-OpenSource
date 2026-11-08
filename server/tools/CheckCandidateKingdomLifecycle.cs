using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.ConquerStructures.Society;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle {
    static long kingdomTicks;
    static bool kingdomFixture;
    static DateTime KingdomNow(){return new DateTime(Interlocked.Read(ref kingdomTicks),DateTimeKind.Utc);}
    static void KingdomTime(int hour,int minute=0,int second=0,int day=12){Interlocked.Exchange(ref kingdomTicks,TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026,10,day,hour,minute,second,DateTimeKind.Unspecified),TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time")).Ticks);}
    static void PrepareKingdom(string mode) {
        Require(Equals(Sql("SELECT shobj_description(oid,'pg_database') FROM pg_database WHERE datname=current_database()"),"revora-owned-kingdom-host-v1"));
        kingdomFixture=true;
        KingdomTime(mode.EndsWith("reload")?18:17,mode.EndsWith("reload")?1:0);
        Feature("KingdomCompetitionField").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)KingdomNow);
        GameAssembly.GetType("Revora.TitleRequirements",true).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)KingdomNow);
        if(mode=="kingdom-interrupt" || mode=="kingdom-recover")KingdomTime(17,0,0,13);
        Environment.SetEnvironmentVariable("REVORA_KINGDOM_COMPETITION_ENABLED",mode=="kingdom-recover"?"false":"true");
    }
    static void KingdomEquip(GameState p) {
        new TitleStorage().Login(p);
        foreach(int key in new[]{11000,40011000})new TitleStorage{Info=new TitleStorageProto{Type=key/10000,Id=key%10000,ActionId=TitleStorage.Action.Equip,Value=new TitleValue{Type=key/10000,Id=key%10000}}}.Handle(p);
        Require(p.Entity.EquippedTitle==11000 && p.Entity.EquippedWing==40011000);
    }
    static void KingdomProcess(string mode) {
        if(mode=="kingdom-interrupt"){KingdomInterrupt();return;}
        if(mode=="kingdom-recover"){KingdomRecover();return;}
        bool reload=mode.EndsWith("reload");
        const uint uid=4294906001;nextUid=uid;
        if(!reload) {
            Require((long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);
            Require((long)Sql("SELECT count(*) FROM guilds WHERE id=50003")==0);
            Require(!new AccountTable("KingdomProbe").exists);
            Player(Kernel.Maps[1002]);nextUid=uid;var p=Player(Kernel.Maps[1002],true);
            var guild=new Guild(p.Entity.Name){ID=50003,Name="CrownProbeGuild"};p.Guild=guild;p.Entity.GuildID=checked((ushort)guild.ID);
            p.AsMember=new Guild.Member(guild.ID){ID=uid,Name=p.Entity.Name,Level=p.Entity.Level,Class=p.Entity.Class,Rank=Enums.GuildMemberRank.GuildLeader};
            p.Entity.GuildRank=(ushort)p.AsMember.Rank;guild.Members[uid]=p.AsMember;guild.Leader=p.AsMember;guild.LeaderName=p.Entity.Name;
            Require(EntityTable.SaveEntity(p));GuildTable.Create(guild);Kernel.Guilds[guild.ID]=guild;
            var name=System.Text.Encoding.Default.GetBytes("CrownProbe");var bytes=new byte[28+name.Length];Revora.Network.Writer.WriteUInt16((ushort)(bytes.Length-8),0,bytes);Revora.Network.Writer.WriteUInt16((ushort)PacketID.MsgLeagueOpt,2,bytes);Revora.Network.Writer.WriteUInt16(7,4,bytes);bytes[18]=1;bytes[19]=(byte)name.Length;Array.Copy(name,0,bytes,20,name.Length);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,bytes.Length-8,bytes);
            Revora.Network.GamePackets.Union.Union.Prosses(bytes,p);Require(p.UnionID!=0 && p.Offical==1);
            p.Entity.PKMode=Enums.PKMode.Peace;p.Entity.Teleport(1002,316,325);HeartTalk(p,903980,1);Require(p.Entity.MapID==3998);
            p.Entity.X=502;p.Entity.Y=300;HeartTalk(p,903982,1);KingdomTime(17,0,5);Call(Feature("KingdomCompetitionField"),"Tick");
            p.Entity.X=302;p.Entity.Y=153;HeartTalk(p,903981,1);
            Require((long)Sql("SELECT count(*) FROM revora_kingdom_deliveries WHERE uid=@uid","uid",(long)uid)==1);
            KingdomTime(18);Call(Feature("KingdomCompetitionField"),"Tick");KingdomEquip(p);
            Require(p.Union.IsKingdom() && p.Entity.MapID==1002 && p.Entity.X==316 && p.Entity.Y==325 && EntityTable.SaveEntity(p));
            Sql("CREATE TABLE revora_kingdom_host_checkpoint(uid bigint,unionid bigint,process integer,started timestamptz,contests text);INSERT INTO revora_kingdom_host_checkpoint SELECT @uid,@union,@pid,pg_postmaster_start_time(),(SELECT jsonb_agg(to_jsonb(c) ORDER BY day)::text FROM revora_kingdom_contests c)","uid",(long)uid,"union",(long)p.UnionID,"pid",Process.GetCurrentProcess().Id);
            Kernel.GamePool.Remove(uid);
            System.Console.WriteLine("[kingdom-process] ordinary quarry earning and equipped crown saved");
        } else {
            Require((bool)Sql("SELECT process<>@pid AND started<>pg_postmaster_start_time() FROM revora_kingdom_host_checkpoint","pid",Process.GetCurrentProcess().Id));
            Require((bool)Sql("SELECT contests=(SELECT jsonb_agg(to_jsonb(c) ORDER BY day)::text FROM revora_kingdom_contests c) FROM revora_kingdom_host_checkpoint"));
            Require(Kernel.ServerKingdom==Convert.ToUInt32(Sql("SELECT unionid FROM revora_kingdom_host_checkpoint")));
        }
        KingdomWire(reload,false);
        Require((bool)Sql("SELECT contests=(SELECT jsonb_agg(to_jsonb(c) ORDER BY day)::text FROM revora_kingdom_contests c) FROM revora_kingdom_host_checkpoint"));
        System.Console.WriteLine("[kingdom-process] kingdom-"+(reload?"reload":"capture")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Kingdom fixture did not close.");
    }
    static void KingdomWire(bool reload,bool recovery) {
        const uint uid=4294906001;
        string password=Guid.NewGuid().ToString("N");var account=new AccountTable("KingdomProbe");
        if(reload)Require(account.exists && account.EntityID==uid && account.State==AccountTable.AccountState.Player);
        account.Password=password;account.EntityID=uid;account.State=AccountTable.AccountState.Player;if(!reload)account.Insert();account.Save();
        var start=new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CandidateKingdomWireProbe.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Environment.CurrentDirectory};
        start.EnvironmentVariables["REVORA_CANDIDATE_ACCOUNT_PASSWORD"]=password;start.EnvironmentVariables.Remove("REVORA_DATABASE_PASSWORD");
        if(recovery)start.EnvironmentVariables["REVORA_KINGDOM_WIRE_RECOVERY"]="1";
        using(var child=Process.Start(start)) {
            start.EnvironmentVariables.Remove("REVORA_CANDIDATE_ACCOUNT_PASSWORD");password=null;
            var errors=child.StandardError.ReadToEndAsync();
            try {
                if(recovery) {
                    TeamWireRead(child,"[kingdom-wire] recovery-ready");GameState p;
                    Require(Kernel.GamePool.TryGetValue(uid,out p) && !p.Fake && p.Account.State==AccountTable.AccountState.Player
                        && p.Entity.MapID==1002 && p.Entity.X==316 && p.Entity.Y==325 && p.Entity.PKMode==Enums.PKMode.Peace);
                    Require((long)Sql("SELECT count(*) FROM revora_kingdom_entries WHERE uid=@uid AND (NOT returned OR carried OR nonce IS NOT NULL)","uid",(long)uid)==0);
                    child.StandardInput.WriteLine("stop");child.StandardInput.Flush();TeamWireRead(child,"[kingdom-wire] passed");
                    Require(child.WaitForExit(5000) && child.ExitCode==0);
                    System.Console.WriteLine("[kingdom-process] authenticated interrupted admission returned with prior PK mode and no cargo");
                } else {
                    var output=child.StandardOutput.ReadToEndAsync();
                    if(!child.WaitForExit(20000))throw new InvalidOperationException("Kingdom wire timeout.");
                    string result=output.Result;System.Console.WriteLine(result);
                    Require(child.ExitCode==0 && result.Contains("[kingdom-wire] passed"));
                }
            } finally {if(!child.HasExited){child.Kill();child.WaitForExit();}System.Console.Error.WriteLine(errors.Result);}
        }
    }
    static void KingdomInterrupt() {
        const uint uid=4294906001;nextUid=uid;var p=Player(Kernel.Maps[1002],true);
        Require(p.UnionID!=0 && p.Account.State==AccountTable.AccountState.Player);
        p.Entity.PKMode=Enums.PKMode.Peace;p.Entity.Teleport(1002,316,325);HeartTalk(p,903980,1);Require(p.Entity.MapID==3998);
        p.Entity.X=502;p.Entity.Y=300;HeartTalk(p,903982,1);KingdomTime(17,0,5,13);Call(Feature("KingdomCompetitionField"),"Tick");
        Require((long)Sql("SELECT count(*) FROM revora_kingdom_entries WHERE uid=@uid AND NOT returned AND carried","uid",(long)uid)==1 && EntityTable.SaveEntity(p));
        Sql("CREATE TABLE revora_kingdom_interrupt_checkpoint(process integer,started timestamptz,deliveries bigint);INSERT INTO revora_kingdom_interrupt_checkpoint SELECT @pid,pg_postmaster_start_time(),(SELECT count(*) FROM revora_kingdom_deliveries)","pid",Process.GetCurrentProcess().Id);
        Kernel.GamePool.Remove(uid);System.Console.WriteLine("[kingdom-process] kingdom-interrupt-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Kingdom fixture did not close.");
    }
    static void KingdomRecover() {
        Require(!(bool)Feature("KingdomCompetitionField").GetProperty("Enabled",Hidden).GetValue(null,null));
        Require((bool)Sql("SELECT process<>@pid AND started<>pg_postmaster_start_time() FROM revora_kingdom_interrupt_checkpoint","pid",Process.GetCurrentProcess().Id));
        Require((long)Sql("SELECT count(*) FROM revora_kingdom_entries WHERE uid=4294906001 AND NOT returned AND carried")==1);
        KingdomWire(true,true);
        Require((bool)Sql("SELECT deliveries=(SELECT count(*) FROM revora_kingdom_deliveries) FROM revora_kingdom_interrupt_checkpoint"));
        Require((long)Sql("SELECT count(*) FROM entities WHERE uid=4294906001 AND mapid=1002 AND x=316 AND y=325")==1);
        System.Console.WriteLine("[kingdom-process] kingdom-recover-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Kingdom fixture did not close.");
    }

}
