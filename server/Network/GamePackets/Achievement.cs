using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Game;
using Revora.Game.Features;

namespace Revora.Network.GamePackets
{
    public unsafe class Achievement : Writer
    {
        public const byte File_Size = 13;
        private readonly Entity entity;
        private readonly object sync = new object();
        private HashSet<int> verified = new HashSet<int>();
        private List<ProgressionEvidence.Award> awards = new List<ProgressionEvidence.Award>();
        private string legacyWords = "";
        private bool loaded;
        private DateTime nextRead;
        private sealed class RequestWindow { internal DateTime Until; internal int Count; }
        private static readonly ConditionalWeakTable<Client.GameState, RequestWindow> Requests = new ConditionalWeakTable<Client.GameState, RequestWindow>();

        public Achievement(Entity owner)
        {
            if (owner == null) throw new ArgumentNullException("owner");
            entity = owner;
        }

        public int CompletedCount { get { lock (sync) return verified.Count; } }

        // Legacy bits are archival input, never a trusted completion API.
        public void Load(string raw)
        {
            lock (sync) {
                if (loaded) throw new InvalidOperationException("Progression evidence is already loaded.");
                legacyWords = raw ?? "";
            }
        }

        internal void Invalidate() { lock (sync) nextRead = DateTime.MinValue; }

        internal void Refresh(DateTime utc, bool force)
        {
            var player = entity.Owner;
            if (player == null || player.Fake || player.ItemSyncRoot == null || entity.UID == 0
                || !entity.FullyLoaded || entity.PersistenceRequiresReload) return;
            if (utc.Kind != DateTimeKind.Utc) throw new ArgumentException("Progression clock must be UTC.");
            lock (player.ItemSyncRoot) {
                int[] added;
                string[] notices;
                lock (sync) {
                    var observations = AchievementCatalog.Observe(player).Where(id => !verified.Contains(id)).ToArray();
                    if (loaded && !force && utc < nextRead && observations.Length == 0) return;
                    string originRealm;
                    Guid originOwner;
                    OriginCtf.TryAuthenticated(player, out originRealm, out originOwner);
                    var next = ProgressionEvidence.ReadOrigin(entity.UID, legacyWords, !loaded, observations, utc, originRealm, originOwner);
                    // Install only after the archive/completions/read transaction commits.
                    added = loaded ? next.Achievements.Except(verified).ToArray() : new int[0];
                    verified = next.Achievements; awards = next.Awards;notices=next.Notices.ToArray();
                    loaded = true; nextRead = utc.AddSeconds(30); legacyWords = "";
                }
                foreach(string notice in notices)player.Send(notice);
                foreach (int id in added) Notify(id);
                if (added.Length != 0) player.Send(ToArray());
            }
        }

        internal int[] AwardKeys(DateTime utc)
        {
            lock (sync) return awards.Where(a => a.Active(utc)).Select(a => a.Key).Distinct().ToArray();
        }

        internal DateTime? AwardExpiry(int key, DateTime utc)
        {
            lock(sync) {
                var active=awards.Where(a=>a.Key==key && a.Active(utc)).ToArray();
                // A permanent award must not be shortened by a concurrent rental.
                return active.Any(a=>!a.ExpiresUtc.HasValue) ? null : active.Select(a=>a.ExpiresUtc).Max();
            }
        }

        public void Send()
        {
            Refresh(DateTime.UtcNow, false);
            if (entity.Owner != null) entity.Owner.Send(ToArray());
        }

        private void Notify(int id)
        {
            byte[] packet = new byte[28];
            WriteUInt16(20, 0, packet); WriteUInt16(1136, 2, packet);
            WriteUInt32(2, 4, packet); WriteUInt32(entity.UID, 8, packet); WriteUInt32((uint)id, 12, packet);
            entity.Owner.Send(packet);
        }

        public override string ToString()
        {
            lock (sync) return string.Join("#", AchievementCatalog.Words(verified).Select(w => w.ToString(CultureInfo.InvariantCulture))) + "#";
        }

        public byte[] ToArray()
        {
            byte[] packet = new byte[76];
            WriteUInt16(68, 0, packet); WriteUInt16(1136, 2, packet);
            WriteUInt32(entity.UID, 8, packet); WriteUInt32(File_Size, 12, packet);
            lock (sync) {
                uint[] words = AchievementCatalog.Words(verified);
                for (int i = 0; i < words.Length; i++) WriteUInt32(words[i], (ushort)(16 + 4 * i), packet);
            }
            return packet;
        }

        public byte[] ViewOpen()
        {
            byte[] packet = ToArray(); WriteUInt32(1, 4, packet); return packet;
        }

        internal static bool IsReadRequest(byte[] packet)
        {
            if (packet == null || packet.Length < 12 || packet.Length > 76) return false;
            int declared = BitConverter.ToUInt16(packet, 0);
            return declared >= 12 && declared <= 68 && (packet.Length == declared || packet.Length == declared + 8)
                && BitConverter.ToUInt16(packet, 2) == 1136 && BitConverter.ToUInt32(packet, 4) == 1;
        }

        public static void Process(byte[] packet, Client.GameState client)
        {
            // Action 2 is deliberately not accepted: the client cannot attest to
            // a quest, balance, tournament result or any other achievement.
            if (!IsReadRequest(packet) || client == null || client.Fake || client.Action != 2
                || client.Entity == null || !client.Entity.FullyLoaded || client.Entity.PersistenceRequiresReload) return;
            var window = Requests.GetValue(client, _ => new RequestWindow());
            lock (window) {
                DateTime utc = DateTime.UtcNow;
                if (utc >= window.Until) { window.Until = utc.AddSeconds(1); window.Count = 0; }
                if (++window.Count > 8) return;
            }
            uint uid = BitConverter.ToUInt32(packet, 8);
            Client.GameState target;
            if (uid == client.Entity.UID) target = client;
            else if (!Kernel.GamePool.TryGetValue(uid, out target)) return;
            if (target == null || target.Fake || target.Entity == null || !target.Entity.FullyLoaded
                || target.Entity.MyAchievement == null) return;
            client.Send(target.Entity.MyAchievement.ViewOpen());
        }
    }
}
