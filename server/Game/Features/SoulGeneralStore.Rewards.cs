using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class SoulGeneralStore
    {
        internal static bool HandInPaper(GameState p,Func<bool> context){
            Ready(p,context);uint itemUid;
            lock(MentorExperience.Sync){
                ExperienceRewardPlan plan;ExperienceRewards.Effects effects;ulong silver,auto;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=10 || !Material(c,t,p,run,721706))return false;itemUid=run.Item;
                    Q.Consume(c,t,p,itemUid,721706);SilverBalance.AssertSaved(c,t,p);silver=checked(p.Entity.Money+4000);if(silver>Entity.MaximumSilver)return false;SilverBalance.Persist(c,t,p,silver);
                    ulong xp=p.Entity.Level>=Config.LevelCap?0:p.ExpBall/3*2+(p.ExpBall%3)*2/3;
                    plan=new ExperienceRewardPlan(CharacterProgression.Capture(p),xp,Config.LevelCap,Config.AttributePointsPerLevel,ReincarnationRecovery.Snapshot(p));
                    CharacterProgression.Persist(c,t,p,plan.After);effects=ExperienceRewards.Persist(c,t,p,plan);ReincarnationRecovery.Consume(c,t,p,plan.Restored);
                    auto=p.Entity.InAutoHunt?checked(p.Entity.AutoHuntEXP+xp/Math.Max(1u,Config.AutoHuntExperienceDivisor)):p.Entity.AutoHuntEXP;
                    run.Stage=11;run.Item=run.Type=0;Save(c,t,p,run,10);
                    Ready(p,context);t.Commit();
                }}
                Q.Install(p,new[]{itemUid},new ConquerItem[0]);SilverBalance.ApplyCommitted(p,silver);ReincarnationRecovery.Committed(p,plan.Restored);
                MentorExperience.Apply(p,effects.MentorCredit);p.ApplyExperienceReward(plan,effects,auto,true);return true;
            }
        }
        internal static bool Claim(GameState p,Func<bool> context){
            Ready(p,context);ConquerItem[] rewards;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=11 || p.Inventory.Count>38)return false;
                ConquerItemBaseInformation soul;if(!ConquerItemInformation.BaseInformations.TryGetValue(run.Soul,out soul) || run.Soul<800000 || run.Soul>=900000 || soul.PurificationLevel!=7)throw new InvalidOperationException("Soul General soul reward changed.");
                rewards=new[]{Q.New(728244,true),Q.New(run.Soul,true)};foreach(var item in rewards)Q.Insert(c,t,p.Entity.UID,item);
                if(Q.Execute(c,t,"UPDATE revora_soul_general SET stage=12,rewards=@rewards WHERE uid=@uid AND life=@life AND stage=11","rewards",rewards.Select(i=>(long)i.UID).ToArray(),"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)throw new InvalidOperationException("Soul General final claim changed.");
                Ready(p,context);ProgressionEvidence.RecordEquipmentQuestAggregate(c,t,p.Entity.UID);t.Commit();
            }}Q.Install(p,new uint[0],rewards);return true;
        }
    }
}
