using System;
using System.Threading;
using Npgsql;
using Revora.Client;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class StudyRewardEvidence
    {
        // Called only after a checked Study Point credit, inside its reward transaction.
        internal static void Record(NpgsqlConnection c,NpgsqlTransaction t,GameState p,int points,string key)
        {
            if(points==0)return;
            if(points<0 || !Q.Player(p,false) || !Monitor.IsEntered(p.ItemSyncRoot) || t==null || t.Connection!=c)
                throw new InvalidOperationException("Study evidence requires its owned reward transaction.");
            int after=checked(p.Entity.SubClasses.StudyPoints+points);
            using(var q=Q.Command(c,t,"SELECT studypoints FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID)) {
                object saved=q.ExecuteScalar();
                if(after>ushort.MaxValue || saved==null || saved==DBNull.Value || Convert.ToInt32(saved)!=after)
                    throw new InvalidOperationException("Study reward was not credited.");
            }
            ProgressionEvidence.Complete(c,t,p.Entity.UID,10319,"study-reward-v1",key,1);
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10319)","uid",(long)p.Entity.UID))
                if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Study reward achievement was not saved.");
        }
    }
}
