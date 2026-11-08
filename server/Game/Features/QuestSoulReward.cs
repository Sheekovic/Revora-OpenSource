using System;
using System.Linq;
using Revora.Database;
using Revora.Network.GamePackets;
namespace Revora.Game.Features {
    internal static class QuestSoulReward {
        internal static ConquerItem Create() {
            var pool=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID>=800000 && i.ID<900000 && i.PurificationLevel==7).Select(i=>i.ID).OrderBy(i=>i).ToArray();
            if(pool.Length==0)throw new InvalidOperationException("Quest P7 soul catalog missing.");
            return QuestInventory.New(pool[Kernel.Random.Next(pool.Length)],true);
        }
    }
}
