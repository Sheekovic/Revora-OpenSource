using System;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class ShelbyRewards
    {
        private sealed class Offer { internal byte Kind; internal Guid Id; internal DateTime Until; }
        private static readonly ConditionalWeakTable<GameState, Offer> offers = new ConditionalWeakTable<GameState, Offer>();
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        private const ulong WalletPrice = 200500000000;
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection) {c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_shelby_rewards(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),kind integer NOT NULL CHECK(kind BETWEEN 1 AND 3),
virtue_before bigint NOT NULL CHECK(virtue_before BETWEEN 0 AND 4294967295),virtue_after bigint NOT NULL CHECK(virtue_after BETWEEN 0 AND 4294967295),
silver_before bigint NOT NULL CHECK(silver_before>=0),silver_after bigint NOT NULL CHECK(silver_after>=0),
item_uid bigint,experience bigint NOT NULL CHECK(experience>=0),level_before integer NOT NULL,level_after integer NOT NULL,
created_at timestamptz NOT NULL DEFAULT now(),
CHECK(virtue_after=virtue_before-CASE kind WHEN 1 THEN 5000 WHEN 2 THEN 15000 ELSE 0 END),
CHECK(silver_after=silver_before-CASE kind WHEN 3 THEN 200500000000 ELSE 0 END),
CHECK((kind IN(1,3) AND item_uid IS NOT NULL AND item_uid>0 AND experience=0) OR (kind=2 AND item_uid IS NULL AND experience>0)));");}
        }
        private static string Claim(GameState p,byte kind) {
            Offer offer;if(!offers.TryGetValue(p,out offer) || offer.Kind!=kind || offer.Until<=Clock())return "Tell me which reward you want first.";
            offers.Remove(p);
            uint virtue=p.VirtuePoints,cost=kind==1?5000u:kind==2?15000u:0;
            ulong silver=p.Entity.Money,price=kind==3?WalletPrice:0;
            if(virtue<cost)return "Help more young adventurers, then return.";
            if(silver<price)return "Bring 200,500,000,000 silver for the wallet.";
            if(kind!=2 && p.Inventory.Count>=40)return "Leave one space in your bag.";
            if(kind==2 && (!CharacterProgression.Managed(p) || p.Entity.Level>=Config.LevelCap
                || p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Cursed)))return "Keep your points until you can receive experience.";
            ulong award=kind==2?p.ExpBall:0;
            if(kind==2 && award==0)return "Keep your points until you can receive experience.";
            ulong autoHunt=p.Entity.InAutoHunt?checked(p.Entity.AutoHuntEXP+award/Math.Max(1u,Config.AutoHuntExperienceDivisor)):p.Entity.AutoHuntEXP;
            var item=kind==2?null:Q.New(kind==1?1088001u:723723u,false);
            ExperienceRewardPlan plan=null;ExperienceRewards.Effects effects=null;bool achievement=false;
            using(var c=DataHolder.MySqlConnection) {c.Open();using(var t=c.BeginTransaction()) {
                VirtueBalance.AssertSaved(c,t,p);SilverBalance.AssertSaved(c,t,p);
                if(kind!=3)VirtueBalance.Persist(c,t,p,virtue-cost);
                if(kind==3)SilverBalance.Persist(c,t,p,silver-price);
                if(item!=null) {
                    Q.Insert(c,t,p.Entity.UID,item);
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM items WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0 AND stacksize=1 AND bound=0)","item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID))
                        if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Shelby item was not saved.");
                } else {
                    plan=new ExperienceRewardPlan(CharacterProgression.Capture(p),award,Config.LevelCap,Config.AttributePointsPerLevel,ReincarnationRecovery.Snapshot(p));
                    CharacterProgression.Persist(c,t,p,plan.After);effects=ExperienceRewards.Persist(c,t,p,plan);ReincarnationRecovery.Consume(c,t,p,plan.Restored);
                }
                if(Q.Execute(c,t,@"INSERT INTO revora_shelby_rewards(id,uid,kind,virtue_before,virtue_after,silver_before,silver_after,item_uid,experience,level_before,level_after)
VALUES(@id,@uid,@kind,@vb,@va,@sb,@sa,NULLIF(@item,0),@exp,@lb,@la)","id",offer.Id,"uid",(long)p.Entity.UID,"kind",(int)kind,
                    "vb",(long)virtue,"va",(long)(virtue-cost),"sb",checked((long)silver),"sa",checked((long)(silver-price)),
                    "item",item==null?0L:(long)item.UID,"exp",checked((long)award),"lb",(int)p.Entity.Level,"la",plan==null?(int)p.Entity.Level:(int)plan.After.Values[0])!=1)
                    throw new InvalidOperationException("Shelby receipt was not saved.");
                if(kind==1) {
                    achievement=ProgressionEvidence.Complete(c,t,p.Entity.UID,10110,"virtue-meteor-v1",offer.Id.ToString("N"),1);
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10110)","uid",(long)p.Entity.UID))
                        if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Virtue achievement was not saved.");
                }
                t.Commit();
            }}
            if(kind!=3)VirtueBalance.ApplyCommitted(p,virtue-cost);
            if(kind==3)SilverBalance.ApplyCommitted(p,silver-price);
            if(item!=null)Q.Install(p,new uint[0],new[]{item});
            else {ReincarnationRecovery.Committed(p,plan.Restored);MentorExperience.Apply(p,effects.MentorCredit);p.ApplyExperienceReward(plan,effects,autoHunt,true);}
            if(achievement)Q.Refresh(p);
            return kind==1?"Your kindness has earned this Meteor.":kind==2?"May this experience serve you well.":"Your silver is wrapped. Keep it safe.";
        }
        internal static void Dialog(GameState p,byte option) {
            if(!Q.Near(p,30000,1036))return;
            lock(p.ItemSyncRoot) lock(MentorExperience.Sync) lock(p.VirtueSyncRoot) try {
                if(!Q.Near(p,30000,1036))return;var d=new Npcs(p);
                if(option==0) {
                    offers.Remove(p);d.Text("I reward those who help young adventurers. Spend your Virtue Points here.");
                    d.Option("A Meteor: 5,000 points.",9);d.Option("Experience: 15,000 points.",11);d.Option("Count my points.",5);d.Option("Wrap my silver.",6);
                } else if(option==5)d.Text("You have "+p.VirtuePoints+" Virtue Points.");
                else if(option==9 || option==11 || option==6) {
                    byte kind=option==9?(byte)1:option==11?(byte)2:(byte)3;
                    offers.Remove(p);offers.Add(p,new Offer{Kind=kind,Id=Guid.NewGuid(),Until=Clock().AddMinutes(1)});
                    d.Text(kind==1?"One Meteor costs 5,000 Virtue Points.":kind==2?"15,000 Virtue Points earns the experience of one EXP Ball.":"I wrap 200,000,000,000 silver for a 500,000,000 fee.");
                    d.Option("Make the exchange.",kind==1?(byte)10:kind==2?(byte)8:(byte)7);
                } else if(option==10 || option==8 || option==7)d.Text(Claim(p,option==10?(byte)1:option==8?(byte)2:(byte)3));
                else {offers.Remove(p);return;}
                d.Option("Farewell.",255);d.Avatar(7);d.Send();
            }catch(Exception ex){Q.Fail(p,"shelby-reward",ex);}
        }
    }
}
