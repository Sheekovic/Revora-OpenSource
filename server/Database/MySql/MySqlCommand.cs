// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Text;
using System.Collections.Generic;

namespace Revora.Database
{
    using MYSQLCOMMAND = Npgsql.NpgsqlCommand;
    using MYSQLREADER = Npgsql.NpgsqlDataReader;
    using MYSQLCONNECTION = Npgsql.NpgsqlConnection;

    public unsafe class MySqlCommand : IDisposable
    {
        private MySqlCommandType _type;
        public MySqlCommandType Type
        {
            get { return _type; }
            set { _type = value; }
        }
        protected StringBuilder _command;
        public string Command
        {
            get { return _command.ToString(); }
            set { _command = new StringBuilder(value); }
        }
        private Dictionary<string, string> _sets;
        private readonly Dictionary<string, long?> conditions = new Dictionary<string, long?>();
        public MySqlCommand SetInt64Parameter(string column,long? value)
        {
            if(Type!=MySqlCommandType.UPDATE)throw new InvalidOperationException("Parameterized assignments require UPDATE.");
            string name="saved_value_"+conditions.Count;
            PutSet(column,"@"+name);conditions.Add(name,value);return this;
        }
        public MySqlCommand AndInt64Parameter(string column, long? value)
        {
            if (Type != MySqlCommandType.UPDATE) throw new InvalidOperationException("Conditional writes require UPDATE.");
            string name = "saved_condition_" + conditions.Count;
            _command.Append(" AND ").Append(Qi(column)).Append(" IS NOT DISTINCT FROM @").Append(name);
            conditions.Add(name, value);
            return this;
        }
        // Legacy numeric counters stored as text need a typed comparison too.
        public MySqlCommand AndNumericTextParameter(string column, long? value)
        {
            if (Type != MySqlCommandType.UPDATE) throw new InvalidOperationException("Conditional writes require UPDATE.");
            string name = "saved_condition_" + conditions.Count;
            _command.Append(" AND CAST(").Append(Qi(column)).Append(" AS bigint) IS NOT DISTINCT FROM @").Append(name);
            conditions.Add(name, value);
            return this;
        }
        private bool firstPart = true;
        private SafeDictionary<byte, string> insertFields;
        private SafeDictionary<byte, string> insertValues;
        private byte lastpair;
        public MySqlCommand(MySqlCommandType Type)
        {
            this.Type = Type;
            switch (Type)
            {
                case MySqlCommandType.SELECT:
                    {
                        _command = new StringBuilder("SELECT * FROM <R>");
                        break;
                    }
                case MySqlCommandType.UPDATE:
                    {
                        _command = new StringBuilder("UPDATE <R> SET ");
                        break;
                    }
                case MySqlCommandType.INSERT:
                    {
                        insertFields = new SafeDictionary<byte, string>();
                        insertValues = new SafeDictionary<byte, string>();
                        lastpair = 0;
                        _command = new StringBuilder("INSERT INTO <R> (<F>) VALUES (<V>)");
                        break;
                    }
                case MySqlCommandType.DELETE:
                    {
                        _command = new StringBuilder("DELETE FROM <R> WHERE <C> = <V>");
                        break;
                    }
                case MySqlCommandType.COUNT:
                    {
                        _command = new StringBuilder("SELECT count(<V>) FROM <R>");
                        break;
                    }
            }
        }
        private static string Qi(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "\"\"";
            return "\"" + name.Replace("\"", "\"\"").ToLowerInvariant() + "\"";
        }
        private bool Comma()
        {
            if (firstPart)
            {
                firstPart = false;
                return false;
            }
            string command = _command.ToString();
            if (command[command.Length - 1] == ',' || command[command.Length - 2] == ',' || command[command.Length - 3] == ',')
                return false;
            return true;
        }
        #region Select
        public MySqlCommand Select(string table)
        {
            _command = _command.Replace("<R>", Qi(table));
            return this;
        }
        #endregion
        #region Count
        public MySqlCommand Count(string table)
        {
            _command = _command.Replace("<R>", Qi(table));
            return this;
        }
        #endregion
        #region Delete
        public MySqlCommand C(string table)
        {
            _command = _command.Replace("<R>", Qi(table));
                return this;
            }
        public MySqlCommand Delete(string table, string column, string value)
        {
            _command = _command.Replace("<R>", Qi(table));
            _command = _command.Replace("<C>", Qi(column));
            _command = _command.Replace("<V>", "'" + value.MySqlEscape() + "'");
            return this;
        }
        public MySqlCommand Delete(string table, string column, long value)
        {
            _command = _command.Replace("<R>", Qi(table));
            _command = _command.Replace("<C>", Qi(column));
            _command = _command.Replace("<V>", value.ToString());
            return this;
        }
        public MySqlCommand Delete(string table, string column, ulong value)
        {
            _command = _command.Replace("<R>", Qi(table));
            _command = _command.Replace("<C>", Qi(column));
            _command = _command.Replace("<V>", value.ToString());
            return this;
        }
        public MySqlCommand Delete(string table, string column, bool value)
        {
            _command = _command.Replace("<R>", Qi(table));
            _command = _command.Replace("<C>", Qi(column));
            _command = _command.Replace("<V>", (value ? "1" : "0"));
            return this;
        }
        #endregion
        #region Update
        public MySqlCommand Update(string table)
        {
            _command = _command.Replace("<R>", Qi(table));
            return this;
        }
        private void PutSet(string column, string sqlValue)
        {
            if (_sets == null)
                _sets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _sets[column] = sqlValue;
        }
        public MySqlCommand Set(string column, long value)
        {
            if (Type == MySqlCommandType.UPDATE)
                PutSet(column, value.ToString());
            return this;
        }
        public MySqlCommand Set(string column, ulong value)
        {
            if (Type == MySqlCommandType.UPDATE)
                PutSet(column, value.ToString());
            return this;
        }
        public MySqlCommand Set(string column, string value)
        {
            if (Type == MySqlCommandType.UPDATE)
                PutSet(column, "'" + (value ?? "").MySqlEscape() + "'");
            return this;
        }
        public MySqlCommand Set(string column, bool value)
        {
            if (Type == MySqlCommandType.UPDATE)
                PutSet(column, value ? "1" : "0");
            return this;
        }
        public MySqlCommand Set(string column, object value)
        {
            if (value is bool) Set(column, (bool)value);
            else Set(column, value.ToString());
            return this;
        }
        #endregion
        #region Insert
        public MySqlCommand Insert(string table)
        {
            _command = _command.Replace("<R>", Qi(table));
            return this;
        }
        public MySqlCommand Insert(string field, long value)
        {
            insertFields.Add(lastpair, field);
            insertValues.Add(lastpair, value.ToString());
            lastpair++;
            return this;
        }
        public MySqlCommand Insert(string field, ulong value)
        {
            insertFields.Add(lastpair, field);
            insertValues.Add(lastpair, value.ToString());
            lastpair++;
            return this;
        }
        public MySqlCommand Insert(string field, bool value)
        {
            insertFields.Add(lastpair, field);
            insertValues.Add(lastpair, (value ? 1 : 0).ToString());
            lastpair++;
            return this;
        }
        public MySqlCommand Insert(string field, string value)
        {
            var array = value.ToCharArray();
            string str = Encoding.Default.GetString(Encoding.Unicode.GetBytes(array, 0, array.Length));
            insertFields.Add(lastpair, field);
            insertValues.Add(lastpair, value.MySqlEscape());
            lastpair++;
            return this;
        }
        #endregion
        #region Where
        public MySqlCommand Where(string column, long value)
        {
            _command = _command.Append("WHERE " + Qi(column) + " = " + value);
            return this;
        }
        public MySqlCommand Where(string column, long value, bool greater)
        {
            if (greater)
                _command = _command.Append("WHERE " + Qi(column) + " > " + value);
            else
                _command = _command.Append("WHERE " + Qi(column) + " < " + value);
            return this;
        }
        public MySqlCommand Where(string column, ulong value)
        {
            _command = _command.Append("WHERE " + Qi(column) + " = " + value);
            return this;
        }
        public MySqlCommand Where(string column, string value)
        {
            _command = _command.Append("WHERE " + Qi(column) + " = '" + value.MySqlEscape() + "'");
            return this;
        }
        public MySqlCommand Where(string column, bool value)
        {
            _command = _command.Append("WHERE " + Qi(column) + " = " + (value ? "1" : "0"));
            return this;
        }
        #endregion
        #region And
        public MySqlCommand And(string column, long value)
        {
            _command = _command.Append(" AND " + Qi(column) + " = " + value);
            return this;
        }
        public MySqlCommand And(string column, ulong value)
        {
            _command = _command.Append(" AND " + Qi(column) + " = " + value);
            return this;
        }
        public MySqlCommand And(string column, string value)
        {
            _command = _command.Append(" AND " + Qi(column) + " = '" + value.MySqlEscape() + "'");
            return this;
        }
        public MySqlCommand And(string column, bool value)
        {
            _command = _command.Append(" AND " + Qi(column) + " = " + (value ? "1" : "0"));
            return this;
        }
        #endregion
        #region Or
        public MySqlCommand Or(string column, long value)
        {
            _command = _command.Append(" Or " + Qi(column) + " = " + value);
            return this;
        }
        public MySqlCommand Or(string column, ulong value)
        {
            _command = _command.Append(" Or " + Qi(column) + " = " + value);
            return this;
        }
        public MySqlCommand Or(string column, string value)
        {
            _command = _command.Append(" Or " + Qi(column) + " = '" + value.MySqlEscape() + "'");
            return this;
        }
        public MySqlCommand Or(string column, bool value)
        {
            _command = _command.Append(" Or " + Qi(column) + " = " + (value ? "1" : "0"));
            return this;
        }
        #endregion
        #region Order
        public MySqlCommand Order(string column)
        {
            _command = _command.Append("ORDER BY " + column + "");
            return this;
        }
        #endregion
        public int Execute()
        {
            using (var conn = DataHolder.MySqlConnection)
            {
                conn.Open();
                return Execute(conn);
            }
        }
        public int Execute(MYSQLCONNECTION conn)
        {
            if (Type == MySqlCommandType.INSERT)
            {
                string fields = "";
                string values = "";
                byte x;
                for (x = 0; x < lastpair; x++)
                {
                    bool comma = (x + 1) == lastpair ? false : true;
                    #region Fields
                    if (comma)
                        fields += "" + Qi(insertFields[x]) + ",";
                    else
                        fields += Qi(insertFields[x]);
                    #endregion
                    #region Values
                    if (comma)
                        values += "'" + insertValues[x] + "'" + ",";
                    else
                        values += "'" + insertValues[x] + "'";
                    #endregion
                }
                _command = _command.Replace("<F>", fields);
                _command = _command.Replace("<V>", values);
            }

            if (Type == MySqlCommandType.UPDATE && _sets != null && _sets.Count > 0)
            {
                var setClause = new StringBuilder();
                bool firstSet = true;
                foreach (var kv in _sets)
                {
                    if (!firstSet)
                        setClause.Append(',');
                    firstSet = false;
                    setClause.Append(Qi(kv.Key)).Append(" = ").Append(kv.Value).Append(' ');
                }
                string text = _command.ToString();
                int setIdx = text.IndexOf(" SET", StringComparison.Ordinal);
                if (setIdx >= 0)
                {
                    int insertAt = setIdx + 4;
                    if (insertAt < text.Length && text[insertAt] == ' ')
                        insertAt++;
                    _command.Insert(insertAt, setClause.ToString());
                }
            }

            using (MYSQLCOMMAND cmd = new MYSQLCOMMAND(Command, conn))
            {
                foreach (var condition in conditions)
                    cmd.Parameters.Add(condition.Key, NpgsqlTypes.NpgsqlDbType.Bigint).Value = condition.Value.HasValue ? (object)condition.Value.Value : DBNull.Value;
                return cmd.ExecuteNonQuery();
            }
        }
        public MySqlReader CreateReader()
        {
            return new MySqlReader(this);
        }
        void IDisposable.Dispose()
        {
            if (insertValues != null)
            {
                insertValues.Clear();
                insertFields.Clear();
            }
            if (_sets != null)
                _sets.Clear();
            _command = null;
        }
    }
    public enum MySqlCommandType
    {
        DELETE, INSERT, SELECT, UPDATE, COUNT, C
    }
}
