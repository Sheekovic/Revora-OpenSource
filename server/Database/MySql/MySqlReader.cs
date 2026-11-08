// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Data;
using System.Text;
using Npgsql;

namespace Revora.Database
{
    public unsafe class MySqlReader : IDisposable
    {
        private DataSet _dataset;
        private DataRow _datarow;
        private int _row;
        const string Table = "table";

        public MySqlReader(MySqlCommand command)
        {
            if (command.Type == MySqlCommandType.SELECT)
            {
                _dataset = new DataSet();
                _row = 0;
                using (var conn = DataHolder.MySqlConnection)
                {
                    conn.Open();
                    using (var DataAdapter = new NpgsqlDataAdapter(command.Command, conn))
                        DataAdapter.Fill(_dataset, Table);
                    ((IDisposable)command).Dispose();
                }
            }
        }
        void IDisposable.Dispose()
        {
            if (_dataset != null)
                _dataset.Dispose();
        }
        public bool Read()
        {
            if (_dataset == null) return false;
            if (_dataset.Tables.Count == 0) return false;
            if (_dataset.Tables[Table].Rows.Count > _row)
            {
                _datarow = _dataset.Tables[Table].Rows[_row];
                _row++;
                return true;
            }
            _row++;
            return false;
        }
        public void Dispose()
        {
        }
        public void Close()
        {
        }
        public int NumberOfRows
        {
            get
            {
                if (_dataset == null) return 0;
                if (_dataset.Tables.Count == 0) return 0;
                return _dataset.Tables[Table].Rows.Count;
            }
        }
        private string Col(string columnName)
        {
            if (_datarow == null || _datarow.Table == null) return columnName;
            if (_datarow.Table.Columns.Contains(columnName)) return columnName;
            foreach (DataColumn c in _datarow.Table.Columns)
            {
                if (string.Equals(c.ColumnName, columnName, StringComparison.OrdinalIgnoreCase))
                    return c.ColumnName;
            }
            return columnName;
        }
        public sbyte ReadSByte(string columnName)
        {
            columnName = Col(columnName);
            if (_datarow.IsNull(columnName)) return default(sbyte);
            sbyte result = 0;
            sbyte.TryParse(_datarow[columnName].ToString(), out result);
            return result;
        }
        public byte ReadByte(string columnName)
        {
            columnName = Col(columnName);
            if (_datarow.IsNull(columnName)) return default(byte);
            byte result = 0;
            byte.TryParse(_datarow[columnName].ToString(), out result);
            return result;
        }
        public short ReadInt16(string columnName)
        {
            columnName = Col(columnName);
            if (_datarow.IsNull(columnName)) return default(short);
            short result = 0;
            short.TryParse(_datarow[columnName].ToString(), out result);
            return result;
        }
        public ushort ReadUInt16(string columnName)
        {
            columnName = Col(columnName);
            if (_datarow.IsNull(columnName)) return default(ushort);
            ushort result = 0;
            ushort.TryParse(_datarow[columnName].ToString(), out result);
            return result;
        }
        public int ReadInt32(string columnName)
        {
            columnName = Col(columnName);
            if (_datarow.IsNull(columnName)) return default(int);
            int result = 0;
            int.TryParse(_datarow[columnName].ToString(), out result);
            return result;
        }
        public uint ReadUInt32(string columnName)
        {
            columnName = Col(columnName);
            if (_datarow.IsNull(columnName)) return default(uint);
            uint result = 0;
            uint.TryParse(_datarow[columnName].ToString(), out result);
            return result;
        }
        public long ReadInt64(string columnName)
        {
            columnName = Col(columnName);
            if (_datarow.IsNull(columnName)) return default(long);
            long result = 0;
            long.TryParse(_datarow[columnName].ToString(), out result);
            return result;
        }
        public long? ReadNullableInt64Strict(string columnName)
        {
            columnName = Col(columnName);
            if (_datarow.IsNull(columnName)) return null;
            return Convert.ToInt64(_datarow[columnName], System.Globalization.CultureInfo.InvariantCulture);
        }
        public ulong ReadUInt64(string columnName)
        {
            columnName = Col(columnName);
            if (_datarow.IsNull(columnName)) return default(ulong);
            ulong result = 0;
            ulong.TryParse(_datarow[columnName].ToString(), out result);
            return result;
        }
        public ulong ReadUInt128(string columnName)
        {
            columnName = Col(columnName);
            if (_datarow.IsNull(columnName)) return default(ulong);
            ulong result = 0;
            ulong.TryParse(_datarow[columnName].ToString(), out result);
            return result;
        }
        public string ReadString(string columnName)
        {
            columnName = Col(columnName);
            if (_datarow.IsNull(columnName)) return "";
            string data = _datarow[columnName].ToString();
            return data == null ? "" : data.TrimEnd();
        }
        public bool ReadBoolean(string columnName)
        {
            columnName = Col(columnName);
            if (_datarow.IsNull(columnName)) return false;
            bool result = false;
            string str = _datarow[columnName].ToString();
            if (str == "") return false;
            if (str[0] == '1') return true;
            if (str[0] == '0') return false;
            bool.TryParse(str, out result);
            return result;
        }
        public byte[] ReadBlob(string columnName)
        {
            columnName = Col(columnName);
            if (_datarow.IsNull(columnName)) return new byte[0];
            object v = _datarow[columnName];
            return v as byte[] ?? new byte[0];
        }
        public DateTime ReadDate(string columnName)
        {
            columnName = Col(columnName);
            if (_datarow.IsNull(columnName)) return new DateTime(2011, 1, 1, 1, 1, 1);
            return Convert.ToDateTime(_datarow[columnName]);
        }
        public string ReadLongString(string columnName)
        {
            columnName = Col(columnName);
            if (_datarow.IsNull(columnName)) return "";
            return _datarow[columnName].ToString();
        }
    }
}
