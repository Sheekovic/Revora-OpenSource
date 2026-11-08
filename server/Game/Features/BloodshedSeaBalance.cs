using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class BloodshedSeaBalance
    {
        internal const int Kinds=10;
        internal sealed class Image
        {
            internal Entity Entity;
            internal bool Present;
            internal DateTime?[] At=new DateTime?[Kinds];
        }
        private static readonly ConditionalWeakTable<GameState,Image> images=new ConditionalWeakTable<GameState,Image>();
        private static void RequireOwner(GameState p)
        {
            if(p==null || !Monitor.IsEntered(p.ItemSyncRoot ?? p) || p.Entity.PersistenceRequiresReload)
                throw new InvalidOperationException("Bloodshed Sea boost requires a healthy owner lock.");
        }
        private static Image State(GameState p)
        {
            Image image;
            return images.TryGetValue(p,out image) && ReferenceEquals(image.Entity,p.Entity)?image:null;
        }
        internal static void Forget(GameState p){images.Remove(p);}
        internal static DateTime GrantStamp(DateTime now)
        {
            if(now.Kind!=DateTimeKind.Utc)now=now.ToUniversalTime();
            return new DateTime(now.Year,now.Month,now.Day,now.Hour,now.Minute,now.Second,DateTimeKind.Utc);
        }
        internal static int Remaining(DateTime? granted,DateTime now)
        {
            if(!granted.HasValue)return 0;
            double elapsed=(GrantStamp(now)-granted.Value).TotalSeconds;
            if(elapsed<0)elapsed=0;
            int left=80-(int)elapsed;
            if(left<0)return 0;
            if(left>80)return 80;
            return left;
        }
        internal static int ShieldLeft(GameState p)
        {
            var image=State(p);
            if(image==null)return 0;
            return Remaining(image.At[1],BloodshedSeaBoosts.Clock());
        }
        internal static bool ShieldPending(GameState p)
        {
            var image=State(p);
            return image!=null && image.At[1].HasValue;
        }
        internal static Image Current(GameState p)
        {
            RequireOwner(p);
            var image=State(p);
            if(image==null)return new Image{Entity=p.Entity};
            return Copy(image);
        }
        private static Image Copy(Image image)
        {
            var next=new Image{Entity=image.Entity,Present=image.Present};
            for(int i=0;i<Kinds;i++)next.At[i]=image.At[i];
            return next;
        }
        private static bool Empty(Image image)
        {
            if(image==null || !image.Present)return true;
            return false;
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
            if(stamp.Value.Kind!=DateTimeKind.Utc)throw new ArgumentException("Bloodshed Sea timestamps require UTC.");
            return stamp.Value;
        }
        private static bool SameStamp(DateTime? left,DateTime? right)
        {
            if(!left.HasValue && !right.HasValue)return true;
            if(!left.HasValue || !right.HasValue)return false;
            return left.Value==right.Value;
        }
        internal static void Loaded(GameState p, bool applyStats = true)
        {
            lock(p.ItemSyncRoot ?? p)
            {
                Forget(p);
                try
                {
                    var image=new Image{Entity=p.Entity};
                    if(!BloodshedSeaBoosts.Enabled){images.Add(p,image);return;}
                    using(var c=DataHolder.MySqlConnection)
                    {
                        c.Open();
                        using(var q=Q.Command(c,null,@"SELECT attr_at,shield_at,break_at,strike_at,anti_at,immune_at,patk_at,matk_at,pdef_at,mdef_at
FROM revora_bloodshed_state WHERE uid=@uid","uid",(long)p.Entity.UID))
                        using(var r=q.ExecuteReader())
                        {
                            if(r.Read())
                            {
                                image.Present=true;
                                for(int i=0;i<Kinds;i++)image.At[i]=UtcStamp(r[i]);
                            }
                        }
                    }
                    images.Add(p,image);
                    BloodshedSeaBoosts.Project(p,image,applyStats);
                }
                catch(Exception ex){Q.Fail(p,"bloodshed-sea-load",ex);throw;}
            }
        }
        internal static void AssertSaved(NpgsqlConnection c,NpgsqlTransaction t,GameState p)
        {
            RequireOwner(p);
            if(t==null)throw new InvalidOperationException("Bloodshed Sea boost requires a transaction.");
            var image=State(p);
            using(var q=Q.Command(c,t,@"SELECT attr_at,shield_at,break_at,strike_at,anti_at,immune_at,patk_at,matk_at,pdef_at,mdef_at
FROM revora_bloodshed_state WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())
            {
                if(!r.Read())
                {
                    if(!Empty(image))throw new InvalidOperationException("Durable Bloodshed Sea boost changed; reload required.");
                    return;
                }
                if(Empty(image))throw new InvalidOperationException("Durable Bloodshed Sea boost changed; reload required.");
                for(int i=0;i<Kinds;i++)
                    if(!SameStamp(UtcStamp(r[i]),image.At[i]))throw new InvalidOperationException("Durable Bloodshed Sea boost changed; reload required.");
            }
        }
        internal static void Persist(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Image next)
        {
            AssertSaved(c,t,p);
            var image=State(p);
            int n;
            if(Empty(image))
            {
                n=Q.Execute(c,t,@"INSERT INTO revora_bloodshed_state(uid,attr_at,shield_at,break_at,strike_at,anti_at,immune_at,patk_at,matk_at,pdef_at,mdef_at)
VALUES(@uid,@a0,@a1,@a2,@a3,@a4,@a5,@a6,@a7,@a8,@a9)","uid",(long)p.Entity.UID,
                    "a0",StampArg(next.At[0]),"a1",StampArg(next.At[1]),"a2",StampArg(next.At[2]),"a3",StampArg(next.At[3]),"a4",StampArg(next.At[4]),
                    "a5",StampArg(next.At[5]),"a6",StampArg(next.At[6]),"a7",StampArg(next.At[7]),"a8",StampArg(next.At[8]),"a9",StampArg(next.At[9]));
            }
            else
            {
                n=Q.Execute(c,t,@"UPDATE revora_bloodshed_state SET attr_at=@a0,shield_at=@a1,break_at=@a2,strike_at=@a3,anti_at=@a4,immune_at=@a5,patk_at=@a6,matk_at=@a7,pdef_at=@a8,mdef_at=@a9
WHERE uid=@uid AND attr_at IS NOT DISTINCT FROM @o0 AND shield_at IS NOT DISTINCT FROM @o1 AND break_at IS NOT DISTINCT FROM @o2
AND strike_at IS NOT DISTINCT FROM @o3 AND anti_at IS NOT DISTINCT FROM @o4 AND immune_at IS NOT DISTINCT FROM @o5
AND patk_at IS NOT DISTINCT FROM @o6 AND matk_at IS NOT DISTINCT FROM @o7 AND pdef_at IS NOT DISTINCT FROM @o8 AND mdef_at IS NOT DISTINCT FROM @o9",
                    "uid",(long)p.Entity.UID,
                    "a0",StampArg(next.At[0]),"a1",StampArg(next.At[1]),"a2",StampArg(next.At[2]),"a3",StampArg(next.At[3]),"a4",StampArg(next.At[4]),
                    "a5",StampArg(next.At[5]),"a6",StampArg(next.At[6]),"a7",StampArg(next.At[7]),"a8",StampArg(next.At[8]),"a9",StampArg(next.At[9]),
                    "o0",StampArg(image.At[0]),"o1",StampArg(image.At[1]),"o2",StampArg(image.At[2]),"o3",StampArg(image.At[3]),"o4",StampArg(image.At[4]),
                    "o5",StampArg(image.At[5]),"o6",StampArg(image.At[6]),"o7",StampArg(image.At[7]),"o8",StampArg(image.At[8]),"o9",StampArg(image.At[9]));
            }
            if(n!=1)throw new InvalidOperationException("Bloodshed Sea boost update was not saved.");
            next.Present=true;
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
