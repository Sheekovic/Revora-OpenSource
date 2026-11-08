// * Created by AliMOssaed
// * Copyright © 2015
// * Epic-Fight - Project
using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Revora
{
    public class IniFile
    {
        public string FileName;
        public string FileSection;

        public IniFile()
        {
        }

        public IniFile(string _FileName, string section = "data")
        {
            this.FileName = Environment.CurrentDirectory + "\\" + _FileName;
            this.FileSection = section;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern int GetPrivateProfileStringA(string Section, string Key, string _Default, StringBuilder Buffer, int BufferSize, string FileName);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern int WritePrivateProfileStringA(string Section, string Key, string Arg, string FileName);

        public object this[object key, object _default = null]
        {
            get
            {
                if (FileSection == null) return null;
                return ReadString(FileSection, key.ToString(), _default.ToString(), 1024);
            }
            set
            {
                if (FileSection == null) return;
                Write(FileSection, key.ToString(), value);
            }
        }

        public byte ReadByte(string Section, string Key, byte _Default)
        {
            byte buf = _Default;
            byte.TryParse(this.ReadString(Section, Key, _Default.ToString(), 6), out buf);
            return buf;
        }

        public short ReadInt16(string Section, string Key, short _Default)
        {
            short buf = _Default;
            short.TryParse(this.ReadString(Section, Key, _Default.ToString(), 9), out buf);
            return buf;
        }

        public int ReadInt32(string Section, string Key, int _Default)
        {
            int buf = _Default;
            int.TryParse(this.ReadString(Section, Key, _Default.ToString(), 15), out buf);
            return buf;
        }

        public sbyte ReadSByte(string Section, string Key, byte _Default)
        {
            sbyte buf = (sbyte)_Default;
            sbyte.TryParse(this.ReadString(Section, Key, _Default.ToString(), 6), out buf);
            return buf;
        }

        public string ReadString(string Section, string Key)
        {
            return this.ReadString(Section, Key, "", 400);
        }

        public string ReadString(string Section, string Key, string _Default, int BufSize)
        {
            StringBuilder Buffer = new StringBuilder(BufSize);
            GetPrivateProfileStringA(Section, Key, _Default, Buffer, BufSize, this.FileName);
            return Buffer.ToString();
        }

        public ushort ReadUInt16(string Section, string Key)
        {
            ushort buf = 0;
            ushort.TryParse(this.ReadString(Section, Key, 0.ToString(), 9), out buf);
            return buf;
        }

        public uint ReadUInt32(string Section, string Key)
        {
            uint buf = 0;
            uint.TryParse(this.ReadString(Section, Key, 0.ToString(), 15), out buf);
            return buf;
        }

        public void Write(string Section, string Key, object Value)
        {
            WritePrivateProfileStringA(Section, Key, Value.ToString(), this.FileName);
        }

        public void Write(string Section, string Key, string Value)
        {
            WritePrivateProfileStringA(Section, Key, Value, this.FileName);
        }

    }

}
namespace Revora
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Text;

    public class IniFiles
    {
        public const int MaxSectionSize = 0x7fff;
        private string GameIP;

        public IniFiles(string path)
        {
            this.GameIP = System.IO.Path.GetFullPath(path);
        }

        public void DeleteKey(string sectionName, string keyName)
        {
            if (sectionName == null)
            {
                throw new ArgumentNullException("sectionName");
            }
            if (keyName == null)
            {
                throw new ArgumentNullException("keyName");
            }
            this.DnySlYqaLh(sectionName, keyName, null);
        }

        public void DeleteSection(string sectionName)
        {
            if (sectionName == null)
            {
                throw new ArgumentNullException("sectionName");
            }
            this.DnySlYqaLh(sectionName, null, null);
        }

        private void DnySlYqaLh(string string_1, string string_2, string string_3)
        {
            if (!Class1.WritePrivateProfileString(string_1, string_2, string_3, this.GameIP))
            {
                throw new Win32Exception();
            }
        }

        public double GetDouble(string sectionName, string keyName, double defaultValue)
        {
            string str = this.GetString(sectionName, keyName, "");
            if ((str == null) || (str.Length == 0))
            {
                return defaultValue;
            }
            return Convert.ToDouble(str, CultureInfo.InvariantCulture);
        }

        public int GetInt16(string sectionName, string keyName, short defaultValue)
        {
            return Convert.ToInt16(this.GetInt32(sectionName, keyName, defaultValue));
        }

        public int GetInt32(string sectionName, string keyName, int defaultValue)
        {
            if (sectionName == null)
            {
                throw new ArgumentNullException("sectionName");
            }
            if (keyName == null)
            {
                throw new ArgumentNullException("keyName");
            }
            return Class1.GetPrivateProfileInt(sectionName, keyName, defaultValue, this.GameIP);
        }

        public string[] GetKeyNames(string sectionName)
        {
            string[] strArray;
            if (sectionName == null)
            {
                throw new ArgumentNullException("sectionName");
            }
            IntPtr ptr = Marshal.AllocCoTaskMem(0x7fff);
            try
            {
                int num = Class1.GetPrivateProfileString_2(sectionName, null, null, ptr, 0x7fff, this.GameIP);
                strArray = smethod_0(ptr, num);
            }
            finally
            {
                Marshal.FreeCoTaskMem(ptr);
            }
            return strArray;
        }

        public string[] GetSectionNames()
        {
            string[] strArray;
            IntPtr ptr = Marshal.AllocCoTaskMem(0x7fff);
            try
            {
                int num = Class1.GetPrivateProfileSectionNames(ptr, 0x7fff, this.GameIP);
                strArray = smethod_0(ptr, num);
            }
            finally
            {
                Marshal.FreeCoTaskMem(ptr);
            }
            return strArray;
        }

        public Dictionary<string, string> GetSectionValues(string sectionName)
        {
            List<KeyValuePair<string, string>> sectionValuesAsList = this.GetSectionValuesAsList(sectionName);
            Dictionary<string, string> dictionary = new Dictionary<string, string>(sectionValuesAsList.Count);
            foreach (KeyValuePair<string, string> pair in sectionValuesAsList)
            {
                if (!dictionary.ContainsKey(pair.Key))
                {
                    dictionary.Add(pair.Key, pair.Value);
                }
            }
            return dictionary;
        }

        public List<KeyValuePair<string, string>> GetSectionValuesAsList(string sectionName)
        {
            string[] strArray;
            if (sectionName == null)
            {
                throw new ArgumentNullException("sectionName");
            }
            IntPtr ptr = Marshal.AllocCoTaskMem(0x7fff);
            try
            {
                int num2 = Class1.GetPrivateProfileSection(sectionName, ptr, 0x7fff, this.GameIP);
                strArray = smethod_0(ptr, num2);
            }
            finally
            {
                Marshal.FreeCoTaskMem(ptr);
            }
            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>(strArray.Length);
            for (int i = 0; i < strArray.Length; i++)
            {
                int index = strArray[i].IndexOf('=');
                string key = strArray[i].Substring(0, index);
                string str2 = strArray[i].Substring(index + 1, (strArray[i].Length - index) - 1);
                list.Add(new KeyValuePair<string, string>(key, str2));
            }
            return list;
        }

        public string GetString(string sectionName, string keyName, string defaultValue)
        {
            if (sectionName == null)
            {
                throw new ArgumentNullException("sectionName");
            }
            if (keyName == null)
            {
                throw new ArgumentNullException("keyName");
            }
            StringBuilder builder = new StringBuilder(0x7fff);
            Class1.GetPrivateProfileString(sectionName, keyName, defaultValue, builder, 0x7fff, this.GameIP);
            return builder.ToString();
        }

        private static string[] smethod_0(IntPtr intptr_0, int int_0)
        {
            if (int_0 == 0)
            {
                return new string[0];
            }
            return Marshal.PtrToStringAuto(intptr_0, int_0 - 1).Split(new char[1]);
        }

        public void WriteValue(string sectionName, string keyName, double value)
        {
            this.WriteValue(sectionName, keyName, value.ToString(CultureInfo.InvariantCulture));
        }

        public void WriteValue(string sectionName, string keyName, short value)
        {
            this.WriteValue(sectionName, keyName, (int)value);
        }

        public void WriteValue(string sectionName, string keyName, int value)
        {
            this.WriteValue(sectionName, keyName, value.ToString(CultureInfo.InvariantCulture));
        }

        public void WriteValue(string sectionName, string keyName, float value)
        {
            this.WriteValue(sectionName, keyName, value.ToString(CultureInfo.InvariantCulture));
        }

        public void WriteValue(string sectionName, string keyName, string value)
        {
            if (sectionName == null)
            {
                throw new ArgumentNullException("sectionName");
            }
            if (keyName == null)
            {
                throw new ArgumentNullException("keyName");
            }
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }
            this.DnySlYqaLh(sectionName, keyName, value);
        }

        public string Path
        {
            get
            {
                return this.GameIP;
            }
        }

        [SuppressUnmanagedCodeSecurity]
        private static class Class1
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
            public static extern int GetPrivateProfileInt(string GameIP, string string_1, int int_0, string string_2);
            [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
            public static extern int GetPrivateProfileSection(string GameIP, IntPtr intptr_0, uint uint_0, string string_1);
            [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
            public static extern int GetPrivateProfileSectionNames(IntPtr intptr_0, uint uint_0, string GameIP);
            [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
            public static extern uint GetPrivateProfileString(string GameIP, string string_1, string string_2, StringBuilder stringBuilder_0, int int_0, string string_3);
            [DllImport("kernel32.dll", EntryPoint = "GetPrivateProfileString", CharSet = CharSet.Auto)]
            public static extern uint GetPrivateProfileString_1(string GameIP, string string_1, string string_2, [In, Out] char[] char_0, int int_0, string string_3);
            [DllImport("kernel32.dll", EntryPoint = "GetPrivateProfileString", CharSet = CharSet.Auto)]
            public static extern int GetPrivateProfileString_2(string GameIP, string string_1, string string_2, IntPtr intptr_0, uint uint_0, string string_3);
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern bool WritePrivateProfileString(string GameIP, string string_1, string string_2, string string_3);
        }
    }
}

