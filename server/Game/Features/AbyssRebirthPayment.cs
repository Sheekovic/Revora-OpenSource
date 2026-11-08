using System;
using System.Threading;
using Npgsql;
using Revora.Client;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.AbyssTrialStore;
using R=Revora.Game.Features.AbyssTrialRules;

namespace Revora.Game.Features
{
    // Caller holds the character lock and owns the same transaction as rebirth.
    internal static class AbyssRebirthPayment
    {
        internal static S.Run Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            if(!AbyssTrialQuest.Enabled)return null;
            if(t==null || !Q.Player(p) || !Monitor.IsEntered(p.ItemSyncRoot) || !R.Eligible(p.Entity.Reborn,p.Entity.Level))
                throw new InvalidOperationException("Abyss rebirth requires its owner transaction.");
            var r=S.Read(c,t,p.Entity.UID,true);
            if(r!=null && (r.Consumed || r.FirstClass!=p.Entity.FirstRebornClass || r.FirstLevel!=p.Entity.FirstRebornLevel))
                throw new InvalidOperationException("Abyss rebirth history changed.");
            return r;
        }
        internal static bool Pending(NpgsqlConnection c,NpgsqlTransaction t,S.Run r) {
            if(r==null)return false;
            if(S.Active(c,t,r)!=null)return true;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_abyss_rewards WHERE run=@run AND claimed_at IS NULL)","run",r.Id))
                return Equals(q.ExecuteScalar(),true);
        }
        internal static bool Earned(NpgsqlConnection c,NpgsqlTransaction t,S.Run r) {
            if(r==null || r.Consumed || r.Progress.Step!=R.Stage.Complete)return false;
            // A terminal stage or achievement bit alone is insufficient. Require
            // the world participant's owned final-form death and completion proof.
            using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_abyss_completions p
JOIN revora_abyss_encounters e ON e.id=p.encounter AND e.run=p.run
JOIN revora_abyss_operations o ON o.id=e.id AND o.run=p.run
WHERE p.run=@run AND p.uid=@uid AND e.family=3646 AND e.defeated_at IS NOT NULL
AND o.kind='summon' AND o.product=3646)","run",r.Id,"uid",(long)r.Owner))return Equals(q.ExecuteScalar(),true);
        }
        internal static void Consume(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Guid? quest,int version,bool bound) {
            var r=Read(c,t,p);
            if(Pending(c,t,r))throw new InvalidOperationException("Abyss encounters or spoils remain outstanding.");
            if(quest.HasValue) {
                if(r==null || r.Id!=quest.Value || r.Version!=version || r.Bound!=bound || !Earned(c,t,r))
                    throw new InvalidOperationException("Earned Abyss rite changed.");
                if(Q.Execute(c,t,@"UPDATE revora_abyss_trials SET consumed_at=now(),selected=false,version=version+1
WHERE id=@run AND uid=@uid AND version=@version AND stage=5 AND consumed_at IS NULL",
                    "run",r.Id,"uid",(long)p.Entity.UID,"version",version)!=1)
                    throw new InvalidOperationException("Abyss rite was not consumed.");
            } else if(r!=null && r.Selected) {
                // A token may bypass unfinished trials, but cannot strand a paid
                // actor or pending item. Preserve the history and restore Cup UI.
                if(Q.Execute(c,t,"UPDATE revora_abyss_trials SET selected=false,version=version+1 WHERE id=@run AND version=@version AND selected",
                    "run",r.Id,"version",r.Version)!=1)throw new InvalidOperationException("Abyss route was not retired.");
            }
        }
    }
}
