// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Npgsql;

namespace Revora.Database
{
    public unsafe class EntityVariableTable
    {
        public static void Load(Client.GameState client)
        {
            Load(client.Entity.UID, out client.Variables);
        }
        public static void Load(uint uid, out Game.VariableVault vars)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("variablevault").Where("entityid", uid))
            using (var reader = cmd.CreateReader())
            {
                vars = new Game.VariableVault();
                if (reader.Read())
                {
                    byte[] raw = reader.ReadBlob("data");
                    if (raw != null && raw.Length >= 4)
                        vars.Deserialize(raw);
                }
                else
                {
                    string SQL = "INSERT INTO `variablevault` (entityid, data) VALUES (@EntityID, @Data)";
                    byte[] rawData = vars.Serialize();
                    using (var conn = Database.DataHolder.MySqlConnection)
                    {
                        conn.Open();
                        using (var cmd2 = new Npgsql.NpgsqlCommand(Revora.Database.DataHolder.ToPostgres(SQL), conn))
                        {
                            DataHolder.AddParameter(cmd2, "@EntityID", uid);
                            DataHolder.AddParameter(cmd2, "@Data", rawData);
                            cmd2.ExecuteNonQuery();
                        }
                    }
                }
                vars.NoteSaved();
            }
        }
        // The caller owns the vault lock and installs the returned image only after commit.
        internal static byte[] PersistRewardVariables(Client.GameState client, NpgsqlConnection conn,
            NpgsqlTransaction tx, string[] names, Game.DynamicVariable[] values)
        {
            var vars = client.Variables;
            if (vars == null || !System.Threading.Monitor.IsEntered(vars.SyncRoot) || names == null || values == null || names.Length != values.Length)
                throw new InvalidOperationException("Quest reward variables require their owner lock.");
            byte[] before;
            using (var command = Game.Features.QuestInventory.Command(conn, tx,
                "SELECT data FROM variablevault WHERE entityid=@uid FOR UPDATE", "uid", (long)client.Entity.UID))
                before = command.ExecuteScalar() as byte[];
            byte[] merged;
            if (before == null || !vars.TryMerge(before, null, null, out merged))
                throw new InvalidOperationException("Quest reward variables changed.");
            var proposed = new Game.VariableVault(); proposed.Deserialize(merged);
            for (int i = 0; i < names.Length; i++)
            {
                if (proposed.ReadULong(names[i]) != vars.ReadULong(names[i]))
                    throw new InvalidOperationException("Quest reward progress changed.");
                proposed[names[i]] = values[i];
            }
            byte[] after = proposed.Serialize();
            if (Game.Features.QuestInventory.Execute(conn, tx,
                "UPDATE variablevault SET data=@data WHERE entityid=@uid AND data=@before",
                "data", after, "uid", (long)client.Entity.UID, "before", before) != 1)
                throw new InvalidOperationException("Quest reward progress was not saved.");
            return after;
        }
        public static void Save(Client.GameState client)
        {
            Save(client.Entity.UID, client.Variables);
        }
        public static void Save(uint uid, Game.VariableVault vars)
        {
            if (vars == null) return;
            lock (vars.SyncRoot)
            {
                if (!vars.Changed) return;
                using (var conn = DataHolder.MySqlConnection)
                {
                    conn.Open();
                    Save(uid, vars, conn);
                }
            }
        }
        internal static void Save(Client.GameState client, NpgsqlConnection conn)
        {
            Save(client.Entity.UID, client.Variables, conn);
        }
        private static void Save(uint uid, Game.VariableVault vars, NpgsqlConnection conn)
        {
            if (vars == null) return;
            lock (vars.SyncRoot)
            {
                if (!vars.Changed) return;
                using (var tx = conn.BeginTransaction())
                {
                    object scalar;
                    using (var q = new NpgsqlCommand("SELECT data FROM variablevault WHERE entityid=@EntityID FOR UPDATE", conn, tx))
                    {
                        DataHolder.AddParameter(q, "@EntityID", uid);
                        scalar = q.ExecuteScalar();
                    }
                    bool exists = scalar != null;
                    byte[] before = scalar as byte[];
                    byte[] merged;
                    if (!exists)
                    {
                        if (!vars.TryMerge(null, null, null, out merged))
                            throw new InvalidOperationException("Quest variable save conflicts with a newer stored value.");
                        using (var cmd = new NpgsqlCommand("INSERT INTO variablevault (entityid, data) VALUES (@EntityID, @Data)", conn, tx))
                        {
                            DataHolder.AddParameter(cmd, "@EntityID", uid);
                            DataHolder.AddParameter(cmd, "@Data", merged);
                            if (cmd.ExecuteNonQuery() != 1)
                                throw new InvalidOperationException("Quest variable save did not update its owner row.");
                        }
                    }
                    else
                    {
                        if (!vars.TryMerge(before, null, null, out merged))
                            throw new InvalidOperationException("Quest variable save conflicts with a newer stored value.");
                        string update = before == null
                            ? "UPDATE variablevault SET data=@Data WHERE entityid=@EntityID AND data IS NULL"
                            : "UPDATE variablevault SET data=@Data WHERE entityid=@EntityID AND data=@Before";
                        using (var cmd = new NpgsqlCommand(update, conn, tx))
                        {
                            DataHolder.AddParameter(cmd, "@EntityID", uid);
                            DataHolder.AddParameter(cmd, "@Data", merged);
                            if (before != null)
                                DataHolder.AddParameter(cmd, "@Before", before);
                            if (cmd.ExecuteNonQuery() != 1)
                                throw new InvalidOperationException("Quest variable save did not update its owner row.");
                        }
                    }
                    tx.Commit();
                    vars.ReplaceContents(merged);
                }
            }
        }
    }
}
