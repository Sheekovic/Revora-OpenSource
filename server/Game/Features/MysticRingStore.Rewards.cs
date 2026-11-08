using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class MysticRingStore
    {
        internal static bool UseRing(GameState p,uint itemUid,Func<bool> context){
            Ready(p,context);Guid token=Guid.Empty;bool committed=false;
            try{lock(MentorExperience.Sync){
                ExperienceRewardPlan plan;ExperienceRewards.Effects effects;ulong silver,auto;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=8 || run.Item!=itemUid || !Material(c,t,p,run,721708))return false;
                    // Reserve appearance before consuming the ring. Context must be a
                    // non-database world/NPC check; this transaction owns the saved row.
                    token=QuestAppearance.Apply(p,QuestAppearance.MouseMesh,context);if(token==Guid.Empty)return false;
                    Q.Consume(c,t,p,itemUid,721708);SilverBalance.AssertSaved(c,t,p);silver=checked(p.Entity.Money+1500);if(silver>Entity.MaximumSilver)return false;SilverBalance.Persist(c,t,p,silver);
                    ulong xp=p.Entity.Level>=Config.LevelCap?0:p.ExpBall/3;
                    plan=new ExperienceRewardPlan(CharacterProgression.Capture(p),xp,Config.LevelCap,Config.AttributePointsPerLevel,ReincarnationRecovery.Snapshot(p));
                    CharacterProgression.Persist(c,t,p,plan.After);effects=ExperienceRewards.Persist(c,t,p,plan);ReincarnationRecovery.Consume(c,t,p,plan.Restored);
                    auto=p.Entity.InAutoHunt?checked(p.Entity.AutoHuntEXP+xp/Math.Max(1u,Config.AutoHuntExperienceDivisor)):p.Entity.AutoHuntEXP;
                    run.Stage=9;run.Item=run.Type=0;Save(c,t,p,run,8);
                    Ready(p,()=>context() && QuestAppearance.Owns(p,token,QuestAppearance.MouseMesh));t.Commit();committed=true;
                }}
                Q.Install(p,new[]{itemUid},new ConquerItem[0]);SilverBalance.ApplyCommitted(p,silver);ReincarnationRecovery.Committed(p,plan.Restored);
                MentorExperience.Apply(p,effects.MentorCredit);p.ApplyExperienceReward(plan,effects,auto,true);return true;
            }}finally{if(!committed && token!=Guid.Empty)QuestAppearance.Clear(p,token,QuestAppearance.MouseMesh);}
        }
        internal static bool Claim(GameState p,Func<bool> context){
            Ready(p,context);ConquerItem[] rewards;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=9 || p.Inventory.Count>38 || FixedOutputPacks.MysticHeadgearFor(p.Entity.Class)==0)return false;
                ConquerItemBaseInformation soul;if(!ConquerItemInformation.BaseInformations.TryGetValue(run.Soul,out soul) || run.Soul<800000 || run.Soul>=900000 || soul.PurificationLevel!=7)throw new InvalidOperationException("Mystic soul reward changed.");
                rewards=new[]{Q.New(728240,true),Q.New(run.Soul,true)};foreach(var item in rewards)Q.Insert(c,t,p.Entity.UID,item);
                if(Q.Execute(c,t,"UPDATE revora_mystic_ring SET stage=10,rewards=@rewards WHERE uid=@uid AND life=@life AND stage=9","rewards",rewards.Select(i=>(long)i.UID).ToArray(),"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)throw new InvalidOperationException("Mystic final claim changed.");
                Ready(p,context);ProgressionEvidence.RecordEquipmentQuestAggregate(c,t,p.Entity.UID);t.Commit();
            }}Q.Install(p,new uint[0],rewards);return true;
        }
    }
}
