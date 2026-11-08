using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle
{
    static bool guildClaimFixture;
    static void GuildClaimPacket(GameState p,uint action,uint target){var b=new GuildCommand(true){Type=action,dwParam=target}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,b.Length-8,b);Revora.Network.PacketHandler.HandlePacket(b,p);}
    static string GuildClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('base',@base::json,
'earning',(SELECT row_to_json(e) FROM revora_guild_earning e WHERE uid=@uid),
'member',(SELECT json_build_array(guildid,guildrank,studypoints) FROM entities WHERE uid=@uid),
'bags',(SELECT json_agg(b ORDER BY used_at,id) FROM revora_silver_study_bag_uses b WHERE uid=@uid))::text",
        "base",NobilityClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void GuildClaimProcess(bool reload){
        guildClaimFixture=true;const uint uid=4294912601;
        if((bool)Sql("SELECT to_regclass('revora_guild_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @first AND @last","first",(long)uid,"last",(long)uid+2)==0);
            nextUid=uid;for(int n=0;n<3;n++)Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=130,reborn=2,conquerpoints=2000000 WHERE uid BETWEEN @first AND @last","first",(long)uid,"last",(long)uid+2);
            Sql("CREATE TABLE revora_guild_claim_probe(uid bigint PRIMARY KEY,snapshot text)");
        }
        nextUid=uid;var players=Enumerable.Range(0,3).Select(_=>Player(Kernel.Maps[1002],true)).ToArray();var leader=players[0];
        foreach(var p in players){SubClassTable.Load(p.Entity);ChiTable.Load(p);WayofHeroes.Load(p);Revora.Database.KnownPersons.LoadPartner(p);Revora.Database.KnownPersons.LoaderFriends(p);Revora.Database.KnownPersons.LoadMentor(p);Revora.Database.KnownPersons.LoadEnemy(p);p.LoadItemStats();Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));}
        if((long)Sql("SELECT count(*) FROM revora_guild_claim_probe")==0){Require(!reload);
            foreach(var p in players){string before=GuildClaimSnapshot(p);NobilityClaimPacket(p,303);Require(before==GuildClaimSnapshot(p));}
            if(leader.Guild==null){HeartTalk(leader,10003,9);var name=System.Text.Encoding.ASCII.GetBytes("GuildProbe9600");var b=new byte[28+name.Length];Revora.Network.Writer.WriteUInt16((ushort)(b.Length-8),0,b);Revora.Network.Writer.WriteUInt16(2032,2,b);Revora.Network.Writer.WriteUInt32(10003,8,b);b[14]=2;b[17]=(byte)name.Length;name.CopyTo(b,18);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,b.Length-8,b);Revora.Network.PacketHandler.HandlePacket(b,leader);}
            Require(leader.Guild!=null && (bool)Sql("SELECT founder FROM revora_guild_earning WHERE uid=@uid","uid",(long)uid));
            // Invitation first for one recruit, application first for the other.
            GuildClaimPacket(leader,GuildCommand.InviteRequest,players[1].Entity.UID);
            foreach(var p in players.Skip(1)){string pending=GuildClaimSnapshot(p);NobilityClaimPacket(p,303);Require(pending==GuildClaimSnapshot(p) && p.Guild==null);}
            GuildClaimPacket(players[1],GuildCommand.JoinRequest,uid);Require(ReferenceEquals(players[1].Guild,leader.Guild));
            GuildClaimPacket(players[2],GuildCommand.JoinRequest,uid);string applied=GuildClaimSnapshot(players[2]);NobilityClaimPacket(players[2],303);Require(applied==GuildClaimSnapshot(players[2]) && players[2].Guild==null);
            GuildClaimPacket(leader,GuildCommand.InviteRequest,players[2].Entity.UID);Require(ReferenceEquals(players[2].Guild,leader.Guild));
            foreach(var p in players.Skip(1)){Require(!(bool)Sql("SELECT founder FROM revora_guild_earning WHERE uid=@uid","uid",(long)p.Entity.UID));GuildClaimPacket(p,GuildCommand.Quit,leader.Guild.ID);Require(p.Guild==null && p.Entity.GuildID==0);}
            foreach(var p in players){
                ushort study=p.Entity.SubClasses.StudyPoints;System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,303));var reward=p.Inventory.Objects.Single();Require(reward.ID==3005123 && reward.Bound && reward.StackSize==1 && reward.Plus==0);
                SoulClaimUse(p,reward);Require(!PirateFenced(p) && p.Inventory.Count==0 && p.Entity.SubClasses.StudyPoints==study+200);
                Require(EntityTable.SaveEntity(p));Sql("INSERT INTO revora_guild_claim_probe(uid,snapshot) VALUES(@uid,@snapshot)","uid",(long)p.Entity.UID,"snapshot",GuildClaimSnapshot(p));
            }
        }
        foreach(var p in players){
            Require(p.Inventory.Count==0 && p.Entity.SubClasses.StudyPoints==200);
            Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=303 AND NOT legacy","uid",(long)p.Entity.UID)==1);
            Require((long)Sql("SELECT count(*) FROM revora_silver_study_bag_uses WHERE uid=@uid AND item_type=3005123","uid",(long)p.Entity.UID)==1);
            string before=GuildClaimSnapshot(p);Require(before==Convert.ToString(Sql("SELECT snapshot FROM revora_guild_claim_probe WHERE uid=@uid","uid",(long)p.Entity.UID)));NobilityClaimPacket(p,303);Require(before==GuildClaimSnapshot(p) && !PirateFenced(p));
        }
        // Existing CTF fixture founded its guild before this earning table existed.
        // Read eligibility without changing that independent checkpoint or claiming its reward.
        Require((long)Sql("SELECT count(*) FROM revora_guild_earning WHERE uid=4294912301")==0);
        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
            Require((bool)Call(Feature("WayOfHeroesClaims"),"SavedGuild",c,t,4294912301u));
            Require(!(bool)Call(Feature("WayOfHeroesClaims"),"SavedGuild",c,t,4294912501u));
            t.Rollback();
        }}
        System.Console.WriteLine("[career-process] career-guild-claim-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Guild claim did not close.");
    }
}
