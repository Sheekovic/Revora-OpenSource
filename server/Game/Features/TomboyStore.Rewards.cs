using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class TomboyStore
    {
        // Revora question: which clicked stone joins Maple to form Rune Jade?
        // The coordinator presents Ocean (1), Leaf (2), and Maple (3).
        internal static bool Answer(GameState p,byte answer,Func<bool> context){
            Ready(p,context);if(answer!=2 || p.Entity.MapID!=1000)return false;
            lock(MentorExperience.Sync){
                ExperienceRewardPlan plan;ExperienceRewards.Effects effects;ulong silver,auto;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=4)return false;
                    SilverBalance.AssertSaved(c,t,p);silver=checked(p.Entity.Money+3000);if(silver>Entity.MaximumSilver)return false;SilverBalance.Persist(c,t,p,silver);
                    ulong xp=p.Entity.Level>=Config.LevelCap?0:p.ExpBall/2;
                    plan=new ExperienceRewardPlan(CharacterProgression.Capture(p),xp,Config.LevelCap,Config.AttributePointsPerLevel,ReincarnationRecovery.Snapshot(p));
                    CharacterProgression.Persist(c,t,p,plan.After);effects=ExperienceRewards.Persist(c,t,p,plan);ReincarnationRecovery.Consume(c,t,p,plan.Restored);
                    auto=p.Entity.InAutoHunt?checked(p.Entity.AutoHuntEXP+xp/Math.Max(1u,Config.AutoHuntExperienceDivisor)):p.Entity.AutoHuntEXP;
                    Save(c,t,p,4,5);Ready(p,context);t.Commit();
                }}SilverBalance.ApplyCommitted(p,silver);ReincarnationRecovery.Committed(p,plan.Restored);
                MentorExperience.Apply(p,effects.MentorCredit);p.ApplyExperienceReward(plan,effects,auto,true);return true;
            }
        }
        internal static bool Claim(GameState p,Func<bool> context){
            Ready(p,context);if(p.Entity.MapID!=1002)return false;ConquerItem[] rewards;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=5 || p.Inventory.Count>37)return false;
                ConquerItemBaseInformation soul;if(!ConquerItemInformation.BaseInformations.TryGetValue(run.Soul,out soul) || run.Soul<800000 || run.Soul>=900000 || soul.PurificationLevel!=7)throw new InvalidOperationException("Tomboy soul reward changed.");
                rewards=new[]{Q.New(728241,true),Q.New(723911,true),Q.New(run.Soul,true)};foreach(var item in rewards)Q.Insert(c,t,p.Entity.UID,item);
                if(Q.Execute(c,t,"UPDATE revora_tomboy SET stage=6,rewards=@rewards WHERE uid=@uid AND life=@life AND stage=5","rewards",rewards.Select(i=>(long)i.UID).ToArray(),"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)throw new InvalidOperationException("Tomboy final claim changed.");
                Ready(p,context);ProgressionEvidence.RecordEquipmentQuestAggregate(c,t,p.Entity.UID);t.Commit();
            }}Q.Install(p,new uint[0],rewards);return true;
        }
    }
}
