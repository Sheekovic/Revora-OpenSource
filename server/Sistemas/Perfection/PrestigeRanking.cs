using System;
using System.Collections.Generic;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.ConquerStructures;
using Revora.Network.GamePackets;
using Npgsql;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using JiangHu = Revora.Game.JiangHu;

namespace Revora
{
    // A read-only scoring input: no account, socket, currency setter or login lifecycle.
    public sealed class PrestigeProfile
    {
        public uint UID, Mesh, HairStyle, Title, CosmeticWing;
        internal bool CosmeticWingLifetimeVerified;
        internal DateTime? CosmeticWingExpiresUtc;
        public string Name = "", Guild = "";
        public byte Level, Class, Reborn;
        public bool AlternateEquipment;
        public int Attributes, SubclassCount, NobilityRank;
        public ChiTable.ChiData Chi;
        public JiangHu Jiang;
        public InnerPower InnerPower;
        public List<ConquerItem> Items = new List<ConquerItem>();
        public List<ConquerItem> Wardrobe = new List<ConquerItem>();
        public List<ConquerItem> Runes = new List<ConquerItem>();

        public static PrestigeProfile FromClient(GameState client)
        {
            if (client == null || client.Entity == null || client.Entity.UID == 0)
                throw new InvalidOperationException("Prestige requires an initialized character.");
            var e = client.Entity;
            var profile = new PrestigeProfile {
                UID = e.UID, Name = e.Name, Level = e.Level, Class = e.Class, Reborn = e.Reborn,
                Mesh = e.Mesh, HairStyle = e.HairStyle, Title = (uint)e.MyTitle,
                CosmeticWing = (uint)Math.Max(0, e.EquippedWing),
                Guild = client.Guild == null ? "" : client.Guild.Name,
                Attributes = e.Strength + e.Agility + e.Vitality + e.Spirit,
                SubclassCount = e.SubClasses == null ? 0 : e.SubClasses.Classes.Count,
                NobilityRank = (int)e.NobilityRank, Chi = client.ChiData, Jiang = e.MyJiang,
                InnerPower = client.InnerPower,
                AlternateEquipment = client.AlternateEquipment,
                Items = client.Equipment.GetItems().Select(CopyItem).ToList(),
                Runes = client.RunesItem == null ? new List<ConquerItem>() : client.RunesItem.Values.Where(RuneProgress.Equipped).Select(CopyItem).ToList(),
                Wardrobe = e.StorageItems == null ? new List<ConquerItem>() : e.StorageItems.Values.Select(CopyItem).ToList()
            };
            var utc=DateTime.UtcNow;
            if (LifetimeWing(profile.CosmeticWing) && e.MyAchievement!=null
                && e.MyAchievement.AwardKeys(utc).Contains((int)profile.CosmeticWing)) {
                profile.CosmeticWingLifetimeVerified=true;
                profile.CosmeticWingExpiresUtc=e.MyAchievement.AwardExpiry((int)profile.CosmeticWing,utc);
            }
            return profile;
        }
        // These event designs have expiring editions; Romance also has a
        // permanent champion edition. A saved selection is not a lifetime grant.
        internal static bool LifetimeWing(uint key) {
            return key==60010020 || key==60096009 || key==60126012 || key==60186018;
        }
        internal uint VisibleCosmeticWing(DateTime utc) {
            if(LifetimeWing(CosmeticWing) && (!CosmeticWingLifetimeVerified
                || CosmeticWingExpiresUtc.HasValue && utc>=CosmeticWingExpiresUtc.Value))return 0;
            return CosmeticWing;
        }
        internal void ReadWingAward(string source,int key,DateTime earned,DateTime? expiry,DateTime utc) {
            int days;
            if(key!=CosmeticWing || !LifetimeWing(CosmeticWing) || earned>utc || expiry.HasValue && expiry.Value<=utc
                || !Game.Features.ProgressionEvidence.AwardRule(source,key,out days)
                || (days==0 ? expiry.HasValue : !expiry.HasValue || expiry.Value!=earned.AddDays(days)))return;
            if(CosmeticWingLifetimeVerified && !CosmeticWingExpiresUtc.HasValue)return;
            if(!CosmeticWingLifetimeVerified || !expiry.HasValue || expiry>CosmeticWingExpiresUtc)CosmeticWingExpiresUtc=expiry;
            CosmeticWingLifetimeVerified=true;
        }
        internal static uint SavedCosmeticWing(byte[] data)
        {
            var wardrobe = new WardrobeTitles { Data = data };
            return wardrobe.Records().Where(r => r.Value
                && StorageManager.Wing<bool>(r.Key / 10000, r.Key % 10000))
                .Select(r => (uint)r.Key).OrderBy(key => key).FirstOrDefault();
        }
        private static ConquerItem CopyItem(ConquerItem item)
        {
            return new ConquerItem(true) { UID = item.UID, ID = item.ID, Position = item.Position, StackSize = item.StackSize,
                Plus = item.Plus, Bless = item.Bless, Enchant = item.Enchant, SocketOne = item.SocketOne,
                SocketTwo = item.SocketTwo, PerfectionLevel = item.PerfectionLevel,
                RefineItem = item.RefineItem, Purification = item.Purification, Warehouse = item.Warehouse };
        }
    }

    public sealed class PrestigeEntry
    {
        public readonly PrestigeProfile Profile;
        public readonly UserAbilityScoreProto Abilities;
        public readonly uint Total;
        public PrestigeEntry(PrestigeProfile profile)
        {
            Profile = profile;
            Abilities = CMsgUserAbilityScore.Calculate(profile);
            Total = checked((uint)Abilities.Abilities.Sum(a => (long)a.Value));
        }
    }

    public static class PrestigeRanking
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<uint, PrestigeEntry> Entries = new Dictionary<uint, PrestigeEntry>();
        private static long refreshedAt;
        private sealed class QueryBudget { public long Started; public int Count; }
        private static readonly ConditionalWeakTable<GameState, QueryBudget> QueryBudgets = new ConditionalWeakTable<GameState, QueryBudget>();
        public static bool AllowQuery(GameState client)
        {
            if (client == null || client.Fake || client.Entity == null || !client.Entity.FullyLoaded) return false;
            var budget = QueryBudgets.GetValue(client, _ => new QueryBudget());
            lock (budget)
            {
                long now = Stopwatch.GetTimestamp();
                if (now - budget.Started >= Stopwatch.Frequency) { budget.Started = now; budget.Count = 0; }
                return ++budget.Count <= 8;
            }
        }
        public static bool IsPrestige(uint type) { return type >= 80000000 && type <= 80000010; }

        public static uint ClassCategory(byte profession)
        {
            switch (profession / 10)
            {
                case 1: return 80000001; // Trojan
                case 2: return 80000002; // Warrior
                case 4: return 80000003; // Archer
                case 5: return 80000004; // Ninja
                case 6: return 80000005; // Monk
                case 7: return 80000006; // Pirate
                case 8: return 80000007; // Dragon Warrior
                case 13: return 80000008; // Water Taoist
                case 14: return 80000009; // Fire Taoist
                case 16: return 80000010; // Windwalker
                default: return 0;
            }
        }

        public static PrestigeEntry Update(GameState client)
        {
            if (client == null || client.Fake || client.Entity == null || client.Equipment == null)
                return null;
            var entry = new PrestigeEntry(PrestigeProfile.FromClient(client));
            lock (Sync) Entries[entry.Profile.UID] = entry;
            return entry;
        }
        public static void RefreshSaved(GameState client)
        {
            // A failed derived score must not interrupt the rest of character persistence.
            try { Update(client); }
            catch (Exception ex)
            {
                Console.WriteLine("[prestige] snapshot-failed uid=" + (client.Entity == null ? 0 : client.Entity.UID)
                    + " error=" + ex.GetType().Name);
            }
        }

        public static PrestigeEntry[] Order(IEnumerable<PrestigeEntry> entries, uint category)
        {
            return entries.Where(e => category == 80000000 || ClassCategory(e.Profile.Class) == category)
                .OrderByDescending(e => e.Total).ThenByDescending(e => e.Profile.Level)
                .ThenBy(e => e.Profile.UID).ToArray();
        }

        public static PrestigeEntry[] Snapshot(GameState requester)
        {
            lock (Sync)
            {
                // Refresh other sessions at most once a second; always refresh the caller.
                // Offline entries survive logout and are rebuilt from saved data at startup.
                long now = Stopwatch.GetTimestamp();
                if (now - refreshedAt >= Stopwatch.Frequency)
                {
                    foreach (var player in Kernel.GamePool.Values.ToArray())
                        if (player != requester && !player.Fake && player.Entity != null && player.Entity.FullyLoaded)
                            Update(player);
                    refreshedAt = now;
                }
                Update(requester);
                return Order(Entries.Values, 80000000);
            }
        }

        public static void SendSelf(GameState client, uint category = 80000000)
        {
            var all = Snapshot(client);
            var self = all.FirstOrDefault(e => e.Profile.UID == client.Entity.UID);
            if (self == null) return;
            client.Send(PrestigePackets.Self(Order(all, category), self, category));
            client.Send(new CMsgUserAbilityScore(self.Abilities));
        }

        public static void SendPage(GameState client, uint category, ushort page)
        {
            var all = Snapshot(client);
            var self = all.FirstOrDefault(e => e.Profile.UID == client.Entity.UID);
            var ranked = Order(all, category);
            if (self != null) client.Send(PrestigePackets.Self(ranked, self, category));
            client.Send(PrestigePackets.Page(ranked, category, page));
        }

        public static void SendWall(GameState client)
        {
            var all = Snapshot(client);
            var self = all.FirstOrDefault(e => e.Profile.UID == client.Entity.UID);
            if (self != null)
            {
                client.Send(PrestigePackets.Self(all, self));
                client.Send(new CMsgUserAbilityScore(self.Abilities));
            }
            client.Send(PrestigePackets.Winners(all));
            client.Send(PrestigePackets.Appearance(all.FirstOrDefault()));
        }

        // Invoked once after gameplay tables load. Rebuild from durable character data,
        // without connecting offline players or calling loaders that delete expired items.
        public static void LoadSavedPlayers()
        {
            using (var connection = DataHolder.MySqlConnection)
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead))
                {
                    using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection)) readOnly.ExecuteNonQuery();
                    LoadSavedPlayers(connection);
                    transaction.Commit();
                }
            }
        }
        private static void LoadSavedPlayers(NpgsqlConnection connection)
        {
            var profiles = new Dictionary<uint, PrestigeProfile>();
            using (var cmd = new NpgsqlCommand("SELECT uid,name,level,class,reborn,face,body,hairstyle,my_title,strength,agility,vitality,spirit,guildid FROM entities", connection))
            using (var r = new SavedReader(cmd.ExecuteReader()))
                while (r.Read())
                {
                    var p = new PrestigeProfile {
                        UID = r.ReadUInt32("UID"), Name = r.ReadString("Name"),
                        Level = r.ReadByte("Level"), Class = r.ReadByte("Class"), Reborn = r.ReadByte("Reborn"),
                        Mesh = r.ReadUInt32("Face") * 10000 + r.ReadUInt32("Body"),
                        HairStyle = r.ReadUInt32("HairStyle"), Title = r.ReadUInt32("My_Title"),
                        Attributes = r.ReadUInt16("Strength") + r.ReadUInt16("Agility")
                            + r.ReadUInt16("Vitality") + r.ReadUInt16("Spirit")
                    };
                    if (p.UID == 0 || p.Level == 0 || String.IsNullOrEmpty(p.Name)) continue;
                    Revora.Game.ConquerStructures.Society.Guild guild;
                    if (Kernel.Guilds.TryGetValue(r.ReadUInt32("GuildID"), out guild)) p.Guild = guild.Name;
                    ChiTable.AllData.TryGetValue(p.UID, out p.Chi);
                    Revora.Interfaces.IJiangHu jiang;
                    if (JiangHu.JiangHuClients.TryGetValue(p.UID, out jiang)) p.Jiang = jiang as JiangHu;
                    InnerPower.InnerPowerPolle.TryGetValue(p.UID, out p.InnerPower);
                    NobilityInformation noble;
                    if (Nobility.Board.TryGetValue(p.UID, out noble)) p.NobilityRank = (int)noble.Rank;
                    else p.NobilityRank = (int)NobilityRank.Duke; // Same non-donor default as EntityTable.Load and NobilityInformation.
                    profiles.Add(p.UID, p);
                }
            using (var cmd = new NpgsqlCommand("SELECT id,data FROM titles", connection))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    PrestigeProfile p;
                    if (profiles.TryGetValue(Convert.ToUInt32(r[0]), out p))
                        p.CosmeticWing = PrestigeProfile.SavedCosmeticWing(r.IsDBNull(1) ? new byte[0] : (byte[])r[1]);
                }
            using (var cmd = new NpgsqlCommand(@"SELECT uid,title_key,source,earned_at,expires_at FROM revora_event_awards
WHERE NOT revoked AND title_key IN(60010020,60096009,60126012,60186018)
AND earned_at<=@now AND (expires_at IS NULL OR expires_at>@now)", connection)) {
                var utc=DateTime.UtcNow;cmd.Parameters.Add("now",NpgsqlTypes.NpgsqlDbType.TimestampTz).Value=utc;
                using(var r=cmd.ExecuteReader())while(r.Read()) {
                    PrestigeProfile p;
                    if(profiles.TryGetValue(Convert.ToUInt32(r[0]),out p))
                        p.ReadWingAward(r.GetString(2),r.GetInt32(1),r.GetFieldValue<DateTimeOffset>(3).UtcDateTime,
                            r.IsDBNull(4)?(DateTime?)null:r.GetFieldValue<DateTimeOffset>(4).UtcDateTime,utc);
                }
            }
            using (var cmd = new NpgsqlCommand("SELECT id FROM subclasses", connection))
            using (var r = new SavedReader(cmd.ExecuteReader()))
                while (r.Read())
                {
                    PrestigeProfile p;
                    if (profiles.TryGetValue(r.ReadUInt32("id"), out p)) p.SubclassCount++;
                }
            var items = new Dictionary<uint, ConquerItem>();
            using (var cmd = new NpgsqlCommand("SELECT entityid,position,warehouse,inwardrobe,uid,id,plus,bless,enchant,socketone,sockettwo,perfectionlevel,refineryitem,stacksize FROM items", connection))
            using (var r = new SavedReader(cmd.ExecuteReader()))
                while (r.Read())
                {
                    PrestigeProfile p;
                    if (!profiles.TryGetValue(r.ReadUInt32("EntityID"), out p)) continue;
                    var position = r.ReadByte("Position");
                    bool equipped = r.ReadUInt32("Warehouse") == 0 && position > 0 && position <= 29;
                    bool rune = r.ReadUInt32("Warehouse") == 0 && RuneProgress.IsRune(r.ReadUInt32("ID"))
                        && (position >= 101 && position <= 108 || position >= 121 && position <= 125);
                    bool wardrobe = r.ReadBoolean("InWardrobe");
                    if (!equipped && !wardrobe && !rune) continue;
                    var item = new ConquerItem(true) {
                        UID = r.ReadUInt32("UID"), ID = r.ReadUInt32("ID"), Position = position, StackSize = r.ReadUInt16("stacksize"),
                        Plus = r.ReadByte("Plus"), Bless = r.ReadByte("Bless"), Enchant = r.ReadByte("Enchant"),
                        SocketOne = (Enums.Gem)r.ReadUInt16("SocketOne"), SocketTwo = (Enums.Gem)r.ReadUInt16("SocketTwo"),
                        PerfectionLevel = r.ReadUInt32("PerfectionLevel"), RefineItem = r.ReadUInt32("RefineryItem")
                    };
                    if (!ConquerItemInformation.BaseInformations.ContainsKey(item.ID)) continue;
                    if (rune && RuneProgress.Equipped(item)) p.Runes.Add(item);
                    if (equipped && !p.Items.Any(i => i.Position == position)) p.Items.Add(item);
                    if (wardrobe) p.Wardrobe.Add(item);
                    items[item.UID] = item;
                }
            using (var cmd = new NpgsqlCommand("SELECT addingtype,uid,duration,addedon,addingid,addinglevel FROM itemadding", connection))
            using (var r = new SavedReader(cmd.ExecuteReader()))
                while (r.Read())
                {
                    ConquerItem item;
                    if (r.ReadInt32("Addingtype") != 0 || !items.TryGetValue(r.ReadUInt32("UID"), out item)) continue;
                    uint duration = r.ReadUInt32("Duration");
                    var added = DateTime.FromBinary(r.ReadInt64("Addedon"));
                    if (duration != 0 && added.AddSeconds(duration) <= DateTime.Now) continue;
                    item.Purification = new ItemAdding.Purification_ {
                        ItemUID = item.UID, Available = true, PurificationItemID = r.ReadUInt32("Addingid"),
                        PurificationLevel = r.ReadUInt32("Addinglevel"), AddedOn = added, PurificationDuration = duration
                    };
                }
            var saved = profiles.Values.Select(p => new PrestigeEntry(p)).ToArray();
            lock (Sync)
            {
                Entries.Clear();
                foreach (var entry in saved) Entries.Add(entry.Profile.UID, entry);
            }
            Console.WriteLine("[prestige] loaded saved character scores: " + saved.Length);
        }
        private sealed class SavedReader : IDisposable
        {
            private readonly NpgsqlDataReader reader;
            public SavedReader(NpgsqlDataReader reader) { this.reader = reader; }
            public bool Read() { return reader.Read(); }
            private object Value(string name) { var v = reader[name.ToLowerInvariant()]; return v == DBNull.Value ? null : v; }
            public uint ReadUInt32(string name) { return Convert.ToUInt32(Value(name)); }
            public ushort ReadUInt16(string name) { return Convert.ToUInt16(Value(name)); }
            public byte ReadByte(string name) { return Convert.ToByte(Value(name)); }
            public int ReadInt32(string name) { return Convert.ToInt32(Value(name)); }
            public long ReadInt64(string name) { return Convert.ToInt64(Value(name)); }
            public bool ReadBoolean(string name) { return Convert.ToBoolean(Value(name)); }
            public string ReadString(string name) { return Convert.ToString(Value(name)); }
            public void Dispose() { reader.Dispose(); }
        }
    }
}
