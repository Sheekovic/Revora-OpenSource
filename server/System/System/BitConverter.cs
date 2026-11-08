using Revora.Network;
// * Created by AliMOssaed
// * Copyright © 2015
// * Epic-Fight - Project
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Revora
{
    public static class Ext
    {
        public static byte[] ToConquerPacket(this object obj, ushort packetid)
        {
            byte[] protobuff;
            using (var ms = new System.IO.MemoryStream())
            {
                ProtoBuf.Serializer.Serialize(ms, obj);
                protobuff = ms.ToArray();
                byte[] buffer;
                buffer = new byte[12 + protobuff.Length];
                Buffer.BlockCopy(protobuff, 0, buffer, 4, protobuff.Length);
                Writer.WriteUInt16((ushort)(buffer.Length - 8), 0, buffer);
                Writer.WriteUInt16(packetid, 2, buffer);
                return buffer;
            }
        }
        public static T ConvertToProto<T>(this byte[] buff)
        {
            byte[] buffer;
            buffer = new byte[buff.Length - 12];
            System.Buffer.BlockCopy(buff, 4, buffer, 0, buff.Length - 12);
            using (var ms = new MemoryStream(buffer))
            {
                return ProtoBuf.Serializer.Deserialize<T>(ms);
            }
        }
        public static string ToHexString(this byte[] hex)
        {
            if (hex == null)
            {
                return null;
            }
            if (hex.Length == 0)
            {
                return string.Empty;
            }
            var s = new StringBuilder();
            foreach (byte b in hex)
            {
                s.Append(b.ToString("x2"));
            }
            return s.ToString();
        }

        public static byte[] ToHexBytes(this string hex)
        {
            if (hex == null)
            {
                return null;
            }
            if (hex.Length == 0)
            {
                return new byte[0];
            }
            int l = hex.Length / 2;
            var b = new byte[l];
            for (int i = 0; i < l; ++i)
            {
                b[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return b;
        }
    }
    public unsafe class BitConverter
    {
        public static ulong ToUInt64(byte[] buffer, int offset)
        {
            if (offset > buffer.Length - 8) return 0;
            fixed (byte* Buffer = buffer)
            {
                return *((ulong*)(Buffer + offset));
            }
        }
        public static Boolean ToBoolean(byte[] buffer, int offset)
        {
            if (offset > buffer.Length - 4) return false;
            fixed (byte* Buffer = buffer)
            {
                return *((Boolean*)(Buffer + offset));
            }
        }
        public static uint ToUInt32(byte[] buffer, int offset)
        {
            if (offset > buffer.Length - 4) return 0;
            fixed (byte* Buffer = buffer)
            {
                return *((uint*)(Buffer + offset));
            }
        }
        public static int ToInt32(byte[] buffer, int offset)
        {
            if (offset > buffer.Length - 4) return 0;
            fixed (byte* Buffer = buffer)
            {
                return *((int*)(Buffer + offset));
            }
        }
        public static ushort ToUInt16(byte[] buffer, int offset)
        {
            if (offset > buffer.Length - 2) return 0;
            fixed (byte* Buffer = buffer)
            {
                return *((ushort*)(Buffer + offset));
            }
        }
        public static short ToInt16(byte[] buffer, int offset)
        {
            if (offset > buffer.Length - 2) return 0;
            fixed (byte* Buffer = buffer)
            {
                return *((short*)(Buffer + offset));
            }
        }

        public static ulong ReadUlong(byte[] buffer, int offset)
        {
            fixed (byte* Buffer = buffer)
            {
                return *((ulong*)(Buffer + offset));
            }
        }
        public static uint ReadUint(byte[] buffer, int offset)
        {
            fixed (byte* Buffer = buffer)
            {
                return *((uint*)(Buffer + offset));
            }
        }
        public static ushort ReadUshort(byte[] buffer, int offset)
        {
            fixed (byte* Buffer = buffer)
            {
                return *((ushort*)(Buffer + offset));
            }
        }
    }
}
