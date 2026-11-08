using System;
using System.Diagnostics;
using System.IO;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.ConquerStructures.Society;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle {
    static bool supervisorFixture;
    static void PrepareSupervisor() {
        Require(Equals(Sql("SELECT shobj_description(oid,'pg_database') FROM pg_database WHERE datname=current_database()"),"revora-owned-kingdom-host-v1"));
        supervisorFixture=true;Environment.SetEnvironmentVariable("REVORA_KINGDOM_COMPETITION_ENABLED","false");
    }
    static void SupervisorProcess(bool reload,bool wire=false) {
        var guild=Kernel.Guilds[50003];nextUid=4294906002u;
        if(!reload) {
            Require((long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN 4294906002 AND 4294906004")==0);
            Require((long)Sql("SELECT count(*) FROM pt_arsenal WHERE syn_id=50003")==0);
            for(int slot=1;slot<=3;slot++) {
                Sql("INSERT INTO pt_arsenal(syn_id,arsenal_id,arsenal_unlocked,arsenal_enhance,syn_id_bp) VALUES(50003,@slot,1,0,0)","slot",slot);
                Sql("INSERT INTO pt_arsenal_inscribed(uid,name,iten_id,iten_atype,syn_id,arsenal_donation) VALUES(@uid,'level fixture',@item,@slot,50003,5000000)","uid",(long)guild.Leader.ID,"item",(long)(889000+slot),"slot",slot);
            }
            foreach(uint amount in new uint[]{500,400,100}) {
                uint uid=nextUid;Player(Kernel.Maps[1002]);nextUid=uid;var p=Player(Kernel.Maps[1002],true);
                p.Guild=guild;p.Entity.GuildID=50003;p.Entity.GuildRank=(ushort)Enums.GuildMemberRank.Member;
                p.AsMember=new Guild.Member(guild.ID){ID=uid,Name=p.Entity.Name,Level=p.Entity.Level,Class=p.Entity.Class,Rank=Enums.GuildMemberRank.Member};
                guild.Members[uid]=p.AsMember;p.Entity.ConquerPoints=2000;Require(EntityTable.SaveEntity(p));
                var packet=new byte[36];Writer.WriteUInt16(28,0,packet);Writer.WriteUInt16(1107,2,packet);Writer.WriteUInt32(GuildCommand.DonateConquerPoints,4,packet);Writer.WriteUInt32(amount,8,packet);Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,28,packet);
                GuildCommand.Process(packet,p);Require(p.Entity.ConquerPoints==2000-amount && !p.Fake && p.Account.State==AccountTable.AccountState.Player);
            }
            Require((bool)Call(Feature("GuildManagers"),"Reconcile",guild));
            Require(Kernel.GamePool[4294906002].AsMember.Rank==Enums.GuildMemberRank.Manager && Kernel.GamePool[4294906003].AsMember.Rank==Enums.GuildMemberRank.Manager);
            SupervisorVerify(Kernel.GamePool[4294906004]);
            Sql("CREATE TABLE revora_supervisor_checkpoint(process integer,started timestamptz,ownership text,evidence text);INSERT INTO revora_supervisor_checkpoint SELECT @pid,pg_postmaster_start_time(),(SELECT to_jsonb(s)::text FROM revora_guild_supervisors s WHERE uid=4294906004),(SELECT to_jsonb(e)::text FROM revora_achievement_evidence e WHERE uid=4294906004 AND achievement_id=10224)","pid",Process.GetCurrentProcess().Id);
        } else {
            Require((bool)Sql("SELECT process<>@pid AND started<>pg_postmaster_start_time() FROM revora_supervisor_checkpoint","pid",Process.GetCurrentProcess().Id));
            Require(guild.Members[4294906004].Rank==Enums.GuildMemberRank.CPSupervisor);
            nextUid=4294906004;var p=Player(Kernel.Maps[1002],true);SupervisorVerify(p);
            Require((bool)Call(Feature("GuildManagers"),"Reconcile",guild));SupervisorVerify(p);
        }
        if(wire){Kernel.GamePool.Remove(4294906004);SupervisorWire();}
        Require((bool)Sql("SELECT ownership=(SELECT to_jsonb(s)::text FROM revora_guild_supervisors s WHERE uid=4294906004) AND evidence=(SELECT to_jsonb(e)::text FROM revora_achievement_evidence e WHERE uid=4294906004 AND achievement_id=10224) FROM revora_supervisor_checkpoint"));
        System.Console.WriteLine("[supervisor-process] supervisor-"+(wire?"wire":reload?"reload":"capture")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Supervisor fixture did not close.");
    }
    static void SupervisorVerify(GameState p) {
        new TitleStorage().Login(p);
        Require(p.Account.State==AccountTable.AccountState.Player && !p.Fake && p.Entity.GuildID==50003 && p.AsMember.Rank==Enums.GuildMemberRank.CPSupervisor && p.Entity.GuildRank==857 && p.Entity.ConquerPoints==1900);
        const int position=(10224/100-101)*32+10224%100-1;
        Require((System.BitConverter.ToUInt32(p.Entity.MyAchievement.ToArray(),16+position/32*4)&(1u<<(position%32)))!=0);
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10224 AND source='guild-supervisor-v1'","uid",(long)p.Entity.UID)==1);
        Require((long)Sql("SELECT guildconquerpointdonation FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID)==100);
    }
    static void SupervisorWire() {
        const uint uid=4294906004;
        string password=Guid.NewGuid().ToString("N");var account=new AccountTable("SupervisorProbe");
        if(account.exists)Require(account.EntityID==uid && account.State==AccountTable.AccountState.Player);
        bool insert=!account.exists;account.Password=password;account.EntityID=uid;account.State=AccountTable.AccountState.Player;if(insert)account.Insert();account.Save();
        var start=new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CandidateKingdomWireProbe.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Environment.CurrentDirectory};
        start.EnvironmentVariables["REVORA_CANDIDATE_ACCOUNT_PASSWORD"]=password;start.EnvironmentVariables["REVORA_SUPERVISOR_WIRE"]="1";start.EnvironmentVariables.Remove("REVORA_DATABASE_PASSWORD");
        using(var child=Process.Start(start)) {
            start.EnvironmentVariables.Remove("REVORA_CANDIDATE_ACCOUNT_PASSWORD");password=null;var errors=child.StandardError.ReadToEndAsync();
            try {
                TeamWireRead(child,"[supervisor-wire] ready");var player=Kernel.GamePool[uid];SupervisorVerify(player);
                child.StandardInput.WriteLine("stop");child.StandardInput.Flush();TeamWireRead(child,"[supervisor-wire] passed");Require(child.WaitForExit(5000) && child.ExitCode==0);
                System.Console.WriteLine("[supervisor-process] authenticated ordinary player received earned achievement10224 and retained rank857");
            } finally {if(!child.HasExited){child.Kill();child.WaitForExit();}System.Console.Error.WriteLine(errors.Result);}
        }
    }

}
