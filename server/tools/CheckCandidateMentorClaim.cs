using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool mentorClaimFixture;
    static string MentorClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'base',@snapshot::json,'admissions',(SELECT json_agg(a ORDER BY id) FROM revora_mentor_admissions a WHERE mentor=@uid OR apprentice=@uid),
'departures',(SELECT json_agg(a ORDER BY id) FROM revora_mentor_departures a WHERE mentor=@uid OR apprentice=@uid),
'membership',(SELECT json_agg(a ORDER BY mentorid,apprenticeid) FROM apprentice a WHERE mentorid=@uid OR apprenticeid=@uid))::text",
        "snapshot",CompositionClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void MentorClaimProcess(bool reload){
        mentorClaimFixture=true;const uint uid=4294911701;
        if((bool)Sql("SELECT to_regclass('revora_mentor_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @uid AND @last","uid",(long)uid,"last",(long)uid+1)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=CASE WHEN uid=@uid THEN 140 ELSE 130 END,reborn=2 WHERE uid BETWEEN @uid AND @last","uid",(long)uid,"last",(long)uid+1);
            Sql("CREATE TABLE revora_mentor_claim_probe(snapshot text,child_snapshot text);INSERT INTO revora_mentor_claim_probe VALUES(NULL,NULL)");
        }
        nextUid=uid;var mentor=Player(Kernel.Maps[1002],true);var child=Player(Kernel.Maps[1002],true);
        foreach(var p in new[]{mentor,child}){ChiTable.Load(p);WayofHeroes.Load(p);Revora.Database.KnownPersons.LoadMentor(p);
            Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));}
        if(Sql("SELECT snapshot FROM revora_mentor_claim_probe")==DBNull.Value){Require(!reload && mentor.Inventory.Count==0 && child.Inventory.Count==0);
            foreach(var p in new[]{mentor,child}){string before=MentorClaimSnapshot(p);NobilityClaimPacket(p,705);Require(before==MentorClaimSnapshot(p));}
            RetainedMentorPacket(child,MentorApprentice.AcceptRequestApprentice,uid);
            Require(child.Mentor==null && mentor.Apprentices.Count==0);
            RetainedMentorPacket(mentor,MentorApprentice.RequestApprentice,child.Entity.UID);
            string pending=MentorClaimSnapshot(mentor);NobilityClaimPacket(mentor,705);Require(pending==MentorClaimSnapshot(mentor));
            RetainedMentorPacket(child,MentorApprentice.AcceptRequestApprentice,uid);
            Require(child.Mentor!=null && child.Mentor.ID==uid && mentor.Apprentices.ContainsKey(child.Entity.UID));
            string admitted=MentorClaimSnapshot(mentor);RetainedMentorPacket(child,MentorApprentice.AcceptRequestApprentice,uid);Require(admitted==MentorClaimSnapshot(mentor));
            string childBefore=MentorClaimSnapshot(child);NobilityClaimPacket(child,705);Require(childBefore==MentorClaimSnapshot(child));
            RetainedMentorPacket(mentor,MentorApprentice.ExpellApprentice,child.Entity.UID);Require(child.Mentor==null && mentor.Apprentices.Count==0);
            System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(mentor,705));var stone=mentor.Inventory.Objects.Single();
            Require(stone.ID==730002 && stone.Plus==2 && stone.Bound && stone.StackSize==1);
            uint type=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID/1000==160 && i.ID%10==5 && i.Level<=15).OrderBy(i=>i.ID).First().ID;
            var boots=RefineryClaimGive(mentor,type);CompositionClaimCompose(mentor,boots,stone);Require(boots.Plus==2 && boots.PlusProgress==0);
            Require(EntityTable.SaveEntity(mentor) && EntityTable.SaveEntity(child));
            Sql("UPDATE revora_mentor_claim_probe SET snapshot=@snapshot,child_snapshot=@child","snapshot",MentorClaimSnapshot(mentor),"child",MentorClaimSnapshot(child));
        }
        Require(mentor.Apprentices.Count==0 && child.Mentor==null && mentor.Inventory.Count==1 && child.Inventory.Count==0);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=705 AND NOT legacy","uid",(long)uid)==1);
        Require((long)Sql("SELECT count(*) FROM revora_mentor_admissions WHERE mentor=@uid","uid",(long)uid)==1);
        Require((long)Sql("SELECT count(*) FROM revora_mentor_departures WHERE mentor=@uid AND reason='expel'","uid",(long)uid)==1);
        string exact=MentorClaimSnapshot(mentor),childExact=MentorClaimSnapshot(child);
        Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_mentor_claim_probe")) && childExact==Convert.ToString(Sql("SELECT child_snapshot FROM revora_mentor_claim_probe")));
        NobilityClaimPacket(mentor,705);NobilityClaimPacket(child,705);Require(exact==MentorClaimSnapshot(mentor) && childExact==MentorClaimSnapshot(child) && !PirateFenced(mentor) && !PirateFenced(child));
        System.Console.WriteLine("[career-process] career-mentor-claim-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Mentor claim did not close.");
    }
}
