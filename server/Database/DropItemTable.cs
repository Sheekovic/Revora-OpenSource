// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System.Collections.Generic;

namespace Revora.Database
{
    public unsafe class DropItemTable
    {
        public struct Type
        {
            public uint Item;
            public int Rate;
        }
        public static List<Type> DropItem = new List<Type>()
            {
                new Type() { Item = 3001133 , Rate = 5 },
                new Type() { Item = 1088000 , Rate = 15 },
                new Type() { Item = 3001136 , Rate = 3 },
                new Type() { Item = 3000051 , Rate = 3 },
                new Type() { Item = 3001134 , Rate = 3 },
                new Type() { Item = 3001135 , Rate = 3 },
                new Type() { Item = 1088001 , Rate = 15 },
                new Type() { Item = 720657 , Rate = 10 },
                new Type() { Item = 720656 , Rate = 10 },
                new Type() { Item = 720655 , Rate = 7 },
                new Type() { Item = 720658 , Rate = 3 },
                new Type() { Item = 720663 , Rate = 3 },
                new Type() { Item = 720659 , Rate = 3 },
                new Type() { Item = 720665 , Rate = 3 },
                new Type() { Item = 720675 , Rate = 3 },
                new Type() { Item = 720653 , Rate = 3 },
                new Type() { Item = 720681 , Rate = 3 },
                new Type() { Item = 720676 , Rate = 3 },
                //new Type() { Item = 720654 , Rate = 3 },
                new Type() { Item = 720666 , Rate = 3 },
                new Type() { Item = 720662 , Rate = 3 },
                new Type() { Item = 729476 , Rate = 15 },
                new Type() { Item = 729477 , Rate = 15 },
                new Type() { Item = 729478 , Rate = 15 },
                new Type() { Item = 723341 , Rate = 20 },
                new Type() { Item = 723340 , Rate = 15 }
            };
    }
}