using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle
{
    static bool teamClaimFixture;
    static void TeamClaimPacket(GameState p,uint action,uint target){System.Console.WriteLine("[team-claim] actor="+p.Entity.UID+" action="+action);var frame=new Team{Type=action,UID=target}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);Revora.Network.PacketHandler.HandlePacket(frame,p);}
    static string TeamClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('base',@snapshot::json,
'earning',(SELECT row_to_json(e) FROM revora_team_earning e WHERE uid=@uid))::text","snapshot",PotionClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void TeamClaimProcess(bool reload){
        teamClaimFixture=true;const uint uid=4294912001;
        if((bool)Sql("SELECT to_regclass('revora_team_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @uid AND @last","uid",(long)uid,"last",(long)uid+2)==0);
            nextUid=uid;for(int n=0;n<3;n++)Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=50,reborn=0 WHERE uid BETWEEN @uid AND @last","uid",(long)uid,"last",(long)uid+2);
            Sql("CREATE TABLE revora_team_claim_probe(uid bigint PRIMARY KEY,snapshot text)");
        }
        nextUid=uid;var players=Enumerable.Range(0,3).Select(_=>Player(Kernel.Maps[1002],true)).ToArray();var leader=players[0];
        foreach(var p in players){ChiTable.Load(p);WayofHeroes.Load(p);p.LoadItemStats();Revora.Database.KnownPersons.LoadPartner(p);Revora.Database.KnownPersons.LoaderFriends(p);Revora.Database.KnownPersons.LoadMentor(p);Revora.Database.KnownPersons.LoadEnemy(p);Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));}
        if((long)Sql("SELECT count(*) FROM revora_team_claim_probe")==0){
            Require(!reload);foreach(var p in players.Where(p=>(long)Sql("SELECT count(*) FROM revora_team_earning WHERE uid=@uid","uid",(long)p.Entity.UID)==0)){string before=TeamClaimSnapshot(p);NobilityClaimPacket(p,203);Require(before==TeamClaimSnapshot(p));}
            TeamClaimPacket(leader,Team.Create,uid);Require(leader.Team!=null && leader.Team.Lider==leader);
            string made=TeamClaimSnapshot(leader);TeamClaimPacket(leader,Team.Create,uid);Require(made==TeamClaimSnapshot(leader));
            TeamClaimPacket(players[1],Team.AcceptInvitation,uid);TeamClaimPacket(leader,Team.AcceptJoinRequest,players[2].Entity.UID);
            Require(players[1].Team==null && players[2].Team==null);
            TeamClaimPacket(leader,Team.InviteRequest,players[1].Entity.UID);TeamClaimPacket(players[2],Team.JoinRequest,uid);
            foreach(var p in players.Skip(1)){string pending=TeamClaimSnapshot(p);NobilityClaimPacket(p,203);Require(pending==TeamClaimSnapshot(p));}
            TeamClaimPacket(players[1],Team.AcceptInvitation,uid);TeamClaimPacket(leader,Team.AcceptJoinRequest,players[2].Entity.UID);
            Require(players.All(p=>ReferenceEquals(p.Team,leader.Team)) && leader.Team.Members.Count==3);
            for(int n=0;n<3;n++)Require(Convert.ToInt32(Sql("SELECT kind FROM revora_team_earning WHERE uid=@uid AND leader=@leader","uid",(long)players[n].Entity.UID,"leader",(long)uid))==n);
            TeamClaimPacket(players[1],Team.ExitTeam,players[1].Entity.UID);TeamClaimPacket(players[2],Team.ExitTeam,players[2].Entity.UID);TeamClaimPacket(leader,Team.Dismiss,uid);
            Require(players.All(p=>p.Team==null));
            foreach(var p in players){
                System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,203));var potion=p.Inventory.Objects.Single();Require(potion.ID==723017 && potion.Bound);
                SoulClaimUse(p,potion);Require(p.Entity.DoubleExperienceTime==3600 && p.Inventory.Count==0 && !PirateFenced(p));
                Require(EntityTable.SaveEntity(p));Sql("INSERT INTO revora_team_claim_probe(uid,snapshot) VALUES(@uid,@snapshot)","uid",(long)p.Entity.UID,"snapshot",TeamClaimSnapshot(p));
            }
        }
        foreach(var p in players){
            Require(p.Team==null && p.Inventory.Count==0 && p.Entity.DoubleExperienceTime==3600);
            Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=203 AND NOT legacy","uid",(long)p.Entity.UID)==1);
            Require((long)Sql("SELECT count(*) FROM revora_exp_potion_uses WHERE uid=@uid AND item_type=723017 AND seconds=3600","uid",(long)p.Entity.UID)==1);
            string exact=TeamClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_team_claim_probe WHERE uid=@uid","uid",(long)p.Entity.UID)));
            NobilityClaimPacket(p,203);Require(exact==TeamClaimSnapshot(p) && !PirateFenced(p));
        }
        System.Console.WriteLine("[career-process] career-team-claim-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Team claim did not close.");
    }
}
