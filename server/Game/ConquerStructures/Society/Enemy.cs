// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace Revora.Game.ConquerStructures.Society
{
    public unsafe class Enemy : Interfaces.IKnownPerson
    {
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
            get { return Kernel.GamePool.ContainsKey(ID); }
        }
        public Game.ConquerStructures.NobilityRank NobilityRank
        {
            get
            {
                if (Kernel.GamePool.ContainsKey(ID))
                    return Kernel.GamePool[ID].Entity.NobilityRank;
                else return 0;
            }
        }
        public bool IsBoy
        {
            get
            {
                if (Kernel.GamePool.ContainsKey(ID))
                    return Game.Features.Flowers.IsBoy(Kernel.GamePool[ID].Entity.Body);
                else return false;
            }
        }
        public Client.GameState Client
        {
            get { return Kernel.GamePool[ID]; }
        }
    }
}