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
    static readonly Type Blessings=typeof(Server).Assembly.GetType("Revora.Game.Features.BlessingBalance");
    static void ClaimBlessing(GameState p)
    { var frame=MentorClaimPacket();Writer.WriteUInt32(3,4,frame);MentorPrize.Process(frame,p); }
    static long BlessingHours(GameState child)
    { return Scalar("SELECT actual_heavenblessing FROM apprentice WHERE apprenticeid=@uid",child.Entity.UID); }
    static long BlessingSeconds(GameState p)
    { return Scalar("SELECT heavenblessingtime FROM entities WHERE uid=@uid",p.Entity.UID); }
    static void LoadBlessingImage(GameState p)
    {
        using(var q=new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("uid",p.Entity.UID))
        using(var r=new MySqlReader(q)) { if(!r.Read())throw new Exception("Fixture owner missing.");Call(Blessings,null,"Loaded",p,r); }
    }
    static void VerifyMentorBlessings(Map map)
    {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL);CREATE TABLE apprentice (LIKE public.apprentice INCLUDING ALL);CREATE TABLE itemadding (LIKE public.itemadding INCLUDING ALL)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);Call(EvidenceType,null,"Initialize");Call(Admissions,null,"Initialize");ConquerItem.ItemUID=new Counter(4950000);
        var mentor=DeparturePlayer(map,130);var child=DeparturePlayer(map,100);StoneLink(mentor,child,250);
        Check(!mentor.Fake && mentor.Account.State==AccountTable.AccountState.Player,"blessing claims use an ordinary authenticated mentor");
        ClaimBlessing(mentor);
        Check(!Fenced(mentor) && mentor.Entity.HeavenBlessing==86400 && BlessingSeconds(mentor)==86400 && BlessingHours(child)==0,
            "twenty-four stored hours deliver twenty-four hours rather than twenty-four seconds");
        Check(mentor.PrizeHeavenBlessing==0 && child.AsApprentice.Actual_HeavenBlessing==0 && child.AsApprentice.Total_HeavenBlessing==48,
            "blessing claim refreshes both contribution views while preserving lifetime hours");
        Check(StonePoints(child)==250 && MentorBalance(child)==600,"blessing claim preserves plus and EXP rewards");
        ClaimBlessing(mentor);Check(mentor.Entity.HeavenBlessing==86400 && Scalar("SELECT count(*) FROM revora_mentor_blessing_claims WHERE mentor=@uid",mentor.Entity.UID)==1,
            "repeated native claims cannot repeat a delivered blessing reward");
        People.SaveApprenticeInfo(new Revora.Game.ConquerStructures.Society.Apprentice{ID=child.Entity.UID,Actual_HeavenBlessing=24,Total_HeavenBlessing=48});
        Check(BlessingHours(child)==0,"stale snapshots cannot restore claimed blessing hours");
        Call(typeof(Entity),mentor.Entity,"TickHeavenBlessing");EntityTable.SaveEntity(mentor);LoadBlessingImage(mentor);
        Check(mentor.Entity.HeavenBlessing==86399 && BlessingSeconds(mentor)==86399,"normal character saves persist owned countdown ticks");
        mentor.AddBless(3600);Check(mentor.Entity.HeavenBlessing==89999 && BlessingSeconds(mentor)==89999,"a grant after countdown save uses the committed blessing preimage");
        var retired=DeparturePlayer(map,130);var oldChild=DeparturePlayer(map,100);StoneLink(retired,oldChild,250);Depart(retired,oldChild,true);ClaimBlessing(retired);
        Check(!Fenced(retired) && retired.Entity.HeavenBlessing==86400 && Retained(retired,"actual_heavenblessing")==0
            && Retained(retired,"total_heavenblessing")==48 && Retained(retired,"actual_plus")==250,"departed apprentices retain separately claimable blessing hours");
        var parent=DeparturePlayer(map,140);var middle=DeparturePlayer(map,130);var bottom=DeparturePlayer(map,100);MentorLink(parent,middle,0);StoneLink(middle,bottom,100);ClaimBlessing(middle);
        Check(middle.Entity.HeavenBlessing==86400 && BlessingHours(middle)==0 && parent.PrizeHeavenBlessing==0,"claiming mentor blessing does not recursively credit an upstream mentor");
        var grantParent=DeparturePlayer(map,130);var grantChild=DeparturePlayer(map,100);MentorLink(grantParent,grantChild,0);
        grantChild.AddBless(3*86400);grantChild.AddBless(3*86400);
        Check(BlessingSeconds(grantChild)==6*86400 && BlessingHours(grantChild)==0 && grantParent.PrizeHeavenBlessing==0,
            "rapid generic grants persist blessing time without inventing a mentor earning source");
        Check(grantChild.AsApprentice.Actual_HeavenBlessing==0 && grantChild.AsApprentice.Total_HeavenBlessing==0,"generic grant leaves both actual and lifetime mentor contribution unchanged");
        var parallel=DeparturePlayer(map,130);var parallelChild=DeparturePlayer(map,100);StoneLink(parallel,parallelChild,100);
        Task.WaitAll(Enumerable.Range(0,8).Select(i=>Task.Run(()=>ClaimBlessing(parallel))).ToArray());
        Check(!Fenced(parallel) && parallel.Entity.HeavenBlessing==86400 && BlessingHours(parallelChild)==0,"parallel blessing claims deliver one reward");
        Task.WaitAll(Enumerable.Range(0,20).Select(i=>Task.Run(()=>Call(typeof(Entity),parallel.Entity,"TickHeavenBlessing"))).Concat(Enumerable.Range(0,4).Select(i=>Task.Run(()=>parallel.AddBless(3600)))).ToArray());
        EntityTable.SaveEntity(parallel);Check(parallel.Entity.HeavenBlessing==100780 && BlessingSeconds(parallel)==100780,"grants, countdown ticks and saves share the character owner lock");
        var cap=DeparturePlayer(map,130);var capChild=DeparturePlayer(map,100);StoneLink(cap,capChild,100);cap.Entity.HeavenBlessing=uint.MaxValue-7200;ClaimBlessing(cap);
        Check(!Fenced(cap) && cap.Entity.HeavenBlessing==uint.MaxValue && BlessingHours(capChild)==22 && cap.PrizeHeavenBlessing==22,"only whole hours that fit are claimed; all undelivered hours remain owed");
        ClaimBlessing(cap);Check(!Fenced(cap) && BlessingHours(capChild)==22,"a full blessing wallet preserves its remaining mentor entitlement");
        var expiry=DeparturePlayer(map,130);expiry.Entity.HeavenBlessing=1;Call(typeof(Entity),expiry.Entity,"TickHeavenBlessing");EntityTable.SaveEntity(expiry);
        Check(BlessingSeconds(expiry)==0 && !expiry.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.HeavenBlessing),"expiry clears the blessing flag and persists zero remaining time");
        var foreign=DeparturePlayer(map,130);var forged=MentorClaimPacket();Writer.WriteUInt32(3,4,forged);Writer.WriteUInt32(cap.Entity.UID,8,forged);Writer.WriteUInt16(65535,32,forged);MentorPrize.Process(forged,foreign);
        Check(foreign.Entity.HeavenBlessing==0 && BlessingHours(capChild)==22,"forged mentor identity and display hours cannot claim another character's rewards");
        var wide=DeparturePlayer(map,130);var wideChild=DeparturePlayer(map,100);MentorLink(wide,wideChild,0);
        Query("UPDATE apprentice SET actual_heavenblessing=100000,total_heavenblessing=200000 WHERE apprenticeid=@uid","uid",(long)wideChild.Entity.UID);People.LoadMentor(wide);People.LoadMentor(wideChild);
        Check(wide.PrizeHeavenBlessing==65535 && wideChild.AsApprentice.Actual_HeavenBlessing==65535 && wideChild.AsApprentice.Total_HeavenBlessing==65535,
            "wide durable contributions clamp native views without wrapping or destroying their balances");
        ClaimBlessing(wide);Check(!Fenced(wide) && wide.Entity.HeavenBlessing==65535u*3600 && BlessingHours(wideChild)==34465,
            "a bounded blessing claim retains hours beyond the native display limit");
        ClaimBlessing(wide);Check(!Fenced(wide) && wide.Entity.HeavenBlessing==360000000 && BlessingHours(wideChild)==0
            && Scalar("SELECT total_heavenblessing FROM apprentice WHERE apprenticeid=@uid",wideChild.Entity.UID)==200000,"subsequent claims deliver the wide balance without truncating lifetime history");
        wideChild.AddBless(3*86400);Check(!Fenced(wideChild) && BlessingHours(wideChild)==0 && wideChild.AsApprentice.Total_HeavenBlessing==65535,
            "generic blessing grants preserve wide mentor history without creating credit");
        VerifyBlessingFailures(map);
    }
    static void VerifyBlessingFailures(Map map)
    {
        foreach(string mode in new[]{"debit","retained-debit","wallet","receipt","silent-wallet"})
        {
            var mentor=DeparturePlayer(map,130);var child=DeparturePlayer(map,100);StoneLink(mentor,child,250);bool retained=mode=="retained-debit";if(retained)Depart(mentor,child,true);
            string table=mode=="debit"?"apprentice":retained?"revora_mentor_departures":mode=="receipt"?"revora_mentor_blessing_claims":"entities";
            string operation=mode=="receipt"?"INSERT":table=="entities"?"UPDATE OF heavenblessingtime":"UPDATE OF actual_heavenblessing";
            string effect=mode=="silent-wallet"?"RETURN NULL;":"RAISE EXCEPTION ''fixture blessing failure'';";
            Query("CREATE FUNCTION fail_blessing() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN "+effect+" END';CREATE TRIGGER fail_blessing BEFORE "+operation+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION fail_blessing()");ClaimBlessing(mentor);
            Check(Fenced(mentor) && mentor.Entity.HeavenBlessing==0 && BlessingSeconds(mentor)==0 && (retained?Retained(mentor,"actual_heavenblessing"):BlessingHours(child))==24
                && Scalar("SELECT count(*) FROM revora_mentor_blessing_claims WHERE mentor=@uid",mentor.Entity.UID)==0,"blessing debit, time and receipt roll back together: "+mode);
            Query("DROP TRIGGER fail_blessing ON "+table+";DROP FUNCTION fail_blessing()");DemonReset(mentor);People.LoadMentor(mentor);LoadBlessingImage(mentor);ClaimBlessing(mentor);
            Check(!Fenced(mentor) && mentor.Entity.HeavenBlessing==86400,"blessing claim recovers after an authoritative reload: "+mode);
        }
        var stale=DeparturePlayer(map,130);var source=DeparturePlayer(map,100);StoneLink(stale,source,100);Query("UPDATE entities SET heavenblessingtime=7200 WHERE uid=@uid","uid",(long)stale.Entity.UID);ClaimBlessing(stale);
        Check(Fenced(stale) && BlessingHours(source)==24 && BlessingSeconds(stale)==7200,"a newer durable blessing balance cannot be overwritten by a stale claim");
        var staleSave=DeparturePlayer(map,130);staleSave.Entity.HeavenBlessing=3600;Query("UPDATE entities SET heavenblessingtime=7200 WHERE uid=@uid","uid",(long)staleSave.Entity.UID);EntityTable.SaveEntity(staleSave);
        Check(Fenced(staleSave) && BlessingSeconds(staleSave)==7200,"a stale full-character save cannot overwrite a newer blessing grant");
        var grantParent=DeparturePlayer(map,130);var grantChild=DeparturePlayer(map,100);MentorLink(grantParent,grantChild,0);
        Query("CREATE FUNCTION fail_blessing() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RAISE EXCEPTION ''fixture grant failure''; END';CREATE TRIGGER fail_blessing BEFORE UPDATE OF heavenblessingtime ON entities FOR EACH ROW EXECUTE FUNCTION fail_blessing()");
        try { grantChild.AddBless(3*86400); } catch { }
        Check(Fenced(grantChild) && BlessingHours(grantChild)==0 && BlessingSeconds(grantChild)==0 && grantParent.PrizeHeavenBlessing==0,"a failed blessing grant cannot leave an earned mentor credit behind");
        Query("DROP TRIGGER fail_blessing ON entities;DROP FUNCTION fail_blessing()");
        var negative=DeparturePlayer(map,130);var negativeChild=DeparturePlayer(map,100);StoneLink(negative,negativeChild,250);
        Query("UPDATE apprentice SET actual_heavenblessing=-1 WHERE apprenticeid=@uid","uid",(long)negativeChild.Entity.UID);ClaimBlessing(negative);
        Check(Fenced(negative) && negative.Entity.HeavenBlessing==0 && BlessingHours(negativeChild)==-1,"invalid historical blessing balances fence the request and preserve repair evidence");
        var disconnected=DeparturePlayer(map,130);Kernel.GamePool.Remove(disconnected.Entity.UID);
        try { disconnected.AddBless(86400); } catch { }
        Check(disconnected.Entity.HeavenBlessing==0 && BlessingSeconds(disconnected)==0,"a disconnected session cannot grant blessing time");
        var duplicate=DeparturePlayer(map,130);var dupChild=DeparturePlayer(map,100);StoneLink(duplicate,dupChild,250);Query("INSERT INTO apprentice SELECT * FROM apprentice WHERE apprenticeid=@uid","uid",(long)dupChild.Entity.UID);ClaimBlessing(duplicate);
        Check(Fenced(duplicate) && AdmissionRows(dupChild)==2 && duplicate.Entity.HeavenBlessing==0,"ambiguous historical blessing rows are retained for repair");
    }
}
