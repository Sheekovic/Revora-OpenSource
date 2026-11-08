

using System;
using System.Drawing;
using System.Collections.Generic;
using CO2_CORE_DLL;

namespace COServer
{
    public partial class MyMath
    {
         
        public const Int32 NORMAL_RANGE = 17;
        public const Int32 BIG_RANGE = 34;
        public const Int32 USERDROP_RANGE = 9;

        public static Boolean Success(Double Chance)
        {
            return ((Double)Generate(1, 1000000)) / 10000 >= 100 - Chance;
        }

        public static Int32 Generate(Int32 Min, Int32 Max)
        {
            if (Max != Int32.MaxValue)
                Max++;

            Int32 Value = 0;
            Value = Kernel.Random.Next(Min, Max);
            return Value;
        }

        public static Int32 Generate(UInt32 Min, UInt32 Max)
        {
            if (Min > 0 && Max > 0)
            {
                if (Max != Int32.MaxValue)
                    Max++;

                Int32 Value = 0;
                Value = Kernel.Random.Next((int)Min, (int)Max); 
                return Value;
            }
            else
                return 1;
            
        }

    }
}
