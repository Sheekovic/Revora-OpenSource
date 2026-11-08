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
    static void StoneLink(GameState mentor,GameState child,long points)
    {
        MentorLink(mentor,child,600);
        Query("UPDATE apprentice SET actual_plus=@points,total_plus=@total,actual_heavenblessing=24,total_heavenblessing=48 WHERE apprenticeid=@uid",
            "points",points,"total",points+100,"uid",(long)child.Entity.UID);
        People.LoadMentor(mentor);People.LoadMentor(child);
    }
    static void ClaimStones(GameState p)
    { var frame=MentorClaimPacket();Writer.WriteUInt32(4,4,frame);MentorPrize.Process(frame,p); }
    static long StonePoints(GameState child)
    { return Scalar("SELECT actual_plus FROM apprentice WHERE apprenticeid=@uid",child.Entity.UID); }
    static int StoneCount(GameState p) { return p.Inventory.Objects.Count(i=>i.ID==730001); }
    static void VerifyMentorStones(Map map)
    {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL);CREATE TABLE apprentice (LIKE public.apprentice INCLUDING ALL);CREATE TABLE itemadding (LIKE public.itemadding INCLUDING ALL)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);Call(EvidenceType,null,"Initialize");Call(Admissions,null,"Initialize");ConquerItem.ItemUID=new Counter(4900000);
        var mentor=DeparturePlayer(map,130);var child=DeparturePlayer(map,100);StoneLink(mentor,child,250);
        Check(!mentor.Fake && mentor.Account.State==AccountTable.AccountState.Player,"mentor stone claims use an ordinary authenticated character");
        ClaimStones(mentor);
        Check(!Fenced(mentor) && StoneCount(mentor)==2 && StonePoints(child)==50 && mentor.PrizePlusStone==50 && child.AsApprentice.Actual_Plus==50,
            "two stones cost exactly two hundred points and immediately refresh both contribution views");
        Check(mentor.Inventory.Objects.All(i=>i.Plus==1 && !i.Bound && i.StackSize==1)
            && mentor.Inventory.Objects.All(i=>ConquerItemTable.LoadItem(i.UID).Plus==1),"granted stones preserve their real plus, binding and identity through production reload");
        Check(MentorBalance(child)==600 && Scalar("SELECT actual_heavenblessing FROM apprentice WHERE apprenticeid=@uid",child.Entity.UID)==24
            && Scalar("SELECT total_plus FROM apprentice WHERE apprenticeid=@uid",child.Entity.UID)==350,"stone claims preserve EXP, blessing and lifetime contributions");
        ClaimStones(mentor);Check(StoneCount(mentor)==2 && StonePoints(child)==50,"a sub-hundred remainder cannot be claimed or discarded");
        People.SaveApprenticeInfo(new Revora.Game.ConquerStructures.Society.Apprentice{ID=child.Entity.UID,Actual_Plus=250,Total_Plus=350,Actual_HeavenBlessing=24,Total_HeavenBlessing=48});
        Check(StonePoints(child)==50,"legacy blessing snapshots cannot restore already claimed plus points");
        People.LoadMentor(mentor);People.LoadMentor(child);
        Check(mentor.PrizePlusStone==50 && child.AsApprentice.Actual_Plus==50,"production relationship reload preserves the exact claim remainder");
        var full=DeparturePlayer(map,130);var fullChild=DeparturePlayer(map,100);StoneLink(full,fullChild,250);
        var filler=Enumerable.Range(0,40).Select(i=>Pack(full,1088001,1)).ToArray();ClaimStones(full);
        Check(StonePoints(fullChild)==250 && StoneCount(full)==0 && !Fenced(full),"a full inventory cannot consume mentor points");
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)filler[0].UID);full.Inventory.Remove(filler[0].UID,Enums.ItemUse.None,false);ClaimStones(full);
        Check(full.Inventory.Count==40 && StoneCount(full)==1 && StonePoints(fullChild)==150,"one free slot grants one stone and retains every undelivered point");
        var parallel=DeparturePlayer(map,130);var parallelChild=DeparturePlayer(map,100);StoneLink(parallel,parallelChild,300);
        Task.WaitAll(Enumerable.Range(0,8).Select(i=>Task.Run(()=>ClaimStones(parallel))).ToArray());
        Check(!Fenced(parallel) && StoneCount(parallel)==3 && StonePoints(parallelChild)==0
            && Scalar("SELECT sum(points) FROM revora_mentor_stone_claims WHERE mentor=@uid",parallel.Entity.UID)==300,"parallel native claims cannot double spend contribution points");
        var retired=DeparturePlayer(map,130);var oldChild=DeparturePlayer(map,100);StoneLink(retired,oldChild,50);Depart(retired,oldChild,true);
        var current=DeparturePlayer(map,100);StoneLink(retired,current,50);ClaimStones(retired);
        Check(StoneCount(retired)==1 && StonePoints(current)==0 && Retained(retired,"actual_plus")==0 && retired.PrizePlusStone==0,
            "active and retained contribution remainders combine into one exact stone claim");
        Check(Retained(retired,"total_plus")==150 && Retained(retired,"actual_experience")==600 && Retained(retired,"actual_heavenblessing")==24,
            "retained stone claims preserve lifetime history and the other reward categories");
        var fragments=DeparturePlayer(map,130);
        Query(@"INSERT INTO revora_mentor_departures(id,mentor,apprentice,actor,reason,left_at,can_join_at,original_row,actual_experience,total_experience,actual_plus,total_plus,actual_heavenblessing,total_heavenblessing,original_fee,fee_gold)
SELECT md5(@uid::text||':'||n::text)::uuid,@uid,91000000+n,@uid,'expel',@now,@now+interval '3 days','{}'::jsonb,0,0,1,1,0,0,0,0 FROM generate_series(1,100)n",
            "uid",(long)fragments.Entity.UID,"now",DateTime.UtcNow);
        ClaimStones(fragments);
        Check(StoneCount(fragments)==1 && Retained(fragments,"actual_plus")==0,"one hundred fragmented retained points cannot starve behind a too-small claim batch");
        var foreign=DeparturePlayer(map,130);var forged=MentorClaimPacket();Writer.WriteUInt32(4,4,forged);Writer.WriteUInt32(mentor.Entity.UID,8,forged);Writer.WriteUInt16(ushort.MaxValue,34,forged);MentorPrize.Process(forged,foreign);
        Check(StoneCount(foreign)==0 && StonePoints(child)==50,"forged claimant identity and displayed balance cannot spend another mentor's points");
        VerifyMentorStoneFailures(map);
    }
    static void VerifyMentorStoneFailures(Map map)
    {
        foreach(string mode in new[]{"debit","retained-debit","first-item","second-item","receipt","silent-item"})
        {
            var mentor=DeparturePlayer(map,130);var child=DeparturePlayer(map,100);StoneLink(mentor,child,250);
            bool retained=mode=="retained-debit";if(retained)Depart(mentor,child,true);
            string table=mode=="debit"?"apprentice":retained?"revora_mentor_departures":mode=="receipt"?"revora_mentor_stone_claims":"items";
            string operation=mode.EndsWith("debit")?"UPDATE OF actual_plus":"INSERT";
            string effect=mode=="silent-item"?"RETURN NULL;":"RAISE EXCEPTION ''fixture stone failure'';";
            string condition=mode=="second-item"?"IF EXISTS(SELECT 1 FROM items WHERE entityid="+mentor.Entity.UID+" AND id=730001) THEN "+effect+" END IF; RETURN NEW;":effect;
            Query("CREATE FUNCTION fail_stones() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN "+condition+" END';CREATE TRIGGER fail_stones BEFORE "+operation+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION fail_stones()");
            ClaimStones(mentor);
            Check(Fenced(mentor) && StoneCount(mentor)==0 && (retained?Retained(mentor,"actual_plus"):StonePoints(child))==250
                && Scalar("SELECT count(*) FROM revora_mentor_stone_claims WHERE mentor=@uid",mentor.Entity.UID)==0,"stone debit and all item deliveries roll back together: "+mode);
            Query("DROP TRIGGER fail_stones ON "+table+";DROP FUNCTION fail_stones()");DemonReset(mentor);People.LoadMentor(mentor);ClaimStones(mentor);
            Check(!Fenced(mentor) && StoneCount(mentor)==2 && (retained?Retained(mentor,"actual_plus"):StonePoints(child))==50,"stone claim recovers safely after reload: "+mode);
        }
        var stale=DeparturePlayer(map,130);var source=DeparturePlayer(map,100);StoneLink(stale,source,250);var hidden=Pack(stale,1088001,1);stale.Inventory.Remove(hidden.UID,Enums.ItemUse.None,false);ClaimStones(stale);
        Check(Fenced(stale) && StonePoints(source)==250 && StoneCount(stale)==0,"stale inventory counts cannot overfill or charge the wrong delivery state");
        var duplicate=DeparturePlayer(map,130);var dupChild=DeparturePlayer(map,100);StoneLink(duplicate,dupChild,250);Query("INSERT INTO apprentice SELECT * FROM apprentice WHERE apprenticeid=@uid","uid",(long)dupChild.Entity.UID);ClaimStones(duplicate);
        Check(Fenced(duplicate) && AdmissionRows(dupChild)==2 && StoneCount(duplicate)==0,"ambiguous legacy contributions are retained for repair instead of doubled into rewards");
    }
}
