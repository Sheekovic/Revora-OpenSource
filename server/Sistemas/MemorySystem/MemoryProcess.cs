using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Revora
{
    public class MemoryProcess
    {
        internal class Native
        {
            [DllImport("kernel32")]
            public static extern IntPtr OpenProcess(UInt32 dwAccess, Int32 dwDesiredAccess, UInt32 dwPID);

            [DllImport("psapi")]
            public static extern Int32 EmptyWorkingSet(IntPtr dwObject);

            [DllImport("kernel32")]
            public static extern Int32 CloseHandle(IntPtr dwObject);

            internal static unsafe void memcpy(byte* buffer, byte* ptr, int p)
            {
                throw new NotImplementedException();
            }

            internal static void memcpy()
            {
                throw new NotImplementedException();
            }
        }
        private uint m_ProcessId = 0;
        public MemoryProcess()
        {
            m_ProcessId = (uint)Process.GetCurrentProcess().Id;
        }
        public void Optimize()
        {
            if (m_ProcessId != 0)
            {
                IntPtr Handle = Native.OpenProcess((uint)0x1F0FFF, 1, m_ProcessId);
                Native.EmptyWorkingSet(Handle);
                Native.CloseHandle(Handle);
            }
            else
                throw new Exception("MeomoryCompressor::Optimize() -> The process Id can't be equal to zero!");
        }

        public void Close()
        {
            m_ProcessId = 0;
        }
    }
}