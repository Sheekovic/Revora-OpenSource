using System;
using Revora.Client;
namespace Revora
{
    internal static class ChiBonuses
    {
        // Published endpoints are exact; intermediate ranks interpolate downward
        // over eleven intervals. See docs/CULTIVATION_COMBAT.md.
        internal static uint TierBonus(int rank, uint top, uint bottom)
        {
            return top - (uint)(Math.Max(4, Math.Min(15, rank)) - 4) * (top - bottom) / 11;
        }

        internal static void Apply(GameState client,uint percentage)
        {
            var Entity = client.Entity;
            var ChiPowers = client.ChiPowers;
            var ChiData = client.ChiData;
            if (ChiPowers == null || ChiData == null) return;
            bool skill=Game.Features.Tournaments.LocalTeamPkCombat.SkillStats(client);
            foreach (var chiPower in ChiPowers)
            {
                foreach (var attribute in chiPower.Attributes)
                {
                    // Clamp the effective contribution, never the persisted roll.
                    uint value=attribute.Value;
                    if(skill)value=Math.Min(value,(uint)Math.Max(0,Game.Enums.ChiMaxValues(attribute.Type))*60/100);
                    switch (attribute.Type)
                    {
                        case Game.Enums.ChiAttribute.CriticalStrike:
                            Entity.CriticalStrike += (int)((ushort)(value * 10) * percentage / 100);
                            break;
                        case Game.Enums.ChiAttribute.Counteraction:
                            Entity.Counteraction += (ushort)(value * percentage / 100);
                            break;
                        case Game.Enums.ChiAttribute.AddAttack:
                            Entity.BaseMinAttack += value * percentage / 100;
                            Entity.BaseMaxAttack += value * percentage / 100;
                            break;
                        case Game.Enums.ChiAttribute.AddMagicAttack:
                            Entity.BaseMagicAttack += value * percentage / 100;
                            break;
                        case Game.Enums.ChiAttribute.AddMagicDefense:
                            Entity.BaseMagicDefence += value * percentage / 100;
                            break;
                        case Game.Enums.ChiAttribute.Breakthrough:
                            Entity.Breaktrough += (ushort)(value * percentage / 100);
                            break;
                        case Game.Enums.ChiAttribute.HPAdd:
                            Entity.ItemHP += value * percentage / 100;
                            break;
                        case Game.Enums.ChiAttribute.Immunity:
                            Entity.Immunity += (int)((ushort)(value * 10) * percentage / 100);
                            break;
                        case Game.Enums.ChiAttribute.MagicDamageDecrease:
                            Entity.MagicDamageDecrease += (ushort)(value * percentage / 100);
                            break;
                        case Game.Enums.ChiAttribute.MagicDamageIncrease:
                            Entity.MagicDamageIncrease += (ushort)(value * percentage / 100);
                            break;
                        case Game.Enums.ChiAttribute.PhysicalDamageDecrease:
                            Entity.PhysicalDamageDecrease += (ushort)(value * percentage / 100);
                            break;
                        case Game.Enums.ChiAttribute.PhysicalDamageIncrease:
                            Entity.PhysicalDamageIncrease += (ushort)(value * percentage / 100);
                            break;
                        case Game.Enums.ChiAttribute.SkillCriticalStrike:
                            Entity.SkillCStrike += (int)((ushort)(value * 10) * percentage / 100);
                            break;
                    }
                }
            }
            #region Dragon Ranking
            if ((ChiData.DragonRank >= 1 && ChiData.DragonRank <= 50 || ChiData.DragonPoints >= 400) && ChiPowers.Count >= 1)
            {
                if (ChiData.DragonRank >= 1 && ChiData.DragonRank <= 3 || ChiData.DragonPoints >= 400)
                {
                    Entity.ItemHP += 5000;
                    Entity.BaseMagicDefence += 300;
                    Entity.PhysicalDamageDecrease += 1000;
                    Entity.MagicDamageDecrease += 300;
                }
                else if (ChiData.DragonRank <= 15)
                {
                    Entity.ItemHP += (uint)TierBonus(ChiData.DragonRank, 3000, 2000);
                    Entity.BaseMagicDefence += (uint)TierBonus(ChiData.DragonRank, 250, 150);
                    Entity.PhysicalDamageDecrease += (ushort)TierBonus(ChiData.DragonRank, 600, 400);
                    Entity.MagicDamageDecrease += (ushort)TierBonus(ChiData.DragonRank, 200, 150);
                }
                else if (ChiData.DragonRank <= 50)
                {
                    Entity.ItemHP += 1500;
                    Entity.BaseMagicDefence += 100;
                    Entity.PhysicalDamageDecrease += 300;
                    Entity.MagicDamageDecrease += 100;
                }
            }
            #endregion
            #region Phoenix Ranking
            if ((ChiData.PhoenixRank >= 1 && ChiData.PhoenixRank <= 50 || ChiData.PhoenixPoints >= 400) && ChiPowers.Count >= 2)
            {
                if (ChiData.PhoenixRank >= 1 && ChiData.PhoenixRank <= 3 || ChiData.PhoenixPoints >= 400)
                {
                    Entity.BaseMinAttack += 3000;
                    Entity.BaseMaxAttack += 3000;
                    Entity.BaseMagicAttack += 3000;
                    Entity.PhysicalDamageIncrease += 1000;
                    Entity.MagicDamageIncrease += 300;
                }
                else if (ChiData.PhoenixRank <= 15)
                {
                    Entity.BaseMinAttack += (uint)TierBonus(ChiData.PhoenixRank, 2000, 1500);
                    Entity.BaseMaxAttack += (uint)TierBonus(ChiData.PhoenixRank, 2000, 1500);
                    Entity.BaseMagicAttack += (uint)TierBonus(ChiData.PhoenixRank, 2000, 1500);
                    Entity.PhysicalDamageIncrease += (ushort)TierBonus(ChiData.PhoenixRank, 600, 400);
                    Entity.MagicDamageIncrease += (ushort)TierBonus(ChiData.PhoenixRank, 200, 150);
                }
                else if (ChiData.PhoenixRank <= 50)
                {
                    Entity.BaseMinAttack += 1000;
                    Entity.BaseMaxAttack += 1000;
                    Entity.BaseMagicAttack += 1000;
                    Entity.PhysicalDamageIncrease += 300;
                    Entity.MagicDamageIncrease += 100;
                }
            }
            #endregion
            #region Tiger Ranking
            if ((ChiData.TigerRank >= 1 && ChiData.TigerRank <= 50 || ChiData.TigerPoints >= 400) && ChiPowers.Count >= 3)
            {
                if (ChiData.TigerRank >= 1 && ChiData.TigerRank <= 3 || ChiData.TigerPoints >= 400)
                {
                    Entity.CriticalStrike += 1500;
                    Entity.SkillCStrike += 1500;
                    Entity.Immunity += 800;
                }
                else if (ChiData.TigerRank <= 15)
                {
                    Entity.CriticalStrike += (ushort)TierBonus(ChiData.TigerRank, 1100, 800);
                    Entity.SkillCStrike += (ushort)TierBonus(ChiData.TigerRank, 1100, 800);
                    Entity.Immunity += 500;
                }
                else if (ChiData.TigerRank <= 50)
                {
                    Entity.CriticalStrike += 500;
                    Entity.SkillCStrike += 500;
                    Entity.Immunity += 200;
                }
            }
            #endregion
            #region Turtle Ranking
            if ((ChiData.TurtleRank >= 1 && ChiData.TurtleRank <= 50 || ChiData.TurtlePoints >= 400) && ChiPowers.Count >= 4)
            {
                if (ChiData.TurtleRank >= 1 && ChiData.TurtleRank <= 3 || ChiData.TurtlePoints >= 400)
                {
                    Entity.Breaktrough += 150;
                    Entity.Counteraction += 150;
                    Entity.Immunity += 800;
                }
                else if (ChiData.TurtleRank <= 15)
                {
                    Entity.Breaktrough += (ushort)TierBonus(ChiData.TurtleRank, 110, 80);
                    Entity.Counteraction += (ushort)TierBonus(ChiData.TurtleRank, 110, 80);
                    Entity.Immunity += 500;
                }
                else if (ChiData.TurtleRank <= 50)
                {
                    Entity.Breaktrough += 50;
                    Entity.Counteraction += 50;
                    Entity.Immunity += 200;
                }
            }
            #endregion
        }
    }
}
