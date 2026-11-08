using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Client;

namespace Revora.Database
{
    // Legacy LastLogin is the last successful character save, not an admission log.
    internal static class CharacterActivity
    {
        internal static Func<DateTime> Clock = () => DateTime.Now;
        private sealed class Image { internal Revora.Game.Entity Entity; internal long? Saved; }
        private static readonly ConditionalWeakTable<GameState, Image> images = new ConditionalWeakTable<GameState, Image>();
        internal static void Forget(GameState p) { images.Remove(p); }
        internal static void Loaded(GameState p, long? saved)
        {
            Forget(p); images.Add(p, new Image { Entity = p.Entity, Saved = saved });
            p.Entity.LastLogin = Decode(saved);
        }
        internal static DateTime PrepareSave(GameState p, MySqlCommand command)
        {
            if (!Monitor.IsEntered(p.ItemSyncRoot ?? p)) throw new InvalidOperationException("Activity requires its owner lock.");
            Image image; long? saved;
            if (images.TryGetValue(p, out image) && ReferenceEquals(image.Entity, p.Entity)) saved = image.Saved;
            else
            {
                using (var q = new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("uid", p.Entity.UID))
                using (var r = new MySqlReader(q))
                {
                    if (!r.Read()) throw new InvalidOperationException("Activity owner missing.");
                    saved = r.ReadNullableInt64Strict("LastLogin");
                }
                if (Decode(saved) != p.Entity.LastLogin) throw new InvalidOperationException("Activity history was not loaded.");
            }
            var observed = Observe(p.Entity.LastLogin);
            command.AndInt64Parameter("LastLogin", saved).Set("LastLogin", observed.Ticks);
            return observed;
        }
        internal static void Committed(GameState p, DateTime observed)
        {
            Loaded(p, observed.Ticks);
            if (p.AsMember != null) p.AsMember.LastLogin = (ulong)observed.Ticks;
        }
        private static readonly long TickFloor = new DateTime(2000, 1, 1).Ticks;

        internal static DateTime Decode(long? stored)
        {
            if (!stored.HasValue || stored.Value <= 0) return DateTime.MinValue;
            long value = stored.Value;
            if (value >= TickFloor && value <= DateTime.MaxValue.Ticks)
                return new DateTime(value, DateTimeKind.Local);
            DateTime legacy;
            if (value >= 20000101000000L && value <= 99991231235959L
                && DateTime.TryParseExact(value.ToString(CultureInfo.InvariantCulture),
                    "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out legacy))
                return legacy;
            return DateTime.MinValue;
        }

        internal static DateTime Observe(DateTime previous)
        {
            var now = Clock();
            if (now.Kind == DateTimeKind.Utc) now = now.ToLocalTime();
            else now = DateTime.SpecifyKind(now, DateTimeKind.Local);
            // A clock rollback cannot make a recently active player look older.
            return previous > now ? previous : now;
        }
    }
}
