// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;

namespace Revora.Game
{
    public unsafe class VariableVault
    {
        private ConcurrentDictionary<ulong, DynamicVariable> values;
        // Keys acknowledged by the last load or successful save of THIS object.
        // A row read during a later write is not this baseline.
        private Dictionary<ulong, string> acknowledged;
        // Serialize and save one stable image. Database saves hold this lock until
        // the write succeeds, so later changes cannot be acknowledged by an older save.
        internal readonly object SyncRoot = new object();
        private bool changed;
        public bool Changed
        {
            get { lock (SyncRoot) return changed; }
            set { lock (SyncRoot) changed = value; }
        }
        public VariableVault()
        {
            values = new ConcurrentDictionary<ulong, DynamicVariable>();
        }
        public DynamicVariable this[string variable]
        {
            get
            {
                lock (SyncRoot)
                {
                    DynamicVariable var;
                    if (!values.TryGetValue(variable.Get64HashCode(), out var))
                    {
                        var = new DynamicVariable();
                        this[variable] = var;
                        Changed = true;
                    }
                    return var;
                }
            }
            set
            {
                lock (SyncRoot)
                {
                    values[variable.Get64HashCode()] = value;
                    Changed = true;
                }
            }
        }
        public byte[] Serialize()
        {
            lock (SyncRoot)
            using(var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(values.Count);
                foreach (var kvp in values)
                {
                    writer.Write(kvp.Key);
                    writer.Write(kvp.Value.Type);
                    if (kvp.Value.Type == 1) writer.Write((bool)kvp.Value);
                    else if (kvp.Value.Type == 2) writer.Write((ulong)kvp.Value);
                    else if (kvp.Value.Type == 3) writer.Write((double)kvp.Value);
                    else if (kvp.Value.Type == 4) writer.Write(kvp.Value.ToString());
                    else if (kvp.Value.Type == 5) writer.Write(((DateTime)kvp.Value).Ticks);
                    else if (kvp.Value.Type == 6) { writer.Write(((byte[])kvp.Value).Length); writer.Write((byte[])kvp.Value); }
                }
                return stream.ToArray();
            }
        }
        public void Deserialize(byte[] data)
        {
            lock (SyncRoot)
            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream))
            {
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    ulong key = reader.ReadUInt64();
                    byte type = reader.ReadByte();
                    if (type == 1) values.Add(key, reader.ReadBoolean());
                    else if (type == 2) values.Add(key, reader.ReadUInt64());
                    else if (type == 3) values.Add(key, reader.ReadDouble());
                    else if (type == 4) values.Add(key, reader.ReadString());
                    else if (type == 5) values.Add(key, DateTime.FromBinary(reader.ReadInt64()));
                    else if (type == 6) values.Add(key, reader.ReadBytes(reader.ReadInt32()));
                }
            }
        }
        internal ulong ReadULong(string name)
        {
            lock (SyncRoot)
            {
                DynamicVariable value;
                if (name == null || !values.TryGetValue(name.Get64HashCode(), out value) || value == null || value.Type != 2)
                    return 0;
                return (ulong)value;
            }
        }
        internal void NoteSaved()
        {
            lock (SyncRoot)
            {
                acknowledged = Signatures();
                changed = false;
            }
        }
        // Install bytes into this same object so callers keep its lock identity.
        internal void ReplaceContents(byte[] data)
        {
            lock (SyncRoot)
            {
                values = new ConcurrentDictionary<ulong, DynamicVariable>();
                if (data != null && data.Length >= 4)
                    Deserialize(data);
                acknowledged = Signatures();
                changed = false;
            }
        }
        // Keep a live change when storage still matches this session, and a stored
        // change when this session has not touched that key. Disagreeing edits fail.
        internal bool TryMerge(byte[] storedImage, string[] forcedNames, DynamicVariable[] forcedValues, out byte[] merged)
        {
            merged = null;
            lock (SyncRoot)
            {
                if (forcedNames != null && (forcedValues == null || forcedNames.Length != forcedValues.Length))
                    return false;
                var storedVault = new VariableVault();
                if (storedImage != null && storedImage.Length >= 4)
                    storedVault.Deserialize(storedImage);
                Dictionary<ulong, string> baseline = acknowledged ?? new Dictionary<ulong, string>();
                Dictionary<ulong, string> live = Signatures();
                Dictionary<ulong, string> stored = storedVault.Signatures();
                var liveValues = new Dictionary<ulong, DynamicVariable>();
                var storedValues = new Dictionary<ulong, DynamicVariable>();
                Collect(liveValues);
                storedVault.Collect(storedValues);
                var forced = new Dictionary<ulong, DynamicVariable>();
                if (forcedNames != null)
                {
                    for (int i = 0; i < forcedNames.Length; i++)
                    {
                        if (forcedNames[i] != null)
                            forced[forcedNames[i].Get64HashCode()] = forcedValues[i];
                    }
                }
                var keys = new HashSet<ulong>();
                foreach (ulong key in live.Keys) keys.Add(key);
                foreach (ulong key in stored.Keys) keys.Add(key);
                foreach (ulong key in baseline.Keys) keys.Add(key);
                foreach (ulong key in forced.Keys) keys.Add(key);
                var result = new VariableVault();
                foreach (ulong key in keys)
                {
                    DynamicVariable forcedValue;
                    if (forced.TryGetValue(key, out forcedValue))
                    {
                        result.SetRaw(key, forcedValue);
                        continue;
                    }
                    string baseSig;
                    string liveSig;
                    string storedSig;
                    baseline.TryGetValue(key, out baseSig);
                    live.TryGetValue(key, out liveSig);
                    stored.TryGetValue(key, out storedSig);
                    bool liveChanged = liveSig != baseSig;
                    bool storedChanged = storedSig != baseSig;
                    string chosen;
                    if (!liveChanged && !storedChanged)
                        chosen = liveSig ?? storedSig;
                    else if (liveChanged && !storedChanged)
                        chosen = liveSig;
                    else if (!liveChanged && storedChanged)
                        chosen = storedSig;
                    else if (liveSig == storedSig)
                        chosen = liveSig;
                    else
                        return false;
                    if (chosen == null) continue;
                    DynamicVariable value;
                    if (liveSig == chosen && liveValues.TryGetValue(key, out value))
                        result.SetRaw(key, value);
                    else if (storedValues.TryGetValue(key, out value))
                        result.SetRaw(key, value);
                    else
                        return false;
                }
                merged = result.Serialize();
                return true;
            }
        }
        private Dictionary<ulong, string> Signatures()
        {
            var map = new Dictionary<ulong, string>();
            foreach (var pair in values)
                map[pair.Key] = DynamicVariable.Signature(pair.Value);
            return map;
        }
        private void Collect(Dictionary<ulong, DynamicVariable> target)
        {
            foreach (var pair in values)
                target[pair.Key] = pair.Value;
        }
        private void SetRaw(ulong key, DynamicVariable value)
        {
            values[key] = value;
            changed = true;
        }
    }
    public unsafe class DynamicVariable
    {
        private byte type;
        public byte Type { get { return type; } }
        private bool boolValue = false;
        private ulong ulongValue = 0;
        private double doubleValue = 0.0;
        private string stringValue = "";
        private DateTime dateValue = DateTime.Now.AddDays(-1);
        private byte[] byteData; 
        #region Conversion in
        public static implicit operator bool(DynamicVariable obj)
        {
            return obj.boolValue;
        }
        public static implicit operator byte(DynamicVariable obj)
        {
            return (byte)obj.ulongValue;
        }
        public static implicit operator short(DynamicVariable obj)
        {
            return (short)obj.ulongValue;
        }
        public static implicit operator ushort(DynamicVariable obj)
        {
            return (ushort)obj.ulongValue;
        }
        public static implicit operator int(DynamicVariable obj)
        {
            return (int)obj.ulongValue;
        }
        public static implicit operator uint(DynamicVariable obj)
        {
            return (uint)obj.ulongValue;
        }
        public static implicit operator long(DynamicVariable obj)
        {
            return (long)obj.ulongValue;
        }
        public static implicit operator ulong(DynamicVariable obj)
        {
            return obj.ulongValue;
        }
        public static implicit operator float(DynamicVariable obj)
        {
            if (obj.type == 2)
            {
                if ((long)obj.ulongValue < 0)
                    return (float)((long)obj.ulongValue);
                return (float)obj.ulongValue;
            }
            return (float)obj.doubleValue;
        }
        public static implicit operator double(DynamicVariable obj)
        {
            if (obj.type == 2)
            {
                if ((long)obj.ulongValue < 0)
                    return (double)((long)obj.ulongValue);
                return (double)obj.ulongValue;
            }
            return obj.doubleValue;
        }
        public static implicit operator string(DynamicVariable obj)
        {
            if (obj.type == 1) return obj.boolValue.ToString();
            else if (obj.type == 2) return obj.ulongValue.ToString();
            else if (obj.type == 3) return obj.doubleValue.ToString();
            else if (obj.type == 5) return obj.dateValue.ToString();
            else if (obj.type == 6) return Convert.ToBase64String(obj.byteData);
            return obj.stringValue.ToString();
        }
        public static implicit operator DateTime(DynamicVariable obj)
        {
            return obj.dateValue;
        }
        public static implicit operator byte[](DynamicVariable obj)
        {
            return obj.byteData;
        }
        #endregion
        #region Conversion out
        public static implicit operator DynamicVariable(bool obj)
        {
            return new DynamicVariable() { type = 1, boolValue = obj };
        }
        public static implicit operator DynamicVariable(byte obj)
        {
            return new DynamicVariable() { type = 2, ulongValue = obj };
        }
        public static implicit operator DynamicVariable(short obj)
        {
            return new DynamicVariable() { type = 2, ulongValue = (ulong)obj };
        }
        public static implicit operator DynamicVariable(ushort obj)
        {
            return new DynamicVariable() { type = 2, ulongValue = obj };
        }
        public static implicit operator DynamicVariable(int obj)
        {
            return new DynamicVariable() { type = 2, ulongValue = (ulong)obj };
        }
        public static implicit operator DynamicVariable(uint obj)
        {
            return new DynamicVariable() { type = 2, ulongValue = obj };
        }
        public static implicit operator DynamicVariable(long obj)
        {
            return new DynamicVariable() { type = 2, ulongValue = (ulong)obj };
        }
        public static implicit operator DynamicVariable(ulong obj)
        {
            return new DynamicVariable() { type = 2, ulongValue = obj };
        }
        public static implicit operator DynamicVariable(float obj)
        {
            return new DynamicVariable() { type = 3, doubleValue = obj };
        }
        public static implicit operator DynamicVariable(double obj)
        {
            return new DynamicVariable() { type = 3, doubleValue = obj };
        }
        public static implicit operator DynamicVariable(string obj)
        {
            return new DynamicVariable() { type = 4, stringValue = obj };
        }
        public static implicit operator DynamicVariable(DateTime obj)
        {
            return new DynamicVariable() { type = 5, dateValue = obj };
        }
        public static implicit operator DynamicVariable(byte[] obj)
        {
            return new DynamicVariable() { type = 6, byteData = obj };
        }
        #endregion
        public override string ToString()
        {
            if (type == 4)
                return stringValue;
            return "";
        }
        internal static string Signature(DynamicVariable value)
        {
            if (value == null) return "null";
            if (value.type == 1) return "1:" + (value.boolValue ? "1" : "0");
            if (value.type == 2) return "2:" + value.ulongValue.ToString(CultureInfo.InvariantCulture);
            if (value.type == 3) return "3:" + value.doubleValue.ToString("R", CultureInfo.InvariantCulture);
            if (value.type == 4) return "4:" + (value.stringValue ?? "");
            if (value.type == 5) return "5:" + value.dateValue.Ticks.ToString(CultureInfo.InvariantCulture);
            if (value.type == 6) return "6:" + (value.byteData == null ? "" : Convert.ToBase64String(value.byteData));
            return "0";
        }
    }
}