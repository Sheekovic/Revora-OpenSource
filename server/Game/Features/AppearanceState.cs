using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Saved preimages are distinct from legitimate unflushed session changes.
    internal static class AppearanceState
    {
        private sealed class Image { internal Entity Entity; internal long? Body, Face, Hair; }
        private static readonly ConditionalWeakTable<GameState, Image> images = new ConditionalWeakTable<GameState, Image>();
        private static Image State(GameState p)
        {
            Image image;
            return images.TryGetValue(p, out image) && ReferenceEquals(image.Entity, p.Entity) ? image : null;
        }
        private static void Require(GameState p)
        {
            if (p == null || p.Entity == null || !Monitor.IsEntered(p.ItemSyncRoot ?? p) || p.Entity.PersistenceRequiresReload)
                throw new InvalidOperationException("Appearance requires a healthy owner lock.");
        }
        private static long? Valid(long? value)
        {
            if (value < 0 || value > ushort.MaxValue) throw new OverflowException("Appearance value is outside its stored width.");
            return value;
        }
        internal static void Loaded(GameState p, MySqlReader reader)
        {
            lock (p.ItemSyncRoot ?? p) {
                images.Remove(p);
                try {
                    var image = new Image { Entity = p.Entity, Body = Valid(reader.ReadNullableInt64Strict("body")),
                        Face = Valid(reader.ReadNullableInt64Strict("face")), Hair = Valid(reader.ReadNullableInt64Strict("hairstyle")) };
                    images.Add(p, image);
                } catch (Exception ex) { Q.Fail(p, "appearance-load", ex); throw; }
            }
        }
        internal static void PrepareSave(GameState p, MySqlCommand command)
        {
            Require(p); var image = State(p);
            if (image == null) return; // New-character bootstrap has no prior appearance.
            command.AndInt64Parameter("body", image.Body).AndInt64Parameter("face", image.Face).AndInt64Parameter("hairstyle", image.Hair);
        }
        internal static void Committed(GameState p)
        {
            Require(p); var image = State(p);
            if (image != null) { image.Body = p.Entity.Body; image.Face = p.Entity.Face; image.Hair = p.Entity.HairStyle; }
        }
        internal static void AssertSaved(NpgsqlConnection c, NpgsqlTransaction t, GameState p)
        {
            Require(p);
            if (t == null) throw new InvalidOperationException("Appearance requires a transaction.");
            var image = State(p);
            using (var q = Q.Command(c, t, "SELECT body,face,hairstyle FROM entities WHERE uid=@uid FOR UPDATE", "uid", (long)p.Entity.UID))
            using (var r = q.ExecuteReader()) {
                if (!r.Read()) throw new InvalidOperationException("Appearance owner is missing.");
                long? body = Valid(r.IsDBNull(0) ? (long?)null : Convert.ToInt64(r.GetValue(0)));
                long? face = Valid(r.IsDBNull(1) ? (long?)null : Convert.ToInt64(r.GetValue(1)));
                long? hair = Valid(r.IsDBNull(2) ? (long?)null : Convert.ToInt64(r.GetValue(2)));
                if (image != null ? body != image.Body || face != image.Face || hair != image.Hair
                    : body.GetValueOrDefault() != p.Entity.Body || face.GetValueOrDefault() != p.Entity.Face || hair.GetValueOrDefault() != p.Entity.HairStyle)
                    throw new InvalidOperationException("Durable appearance changed; reload required.");
            }
        }
        internal static void Persist(NpgsqlConnection c, NpgsqlTransaction t, GameState p, ushort body, ushort face, ushort hair)
        {
            AssertSaved(c, t, p);
            if (Q.Execute(c, t, "UPDATE entities SET body=@body,face=@face,hairstyle=@hair WHERE uid=@uid",
                "body", (int)body, "face", (int)face, "hair", (int)hair, "uid", (long)p.Entity.UID) != 1)
                throw new InvalidOperationException("Appearance update was not saved.");
        }
        internal static void ApplyCommitted(GameState p, ushort body, ushort face, ushort hair)
        {
            Require(p);
            images.Remove(p); images.Add(p, new Image { Entity = p.Entity, Body = body, Face = face, Hair = hair });
            if (p.Entity.Body != body) {
                p.Entity.Body = body;
                if(p.Entity.MyFlowers!=null)EntityTable.SetFlowers(p);
            }
            if (p.Entity.Face != face) p.Entity.Face = face;
            if (p.Entity.HairStyle != hair) p.Entity.HairStyle = hair;
        }
    }
}
