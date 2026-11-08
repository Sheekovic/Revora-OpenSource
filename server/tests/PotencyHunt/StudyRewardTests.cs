using Revora.Client;
using Revora.Database;

internal static partial class PotencyHuntTests
{
    static long StudyEvidence(GameState p) { return Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10319",p.Entity.UID); }
    static void VerifyStudyAward(GameState p,string prefix)
    {
        p.Entity.MyAchievement.Send();
        Check(!Fenced(p) && HasAchievement(p,10319) && StudyEvidence(p)==1 && p.Entity.SubClasses.StudyPoints>0
            && EpkCount("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10319 AND source='study-reward-v1' AND source_key LIKE @prefix",
                "uid",(long)p.Entity.UID,"prefix",prefix+":%")==1,"committed "+prefix+" Study Points produce one saved and visible first-award achievement");
    }
    static void VerifyStudyRollback(GameState p)
    {
        Check(StudyEvidence(p)==0 && Scalar("SELECT COALESCE(studypoints,0) FROM entities WHERE uid=@uid",p.Entity.UID)==p.Entity.SubClasses.StudyPoints,
            "suppressed first-study evidence rolls back the point credit with the whole reward");
    }
}
