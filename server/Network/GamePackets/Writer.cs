using System;
using System.Text;
using System.Collections.Generic;

namespace Revora.Network
{
    public class Writer
    {
        public static void WriteStringWithLengthX(string arg, int offset, byte[] buffer)
        {
            if (buffer == null) return;
            if (offset > buffer.Length - 1) return;
            int till = buffer.Length - offset;
            till = Math.Min(arg.Length, till);
            WriteUInt16((ushort)arg.Length, offset, buffer);
            offset += 2; ;
            byte[] argEncoded = Server.Encoding.GetBytes(arg);
            Array.Copy(argEncoded, 0, buffer, offset, till);
        }
        public static void WriteStringWithLength(string arg, int len, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            int till = len;
            till = Math.Min(arg.Length, till);
            buffer[offset] = (byte)till;
            offset++;
            byte[] argEncoded = Server.Encoding.GetBytes(arg);
            Array.Copy(argEncoded, 0, buffer, offset, till);
        }
        public static void WriteStringWithLength(string arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            int till = buffer.Length - offset;
            till = Math.Min(arg.Length, till);
            buffer[offset] = (byte)arg.Length;
            offset++;
            byte[] argEncoded = Server.Encoding.GetBytes(arg);
            Array.Copy(argEncoded, 0, buffer, offset, till);
        }
        public static void WriteString(string arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            byte[] argEncoded = Server.Encoding.GetBytes(arg);
            if (buffer.Length >= offset + arg.Length)
                Array.Copy(argEncoded, 0, buffer, offset, arg.Length);
        }

        /* public static void WriteStringWithLength(string arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            int till = buffer.Length - offset;
            till = Math.Min(arg.Length, till);
            buffer[offset] = (byte)arg.Length;
            offset++;
            ushort i = 0;
            while (i < till)
            {
                buffer[(ushort)(i + offset)] = (byte)arg[i];
                i = (ushort)(i + 1);
            }
        }        
        public static void WriteString(string arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            if (buffer.Length >= offset + arg.Length)
            {
                unsafe
                {
#if UNSAFE
                    fixed (byte* Buffer = buffer)
                    {
                        ushort i = 0;
                        while (i < arg.Length)
                        {
                            *((byte*)(Buffer + offset + i)) = (byte)arg[i];
                            i++;
                        }
                    }
#else
                    ushort i = 0;
                    while (i < arg.Length)
                    {
                        buffer[(ushort)(i + offset)] = (byte)arg[i];
                        i = (ushort)(i + 1);
                    }
#endif
                }
            }
        }*/

        public static void WriteByte(byte arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            buffer[offset] = arg;
        }
        public static void WriteBoolean(bool arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            WriteByte(arg == true ? (byte)1 : (byte)0, offset, buffer);
        }
        public static void WriteUInt16(ushort arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            if (buffer.Length >= offset + sizeof(ushort))
            {
                unsafe
                {
#if UNSAFE
                    fixed (byte* Buffer = buffer)
                        *((ushort*)(Buffer + offset)) = arg;
#else
                    buffer[offset] = (byte)arg;
                    buffer[offset + 1] = (byte)(arg >> 8);
#endif
                }
            }
        }
        public static void WriteUInt32(uint arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            if (buffer.Length >= offset + sizeof(uint))
            {
                unsafe
                {
#if UNSAFE
                    fixed (byte* Buffer = buffer)
                        *((uint*)(Buffer + offset)) = arg;
#else
                    buffer[offset] = (byte)arg;
                    buffer[offset + 1] = (byte)(arg >> 8);
                    buffer[offset + 2] = (byte)(arg >> 16);
                    buffer[offset + 3] = (byte)(arg >> 24);
#endif
                }
            }
        }
        public static unsafe void WriteUInt128(decimal arg, int offset, byte[] Buffer)
        {
            try
            {
                fixed (byte* buffer = Buffer)
                    *((decimal*)(buffer + offset)) = arg;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        public static void WriteInt32(int arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            if (buffer.Length >= offset + sizeof(uint))
            {
                unsafe
                {
#if UNSAFE
                    fixed (byte* Buffer = buffer)
                        *((int*)(Buffer + offset)) = arg;
#else
                    buffer[offset] = (byte)(arg);
                    buffer[offset + 1] = (byte)(arg >> 8);
                    buffer[offset + 2] = (byte)(arg >> 16);
                    buffer[offset + 3] = (byte)(arg >> 24);
#endif
                }
            }
        }
        public static void WriteUInt64(ulong arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            if (buffer.Length >= offset + sizeof(ulong))
            {
                unsafe
                {
#if UNSAFE
                    fixed (byte* Buffer = buffer)
                        *((ulong*)(Buffer + offset)) = arg;
#else
                    buffer[offset] = (byte)(arg);
                    buffer[offset + 1] = (byte)(arg >> 8);
                    buffer[offset + 2] = (byte)(arg >> 16);
                    buffer[offset + 3] = (byte)(arg >> 24);
                    buffer[offset + 4] = (byte)(arg >> 32);
                    buffer[offset + 5] = (byte)(arg >> 40);
                    buffer[offset + 6] = (byte)(arg >> 48);
                    buffer[offset + 7] = (byte)(arg >> 56);
#endif
                }
            }
        }
        public static void WriteStringList(List<string> arg, int offset, byte[] buffer)
        {
            if (arg == null)
                return;
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            buffer[offset] = (byte)arg.Count;
            offset++;
            foreach (string str in arg)
            {
                buffer[offset] = (byte)str.Length;
                WriteString(str, offset + 1, buffer);
                offset += str.Length + 1;
            }
        }
        public static void WriteStringList(string[] arg, int offset, byte[] buffer)
        {
            if (arg == null)
                return;
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            buffer[offset] = (byte)arg.Length;
            offset++;
            foreach (string str in arg)
            {
                buffer[offset] = (byte)str.Length;
                WriteString(str, offset + 1, buffer);
                offset += str.Length + 1;
            }
        }

        /* public static void String(string arg, int offset, byte[] buffer)
         {
             if (buffer == null)
                 return;
             if (offset > buffer.Length - 1)
                 return;
             byte[] argEncoded = Program.Encoding.GetBytes(arg);
             if (buffer.Length >= offset + arg.Length)
                 Array.Copy(argEncoded, 0, buffer, offset, arg.Length);
         }*/

        /*  public static void String(string arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            if (buffer.Length >= offset + arg.Length)
            {
                unsafe
                {
#if UNSAFE
                    fixed (byte* Buffer = buffer)
                    {
                        ushort i = 0;
                        while (i < arg.Length)
                        {
                            *((byte*)(Buffer + offset + i)) = (byte)arg[i];
                            i++;
                        }
                    }
#else
                    ushort i = 0;
                    while (i < arg.Length)
                    {
                        buffer[(ushort)(i + offset)] = (byte)arg[i];
                        i = (ushort)(i + 1);
                    }
#endif
                }
            }
        }*/
        public static void Byte(byte arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            buffer[offset] = arg;
        }
        public static void Boolean(bool arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            WriteByte(arg == true ? (byte)1 : (byte)0, offset, buffer);
        }
        public static void Ushort(ushort arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            if (buffer.Length >= offset + sizeof(ushort))
            {
                unsafe
                {
#if UNSAFE
                    fixed (byte* Buffer = buffer)
                    {
                        *((ushort*)(Buffer + offset)) = arg;
                    }
#else
                    buffer[offset] = (byte)arg;
                    buffer[offset + 1] = (byte)(arg >> 8);
#endif
                }
            }
        }
        public static void Uint(uint arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            if (buffer.Length >= offset + sizeof(uint))
            {
                unsafe
                {
#if UNSAFE
                    fixed (byte* Buffer = buffer)
                    {
                        *((uint*)(Buffer + offset)) = arg;
                    }
#else
                    buffer[offset] = (byte)arg;
                    buffer[offset + 1] = (byte)(arg >> 8);
                    buffer[offset + 2] = (byte)(arg >> 16);
                    buffer[offset + 3] = (byte)(arg >> 24);
#endif
                }
            }
        }
        public static unsafe void Decimal(decimal arg, int offset, byte[] Buffer)
        {
            try
            {
                fixed (byte* buffer = Buffer)
                {
                    if (arg.GetType() == typeof(decimal))
                    {
                        *((decimal*)(buffer + offset)) = arg;
                    }
                }
            }
            catch (Exception e)
            {
                Revora.Console.WriteLine(e);
            }
        }
        public static void Int(int arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            if (buffer.Length >= offset + sizeof(uint))
            {
                unsafe
                {
#if UNSAFE
                    fixed (byte* Buffer = buffer)
                    {
                        *((int*)(Buffer + offset)) = arg;
                    }
#else
                    buffer[offset] = (byte)(arg);
                    buffer[offset + 1] = (byte)(arg >> 8);
                    buffer[offset + 2] = (byte)(arg >> 16);
                    buffer[offset + 3] = (byte)(arg >> 24);
#endif
                }
            }
        }
        public static void Ulong(ulong arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            if (buffer.Length >= offset + sizeof(ulong))
            {
                unsafe
                {
#if UNSAFE
                    fixed (byte* Buffer = buffer)
                    {
                        *((ulong*)(Buffer + offset)) = arg;
                    }
#else
                    buffer[offset] = (byte)(arg);
                    buffer[offset + 1] = (byte)(arg >> 8);
                    buffer[offset + 2] = (byte)(arg >> 16);
                    buffer[offset + 3] = (byte)(arg >> 24);
                    buffer[offset + 4] = (byte)(arg >> 32);
                    buffer[offset + 5] = (byte)(arg >> 40);
                    buffer[offset + 6] = (byte)(arg >> 48);
                    buffer[offset + 7] = (byte)(arg >> 56);
#endif
                }
            }
        }

        public static void WriteUshort(ushort arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            if (buffer.Length >= offset + sizeof(ushort))
            {
                unsafe
                {
#if UNSAFE
                    fixed (byte* Buffer = buffer)
                    {
                        *((ushort*)(Buffer + offset)) = arg;
                    }
#else
                    buffer[offset] = (byte)arg;
                    buffer[offset + 1] = (byte)(arg >> 8);
#endif
                }
            }
        }
        public static void WriteUint(uint arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            if (buffer.Length >= offset + sizeof(uint))
            {
                unsafe
                {
#if UNSAFE
                    fixed (byte* Buffer = buffer)
                    {
                        *((uint*)(Buffer + offset)) = arg;
                    }
#else
                    buffer[offset] = (byte)arg;
                    buffer[offset + 1] = (byte)(arg >> 8);
                    buffer[offset + 2] = (byte)(arg >> 16);
                    buffer[offset + 3] = (byte)(arg >> 24);
#endif
                }
            }
        }
        public static void WriteUlong(ulong arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            if (buffer.Length >= offset + sizeof(ulong))
            {
                unsafe
                {
#if UNSAFE
                    fixed (byte* Buffer = buffer)
                    {
                        *((ulong*)(Buffer + offset)) = arg;
                    }
#else
                    buffer[offset] = (byte)(arg);
                    buffer[offset + 1] = (byte)(arg >> 8);
                    buffer[offset + 2] = (byte)(arg >> 16);
                    buffer[offset + 3] = (byte)(arg >> 24);
                    buffer[offset + 4] = (byte)(arg >> 32);
                    buffer[offset + 5] = (byte)(arg >> 40);
                    buffer[offset + 6] = (byte)(arg >> 48);
                    buffer[offset + 7] = (byte)(arg >> 56);
#endif
                }
            }
        }

        #region New Write
        public static void Write(int arg, int offset, byte[] buffer)
        {
            if (buffer == null)
            {
                return;
            }
            if (offset > buffer.Length - 1)
            {
                return;
            }
            if (buffer.Length >= offset + sizeof(uint))
            {
                unsafe
                {
#if UNSAFE
                    fixed (byte* Buffer = buffer)
                    {
                        *((int*)(Buffer + offset)) = arg;
                    }
#else
                    buffer[offset] = (byte)(arg);
                    buffer[offset + 1] = (byte)(arg >> 8);
                    buffer[offset + 2] = (byte)(arg >> 16);
                    buffer[offset + 3] = (byte)(arg >> 24);
#endif
                }
            }
        }
        public static void Write(string arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            byte[] argEncoded = Server.Encoding.GetBytes(arg);
            if (buffer.Length >= offset + arg.Length)
                Array.Copy(argEncoded, 0, buffer, offset, arg.Length);
        }
        public static void Write(byte arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            buffer[offset] = arg;
        }
        public static void Write(bool arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            WriteByte(arg == true ? (byte)1 : (byte)0, offset, buffer);
        }
        public static void Write(ushort arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            if (buffer.Length >= offset + sizeof(ushort))
            {
                unsafe
                {
#if UNSAFE
                    fixed (byte* Buffer = buffer)
                    {
                        *((ushort*)(Buffer + offset)) = arg;
                    }
#else
                    buffer[offset] = (byte)arg;
                    buffer[offset + 1] = (byte)(arg >> 8);
#endif
                }
            }
        }
        public static void Write(uint arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            if (buffer.Length >= offset + sizeof(uint))
            {
                unsafe
                {
#if UNSAFE
                    fixed (byte* Buffer = buffer)
                    {
                        *((uint*)(Buffer + offset)) = arg;
                    }
#else
                    buffer[offset] = (byte)arg;
                    buffer[offset + 1] = (byte)(arg >> 8);
                    buffer[offset + 2] = (byte)(arg >> 16);
                    buffer[offset + 3] = (byte)(arg >> 24);
#endif
                }
            }
        }
        public static void Write(ulong arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            if (buffer.Length >= offset + sizeof(ulong))
            {
                unsafe
                {
#if UNSAFE
                    fixed (byte* Buffer = buffer)
                    {
                        *((ulong*)(Buffer + offset)) = arg;
                    }
#else
                    buffer[offset] = (byte)(arg);
                    buffer[offset + 1] = (byte)(arg >> 8);
                    buffer[offset + 2] = (byte)(arg >> 16);
                    buffer[offset + 3] = (byte)(arg >> 24);
                    buffer[offset + 4] = (byte)(arg >> 32);
                    buffer[offset + 5] = (byte)(arg >> 40);
                    buffer[offset + 6] = (byte)(arg >> 48);
                    buffer[offset + 7] = (byte)(arg >> 56);
#endif
                }
            }
        }
        #endregion
        public static void Sombra(ushort arg, int offset, byte[] buffer)
        {
            if (buffer == null)
                return;
            if (offset > buffer.Length - 1)
                return;
            if (buffer.Length >= offset + sizeof(ushort))
            {
                unsafe
                {
#if UNSAFE
                    fixed (byte* Buffer = buffer)
                    {
                        *((ushort*)(Buffer + offset)) = arg;
                    }
#else
                    buffer[offset] = (byte)arg;
                    buffer[offset + 1] = (byte)(arg >> 8);
#endif
                }
            }
        }
    }
}