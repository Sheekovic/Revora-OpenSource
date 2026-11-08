
using System;

namespace Revora
{
    public unsafe class Cargando
    {
        private static Int32 Next = -1;
        private static String[] Array = new String[] { "|", "/", "-", "\\" };
        public static String NextChar()
        {
            Next++;
            if (Next > 3)
                Next = 0;
            return Array[Next];
        }
    }
}