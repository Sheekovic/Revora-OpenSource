using System;
using System.Linq;
using System.Collections.Generic;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class MadManStore
    {
        // Explicit Revora duration/intensity. The original guide specifies outcomes only.
        internal const int MixtureSeconds=20;
        internal static bool ApplyMixture(GameState p,Guid receipt,Func<bool> context){
            using(var health=HealthMutationLease.Acquire(p==null?null:p.Entity,null)){
                if(!health.Allowed)return false;return ApplyMixtureOwned(p,receipt,context);
            }
        }
        private static bool ApplyMixtureOwned(GameState p,Guid receipt,Func<bool> context){
            Ready(p,context);Mixture outcome;int seconds=0;uint healed=0;Guid appearance=Guid.Empty;
            try{
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);if(Read(c,t,p).Stage!=5)return false;DateTime mixed;
                    using(var q=Q.Command(c,t,"SELECT outcome,mixed_at FROM revora_mad_mixes WHERE id=@id AND uid=@uid AND life=@life AND NOT applied FOR UPDATE","id",receipt,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))using(var r=q.ExecuteReader()){
                        if(!r.Read())return false;outcome=(Mixture)r.GetInt16(0);mixed=r.GetDateTime(1).ToUniversalTime();
                    }
                    if(outcome<Mixture.Poison || outcome>Mixture.Zombie)return false;
                    var now=Now();if(now<mixed)throw new InvalidOperationException("Mad effect clock moved backwards.");
                    seconds=(int)Math.Ceiling((mixed.AddSeconds(MixtureSeconds)-now).TotalSeconds);
                    if(seconds>0){
                        if(p.Entity.MapID!=1002 || p.InQualifier() || p.InTeamQualifier())return false;
                        if(outcome==Mixture.Poison && (p.Entity.ToxicFogLeft>0 || p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Poisoned)))return false;
                        if(outcome==Mixture.Strong && p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Stigma))return false;
                        if(outcome==Mixture.Float && (p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Fly) || p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Ride) || p.Entity.TransformationID!=0))return false;
                        if(outcome==Mixture.Zombie){if(p.Entity.TransformationID!=0)return false;appearance=QuestAppearance.ApplyZombie(p,mixed.AddSeconds(MixtureSeconds),context);if(appearance==Guid.Empty)return false;}
                        if(outcome==Mixture.Heal){healed=p.Entity.MaxHitpoints;if(healed==0)return false;
                            HealthBalance.Persist(c,t,p,healed,p.Entity.Mana);}
                    }
                    if(Q.Execute(c,t,"UPDATE revora_mad_mixes SET applied=true WHERE id=@id AND NOT applied","id",receipt)!=1)throw new InvalidOperationException("Mad effect receipt changed.");
                    Ready(p,context);t.Commit();
                    if(healed>0)HealthBalance.Committed(p,healed,p.Entity.Mana);
                }}
            }catch{if(appearance!=Guid.Empty)QuestAppearance.Clear(p,appearance,QuestAppearance.ZombieMesh);throw;}
            // Receipt commits before transient projection: never replay healing or extend buffs
            // on reconnect. Pending receipts expire from mixing time, including offline time.
            if(seconds>0){var body=p.Entity;var stamp=Time32.Now;
                if(outcome==Mixture.Heal)body.Hitpoints=healed;
                else if(outcome==Mixture.Poison){body.ToxicFogStamp=stamp;body.ToxicFogPercent=0.02f;body.ToxicFogLeft=(byte)Math.Max(1,(seconds+1)/2);if(!body.AddFlag((ulong)FlagsUpdate.Flags.Poisoned,seconds,true))throw new InvalidOperationException("Mad poison projection changed.");}
                else if(outcome==Mixture.Strong){body.StigmaStamp=stamp;body.StigmaIncrease=1.15f;body.StigmaTime=(byte)seconds;if(!body.AddFlag((ulong)FlagsUpdate.Flags.Stigma,seconds,true))throw new InvalidOperationException("Mad strength projection changed.");p.Send(Constants.Stigma(1.15f,seconds));}
                else if(outcome==Mixture.Float){body.FlyStamp=stamp;body.FlyTime=(byte)seconds;if(!body.AddFlag((ulong)FlagsUpdate.Flags.Fly,seconds,true))throw new InvalidOperationException("Mad flight projection changed.");}
            }return true;
        }
        private static bool MayReplenish(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p){
            using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_mad_toxins WHERE uid=@uid AND life=@life AND state=2)
AND NOT EXISTS(SELECT 1 FROM revora_mad_mixes WHERE uid=@uid AND life=@life AND (NOT applied OR mixed_at>@cutoff))",
                "uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"cutoff",Now().AddSeconds(-MixtureSeconds)))return Equals(q.ExecuteScalar(),true);
        }
        // A wrong mixture requires a fresh earned fang; no free repeatable healing/buffs.
        internal static bool Replenish(GameState p,Func<bool> context){
            Ready(p,context);uint fang;var added=new List<ConquerItem>();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=5 || !MayReplenish(c,t,p) || !Material(c,t,p,run,729978))return false;
                var spent=Toxins(c,t,p).Where(x=>x.State==2).ToArray();if(p.Inventory.Count-1+spent.Length>40)return false;
                fang=run.Item;Q.Consume(c,t,p,fang,729978);
                foreach(var toxin in spent){
                    using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE uid=@uid FOR UPDATE","uid",(long)toxin.Item))if(q.ExecuteScalar()!=null)throw new InvalidOperationException("Mad spent toxin still exists.");
                    var item=Q.New(toxin.Type,true);Q.Insert(c,t,p.Entity.UID,item);added.Add(item);
                    if(Q.Execute(c,t,"UPDATE revora_mad_toxins SET item_uid=@new,state=0 WHERE uid=@uid AND life=@life AND type=@type AND item_uid=@old AND state=2","new",(long)item.UID,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)toxin.Type,"old",(long)toxin.Item)!=1)throw new InvalidOperationException("Mad replenishment changed.");
                    if(Q.Execute(c,t,"INSERT INTO revora_mad_recoveries(uid,life,old_uid,new_uid,type) VALUES(@uid,@life,@old,@new,@type)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"old",(long)toxin.Item,"new",(long)item.UID,"type",(long)toxin.Type)!=1)throw new InvalidOperationException("Mad replenishment receipt failed.");
                }
                run.Item=run.Type=0;Save(c,t,p,run,5);Ready(p,context);t.Commit();
            }}Q.Install(p,new[]{fang},added);return true;
        }
    }
}
