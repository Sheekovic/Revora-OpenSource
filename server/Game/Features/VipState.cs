using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Login clamps and daily expiry can change the cache before an autosave.
    // Keep that pending state distinct from the actual three stored VIP fields.
    internal static class VipState
    {
        private sealed class Image { internal Entity Entity; internal long? Level, Days, LevelDays; }
        private static readonly ConditionalWeakTable<GameState, Image> images = new ConditionalWeakTable<GameState, Image>();
        private static Image State(GameState p) { Image image; return images.TryGetValue(p,out image) && ReferenceEquals(image.Entity,p.Entity)?image:null; }
        private static void Require(GameState p) {
            if(p==null || p.Entity==null || p.Entity.PersistenceRequiresReload || !Monitor.IsEntered(p.ItemSyncRoot ?? p))
                throw new InvalidOperationException("VIP requires a healthy owner lock.");
        }
        private static long? Valid(long? value) {if(value<0 || value>byte.MaxValue)throw new OverflowException("Stored VIP field is out of range.");return value;}
        internal static void Forget(GameState p) {images.Remove(p);}
        internal static void Loaded(GameState p,MySqlReader reader) {
            lock(p.ItemSyncRoot ?? p) {
                Forget(p);images.Add(p,new Image {Entity=p.Entity,Level=Valid(reader.ReadNullableInt64Strict("viplevel")),
                    Days=Valid(reader.ReadNullableInt64Strict("vipdays")),LevelDays=Valid(reader.ReadNullableInt64Strict("vipleveldays"))});
            }
        }
        internal static void PrepareSave(GameState p,MySqlCommand command) {
            Require(p);var image=State(p);if(image==null)return;
            command.AndInt64Parameter("viplevel",image.Level).AndNumericTextParameter("vipdays",image.Days).AndInt64Parameter("vipleveldays",image.LevelDays);
        }
        internal static void Committed(GameState p) {
            Require(p);Forget(p);images.Add(p,new Image {Entity=p.Entity,Level=p.Entity.VIPLevel,Days=p.Entity.VIPDays,LevelDays=p.Entity.VIPLevelDays});
        }
        // Arena may capture a fenced outcome; validate its loaded durable level
        // without confusing login's effective VIP floor with a database change.
        internal static bool MatchesSavedLevel(GameState p,long saved) {
            if(p==null || p.Entity==null || p.ItemSyncRoot==null || !Monitor.IsEntered(p.ItemSyncRoot))
                throw new InvalidOperationException("VIP snapshot requires its owner lock.");
            var image=State(p);
            return saved==(image==null?p.Entity.VIPLevel:image.Level.GetValueOrDefault());
        }
        private static void AssertSaved(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            Require(p);if(t==null)throw new InvalidOperationException("VIP requires a transaction.");var image=State(p);
            using(var q=Q.Command(c,t,"SELECT viplevel,vipdays,vipleveldays FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader()) {
                if(!r.Read())throw new InvalidOperationException("VIP owner missing.");
                long? level=Valid(r.IsDBNull(0)?(long?)null:Convert.ToInt64(r.GetValue(0))),days=Valid(r.IsDBNull(1)?(long?)null:Convert.ToInt64(r.GetValue(1))),
                    levelDays=Valid(r.IsDBNull(2)?(long?)null:Convert.ToInt64(r.GetValue(2)));
                if(image!=null ? level!=image.Level || days!=image.Days || levelDays!=image.LevelDays
                    : level.GetValueOrDefault()!=p.Entity.VIPLevel || days.GetValueOrDefault()!=p.Entity.VIPDays || levelDays.GetValueOrDefault()!=p.Entity.VIPLevelDays)
                    throw new InvalidOperationException("Durable VIP changed; reload required.");
            }
        }
        internal static void Persist(NpgsqlConnection c,NpgsqlTransaction t,GameState p,byte level,byte days,byte levelDays) {
            AssertSaved(c,t,p);
            if(Q.Execute(c,t,"UPDATE entities SET viplevel=@level,vipdays=@days,vipleveldays=@leveldays WHERE uid=@uid",
                "level",(int)level,"days",days.ToString(System.Globalization.CultureInfo.InvariantCulture),"leveldays",(int)levelDays,"uid",(long)p.Entity.UID)!=1)
                throw new InvalidOperationException("VIP reward was not saved.");
        }
        internal static void ApplyCommitted(GameState p,byte level,byte days,byte levelDays) {
            Require(p);p.Entity.ApplyCommittedVip(level,days,levelDays);Committed(p);
        }
        internal static bool TrySetLevel(GameState p,byte level) {
            if(p==null || State(p)==null)return false;
            lock(p.ItemSyncRoot ?? p) try {
                Require(p);var image=State(p);
                using(var c=DataHolder.MySqlConnection) {c.Open();using(var t=c.BeginTransaction()) {
                    AssertSaved(c,t,p);
                    if(Q.Execute(c,t,"UPDATE entities SET viplevel=@level WHERE uid=@uid","level",(int)level,"uid",(long)p.Entity.UID)!=1)
                        throw new InvalidOperationException("VIP level was not saved.");
                    t.Commit();
                }}
                image.Level=level;return true;
            }catch(Exception ex){Q.Fail(p,"vip-level",ex);throw;}
        }
    }
}
