using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    internal static class GuildCupStats
    {
        internal static bool Apply(Entity e,ConquerItem item,ConquerItemBaseInformation info) {
            if(item.ID!=2100055 && item.ID!=2100065 && item.ID!=2100075 && item.ID!=2100085 && item.ID!=2100095)return false;
            // Active-set selection happens before this call. Cups contribute
            // their installed catalog values exactly once, including magic attack.
            if(item.Position!=ConquerItem.Bottle && item.Position!=ConquerItem.AlternateBottle)return true;
            e.ItemHP+=info.ItemHP;e.ItemMP+=info.ItemMP;
            e.BaseMinAttack+=info.MinAttack;e.BaseMaxAttack+=info.MaxAttack;e.BaseMagicAttack+=info.MagicAttack;
            e.Defence+=info.PhysicalDefence;
            e.CriticalStrike+=info.CriticalStrike;e.SkillCStrike+=info.SkillCStrike;e.Immunity+=info.Immunity;
            e.Breaktrough+=info.BreakThrough;e.Counteraction+=info.CounterAction;
            return true;
        }
    }
}
