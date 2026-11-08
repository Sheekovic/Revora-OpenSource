using System;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class PowerBookStore
    {
        internal static uint ArmorFor(byte job)
        {
            uint id=0;byte required=0;
            if(job>=10 && job<=15){id=130109;required=15;}
            else if(job>=20 && job<=25){id=131109;required=25;}
            else if(job>=40 && job<=45){id=133109;required=45;}
            else if(job>=50 && job<=55){id=135109;required=50;}
            else if(job>=60 && job<=65){id=136109;required=60;}
            else if(job>=70 && job<=75){id=139109;required=70;}
            else if(job>=80 && job<=85){id=138109;required=80;}
            else if(job==100 || job==101 || job>=132 && job<=135 || job>=142 && job<=145){id=134109;required=190;}
            else if(job>=160 && job<=165){id=101109;required=160;}
            if(id==0)return 0;
            ConquerItemBaseInformation info;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(id,out info) || info.Level!=120 || info.Class!=required || id%10!=9
                || !ConquerItemInformation.BaseInformations.ContainsKey(723911))throw new InvalidOperationException("Power Book armor/EXP catalog changed.");
            return id;
        }
        internal static bool HandIn(GameState p,Func<bool> context)
        {
            Ready(p,context);
            lock(MentorExperience.Sync){
                ExperienceRewardPlan plan;ExperienceRewards.Effects effects;ulong silver,auto;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);if(Read(c,t,p).Stage!=12)return false;
                    SilverBalance.AssertSaved(c,t,p);silver=checked(p.Entity.Money+6000);if(silver>Entity.MaximumSilver)return false;
                    ulong xp=p.Entity.Level>=Config.LevelCap?0:p.ExpBall;
                    plan=new ExperienceRewardPlan(CharacterProgression.Capture(p),xp,Config.LevelCap,Config.AttributePointsPerLevel,ReincarnationRecovery.Snapshot(p));
                    CharacterProgression.Persist(c,t,p,plan.After);effects=ExperienceRewards.Persist(c,t,p,plan);ReincarnationRecovery.Consume(c,t,p,plan.Restored);
                    auto=p.Entity.InAutoHunt?checked(p.Entity.AutoHuntEXP+xp/Math.Max(1u,Config.AutoHuntExperienceDivisor)):p.Entity.AutoHuntEXP;
                    SilverBalance.Persist(c,t,p,silver);
                    if(Q.Execute(c,t,"UPDATE revora_power_book SET stage=13 WHERE uid=@uid AND life=@life AND stage=12","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)
                        throw new InvalidOperationException("Power Book hand-in changed.");
                    Ready(p,context);t.Commit();
                }}
                SilverBalance.ApplyCommitted(p,silver);ReincarnationRecovery.Committed(p,plan.Restored);MentorExperience.Apply(p,effects.MentorCredit);p.ApplyExperienceReward(plan,effects,auto,true);return true;
            }
        }
        private static bool QuestItem(GameState p,uint uid,uint type)
        {
            ConquerItem item;return p.Inventory.TryGetItem(uid,out item) && item.ID==type && item.Bound && item.StackSize==1 && Usable(item);
        }
        internal static bool Claim(GameState p,Func<bool> context)
        {
            Ready(p,context);ConquerItem pack,soul;uint badge,book;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=13)return false;
                badge=run.BadgeUid;book=run.BookUid;if(!QuestItem(p,badge,Badge) || !QuestItem(p,book,Book) || ArmorFor(p.Entity.Class)==0)return false;
                ConquerItemBaseInformation info;
                if(!ConquerItemInformation.BaseInformations.TryGetValue(run.Soul,out info) || info.PurificationLevel!=7 || run.Soul<800000 || run.Soul>=900000)
                    throw new InvalidOperationException("Power Book promised soul changed.");
                Q.Consume(c,t,p,badge,Badge);Q.Consume(c,t,p,book,Book);
                pack=Q.New(728251,true);soul=Q.New(run.Soul,true);Q.Insert(c,t,p.Entity.UID,pack);Q.Insert(c,t,p.Entity.UID,soul);
                if(Q.Execute(c,t,"INSERT INTO revora_power_book_rewards(uid,life,pack_uid,soul_uid,soul_type) VALUES(@uid,@life,@pack,@soul,@type)",
                    "uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"pack",(long)pack.UID,"soul",(long)soul.UID,"type",(long)soul.ID)!=1)
                    throw new InvalidOperationException("Power Book reward receipt missing.");
                if(Q.Execute(c,t,"UPDATE revora_power_book SET stage=14 WHERE uid=@uid AND life=@life AND stage=13","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)
                    throw new InvalidOperationException("Power Book completion changed.");
                Ready(p,context);ProgressionEvidence.RecordEquipmentQuestAggregate(c,t,p.Entity.UID);t.Commit();
            }}
            Q.Install(p,new[]{badge,book},new[]{pack,soul});return true;
        }
    }
}
