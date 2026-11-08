using System;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using R=Revora.Game.Features.EliminationRules;
namespace Revora.Game.Features {
    internal static partial class EliminationStore {
        internal sealed class Reward {
            internal ExperienceRewardPlan Plan;internal ExperienceRewards.Effects Effects;
            internal ConquerItem Rune;internal ulong AutoHunt;internal ushort Study;
        }
        internal static Reward Claim(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            Owner(c,t,p);
            if(!Monitor.IsEntered(MentorExperience.Sync) || !CharacterProgression.Managed(p) || p.Spells==null || p.Entity.SubClasses==null)
                throw new InvalidOperationException("Elimination reward owner is not ready.");
            if(!Site(p,R.Georgia,R.GeorgiaX,R.GeorgiaY,4680) || p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Cursed))return null;
            string day=SummerHunt.Day(Clock());var run=Read(c,t,p,day);if(run==null || run.Stage!=6)return null;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_elimination_claims WHERE run=@run)","run",run.Id))if(Equals(q.ExecuteScalar(),true))return null;
            if(EliminationDenBindings.Next(c,t,run.Id)!=7)throw new InvalidOperationException("Elimination den victory proof missing.");
            int before=p.Entity.SubClasses.StudyPoints;if(before+R.StudyPoints>ushort.MaxValue)return null;
            using(var q=Q.Command(c,t,"SELECT studypoints FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))
                if(Convert.ToInt32(q.ExecuteScalar())!=before)throw new InvalidOperationException("Elimination Study Points require reload.");
            if(run.RuneReward && p.Inventory.Count>=40)return null;Bag(c,t,p,run.RuneReward);
            ulong experience=p.Entity.Level>=Config.LevelCap?0:ExperienceMath.Ball(p.Entity.Level);
            var reward=new Reward{Study=(ushort)(before+R.StudyPoints),AutoHunt=p.Entity.AutoHuntEXP,
                Plan=new ExperienceRewardPlan(CharacterProgression.Capture(p),experience,Config.LevelCap,Config.AttributePointsPerLevel,ReincarnationRecovery.Snapshot(p))};
            if(run.RuneReward){reward.Rune=Q.New(4030101,true);Q.Insert(c,t,p.Entity.UID,reward.Rune);}
            if(p.Entity.InAutoHunt)reward.AutoHunt=checked(reward.AutoHunt+experience/Math.Max(1u,Config.AutoHuntExperienceDivisor));
            CharacterProgression.Persist(c,t,p,reward.Plan.After);reward.Effects=ExperienceRewards.Persist(c,t,p,reward.Plan);
            ReincarnationRecovery.Consume(c,t,p,reward.Plan.Restored);
            if(Q.Execute(c,t,"UPDATE entities SET studypoints=@after WHERE uid=@uid AND studypoints=@before","after",(int)reward.Study,"uid",(long)p.Entity.UID,"before",before)!=1)
                throw new InvalidOperationException("Elimination Study Points not saved.");
            StudyRewardEvidence.Record(c,t,p,R.StudyPoints,"elimination:"+run.Id.ToString("N"));
            ProgressionEvidence.Complete(c,t,p.Entity.UID,11010,"elimination-v1",run.Id.ToString("N"),1);
            if(Q.Execute(c,t,"INSERT INTO revora_elimination_claims(run,experience,study,claimed_at,rune_uid) VALUES(@run,@exp,50,@now,@rune)",
                "run",run.Id,"exp",checked((long)experience),"now",Clock(),"rune",reward.Rune==null?(object)DBNull.Value:(long)reward.Rune.UID)!=1)throw new InvalidOperationException("Elimination claim not saved.");
            SameDay(day);return reward;
        }
        internal static void ApplyReward(GameState p,Reward reward) {
            if(reward==null)return;if(reward.Rune!=null)Q.Install(p,new uint[0],new[]{reward.Rune});ReincarnationRecovery.Committed(p,reward.Plan.Restored);MentorExperience.Apply(p,reward.Effects.MentorCredit);
            p.ApplyExperienceReward(reward.Plan,reward.Effects,reward.AutoHunt,true);p.Entity.SubClasses.StudyPoints=reward.Study;
            p.Send(new SubClassShowFull(0){ID=8,Study=reward.Study,StudyReceive=50}.ToArray());Q.Refresh(p);
        }
    }
}
