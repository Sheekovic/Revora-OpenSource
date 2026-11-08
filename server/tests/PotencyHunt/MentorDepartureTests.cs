using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using People = Revora.Database.KnownPersons;

internal static partial class PotencyHuntTests
{
    static readonly Type Departures = typeof(Entity).Assembly.GetType("Revora.Game.Features.MentorDepartures");
    static DateTime departureNow = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    static GameState DeparturePlayer(Map map, byte level)
    { var p=AdmissionPlayer(map,level);p.Entity.Money=100000;EntityTable.UpdateMoney(p);return p; }
    static void DepartureLink(GameState mentor,GameState child,long exp=600)
    {
        InviteMentor(mentor,child,false);AcceptMentor(mentor,child,false);
        if(child.Mentor==null)throw new Exception("Fixture admission failed.");
        Query("UPDATE apprentice SET actual_experience=@exp,total_experience=@exp,actual_plus=250,total_plus=300,actual_heavenblessing=24,total_heavenblessing=48 WHERE apprenticeid=@uid","exp",exp,"uid",(long)child.Entity.UID);
        People.LoadMentor(mentor);People.LoadMentor(child);
    }
    static void Depart(GameState mentor,GameState child,bool expel=false)
    { MentorApprentice.MentorApprentices(AdmissionFrame(expel?4u:3u,expel?mentor:child,expel?child:mentor),expel?mentor:child); }
    static long Retained(GameState mentor,string column)
    { return Scalar("SELECT COALESCE(sum("+column+"),0)::bigint FROM revora_mentor_departures WHERE mentor=@uid",mentor.Entity.UID); }
    static void VerifyMentorDepartures(Map map)
    {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL);CREATE TABLE apprentice (LIKE public.apprentice INCLUDING ALL)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);Call(EvidenceType,null,"Initialize");Call(Admissions,null,"Initialize");
        Departures.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>departureNow));Admissions.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>departureNow));
        var mentor=DeparturePlayer(map,130);var child=DeparturePlayer(map,100);DepartureLink(mentor,child);
        Check(!mentor.Fake && child.Account.State==AccountTable.AccountState.Player,"departure fixtures are ordinary authenticated players");
        Depart(mentor,child);
        Check(!Fenced(mentor) && !Fenced(child) && AdmissionRows(child)==0 && child.Mentor==null && child.AsApprentice==null && mentor.Apprentices.Count==0,
            "departure immediately ends the saved membership and refreshes both role caches");
        Check(child.Entity.Money==50000 && mentor.Entity.Money==150000 && Retained(mentor,"original_fee")==50000 && Retained(mentor,"fee_gold")==0,
            "apprentice pays exactly fifty thousand silver and the online mentor receives it once");
        Check(Retained(mentor,"actual_experience")==600 && Retained(mentor,"actual_plus")==250 && Retained(mentor,"actual_heavenblessing")==24
            && Retained(mentor,"(original_row->>'total_plus')::bigint")==300 && mentor.PrizeExperience==600,
            "departure retains all contribution categories and the original row while exposing claimable EXP");
        Check(HasAchievement(mentor,10124) && HasAchievement(child,10125),"departure does not revoke earned role achievements");
        Task.WaitAll(Enumerable.Range(0,8).Select(i=>Task.Run(()=>Depart(mentor,child))).ToArray());
        Check(child.Entity.Money==50000 && mentor.Entity.Money==150000 && Retained(mentor,"original_fee")==50000,"parallel departure replay cannot charge or pay again");
        var next=DeparturePlayer(map,140);InviteMentor(next,child,false);AcceptMentor(next,child,false);
        Check(AdmissionRows(child)==0,"a departed apprentice cannot immediately accept a new mentor");
        departureNow=departureNow.AddDays(3).AddTicks(-1);InviteMentor(next,child,false);AcceptMentor(next,child,false);
        Check(AdmissionRows(child)==0,"admission remains blocked until the full seventy-two-hour cooldown ends");
        departureNow=departureNow.AddTicks(1);AcceptMentor(next,child,false);
        Check(AdmissionRows(child)==1 && child.Mentor.ID==next.Entity.UID,"an unexpired new invitation can be accepted at the exact cooldown boundary");
        ulong before=mentor.Entity.Experience;ClaimMentor(mentor);
        Check(!Fenced(mentor) && Retained(mentor,"actual_experience")==0 && mentor.Entity.Experience==before+mentor.ExpBall && mentor.PrizeExperience==0,
            "native mentor claim delivers retained EXP through the complete progression transaction");
        Check(child.Mentor.ID==next.Entity.UID && child.AsApprentice.Actual_Experience==0 && Retained(mentor,"total_experience")==600,
            "claiming old contributions cannot alter the apprentice's new relationship and retains lifetime evidence");
        ClaimMentor(mentor);Check(mentor.Entity.Experience==before+mentor.ExpBall,"retained EXP cannot be claimed twice");

        var offline=DeparturePlayer(map,130);var leaving=DeparturePlayer(map,100);DepartureLink(offline,leaving);GameState removed;Kernel.GamePool.TryRemove(offline.Entity.UID,out removed);Depart(offline,leaving);
        Check(!Fenced(leaving) && leaving.Entity.Money==50000 && Retained(offline,"fee_gold")==50000 && AdmissionRows(leaving)==0,"offline mentor retains its full fee and contribution entitlements");
        Kernel.GamePool[offline.Entity.UID]=offline;People.LoadMentor(offline);Call(Departures,null,"ClaimFees",offline);Call(Departures,null,"ClaimFees",offline);
        Check(offline.Entity.Money==150000 && Retained(offline,"fee_gold")==0 && offline.PrizeExperience==600,"login/reward recovery delivers an offline fee once and restores retained EXP");
        var rich=DeparturePlayer(map,130);var richChild=DeparturePlayer(map,100);DepartureLink(rich,richChild);rich.Entity.Money=Entity.MaximumSilver;EntityTable.UpdateMoney(rich);Depart(rich,richChild);
        Check(!Fenced(rich) && Retained(rich,"fee_gold")==50000 && rich.Entity.Money==Entity.MaximumSilver,"a full mentor purse leaves the fee durably pending");
        rich.Entity.Money-=50000;EntityTable.UpdateMoney(rich);Call(Departures,null,"ClaimFees",rich);
        Check(rich.Entity.Money==Entity.MaximumSilver && Retained(rich,"fee_gold")==0,"pending fee is delivered when the purse has enough room");
        var expulsor=DeparturePlayer(map,130);var expelled=DeparturePlayer(map,100);DepartureLink(expulsor,expelled);Kernel.GamePool.TryRemove(expelled.Entity.UID,out removed);Depart(expulsor,expelled,true);Kernel.GamePool[expelled.Entity.UID]=expelled;People.LoadMentor(expelled);
        Check(expulsor.Entity.Money==100000 && expelled.Entity.Money==100000 && AdmissionRows(expelled)==0 && Retained(expulsor,"actual_experience")==600 && Retained(expulsor,"original_fee")==0,
            "owned expulsion works offline without an apprentice departure fee and preserves contributions");
        var stranger=DeparturePlayer(map,130);Depart(stranger,child,true);
        Check(child.Mentor.ID==next.Entity.UID && AdmissionRows(child)==1 && Retained(stranger,"original_fee")==0,"a stranger cannot expel another mentor's apprentice");
        var poor=DeparturePlayer(map,100);var teacher=DeparturePlayer(map,130);DepartureLink(teacher,poor);poor.Entity.Money=49999;EntityTable.UpdateMoney(poor);Depart(teacher,poor);
        Check(AdmissionRows(poor)==1 && poor.Entity.Money==49999 && Retained(teacher,"actual_experience")==0,"insufficient silver leaves the relationship and contributions intact");
        VerifyMentorDepartureFailures(map);
    }
    static void VerifyMentorDepartureFailures(Map map)
    {
        foreach(string mode in new[]{"archive","debit","delete","silent-delete"})
        {
            var mentor=DeparturePlayer(map,130);var child=DeparturePlayer(map,100);DepartureLink(mentor,child);
            string table=mode=="archive"?"revora_mentor_departures":mode=="debit"?"entities":"apprentice";
            string operation=mode=="archive"?"INSERT":mode=="debit"?"UPDATE":"DELETE";
            string effect=mode=="silent-delete"?"RETURN NULL;":"RAISE EXCEPTION ''fixture departure failure'';";
            Query("CREATE FUNCTION fail_departure() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN "+effect+" END';CREATE TRIGGER fail_departure BEFORE "+operation+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION fail_departure()");
            Depart(mentor,child);
            Check(Fenced(child) && AdmissionRows(child)==1 && child.Entity.Money==100000 && mentor.Entity.Money==100000 && Retained(mentor,"original_fee")==0 && MentorBalance(child)==600,
                "departure preserves membership, fee and contributions on failure: "+mode);
            Query("DROP TRIGGER fail_departure ON "+table+";DROP FUNCTION fail_departure()");DemonReset(mentor);DemonReset(child);People.LoadMentor(mentor);People.LoadMentor(child);Depart(mentor,child);
            Check(!Fenced(mentor) && !Fenced(child) && AdmissionRows(child)==0 && child.Entity.Money==50000 && mentor.Entity.Money==150000,"departure retry succeeds exactly once after reload: "+mode);
        }
        var claimant=DeparturePlayer(map,130);var source=DeparturePlayer(map,100);DepartureLink(claimant,source);Depart(claimant,source);
        Query("CREATE FUNCTION fail_retained_exp() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RAISE EXCEPTION ''fixture retained EXP failure''; END';CREATE TRIGGER fail_retained_exp BEFORE UPDATE OF experience ON entities FOR EACH ROW EXECUTE FUNCTION fail_retained_exp()");
        bool rejected=false;try{ClaimMentor(claimant);}catch(Npgsql.PostgresException){rejected=true;}
        Check(rejected && Fenced(claimant) && Retained(claimant,"actual_experience")==600,"failed retained EXP grant rolls its reserve debit back");
        Query("DROP TRIGGER fail_retained_exp ON entities;DROP FUNCTION fail_retained_exp()");DemonReset(claimant);LoadProgressionImage(claimant);ClaimMentor(claimant);
        Check(!Fenced(claimant) && Retained(claimant,"actual_experience")==0,"retained EXP remains claimable after failure and reload");
        var feeOwner=DeparturePlayer(map,130);var payer=DeparturePlayer(map,100);DepartureLink(feeOwner,payer);
        Query("CREATE FUNCTION fail_fee() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.uid="+feeOwner.Entity.UID+" THEN RAISE EXCEPTION ''fixture fee failure''; END IF; RETURN NEW; END';CREATE TRIGGER fail_fee BEFORE UPDATE OF money ON entities FOR EACH ROW EXECUTE FUNCTION fail_fee()");
        Depart(feeOwner,payer);
        Check(Fenced(feeOwner) && !Fenced(payer) && payer.Entity.Money==50000 && AdmissionRows(payer)==0 && feeOwner.Entity.Money==100000 && Retained(feeOwner,"fee_gold")==50000,
            "failed fee delivery leaves the completed departure and exact mentor entitlement intact");
        Query("DROP TRIGGER fail_fee ON entities;DROP FUNCTION fail_fee()");DemonReset(feeOwner);People.LoadMentor(feeOwner);Call(Departures,null,"ClaimFees",feeOwner);
        Check(!Fenced(feeOwner) && feeOwner.Entity.Money==150000 && Retained(feeOwner,"fee_gold")==0,"fee delivery can recover after its transaction fails");
        var ambiguous=DeparturePlayer(map,130);var ambiguousChild=DeparturePlayer(map,100);DepartureLink(ambiguous,ambiguousChild);
        Query("INSERT INTO apprentice SELECT * FROM apprentice WHERE apprenticeid=@uid","uid",(long)ambiguousChild.Entity.UID);
        Depart(ambiguous,ambiguousChild);
        Check(Fenced(ambiguousChild) && AdmissionRows(ambiguousChild)==2 && ambiguousChild.Entity.Money==100000 && Retained(ambiguous,"original_fee")==0,
            "ambiguous legacy departures preserve every original row and cannot charge a fee");
        var capped=DeparturePlayer(map,130);var reserve=DeparturePlayer(map,100);DepartureLink(capped,reserve,31000);Depart(capped,reserve);capped.Entity.Level=Config.LevelCap;EntityTable.SaveEntity(capped);ClaimMentor(capped);
        Check(Retained(capped,"actual_experience")==31000 && capped.PrizeExperience==30300,"maximum-level mentors keep all retained EXP beyond the visible cap");
    }
}
