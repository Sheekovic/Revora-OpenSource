// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revora.Game.ConquerStructures.Society
{

    public unsafe class Mentor : Interfaces.IKnownPerson
    {
        public ConcurrentDictionary<uint, Mentor> Mentors;
        public uint ID
        {
            get;
            set;
        }
        public string Name
        {
            get;
            set;
        }
        public bool IsOnline
        {
            get
            {
                return Kernel.GamePool.ContainsKey(ID);
            }
        }
        public Client.GameState Client
        {
            get
            {
                return Kernel.GamePool[ID];
            }
        }
        public uint EnroleDate
        {
            get;
            set;
        }
    }
}
