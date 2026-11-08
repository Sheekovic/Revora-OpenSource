using System;
using Npgsql;
using Revora.Game.Features;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Database
{
    // One transaction owns the new character, the account pointer, the identity
    // counter and the starter-gift receipt. A per-character session lease cannot
    // see another proposed id for the same account.
    internal static class CharacterCreation
    {
        internal const ushort StartMap = 1002, StartX = 327, StartY = 303;
        internal static Action<NpgsqlConnection, NpgsqlTransaction> BeforeCreateCommit = null;
        internal static Action<NpgsqlConnection, NpgsqlTransaction> BeforePacksCommit = null;
        internal static Action AfterPacksCommit = null;
        internal static Action<NpgsqlConnection, NpgsqlTransaction> BeforeGearCommit = null;
        internal static Action AfterGearCommit = null;
        const string EnsureSql = @"CREATE TABLE IF NOT EXISTS revora_character_creation(
account_name text PRIMARY KEY,
character_uid bigint NOT NULL UNIQUE,
character_name text NOT NULL,
starter_issued boolean NOT NULL DEFAULT false,
created_at timestamptz NOT NULL DEFAULT clock_timestamp());
ALTER TABLE revora_character_creation ADD COLUMN IF NOT EXISTS packs_issued boolean NOT NULL DEFAULT false;
ALTER TABLE revora_character_creation ADD COLUMN IF NOT EXISTS revora_starter_pending boolean NOT NULL DEFAULT false;";

        internal enum Outcome { Saved, NameTaken, AccountTaken, Failed }

        internal static Outcome Commit(Client.GameState client, string name, uint uid, uint startCps)
        {
            if (client == null || client.Account == null || client.Entity == null || uid == 0
                || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(client.Account.Username))
                return Outcome.Failed;
            using (var connection = DataHolder.MySqlConnection)
            {
                connection.Open();
                using (var tx = connection.BeginTransaction())
                {
                    try
                    {
                        Ensure(connection, tx);
                        using (var accountGate = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext('revora-create-account:' || lower(@name)))", connection, tx))
                        {
                            accountGate.Parameters.AddWithValue("name", client.Account.Username);
                            accountGate.ExecuteNonQuery();
                        }
                        using (var nameGate = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext('revora-create-name:' || lower(@character)))", connection, tx))
                        {
                            nameGate.Parameters.AddWithValue("character", name);
                            nameGate.ExecuteNonQuery();
                        }
                        long existingId = 0;
                        int accounts = 0;
                        using (var q = new NpgsqlCommand("SELECT entityid FROM accounts WHERE lower(username)=lower(@name) FOR UPDATE", connection, tx))
                        {
                            q.Parameters.AddWithValue("name", client.Account.Username);
                            using (var reader = q.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    accounts++;
                                    existingId = reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0));
                                }
                                if (reader.Read()) accounts++;
                            }
                        }
                        if (accounts != 1) return Outcome.Failed;
                        long receiptUid = 0;
                        bool haveReceipt = false;
                        using (var q = new NpgsqlCommand("SELECT character_uid FROM revora_character_creation WHERE account_name=lower(@name) FOR UPDATE", connection, tx))
                        {
                            q.Parameters.AddWithValue("name", client.Account.Username);
                            object got = q.ExecuteScalar();
                            if (got != null && got != DBNull.Value)
                            {
                                haveReceipt = true;
                                receiptUid = Convert.ToInt64(got);
                            }
                        }
                        if (haveReceipt)
                        {
                            if (receiptUid != (long)uid || existingId != (long)uid) return Outcome.AccountTaken;
                            using (var q = Q.Command(connection, tx, @"SELECT 1 FROM entities e
JOIN revora_character_creation r ON r.character_uid=e.uid
WHERE e.uid=@uid AND lower(e.owner)=lower(@owner) AND e.name=@character AND r.character_name=e.name",
                                "uid", (long)uid, "owner", client.Account.Username, "character", name))
                                if (q.ExecuteScalar() == null) return Outcome.Failed;
                            tx.Commit();
                            client.Account.EntityID = uid;
                            client.Entity.UID = uid;
                            return Outcome.Saved;
                        }
                        if (existingId != 0 && existingId != (long)uid) return Outcome.AccountTaken;
                        using (var q = new NpgsqlCommand("SELECT uid FROM entities WHERE lower(name)=lower(@character) FOR UPDATE", connection, tx))
                        {
                            q.Parameters.AddWithValue("character", name);
                            using (var reader = q.ExecuteReader())
                            {
                                if (reader.Read() && Convert.ToInt64(reader.GetValue(0)) != (long)uid)
                                    return Outcome.NameTaken;
                            }
                        }
                        using (var q = new NpgsqlCommand("SELECT 1 FROM entities WHERE uid=@uid", connection, tx))
                        {
                            q.Parameters.AddWithValue("uid", (long)uid);
                            if (q.ExecuteScalar() != null) return Outcome.Failed;
                        }
                        using (var insert = new NpgsqlCommand(@"INSERT INTO entities(
""name"",""owner"",""class"",""uid"",""level"",""hitpoints"",""mana"",""body"",""face"",""hairstyle"",
""strength"",""warehousepw"",""agility"",""vitality"",""spirit"",""windwalker"",""onlinepoints"",
""conquerpoints"",""claimedteampk"",""claimedsteampk"",""firstcredit"",""claimedelitepk"",""cursedtime"",
""swordsoul"",""bansheespirit"",""claimedexp"",""vipleveldays"",""viplevel"",""mapid"",""x"",""y"")
VALUES(@name,@owner,@class,@uid,1,@hitpoints,@mana,@body,@face,@hair,
@strength,'',@agility,@vitality,@spirit,@wind,@onlinepoints,
@cps,0,0,0,0,0,0,0,0,0,4,@map,@x,@y)", connection, tx))
                        {
                            insert.Parameters.AddWithValue("name", name);
                            insert.Parameters.AddWithValue("owner", client.Account.Username);
                            insert.Parameters.AddWithValue("class", (int)client.Entity.Class);
                            insert.Parameters.AddWithValue("uid", (long)uid);
                            insert.Parameters.AddWithValue("hitpoints", Convert.ToInt32(client.Entity.Hitpoints));
                            insert.Parameters.AddWithValue("mana", Convert.ToInt32(client.Entity.Mana));
                            insert.Parameters.AddWithValue("body", Convert.ToInt32(client.Entity.Body));
                            insert.Parameters.AddWithValue("face", Convert.ToInt32(client.Entity.Face));
                            insert.Parameters.AddWithValue("hair", Convert.ToInt32(client.Entity.HairStyle));
                            insert.Parameters.AddWithValue("strength", Convert.ToInt32(client.Entity.Strength));
                            insert.Parameters.AddWithValue("agility", Convert.ToInt32(client.Entity.Agility));
                            insert.Parameters.AddWithValue("vitality", Convert.ToInt32(client.Entity.Vitality));
                            insert.Parameters.AddWithValue("spirit", Convert.ToInt32(client.Entity.Spirit));
                            insert.Parameters.AddWithValue("wind", Convert.ToInt32(client.Entity.Windwalker));
                            insert.Parameters.AddWithValue("onlinepoints", 0);
                            insert.Parameters.AddWithValue("cps", (long)startCps);
                            insert.Parameters.AddWithValue("map", (int)StartMap);
                            insert.Parameters.AddWithValue("x", (int)StartX);
                            insert.Parameters.AddWithValue("y", (int)StartY);
                            if (insert.ExecuteNonQuery() != 1) return Outcome.Failed;
                        }
                        using (var config = new NpgsqlCommand("UPDATE configuration SET entityid=GREATEST(entityid::bigint, @uid) WHERE lower(server)=lower(@server)", connection, tx))
                        {
                            config.Parameters.AddWithValue("uid", (long)uid);
                            config.Parameters.AddWithValue("server", Constants.ServerName ?? "");
                            if (config.ExecuteNonQuery() != 1) return Outcome.Failed;
                        }
                        using (var account = new NpgsqlCommand(@"UPDATE accounts SET entityid=@uid, ip=@ip
WHERE lower(username)=lower(@name) AND (entityid IS NULL OR entityid=0 OR entityid=@uid)", connection, tx))
                        {
                            account.Parameters.AddWithValue("uid", (long)uid);
                            account.Parameters.AddWithValue("ip", client.Account.IP ?? "");
                            account.Parameters.AddWithValue("name", client.Account.Username);
                            if (account.ExecuteNonQuery() != 1) return Outcome.AccountTaken;
                        }
                        using (var receipt = new NpgsqlCommand(@"INSERT INTO revora_character_creation(account_name,character_uid,character_name,revora_starter_pending)
VALUES(lower(@name),@uid,@character,true)", connection, tx))
                        {
                            receipt.Parameters.AddWithValue("name", client.Account.Username);
                            receipt.Parameters.AddWithValue("uid", (long)uid);
                            receipt.Parameters.AddWithValue("character", name);
                            if (receipt.ExecuteNonQuery() != 1) return Outcome.Failed;
                        }
                        var hook = BeforeCreateCommit;
                        if (hook != null) hook(connection, tx);
                        tx.Commit();
                        client.Account.EntityID = uid;
                        client.Entity.UID = uid;
                        return Outcome.Saved;
                    }
                    catch (Exception ex)
                    {
                        try { tx.Rollback(); } catch (Exception) { }
                        Console.WriteLine("[game] create-insert-fail uid=" + uid + " " + ex.GetType().Name);
                        return Outcome.Failed;
                    }
                }
            }
        }

        internal static bool StarterOwed(Client.GameState client)
        {
            if (client == null || client.Account == null || client.Entity == null || client.Entity.UID == 0
                || string.IsNullOrEmpty(client.Account.Username)) return false;
            try
            {
                using (var connection = DataHolder.MySqlConnection)
                {
                    connection.Open();
                    Ensure(connection, null);
                    using (var q = new NpgsqlCommand(@"SELECT starter_issued FROM revora_character_creation
WHERE account_name=lower(@name) AND character_uid=@uid", connection))
                    {
                        q.Parameters.AddWithValue("name", client.Account.Username);
                        q.Parameters.AddWithValue("uid", (long)client.Entity.UID);
                        object issued = q.ExecuteScalar();
                        return issued != null && issued != DBNull.Value && Equals(issued, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[game] starter-owed " + ex.GetType().Name);
                return false;
            }
        }

        // Retire legacy pack issuance without creating items. Keep packs_issued as
        // the compatibility prerequisite for class gear. The separate Revora pack
        // entitlement remains pending until the owner defines its rewards.
        internal static bool RetireLegacyStarterPacks(Client.GameState client)
        {
            if (client == null || client.Account == null || client.Entity == null || client.Inventory == null
                || client.ItemSyncRoot == null || !SessionInstall.Holds(client.Entity.UID)
                || !SessionInstall.IsCurrent(client.Entity.UID, client)) return false;
            lock (client.ItemSyncRoot)
            {
                bool commitAttempted = false;
                try
                {
                    using (var c = DataHolder.MySqlConnection)
                    {
                        c.Open();
                        using (var t = c.BeginTransaction())
                        {
                            Ensure(c, t);
                            using (var q = Q.Command(c, t, "SELECT uid FROM entities WHERE uid=@uid AND lower(owner)=lower(@name) FOR UPDATE",
                                "uid", (long)client.Entity.UID, "name", client.Account.Username))
                                if (q.ExecuteScalar() == null) return false;
                            using (var q = Q.Command(c, t, @"SELECT packs_issued OR starter_issued FROM revora_character_creation
WHERE account_name=lower(@name) AND character_uid=@uid FOR UPDATE", "name", client.Account.Username, "uid", (long)client.Entity.UID))
                            {
                                object issued = q.ExecuteScalar();
                                if (issued == null || issued == DBNull.Value) return false;
                                if (Equals(issued, true)) return true;
                            }
                            if (Q.Execute(c, t, @"UPDATE revora_character_creation SET packs_issued=true
WHERE account_name=lower(@name) AND character_uid=@uid AND NOT packs_issued",
                                "name", client.Account.Username, "uid", (long)client.Entity.UID) != 1)
                                throw new InvalidOperationException("Starter pack receipt changed.");
                            var before = BeforePacksCommit;
                            if (before != null) before(c, t);
                            if (!SessionInstall.IsCurrent(client.Entity.UID, client)) return false;
                            commitAttempted = true;
                            t.Commit();
                        }
                    }
                    var after = AfterPacksCommit;
                    if (after != null) after();
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[game] starter-packs " + ex.GetType().Name);
                    if (commitAttempted)
                    {
                        client.Entity.PersistenceRequiresReload = true;
                        throw; // Login retires this projection; reload the committed rows.
                    }
                    return false;
                }
            }
        }

        struct KitPiece
        {
            internal uint Id;
            internal byte Position;
            internal ushort Durability;
            internal bool Bound;
            internal KitPiece(uint id, byte position, ushort durability, bool bound)
            {
                Id = id; Position = position; Durability = durability; Bound = bound;
            }
        }

        // Null means this class has no configured starter kit. An empty result is not used.
        static KitPiece[] KitFor(byte cls)
        {
            if (cls >= 10 && cls <= 11) return Pair(130006, 410009);
            if (cls >= 20 && cls <= 21) return Pair(131006, 410009);
            if (cls >= 40 && cls <= 41) return Pair(133006, 500009);
            if (cls >= 50 && cls <= 51) return Pair(135006, 601009);
            if (cls >= 60 && cls <= 61) return Pair(136006, 610009);
            if (cls >= 70 && cls <= 71) return Pair(139006, 611009);
            if (cls >= 80 && cls <= 81) return Pair(138006, 617009);
            if (cls >= 100 && cls <= 145) return Pair(134006, 421009);
            if (cls >= 160 && cls <= 165)
                return new[] {
                    new KitPiece(170006, Network.GamePackets.ConquerItem.Head, 3599, false),
                    new KitPiece(101009, Network.GamePackets.ConquerItem.Armor, 3599, false),
                    new KitPiece(626006, Network.GamePackets.ConquerItem.RightWeapon, 599, false),
                    new KitPiece(626006, Network.GamePackets.ConquerItem.LeftWeapon, 1599, false) };
            return null;
        }

        static KitPiece[] Pair(uint armor, uint weapon)
        {
            return new[] {
                new KitPiece(armor, Network.GamePackets.ConquerItem.Armor, 3599, true),
                new KitPiece(weapon, Network.GamePackets.ConquerItem.RightWeapon, 1599, true) };
        }

        // The complete class kit and starter_issued commit together. A consumed or
        // moved kit is not rebuilt once that receipt is set, and a partial set is
        // not marked complete.
        internal static bool TryIssueStarterGear(Client.GameState client)
        {
            if (client == null || client.Account == null || client.Entity == null || client.Equipment == null || client.ItemSyncRoot == null
                || !SessionInstall.Holds(client.Entity.UID) || !SessionInstall.IsCurrent(client.Entity.UID, client))
                return false;
            var kit = KitFor(client.Entity.Class);
            if (kit != null)
            {
                foreach (var piece in kit)
                    if (!ConquerItemInformation.BaseInformations.ContainsKey(piece.Id)) return false;
            }
            lock (client.ItemSyncRoot)
            {
                bool commitAttempted = false;
                Network.GamePackets.ConquerItem[] granted = null;
                try
                {
                    using (var c = DataHolder.MySqlConnection)
                    {
                        c.Open();
                        using (var t = c.BeginTransaction())
                        {
                            Ensure(c, t);
                            using (var q = Q.Command(c, t, "SELECT uid FROM entities WHERE uid=@uid AND lower(owner)=lower(@name) FOR UPDATE",
                                "uid", (long)client.Entity.UID, "name", client.Account.Username))
                                if (q.ExecuteScalar() == null) return false;
                            using (var q = Q.Command(c, t, @"SELECT starter_issued FROM revora_character_creation
WHERE account_name=lower(@name) AND character_uid=@uid FOR UPDATE",
                                "name", client.Account.Username, "uid", (long)client.Entity.UID))
                            {
                                object issued = q.ExecuteScalar();
                                if (issued == null || issued == DBNull.Value) return false;
                                if (Equals(issued, true)) return true;
                            }
                            int present = kit == null ? 0 : CountKit(c, t, client.Entity.UID, kit);
                            if (kit != null && present > 0 && present < kit.Length) return false;
                            if (kit != null && present == 0)
                            {
                                // An unrelated equipped item is not a starter entitlement,
                                // and inserting beside it would strand a second row in its slot.
                                foreach (var piece in kit)
                                {
                                    if (client.Equipment.TryGetItem(piece.Position) != null) return false;
                                    using (var q = Q.Command(c, t, @"SELECT 1 FROM items
WHERE entityid=@uid AND position=@position AND warehouse=0 AND COALESCE(inwardrobe,0)=0 LIMIT 1",
                                        "uid", (long)client.Entity.UID, "position", (int)piece.Position))
                                        if (q.ExecuteScalar() != null) return false;
                                }
                                granted = new Network.GamePackets.ConquerItem[kit.Length];
                                for (int i = 0; i < kit.Length; i++)
                                {
                                    var piece = kit[i];
                                    var item = new Network.GamePackets.ConquerItem(true)
                                    {
                                        ID = piece.Id,
                                        UID = Network.GamePackets.ConquerItem.ItemUID.Next,
                                        Plus = 0,
                                        Enchant = 0,
                                        Bless = 0,
                                        Bound = piece.Bound,
                                        Durability = piece.Durability,
                                        MaximDurability = piece.Durability,
                                        Position = piece.Position,
                                        Color = (Revora.Game.Enums.Color)Kernel.Random.Next(3, 9),
                                        StackSize = 1
                                    };
                                    if (Q.Execute(c, t, @"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability,plus,enchant,bless,color)
VALUES(@item,@type,@uid,@position,0,1,@bound,@dur,@max,0,0,0,@color)",
                                        "item", (long)item.UID, "type", (int)item.ID, "uid", (long)client.Entity.UID,
                                        "position", (int)item.Position, "bound", item.Bound ? 1 : 0,
                                        "dur", (int)item.Durability, "max", (int)item.MaximDurability, "color", (int)item.Color) != 1)
                                        throw new InvalidOperationException("Starter kit item was not saved.");
                                    granted[i] = item;
                                }
                            }
                            if (Q.Execute(c, t, @"UPDATE revora_character_creation SET starter_issued=true
WHERE account_name=lower(@name) AND character_uid=@uid AND packs_issued AND NOT starter_issued",
                                "name", client.Account.Username, "uid", (long)client.Entity.UID) != 1)
                                return false;
                            var before = BeforeGearCommit;
                            if (before != null) before(c, t);
                            if (!SessionInstall.IsCurrent(client.Entity.UID, client)) return false;
                            commitAttempted = true;
                            t.Commit();
                        }
                    }
                    var after = AfterGearCommit;
                    if (after != null) after();
                    if (granted != null)
                    {
                        foreach (var item in granted)
                        {
                            if (!client.Equipment.Add(item)
                                || !object.ReferenceEquals(client.Equipment.TryGetItem(item.Position), item))
                                throw new InvalidOperationException("Committed starter kit requires reload.");
                        }
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[game] starter-gear " + ex.GetType().Name);
                    if (commitAttempted)
                    {
                        client.Entity.PersistenceRequiresReload = true;
                        throw;
                    }
                    return false;
                }
            }
        }

        static int CountKit(NpgsqlConnection c, NpgsqlTransaction t, uint uid, KitPiece[] kit)
        {
            int present = 0;
            foreach (var piece in kit)
            {
                using (var q = Q.Command(c, t, @"SELECT 1 FROM items WHERE entityid=@uid AND id=@type AND position=@position AND bound=@bound LIMIT 1",
                    "uid", (long)uid, "type", (int)piece.Id, "position", (int)piece.Position, "bound", piece.Bound ? 1 : 0))
                    if (q.ExecuteScalar() != null) present++;
            }
            return present;
        }

        internal static bool HasGear(Client.GameState client)
        {
            if (client == null || client.Entity == null || client.Entity.UID == 0) return false;
            return ItemExists(client.Entity.UID, 0, true);
        }

        internal static void MarkStarterIssued(Client.GameState client)
        {
            if (client == null || client.Account == null || client.Entity == null || client.Entity.UID == 0
                || string.IsNullOrEmpty(client.Account.Username)) return;
            try
            {
                using (var connection = DataHolder.MySqlConnection)
                {
                    connection.Open();
                    Ensure(connection, null);
                    using (var q = new NpgsqlCommand(@"UPDATE revora_character_creation SET starter_issued=true
WHERE account_name=lower(@name) AND character_uid=@uid AND packs_issued AND NOT starter_issued", connection))
                    {
                        q.Parameters.AddWithValue("name", client.Account.Username);
                        q.Parameters.AddWithValue("uid", (long)client.Entity.UID);
                        q.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[game] starter-mark " + ex.GetType().Name);
            }
        }

        static bool ItemExists(uint uid, uint id, bool gear)
        {
            try
            {
                using (var connection = DataHolder.MySqlConnection)
                {
                    connection.Open();
                    string sql = gear
                        ? "SELECT 1 FROM items WHERE entityid=@uid AND position<>0 LIMIT 1"
                        : "SELECT 1 FROM items WHERE entityid=@uid AND id=@id LIMIT 1";
                    using (var q = new NpgsqlCommand(sql, connection))
                    {
                        q.Parameters.AddWithValue("uid", (long)uid);
                        if (!gear) q.Parameters.AddWithValue("id", (long)id);
                        return q.ExecuteScalar() != null;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static void Ensure(NpgsqlConnection connection, NpgsqlTransaction tx)
        {
            using (var cmd = tx == null ? new NpgsqlCommand(EnsureSql, connection) : new NpgsqlCommand(EnsureSql, connection, tx))
                cmd.ExecuteNonQuery();
        }
    }
}
