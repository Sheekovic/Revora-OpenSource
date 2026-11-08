using System;
using System.IO;
using System.Text;
using System.Data;
using Npgsql;


namespace Revora.Database
{
    public class AccountTable
    {
        public enum AccountState : byte
        {
            NotActivated = 100,
            Administrador = 4,
            Ayudante = 7,
            GameHelper = 5,
            GameMaster = 3,
            Player = 2,
            Banned = 1,
            DoesntExist = 0,
            Premium = 6,
        }
        public string Username;
        public string Password;
        private const string SrpMarker = "!revora-srp6711-v1!";
        private string loadedPassword = "";
        private byte[] srpSalt, srpVerifier;
        public bool HasSrpVerifier { get { return srpSalt != null && srpVerifier != null; } }
        public Network.AuthPackets.Srp6711Exchange BeginSrp(string wireUsername)
        {
            return HasSrpVerifier
                ? Network.AuthPackets.Srp6711Exchange.BeginWithVerifier(wireUsername, srpSalt, srpVerifier)
                : Network.AuthPackets.Srp6711Exchange.Begin(wireUsername, Password);
        }
        public string Email;
        public string IP;
        public uint IDReferido;
        public DateTime LastCheck;
        public AccountState State;
        public uint EntityID;
        public int RandomKey;
        public string EarthAdress;

        public bool exists = false;
        public AccountTable(string username)
        {
            if (username == null) return;
            this.Username = username;
            this.Password = "";
            this.IP = "";
            this.LastCheck = DateTime.Now;
            this.State = AccountState.DoesntExist;
            this.EntityID = 0;
            using (var connection = DataHolder.MySqlConnection)
            using (var command = new NpgsqlCommand(
                "SELECT username,password,ip,entityid,lastcheck,state,email,idreferido " +
                "FROM accounts WHERE lower(username)=lower(@username) LIMIT 2", connection))
            using (var adapter = new NpgsqlDataAdapter(command))
            using (var rows = new DataTable())
            {
                command.Parameters.AddWithValue("username", username);
                connection.Open();
                adapter.Fill(rows);
                // Website usernames are case-insensitive. Keep the stored spelling
                // for subsequent saves and refuse ambiguous legacy duplicates.
                if (rows.Rows.Count != 1) return;
                var row = rows.Rows[0];
                exists = true;
                this.Password = Convert.ToString(row["password"]).TrimEnd();
                loadedPassword = this.Password;
                this.Username = Convert.ToString(row["username"]).TrimEnd();
                this.IP = Convert.ToString(row["ip"]).TrimEnd();
                this.EntityID = row.IsNull("entityid") ? 0 : Convert.ToUInt32(row["entityid"]);
                this.LastCheck = DateTime.FromBinary(row.IsNull("lastcheck") ? 0 : Convert.ToInt64(row["lastcheck"]));
                this.State = (AccountState)(row.IsNull("state") ? 0 : Convert.ToInt32(row["state"]));
                this.Email = Convert.ToString(row["email"]).TrimEnd();
                this.IDReferido = row.IsNull("idreferido") ? 0 : Convert.ToUInt32(row["idreferido"]);
                if (Password == SrpMarker)
                {
                    // A marker without its verifier must never become an empty-password account.
                    Password = "";
                    if (connection.State != ConnectionState.Open) connection.Open();
                    using (var present = new NpgsqlCommand("SELECT to_regclass('revora_account_srp') IS NOT NULL", connection))
                        if (!(bool)present.ExecuteScalar()) { exists = false; State = AccountState.DoesntExist; return; }
                    using (var credential = new NpgsqlCommand("SELECT salt,verifier FROM revora_account_srp WHERE username=@username", connection))
                    {
                        credential.Parameters.AddWithValue("username", Username);
                        using (var reader = credential.ExecuteReader())
                        {
                            if (!reader.Read()) { exists = false; State = AccountState.DoesntExist; return; }
                            srpSalt = reader.GetFieldValue<byte[]>(0);
                            srpVerifier = reader.GetFieldValue<byte[]>(1);
                        }
                    }
                    if (srpSalt.Length != 32 || !Network.AuthPackets.Srp6711Exchange.IsValidStoredVerifier(srpVerifier))
                    { srpSalt = srpVerifier = null; exists = false; State = AccountState.DoesntExist; }
                }
            }
        }
        #region Referrals
        public bool Referido(string IP, string myusername, string refername)
        {
            uint UIDX = 0;
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("Name", refername))
            using (var reader = new MySqlReader(cmd))
            {
                if (reader.Read())
                {
                    UIDX = reader.ReadUInt32("UID");
                }
            }
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("accounts").Where("EntityID", UIDX))
            using (var reader = new MySqlReader(cmd))
            {
                if (reader.Read())
                {
                    if (myusername != reader.ReadString("Username"))
                    {
                        if (IP == reader.ReadString("IP"))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }
        public void GuardarReferidos(string username)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("accounts").Set("IDReferido", IDReferido).Where("Username", username).Execute();
        }
        #endregion
        public uint GenerateKey(int randomKey = 0)
        {
            if (randomKey == 0)
                RandomKey = Kernel.Random.Next(11, 253) % 100 + 1;
            return (uint)(Username.GetHashCode() * Password.GetHashCode() * RandomKey);
        }

        public bool MatchKey(uint key)
        {
            return key == GenerateKey(RandomKey);
        }

        public void SetCurrentIP(string ip)
        {
            var loc = IPtoLocation.GetLocation(ip);
            IP = ip;

            if (loc == null)
            {
                EarthAdress = "N/A no. N/A, N/A, N/A, N/A";
            }
            else
            {
                EarthAdress = loc.city + ", " + loc.regionName + ", " + loc.countryName;
            }
        }

        public void Save()
        {
            if (Password != (HasSrpVerifier ? "" : loadedPassword))
            { PersistSrp(false); return; }
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("accounts").Set("Ip", IP).Set("EntityID", EntityID)
                    .Where("Username", Username).Execute();
        }
        public void Insert()
        {
            PersistSrp(true);
        }
        private void PersistSrp(bool insert)
        {
            if (string.IsNullOrEmpty(Username) || Username.Length > 25 || string.IsNullOrEmpty(Password))
                throw new ArgumentException("Account credentials are required.");
            byte[] salt, verifier;
            Network.AuthPackets.Srp6711Exchange.CreateVerifier(Password, out salt, out verifier);
            using (var connection = DataHolder.MySqlConnection)
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    using (var schema = new NpgsqlCommand(@"CREATE TABLE IF NOT EXISTS revora_account_srp(
username varchar(25) PRIMARY KEY REFERENCES accounts(username) ON DELETE CASCADE,
salt bytea NOT NULL CHECK(octet_length(salt)=32),verifier bytea NOT NULL CHECK(octet_length(verifier)=32));", connection, transaction)) schema.ExecuteNonQuery();
                    using (var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext(lower(@username)))", connection, transaction))
                    { gate.Parameters.AddWithValue("username", Username); gate.ExecuteNonQuery(); }
                    using (var count = new NpgsqlCommand("SELECT count(*) FROM accounts WHERE lower(username)=lower(@username)", connection, transaction))
                    {
                        count.Parameters.AddWithValue("username", Username);
                        if (Convert.ToInt64(count.ExecuteScalar()) != (insert ? 0 : 1))
                            throw new InvalidOperationException("Account identity is absent or ambiguous.");
                    }
                    string sql = insert ? "INSERT INTO accounts(username,password,state) VALUES(@username,@marker,@state)"
                        : "UPDATE accounts SET password=@marker,ip=@ip,entityid=@entity WHERE username=@username AND password=@previous";
                    using (var account = new NpgsqlCommand(sql, connection, transaction))
                    {
                        account.Parameters.AddWithValue("username", Username); account.Parameters.AddWithValue("marker", SrpMarker);
                        account.Parameters.AddWithValue("state", (short)State); account.Parameters.AddWithValue("ip", IP ?? "");
                        account.Parameters.AddWithValue("entity", (long)EntityID); account.Parameters.AddWithValue("previous", loadedPassword);
                        if (account.ExecuteNonQuery() != 1) throw new InvalidOperationException("Account credential write was not applied.");
                    }
                    using (var save = new NpgsqlCommand("INSERT INTO revora_account_srp(username,salt,verifier) VALUES(@username,@salt,@verifier) ON CONFLICT(username) DO UPDATE SET salt=EXCLUDED.salt,verifier=EXCLUDED.verifier", connection, transaction))
                    {
                        save.Parameters.AddWithValue("username", Username); save.Parameters.AddWithValue("salt", salt); save.Parameters.AddWithValue("verifier", verifier);
                        if (save.ExecuteNonQuery() != 1) throw new InvalidOperationException("Account verifier write was not applied.");
                    }
                    transaction.Commit();
                }
            }
            srpSalt = salt; srpVerifier = verifier; Password = ""; loadedPassword = SrpMarker; exists = true;
        }
        public void SaveState()
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("accounts").Set("State", (int)State)
                    .Where("Username", Username).Execute();
        }
    }
}
