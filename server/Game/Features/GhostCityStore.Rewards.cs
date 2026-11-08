using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using W=Revora.Game.Features.GhostCityScenes;
namespace Revora.Game.Features
{
    internal static partial class GhostCityStore
    {
        internal static bool RescueChild(GameState p,W.Scene scene,int child,Func<bool> context){
            Ready(p,context);if(child<0 || child>4 || !scene.Dark || !AtActor(p,scene,child+2))return false;
            lock(MentorExperience.Sync){
                ExperienceRewardPlan plan;ExperienceRewards.Effects effects;ulong auto;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);
                    if(run.Stage!=8 || run.Type!=721714 || !Material(c,t,p,run.Item,721714) || !Visit(c,t,p,scene,false))return false;
                    if(Q.Execute(c,t,"INSERT INTO revora_ghost_children(uid,life,child) VALUES(@uid,@life,@child) ON CONFLICT DO NOTHING","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"child",child)!=1)return false;
                    ulong xp=p.Entity.Level>=Config.LevelCap?0:p.ExpBall/6;
                    plan=new ExperienceRewardPlan(CharacterProgression.Capture(p),xp,Config.LevelCap,Config.AttributePointsPerLevel,ReincarnationRecovery.Snapshot(p));
                    CharacterProgression.Persist(c,t,p,plan.After);effects=ExperienceRewards.Persist(c,t,p,plan);ReincarnationRecovery.Consume(c,t,p,plan.Restored);
                    auto=p.Entity.InAutoHunt?checked(p.Entity.AutoHuntEXP+xp/Math.Max(1u,Config.AutoHuntExperienceDivisor)):p.Entity.AutoHuntEXP;
                    Ready(p,()=>context() && AtActor(p,scene,child+2));t.Commit();
                }}ReincarnationRecovery.Committed(p,plan.Restored);MentorExperience.Apply(p,effects.MentorCredit);p.ApplyExperienceReward(plan,effects,auto,true);return true;
            }
        }
        internal static bool FeedMoMo(GameState p,W.Scene scene,Func<bool> context){
            Ready(p,context);if(!scene.Dark || !AtActor(p,scene,0))return false;
            uint[] removed;ulong silver;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);
                if(run.Stage!=8 || run.Type!=721714 || !Material(c,t,p,run.Item,721714)
                    || !Material(c,t,p,run.Charm,721713) || !Visit(c,t,p,scene,false))return false;
                using(var q=Q.Command(c,t,"SELECT kills FROM revora_ghost_combat WHERE uid=@uid AND life=@life FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))if(Convert.ToInt32(q.ExecuteScalar())!=102)return false;
                removed=new[]{run.Item,run.Charm};Q.Consume(c,t,p,run.Item,721714);Q.Consume(c,t,p,run.Charm,721713);
                SilverBalance.AssertSaved(c,t,p);silver=checked(p.Entity.Money+100);if(silver>Entity.MaximumSilver)return false;SilverBalance.Persist(c,t,p,silver);
                run.Item=run.Type=run.Charm=0;run.Stage=9;Save(c,t,p,run,8);
                Ready(p,()=>context() && AtActor(p,scene,0));t.Commit();
            }}scene.Completed=true;Q.Install(p,removed,new ConquerItem[0]);SilverBalance.ApplyCommitted(p,silver);return true;
        }
        internal static bool ReportRescue(GameState p,Func<bool> context){
            Ready(p,context);if(p.Entity.MapID!=1002 || W.HasOwner(p.Entity.UID))return false;
            lock(MentorExperience.Sync){
                ExperienceRewardPlan plan;ExperienceRewards.Effects effects;ulong silver,auto;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=9)return false;
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_ghost_visits WHERE uid=@uid AND active)","uid",(long)p.Entity.UID))if(Equals(q.ExecuteScalar(),true))return false;
                    SilverBalance.AssertSaved(c,t,p);silver=checked(p.Entity.Money+6000);if(silver>Entity.MaximumSilver)return false;SilverBalance.Persist(c,t,p,silver);
                    ulong xp=p.Entity.Level>=Config.LevelCap?0:p.ExpBall;
                    plan=new ExperienceRewardPlan(CharacterProgression.Capture(p),xp,Config.LevelCap,Config.AttributePointsPerLevel,ReincarnationRecovery.Snapshot(p));
                    CharacterProgression.Persist(c,t,p,plan.After);effects=ExperienceRewards.Persist(c,t,p,plan);ReincarnationRecovery.Consume(c,t,p,plan.Restored);
                    auto=p.Entity.InAutoHunt?checked(p.Entity.AutoHuntEXP+xp/Math.Max(1u,Config.AutoHuntExperienceDivisor)):p.Entity.AutoHuntEXP;
                    run.Stage=10;Save(c,t,p,run,9);Ready(p,context);t.Commit();
                }}SilverBalance.ApplyCommitted(p,silver);ReincarnationRecovery.Committed(p,plan.Restored);
                MentorExperience.Apply(p,effects.MentorCredit);p.ApplyExperienceReward(plan,effects,auto,true);return true;
            }
        }
        internal static bool Claim(GameState p,Func<bool> context){
            Ready(p,context);if(p.Entity.MapID!=1002 || W.HasOwner(p.Entity.UID))return false;ConquerItem[] rewards;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=10 || p.Inventory.Count>37)return false;
                ConquerItemBaseInformation soul;if(!ConquerItemInformation.BaseInformations.TryGetValue(run.Soul,out soul) || run.Soul<800000 || run.Soul>=900000 || soul.PurificationLevel!=7)throw new InvalidOperationException("Ghost City soul reward changed.");
                rewards=new[]{Q.New(728246,true),Q.New(723911,true),Q.New(run.Soul,true)};foreach(var item in rewards)Q.Insert(c,t,p.Entity.UID,item);
                if(Q.Execute(c,t,"UPDATE revora_ghost_city SET stage=11,rewards=@rewards WHERE uid=@uid AND life=@life AND stage=10","rewards",rewards.Select(i=>(long)i.UID).ToArray(),"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)throw new InvalidOperationException("Ghost City final claim changed.");
                Ready(p,context);ProgressionEvidence.RecordEquipmentQuestAggregate(c,t,p.Entity.UID);t.Commit();
            }}Q.Install(p,new uint[0],rewards);return true;
        }
    }
}
