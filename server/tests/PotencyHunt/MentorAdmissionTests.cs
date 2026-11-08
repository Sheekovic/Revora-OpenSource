using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;
using People = Revora.Database.KnownPersons;

internal static partial class PotencyHuntTests
{
    static readonly Type Admissions = typeof(Entity).Assembly.GetType("Revora.Game.Features.MentorAdmissions");
    static DateTime admissionNow = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    static GameState AdmissionPlayer(Map map, byte level, byte reborn = 0, byte first = 0, byte second = 0)
    {
        var p = SkillPlayer(map); p.Entity.FullyLoaded = false; p.Entity.Level = level; p.Entity.Reborn = reborn;
        p.Entity.FirstRebornLevel = first; p.Entity.SecondRebornLevel = second; p.Entity.FullyLoaded = true;
        EntityTable.SaveEntity(p); People.LoadMentor(p); return p;
    }
    static byte[] AdmissionFrame(uint type, GameState sender, GameState receiver, byte accept = 1)
    {
        byte[] bytes = new MentorApprentice(true) { Type = type, UID = sender.Entity.UID, dwParam = receiver.Entity.UID, Dynamic = accept }.ToArray();
        Writer.WriteUInt64(PacketHandler.ClientSeal, bytes.Length - 8, bytes); return bytes;
    }
    static void InviteMentor(GameState mentor, GameState child, bool childRequests)
    { MentorApprentice.MentorApprentices(AdmissionFrame(childRequests ? 2u : 1u, childRequests ? child : mentor, childRequests ? mentor : child), childRequests ? child : mentor); }
    static void AcceptMentor(GameState mentor, GameState child, bool childRequests, byte response = 1)
    { MentorApprentice.MentorApprentices(AdmissionFrame(childRequests ? 9u : 8u, childRequests ? child : mentor, childRequests ? mentor : child, response), childRequests ? mentor : child); }
    static long AdmissionRows(GameState child) { return Scalar("SELECT count(*) FROM apprentice WHERE apprenticeid=@uid", child.Entity.UID); }
    static long AdmissionAwards(GameState p) { return Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id IN(10124,10125)", p.Entity.UID); }
    static void VerifyMentorAdmissions(Map map)
    {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL);CREATE TABLE apprentice (LIKE public.apprentice INCLUDING ALL)");
        StorageManager.Load(); StorageItem.Load(); PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        Call(EvidenceType, null, "Initialize"); Call(Admissions, null, "Initialize"); Admissions.GetField("Clock", Hidden).SetValue(null, new Func<DateTime>(() => admissionNow));
        foreach (int[] pair in new[] { new[] {69,0},new[] {70,2},new[] {109,2},new[] {110,3},new[] {119,3},new[] {120,5},new[] {129,5},new[] {130,7},new[] {133,7},new[] {134,10},new[] {140,10} })
            Check((int)Call(Admissions, null, "Capacity", pair[0], 0, 0, 0) == pair[1], "official apprentice capacity boundary at level " + pair[0]);
        Check((int)Call(Admissions, null, "Capacity", 15, 1, 134, 0) == 10 && (int)Call(Admissions, null, "Capacity", 15, 2, 120, 130) == 7,
            "mentor capacity retains recorded pre-rebirth levels");
        foreach (int reborn in new[] {0,1,2})
        {
            int gap = 30 - reborn * 10;
            Check((bool)Call(Admissions, null, "Eligible", 130, reborn, 130, 130, 130-gap, reborn)
                && !(bool)Call(Admissions, null, "Eligible", 130, reborn, 130, 130, 131-gap, reborn), "same-rebirth admission respects level difference " + gap);
        }
        Check((bool)Call(Admissions, null, "Eligible", 15, 2, 130, 130, 140, 1)
            && !(bool)Call(Admissions, null, "Eligible", 140, 0, 0, 0, 1, 1), "higher rebirth can teach an earlier rebirth while the reverse is rejected");
        foreach (bool childRequests in new[] { false, true })
        {
            var mentor = AdmissionPlayer(map, 130); var child = AdmissionPlayer(map, 100);
            Check(!mentor.Fake && !child.Fake && mentor.Account.State == AccountTable.AccountState.Player, "mentor admission uses ordinary authenticated players");
            AcceptMentor(mentor, child, childRequests); Check(AdmissionRows(child) == 0, "unsolicited mentor acceptance is rejected in direction " + childRequests);
            InviteMentor(mentor, child, childRequests); Check(AdmissionRows(child) == 0 && AdmissionAwards(mentor) == 0, "invitation alone cannot earn a mentor achievement");
            AcceptMentor(mentor, child, childRequests);
            Check(!Fenced(mentor) && !Fenced(child) && AdmissionRows(child) == 1 && child.Mentor.ID == mentor.Entity.UID
                && mentor.Apprentices.ContainsKey(child.Entity.UID) && mentor.apprtnum == 1 && child.AsApprentice.ID == child.Entity.UID,
                "consent saves and projects both mentor roles in direction " + childRequests);
            Check(HasAchievement(mentor,10124) && HasAchievement(child,10125) && !HasAchievement(mentor,10125) && !HasAchievement(child,10124),
                "mentor and apprentice receive their distinct achievement identities");
            Query("UPDATE apprentice SET actual_experience=600,total_experience=600,actual_plus=7,total_plus=7 WHERE apprenticeid=@uid", "uid", (long)child.Entity.UID);
            Task.WaitAll(Enumerable.Range(0,8).Select(i=>Task.Run(()=>AcceptMentor(mentor,child,childRequests))).ToArray()); People.LoadMentor(mentor); People.LoadMentor(child); EvidenceRefresh(child);
            Check(AdmissionRows(child)==1 && MentorBalance(child)==600 && mentor.PrizeExperience==600 && mentor.Apprentices[child.Entity.UID].Actual_Plus==7 && HasAchievement(child,10125),
                "acceptance replay and production reload preserve existing contributions and completion");
            var other=AdmissionPlayer(map,140); InviteMentor(other,child,false); AcceptMentor(other,child,false);
            Check(child.Mentor.ID==mentor.Entity.UID && AdmissionRows(child)==1 && AdmissionAwards(other)==0, "a new requester cannot replace an existing mentor or its rewards");
        }
        var declining=AdmissionPlayer(map,130);var declined=AdmissionPlayer(map,100);InviteMentor(declining,declined,false);AcceptMentor(declining,declined,false,0);AcceptMentor(declining,declined,false);
        Check(AdmissionRows(declined)==0,"declining an invitation invalidates later acceptance");
        var expiry=AdmissionPlayer(map,130);var late=AdmissionPlayer(map,100);InviteMentor(expiry,late,false);admissionNow=admissionNow.AddMinutes(2);AcceptMentor(expiry,late,false);
        Check(AdmissionRows(late)==0,"expired mentor invitation cannot establish a relationship");
        var guarded=AdmissionPlayer(map,130);var target=AdmissionPlayer(map,100);
        foreach(int size in new[]{0,1,3,8,16,21,31,129,65536})MentorApprentice.MentorApprentices(new byte[size],guarded);
        foreach(int offset in new[]{0,2,21,24}){var f=AdmissionFrame(1,guarded,target);f[offset]=255;MentorApprentice.MentorApprentices(f,guarded);}
        AcceptMentor(guarded,target,false);Check(!Fenced(guarded) && AdmissionRows(target)==0,"malformed mentor frames and overflowing name lengths cannot create consent");
        var name=new MentorApprentice(true){Name=new string('N',1000)};byte[] named=name.ToArray();Writer.WriteUInt64(PacketHandler.ClientSeal,named.Length-8,named);
        Check(named.Length==48 && named[21]==16 && (bool)Call(Admissions,null,"Frame",(object)named),"outgoing mentor names use bounded encoded length and separate suffix");
        var display=new MentorInformation(true){Apprentice_Name=new string('A',32),Mentor_Spouse_Name=new string('S',32),Mentor_Name=new string('M',32)};
        var info=display.ToArray();
        Check(info.Length==158 && info[77]==16 && info[95]==16 && info[113]==16 && info.Skip(150).All(i=>i==0),
            "mentor display preserves inherited spaced name fields independently of assignment order");
        var apprenticeInfo=new ApprenticeInformation{Mentor_Name=new string('M',32),Apprentice_Name=new string('A',32),Apprentice_Spouse_Name=new string('S',32)};
        var app=apprenticeInfo.ToArray();
        Check(app.Length==136 && app[76]==3 && app[77]==16 && app[94]==16 && app[111]==16 && app.Skip(128).All(i=>i==0),
            "apprentice display expands its bounded string list without overwriting the transport suffix");
        apprenticeInfo.Apprentice_Spouse_Name=null;display.Mentor_Spouse_Name=null;
        Check(apprenticeInfo.ToArray()[111]==0 && display.ToArray()[95]==0,"missing spouse names serialize safely without stale text");
        InviteMentor(guarded,target,false);AcceptMentor(guarded,target,true);
        Check(AdmissionRows(target)==0,"opposite-direction acceptance cannot reinterpret a mentor invitation");
        VerifyMentorAdmissionFailures(map);
    }
    static void VerifyMentorAdmissionFailures(Map map)
    {
        foreach(string mode in new[]{"row","receipt","evidence","silent-row","silent-evidence"})
        {
            var mentor=AdmissionPlayer(map,130);var child=AdmissionPlayer(map,100);InviteMentor(mentor,child,false);
            string table=mode=="receipt"?"revora_mentor_admissions":mode.EndsWith("evidence")?"revora_achievement_evidence":"apprentice";
            string effect=mode.StartsWith("silent")?"RETURN NULL;":"RAISE EXCEPTION ''fixture admission failure'';";
            Query("CREATE FUNCTION fail_admission() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN "+effect+" END';CREATE TRIGGER fail_admission BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION fail_admission()");
            AcceptMentor(mentor,child,false);
            Check(Fenced(mentor) && Fenced(child) && AdmissionRows(child)==0 && AdmissionAwards(mentor)==0 && AdmissionAwards(child)==0 && child.Mentor==null && mentor.Apprentices.Count==0,
                "mentor admission rolls back both roles and achievements: "+mode);
            Query("DROP TRIGGER fail_admission ON "+table+";DROP FUNCTION fail_admission()");DemonReset(mentor);DemonReset(child);People.LoadMentor(mentor);People.LoadMentor(child);AcceptMentor(mentor,child,false);
            Check(!Fenced(mentor) && !Fenced(child) && AdmissionRows(child)==1,"mentor admission safely retries after reload: "+mode);
        }
        var full=AdmissionPlayer(map,70);var applicant=AdmissionPlayer(map,40);
        Query("INSERT INTO apprentice(mentorid,apprenticeid,mentorname,apprenticename,enroledate,actual_experience,total_experience,actual_plus,total_plus,actual_heavenblessing,total_heavenblessing) SELECT @uid,90000000+n,'Fixture','Fixture',0,0,0,0,0,0,0 FROM generate_series(1,2)n","uid",(long)full.Entity.UID);
        InviteMentor(full,applicant,false);AcceptMentor(full,applicant,false);
        Check(AdmissionRows(applicant)==0 && AdmissionAwards(full)==0,"durable apprentice count enforces capacity even when the live list is stale");
        var stale=AdmissionPlayer(map,130);var staleChild=AdmissionPlayer(map,100);InviteMentor(stale,staleChild,false);
        Query("UPDATE entities SET level=120 WHERE uid=@uid","uid",(long)stale.Entity.UID);AcceptMentor(stale,staleChild,false);
        Check(AdmissionRows(staleChild)==0 && AdmissionAwards(stale)==0,"admission cannot trust unsaved or externally changed eligibility levels");
        var duplicates=AdmissionPlayer(map,130);var duplicateChild=AdmissionPlayer(map,100);
        Query("INSERT INTO apprentice(mentorid,apprenticeid,mentorname,apprenticename,enroledate,actual_experience,total_experience,actual_plus,total_plus,actual_heavenblessing,total_heavenblessing) SELECT @uid,90000500,'Fixture','Fixture',0,100,100,1,1,2,2 FROM generate_series(1,2)n","uid",(long)duplicates.Entity.UID);
        InviteMentor(duplicates,duplicateChild,false);AcceptMentor(duplicates,duplicateChild,false);
        Check(Fenced(duplicates) && Fenced(duplicateChild) && AdmissionRows(duplicateChild)==0 && Scalar("SELECT sum(actual_experience) FROM apprentice WHERE mentorid=@uid",duplicates.Entity.UID)==200,
            "ambiguous legacy apprentice membership is preserved and cannot admit another child");
        var reconnect=AdmissionPlayer(map,130);var waiting=AdmissionPlayer(map,100);InviteMentor(reconnect,waiting,false);
        var replacement=AdmissionPlayer(map,130);GameState removed;Kernel.GamePool.TryRemove(replacement.Entity.UID,out removed);
        replacement.Entity.UID=reconnect.Entity.UID;replacement.Account.EntityID=reconnect.Entity.UID;Kernel.GamePool[reconnect.Entity.UID]=replacement;AcceptMentor(replacement,waiting,false);
        Check(AdmissionRows(waiting)==0,"a replacement session cannot inherit the old mentor's invitation");
        var competingA=AdmissionPlayer(map,130);var competingB=AdmissionPlayer(map,130);var contested=AdmissionPlayer(map,100);
        InviteMentor(competingA,contested,false);InviteMentor(competingB,contested,false);
        Task.WaitAll(Task.Run(()=>AcceptMentor(competingA,contested,false)),Task.Run(()=>AcceptMentor(competingB,contested,false)));
        Check(!Fenced(contested) && AdmissionRows(contested)==1 && AdmissionAwards(competingA)+AdmissionAwards(competingB)==1,
            "competing consent cannot assign two mentors to the same apprentice");
        var locked=AdmissionPlayer(map,130);var expiry=AdmissionPlayer(map,100);InviteMentor(locked,expiry,false);
        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
            using(var q=new Npgsql.NpgsqlCommand("SELECT uid FROM entities WHERE uid=@uid FOR UPDATE",c,t)){q.Parameters.AddWithValue("uid",(long)Math.Min(locked.Entity.UID,expiry.Entity.UID));q.ExecuteScalar();}
            var attempt=Task.Run(()=>AcceptMentor(locked,expiry,false));
            bool blocked=System.Threading.SpinWait.SpinUntil(()=>Scalar("SELECT count(*) FROM pg_stat_activity WHERE pid<>pg_backend_pid() AND wait_event_type='Lock' AND query LIKE '%SELECT uid FROM entities WHERE uid=%' AND @uid>0",locked.Entity.UID)>0,5000);
            admissionNow=admissionNow.AddMinutes(2);t.Commit();Check(attempt.Wait(5000) && blocked && AdmissionRows(expiry)==0 && !Fenced(locked),"mentor consent expires even while acceptance waits for database ownership");
        }}
    }
}
