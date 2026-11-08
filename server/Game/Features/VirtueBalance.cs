using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Earned, unflushed Virtue is distinct from its last durable preimage.
    internal static class VirtueBalance
    {
        private sealed class Image { internal Entity Entity; internal long? Saved; }
        private static readonly ConditionalWeakTable<GameState, Image> images = new ConditionalWeakTable<GameState, Image>();
        private static Image State(GameState p) { Image image; return images.TryGetValue(p, out image) && ReferenceEquals(image.Entity, p.Entity) ? image : null; }
        private static void Require(GameState p) {
            if (p == null || p.Entity == null || p.Entity.PersistenceRequiresReload || !Monitor.IsEntered(p.ItemSyncRoot ?? p) || !Monitor.IsEntered(p.VirtueSyncRoot))
                throw new InvalidOperationException("Virtue requires both healthy owner locks.");
        }
        private static long? Valid(long? points) { if (points < 0 || points > uint.MaxValue) throw new OverflowException("Stored Virtue is outside its range."); return points; }
        internal static void Loaded(GameState p, MySqlReader reader) {
            lock(p.ItemSyncRoot ?? p) lock(p.VirtueSyncRoot) {
                images.Remove(p);
                try { images.Add(p, new Image { Entity=p.Entity, Saved=Valid(reader.ReadNullableInt64Strict("virtuepoints")) }); }
                catch(Exception ex) {Q.Fail(p,"virtue-load",ex);throw;}
            }
        }
        internal static void PrepareSave(GameState p, MySqlCommand command) {
            Require(p);var image=State(p);
            if(image!=null)command.AndInt64Parameter("virtuepoints",image.Saved);
        }
        internal static void Committed(GameState p) {
            Require(p);images.Remove(p);images.Add(p,new Image {Entity=p.Entity,Saved=p.VirtuePoints});
        }
        internal static void AssertSaved(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            Require(p);if(t==null)throw new InvalidOperationException("Virtue requires a transaction.");var image=State(p);
            using(var q=Q.Command(c,t,"SELECT virtuepoints FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID)) {
                object value=q.ExecuteScalar();if(value==null)throw new InvalidOperationException("Virtue owner missing.");
                long? saved=Valid(value==DBNull.Value?(long?)null:Convert.ToInt64(value));
                if(image!=null?saved!=image.Saved:saved.GetValueOrDefault()!=p.VirtuePoints)throw new InvalidOperationException("Durable Virtue changed; reload required.");
            }
        }
        internal static void Persist(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint after) {
            AssertSaved(c,t,p);
            if(Q.Execute(c,t,"UPDATE entities SET virtuepoints=@points WHERE uid=@uid","points",(long)after,"uid",(long)p.Entity.UID)!=1)
                throw new InvalidOperationException("Virtue update was not saved.");
        }
        internal static void ApplyCommitted(GameState p,uint after) {Require(p);p.VirtuePoints=after;Committed(p);}
    }
}
