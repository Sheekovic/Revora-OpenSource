using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class HealingMedicineBalance
    {
        internal sealed class Image
        {
            internal Entity Entity;
            internal bool Present;
            internal int Seconds;
            internal DateTime? Granted;
            internal bool PoisonStar;
        }
        private static readonly ConditionalWeakTable<GameState,Image> images=new ConditionalWeakTable<GameState,Image>();
        private static void RequireOwner(GameState p)
        {
            if(p==null || !Monitor.IsEntered(p.ItemSyncRoot ?? p) || p.Entity.PersistenceRequiresReload)
                throw new InvalidOperationException("Healing medicine requires a healthy owner lock.");
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
        internal static int Remaining(int seconds,DateTime? granted,DateTime now)
        {
            if(seconds<=0 || !granted.HasValue)return 0;
            double elapsed=(GrantStamp(now)-granted.Value).TotalSeconds;
            if(elapsed<0)elapsed=0;
            int left=seconds-(int)elapsed;
            if(left<0)return 0;
            return left;
        }
        internal static Image Current(GameState p)
        {
            RequireOwner(p);
            var image=State(p);
            if(image==null)return new Image{Entity=p.Entity};
            return new Image{Entity=image.Entity,Present=image.Present,Seconds=image.Seconds,Granted=image.Granted,PoisonStar=image.PoisonStar};
        }
        private static bool Empty(Image image){return image==null || !image.Present;}
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
            if(stamp.Value.Kind!=DateTimeKind.Utc)throw new ArgumentException("Healing medicine timestamps require UTC.");
            return stamp.Value;
        }
        internal static void Loaded(GameState p)
        {
            lock(p.ItemSyncRoot ?? p)
            {
                Forget(p);
                try
                {
                    var image=new Image{Entity=p.Entity};
                    if(!HealingMedicine.Enabled){images.Add(p,image);return;}
                    using(var c=DataHolder.MySqlConnection)
                    {
                        c.Open();
                        using(var q=Q.Command(c,null,"SELECT seconds,granted_at,poison_star FROM revora_no_drugs_state WHERE uid=@uid","uid",(long)p.Entity.UID))
                        using(var r=q.ExecuteReader())
                        {
                            if(r.Read()){image.Present=true;image.Seconds=Convert.ToInt32(r[0]);image.Granted=UtcStamp(r[1]);image.PoisonStar=Convert.ToBoolean(r[2]);}
                        }
                    }
                    images.Add(p,image);
                    HealingMedicine.Project(p,image);
                }
                catch(Exception ex){Q.Fail(p,"healing-medicine-load",ex);throw;}
            }
        }
        internal static void AssertSaved(NpgsqlConnection c,NpgsqlTransaction t,GameState p)
        {
            RequireOwner(p);
            if(t==null)throw new InvalidOperationException("Healing medicine requires a transaction.");
            var image=State(p);
            using(var q=Q.Command(c,t,"SELECT seconds,granted_at,poison_star FROM revora_no_drugs_state WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())
            {
                if(!r.Read())
                {
                    if(!Empty(image))throw new InvalidOperationException("Durable NoDrugsTime changed; reload required.");
                    return;
                }
                if(Empty(image) || Convert.ToInt32(r[0])!=image.Seconds || UtcStamp(r[1])!=image.Granted || Convert.ToBoolean(r[2])!=image.PoisonStar)
                    throw new InvalidOperationException("Durable NoDrugsTime changed; reload required.");
            }
        }
        internal static void Persist(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Image next)
        {
            AssertSaved(c,t,p);
            var image=State(p);
            int n;
            if(Empty(image))
            {
                n=Q.Execute(c,t,@"INSERT INTO revora_no_drugs_state(uid,seconds,granted_at,poison_star) VALUES(@uid,@seconds,@granted,@poison)",
                    "uid",(long)p.Entity.UID,"seconds",next.Seconds,"granted",StampArg(next.Granted),"poison",next.PoisonStar);
            }
            else
            {
                n=Q.Execute(c,t,@"UPDATE revora_no_drugs_state SET seconds=@seconds,granted_at=@granted,poison_star=@poison WHERE uid=@uid AND seconds=@old AND granted_at IS NOT DISTINCT FROM @oldg AND poison_star=@oldp",
                    "uid",(long)p.Entity.UID,"seconds",next.Seconds,"granted",StampArg(next.Granted),"poison",next.PoisonStar,"old",image.Seconds,"oldg",StampArg(image.Granted),"oldp",image.PoisonStar);
            }
            if(n!=1)throw new InvalidOperationException("NoDrugsTime update was not saved.");
            next.Present=true;
        }
        internal static void Committed(GameState p,Image next)
        {
            RequireOwner(p);
            Forget(p);
            next.Entity=p.Entity;
            images.Add(p,new Image{Entity=p.Entity,Present=true,Seconds=next.Seconds,Granted=next.Granted,PoisonStar=next.PoisonStar});
        }
    }
    // Last committed HP/MP is distinct from current combat state. This image is
    // advanced only after the owning SQL transaction or character save commits.
    internal static class HealthBalance
    {
        internal sealed class Image { internal Entity Entity;internal long? Hp,Mp; }
        private sealed class Holder { internal Entity Entity;internal volatile Image Current; }
        private static readonly ConditionalWeakTable<GameState,Holder> saved=new ConditionalWeakTable<GameState,Holder>();
        internal static void Forget(GameState p){saved.Remove(p);}
        private static Image State(GameState p){Holder value;return p!=null && saved.TryGetValue(p,out value) && ReferenceEquals(value.Entity,p.Entity)?value.Current:null;}
        internal static bool Managed(GameState p){return State(p)!=null;}
        private static void RequireOwner(GameState p){if(p==null || !Monitor.IsEntered(p.ItemSyncRoot ?? p) || p.Entity.PersistenceRequiresReload)throw new InvalidOperationException("Health persistence requires a healthy owner lock.");}
        internal static void Loaded(GameState p,MySqlReader reader){
            lock(p.ItemSyncRoot ?? p){Forget(p);saved.Add(p,new Holder{Entity=p.Entity,Current=new Image{Entity=p.Entity,Hp=reader.ReadNullableInt64Strict("hitpoints"),Mp=reader.ReadNullableInt64Strict("mana")}});}
        }
        internal static Image Capture(GameState p){RequireOwner(p);return new Image{Entity=p.Entity,Hp=p.Entity.Hitpoints,Mp=p.Entity.Mana};}
        internal static Image PrepareSave(GameState p,MySqlCommand command){
            RequireOwner(p);var prior=State(p);if(prior==null)return null;
            command.AndInt64Parameter("hitpoints",prior.Hp);command.AndInt64Parameter("mana",prior.Mp);return Capture(p);
        }
        internal static void Persist(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint hp,ushort mp){
            RequireOwner(p);var prior=State(p) ?? Capture(p);
            if(Q.Execute(c,t,@"UPDATE entities SET hitpoints=@hp,mana=@mp WHERE uid=@uid
AND hitpoints IS NOT DISTINCT FROM CAST(@oldhp AS bigint) AND mana IS NOT DISTINCT FROM CAST(@oldmp AS bigint)",
                "uid",(long)p.Entity.UID,"hp",(long)hp,"mp",(int)mp,"oldhp",prior.Hp.HasValue?(object)prior.Hp.Value:DBNull.Value,"oldmp",prior.Mp.HasValue?(object)prior.Mp.Value:DBNull.Value)!=1)
                throw new InvalidOperationException("Durable HP/MP changed; reload required.");
        }
        internal static void Committed(GameState p,Image next){RequireOwner(p);Holder holder;if(next==null || !saved.TryGetValue(p,out holder))return;if(!ReferenceEquals(next.Entity,p.Entity) || !ReferenceEquals(holder.Entity,p.Entity))throw new InvalidOperationException("Health owner changed.");holder.Current=next;}
        internal static void Committed(GameState p,uint hp,ushort mp){Committed(p,new Image{Entity=p.Entity,Hp=hp,Mp=mp});}
    }

}
