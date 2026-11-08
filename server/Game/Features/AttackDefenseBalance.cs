using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class AttackDefenseBalance
    {
        internal sealed class Image
        {
            internal Entity Entity;
            internal int StigmaSeconds,StigmaIncrease,MagicSeconds,MagicIncrease;
            internal DateTime? StigmaGranted,MagicGranted;
        }
        private static readonly ConditionalWeakTable<GameState,Image> images=new ConditionalWeakTable<GameState,Image>();
        private static void RequireOwner(GameState p)
        {
            if(p==null || !Monitor.IsEntered(p.ItemSyncRoot ?? p) || p.Entity.PersistenceRequiresReload)
                throw new InvalidOperationException("Attack/Defense potion requires a healthy owner lock.");
        }
        private static Image State(GameState p)
        {
            Image image;
            return images.TryGetValue(p,out image) && ReferenceEquals(image.Entity,p.Entity)?image:null;
        }
        internal static bool Managed(GameState p){return p!=null && State(p)!=null;}
        internal static void Forget(GameState p){images.Remove(p);}
        internal static Image Current(GameState p)
        {
            RequireOwner(p);
            var image=State(p);
            if(image!=null)return Copy(image);
            return new Image{Entity=p.Entity};
        }
        private static Image Copy(Image image)
        {
            return new Image{
                Entity=image.Entity,
                StigmaSeconds=image.StigmaSeconds,StigmaIncrease=image.StigmaIncrease,StigmaGranted=image.StigmaGranted,
                MagicSeconds=image.MagicSeconds,MagicIncrease=image.MagicIncrease,MagicGranted=image.MagicGranted
            };
        }
        internal static DateTime GrantStamp(DateTime now)
        {
            if(now.Kind!=DateTimeKind.Utc)now=now.ToUniversalTime();
            return new DateTime(now.Year,now.Month,now.Day,now.Hour,now.Minute,now.Second,DateTimeKind.Utc);
        }
        internal static int Remaining(int seconds,DateTime? granted,DateTime now)
        {
            if(seconds<=0 || !granted.HasValue)return 0;
            double elapsed=(GrantStamp(now)-granted.Value).TotalSeconds;
            if(elapsed<0)elapsed=0;
            int left=seconds-(int)elapsed;
            if(left<0)return 0;
            if(left>45)return 45;
            return left;
        }
        private static bool SameStamp(DateTime? left,DateTime? right)
        {
            if(!left.HasValue && !right.HasValue)return true;
            if(!left.HasValue || !right.HasValue)return false;
            return left.Value==right.Value;
        }
        private static bool Empty(Image image)
        {
            return image==null || (image.StigmaSeconds==0 && image.MagicSeconds==0 && !image.StigmaGranted.HasValue && !image.MagicGranted.HasValue);
        }
        private static DateTime? UtcStamp(object value)
        {
            if(value==null || value==DBNull.Value)return null;
            var stamp=Convert.ToDateTime(value);
            if(stamp.Kind==DateTimeKind.Unspecified)stamp=DateTime.SpecifyKind(stamp,DateTimeKind.Utc);
            else stamp=stamp.ToUniversalTime();
            return GrantStamp(stamp);
        }
        private static object StampArg(DateTime? stamp)
        {
            if(!stamp.HasValue)return DBNull.Value;
            var utc=stamp.Value;
            if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Attack/Defense timestamps require UTC.");
            return utc;
        }
        internal static void Loaded(GameState p)
        {
            lock(p.ItemSyncRoot ?? p)
            {
                Forget(p);
                try
                {
                    var image=new Image{Entity=p.Entity};
                    if(!AttackDefensePotions.Enabled)
                    {
                        images.Add(p,image);
                        return;
                    }
                    using(var c=DataHolder.MySqlConnection)
                    {
                        c.Open();
                        using(var q=Q.Command(c,null,@"SELECT stigma_seconds,stigma_increase,stigma_granted_at,magic_seconds,magic_increase,magic_granted_at
FROM revora_attack_defense_state WHERE uid=@uid","uid",(long)p.Entity.UID))
                        using(var r=q.ExecuteReader())
                        {
                            if(r.Read())
                            {
                                image.StigmaSeconds=Convert.ToInt32(r[0]);
                                image.StigmaIncrease=Convert.ToInt32(r[1]);
                                image.StigmaGranted=UtcStamp(r[2]);
                                image.MagicSeconds=Convert.ToInt32(r[3]);
                                image.MagicIncrease=Convert.ToInt32(r[4]);
                                image.MagicGranted=UtcStamp(r[5]);
                            }
                        }
                    }
                    images.Add(p,image);
                    AttackDefensePotions.Project(p,image);
                }
                catch(Exception ex){Q.Fail(p,"attack-defense-load",ex);throw;}
            }
        }
        internal static void AssertSaved(NpgsqlConnection c,NpgsqlTransaction t,GameState p)
        {
            RequireOwner(p);
            if(t==null)throw new InvalidOperationException("Attack/Defense potion requires a transaction.");
            var image=State(p);
            using(var q=Q.Command(c,t,@"SELECT stigma_seconds,stigma_increase,stigma_granted_at,magic_seconds,magic_increase,magic_granted_at
FROM revora_attack_defense_state WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())
            {
                if(!r.Read())
                {
                    if(!Empty(image))throw new InvalidOperationException("Durable Attack/Defense potion changed; reload required.");
                    return;
                }
                if(image==null || Convert.ToInt32(r[0])!=image.StigmaSeconds || Convert.ToInt32(r[1])!=image.StigmaIncrease
                    || !SameStamp(UtcStamp(r[2]),image.StigmaGranted) || Convert.ToInt32(r[3])!=image.MagicSeconds
                    || Convert.ToInt32(r[4])!=image.MagicIncrease || !SameStamp(UtcStamp(r[5]),image.MagicGranted))
                    throw new InvalidOperationException("Durable Attack/Defense potion changed; reload required.");
            }
        }
        internal static void Persist(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Image next)
        {
            AssertSaved(c,t,p);
            var image=State(p);
            bool exists=!Empty(image);
            int n;
            if(!exists)
            {
                n=Q.Execute(c,t,@"INSERT INTO revora_attack_defense_state(uid,stigma_seconds,stigma_increase,stigma_granted_at,magic_seconds,magic_increase,magic_granted_at)
VALUES(@uid,@ss,@si,@sg,@ms,@mi,@mg)","uid",(long)p.Entity.UID,"ss",next.StigmaSeconds,"si",next.StigmaIncrease,"sg",StampArg(next.StigmaGranted),
                    "ms",next.MagicSeconds,"mi",next.MagicIncrease,"mg",StampArg(next.MagicGranted));
            }
            else
            {
                n=Q.Execute(c,t,@"UPDATE revora_attack_defense_state SET stigma_seconds=@ss,stigma_increase=@si,stigma_granted_at=@sg,magic_seconds=@ms,magic_increase=@mi,magic_granted_at=@mg
WHERE uid=@uid AND stigma_seconds=@oss AND stigma_increase=@osi AND stigma_granted_at IS NOT DISTINCT FROM @osg
AND magic_seconds=@oms AND magic_increase=@omi AND magic_granted_at IS NOT DISTINCT FROM @omg",
                    "uid",(long)p.Entity.UID,"ss",next.StigmaSeconds,"si",next.StigmaIncrease,"sg",StampArg(next.StigmaGranted),
                    "ms",next.MagicSeconds,"mi",next.MagicIncrease,"mg",StampArg(next.MagicGranted),
                    "oss",image.StigmaSeconds,"osi",image.StigmaIncrease,"osg",StampArg(image.StigmaGranted),
                    "oms",image.MagicSeconds,"omi",image.MagicIncrease,"omg",StampArg(image.MagicGranted));
            }
            if(n!=1)throw new InvalidOperationException("Attack/Defense potion update was not saved.");
        }
        internal static void Committed(GameState p,Image next)
        {
            RequireOwner(p);
            Forget(p);
            next.Entity=p.Entity;
            images.Add(p,Copy(next));
        }
    }
}
