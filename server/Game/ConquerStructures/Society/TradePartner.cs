// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revora.Game.ConquerStructures.Society
{
    public unsafe class TradePartner : Interfaces.IKnownPerson
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

        public bool StillOnProbation
        {
            get
            {
                return Game.Features.TradePartnerships.RemainingMinutes(ProbationStartedOn) > 0;
            }
        }
        public DateTime ProbationStartedOn
        {
            get;
            set;
        }
    }
}