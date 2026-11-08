using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Revora;
using Revora.Database;
using Revora.Game;
using Revora.Interfaces;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static void VerifyMastery(Map map)
    {
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL); CREATE TABLE profs (LIKE public.profs INCLUDING ALL)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        Check(ConquerItemInformation.BaseInformations.ContainsKey(511004) && ConquerItemInformation.BaseInformations.ContainsKey(511439)
            && !ConquerItemInformation.BaseInformations.Keys.Any(id=>id/1000==550) && ItemHandler.IsScythe(511004) && ItemHandler.IsScythe(511439),
            "loaded equipment catalog and equip classification identify Scythe family 511, not the unused 550 name row");
        var p=ProgressPlayer(map,"");p.Proficiencies=new SafeDictionary<ushort,IProf>();
        Check(!p.Fake && p.Account.State==AccountTable.AccountState.Player,"mastery uses an ordinary authenticated player");
        var identities=new[]{
            new[]{0,10321},new[]{420,10322},new[]{410,10323},new[]{460,10324},new[]{540,10325},new[]{580,10326},
            new[]{560,10327},new[]{490,10328},new[]{561,10329},new[]{510,10330},new[]{530,10331},new[]{430,10332},
            new[]{450,10401},new[]{480,10402},new[]{481,10403},new[]{440,10404},new[]{500,10405},new[]{610,10406},
            new[]{601,10407},new[]{421,10408},new[]{900,11132},new[]{611,11210},new[]{612,11211},new[]{511,11212},new[]{613,11213},
            new[]{614,11214},new[]{616,11215},new[]{617,11221},new[]{620,11222},new[]{622,11223},new[]{624,11302},
            new[]{626,11303},new[]{671,11322},new[]{670,11323}};
        foreach(var row in identities) {
            var prof=new Proficiency(true){ID=(ushort)row[0],Level=11,Experience=DataHolder.ProficiencyLevelExperience(11)-1};
            p.AddProficiency(prof);EvidenceRefresh(p);
            Check(!HasAchievement(p,row[1]),"level-eleven proficiency cannot earn mastery: "+row[0]);
            p.IncreaseProficiencyExperience(1,prof.ID);SkillTable.SaveProficiencies(p);EvidenceRefresh(p);
            Check(prof.Level==12 && HasAchievement(p,row[1]),"production proficiency progression earns the mapped mastery: "+row[0]);
        }
        Check(p.Entity.MyAchievement.CompletedCount==34 && Scalar("SELECT count(*) FROM profs WHERE entityid=@uid AND level=12",p.Entity.UID)==34,"all thirty-four real proficiency records and distinct achievements persist");
        p.Proficiencies.Clear();SkillTable.LoadProficiencies(p);
        Check(p.Proficiencies.Count==34 && p.Proficiencies.Values.All(prof=>prof.Level==12),"production proficiency loader restores each mapped weapon level");
        foreach(var prof in p.Proficiencies.Values){prof.Level=0;prof.PreviousLevel=12;}
        p.Entity.MyAchievement=new Achievement(p.Entity);p.Entity.MyAchievement.Load("");EvidenceRefresh(p);
        Check(identities.All(row=>HasAchievement(p,row[1])),"historical mastery survives proficiency reset and evidence reload");
        Parallel.For(0,12,i=>EvidenceRefresh(p));
        Check(Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid",p.Entity.UID)==34,"concurrent reconciliation cannot duplicate mastery evidence");
        var invalid=ProgressPlayer(map,"");invalid.Proficiencies=new SafeDictionary<ushort,IProf>();
        invalid.Proficiencies.Add(420,new Proficiency(true){ID=410,Level=12});
        invalid.Proficiencies.Add(410,new Proficiency(true){ID=410,Level=255});
        invalid.Proficiencies.Add(550,new Proficiency(true){ID=550,Level=12});
        invalid.Proficiencies.Add(490,new Proficiency(true){ID=490,Level=0,PreviousLevel=12});EvidenceRefresh(invalid);
        Check(invalid.Entity.MyAchievement.CompletedCount==0,"mismatched IDs, corrupt levels, old levels and unused Scythe alias 550 cannot prove mastery");
        invalid.Proficiencies[410].Level=12;
        Query("ALTER TABLE revora_achievement_evidence ADD CONSTRAINT mastery_failure CHECK(achievement_id<>10323) NOT VALID");
        bool failed=false;try{EvidenceRefresh(invalid);}catch(PostgresException){failed=true;}
        Check(failed && invalid.Entity.MyAchievement.CompletedCount==0,"failed mastery transaction cannot install client completion");
        Query("ALTER TABLE revora_achievement_evidence DROP CONSTRAINT mastery_failure");EvidenceRefresh(invalid);
        Check(HasAchievement(invalid,10323) && invalid.Entity.MyAchievement.CompletedCount==1,"mastery retries once after persistence recovery");
    }
}
